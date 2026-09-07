using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Verse;

namespace NvidiaGpuMonitor
{
    /// <summary>
    /// Optional, standalone LM Studio awareness. Talks to an LM Studio (or any
    /// OpenAI-compatible) endpoint directly over HTTP — no dependency on RimSynapse,
    /// RimTalk, or any other mod. Off by default; the user opts in and sets the
    /// endpoint in mod settings.
    ///
    /// When enabled and reachable, it reads the loaded model name from the server's
    /// <c>/v1/models</c> route and estimates the model's VRAM footprint from its
    /// parameter count. A remote host (endpoint that is not localhost) is detected so
    /// its VRAM is never attributed to the local GPU.
    ///
    /// Everything runs on a background thread and degrades quietly: any failure leaves
    /// <see cref="Reachable"/> false and the breakdown falls back to a pure-GPU view.
    /// </summary>
    internal static class LmStudioProbe
    {
        private static Thread _thread;
        private static volatile bool _shutdown;
        private static readonly object _lock = new object();

        /// <summary>Wall-clock gap between reachability checks.</summary>
        private const int PollIntervalMs = 5000;

        // ── Cached state (guarded by _lock) ──
        private static bool _reachable;
        private static bool _isRemote;
        private static string _modelName;
        private static float _estimatedVramMb;
        private static string _lastError;
        private static DateTime _lastUpdated = DateTime.MinValue;

        // ── Public accessors ──

        /// <summary>Whether the user has opted into LM Studio awareness.</summary>
        internal static bool Enabled => DevToolsMod.Instance?.Settings?.lmStudioEnabled ?? false;

        /// <summary>The configured endpoint, e.g. "http://localhost:1234".</summary>
        internal static string Endpoint =>
            DevToolsMod.Instance?.Settings?.lmStudioEndpoint ?? "http://127.0.0.1:1234";

        /// <summary>True when the endpoint answered on the last check.</summary>
        internal static bool Reachable { get { lock (_lock) return _reachable; } }

        /// <summary>True when the configured endpoint is a remote host (not localhost),
        /// so its model runs on another machine's GPU and must not count as local VRAM.</summary>
        internal static bool IsRemote { get { lock (_lock) return _isRemote; } }

        /// <summary>Loaded model name reported by the server, or null.</summary>
        internal static string ModelName { get { lock (_lock) return _modelName; } }

        /// <summary>Estimated total VRAM (MB) for the loaded model. Zero when disabled,
        /// remote, unreachable, or the size can't be parsed.</summary>
        internal static float EstimatedVramMb { get { lock (_lock) return _estimatedVramMb; } }

        internal static string LastError { get { lock (_lock) return _lastError; } }
        internal static DateTime LastUpdated { get { lock (_lock) return _lastUpdated; } }

        /// <summary>Start the background probe. Safe to call once at mod load.</summary>
        internal static void Start()
        {
            if (_thread != null) return;
            _shutdown = false;
            _thread = new Thread(PollLoop)
            {
                IsBackground = true,
                Name = "NvidiaGpuMonitor-LmStudioProbe",
            };
            _thread.Start();
        }

        internal static void Shutdown() => _shutdown = true;

        private static void PollLoop()
        {
            while (!_shutdown)
            {
                try
                {
                    if (Enabled)
                        Sample();
                    else
                        Clear();
                }
                catch (Exception ex)
                {
                    lock (_lock) { _lastError = ex.Message; _reachable = false; }
                }

                Thread.Sleep(PollIntervalMs);
            }
        }

        /// <summary>Force an immediate sample (used by the debug action). Returns Reachable.</summary>
        internal static bool ForceSample()
        {
            if (!Enabled) { Clear(); return false; }
            Sample();
            return Reachable;
        }

        private static void Clear()
        {
            lock (_lock)
            {
                _reachable = false;
                _isRemote = false;
                _modelName = null;
                _estimatedVramMb = 0f;
                _lastError = null;
            }
        }

        private static void Sample()
        {
            string endpoint = Endpoint;
            bool remote = IsRemoteEndpoint(endpoint);
            string model = QueryLoadedModel(endpoint, out string error);
            bool reachable = model != null;

            // Attribute VRAM only when the model actually runs on this GPU.
            float vramMb = 0f;
            if (reachable && !remote)
                vramMb = EstimateModelVramMb(model);

            lock (_lock)
            {
                _isRemote = remote;
                _reachable = reachable;
                _modelName = model;
                _estimatedVramMb = vramMb;
                _lastError = error;
                _lastUpdated = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// GET {endpoint}/v1/models and extract the first model id. Returns null on any
        /// failure. Uses a short timeout so the background loop never hangs.
        ///
        /// Windows gotcha: "localhost" frequently resolves to IPv6 (::1) first, but LM Studio
        /// binds to IPv4 (127.0.0.1) only, so a "localhost" endpoint fails to connect. When the
        /// configured host is "localhost" and the request fails, we transparently retry against
        /// 127.0.0.1 so the user doesn't have to know this.
        /// </summary>
        private static string QueryLoadedModel(string endpoint, out string error)
        {
            string model = TryFetchModel(endpoint, out error);
            if (model != null) return model;

            string ipv4 = SubstituteLocalhostForIpv4(endpoint);
            if (ipv4 != null)
            {
                string retryModel = TryFetchModel(ipv4, out string retryError);
                if (retryModel != null) { error = null; return retryModel; }
                // Keep the original error unless the retry produced a more specific one.
                if (string.IsNullOrEmpty(error)) error = retryError;
            }
            return null;
        }

        private static string TryFetchModel(string endpoint, out string error)
        {
            error = null;
            try
            {
                string url = endpoint.TrimEnd('/') + "/v1/models";
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = 1500;
                req.ReadWriteTimeout = 1500;
                req.Proxy = null; // skip system proxy resolution — faster, avoids hangs

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var stream = resp.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string body = reader.ReadToEnd();
                    return ExtractFirstModelId(body);
                }
            }
            catch (WebException wex)
            {
                error = wex.Status == WebExceptionStatus.ConnectFailure
                    ? "LM Studio endpoint not reachable."
                    : wex.Message;
                return null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// If the endpoint's host is "localhost", return the same endpoint with the host
        /// rewritten to 127.0.0.1 (forcing IPv4); otherwise null.
        /// </summary>
        private static string SubstituteLocalhostForIpv4(string endpoint)
        {
            try
            {
                var uri = new Uri(endpoint);
                if (!string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                    return null;
                var builder = new UriBuilder(uri) { Host = "127.0.0.1" };
                return builder.Uri.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Pull the first <c>"id":"..."</c> value out of an OpenAI-style /v1/models
        /// payload without a JSON library (Core used to bundle Newtonsoft; we don't).
        /// </summary>
        internal static string ExtractFirstModelId(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var m = Regex.Match(json, "\"id\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static bool IsRemoteEndpoint(string endpoint)
        {
            try
            {
                var uri = new Uri(endpoint);
                string host = uri.Host.ToLowerInvariant();
                return host != "localhost" && host != "127.0.0.1" && host != "::1" && host != "[::1]";
            }
            catch
            {
                // Unparseable endpoint — treat as local so we don't hide a real usage figure.
                return false;
            }
        }

        /// <summary>
        /// Estimate a model's VRAM footprint from its parameter count. Assumes a
        /// Q4_K_M-class quantization (~0.65 GB per billion params) plus ~0.5 GB overhead
        /// for the KV cache, context, and runtime.
        /// </summary>
        internal static float EstimateModelVramMb(string modelName)
        {
            float billionParams = ParseBillionParams(modelName);
            if (billionParams <= 0f) return 0f;
            float estimateGb = (billionParams * 0.65f) + 0.5f;
            return estimateGb * 1024f;
        }

        /// <summary>
        /// Parse billion-parameter count from a model name. Handles patterns like
        /// "12b", "70b", "3.8b", "0.5b", and MoE/expert notation ("e4b", "a4b"),
        /// with keyword fallbacks (mini/small/medium/large).
        /// </summary>
        internal static float ParseBillionParams(string modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return 0f;
            modelName = modelName.ToLowerInvariant();

            var match = Regex.Match(modelName, @"(?<![a-z])(\d+\.?\d*)b(?!\w)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out float val) && val > 0f)
                return val;

            var moeMatch = Regex.Match(modelName, @"[ea](\d+\.?\d*)b(?!\w)");
            if (moeMatch.Success && float.TryParse(moeMatch.Groups[1].Value, out float mval) && mval > 0f)
                return mval;

            if (modelName.Contains("mini")) return 3.8f;
            if (modelName.Contains("small")) return 7f;
            if (modelName.Contains("medium")) return 13f;
            if (modelName.Contains("large")) return 34f;

            return 0f;
        }
    }
}
