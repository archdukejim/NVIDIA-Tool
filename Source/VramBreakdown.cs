using System;
using UnityEngine;

namespace NvidiaGpuMonitor
{
    /// <summary>
    /// Estimates a per-application VRAM breakdown from measured totals.
    ///
    /// Sources:
    ///   RimWorld  → Unity's own Texture.currentTextureMemory + overhead
    ///   LM Studio → optional, from the standalone <see cref="LmStudioProbe"/> (off unless
    ///               the user enables it). A remote host contributes nothing to local VRAM.
    ///   System    → Total VRAM used − RimWorld − LM Studio
    /// </summary>
    internal static class VramBreakdown
    {
        private static float _rimworldMb;
        private static float _lmStudioMb;
        private static float _lmStudioVramMb;
        private static float _lmStudioRamMb;
        private static float _systemMb;
        private static bool _lmStudioIsRemote;
        private static DateTime _lastUpdate = DateTime.MinValue;
        private const float UpdateIntervalSec = 3f;

        // ── Public accessors ──

        internal static float RimWorldMb => _rimworldMb;
        /// <summary>Full LM Studio estimate (VRAM-resident + any RAM-offloaded portion).</summary>
        internal static float LmStudioMb => _lmStudioMb;
        /// <summary>LM Studio portion actually resident on this GPU. Zero for a remote host.</summary>
        internal static float LmStudioVramMb => _lmStudioVramMb;
        /// <summary>LM Studio portion estimated to be offloaded to system RAM (not on the GPU).</summary>
        internal static float LmStudioRamMb => _lmStudioRamMb;
        internal static float SystemMb => _systemMb;
        /// <summary>True when the configured LM Studio endpoint is a remote host, so none of its VRAM is on this GPU.</summary>
        internal static bool LmStudioIsRemote => _lmStudioIsRemote;

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

            // 1. RimWorld — query Unity's own GPU memory tracking
            _rimworldMb = GetRimWorldVramMb();

            // 2. LM Studio — from the optional standalone probe. The probe already returns
            //    zero when disabled, unreachable, or pointed at a remote host, so a remote
            //    model never becomes phantom local VRAM.
            _lmStudioIsRemote = LmStudioProbe.Enabled && LmStudioProbe.IsRemote;
            _lmStudioMb = LmStudioProbe.Enabled ? LmStudioProbe.EstimatedVramMb : 0f;

            // 3. Split LM Studio into VRAM vs offloaded RAM based on what's physically possible
            float maxAvailableForLms = totalUsedMb - _rimworldMb;
            if (maxAvailableForLms < 0f) maxAvailableForLms = 0f;

            // Estimate System base overhead (e.g. max 1 GB or whatever is left)
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

            _systemMb = totalUsedMb - _rimworldMb - _lmStudioVramMb;
            if (_systemMb < 0f) _systemMb = 0f;
        }

        // ────────────────────────────────────────────────────────
        //  RimWorld VRAM (from Unity)
        // ────────────────────────────────────────────────────────

        /// <summary>
        /// Uses Unity's own memory APIs to determine how much VRAM
        /// RimWorld is consuming. No external calls needed.
        /// </summary>
        private static float GetRimWorldVramMb()
        {
            try
            {
                // Texture.currentTextureMemory = actual GPU-resident texture bytes
                // This is the most reliable Unity API for GPU memory tracking
                long texBytes = (long)Texture.currentTextureMemory;
                float texMb = texBytes / (1024f * 1024f);

                // Add overhead for:
                //   - Render targets / frame buffers (~15-25% of texture memory)
                //   - Shader programs, constant buffers
                //   - Mesh GPU buffers (vertex/index)
                //   - Unity internal GPU allocations
                // Conservative 40% overhead multiplier for a 2D-heavy game like RimWorld
                float estimatedTotalMb = texMb * 1.4f;

                // Floor: even a minimal RimWorld scene uses some GPU memory
                if (estimatedTotalMb < 50f) estimatedTotalMb = 50f;

                return estimatedTotalMb;
            }
            catch
            {
                // If Unity's API fails, return a reasonable default
                return 200f; // ~200 MB is typical for RimWorld
            }
        }
    }
}
