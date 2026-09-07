using System;
using UnityEngine;
using Verse;

namespace NvidiaGpuMonitor
{
    /// <summary>
    /// Section drawing methods for the GPU dashboard: GPU hardware stats and the
    /// optional LM Studio status.
    /// </summary>
    public partial class DevToolsWindow
    {
        // ────────────────────────────────────────────────────────
        //  GPU Stats
        // ────────────────────────────────────────────────────────

        private void DrawGpuSection(Listing_Standard listing)
        {
            DrawSectionHeader(listing, "GPU Status");

            if (!NvidiaSmiReader.IsAvailable)
            {
                var prev = GUI.color;
                GUI.color = ColorRed;
                listing.Label("  ✗ NVIDIA GPU not detected");
                GUI.color = ColorDimText;
                listing.Label("    " + (NvidiaSmiReader.LastError ?? "nvml.dll not found"));
                GUI.color = prev;
                return;
            }

            DrawLabelValue(listing, "GPU", NvidiaSmiReader.GpuName);
            DrawLabelValue(listing, "Driver", NvidiaSmiReader.DriverVersion);

            listing.Gap(4f);

            float utilPct = NvidiaSmiReader.UtilizationPercent / 100f;
            DrawProgressBar(listing, "GPU Utilization",
                $"{NvidiaSmiReader.UtilizationPercent}%",
                utilPct, GetLoadColor(utilPct));

            float vramPct = NvidiaSmiReader.TotalVramMb > 0
                ? NvidiaSmiReader.UsedVramMb / NvidiaSmiReader.TotalVramMb : 0f;
            string vramText = $"{NvidiaSmiReader.UsedVramMb / 1024f:F1} / " +
                              $"{NvidiaSmiReader.TotalVramMb / 1024f:F1} GB";
            DrawProgressBar(listing, "VRAM", vramText, vramPct, ColorVramBar);

            float tempPct = Math.Min(1f, NvidiaSmiReader.TemperatureC / 100f);
            DrawProgressBar(listing, "Temperature",
                $"{NvidiaSmiReader.TemperatureC}°C",
                tempPct, GetTempColor(NvidiaSmiReader.TemperatureC));

            float powerPct = NvidiaSmiReader.PowerLimitW > 0
                ? NvidiaSmiReader.PowerDrawW / NvidiaSmiReader.PowerLimitW : 0f;
            DrawProgressBar(listing, "Power Draw",
                $"{NvidiaSmiReader.PowerDrawW:F0} / {NvidiaSmiReader.PowerLimitW:F0} W",
                powerPct, ColorPowerBar);

            DrawLabelValue(listing, "GPU Clock", $"{NvidiaSmiReader.GpuClockMhz} MHz");
            DrawLabelValue(listing, "Mem Clock", $"{NvidiaSmiReader.MemClockMhz} MHz");
            DrawLabelValue(listing, "Fan Speed", $"{NvidiaSmiReader.FanSpeedPercent}%");

            var processes = NvidiaSmiReader.Processes;
            if (processes.Count > 0)
            {
                listing.Gap(4f);
                listing.Label("  VRAM by Process:");

                foreach (var p in processes)
                {
                    string icon = p.IsLmStudio ? "⚡" : p.IsRimWorld ? "🎮" : "  ";
                    DrawLabelValue(listing, $"    {icon} {p.Name}",
                        $"{p.VramMb:F0} MB");
                }
            }

            if (NvidiaSmiReader.LastUpdated != DateTime.MinValue)
            {
                var age = DateTime.UtcNow - NvidiaSmiReader.LastUpdated;
                if (age.TotalSeconds > 10)
                {
                    var prev = GUI.color;
                    GUI.color = ColorYellow;
                    listing.Label($"  ⚠ Data is {age.TotalSeconds:F0}s old");
                    GUI.color = prev;
                }
            }
        }

        // ────────────────────────────────────────────────────────
        //  LM Studio Status (optional)
        // ────────────────────────────────────────────────────────

        private void DrawLmStudioSection(Listing_Standard listing)
        {
            DrawSectionHeader(listing, "LM Studio (optional)");

            var prev = GUI.color;

            if (!LmStudioProbe.Enabled)
            {
                GUI.color = ColorDimText;
                listing.Label("  Disabled — enable in mod settings to monitor a local model.");
                GUI.color = prev;
                return;
            }

            DrawLabelValue(listing, "Endpoint", LmStudioProbe.Endpoint);

            if (LmStudioProbe.Reachable)
            {
                GUI.color = ColorGreen;
                listing.Label(LmStudioProbe.IsRemote ? "  ✓ Connected (remote host)" : "  ✓ Connected");
                GUI.color = prev;

                DrawLabelValue(listing, "Model", LmStudioProbe.ModelName ?? "—");
                if (LmStudioProbe.IsRemote)
                {
                    DrawLabelValue(listing, "Local VRAM", "n/a (runs on remote GPU)");
                }
                else
                {
                    DrawLabelValue(listing, "Est. Model VRAM",
                        $"~{LmStudioProbe.EstimatedVramMb / 1024f:F1} GB");
                }
            }
            else
            {
                GUI.color = ColorRed;
                listing.Label("  ✗ Not reachable");
                GUI.color = ColorDimText;
                if (!string.IsNullOrEmpty(LmStudioProbe.LastError))
                    listing.Label("    " + LmStudioProbe.LastError);
                GUI.color = prev;
            }
        }
    }
}
