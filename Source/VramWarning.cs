using System;
using UnityEngine;
using Verse;

namespace NvidiaGpuMonitor
{
    /// <summary>
    /// Checks VRAM headroom on game load and warns the player if
    /// available VRAM is dangerously low before the colony even starts.
    ///
    /// NOT a GameComponent — uses static state only. Zero save-file footprint.
    /// The mod can be safely added or removed from any save game.
    ///
    /// Called from the Harmony GUI patch when a colony is first loaded.
    /// </summary>
    internal static class VramWarning
    {
        /// <summary>Minimum GB of free VRAM recommended for stable play.</summary>
        private const float MinFreeGb = 2.0f;

        /// <summary>Only warn/notify once per game load.</summary>
        private static bool _hasChecked;

        /// <summary>Track which Game instance we last checked for.</summary>
        private static int _lastGameId;

        /// <summary>
        /// Called from the GUI patch each frame. Checks once per game load.
        /// </summary>
        internal static void CheckOnce()
        {
            if (!NvidiaSmiReader.IsAvailable) return;
            if (NvidiaSmiReader.TotalVramMb <= 0f) return;

            // Detect new game load by checking if the Game instance changed
            var game = Verse.Current.Game;
            if (game == null) return;
            int gameId = game.GetHashCode();

            if (_hasChecked && gameId == _lastGameId) return;

            _hasChecked = true;
            _lastGameId = gameId;

            // Conflict avoidance: if another loaded mod already owns an on-load VRAM
            // advisory (e.g. RimSynapse Core's cross-vendor VRAM advisor), stay silent
            // so the player doesn't get two dialogs. The overlay and dashboard still work.
            if (AnotherModOwnsVramAdvisory()) return;

            CheckVramHeadroom(force: false);
        }

        /// <summary>
        /// Force the VRAM status dialog to appear right now, bypassing the once-per-load
        /// guard and the conflict-avoidance check. Used by the debug action for validation.
        /// </summary>
        internal static void ForceShow()
        {
            CheckVramHeadroom(force: true);
        }

        /// <summary>
        /// True when another active mod ships its own on-load VRAM advisory, so this tool
        /// should defer to it. Matched by packageId — no compile-time dependency.
        /// </summary>
        private static bool AnotherModOwnsVramAdvisory()
        {
            try
            {
                foreach (var m in ModLister.AllInstalledMods)
                {
                    if (m == null || !m.Active) continue;
                    if (string.Equals(m.PackageId, "rimsynapse.core", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static void CheckVramHeadroom(bool force)
        {
            // Check user preference (forced calls always show the info dialog)
            bool alwaysNotify = force || (DevToolsMod.Instance?.Settings?.alwaysNotifyVram ?? false);
            bool isRemote = LmStudioProbe.Enabled && LmStudioProbe.IsRemote;

            if (isRemote)
            {
                if (alwaysNotify)
                {
                    ShowRemoteInfoDialog();
                }
                return; // Never trigger a low-VRAM warning for a remote LM Studio setup
            }

            float totalMb = NvidiaSmiReader.TotalVramMb;
            float usedMb = NvidiaSmiReader.UsedVramMb;
            float freeMb = totalMb - usedMb;
            float freeGb = freeMb / 1024f;

            if (alwaysNotify)
            {
                // Always show — informational status, not alarming
                ShowInfoDialog(freeGb, totalMb, usedMb);
            }
            else if (freeGb < MinFreeGb)
            {
                // Only warn when VRAM is critically low
                ShowWarningDialog(freeGb, totalMb, usedMb);
            }
        }

        private static void ShowRemoteInfoDialog()
        {
            string msg = "NVIDIA GPU Monitor is active, but LM Studio is configured to use a remote host.\n\n" +
                         "Local VRAM will not be attributed to the LM Studio model, since it runs on another machine.";

            var dialog = new Dialog_MessageBox(
                text: msg,
                buttonAText: "OK",
                title: "GPU Monitor: Remote LM Studio Detected"
            );
            Find.WindowStack.Add(dialog);
        }

        /// <summary>
        /// Informational dialog — shown every load when "Always Notify" is checked.
        /// Non-alarming, just tells them their VRAM status.
        /// </summary>
        private static void ShowInfoDialog(float freeGb, float totalMb, float usedMb)
        {
            VramBreakdown.Refresh();
            float totalGb = totalMb / 1024f;
            float usedGb = usedMb / 1024f;
            float systemGb = VramBreakdown.SystemMb / 1024f;
            // Only the GPU-resident LM Studio portion counts as local VRAM; the
            // offloaded portion lives in system RAM and must not inflate this line.
            float lmsGb = VramBreakdown.LmStudioVramMb / 1024f;
            float lmsRamGb = VramBreakdown.LmStudioRamMb / 1024f;
            float rwGb = VramBreakdown.RimWorldMb / 1024f;

            string status = freeGb >= MinFreeGb
                ? $"✓  You have {freeGb:F1} GB free — you're in good shape."
                : $"⚠  You have {freeGb:F1} GB free — this is tight for late-game.";

            // System / LM Studio / RimWorld are estimates (~); used/free/total are measured.
            string breakdown =
                $"  • System / Desktop:  ~{systemGb:F1} GB\n";
            if (lmsGb >= 0.05f || lmsRamGb >= 0.05f)
            {
                breakdown += $"  • LM Studio model:   ~{lmsGb:F1} GB\n";
                if (lmsRamGb >= 0.05f)
                    breakdown += $"      (+~{lmsRamGb:F1} GB offloaded to system RAM, not on GPU)\n";
            }
            breakdown +=
                $"  • RimWorld:          ~{rwGb:F1} GB\n" +
                $"  • Free:              {freeGb:F1} GB\n";

            string msg =
                "GPU Monitor — VRAM Status\n\n" +
                $"GPU: {NvidiaSmiReader.GpuName}\n" +
                $"VRAM: {usedGb:F1} / {totalGb:F1} GB used\n\n" +
                breakdown + "\n" +
                status + "\n\n" +
                "Values marked ~ are estimates (RimWorld from texture memory, LM Studio from model size).\n" +
                "Disable 'Show VRAM status on game load' in mod settings to only see warnings.";

            Find.WindowStack.Add(new Dialog_MessageBox(
                msg,
                "OK",
                null,
                null,
                null,
                null,
                false,
                null,
                null));
        }

        /// <summary>
        /// Warning dialog — shown only when VRAM headroom is critically low.
        /// Includes actionable suggestions.
        /// </summary>
        private static void ShowWarningDialog(float freeGb, float totalMb, float usedMb)
        {
            VramBreakdown.Refresh();
            float totalGb = totalMb / 1024f;
            float usedGb = usedMb / 1024f;
            float systemGb = VramBreakdown.SystemMb / 1024f;
            // GPU-resident LM Studio portion only; offloaded portion is system RAM.
            float lmsGb = VramBreakdown.LmStudioVramMb / 1024f;
            float lmsRamGb = VramBreakdown.LmStudioRamMb / 1024f;
            float rwGb = VramBreakdown.RimWorldMb / 1024f;

            string lmsLines = "";
            if (lmsGb >= 0.05f || lmsRamGb >= 0.05f)
            {
                lmsLines = $"  • LM Studio model:   ~{lmsGb:F1} GB\n";
                if (lmsRamGb >= 0.05f)
                    lmsLines += $"      (+~{lmsRamGb:F1} GB offloaded to system RAM, not on GPU)\n";
            }

            string msg =
                "GPU Monitor — VRAM Warning\n\n" +
                $"Your GPU has {freeGb:F1} GB free out of {totalGb:F1} GB.\n" +
                $"Before RimWorld even started, your system was already using {usedGb:F1} GB:\n\n" +
                $"  • System / Desktop:  ~{systemGb:F1} GB\n" +
                lmsLines +
                $"  • RimWorld:          ~{rwGb:F1} GB\n\n" +
                $"With less than {MinFreeGb:F0} GB free, you may experience:\n" +
                "  • Late-game slowdowns as colony grows\n" +
                "  • Frame drops during large raids\n" +
                "  • GPU memory thrashing (stuttering)\n\n" +
                "Suggestions:\n" +
                "  • If running a local LLM, load a smaller model (e.g., 7B instead of 12B)\n" +
                "  • Reduce the model's context window\n" +
                "  • Close GPU-heavy background apps (Chrome, Discord)\n" +
                "  • Lower RimWorld graphics settings\n\n" +
                "Enable 'Show VRAM status on game load' in mod settings for info every load.";

            Find.WindowStack.Add(new Dialog_MessageBox(
                msg,
                "Got it",
                null,
                "Open GPU Overlay",
                delegate
                {
                    OverlayHud.SetMode(OverlayMode.Basic);
                },
                null,
                false,
                null,
                null));

            Log.Warning(
                $"[GPU Monitor] Low VRAM: {freeGb:F1} GB free of {totalGb:F1} GB. " +
                $"System: {systemGb:F1} GB, LM Studio: {lmsGb:F1} GB.");
        }
    }
}
