using LudeonTK;
using Verse;

namespace NvidiaGpuMonitor
{
    /// <summary>
    /// Debug actions for validating the GPU monitor's mechanics without waiting for their
    /// natural triggers. Grouped under the "GPU Monitor" category in the Debug Actions
    /// menu, and headlessly triggerable via the dev-tools bridge (run_debug_action).
    /// </summary>
    public static class NvidiaMonitorDebugActions
    {
        private const string Category = "GPU Monitor";

        /// <summary>Dump the current NVML/GPU readout to the log so it can be inspected headlessly.</summary>
        [DebugAction(Category, "Dump GPU stats", allowedGameStates = AllowedGameStates.Entry)]
        public static void DumpGpuStats()
        {
            if (!NvidiaSmiReader.IsAvailable)
            {
                Log.Message($"[GPU Monitor] GPU unavailable: {NvidiaSmiReader.LastError ?? "nvml.dll not found"}");
                return;
            }

            Log.Message(
                "[GPU Monitor] GPU stats dump:\n" +
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
            string rwSrc = VramBreakdown.RimWorldMeasured ? "measured" : "estimated";
            string lmSrc = VramBreakdown.LmStudioMeasured ? "measured" : "estimated";
            Log.Message(
                "[GPU Monitor] VRAM breakdown:\n" +
                $"  System:   {VramBreakdown.SystemMb / 1024f:F2} GB (remainder)\n" +
                $"  RimWorld: {VramBreakdown.RimWorldMb / 1024f:F2} GB ({rwSrc})\n" +
                $"  LM Studio (on-GPU): {VramBreakdown.LmStudioVramMb / 1024f:F2} GB ({lmSrc}, " +
                $"+{VramBreakdown.LmStudioRamMb / 1024f:F2} GB offloaded, remote={VramBreakdown.LmStudioIsRemote})");
        }

        /// <summary>Dump the raw NVML per-process VRAM list — proves whether the driver reports
        /// per-process memory on this system and which process names LM Studio / RimWorld use.</summary>
        [DebugAction(Category, "Dump GPU processes", allowedGameStates = AllowedGameStates.Entry)]
        public static void DumpProcesses()
        {
            var procs = NvidiaSmiReader.Processes;
            if (procs.Count == 0)
            {
                Log.Message("[GPU Monitor] NVML reported no GPU processes (per-process memory may be unavailable on this driver/OS).");
                return;
            }
            var sb = new System.Text.StringBuilder("[GPU Monitor] GPU processes (NVML _v3):\n");
            foreach (var p in procs)
            {
                string tag = p.IsLmStudio ? " [LM Studio]" : p.IsRimWorld ? " [RimWorld]" : "";
                sb.AppendLine($"  PID {p.Pid}  {p.Name}{tag}: {p.VramMb:F0} MB");
            }
            Log.Message(sb.ToString().TrimEnd());
        }

        /// <summary>Force the on-load VRAM status dialog to appear now (bypasses the once-per-load
        /// and conflict-avoidance guards).</summary>
        [DebugAction(Category, "Force VRAM advisory", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ForceVramAdvisory()
        {
            if (!NvidiaSmiReader.IsAvailable)
            {
                Log.Message("[GPU Monitor] Cannot show VRAM advisory — GPU unavailable.");
                return;
            }
            VramWarning.ForceShow();
            Log.Message("[GPU Monitor] Forced VRAM advisory dialog.");
        }

        /// <summary>Force an immediate LM Studio probe and log the result.</summary>
        [DebugAction(Category, "Probe LM Studio now", allowedGameStates = AllowedGameStates.Entry)]
        public static void ProbeLmStudio()
        {
            if (!LmStudioProbe.Enabled)
            {
                Log.Message("[GPU Monitor] LM Studio probe is disabled in settings.");
                return;
            }
            bool ok = LmStudioProbe.ForceSample();
            Log.Message(
                $"[GPU Monitor] LM Studio probe: reachable={ok}, endpoint={LmStudioProbe.Endpoint}, " +
                $"model={LmStudioProbe.ModelName ?? "—"}, remote={LmStudioProbe.IsRemote}, " +
                $"estVram=~{LmStudioProbe.EstimatedVramMb / 1024f:F2} GB" +
                (string.IsNullOrEmpty(LmStudioProbe.LastError) ? "" : $", error={LmStudioProbe.LastError}"));
        }

        /// <summary>Dump everything the receiver endpoint (GpuMonitorApi) currently holds —
        /// proves the listener received reports from other mods (in-process or cross-assembly).</summary>
        [DebugAction(Category, "Dump reported consumers", allowedGameStates = AllowedGameStates.Entry)]
        public static void DumpReportedConsumers()
        {
            var all = GpuMonitorApi.AllSnapshot();
            if (all.Count == 0)
            {
                Log.Message("[GPU Monitor] Listener holds 0 reported consumers.");
                return;
            }
            var sb = new System.Text.StringBuilder($"[GPU Monitor] Listener holds {all.Count} reported consumer(s):\n");
            foreach (var c in all)
                sb.AppendLine($"  id={c.id}  label=\"{c.label}\"  {c.vramMb:F0} MB  resident={c.resident}");
            Log.Message(sb.ToString().TrimEnd());
        }

        private const string TestConsumerId = "gpumonitor.debug.testconsumer";

        /// <summary>Register a fake ~512 MB in-process consumer through the public receiver
        /// endpoint, then dump the breakdown — proves GpuMonitorApi and the carve-out work.</summary>
        [DebugAction(Category, "Register test consumer + dump", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RegisterTestConsumer()
        {
            GpuMonitorApi.UpsertConsumer(TestConsumerId, "Debug test consumer", 512f, true);
            Log.Message("[GPU Monitor] Registered a 512 MB test consumer via GpuMonitorApi.");
            DumpVramBreakdown();
        }

        /// <summary>Remove the debug test consumer registered above.</summary>
        [DebugAction(Category, "Remove test consumer", allowedGameStates = AllowedGameStates.Entry)]
        public static void RemoveTestConsumer()
        {
            GpuMonitorApi.RemoveConsumer(TestConsumerId);
            Log.Message("[GPU Monitor] Removed the test consumer.");
        }

        /// <summary>Cycle the overlay HUD mode (Off → Basic → LM Studio → Developer).</summary>
        [DebugAction(Category, "Cycle overlay mode", allowedGameStates = AllowedGameStates.Entry)]
        public static void CycleOverlayMode()
        {
            OverlayHud.CycleMode();
            Log.Message($"[GPU Monitor] Overlay mode: {OverlayHud.CurrentMode}");
        }
    }
}
