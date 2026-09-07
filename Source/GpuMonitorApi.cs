using System.Collections.Generic;

namespace NvidiaGpuMonitor
{
    /// <summary>
    /// Public receiver endpoint. Other mods report their <em>in-process</em> GPU-memory
    /// consumers here so the monitor can break them out of the RimWorld process line.
    ///
    /// This is the answer to a real limitation: a model another mod loads inside RimWorld's
    /// own process (e.g. Local TTS's Kokoro) is invisible to NVML per-process enumeration —
    /// its VRAM is counted against the RimWorld process. A self-report is the only way to
    /// attribute it, so this endpoint lets a mod hand us its footprint.
    ///
    /// Bound by reflection from the reporting mod, so there is NO hard dependency in either
    /// direction. The type name, method names, and signatures deliberately match the legacy
    /// RimSynapse Core GpuStats channel — <c>UpsertConsumer(string,string,float,bool)</c> and
    /// <c>RemoveConsumer(string)</c> — so an existing reflective reporter can add this type as a
    /// target with no shape change.
    /// </summary>
    public static class GpuMonitorApi
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, GpuConsumer> _consumers =
            new Dictionary<string, GpuConsumer>();

        /// <summary>
        /// Report or update a consumer's VRAM row, keyed by a stable <paramref name="id"/>
        /// (e.g. the reporting mod's packageId). A non-resident consumer (running on CPU
        /// rather than the GPU) reports 0 MB. Thread-safe; a null/empty id is ignored.
        /// </summary>
        public static void UpsertConsumer(string id, string label, float vramMb, bool resident)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (_lock)
            {
                if (!_consumers.TryGetValue(id, out var c))
                {
                    c = new GpuConsumer { id = id };
                    _consumers[id] = c;
                }
                c.label = string.IsNullOrEmpty(label) ? id : label;
                c.vramMb = resident && vramMb > 0f ? vramMb : 0f;
                c.resident = resident;
            }
        }

        /// <summary>Drop a consumer's row (call when the model is unloaded). Thread-safe.</summary>
        public static void RemoveConsumer(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            lock (_lock) { _consumers.Remove(id); }
        }

        /// <summary>Thread-safe snapshot of the rows that are actually resident on the GPU
        /// (non-resident/zero rows are skipped) — used by the VRAM breakdown.</summary>
        internal static List<GpuConsumer> ResidentSnapshot()
        {
            var result = new List<GpuConsumer>();
            lock (_lock)
            {
                foreach (var c in _consumers.Values)
                    if (c.resident && c.vramMb > 0f)
                        result.Add(c.Clone());
            }
            return result;
        }

        /// <summary>Thread-safe snapshot of every registered row (for diagnostics).</summary>
        internal static List<GpuConsumer> AllSnapshot()
        {
            lock (_lock)
            {
                var result = new List<GpuConsumer>(_consumers.Count);
                foreach (var c in _consumers.Values) result.Add(c.Clone());
                return result;
            }
        }
    }

    /// <summary>One reported in-process GPU-memory consumer.</summary>
    internal class GpuConsumer
    {
        public string id;
        public string label;
        public float vramMb;
        public bool resident;

        internal GpuConsumer Clone() =>
            new GpuConsumer { id = id, label = label, vramMb = vramMb, resident = resident };
    }
}
