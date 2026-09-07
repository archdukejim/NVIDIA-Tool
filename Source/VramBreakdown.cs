using System;
using System.Collections.Generic;
using UnityEngine;

namespace NvidiaGpuMonitor
{
    /// <summary>
    /// Estimates a per-application VRAM breakdown from measured totals.
    ///
    /// Sources, best first:
    ///   RimWorld / LM Studio → MEASURED per-process VRAM from NVML when the driver reports it
    ///                          (accurate; sums multiple loaded models automatically).
    ///   RimWorld (fallback)  → Unity's own Texture.currentTextureMemory + overhead.
    ///   LM Studio (fallback) → a rough name-based estimate from LmStudioProbe (off unless the
    ///                          user enables it). A remote host contributes nothing to local VRAM.
    ///   System               → Total VRAM used − RimWorld − LM Studio.
    /// </summary>
    internal static class VramBreakdown
    {
        private static float _rimworldMb;
        private static float _lmStudioMb;
        private static float _lmStudioVramMb;
        private static float _lmStudioRamMb;
        private static float _systemMb;
        private static bool _lmStudioIsRemote;
        private static bool _rimworldMeasured;
        private static bool _lmStudioMeasured;
        private static List<GpuConsumer> _consumers = new List<GpuConsumer>();
        private static float _consumersMb;
        private static DateTime _lastUpdate = DateTime.MinValue;
        private const float UpdateIntervalSec = 3f;

        // ── Public accessors ──

        internal static float RimWorldMb => _rimworldMb;
        /// <summary>Full LM Studio figure (VRAM-resident + any RAM-offloaded portion of an estimate).</summary>
        internal static float LmStudioMb => _lmStudioMb;
        /// <summary>LM Studio portion actually resident on this GPU. Zero for a remote host.</summary>
        internal static float LmStudioVramMb => _lmStudioVramMb;
        /// <summary>LM Studio portion estimated to be offloaded to system RAM (0 when measured).</summary>
        internal static float LmStudioRamMb => _lmStudioRamMb;
        internal static float SystemMb => _systemMb;
        /// <summary>True when the configured LM Studio endpoint is a remote host, so none of its VRAM is on this GPU.</summary>
        internal static bool LmStudioIsRemote => _lmStudioIsRemote;
        /// <summary>True when the RimWorld figure is a real per-process measurement (not the Unity estimate).</summary>
        internal static bool RimWorldMeasured => _rimworldMeasured;
        /// <summary>True when the LM Studio figure is a real per-process measurement (not a name-based estimate).</summary>
        internal static bool LmStudioMeasured => _lmStudioMeasured;
        /// <summary>Resident in-process consumers reported by other mods via <see cref="GpuMonitorApi"/>
        /// (e.g. Local TTS's Kokoro), carved out of the RimWorld line into their own rows.</summary>
        internal static List<GpuConsumer> Consumers => _consumers;
        /// <summary>Total VRAM (MB) attributed to reported in-process consumers.</summary>
        internal static float ConsumersMb => _consumersMb;

        /// <summary>
        /// Refresh the breakdown. Call from the overlay's OnGUI (throttled internally).
        /// </summary>
        internal static void Refresh()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastUpdate).TotalSeconds < UpdateIntervalSec) return;
            _lastUpdate = now;

            float totalUsedMb = NvidiaSmiReader.UsedVramMb;
            if (totalUsedMb <= 0f) return;

            // Sum measured per-process VRAM by role. NVML's _v3 process API reports this on
            // recent drivers; where it isn't available these stay 0 and we fall back to estimates.
            float measuredRw = 0f, measuredLm = 0f;
            var processes = NvidiaSmiReader.Processes;
            foreach (var p in processes)
            {
                if (p.VramMb <= 0f) continue;
                if (p.IsLmStudio) measuredLm += p.VramMb;
                else if (p.IsRimWorld) measuredRw += p.VramMb;
            }

            // 1. RimWorld — prefer the measured process figure, else Unity's texture tracking.
            if (measuredRw > 0f)
            {
                _rimworldMb = measuredRw;
                _rimworldMeasured = true;
            }
            else
            {
                _rimworldMb = GetRimWorldVramMb();
                _rimworldMeasured = false;
            }

            // 2. LM Studio.
            _lmStudioIsRemote = LmStudioProbe.Enabled && LmStudioProbe.IsRemote;

            if (measuredLm > 0f && !_lmStudioIsRemote)
            {
                // Measured: the process's resident VRAM is the real, exact number, and already
                // covers every model LM Studio has loaded. No offload guesswork needed.
                _lmStudioMeasured = true;
                _lmStudioMb = measuredLm;
                _lmStudioVramMb = measuredLm;
                _lmStudioRamMb = 0f;
            }
            else
            {
                // Fallback: rough name-based estimate (only when enabled and local).
                _lmStudioMeasured = false;
                _lmStudioMb = (LmStudioProbe.Enabled && !_lmStudioIsRemote) ? LmStudioProbe.EstimatedVramMb : 0f;

                // Split the estimate into VRAM vs offloaded RAM by what's physically possible.
                float maxAvailableForLms = totalUsedMb - _rimworldMb;
                if (maxAvailableForLms < 0f) maxAvailableForLms = 0f;
                float systemEstimate = Math.Min(1024f, maxAvailableForLms);
                float lmsVramLimit = maxAvailableForLms - systemEstimate;
                if (lmsVramLimit < 0f) lmsVramLimit = 0f;

                if (_lmStudioMb > lmsVramLimit)
                {
                    _lmStudioVramMb = lmsVramLimit;
                    _lmStudioRamMb = _lmStudioMb - lmsVramLimit;
                }
                else
                {
                    _lmStudioVramMb = _lmStudioMb;
                    _lmStudioRamMb = 0f;
                }
            }

            // 3. In-process consumers reported by other mods (e.g. Local TTS's Kokoro). These run
            //    inside RimWorld's own process, so NVML counts them against RimWorld — carve them
            //    into their own rows instead of hiding them in the RimWorld figure.
            _consumers = GpuMonitorApi.ResidentSnapshot();
            _consumersMb = 0f;
            foreach (var c in _consumers) _consumersMb += c.vramMb;

            float rimworldProcess = _rimworldMb;
            if (_rimworldMeasured)
            {
                // Measured RimWorld VRAM already includes the in-process consumers → split for display.
                _rimworldMb = Math.Max(0f, rimworldProcess - _consumersMb);
                _systemMb = totalUsedMb - rimworldProcess - _lmStudioVramMb;
            }
            else
            {
                // Unity's texture estimate doesn't include them → they come out of the remainder.
                _systemMb = totalUsedMb - rimworldProcess - _lmStudioVramMb - _consumersMb;
            }
            if (_systemMb < 0f) _systemMb = 0f;
        }

        // ────────────────────────────────────────────────────────
        //  RimWorld VRAM (Unity fallback)
        // ────────────────────────────────────────────────────────

        /// <summary>
        /// Uses Unity's own memory APIs to determine how much VRAM RimWorld is consuming.
        /// A fallback for when NVML per-process VRAM isn't available on the host.
        /// </summary>
        private static float GetRimWorldVramMb()
        {
            try
            {
                // Texture.currentTextureMemory = actual GPU-resident texture bytes.
                long texBytes = (long)Texture.currentTextureMemory;
                float texMb = texBytes / (1024f * 1024f);

                // Add overhead for render targets, shaders, mesh buffers, and Unity internals.
                float estimatedTotalMb = texMb * 1.4f;
                if (estimatedTotalMb < 50f) estimatedTotalMb = 50f;
                return estimatedTotalMb;
            }
            catch
            {
                return 200f; // ~200 MB is typical for RimWorld
            }
        }
    }
}
