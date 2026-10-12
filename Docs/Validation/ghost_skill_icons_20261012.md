# Ghost skill button icons — 2026-10-12

User requested the existing local skill icons in their corresponding slots.

- GameHudBuilder assigns Resources/skill_han to Grudge and Resources/skill_capture to Possession as 42x42 aspect-preserving child Images. Button labels reserve the icon's width; cooldown and spent text remain outside the button.
- Existing button backgrounds, command handlers, visibility and cooldown/one-use rules are unchanged. Icon Images do not receive raycasts.
- Verified connected Unity 6000.3.11f1 at this repository. Normal asset refresh/compilation completed; actual builder evaluation passed four button/state cases (resource binding, aspect, raycast setting, label offset and callback invocation).
- Visually inspected actual Unity component preview: output/validation/ghost-skill-icons/preview.png. It shows available and unavailable states; it is not a live multiplayer screenshot or physical click test.
- Initial verification was preview-only. Follow-up requested in-game captures: build job `067e55bec9924d4bbda6e8f3686e1e15` succeeded (0 errors, 2 warnings), updating the fixed standalone executable.
- Real Relay host + three clients ran the existing ghost-showcase fixture: report passed, synchronized checkpoints and complete captures; all four processes exited 0. Fixture deliberately makes seat 1 a ghost, then uses Grudge and Possession through existing network commands.
- Actual 1600x900 framebuffer captures visually inspected: `output/validation/ghost-skill-icons/ingame/seat1.0003.turn-01-settled-seq-1.png` (available icons) and `seat1.0008.final-ghost-showcase.png` (cooldown/spent states). No composite or simulated UI preview. Runner/report and all peer frames preserved in the same directory.
- Character ghost artwork/animation import remains separate; no such asset was substituted with these skill icons.

## Compact panel follow-up

- Removed the persistent English heading, including its reset after target selection. Panel reduced from 440x140 to 420x100; two 190x50 buttons and cooldown/spent labels fit with a 10px button gap. Temporary targeting feedback uses Korean outside the compact panel, with no reserved empty heading row.
- Build `db9305585045485892d2f7bbabd69b3d` succeeded: 0 errors, 2 warnings. Relay four-player ghost showcase passed again; actual frame `output/validation/ghost-skill-icons/ingame-compact/seat1.0003.turn-01-settled-seq-1.png` visually confirms the compact panel and absent heading.
- User also requested a duel capture. Actual two-player Relay role-cat fixture completed successfully (`output/validation/plan069/compact-ui-duel/result-2.json`). Actual displayed frame `role-cat-2-w0/host.role-01-Cat-item-start.png` under that directory shows the day map and duel HUD; duel has no ghost skill panel by design. Both game groups used controlled scenario setup, not a full natural match.
