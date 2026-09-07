# VRAM Planning

Why the breakdown is split rather than shown as one total, and how to act on it. This is mainly for people running a local language model (LM Studio, and mods such as RimTalk or the RimSynapse suite) alongside RimWorld — the case where VRAM gets tight.

---

## Why a breakdown

A used-VRAM total tells you *how much* is gone, not *where it went*. The split separates the parts you can act on from the parts you cannot:

- **System / Desktop** — the compositor, other apps, hardware-accelerated browser tabs. Closing GPU-heavy background apps frees this.
- **RimWorld** — the game drawing itself on the same card. Lower graphics settings trim it a little.
- **LM Studio model** *(optional)* — the model's estimated footprint, from its parameter count. This is usually the biggest single lever.

On a single-card machine you are sharing the GPU between the game and the model, so a model that "fits" in isolation can still push you over once RimWorld and the desktop are accounted for.

---

## The usual failure

A local model loads fine, works for a while, and then responses slow dramatically or stop. Almost always this is memory: as the conversation grows, the model's KV cache grows with it until the total no longer fits and the runtime spills into system memory. Spilled inference is not slightly slower — it is slower by an order of magnitude.

The measured **used / total** VRAM figure is what to watch: as it climbs toward your card's limit, you are approaching the spill. The advisory warns you before you cross it, because the symptom of crossing it is everything becoming slow rather than an obvious error.

---

## What to change, in order

**Reduce the model's context window first.** It is the biggest lever on the KV cache and the least destructive. Many RimWorld LLM setups are designed to work down to modest context windows.

**Then consider a smaller or more heavily quantised model.** A quantised model that fits comfortably will beat a larger one that spills, every time.

**Then look at what else is on the card.** Other applications, browser tabs with hardware acceleration, and RimWorld itself all take a share.

---

## Warnings are advice

Nothing is changed for you, no request is blocked, and no setting is adjusted. If you know why you are close to the limit and it is fine, ignore them. (If another mod already shows its own VRAM advisory on load, this tool stays silent so you do not get two.)

---

## If the model runs on another machine

None of this applies to the machine running RimWorld. Point the optional LM Studio feature at the remote endpoint and it is detected as remote — the readout shows a largely idle local GPU, which is correct, since the model's memory is on the other machine.
