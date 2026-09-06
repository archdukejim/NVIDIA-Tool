# NVIDIA GPU Monitor

A standalone, in-game GPU toolkit for RimWorld. It renders your NVIDIA GPU's VRAM,
temperature, utilization, power, clocks, and fan directly inside RimWorld's UI —
with a toggleable overlay, a dashboard window, and an on-load VRAM advisory.

No dependency on any other mod. Optional LM Studio awareness (off by default) lets it
show a locally-loaded model and estimate its VRAM — handy for RimTalk, the RimSynapse
suite, or anyone running a local LLM alongside the game.

Requires Harmony and an NVIDIA GPU (reads NVML / `nvml.dll`, shipped with every NVIDIA
driver). On machines without an NVIDIA driver it stays quiet.
