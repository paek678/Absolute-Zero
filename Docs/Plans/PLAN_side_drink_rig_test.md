# Side-view drink rig test

## Scope
User-approved isolated test scene for a side character taking the existing Warm Tea item with both hands and using it. Preserve game scenes, front assets, networking, item balance and build scene list.

## Evidence and design
- Front controller: `Assets/MainFolder/Animations/playerA.controller`, drink clip 1.5 s; sprite swaps and Transform curves, not skinned arms.
- WarmTea: opponent trigger `drink`, duration 1.5 s, effect delay 0.75 s. Use the existing icon; read data without modifying it.
- Side atlas is generated draft artwork. Slice nine character parts, preserve names, and add a closed-eye head variant.
- Test-only acquisition and return surround the 1.5 s use segment. A presentation marker demonstrates the effect timing; no health, inventory, RPC or NetworkVariable writes.

## Implementation
1. Import isolated side atlas, arm mesh/bone weights and closed-eye sprite.
2. Build two three-bone SpriteSkin arm chains with two-hand grip targets and analytic IK.
3. Create a dedicated Animator clip/controller that animates the item target and sequence time. Use the original drink clip's small head/cup motion as reference.
4. Create a separate preview scene with replay/pause/bone-overlay controls and the existing WarmTea asset reference.
5. Validate compilation, SpriteSkin validity, grip error, one effect marker per cycle, repeat playback and restored original scene. Capture real Unity frames and GIF.

## Implementation result
- Scene: `Assets/Tests/SideDrinkPreview/SideDrinkRigTest.unity`.
- Prefab: `Assets/Tests/SideDrinkPreview/SideDrinkCharacter.prefab`.
- `SideDrinkPreview.cs`: two three-bone arm chains, two-hand analytic IK, eye swap, effect marker, replay/pause/overlay controls, frame capture.
- `Editor/SideDrinkPreviewBuilder.cs`: imports independent sprites, writes mesh weights through supported data providers, creates Animator assets and isolated scene. Invoke `AbsoluteZero.Preview.SideDrinkPreviewBuilder.Build()` in Edit Mode with a different clean scene active.
- 4 s test sequence: 0–0.5 reach, 0.5–1 bring cup to mouth, 1–2.5 use, 2.5–3.2 return, 3.2–4 release/rest. Use marker at 1.75 s (use+0.75).
- Existing WarmTea icon and SO timing are reused. Timing is read during scene authoring; rebuild if SO timing changes. Acquisition/return timings are test choreography, not a new combat timing contract.
- Actual SpriteSkin deformation is used for both arms (175 vertices per arm). Head, torso and hair remain rigid cutout parts. This is a dedicated controller inspired by front-view presentation, not a retarget of every front controller state.
- Sleeve overlap was removed with a generated torso variant. A closed-eye head was generated. Source parts were sliced from the previously generated side atlas. All generated art used the built-in image tool, not a verified Sunburst invocation.

## Validation and corrections
- Unity auto compilation passed; no forced script recompilation tool was used.
- First pass found an unreachable cup starting point (0.279 units grip error); brought it inside both arms' reach.
- Repeated playback exposed zeroed nonserialized IK caches after script reload. Added OnEnable initialization and solve-time recovery. Explicit cache reset plus disable/re-enable restored both chain lengths.
- Final clean capture: 120 actual Play Mode frames at 30 fps, maximum wrist-to-grip error 2.98023224e-7 world units; exactly one presentation effect; deformed vertex data present for both SpriteSkins.
- Six normal cycles had already completed with valid deformation after the reach correction; final capture is re-run after cache recovery fix.
- Evidence: `output/animation-preview/side-rig-test/final-clean/validation.json` and `final-bones/validation.json`; GIFs `side_drink_actual.gif` and `side_drink_bones.gif`.
- Existing font dynamic-atlas changes caused by editor activity were restored; original game scenes, front art, SO values, build settings and networking are unchanged.

## Limits and handoff
- Latest layout correction: raised the crown to expose the eyebrow, aligned the back-hair crown, widened the side-lock, and authored both arm rest targets toward the knees with a 20-degree rest wrist angle. Prefab/scene now save the posed arms and hide the cup until Awake; runtime enables it and retains the drink sequence. Builder reproduces these values. `layout-review.png` is the actual edit-mode silhouette; `layout-final/validation.json` confirms 120 frames, one effect, both skins and 2.98023224e-7 grip error. Art silhouette and the painted cap seam still differ from the assembled reference; this is not an exact match.
- Reference alignment follow-up: enlarged all head parts 12% around the neck and adjusted the near side-lock position/height. Scene, prefab and builder agree; bone guides default off. Before/iteration screenshots are `reference-before.png` and `reference-pass1.png`; final runtime evidence is `reference-final/` and `side_drink_reference.gif` in the existing output directory. The 120-frame capture retained both deformed skins, one effect event and 2.98023224e-7 maximum grip error. Source frontHair contains a black lower edge absent from the assembled reference; transforms cannot remove that painted seam. Reference matching is approximate, not pixel-identical.
- Hair alignment follow-up: lowered/shifted the front crown, widened it to 115%, and placed it above side-lock roots. Saved the test scene, prefab and builder defaults. Play Mode capture `output/animation-preview/side-rig-test/hair-fixed/` verified 120 frames, one effect event and both deformed skins; updated `side_drink_actual.gif`. The original layered artwork still has a drawn join line between front and back hair.
- Isolated visual test only; the USED label represents the timing cue, not inventory consumption or authoritative temperature recovery.
- No multiplayer integration or host/client validation is claimed.
- SpriteSkin logs shader GPU-deformation fallback warnings in this project; CPU deformation runs and is verified. No global rendering settings were changed to suppress these warnings.
- Generated outlines, wrist silhouettes, and closed-eye head registration still merit artist polish before production integration.
- Open the test scene and press Play. Replay, Pause, Show arm bones and Repeat are available in the test panel.
