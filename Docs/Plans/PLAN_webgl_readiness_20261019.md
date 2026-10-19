# WebGL readiness audit and migration plan — 2026-10-19

Follow-up authorization: the user requested a separate Git branch and a detailed implementation plan, while continuing to prohibit code changes. `codex/webgl-build` was created from main `f2e7e08`. The original audit below remains historical evidence; the executable task sequence is now [the detailed implementation plan](PLAN_webgl_implementation_20261019.md). Branch creation does not close the Web build or runtime gates.

## Scope and evidence boundary

User request: inspect the entire current project for WebGL incompatibilities, explain causes, and propose a plan without implementing changes. Baseline: main `f2e7e08261f782c0a2a90ce4eb4f83f51db3766b`. No source, asset, package, build-target or branch change is authorized by this audit. This document is the only new repository artifact.

Static inspection covers production session entry/return, installed NGO source, Solo graph entry, UI/input/fonts/audio/preferences, development probes, scene list, packages and build/quality settings. Read-only Unity CLI inspection targeted the verified running `C:/Users/paek6/AbsoluteZero-SoloIntegration` Editor (6000.3.11f1, PID 23424), in Edit Mode. The inspected settings have no integration worktree diff. Main gameplay fixes match the previously tested integration source; unrelated integration font/harness changes remain separate.

There was NO WebGL build, browser gameplay, platform switch, module installation or new gameplay test. Prior Windows/Editor 1,363-test success does not establish WebGL compatibility. Findings marked code-confirmed are contradictions visible in the current implementation and installed platform branches, not browser reproductions. Other entries explicitly remain risks or test requirements.

Planning baseline: desktop Chrome/Edge first, Firefox cross-check, keyboard and mouse. Mobile Safari/Android touch support is a separate acceptance scope, not assumed from successful desktop WebGL. Hosting provider and desktop/browser support matrix must be settled before release configuration.

## Current settings observed through read-only Editor API

| Property | Observed value | Implication |
|---|---|---|
| Active target | StandaloneWindows64 | Web target has not been selected |
| BuildPipeline.IsBuildTargetSupported(WebGL) | false | Web Build Support unavailable in this Editor installation |
| Web graphics API | OpenGLES3 | WebGL 2 path, not WebGPU |
| Compression / decompression fallback | Brotli / false | Hosting must serve compressed content with correct headers |
| Exceptions | ExplicitlyThrownExceptionsOnly | Preserve catches used in session cleanup; do not blindly disable exceptions |
| Initial / maximum memory | 32 / 2048 MB | Initial size is a measurement candidate, not proof of a memory crash |
| Growth / data caching | Geometric / true | Measure cold and cached starts, memory growth and cache update behavior |
| Managed stripping | Minimal | Start conservatively; verify bot graph and runtime-created UI before increasing stripping |
| API compatibility enum returned by installed API | NET_Standard_2_0 | Record raw installed enum; verify selected API profile in Web build rather than inferring UI label |
| Quality profiles | PC only | Create a measured Web profile; PC settings are not demonstrated broken |
| Enabled scenes | LobbyScene, GameScene, GameScene_Multi, GameScene_Solo | Preserve scene order and network scene availability |

Packages: NGO 2.11.2, Transport 2.7.2, Relay 1.0.5, Lobby 1.3.0, Authentication 3.6.1, Behavior 1.0.16, URP 17.3.0, Input System 1.19.0, 2D Animation 13.0.5. Upgrading all packages or migrating to the newer Multiplayer Services SDK is not a prerequisite justified by this audit.

## Findings and planned treatment

| ID / priority / evidence | Affected flow and cause | Owner / proposed treatment | Acceptance evidence required |
|---|---|---|---|
| W00 P0 — confirmed tooling blocker | Installed Editor cannot build WebGL because support is unavailable. | Install matching 6000.3.11f1 Web Build Support when implementation starts; create Web profile from a branch based on the pinned main commit. | Support API returns true; first development Web build succeeds. |
| W01 P0 — code-confirmed online incompatibility | Actual coordinator constructs RelayGateway; allocation and join both hardcode `dtls`. Browser uses WebSocket transport and cannot use the native UDP/DTLS endpoint. | `RelayGateway.cs:29,46`, `NgoNetworkRuntime.cs:31,53`; choose `wss` and matching `UseWebSockets` for Web. Review legacy `RelayManager.cs:63,105` to prevent alternate routes retaining incompatible settings. Preserve native transport configuration on Windows. | Browser host/client Duel and host+3-client Multi connect through real Relay; both Web↔Windows host directions if cross-play is in release scope. |
| W02 P0 — code-confirmed Solo incompatibility | `AppBootstrapper:62` → SoloSessionCoordinator → NgoLocalHostRuntime creates UnityTransport, binds loopback, calls StartHost. Installed UnityTransport.CreateDriver:1814–1818 throws for Web server without Relay. `SetConnectionData(true,...)` means force command-line override, NOT IPC. | `NgoLocalHostRuntime.cs:41,47`; prototype a host-only in-process NetworkTransport adapter with no socket/listener while retaining NGO local host, server authority, registry and bots. Prove NGO lifecycle contract before adopting. Do not bypass TurnManager or fake network callbacks globally. | Browser offline Solo launch, bot graph, spawn/round/mini-game/result, normal leave and repeated online↔Solo transitions; no leaked transport/approval configuration. |
| W03 P1 — high-risk async portability, not reproduced | Startup, scene readiness, lobby-code waits and cleanup poll through Task.Delay; the Web platform does not support managed timer/thread behavior like desktop. Timer-dependent polling may never resume and prevent timeout/return. | SoloSessionCoordinator:130,191,218; NetworkSessionCoordinator:683,717,1012; NgoLocalHostRuntime:85; SceneTransitionService:41; OnlineLobbyJoinPolicy uses WhenAny(request, Delay). Replace only frame/time waits with a Unity main-loop wait adapter using realtime deadlines and existing generation/cancellation checks. Retain SDK Task-returning I/O. | Delayed request, never-completing request, cancel during entry, quit/re-enter, late response and scene timeout all exit within intended budgets in a browser. |
| W04 P1 — known browser restriction / runtime test needed | Host tab suspension throttles authoritative game updates and lobby heartbeats. Existing runInBackground=true cannot prevent browser suspension. HostPresenceTimeout is 60s. | NetworkSessionCoordinator.Update, LobbySessionWork.Tick, OnlineLobbyJoinPolicy; specify visibility warning and bounded disconnect/recovery behavior. Preserve rules and do not promise uninterrupted hidden-tab hosting or host migration. | Host and remote tab hide/minimize for 10/30/70s; host close, client close, network loss and refresh produce an understandable result and usable lobby. |
| W05 P1 — nonportable font lookup, fallback already present | MiniGameUIBase.CreateText calls UiFont.Apply, which searches Windows OS font paths. Browser cannot depend on installed Windows fonts. However TMP Settings already has bundled MalgunGothic fallback, so universal missing Korean text is NOT established. | UiFont.cs:25; use a bundled font reference for Web and preserve the existing fallback chain. Gmarket dynamic and Malgun source font assets exist. | All nine mini-game instructions, nickname composition, roster, result and settings show Korean without Windows fonts; test dynamic glyph generation after release stripping. |
| W06 P1 — browser UX difference | BGM before a user gesture can be silent due to autoplay policy. AudioSource volume controls are supported; the project adjusts sources directly, not an unsupported mixer-only implementation. | GameAudioManager; deliberate click-to-start/audio activation and visible mute state if needed; preserve master/BGM/SFX semantics. | Fresh browser profile: first click enables BGM/SFX, settings sliders work, mute/unmute and focus recovery work. |
| W07 P1 — confirmed platform semantic difference | Settings Quit uses Application.Quit. On Web it stops the player but does not close the page; the current button may leave a stopped canvas without a useful page state. | LobbySettingsView.Quit; decide Web action: orderly leave and landing screen/restart control. Template may use instance.Quit cleanup if actually ending the player. | Quit from lobby and battle performs session cleanup and leaves a usable page; restart works. |
| W08 P1 — fullscreen/input behavior risk | LocalSettingsRuntime automatically restores fullscreen at Start and uses desktop-style SetResolution. Web fullscreen requires user interaction. Esc also belongs to browser fullscreen exit while MatchMenu and targeting use it. | LocalSettingsRuntime:53,75; explicit Web fullscreen control, visible menu button, correct fullscreen change feedback; keep mouse selection and cancel behavior. | Windowed/fullscreen/resized/zoomed canvas; Esc once/twice; focus loss; mini-game/target clicking; no double action or inaccessible menu. |
| W09 P1 — deployment requirement, host not chosen | Brotli with fallback off needs proper Content-Encoding and wasm MIME; opening index.html as a desktop file is not the delivery flow. | Web template/hosting config; HTTPS, `Content-Encoding: br`, `Content-Type: application/wasm`, correct JS MIME; versioned cache policy. Enable fallback only if the chosen host cannot set headers. | Fresh and cached load, new-version update, URL refresh and iframe embedding if used; no CORS/mixed-content/decompression failures. |
| W10 P2 — persistence/identity risk | Settings and cosmetic/profile state use PlayerPrefs; Web storage is browser-local IndexedDB, not Windows preferences. Anonymous auth and multiple tabs may reuse stored identity. | LocalSettingsService / cosmetic stores / UnityServicesGateway; retain browser persistence, handle unavailable storage; independent profiles/devices for multiplayer tests and explicit duplicate-session behavior. | Refresh retains settings; private/cleared-storage failure is graceful; distinct players have distinct IDs; same-profile double-open does not corrupt lobby membership. |
| W11 P2 — performance and download unmeasured | Only PC quality exists (URP renderScale1, soft shadows); resources load synchronously. Assets/Resources alone has 175 non-meta source files totaling43.1MiB, including PNG10.4MiB and BGM WAV7.3MiB. These are SOURCE sizes, not download/runtime memory. Other Resources folders and scene dependencies add content. | Build report and browser profiler first; Web quality, texture/audio import overrides, shader variant audit and measured memory settings. Keep Addressables optional and avoid moving NGO prefabs without a separate plan. | Measure compressed transfer, cold/warm first-interaction time, heap/GPU use, 4-player worst effect frame times and repeated scene cycles; no OOM or context loss. |
| W12 P2 — graphics/AOT regression not proven | Custom foreground sprite shader uses ZTest Always; URP, 2D skinning, ghost animation and dynamic fonts need browser verification. Current bots use BehaviorGraphAgent, not a discovered neural-inference worker. Inference package presence alone is not a compute blocker. | Preserve gameplay/visual assets; verify WebGL shaders, CPU fallback where required, serialized graph/node preservation, and stripping gradually. | Every item/effect, billboards during camera movement, foreground items, ghost spawn/skills and both bot graphs work in release Web build; no pink materials/missing nodes. |
| W13 P2 — desktop test tooling cannot be reused unchanged | Development-only PlaytestJsonlSink starts a C# Thread; GameplayVideoProbe launches ffmpeg and a Thread. Ordinary release excludes these guards, but a development Web build includes them. Legacy SoloMatchStarter contains Process.Start although current menu uses SoloSessionCoordinator. | Isolate unsupported development tools by platform; browser console/bounded JSON export and browser-side observation instead. Audit unreachable legacy code before compile; do not call it the active bot architecture. | Both development and release Web compilation pass; no desktop process/thread capture is invoked; observed tests still have machine-readable evidence. |
| W14 P2 — mobile/IME/accessibility scope | Gameplay targets Mouse.current/Keyboard.current; browser availability of touch does not prove game touch usability. Korean TMP composition/clipboard and page keyboard focus need real tests. | Desktop-first baseline; inspect IME and HTML input integration only if actual failures demand it. Separate touch UX plan if mobile is required. | Korean nickname entry/composition/backspace/paste and mouse targeting work on supported desktop browsers; mobile not advertised before its own gate. |

## Solo transport options

1. Preferred investigation: host-only local NetworkTransport implementation, no browser socket. Bots are already in the same process. Retain the authoritative NGO host and existing game logic; implement/test initialization, PollEvent, shutdown and local-host semantics against NGO2.11.2. This is a proposed solution requiring a small browser proof, not an available validated component.
2. Alternative: allocate Relay for Solo as well. Technically aligns with Web-supported host mode, but makes local bot play depend on Internet, authentication, Relay availability and cost. Do not silently change offline behavior to take this shortcut.
3. Broad removal of NGO from Solo would affect inventory, RPCs, scene transitions and match lifecycle. Not recommended for this conversion.

## Sequenced implementation gates (all pending)

| Phase | Scope | Exit condition |
|---|---|---|
| G0 | Choose a Web branch from f2e7e08, desktop browser baseline and host; install exact Web module; record baseline hashes/settings | Development Web compile succeeds or yields a preserved actionable failure log. Branch/settings changes happen only in implementation. |
| G1 | W01 WSS, W02 local Solo transport proof, W03 main-loop waits | Solo and real Relay Duel can enter and return twice; cancellation/error paths release ownership; existing Windows paths still work. |
| G2 | W05–W08 W10 W14 fonts/audio/input/settings/identity and W13 browser diagnostics | First-user interaction, Korean UI/input, all mini-games, settings persistence and Web exit behavior pass. |
| G3 | W04 online lifecycle and cross-browser full gameplay | Complete browser matrix below; no blocking entry/return/pending-selection defects or divergent authority. |
| G4 | W09 W11 W12 measured hosting/performance/release stripping | Final compressed release served through intended host; release matrix passes on exact final files. |

Preserve current game design: initial four items, immediate temperature threshold rewards, per-mode inventory caps, fast Ready, item values, MiniGameTicket ordering/deadlines, host authority, 4-player3round/3kill/tied-winner behavior and bot difficulty assets. No balance tuning is part of Web conversion.

A Web-specific Editor validation/apply menu can be prepared during implementation to record/save/read back approved settings consistently. It must not blindly apply generic High stripping, exceptions=None or large initial memory before evidence. No such script has been created in this audit.

## Deferred validation ledger

All entries below are NOT RUN. Evidence required: exact commit/build profile/hash/browser/version, distinct participant identity, server/client logs, screenshot or scene/state checkpoint, failure retention and normal cleanup.

| ID | Stimulus / acceptance |
|---|---|
| V01 | Development and ordinary Web builds, then HTTP(S) boot from all four included scenes through normal lobby entry; production build excludes unsupported probes. |
| V02 | Beginner and Advanced Solo, at least3 complete matches per difficulty; normal menu selection, fast Ready, immediate rewards, round2 mini-game starts, all9 factories, second match and normal lobby return. |
| V03 | Web host+remote Duel, at least3 complete matches with Chrome/Edge/Firefox rotation and host-role swaps. |
| V04 | Web host+3remotes Multi, at least3 complete matches; every seat, death/ghost skills, item targets, 3round/3kill termination and tied scores. Distinct browser profiles/devices, not shared-auth tabs. |
| V05 | Cross-play if supported: Windows host→Web client and Web host→Windows client, gameplay and return; compatible protocol/allocation choices verified without assumptions. |
| V06 | Pending mini-game request without UI/result, stale result, expiry, repeat selection and rapid round rollover; no permanent item lock, no double consumption. |
| V07 | Hide host/client tabs10/30/70s, host/client refresh/close, disconnect during loading/mini-game, restore network. Bounded cleanup and accessible lobby; no false promise of automatic match reconnection. |
| V08 | Browser first-gesture audio; all sliders; Korean text/input; fullscreen, Esc, canvas resizing, zoom/DPI, mouse hit positions and settings across refresh. |
| V09 | Release WebIL2CPP graph/node/shader/font retention, every item/VFX, environment camera, 2D skinning and ghosts. |
| V10 | Real intended host: compression/MIME/CORS where needed, iframe policy if used, cold/warm cache, replacement release. Record load/memory/frame measurements on declared baseline hardware; choose budgets from measurements before release approval. |

V02–V04 propose12 complete matches total, plus explicit failure/regression cases. Counts are a bounded minimum, not proof that no bugs remain. All Windows/Editor historical passes remain separate evidence.

## Official references

- [Relay transport protocols](https://docs.unity.com/relay/networking): WSS support; browser hosting through Relay is possible.
- [Unity6.3 technical limitations](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-technical-overview.html): managed threads/timers, AOT, main-loop alternatives.
- [Unity6.3 audio](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-audio.html): first-interaction autoplay and supported audio APIs.
- [Unity6.3 input/fullscreen](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-input.html): fullscreen user gesture and Esc behavior.
- [Application.Quit](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application.Quit.html): Web Player stops while page remains.
- [PlayerPrefs](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PlayerPrefs.html): IndexedDB and browser storage limit.
- [Web deployment](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-deploying.html): compression and MIME headers.
- [Web performance](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-performance.html): background tab throttling.
- [Web memory](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-memory.html): heap and asset-data accounting.

## Audit outcome

Three immediate blockers are established by read-only evidence: missing Web build module, native-only Relay endpoint choice, and non-Relay local host startup. Other findings require bounded platform adaptation or actual browser validation, not unsupported claims of failure. No overall approval score is assigned. The project is a viable migration candidate, but switching the build target alone is insufficient.
