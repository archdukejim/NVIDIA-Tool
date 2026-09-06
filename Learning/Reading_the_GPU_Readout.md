# Reading the GPU Readout

What the numbers mean, and which ones are worth reacting to.

---

## GPU statistics

**VRAM used / total** — the single most important number when running a local model. Once a model no longer fits, it either fails to load or spills into system memory and becomes dramatically slower. This figure is **measured** (from NVML).

**Utilisation** — how busy the GPU is. If you are driving it with a local LLM, this is spiky rather than steady: near zero between requests, high during one. **A low average is normal** and does not mean the model is idle or misconfigured.

**Temperature** — thermal throttling shows up here before it shows up as slow responses. A card that is hot and slow is being throttled, not overloaded.

**Power draw** — mostly useful as corroboration. Power tracking utilisation confirms the GPU is genuinely working rather than waiting on something else.

**Clocks and fan** (Developer overlay mode) — supporting detail for diagnosing throttling: a card that has dropped its clocks under load and pinned its fan is thermally or power limited.

---

## The VRAM breakdown

Rather than one used-VRAM total, the overlay and the on-load advisory show an estimated split:

- **System / Desktop** — everything else on the card: the compositor, other apps, browser tabs with hardware acceleration. On a single-card machine this is never zero.
- **RimWorld** — estimated from Unity's own texture-memory tracking. RimWorld draws the game on the same GPU.
- **LM Studio** *(optional, only when you enable it)* — an estimate of a locally-loaded model's footprint, from its parameter count. A model on a **remote** host is labelled as such and contributes nothing to local VRAM.

The **used / total** figure is measured; the split is an **estimate** (lines marked `~`). They are for reasoning about proportions, not exact accounting.

---

## Reading it in practice

**VRAM near the limit and a local model suddenly much slower.** The model has likely spilled out of VRAM. Reduce the context window or use a smaller model — see [VRAM Planning](VRAM_Planning).

**Response times crept up over a long session, temperature is high.** Thermal throttling. It is a cooling problem, not a configuration one.

**Utilisation looks idle but the model is clearly working.** Normal — inference is spiky and a periodic poll frequently lands between requests. Judge by whether VRAM and temperature look healthy.

**Nothing is reported at all.** See [Troubleshooting](Troubleshooting).
