# Changelog

Full version history for NVIDIA GPU Monitor. The mod page and Workshop description show only the latest release; every earlier version is recorded here.

## v1.0.0 - Standalone
- **Now a standalone, general-purpose GPU tool.** Dropped the RimSynapse branding and the hard dependency on RimSynapse Core. The mod is renamed to **NVIDIA GPU Monitor** (packageId `GpuTools.NvidiaMonitor`) and requires only Harmony. GPU monitoring, the overlay, the dashboard, and the VRAM advisory all work with no other mod installed.
- **LM Studio awareness is now optional and self-contained.** It talks to your LM Studio (OpenAI-compatible) endpoint directly over HTTP — no Core, RimTalk, or other mod required — so anyone running a local model can use it. Off by default; enable it and set your endpoint in mod settings. A remote host is detected and never counted as local VRAM. The default endpoint is `127.0.0.1` and a `localhost` endpoint that fails is auto-retried over IPv4, working around the Windows quirk where `localhost` resolves to IPv6 (`::1`) but LM Studio binds IPv4 only.
- **Removed the RimSynapse-specific dashboard.** The LLM request queue, throttle, token metrics, registered-mods, and context-embedding sections were tied to Core's pipeline and are gone. The dashboard now covers GPU hardware and (optionally) LM Studio status.
- **Conflict-free alongside Core.** RimSynapse Core (and any mod that ships its own on-load VRAM advisory) is detected at load, and this tool suppresses its own duplicate advisory popup while keeping the overlay and dashboard.
- Overlay modes are now Off → Basic (VRAM) → LM Studio → Developer (full hardware).

## v0.7.1 - VRAM Accuracy and Quieter Startup
- Fixed - VRAM breakdown reconciles. The "VRAM Status" popup previously summed the full LM Studio estimate - including the portion the estimator assumes is offloaded to system RAM - and attributed models running on a remote LM Studio host to local VRAM, so the components did not add up to the reported total. The dialog now counts only GPU-resident memory: the offloaded LM Studio portion is labelled as system RAM rather than VRAM, and a remote host contributes zero local VRAM instead of a phantom amount. Estimated lines are marked with a tilde so measured and estimated figures are no longer conflated. (#15)
- Fixed - Quieter startup. On a machine without a resolvable NVML library, the tool no longer emits roughly 16 "Fallback handler could not load library" lines to Player.log before it can log anything of its own. It now probes for the library once via the Windows loader and, when NVML is absent, logs a single line noting that GPU VRAM advisories are unavailable. VRAM advisories are unaffected where NVML is present. (#13)
- Requires Core v0.7.0; saves and settings carry over unchanged.

## v0.7.0 - Regions and Territories Compatibility
- Moves in step with RimSynapse Core v0.7.0.
- Requires Core v0.7.0; saves and settings carry over unchanged.

## v0.6.1
- Fixed - mod list metadata: the in-game mod list still showed v0.5.2 with no v0.6.0 notes. Version and changelog now agree in every place they are stated.
- Roadmap updated: 0.7 is now Regions and Territories compatibility - the groundwork the Factions work depends on. Everything after it shifts up one release.

## v0.6.0
- Requires RimSynapse Core v0.6.0. This release moves in step with Core's Agent and Tool Foundation update - your saves and settings carry over unchanged.
- Documentation: in-game wiki guides updated; "MCP" renamed to game tools throughout, matching Core's native tool-calling engine.

## v0.5.2
- Maintenance release: no gameplay changes. Version aligned with the rest of the RimSynapse suite, which carries fixes in Core and Psychology.
- Licence: now PolyForm Noncommercial 1.0.0. Free to use, modify and share for any noncommercial purpose.

## v0.5.1
- Playtest improvements: general stability enhancements and compatibility optimizations for playtesting.

## v0.4.0
- Updated to support RimSynapse Core v0.4.0 (Multi-provider routing and Image generation).
