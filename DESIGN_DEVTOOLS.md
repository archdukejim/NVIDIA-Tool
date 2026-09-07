# NVIDIA GPU Monitor — Design Notes

## Overview
A standalone, general-purpose GPU toolkit for RimWorld. It surfaces the local NVIDIA
GPU's health inside the game UI. It has **no dependency on any other mod** (only Harmony),
keeps **zero save-file footprint**, and does nothing on non-NVIDIA hardware but stay quiet.

Formerly "RimSynapse - NVIDIA Tool"; the RimSynapse-specific LLM tooling (request queue,
throttle, token metrics, per-mod stats, context embedding) was removed when the mod became
standalone. RimSynapse Core now owns cross-vendor VRAM monitoring natively, in a different
way (a PDH-based meter and its own advisory).

## Architecture

- **`NvidiaSmiReader`** — background thread that reads GPU stats via NVML P/Invoke
  (`nvml.dll`). Probes for the library with a quiet Win32 `LoadLibrary` first to avoid
  Mono loader spam on machines without an NVIDIA driver. Exposes VRAM, temp, power, clocks,
  fan, utilisation, and (where NVML permits) per-process VRAM.
- **`NvmlBindings`** — the raw NVML P/Invoke declarations and `GpuProcessInfo`.
- **`LmStudioProbe`** — optional, opt-in. A background thread that GETs `{endpoint}/v1/models`
  to read the loaded model and estimate its VRAM from the parameter count. Detects a remote
  host so its memory is never attributed to the local GPU. Talks to LM Studio directly — no
  Core, no other mod.
- **`VramBreakdown`** — splits measured used-VRAM into System / RimWorld (Unity texture
  memory) / LM Studio (from the probe).
- **`VramWarning`** — the on-load VRAM advisory. Defers (stays silent) when another loaded
  mod already ships its own advisory, matched by packageId with no compile-time dependency.
- **`OverlayHud` / `OverlayHud_Rendering`** — the toggleable on-screen HUD (Off → Basic →
  LM Studio → Developer), driven by a Harmony postfix on `GameComponentUtility.GameComponentOnGUI`
  (no GameComponent, so no save entry).
- **`ToolbarToggle`** — the play-settings toolbar button.
- **`DevToolsWindow` / `_Sections`** — the dashboard window (GPU + optional LM Studio).
- **`DevToolsMod` / `DevToolsSettings`** — mod entry point and persisted settings.
- **`NvidiaMonitorDebugActions`** — `[DebugAction]`s under the "NVIDIA Monitor" category for
  headless validation (dump GPU stats, dump/force the VRAM breakdown/advisory, probe LM Studio,
  cycle overlay).

## Non-goals
- No control of models or settings; the advisory is advice only.
- No VRAM management or allocation.
- No non-NVIDIA GPU support.
- No gameplay.
