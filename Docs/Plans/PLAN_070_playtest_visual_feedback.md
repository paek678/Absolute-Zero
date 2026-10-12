# PLAN 070 — Playtest visual and feedback corrections

User request: inspect first-turn remote fan; compact/top-align the duel room; keep item artwork above the floor in every mode; investigate failed mini-game followed by item use; remove item diagnostic names and Multi quantities; translate phase headings.

| Task | Owner and finding | Validation |
|---|---|---|
| T1 Room layout | CasualLobbySkin / LobbyRoomView; hidden join controls leave a fixed gap, duel reserves four-row space | Real room capture, two/four rows do not overlap controls |
| T2 Item text/phase | GameplayDebugDisplay, ItemWorldView, MultiPerspectiveLayout, MatchHudPresenter | Clean labels absent, phase text Korean in runtime |
| T3 Ground contact | InventoryPresenter has no duel/solo floor clearance; render facing/hover must preserve contact | All three modes, actual sprite corners above floor after render alignment, distribution and hover |
| T4 Mini-game consistency | Server failure consumes one use and returns before queue; FullMatchPlaytestDriver submits modeled results while real UI remains running, cancellation displays false failure | Modeled success/failure through one UI submission path; stale/cancel silent; failure not queued/used, server ticket regression |
| T5 First-turn fan | Trace initial IsFanActive and binding/rotation before first ready; do not force spin against authoritative stopped state | First prep before ready and after ready, local/remote, solo/duel |

Use existing server authority and turn ownership. No balance changes, sprite changes, forced compilation, scene YAML edits or network API expansion. Development-only automation may be corrected but must remain excluded from release. Preserve unrelated dirty files. Record observed results and any remaining gaps in the corresponding validation note.

Implementation and focused validation completed on 2026-10-12. See [results and deferred scope](../Validation/PLAN_070_playtest_visual_feedback.md). The reported first-turn fan defect was not reproduced; active/ready state and real blade rotation were checked rather than claiming a confirmed underlying state fix.
