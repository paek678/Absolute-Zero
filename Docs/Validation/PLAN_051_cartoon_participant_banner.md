# Cartoon participant banner

Date: 2026-10-06. User requested generated pastel upper banners with thick white outlines and gently rounded corners, matching the existing illustrated game HUD.

## Implementation

- Selected generated asset: `Assets/Resources/UI/ParticipantBannerFrame.png` (and Unity-generated `.meta`). Created with the built-in image generation tool, without an API key. Earlier rejected generations remain outside the project in Codex's generated-image directory.
- The final asset is a clean hollow white cartoon frame with a thin brown keyline and transparent center/exterior. The first candidate's internal shading looked muddy at gameplay size and was rejected after a real capture.
- Unity Sprite Editor data provider capability checks were performed. The single sprite inside the texture is cropped by metadata to `(36,72,1547,832)`, with 140-pixel borders, 800 pixels per unit, FullRect mesh, bilinear filtering, no mipmaps and no compression. The PNG is not painted/cut by an external script.
- `GameHudBuilder.BuildProgressHud` uses the imported 9-slice frame and a separate opaque pastel fill. White borders retain their color during state changes. Name cards use a 10-unit gap and 84-unit height; a matching cream score strip sits below them. Dark brown text replaces white/yellow text for contrast on the light panels.
- `ParticipantBannerPresenter` uses mint for the local player, powder blue for opponents, peach for the acting player, lavender for ghosts and gray for empty seats. The actual first/current labels remain execution-driven. Network state, names, scores, initiative and input handling are unchanged.
- Updated banner tests assert imported sprite presence, 9-slice borders, untinted white frames, Korean label fit and non-interactive UI graphics.

## Selected generation prompt

Built-in tool; `transparent_background: true`; no reference image supplied for the final generation:

> A single clean cartoon game UI FRAME sprite, a hollow rounded rectangle border, on a truly transparent canvas. IMPORTANT this is ONLY a border: both the whole center opening and everything outside are fully transparent. A thick solid pure warm-white stroke, edged on its exterior by a much thinner solid dark chocolate-brown ink line. Flat vector-like hand-inked shapes. Horizontal rounded rectangle 1.7 width to 1 height; corners only gently rounded, radius 8 percent of height, straight sides. White stroke thickness 5 percent of height, exterior dark keyline thickness 1 percent of height. Single connected hollow frame fills most of the canvas, small equal margins. ABSOLUTELY NO FILL in the center, NO mint color, NO gradients, NO bevel, NO shadow, NO glow, NO blur, NO inner shadow, NO gray shading, NO texture, NO glass, NO background, NO text, NO icons. Just two crisp flat colors making a white cartoon border with a dark outer outline, all else clear alpha. Game production 9-slice UI sprite.

## Validation

- Focused PLAN047 suite: 20/20 passed after builder/presenter changes. Final selected frame: 3/3 banner tests passed for 2, 3 and 4 seats.
- Initial development build succeeded with 0 errors / 487 warnings, mostly package shader warnings plus existing optional Pipeline configuration and dynamic TMP importer warnings; not a clean-warning claim.
- Initial actual Relay 4-player, Relay 1v1 and local Solo menu-to-game runs passed (7 processes). This prompted the final cleaner frame iteration, which is verified separately below.
- Local evidence root: `output/validation/plan051/`; captures use the established diagnostic camera/canvas renderer.

Final selected asset build `build_add83b1446bd`: succeeded, 0 errors / 487 warnings (package shader warnings plus the previously listed Pipeline/font warnings). Final actual Relay 4-player, Relay 1v1 and local Solo runs: **3/3 scenarios, 7 processes passed**, with agreeing replicated names and banner text. Evidence: `output/validation/plan051/final/summary.json`; `final/players-4/0.game.png` and `final/players-1/0.game.png` visually inspected. No nickname failures or exception headers were found in those final player logs. These runs verify preparation-phase presentation; new action/ghost color branches were inspected in code, not separately captured in a real combat this turn.

Editor compile state: no compilation failure, zero console errors; warnings remain. User's existing dirty font and bot graph hashes match the pre-task baseline. Original nickname preferences are restored by the opt-in test harness. Test processes have exited; no scene/prefab changes, commit or push. Asset and prompt are retained here for future style revisions.

## Remaining visual acceptance

- Human preference on banner size/colors and different display scaling remains subjective. Current validation targets 1280x720; 3-seat geometry is tested but a new actual 3-player network run is not part of this presentation-only task.
- Network authority and extended lifecycle debt remain in PLAN047/PLAN050; cosmetic banner changes do not close those unrelated checks.

## Requested combat recheck — 2026-10-06

- Re-ran the focused Editor suite: **20/20 passed**. Added a regression assertion that the first name card clears the actual local HP outline, for 2, 3 and 4 seats; the final suite still passes 20/20. Evidence: `output/validation/plan051/recheck-20261006/editor-tests-final.json`.
- Found a small overlap between the Multi banner's left border and the local HP outline in the real capture. Shifted only the 3/4-seat banner root 24 reference units right. The 1v1/Solo positions are unchanged. The final 960x540 Multi capture has visible separation.
- Fresh execution before this spacing adjustment: actual Unity Relay 1v1 combat/rematch, actual Unity Relay 4-player ghost victory, and local Solo production BT: **3/3 scenarios passed, 7 processes**. Solo passed 33 checks, including combat, delayed decision cancellation, empty-inventory fallback and lobby return. Both Relay scenarios have equal replicated checkpoints and no runner errors.
- Rebuilt the spacing fix: `build_7e7a8316115e` **Succeeded, 0 errors / 2 warnings**. The remaining warnings concern missing optional RuntimePipelineConfig and the existing dynamic TMP font importer. This incremental build did not re-emit the earlier cached shader warnings.
- Re-ran the actual Relay 4-player ghost scenario after the spacing fix: **passed, all 4 peers agreed**, all processes exited normally. Evidence: `output/validation/plan051/recheck-20261006/after-spacing/summary.json`.
- Visually inspected diagnostic camera/canvas captures from actual running players: `duel-2-w0/host.banner-4.png` (peach current actor with `선공 · 행동 중`), `solo/solo.banner-4.png` (human currently acting while bot retains `선공`), and `after-spacing/ghost-4-w0/host.banner-4.png` (lavender ghosts, synchronized names and 5:0:0:0 score). Paths are relative to `output/validation/plan051/recheck-20261006/`. The ghost scenario deliberately seeds the near-win state; it is not an unassisted full random match.
- No nickname mismatch, exception header or matrix failure was found in these logs. Existing shutdown `ComputeBuffer` disposal warnings remain and are not claimed fixed by this UI change. Current Editor ground-truth console: 0 errors, 2 warnings, compilation not failed. Test processes are closed. Font and bot graph hashes still match the pre-task dirty baselines.
- Scope: runtime screenshots at 960x540 (Relay) and 1280x720 (Solo), plus 2/3/4-seat geometry assertions. Actual 3-player networking, other aspect ratios/scaling, long sessions and separate PCs were not newly tested here. The preparation-only limitation above describes the earlier run; this follow-up adds real combat and ghost-state evidence.

## Typography and highlight polish — 2026-10-06

- User requested prettier type and banner highlights. Retained the generated white frame and existing font assets. Nickname maximum size increased from 22 to 26 reference units, with bold styling, subtle tracking and a brown vertex-color gradient. Long names retain autosizing. Score typography receives the same styling. No shared font material is mutated or allocated for these gradients.
- Added an inset upper sheen and fine glint, a restrained lower frame shadow, cream/gold action badges and a gold outer accent for the current actor. Existing event-driven presenter toggles these decorations from the same observed first/current seats; no extra Update loop, animation timer or network state was introduced. Decorations are non-interactive. The Multi spacing fix is retained.
- Focused Editor suite **20/20 passed**, including label fit, non-interactive graphics and 2/3/4-seat HP separation. Build `build_6f03a0f33ae9` succeeded with **0 errors / 2 existing warnings** (optional runtime Pipeline configuration, dynamic TMP importer).
- Fresh actual Relay duel/rematch, actual Relay 4-player seeded ghost-win scenario, and local production-BT Solo all passed: **3/3 scenarios, 7 processes**; Solo has 33 successful checks. Relay checkpoints agree. Test processes exited and no name mismatch, exception header or assertion failure appeared in the player logs. Existing shutdown ComputeBuffer warning remains outside this presentation change.
- Evidence: `output/validation/plan051/highlights/{editor-tests.json,build.json,summary.json}`. Visually reviewed `duel-2-w0/host.banner-4.png`, `ghost-4-w0/host.banner-4.png`, `solo/solo.banner-4.png`. The Solo capture shows the human retaining first-move status while the bot is the gold-highlighted current actor. Screenshots are actual diagnostic renders of running game camera/canvas output, at the resolutions listed above.
- Existing dirty font and bot graph content hashes remain unchanged. No scenes or source image assets were modified for this polish. Other display ratios and human visual preference remain open as above.

## Jua typeface replacement — 2026-10-07

- Replaced the participant banner typeface itself with **Jua (Woowahan Brothers / Baemin Jua)**: nickname, seat, first/current action and score. Existing highlights and spacing remain. Removed synthetic bold and tracking because Jua's native rounded strokes are already heavy. Other game UI fonts are unchanged.
- Source: [Google Fonts Jua](https://github.com/google/fonts/tree/main/ofl/jua), downloaded `Jua-Regular.ttf` unchanged. SHA256: `769677AEF240BFC3B9965F2B50748075BFF885E6C6992FC591A3FB268279F898`. [SIL OFL 1.1](https://raw.githubusercontent.com/google/fonts/main/ofl/jua/OFL.txt) permits embedding and redistribution with its copyright/license. Included the license at `Assets/StreamingAssets/ThirdPartyLicenses/Jua-OFL.txt` and verified it exists in the player build.
- Assets: `Assets/Fonts/Jua/Jua-Regular.ttf`, `Assets/Resources/UI/Fonts/JuaBanner.asset` and `JuaBannerDynamic.asset`. Created through Unity TMP/AssetDatabase APIs with material/atlas subassets and generated metadata. Static common labels/Latin atlas plus a 1024x1024 dynamic Jua fallback, 48-point sampling and 5-pixel padding. Dynamic fallback clears cached glyph data on build. Existing Korean/default fallbacks cover unsupported rare syllables/symbols; initial creation found Jua lacks several punctuation glyphs, which are explicitly handled by this fallback chain.
- Targeted actual-font banner tests **3/3 passed** for 2/3/4 seats, including eight-character Korean names, action-label fit, non-interactive graphics and HP separation. Removed the test's old Malgun override so it now exercises the font actually used by the builder. A mixed Korean/Latin/rare-syllable/symbol coverage probe passed with no missing characters.
- Build `build_9f1dae01e863`: **Succeeded, 0 errors / 487 existing warnings** (package shader warnings, optional Pipeline configuration and existing TMP importer). Fresh Relay duel/rematch, Relay seeded 4-player ghost-win and local production-BT Solo: **3/3 scenarios / 7 processes passed**. No missing-glyph, name mismatch, exception or assertion failure appeared in player logs. Existing shutdown ComputeBuffer warning is unchanged.
- Evidence: `output/validation/plan051/jua/{editor-tests.json,build.json,summary.json}`; inspected `duel-2-w0/host.banner-4.png`, `ghost-4-w0/host.banner-4.png`, `solo/solo.banner-4.png`. Tests exited. Existing dirty Malgun font and bot graph hashes are preserved. Resolution/other-PC limitations above remain.

## Four alternative font previews — 2026-10-07

User requested four web-referenced casual/cute Korean fonts for selection, not a final replacement. Downloaded unchanged TTFs and their OFL licenses from the official Google Fonts repository into `Assets/Editor/FontComparisons/` (Editor-only, not runtime Resources):

1. [Dongle Bold](https://github.com/google/fonts/tree/main/ofl/dongle): rounded, compact and heavy; preferred for this HUD's small labels.
2. [Gaegu Bold](https://github.com/google/fonts/tree/main/ofl/gaegu): playful irregular handwriting.
3. [Gamja Flower](https://github.com/google/fonts/tree/main/ofl/gamjaflower): lighter handwritten look.
4. [Single Day](https://github.com/google/fonts/tree/main/ofl/singleday): soft rounded handwriting.

Captured the running Editor's Solo scene while paused at one frame, 1920x1080. Used temporary copies of the actual banner, identical sample labels (`말랑눈꽃`, `구름친구`, first/current badge and score), common colors/geometry, and fonts baked into temporary TMP assets. Matched visible glyph heights (not nominal point sizes or TMP line bounds): names 22 reference units, small labels 10, score 13. The preview uses geometry-centered alignment. These are comparison settings; a selected font still needs final layout/long-name validation in the production builder.

Four full-game captures are `output/validation/plan051/font-comparison/{1-dongle,2-gaegu,3-gamjaflower,4-singleday}-game.png`. `00-four-fonts.png` is a separate enlarged comparison board rendered by Unity; scene renderers were temporarily hidden only for that board to avoid foreground item sprites obscuring it. All renderers were restored in finally. Sample labels/highlight states are presentation-only and do not represent new network/game state. All five final images were visually inspected; candidate fonts natively cover the sample Korean text. Source licenses are retained next to each TTF.

No production code, font selection, scenes or prefabs changed. Temporary objects/font textures/materials are destroyed and the original Jua banner restored after capture. Capture recipe and metadata are in the ignored output directory (`capture.cs.txt`, `capture-report.json`). Final choice remains pending. This is font visual comparison evidence, not a new multiplayer regression suite.

## Selected Dongle Bold, heavier strokes — 2026-10-07

- User selected Dongle Bold and requested more weight. Production participant banners now load `UI/Fonts/DongleBanner`: nickname, seat, action and score in the shared Solo/duel/Multi builder. This supersedes the pending choice and previous Jua selection above.
- Copied the unchanged existing candidate TTF to `Assets/Fonts/Dongle/`; retained its OFL alongside the font and under `Assets/StreamingAssets/ThirdPartyLicenses/Dongle-OFL.txt`. TMP static common atlas (143 characters) + dynamic fallback use 96-point sampling, 10-pixel padding, 1024 atlases, native Bold and shared material `_FaceDilate=0.12`. Both Dongle materials use the same weight. Dynamic data clears on build; existing Korean/default fallback remains. No runtime material clones or font-file outline edits.
- Dongle's large line metrics require taller non-interactive text rectangles and geometry-centered alignment. Name maximum/minimum size is 32/16 with autosizing, small labels 18, score 22. Card/frame placement and state logic remain unchanged. No extra synthetic bold is stacked on native Bold.
- Focused actual-font Editor tests **3/3 passed**, including 2/3/4 seats, eight-character Korean names, action text fit, non-interactive graphics and HP/frame clearance. Auto compilation passed; console errors 0. Existing root-only DontDestroyOnLoad warning remains. Early test-time discovery of the two new license files was a test-runner import warning; final repeat passed after imports completed.
- Launched the actual Solo scene from the lobby controls, paused, confirmed all seven live banner texts use Dongle with face dilation 0.12 and no reported overflow, and inspected a 1920x1080 diagnostic game render. A separate enlarged temporary comparison board uses sample names and action state: top native Bold, bottom applied weight. Temporary objects/material were destroyed and renderers restored in finally. These sample labels are not replicated gameplay data.
- Evidence: `output/validation/plan051/dongle-bold/{editor-tests.json,solo-game.png,weight-comparison.png,capture.cs}`. Returned normally to Lobby then stopped Play Mode; original scene clean. The existing dirty Malgun asset was auto-regenerated during validation and restored from a backup matching the exact pre-task SHA256; bot graph hash unchanged. No gameplay edits, new standalone build, or new Relay multiplayer run for this typography-only change. Existing builds require rebuilding to include the chosen font.

## Action badge border and containment — 2026-10-07

- Replaced the flat, unframed action strip with an inset badge reusing the existing cartoon frame sprite (9-slice, compact corners), cream/gold fill and a shared root for border/text visibility. The badge is 24 units high, centered at Y=-22, with 12 units horizontal inset from each card edge. Name ink moved up 4 units to retain separation.
- Action text is now a child of the badge, with 8-unit horizontal padding, 18→14 autosizing and no wrapping. Its 28-unit metric rectangle is retained for Dongle; a RectMask2D is an additional containment guard. Tests assert actual visible glyph corners remain inside the frame, rather than assuming text-rectangle bounds equal glyph bounds.
- Presenter toggles the complete badge root, so idle/end-of-playback hides both fill and border. Existing first-seat/current-seat state, nickname, score and network behavior are unchanged.
- Verification: current Unity project identified; normal import/compile succeeded. Focused banner tests **3/3 passed** for 2/3/4 seats, each testing `선공`, `행동 중`, `선공 · 행동 중`, no truncation/wrap and inset glyph bounds. Existing name/input/HP clearance checks retained. Final console has 0 current errors. Independent static review found no blocking issues in badge lifetime, references, clipping or evidence claims.
- Captured the actual runtime HUD builder components in an isolated Unity preview scene with sample states; inspected spacing and all three badge captions. Evidence: `output/validation/plan051/action-badge/{preview.png,capture.cs,tests-final.json,console.json}`. The enlarged board is a UI component preview, not an actual combat-state capture.
- No full match or standalone rebuild for this presentation-only change. The four already-running manual players still use their earlier build. No scene/assets saved; existing dirty Malgun font and bot-graph hashes preserved. No commit/push.
