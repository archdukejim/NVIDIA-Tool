using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace NvidiaGpuMonitor
{
    /// <summary>
    /// Compact, always-on-screen GPU overlay HUD.
    ///
    /// IMPORTANT: This is NOT a GameComponent. Using a Harmony patch on
    /// GameComponentUtility.GameComponentOnGUI() instead ensures zero save-file
    /// footprint — the mod can be safely added or removed from any save game
    /// without errors.
    ///
    /// Three display modes (cycled via button):
    ///   Basic     → VRAM breakdown
    ///   LM Studio → + loaded model and estimated VRAM (opt-in)
    ///   Developer → + GPU temp, power, clocks, fan, utilization
    /// </summary>
    [StaticConstructorOnStartup]
    public static partial class OverlayHud
    {
        // ── Display state ──
        private static OverlayMode _mode = OverlayMode.Off;
        private static Vector2 _position = new Vector2(10f, 80f);
        private static bool _dragging;
        private static Vector2 _dragOffset;

        // ── Layout constants ──
        private const float PanelWidth = 280f;
        private const float RowHeight = 20f;
        private const float Padding = 10f;
        private const float HeaderHeight = 24f;
        private const float BarHeight = 4f;

        // ── Colors ──
        private static readonly Color BgColor = new Color(0.08f, 0.08f, 0.10f, 0.88f);
        private static readonly Color HeaderBg = new Color(0.12f, 0.14f, 0.18f, 0.95f);
        private static readonly Color BorderColor = new Color(0.25f, 0.55f, 0.90f, 0.6f);
        private static readonly Color TextLabel = new Color(0.70f, 0.70f, 0.70f);
        private static readonly Color TextValue = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color AccentGreen = new Color(0.35f, 0.85f, 0.35f);
        private static readonly Color AccentYellow = new Color(0.90f, 0.85f, 0.30f);
        private static readonly Color AccentOrange = new Color(0.95f, 0.55f, 0.20f);
        private static readonly Color AccentRed = new Color(0.95f, 0.30f, 0.25f);
        private static readonly Color BarVram = new Color(0.30f, 0.60f, 0.95f, 0.8f);

        // Cached textures
        private static Texture2D _bgTex;
        private static Texture2D _headerTex;
        private static Texture2D _borderTex;
        private static Texture2D _barBgTex;
        private static Texture2D _barFillTex;
        private static Texture2D _whiteTex;

        /// <summary>Toggle overlay mode: Off → Basic → LM Studio → Developer → Off.</summary>
        public static void CycleMode()
        {
            switch (_mode)
            {
                case OverlayMode.Off: _mode = OverlayMode.Basic; break;
                case OverlayMode.Basic: _mode = OverlayMode.LmStudio; break;
                case OverlayMode.LmStudio: _mode = OverlayMode.Developer; break;
                case OverlayMode.Developer: _mode = OverlayMode.Off; break;
            }
        }

        /// <summary>Set overlay to a specific mode.</summary>
        public static void SetMode(OverlayMode mode) => _mode = mode;
        public static OverlayMode CurrentMode => _mode;

        /// <summary>
        /// Called from Harmony patch every GUI frame.
        /// </summary>
        internal static void OnGUI()
        {
            if (_mode == OverlayMode.Off) return;
            if (!NvidiaSmiReader.IsAvailable) return;

            // Only show during active colony gameplay (not world map, not menus)
            if (Verse.Current.Game?.CurrentMap == null) return;

            EnsureTextures();

            bool showLmStudio = _mode == OverlayMode.LmStudio || _mode == OverlayMode.Developer;

            // Calculate panel height based on mode
            int basicRows = 5;
            int lmStudioRows = showLmStudio ? 3 : 0;
            int devRows = _mode == OverlayMode.Developer ? 8 : 0;
            float panelHeight = Padding + HeaderHeight + Padding
                + (basicRows * RowHeight)
                + (lmStudioRows > 0 ? 4f + (lmStudioRows * RowHeight) : 0f)
                + (devRows > 0 ? 4f + (devRows * RowHeight) : 0f)
                + Padding;

            // Clamp position to screen
            _position.x = Mathf.Clamp(_position.x, 0, Screen.width - PanelWidth);
            _position.y = Mathf.Clamp(_position.y, 0, Screen.height - panelHeight);

            var panelRect = new Rect(_position.x, _position.y, PanelWidth, panelHeight);

            // ── Handle dragging ──
            HandleDrag(panelRect);

            // ── Draw panel background ──
            GUI.DrawTexture(panelRect, _bgTex);

            // Border (1px)
            DrawBorder(panelRect);

            float y = panelRect.y + Padding;
            float x = panelRect.x + Padding;
            float contentWidth = PanelWidth - Padding * 2;

            // ── Header row ──
            var headerRect = new Rect(panelRect.x, panelRect.y, PanelWidth, HeaderHeight + Padding);
            GUI.DrawTexture(headerRect, _headerTex);

            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.4f, 0.7f, 1.0f) },
            };
            GUI.Label(new Rect(x, y, contentWidth * 0.6f, HeaderHeight), "▸ GPU Monitor", headerStyle);

            // Mode toggle button (entire header is clickable)
            var modeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.5f, 0.5f, 0.6f) },
            };
            string modeLabel;
            switch (_mode)
            {
                case OverlayMode.Developer: modeLabel = "[Dev]"; break;
                case OverlayMode.LmStudio: modeLabel = "[LM]"; break;
                default: modeLabel = "[Basic]"; break;
            }

            // Draw the mode label
            var modeBtnRect = new Rect(x + contentWidth - 60f, y, 60f, HeaderHeight - 4f);
            GUI.Label(modeBtnRect, modeLabel, modeStyle);

            y += HeaderHeight + 2f;

            // ── VRAM Block ──
            bool isRemote = LmStudioProbe.Enabled && LmStudioProbe.IsRemote;

            if (isRemote)
            {
                var redStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.3f, 0.3f) }
                };
                GUI.Label(new Rect(x, y, contentWidth, 20f), "REMOTE LMSTUDIO", redStyle);
                y += 24f;
            }

            {
                float totalVramGb = NvidiaSmiReader.TotalVramMb / 1024f;
                float usedVramGb = NvidiaSmiReader.UsedVramMb / 1024f;
                float availableVramGb = totalVramGb - usedVramGb;
                float vramPct = totalVramGb > 0 ? usedVramGb / totalVramGb : 0f;

                DrawRow(x, ref y, contentWidth, "Available VRAM",
                    $"{availableVramGb:F1} / {totalVramGb:F1} GB",
                    VramColor(vramPct));

                // VRAM bar
                DrawMiniBar(x, ref y, contentWidth, vramPct, BarVram);
                y += 4f;

                // ── Per-application breakdown ──
                VramBreakdown.Refresh();
                float totalUsedMb = NvidiaSmiReader.UsedVramMb;

                DrawProcessRow(x, ref y, contentWidth, "System",
                    VramBreakdown.SystemMb, totalUsedMb, NvidiaSmiReader.TotalVramMb);
                DrawProcessRow(x, ref y, contentWidth, "RimWorld",
                    VramBreakdown.RimWorldMb, totalUsedMb, NvidiaSmiReader.TotalVramMb);
                DrawProcessRow(x, ref y, contentWidth, "LM Studio",
                    VramBreakdown.LmStudioVramMb, totalUsedMb, NvidiaSmiReader.TotalVramMb, VramBreakdown.LmStudioRamMb);
            }

            // ── LM Studio section (shown in LM Studio + Developer) ──
            if (showLmStudio)
            {
                y += 2f;
                var divRect = new Rect(x, y, contentWidth, 1f);
                GUI.DrawTexture(divRect, _barBgTex);
                y += 6f;

                if (!LmStudioProbe.Enabled)
                {
                    DrawRow(x, ref y, contentWidth, "LM Studio", "disabled", TextLabel);
                    DrawRow(x, ref y, contentWidth, "", "enable in settings", TextLabel);
                    DrawRow(x, ref y, contentWidth, "", "", TextLabel);
                }
                else if (LmStudioProbe.IsRemote)
                {
                    DrawRow(x, ref y, contentWidth, "LM Studio", "remote host", AccentYellow);
                    DrawRow(x, ref y, contentWidth, "Model",
                        LmStudioProbe.Reachable ? TruncateModel(LmStudioProbe.ModelName) : "—", TextValue);
                    DrawRow(x, ref y, contentWidth, "", "(not local VRAM)", TextLabel);
                }
                else if (LmStudioProbe.Reachable)
                {
                    DrawRow(x, ref y, contentWidth, "Model",
                        TruncateModel(LmStudioProbe.ModelName), TextValue);
                    DrawRow(x, ref y, contentWidth, "Est. VRAM",
                        $"~{LmStudioProbe.EstimatedVramMb / 1024f:F1} GB", TextValue);
                    DrawRow(x, ref y, contentWidth, "Endpoint", "connected", AccentGreen);
                }
                else
                {
                    DrawRow(x, ref y, contentWidth, "LM Studio", "offline", AccentOrange);
                    DrawRow(x, ref y, contentWidth, "Endpoint", "unreachable", AccentOrange);
                    DrawRow(x, ref y, contentWidth, "", "", TextLabel);
                }
            }

            // ── Developer section (full GPU hardware stats) ──
            if (_mode == OverlayMode.Developer)
            {
                y += 2f;
                var divRect2 = new Rect(x, y, contentWidth, 1f);
                GUI.DrawTexture(divRect2, _barBgTex);
                y += 6f;

                DrawRow(x, ref y, contentWidth, "GPU",
                    NvidiaSmiReader.GpuName, TextValue);

                DrawRow(x, ref y, contentWidth, "Driver",
                    NvidiaSmiReader.DriverVersion, TextValue);

                int tempC = NvidiaSmiReader.TemperatureC;
                Color tempColor = tempC < 60 ? AccentGreen
                    : tempC < 75 ? AccentYellow
                    : tempC < 85 ? AccentOrange : AccentRed;
                DrawRow(x, ref y, contentWidth, "Temp",
                    $"{tempC}°C", tempColor);

                float pw = NvidiaSmiReader.PowerDrawW;
                float pwLim = NvidiaSmiReader.PowerLimitW;
                float pwPct = pwLim > 0 ? pw / pwLim : 0f;
                Color pwColor = pwPct < 0.7f ? AccentGreen
                    : pwPct < 0.9f ? AccentYellow : AccentOrange;
                DrawRow(x, ref y, contentWidth, "Power",
                    $"{pw:F0} / {pwLim:F0} W", pwColor);

                DrawRow(x, ref y, contentWidth, "GPU Clock",
                    $"{NvidiaSmiReader.GpuClockMhz} MHz", TextValue);

                DrawRow(x, ref y, contentWidth, "Mem Clock",
                    $"{NvidiaSmiReader.MemClockMhz} MHz", TextValue);

                int fan = NvidiaSmiReader.FanSpeedPercent;
                Color fanColor = fan < 50 ? AccentGreen
                    : fan < 75 ? AccentYellow : AccentOrange;
                DrawRow(x, ref y, contentWidth, "Fan",
                    $"{fan}%", fanColor);

                int util = NvidiaSmiReader.UtilizationPercent;
                Color utilColor = util < 50 ? AccentGreen
                    : util < 80 ? AccentYellow : AccentOrange;
                DrawRow(x, ref y, contentWidth, "GPU Util",
                    $"{util}%", utilColor);
            }
        }
    }
}
