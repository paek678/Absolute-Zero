# Design Review — Absolute Zero

> Design document analysis. Issues that need clarification before implementation.
> Last reviewed: 2026-07-14

---

## A. Internal Contradictions (must resolve)

### A-1. Smartphone contradicts consumable definition
- **Rule**: "Random consumables are destroyed on use"
- **Smartphone**: "Can be used 3 times. Recovery increases with each use" (exact values not defined)
- **Problem**: Multi-use item conflicts with single-use consumable definition.
- **Decision needed**: Is Smartphone a special exception? Does it occupy the slot for 3 turns? Does it have a unique item type?

### A-2. Item slot display vs max capacity mismatch
- **Section 2-⑥**: "4 basic item slots on left, 4 random item slots on right" (8 total displayed)
- **Section 2-③**: "Max 8 random items (excluding basic items)"
- **Problem**: 4 display slots for random items, but up to 8 can be held. How are items beyond 4 displayed? Scrolling? Second row? Page system?

### A-3. "Per game" scope ambiguous
- Basic consumable items (Warm Tea, Cat) refresh "every game" (매 게임)
- **Problem**: Does "game" mean each round (of best-of-3) or the entire match?
- This significantly affects balance: if Tea refreshes every round, recovery is abundant.

---

## B. Undefined Edge Cases (should define)

### B-1. Simultaneous Ready press
- Actions execute in "Ready press order"
- **Problem**: What happens if both players press Ready on the exact same network frame?
- **Suggestion**: Use server-received timestamp. If truly simultaneous (same tick), random coinflip or simultaneous resolution.

### B-2. Heat Wave Warning + same temperature
- "Lower-temp player acts first"
- **Problem**: What if both players have identical temperature?
- **Suggestion**: Fall back to Ready-press order, or coinflip.

### B-3. Both players reach 0° simultaneously
- Section 9 says "TBD" — needs definition.
- **Suggestion options**: Higher final temp wins / Ready-press order decides / Draw (both lose the round)

### B-4. Both players defend
- Section 3-①-2 only covers "one side defends"
- **Problem**: If both use defensive items, what happens? Nothing? Both heal?
- **Suggestion**: Both defense effects apply to self. No attacks to block → defense still grants its recovery value.

### B-5. Defense vs food items
- Windbreaker: "blocks temperature attacks"
- Mask: "blocks food items 100%"
- **Problem**: Does Windbreaker block Samgyetang (food-type debuff that includes temperature change)? Or only pure temperature attacks?
- **Suggestion**: Windbreaker blocks temperature-category attacks only. Mask blocks food-category effects only. Item categories must be clearly tagged.

### B-6. Ambulance environment timing
- "Activates at 4th Prep Time"
- **Problem**: In best-of-3, a round may end in 2-3 turns. The 4th Prep Time may never occur.
- **Question**: Is this the 4th prep turn of the current round, or across the entire match?

### B-7. Mini-game failure cascade
- Failing mini-game → item destroyed → must choose another item.
- **Problem**: If new item also requires mini-game, and player fails again, is that item also destroyed? Can this chain until no items remain?
- **Problem**: With 10-20 second prep time (or 10s in Summer Vacation), mini-game retry time may be insufficient after failure.

### B-8. Tarot Card + mutual reveal paradox
- Tarot Card: "can be used after opponent presses Ready"
- **Problem**: If both players are waiting for the other to press Ready first (to use Tarot), deadlock until timer expires.
- **Note**: This may be intentional design tension. Timer expiration resolves the deadlock.

---

## C. Balance Observations (informational)

### C-1. Item numeric values — RESOLVED
- ~~No specific damage/heal values are defined in the design doc~~
- **All 21 item values now confirmed** (2026-07-14) — see `GAME_DESIGN.md` Item tables
- Remaining issue: Windbreaker defense value "4" interpretation unclear (Q34)

### C-2. Sabotage item stacking concern
- Cat (basic): rerolls ALL opponent random items — can destroy opponent's saved strategy
- Red Card: completely neutralizes opponent's turn
- Blue Tape: blocks basic items next turn
- **Note**: Multiple sabotage items stacking could create "unfun" turns where a player is completely locked out

### C-3. Hug T-shirt high-variance concern
- "Opponent temp = my temp" — when used at low temp vs high temp opponent, can create massive swing
- Balanced by mini-game requirement and random drop, but may still feel unfair

---

## D. Code vs Design Mismatches (current implementation gaps)

| Feature | Current Code | New Design | Change Needed |
|---------|-------------|------------|---------------|
| Action resolution | Simultaneous | Ready-press order (sequential) | Major refactor |
| Round system | Single continuous game | Best of 3 | New: MatchManager |
| Action types | Attack/Defend/Charge (3 fixed) | Item-based system (20+ items) | Full rewrite |
| Ready button | Action select = prep end | Separate "Ready" button, can select then wait | New mechanic |
| Temperature recovery | None after action select | Recovers after pressing Ready | New mechanic |
| Mini-games | None | Item-specific mini-games | Entire new system |
| Environment variables | None | 7 random environment modifiers | New system |
| Random item drops | None | Temperature threshold triggers | New system |
| Item inventory | None | 4 basic + up to 8 random slots | New UI + data |
| Visual effects | None | Freeze, shatter, screen edge ice | VFX system |
| Buff/Debuff system | None | Multi-turn effects | New system |

### Architecture Impact Assessment
The new design requires these major system additions:
1. **MatchManager** — best-of-3 round tracking, round reset, match result
2. **ItemSystem** — item definitions (ScriptableObject), inventory, consumption, categories
3. **MiniGameSystem** — mini-game framework, per-item mini-game implementations
4. **EnvironmentSystem** — random environment selection, modifier application
5. **BuffDebuffSystem** — multi-turn effect tracking, per-turn application
6. **ReadyOrderTracker** — server-side timestamp tracking for action execution order
7. **Enhanced UI** — item slots, item detail view, mini-game UI, freeze/shatter VFX
