# PLAN053 - Round ribbon, participant scores and center-facing items

## User request (2026-10-07)
- Put the round ribbon above the participant name cards.
- Put each participant's score inside their card. Preserve actual scoring: Multi kills,
  Duel/Solo round wins. No gameplay rule or authority changes.
- Orient Multi inventory artwork towards the central icebox; keep uses upright and
  local item hitboxes/selection outlines aligned. Optional direction clarification pending;
  proceed with sprite tops facing the box as the stated default.
- Provide ten real Korean font specimens for selection. Do not replace the production
  font until the user chooses. Check publisher usage conditions before suggesting candidates.

## Scope
HUD builder/refs/presenters + focused banner tests. Multi inventory presentation and
ItemWorldView facing only; preserve 1v1/Solo item layout, remote seat mapping, inventory
copies, authority, damage and VFX timing. Font candidates stay in output, outside builds.

## Verification
- Compile and focused Editor tests; inspect real HUD component captures.
- Real Relay Multi layout/input fixtures and 2-seat regression where relevant.
- Inspect center-facing icons, their counts, all four seat views, and 8/12-item inventories.
- Record changes, specimen sources, screenshots and precise limitations in this plan.

## Implementation and results (2026-10-07)

- RoundRibbon is now above the participant cards. Each card owns its ScoreLabel;
  the presenter reads Multi KillScores or Duel/Solo round wins without changing rules.
  Missing participants/data clear their score instead of retaining stale text.
- MultiItemFacing projects the icebox direction onto the captured seating camera plane.
  Both local and remote sprite tops point towards the shared icebox position. Local
  colliders, selection outlines and ban overlays rotate with their artwork; remaining-use
  labels counter-rotate and stay readable. Inventory refreshes reuse the seating view,
  so a combat camera close-up cannot change the reference direction.
- Duel/Solo inventory orientation and all match authority/score values are unchanged.
- The first banner check detected insufficient score/name glyph clearance. Moving the
  seat/score row from Y 27 to 28.5 fixed it without weakening the assertion.

| Verification | Result | Evidence under output/validation/plan053/runtime |
| --- | --- | --- |
| Actual-font banner tests: 2/3/4 seats, glyph bounds, score binding, re-enable/departure | 6/6 passed | tests-banner-final.json |
| Center-facing directions and preserved sprite plane | 5/5 passed | tests-facing.json |
| Development Player build | Succeeded, 0 errors / 4 warnings | build.json |
| Actual Relay four-player input | Passed | relay/input-4-w0/report.json |
| Actual Relay four-player layout, 8 items per seat | Passed | relay/layout-4-w0/report.json |
| Actual Relay four-player layout, 12 items per seat | Passed | relay/layout-4-w1/report.json |
| Actual Relay duel input regression | Passed | duel/input-2-w0/report.json |

The four Relay cases used 14 player processes in total. All four seat captures of the
8-item layout and the host 12-item capture were visually inspected. HUD preview uses
the real Unity HUD builder with sample names/scores/action states, not a gameplay result.
The runtime captures use a camera/root-canvas RenderTexture at 960x540.

### Limits and observations

- The generic layout probe projects the union of world-axis-aligned renderer bounds.
  Its `fullyInside` is false for some bottom-row local items after rotation, although
  the inspected visible art/counts are inside the capture and collider bounds remain
  inside. Do not interpret this conservative bound as a pixel-level clipping test or
  claim that every renderer-bound check passed.
- Long matches, every aspect ratio and every animated item combination were not rerun.
- The extra character fragment below the top HUD also exists in earlier captures;
  it is outside this layout/font-selection change and has not been diagnosed here.
- Production Dongle Bold is retained pending font selection. Its unusually tall,
  disconnected Korean letter forms remain visible in these runtime screenshots.
- The prior four manual test windows were closed to rebuild the same approved executable.
  Automated test processes exited. Play-4P-Clean.cmd launches the newly built version.

## Font selection deliverables

Ten actual font files were used to render the same Korean UI specimen at large and
small sizes. These are font comparison images, not Unity gameplay captures. Candidates
were kept outside production until the subsequent user selection below.

1. Baemin Jua Regular
2. NanumSquareRound ExtraBold
3. MapleStory Bold
4. Cafe24 Ssurround Bold
5. Jalnan 2 Regular
6. LINE Seed KR Bold
7. Gmarket Sans Bold
8. Cafe24 Ohsquare (Anemone) Regular
9. Baemin Do Hyeon Regular
10. Gugi Regular

Artifacts: `output/validation/plan053/font-options/comparison-01.png`,
`comparison-02.png`, ten individual specimens, `font-metadata.json`, `validation.json`,
the rendering script and publisher license files. Metadata records the official
download/license sources and supported glyphs. For 01/04/08/09/10, only missing
degree/middle-dot symbols use the shared NanumSquareRound fallback; Korean sample
letters use each candidate's own glyphs. All 12 PNGs passed bounds/decode validation.

## Preserved local work

The existing MalgunGothic SDF and SoloDuelDecision graph hashes still match the task
baseline. Unrelated dirty work was preserved. No commit or push was requested.

## User selection applied: 07 Gmarket Sans Bold (2026-10-07)

The user selected the Gmarket specimen. The shared participant HUD now uses native
Gmarket Sans TTF Bold for nicknames, seat identity, personal scores, round ribbon and
first/current-action badges in every mode. Point sizes and text rectangles were tuned
to Gmarket's full-height Korean glyphs. Native Bold is used without synthetic bold or
extra material face dilation; existing pastel colors, borders and title gradient remain.

- Unmodified source: `Assets/Fonts/GmarketSans/GmarketSansTTFBold.ttf`.
- Main font: `Assets/Resources/UI/Fonts/GmarketBanner.asset`, static 153-character seed,
  48-point sampling / 5 padding / 1024 atlas.
- Same-family dynamic fallback: `GmarketBannerDynamic.asset`, 1024 multi-atlas with
  Clear Dynamic Data On Build, followed by the existing general fallbacks.
- Exact embedded license, including its additional clause 6, extracted into
  `Assets/StreamingAssets/ThirdPartyLicenses/GmarketSans-LICENSE.txt`. The copied license
  exists in the finished Player. Official source: https://corp.gmarket.com/fonts/.
- Scope is the top participant/round HUD; unrelated menus and item-count fonts are
  unchanged. Gameplay and networking code were not edited for this font selection.

### Selection validation

- `Plan047BannerTests`: 6/6 passed for 2/3/4-seat bounds, eight-character Korean names,
  local ghost seat labels, score spacing, all action captions and score binding.
- Real Unity HUD-component screenshot: `output/validation/plan053/gmarket/hud-preview.png`.
  Sample names/action states are deliberately set for typography comparison.
- Independent read-only review found no blocking font/clipping/fallback issue.
- Player build succeeded, 0 errors / 487 warnings (including Sentis shader warnings,
  absent Player Pipeline config and the existing Malgun importer warning). The previous
  Malgun asset and Solo graph hashes remain identical; warnings were not erased.
- Actual Relay four-player input scenario passed with all four processes; host gameplay
  capture inspected: `output/validation/plan053/gmarket/runtime/input-4-w0/host.input-aim-1.png`.
- Evidence: `output/validation/plan053/gmarket/tests.json`, `build.json`, and the runtime
  `report.json`. The existing approved Player executable and Play-4P-Clean.cmd now use
  the selected font. Tests ended; Editor remains clean LobbyScene/EditMode.
- A new long-match/duel/Solo Player regression was not run for this typography-only
  follow-up. Shared HUD component tests cover their score bindings; earlier gameplay
  results remain separate evidence.
