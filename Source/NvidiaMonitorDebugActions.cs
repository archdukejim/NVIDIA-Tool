using LudeonTK;
using Verse;

namespace NvidiaGpuMonitor
{
    /// <summary>
    /// Debug actions for validating the GPU monitor's mechanics without waiting for their
    /// natural triggers. Grouped under the "NVIDIA Monitor" category in the Debug Actions
    /// menu, and headlessly triggerable via the dev-tools bridge (run_debug_action).
    /// </summary>
    public static class NvidiaMonitorDebugActions
    {
        private const string Category = "NVIDIA Monitor";

        /// <summary>Dump the current NVML/GPU readout to the log so it can be inspected headlessly.</summary>
        [DebugAction(Category, "Dump GPU stats", allowedGameStates = AllowedGameStates.Entry)]
        public static void DumpGpuStats()
        {
            if (!NvidiaSmiReader.IsAvailable)
            {
                Log.Message($"[NVIDIA Monitor] GPU unavailable: {NvidiaSmiReader.LastError ?? "nvml.dll not found"}");
                return;
            }

            Log.Message(
                "[NVIDIA Monitor] GPU stats dump:\n" +
                $"  GPU: {NvidiaSmiReader.GpuName} (driver {NvidiaSmiReader.DriverVersion})\n" +
                $"  VRAM: {NvidiaSmiReader.UsedVramMb / 1024f:F2} / {NvidiaSmiReader.TotalVramMb / 1024f:F2} GB\n" +
                $"  Util: {NvidiaSmiReader.UtilizationPercent}%  Temp: {NvidiaSmiReader.TemperatureC}°C  " +
                $"Power: {NvidiaSmiReader.PowerDrawW:F0}/{NvidiaSmiReader.PowerLimitW:F0} W\n" +
                $"  Clocks: {NvidiaSmiReader.GpuClockMhz} MHz gpu / {NvidiaSmiReader.MemClockMhz} MHz mem  " +
                $"Fan: {NvidiaSmiReader.FanSpeedPercent}%  Processes: {NvidiaSmiReader.Processes.Count}");
        }

        /// <summary>Recompute and log the estimated VRAM breakdown (System / RimWorld / LM Studio).</summary>
        [DebugAction(Category, "Dump VRAM breakdown", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void DumpVramBreakdown()
        {
            VramBreakdown.Refresh();
            Log.Message(
                "[NVIDIA Monitor] VRAM breakdown (estimates):\n" +
                $"  System:   ~{VramBreakdown.SystemMb / 1024f:F2} GB\n" +
                $"  RimWorld: ~{VramBreakdown.RimWorldMb / 1024f:F2} GB\n" +
                $"  LM Studio (on-GPU): ~{VramBreakdown.LmStudioVramMb / 1024f:F2} GB " +
                $"(+{VramBreakdown.LmStudioRamMb / 1024f:F2} GB offloaded to RAM, remote={VramBreakdown.LmStudioIsRemote})");
        }

        /// <summary>Force the on-load VRAM status dialog to appear now (bypasses the once-per-load
        /// and conflict-avoidance guards).</summary>
        [DebugAction(Category, "Force VRAM advisory", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ForceVramAdvisory()
        {
            if (!NvidiaSmiReader.IsAvailable)
            {
                Log.Message("[NVIDIA Monitor] Cannot show VRAM advisory — GPU unavailable.");
                return;
            }
            VramWarning.ForceShow();
            Log.Message("[NVIDIA Monitor] Forced VRAM advisory dialog.");
        }

        /// <summary>Force an immediate LM Studio probe and log the result.</summary>
        [DebugAction(Category, "Probe LM Studio now", allowedGameStates = AllowedGameStates.Entry)]
        public static void ProbeLmStudio()
        {
            if (!LmStudioProbe.Enabled)
            {
                Log.Message("[NVIDIA Monitor] LM Studio probe is disabled in settings.");
                return;
            }
            bool ok = LmStudioProbe.ForceSample();
            Log.Message(
                $"[NVIDIA Monitor] LM Studio probe: reachable={ok}, endpoint={LmStudioProbe.Endpoint}, " +
                $"model={LmStudioProbe.ModelName ?? "—"}, remote={LmStudioProbe.IsRemote}, " +
                $"estVram=~{LmStudioProbe.EstimatedVramMb / 1024f:F2} GB" +
                (string.IsNullOrEmpty(LmStudioProbe.LastError) ? "" : $", error={LmStudioProbe.LastError}"));
        }

        /// <summary>Cycle the overlay HUD mode (Off → Basic → LM Studio → Developer).</summary>
        [DebugAction(Category, "Cycle overlay mode", allowedGameStates = AllowedGameStates.Entry)]
        public static void CycleOverlayMode()
        {
            OverlayHud.CycleMode();
            Log.Message($"[NVIDIA Monitor] Overlay mode: {OverlayHud.CurrentMode}");
        }
    }
}
