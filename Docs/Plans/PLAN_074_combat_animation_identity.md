# PLAN074 — Correct combat animation and prop identity

User reports intermittent damage gestures while attacking and Fan actions showing a tea cup in Multi.

- Trace the production animation controller, delayed reaction cleanup, presentation identity and per-renderer texture binding.
- Reproduce using actual four-peer item sequences; record requested item, current clip, sprite and texture per actor.
- Fix only demonstrated presentation ownership conflicts. Keep item effects, target selection, phase timings, inventory authority, art and controller assets unchanged unless evidence requires an asset correction.
- Verify hit-to-action, tea-to-fan and alternating actors, with host/three clients; regression-check duel/FPS and record visual captures.

Ledger: `Docs/Validation/PLAN_074_results.md`.
