# NVIDIA GPU Monitor

Welcome to the documentation for **NVIDIA GPU Monitor**, a standalone in-game GPU toolkit for RimWorld.

It reports what your NVIDIA GPU is doing — VRAM, temperature, utilisation, power, clocks and fan — without leaving RimWorld. If you run a local language model (LM Studio, and mods such as RimTalk or the RimSynapse suite), it can optionally show the loaded model and estimate its VRAM footprint too.

## Table of Contents

- [NVIDIA Tool Overview](NVIDIA_Tool_Overview)
- [Reading the GPU Readout](Reading_the_GPU_Readout)
- [VRAM Planning](VRAM_Planning)
- [Troubleshooting](Troubleshooting)

---

## Before you start

- **This is a monitoring tool.** It reads GPU state and reports it. It does not change any settings, allocate memory, or alter how any other mod behaves.
- **No dependencies.** It requires only Harmony. It does not need RimSynapse, RimTalk, or any other mod.
- **NVIDIA hardware only.** Readings come from NVIDIA's own management library (NVML). On other GPUs the mod loads and stays quiet rather than reporting wrong numbers.
- **The overlay is off by default.** Turn it on from the toolbar button or in mod settings.
- **LM Studio awareness is optional and off by default.** Enable it in settings and point it at your endpoint (default `http://localhost:1234`) to see the loaded model. If your model runs on another machine, the tool detects the remote host and never counts its memory against your local GPU.
- **Plays nicely with others.** If another loaded mod already shows its own on-load VRAM advisory (e.g. RimSynapse Core), this tool steps aside from the duplicate popup and keeps its overlay and dashboard.
