# NVIDIA Tool Overview

NVIDIA GPU Monitor makes your GPU visible while you play: what it is doing, and where VRAM is going.

---

## What it shows you

**Live GPU statistics** — VRAM in use, temperature, utilisation, power draw, clocks and fan speed, refreshed on an interval.

**A VRAM breakdown** — rather than one total, an estimate split into System/desktop, RimWorld itself, and (optionally) a local LM Studio model. A model that "fits" until you raise the context window is a common reason a setup stops working, and this is where you see that coming.

**A VRAM advisory** — a heads-up on colony load when you are approaching your card's limit, with practical suggestions.

**Optional LM Studio awareness** — when enabled, the loaded model name (read directly from your LM Studio endpoint) and an estimate of its VRAM footprint. A remote host is detected and never counted as local VRAM. This is opt-in and needs no other mod.

---

## Where to find it

**The on-screen overlay** is a compact heads-up display. It is **off by default** — toggle it from the toolbar button (bottom-right play settings row) or in mod settings. Cycle it Off → Basic (VRAM) → LM Studio → Developer (full hardware).

**The dashboard window** is the full view: GPU hardware stats and, when enabled, LM Studio status. Open it from mod settings.

---

## How it reads the GPU

- **NVML** — NVIDIA's management library (`nvml.dll`, shipped with every NVIDIA driver), called directly via P/Invoke. No process spawning and no shell commands, so it is Workshop-safe.

If NVML is not available — no NVIDIA card, or drivers absent — the mod does not guess. It reports nothing rather than plausible-looking numbers, and logs a single line saying so.

See [Troubleshooting](Troubleshooting) if you see loader messages about NVML in your log at startup. They are expected on machines without it and are not an error.

---

## What it does not do

- It does not change your model, context window, or any setting. The advisory is advice; acting on it is yours.
- It does not manage VRAM or free memory.
- It does not monitor non-NVIDIA GPUs.
- It adds no gameplay of its own, and keeps zero save-file footprint (safe to add or remove from any save).

---

## Performance cost

GPU stats are polled on a background thread on an interval, so the cost to frame time is negligible. The optional LM Studio probe makes a short HTTP call to your endpoint on its own interval and degrades quietly if the endpoint is unreachable.
