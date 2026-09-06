using UnityEngine;
using Verse;

namespace NvidiaGpuMonitor
{
    /// <summary>
    /// Mod entry point for NVIDIA GPU Monitor.
    /// Starts GPU polling, the optional LM Studio probe, and manages the overlay HUD.
    /// Fully standalone — no dependency on any other mod.
    /// </summary>
    public class DevToolsMod : Mod
    {
        public static DevToolsMod Instance { get; private set; }

        public DevToolsSettings Settings { get; private set; }

        private DevToolsWindow _window;

        public DevToolsMod(ModContentPack content) : base(content)
        {
            Instance = this;

            // Load persistent settings
            Settings = GetSettings<DevToolsSettings>();

            // Start GPU polling and the optional LM Studio probe
            NvidiaSmiReader.Start();
            LmStudioProbe.Start();

            Log.Message("[NVIDIA Monitor] NVIDIA GPU Monitor loaded.");
        }

        public override string SettingsCategory() => "NVIDIA GPU Monitor";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("NVIDIA GPU Monitor",
                tooltip: "GPU monitoring and hardware dashboard.");
            listing.GapLine();

            // ── Overlay controls ──
            listing.Label("In-Game Overlay");
            listing.Gap(4f);

            string modeText;
            switch (OverlayHud.CurrentMode)
            {
                case OverlayMode.Basic: modeText = "Basic"; break;
                case OverlayMode.LmStudio: modeText = "LM Studio"; break;
                case OverlayMode.Developer: modeText = "Developer"; break;
                default: modeText = "Off"; break;
            }

            if (listing.ButtonText($"Overlay: {modeText}  (click to cycle)"))
            {
                OverlayHud.CycleMode();
            }

            listing.Gap(4f);
            var prev1 = GUI.color;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            listing.Label("  Basic: VRAM breakdown (System, RimWorld, LM Studio)");
            listing.Label("  LM Studio: + loaded model and estimated VRAM (opt-in below)");
            listing.Label("  Developer: + GPU temp, power, clocks, fan, utilization");
            listing.Label("  Click the mode label on the overlay to cycle.");
            listing.Label("  Drag the overlay header to reposition.");
            GUI.color = prev1;

            listing.Gap(12f);
            listing.GapLine();

            // ── VRAM notification ──
            listing.Label("VRAM Notifications");
            listing.Gap(4f);

            listing.CheckboxLabeled(
                "Show VRAM status on game load",
                ref Settings.alwaysNotifyVram,
                "Shows your VRAM breakdown every time you load a colony.\n" +
                "Uncheck to disable notifications (still warns if critically low).");

            listing.Gap(4f);
            var prev2 = GUI.color;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            listing.Label("  Checked (default): VRAM status every colony load");
            listing.Label("  Unchecked: only warns when < 3 GB free");
            GUI.color = prev2;

            listing.Gap(12f);
            listing.GapLine();

            // ── LM Studio awareness (optional) ──
            listing.Label("Local LLM (LM Studio) — optional");
            listing.Gap(4f);

            listing.CheckboxLabeled(
                "Monitor a local LM Studio model",
                ref Settings.lmStudioEnabled,
                "Off by default. When enabled, the tool reads the loaded model from your\n" +
                "LM Studio endpoint and estimates its VRAM. Works with RimTalk, RimSynapse,\n" +
                "or any setup running LM Studio — no other mod required.");

            if (Settings.lmStudioEnabled)
            {
                listing.Gap(4f);
                listing.Label("  Endpoint:");
                Settings.lmStudioEndpoint = listing.TextEntry(Settings.lmStudioEndpoint);

                listing.Gap(4f);
                var prevLm = GUI.color;
                if (LmStudioProbe.Reachable)
                {
                    GUI.color = new Color(0.35f, 0.85f, 0.35f);
                    string tag = LmStudioProbe.IsRemote ? " (remote — not counted as local VRAM)" : "";
                    listing.Label($"  ✓ Connected — model: {LmStudioProbe.ModelName}{tag}");
                }
                else
                {
                    GUI.color = new Color(0.9f, 0.6f, 0.2f);
                    listing.Label("  … not reachable yet " +
                        (string.IsNullOrEmpty(LmStudioProbe.LastError) ? "" : $"({LmStudioProbe.LastError})"));
                }
                GUI.color = prevLm;
            }

            listing.Gap(12f);
            listing.GapLine();

            // ── Full dashboard ──
            if (listing.ButtonText("Open Full Dashboard"))
            {
                OpenDashboard();
            }

            listing.Gap(12f);
            listing.GapLine();

            // ── GPU status summary ──
            listing.Label("GPU Status");
            listing.Gap(4f);

            if (NvidiaSmiReader.IsAvailable)
            {
                listing.Label($"  GPU: {NvidiaSmiReader.GpuName}");
                listing.Label($"  Driver: {NvidiaSmiReader.DriverVersion}");
                listing.Label($"  Utilization: {NvidiaSmiReader.UtilizationPercent}%");
                listing.Label($"  VRAM: {NvidiaSmiReader.UsedVramMb / 1024f:F1} / " +
                              $"{NvidiaSmiReader.TotalVramMb / 1024f:F1} GB");
                listing.Label($"  Temperature: {NvidiaSmiReader.TemperatureC}°C");
                listing.Label($"  Power: {NvidiaSmiReader.PowerDrawW:F0} / " +
                              $"{NvidiaSmiReader.PowerLimitW:F0} W");
                listing.Label($"  GPU Clock: {NvidiaSmiReader.GpuClockMhz} MHz");
                listing.Label($"  Mem Clock: {NvidiaSmiReader.MemClockMhz} MHz");
                listing.Label($"  Fan: {NvidiaSmiReader.FanSpeedPercent}%");

                // Process breakdown
                var processes = NvidiaSmiReader.Processes;
                if (processes.Count > 0)
                {
                    listing.Gap(4f);
                    listing.Label("  VRAM by Process:");
                    foreach (var p in processes)
                    {
                        string icon = p.IsLmStudio ? "⚡" : p.IsRimWorld ? "🎮" : "  ";
                        listing.Label($"    {icon} {p.Name}: {p.VramMb:F0} MB");
                    }
                }
            }
            else
            {
                var prev = GUI.color;
                GUI.color = Color.red;
                listing.Label("  NVIDIA GPU not detected.");
                GUI.color = prev;

                if (!string.IsNullOrEmpty(NvidiaSmiReader.LastError))
                {
                    GUI.color = Color.yellow;
                    listing.Label($"  {NvidiaSmiReader.LastError}");
                    GUI.color = prev;
                }
            }

            listing.End();
        }

        /// <summary>
        /// Open or focus the full dashboard window.
        /// </summary>
        public void OpenDashboard()
        {
            if (_window != null && Find.WindowStack.IsOpen(_window))
            {
                Find.WindowStack.TryRemove(_window);
                return;
            }

            _window = new DevToolsWindow();
            Find.WindowStack.Add(_window);
        }
    }

    /// <summary>
    /// Post-load hook. Overlay starts Off — use the toolbar toggle to enable.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DevToolsStartup
    {
        static DevToolsStartup()
        {
            // Overlay starts off — toolbar toggle icon lets users enable it
            OverlayHud.SetMode(OverlayMode.Off);
            Log.Message("[NVIDIA Monitor] Ready. Use the toolbar icon to toggle the GPU overlay.");
        }
    }
}
