# PLAN075 — Reuse duel item choreography in Multi

## User decision and scope
The 2026-10-12 request requires Multi to use the existing duel item performance,
including the gesture and impact timeline, rather than only assigning a hand sprite.
PLAN074 fixed sprite ownership and stale animation triggers; those fixes remain.

| Requirement | Owner/change | Evidence |
|---|---|---|
| Reuse duel ordinary-item performance | ItemPresentationSequence.PlayAnimatedItem, called by both modes | Actual duel and four-peer animation, pose and timeline samples |
| Preserve authored special performances | Existing shared ContactItemChoreography (hug/feed) and CatItemChoreography | No new substitute gestures or camera zooms |
| Correct spectator perspective | Multi reactions use actual target seat; screen hit only for local victim | Four viewing peers |
| Keep gameplay authority | Outcome callbacks retain mode-specific replicated results; no inventory, RPC or phase writes | Source diff and settlement checkpoints |

## Implementation
Extract the duel's normal animator/FPS start, authored sound selection, effect delay,
hit interval, remaining animation tail and idle restoration into one routine.
Both modes call it; only outcome application and victim reactions remain mode-specific.
Preserve shared feed/hug/cat choreography and the existing no-animation fallback.
Bring the duel's delayed Buldak sound and victim screen feedback into Multi.
Use finally for gesture cleanup under cancellation; preserve presentation guards.

## Validation
Compile in the connected project and build at the existing allowed executable path.
Run deterministic real Relay duel and four-player sequences (fan, tea, hand fan,
defense, feeding, fan after reaction). Check identity, moving poses, impact timing,
settlement equality and console failures. Review sequential screenshots, not just
the held sprite. Record evidence and remaining visual review limitations in results.

## Non-goals
No new animation art, invented attack motion, balance changes, camera zoom for all
attacks, multiplayer seat changes, layout changes, or replacement of approved feed reactions.

## Follow-up decision: only the basic fan may swing
The user clarified that only Fan should play a swinging gesture. Audit found that
HandFan.asset explicitly requested `swing` for remote characters, including duel,
and AZPlayerVisual's missing-`fan` fallback also mapped to `swing`.
Change both to the existing forward-use `attack` gesture. Retain FPS `fan`.
Audit every item SO against the real duel and Multi controllers; verify the actual
remote/FPS clips in disposable Play Mode copies, then replay live Hand Fan turns.
This user decision overrides the old shared setting rather than copying its defect.
