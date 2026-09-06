# Troubleshooting

---

## A line about NVML being unavailable appears in my log

**This is expected on a machine without an NVIDIA driver, and nothing is broken.**

The mod reads the GPU through NVML (`nvml.dll`, which ships with the NVIDIA driver). On a machine where that library is not present, the mod logs a single explanatory line:

> nvml.dll not found — GPU VRAM advisories unavailable (expected on machines without an NVIDIA driver).

It probes for the library quietly first (via the Windows loader), so a missing driver does **not** produce a wall of "Fallback handler could not load library" lines. If you are on an NVIDIA machine and still see this, your driver install is the place to look.

---

## No GPU statistics at all

In rough order of likelihood:

- **Not an NVIDIA card.** This mod reads NVIDIA's management library (NVML) only. It stays quiet rather than reporting numbers it cannot verify. It does not monitor AMD or Intel GPUs.
- **Driver problem.** NVML ships with the NVIDIA driver. Running `nvidia-smi` yourself in a terminal is a quick sanity check — if that fails, the mod cannot read anything either, so fix the driver first.
- **Overlay is off.** It is off by default. Toggle it from the toolbar button (bottom-right play settings row) or mod settings — the dashboard window works regardless.

---

## Numbers look implausible

**VRAM higher than expected** — other applications share the card, and RimWorld is drawing the game on it too. The total is the card's, not any one model's.

**Utilisation near zero with a model clearly working** — normal. Inference is spiky; a periodic poll frequently lands between requests. Judge by VRAM and temperature rather than instantaneous utilisation.

**Breakdown does not sum to the reported total** — the breakdown is an *estimate* of where memory is going (System / RimWorld / an optional LM Studio model). The total is measured. They will not agree exactly; the breakdown is for reasoning about proportions rather than accounting.

---

## LM Studio shows as offline or the model is blank

The LM Studio feature is **optional and off by default**. If you enabled it:

- Check the **endpoint** in mod settings (default `http://127.0.0.1:1234`) matches where LM Studio is serving.
- **On Windows, use `127.0.0.1` rather than `localhost`.** Windows often resolves `localhost` to IPv6 (`::1`) first, but LM Studio binds to IPv4 (`127.0.0.1`) only, so a `localhost` endpoint can fail to connect. The tool auto-retries `localhost` against `127.0.0.1`, but setting `127.0.0.1` directly avoids the problem entirely.
- Make sure a model is actually loaded in LM Studio and its server is running.
- A **remote** endpoint (not localhost) is detected and shown as remote; its memory is intentionally not counted against your local GPU.

---

## The overlay is in the way

Toggle it from the toolbar button, or drag its header to reposition it. The dashboard window carries the same GPU information without occupying screen space.

---

## Reporting a problem

Include your GPU model, driver version, the output of running `nvidia-smi` yourself, and the `Player.log` from the run. The last of these usually settles whether the mod could not read the GPU or read it and reported something you did not expect — which are different problems.
