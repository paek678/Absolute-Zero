#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Validation.Presentation
{
    // This fixture drives presentation payloads, not player decisions/combat resolution.
    // It uses the production player, copied duel scene and real local NGO lifecycle.
    public sealed class Plan036PresentationProbe : MonoBehaviour
    {
        public SoloDuelDefinitionSO Encounter;
        public ItemDataSO[] Catalog;
        public CosmeticRegistrySO Cosmetics;
        public GameObject PlayerPrefab;
        public PlayerSpawnManager Spawner;
        const string Menu = "Assets/Tests/Plan036Presentation/PresentationFixture.unity";
        static Plan036PresentationProbe _instance;
        readonly Report _report = new();
        readonly List<uint> _settled = new();
        bool _finished;
        bool _expectMissingView;
        bool _sawMissingViewFailure;
        double _deadline;
        string _reportPath;

        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope = "Production presentation with authored result payloads; not BT/full combat gameplay";
            public List<string> checks = new();
            public List<string> screenshots = new();
            public List<CaptureEvidence> captures = new();
            public List<string> animationStates = new();
            public List<string> errors = new();
            public string failure;
        }
        [Serializable] sealed class CaptureEvidence
        {
            public string path;
            public string mode = "Explicit main camera stack render; screen-space overlay UI is outside this capture";
            public int width, height, brightSamples, totalSamples, luminanceRange;
        }

        void Awake()
        {
            if (_instance != null) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        async void Start()
        {
            if (_instance != this) return;
            _reportPath = Path.Combine(Application.persistentDataPath, "plan036-presentation.json");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "--plan036-report") _reportPath = Path.GetFullPath(args[i + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(_reportPath));
            _deadline = Time.realtimeSinceStartupAsDouble + 180;
            Application.logMessageReceived += OnLog;
            CombatVFXManager.OnPresentationSettled += OnSettled;
            try { await Run(); _report.passed = _report.errors.Count == 0; }
            catch (Exception error) { _report.failure = error.ToString(); }
            Finish();
        }

        async Task Run()
        {
            var root = new GameObject("T04 application fixture");
            DontDestroyOnLoad(root);
            var nm = root.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { EnableSceneManagement = true };
            var transport = root.AddComponent<UnityTransport>();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = PlayerPrefab;
            nm.AddNetworkPrefab(PlayerPrefab);
            root.AddComponent<NetworkSessionCoordinator>();
            var app = root.AddComponent<AppBootstrapper>();
            await Until(() => app.IsReady, 5, "local bootstrap");
            var solo = new SoloSessionCoordinator(app.SessionRouter, new NgoLocalHostRuntime(nm), 18784, ReturnToMenu);

            // A deliberately late remote visual must keep initialization waiting.
            bool holdBot = true;
            AZPlayerVisual.DebugHoldPresentationBinding = state => holdBot && state.IsBotControlled;
            await StartSession(solo);
            await Until(() => MatchCompositionRoot.Instance?.Registry.ReadyCount == 2, 20, "logical participants");
            await Task.Delay(200);
            var waiting = MatchCompositionRoot.Instance;
            Check(TurnManager.Instance.CurrentPhase.Value == TurnPhase.WaitingForPlayers
                && !TurnManager.Instance.GetPlayer(1).GetComponent<AZPlayerVisual>().IsPresentationReady,
                "late bot visual blocks Prep even when both logical participants are ready");
            holdBot = false;
            AZPlayerVisual.DebugHoldPresentationBinding = null;
            await Until(() => TurnManager.Instance != null && TurnManager.Instance.CurrentPhase.Value == TurnPhase.PrepPhase,
                20, "presentation-ready initialization");
            var mcr = MatchCompositionRoot.Instance;
            var turn = TurnManager.Instance;
            turn.DebugHoldTurnForPresentationValidation();
            Check(LocalMatchPerspective.TryResolveCurrent(out var perspective)
                && perspective.HumanSeat == 0 && perspective.TryGetOpponentBinding(out var opponent)
                && opponent.Identity.ControllerKind == PlayerControllerKind.Bot,
                "shared-owner bot resolves as opponent while human alone owns local perspective");
            var human = turn.GetPlayer(0);
            var bot = turn.GetPlayer(1);
            var humanVisual = human.GetComponent<AZPlayerVisual>();
            var botVisual = bot.GetComponent<AZPlayerVisual>();
            Check(humanVisual.IsLocalHuman && !botVisual.IsLocalHuman && humanVisual.IsPresentationReady
                && botVisual.IsPresentationReady && FPSVisualController.Instance != null
                && botVisual.GetVisualRoot() != null && botVisual.GetAnimator() != null,
                "production human FPS and bot scene character both bind exactly once");
            Check(humanVisual.DebugPresentationBindCount == 1 && botVisual.DebugPresentationBindCount == 1,
                "deferred identity and view notifications do not double-bind either view");
            Check(turn.DebugExpectedPresentationViewers.SequenceEqual(new[] { nm.LocalClientId }),
                "presentation barrier expects one actual human connection and no bot client");
            Check(bot.CosmeticDataNV.Value.ToString().Contains("hat_01"),
                "bot receives authored cosmetic DTO before or during delayed view binding");
            var hat = Cosmetics.GetById("hat_01");
            var replacements = hat.Atlas.Bindings.Where(x => x.View == CosmeticView.Character && x.Replacement != null)
                .Select(x => x.Replacement).ToArray();
            Check(botVisual.GetVisualRoot().GetComponentsInChildren<SpriteRenderer>(true).Any(x =>
                x.enabled && x.gameObject.activeInHierarchy && replacements.Contains(x.sprite)),
                "retained bot cosmetic DTO is actually applied to an active character renderer");
            // Re-publish exact membership without creating another player or changing its seat.
            var humanBinding = perspective.HumanBinding;
            mcr.WritableRegistry.Unregister(humanBinding);
            Check(mcr.WritableRegistry.RegisterPending(humanBinding) && human.AssignParticipant(human.Participant),
                "exact human binding can be republished to exercise late observers");
            await Task.Delay(100);
            Check(humanVisual.DebugPresentationBindCount == 1 && botVisual.DebugPresentationBindCount == 1
                && LocalMatchPerspective.TryResolveCurrent(out var republished)
                && ReferenceEquals(republished.HumanBinding, humanBinding),
                "republished current binding keeps one FPS and one opponent view");
            await Capture("01-bound-human-fps-bot-hat");

            await VerifyDefense(turn, human, bot, humanVisual, botVisual, 0, 0);
            await VerifyDefense(turn, human, bot, humanVisual, botVisual, 0, 1);
            await VerifyDefense(turn, human, bot, humanVisual, botVisual, 1, 0);
            await VerifyDefense(turn, human, bot, humanVisual, botVisual, 1, 1);
            await VerifyDefense(turn, human, bot, humanVisual, botVisual, 0, 0, "Mask", "Iced Americano");
            await VerifyDefense(turn, human, bot, humanVisual, botVisual, 0, 1, "Mask", "Iced Americano");
            await VerifyDefense(turn, human, bot, humanVisual, botVisual, 1, 0, "Mask", "Iced Americano");
            await VerifyDefense(turn, human, bot, humanVisual, botVisual, 1, 1, "Mask", "Iced Americano");
            if (Environment.GetCommandLineArgs().Contains("--plan036-defense-capture-only"))
            {
                _report.scope = "Targeted production defense timing captures; other T04 gates are recorded by the full fixture run";
                await solo.StopAsync();
                solo.Dispose();
                Destroy(root);
                Destroy(Spawner.gameObject);
                return;
            }
            await VerifyAck(turn, human, bot);

            bot.IsFanActive.Value = true;
            await Task.Delay(150);
            Check(botVisual.GetAnimator().GetBool("isWind"), "bot fan state drives third-person animator");
            bot.IsFanActive.Value = false;
            await Task.Delay(100);
            Check(!botVisual.GetAnimator().GetBool("isWind"), "bot fan stop clears third-person animator");

            await VerifySpecial(turn, 0, "Cat");
            await VerifySpecial(turn, 1, "Cat");
            await VerifySpecial(turn, 0, "Hug T-shirt");
            await VerifySpecial(turn, 1, "Hug T-shirt");
            await VerifySpecial(turn, 0, "Samgyetang");
            await VerifySpecial(turn, 1, "Samgyetang");
            await VerifyInterruptedItem(turn, human, "Cat", false);
            await VerifyInterruptedItem(turn, human, "Samgyetang", true);

            bot.Temperature.Value = 0;
            turn.DebugTriggerDeathForValidation(1, false);
            await Until(() => botVisual.IsDead, 5, "bot death presentation");
            await Task.Delay(400);
            await Capture("12-bot-death-freeze");
            await Task.Delay(2500);
            Check(botVisual.IsDead && !botVisual.IsGhost, "duel bot death stays duel presentation without Multi ghost conversion");
            botVisual.ReviveVisual();
            bot.Temperature.Value = 37;
            Check(!botVisual.IsDead, "bot death visual can reset for a later duel round");

            // End while a camera-moving sequence is pending, then create fresh views.
            var oldVfx = CombatVFXManager.Instance;
            var oldFps = FPSVisualController.Instance;
            Vector3 oldCameraPosition = Camera.main.transform.position;
            turn.DebugPresentForValidation(ResultFor(0, ItemId("Hug T-shirt"), -1, 0));
            await Until(() => oldVfx.IsPlaying, 3, "in-flight presentation before stop");
            await Until(() => Vector3.Distance(Camera.main.transform.position, oldCameraPosition) > 0.01f,
                8, "camera moving before presentation stop");
            await Capture("12b-stop-during-hug-camera");
            await solo.StopAsync();
            Check(!nm.IsListening && app.SessionRouter.Current == null && oldVfx == null && oldFps == null,
                "stop during presentation destroys old views and releases local session");
            Check(!perspective.TryGetBinding(0, out _) && !perspective.TryGetOpponentBinding(out _),
                "retained old perspective rejects both despawned bindings after stop");
            int settledAfterStop = _settled.Count;
            await StartSession(solo);
            await Until(() => TurnManager.Instance != null && TurnManager.Instance.CurrentPhase.Value == TurnPhase.PrepPhase,
                25, "restarted presentation binding");
            turn = TurnManager.Instance;
            turn.DebugHoldTurnForPresentationValidation();
            await Task.Delay(500);
            Check(_settled.Count == settledAfterStop && turn.DebugPresentationBarrierState == BarrierState.Idle,
                "old presentation completion does not settle or ACK the restarted match");
            Check(FPSVisualController.Instance != null && CombatVFXManager.Instance != null
                && turn.GetPlayer(1).GetComponent<AZPlayerVisual>().IsPresentationReady
                && Vector3.Distance(Camera.main.transform.position, oldCameraPosition) < 0.01f,
                "restart creates fresh FPS and opponent views with the authored camera position");
            await Capture("13-restarted-views");
            await solo.StopAsync();
            await VerifyMissingView(solo, nm, app.SessionRouter);
            Check(UnityServices.State == ServicesInitializationState.Uninitialized,
                "local production presentation leaves UGS uninitialized");
            solo.Dispose();
            Destroy(root);
            Destroy(Spawner.gameObject);
        }

        async Task VerifyMissingView(SoloSessionCoordinator solo, NetworkManager nm, MatchSessionRouter router)
        {
            _expectMissingView = true;
            AZPlayerVisual.DebugHoldPresentationBinding = state => !state.IsBotControlled;
            await StartSession(solo);
            await Until(() => MatchCompositionRoot.Instance?.Registry.ReadyCount == 2
                && FPSVisualController.Instance != null, 15, "missing view fixture participants");
            var match = MatchCompositionRoot.Instance;
            var turn = TurnManager.Instance;
            var fps = FPSVisualController.Instance;
            Check(fps.TryGetComponent<Animator>(out var animator), "missing view fixture starts from a real FPS animator");
            Destroy(animator);
            await Task.Delay(100);
            AZPlayerVisual.DebugHoldPresentationBinding = null;
            await Task.Delay(250);
            Check(!fps.IsPresentationUsable && turn.CurrentPhase.Value == TurnPhase.WaitingForPlayers,
                "removed required FPS animator keeps presentation unready and prevents Prep");
            await Until(() => _sawMissingViewFailure && router.Current == null && !nm.IsListening,
                40, "missing view bounded failure and rollback");
            Check(_sawMissingViewFailure && MatchCompositionRoot.Instance == null,
                "missing required view fails within initialization deadline and rolls the session back");
            _expectMissingView = false;
        }

        async Task StartSession(SoloSessionCoordinator solo)
        {
            var result = await solo.StartValidationAsync(Encounter,
                new SoloConfigurationContext(Encounter.SharedOneVsOneRule, Catalog, Cosmetics));
            Check(result.IsSuccess, "production Solo coordinator starts copied duel scene: " + result.ErrorMessage);
        }

        async Task VerifyDefense(TurnManager turn, PlayerState human, PlayerState bot,
            AZPlayerVisual humanView, AZPlayerVisual botView, int attacker, int first,
            string defenseName = "Windbreaker", string attackName = "Hand Fan", string captureSuffix = "")
        {
            int fpsBefore = FPSVisualController.Instance.DebugAnimationCount;
            int botBefore = botView.DebugCombatAnimationCount;
            int humanThirdBefore = humanView.DebugCombatAnimationCount;
            uint sequence = turn.DebugPresentForValidation(ResultFor(attacker, ItemId(attackName), ItemId(defenseName), first));
            await Until(() => FPSVisualController.Instance.DebugAnimationCount > fpsBefore
                && botView.DebugCombatAnimationCount > botBefore, 8, "attack and defense animation routing");
            Check(FPSVisualController.Instance.DebugAnimationCount == fpsBefore + 1
                && botView.DebugCombatAnimationCount == botBefore + 1
                && humanView.DebugCombatAnimationCount == humanThirdBefore,
                $"{defenseName} attacker {attacker}, first {first}: attack and counter-defense route once to distinct FPS/opponent views");
            // The trigger is emitted at impact, before the Animator evaluates its
            // transition. Capture the visible pose, not only the trigger frame.
            var fpsAnimator = FPSVisualController.Instance.GetComponent<Animator>();
            var opponentAnimator = botView.GetAnimator();
            var fpsAtTrigger = fpsAnimator.GetCurrentAnimatorStateInfo(0);
            var opponentAtTrigger = opponentAnimator.GetCurrentAnimatorStateInfo(0);
            await Task.Delay(300);
            var fpsAtCapture = fpsAnimator.GetCurrentAnimatorStateInfo(0);
            var opponentAtCapture = opponentAnimator.GetCurrentAnimatorStateInfo(0);
            _report.animationStates.Add($"attacker={attacker}, first={first}; "
                + $"FPS culling={fpsAnimator.cullingMode} state={fpsAtTrigger.shortNameHash}->{fpsAtCapture.shortNameHash} normalized={fpsAtTrigger.normalizedTime:F3}->{fpsAtCapture.normalizedTime:F3}; "
                + $"opponent culling={opponentAnimator.cullingMode} state={opponentAtTrigger.shortNameHash}->{opponentAtCapture.shortNameHash} normalized={opponentAtTrigger.normalizedTime:F3}->{opponentAtCapture.normalizedTime:F3}");
            await Capture($"defense-{defenseName}-attacker{attacker}-first{first}{captureSuffix}");
            await Until(() => turn.DebugPresentationBarrierState == BarrierState.Completed
                && !CombatVFXManager.Instance.IsPlaying, 10, "human presentation ACK");
            Check(_settled.Count(x => x == sequence) == 1,
                $"{defenseName} attacker {attacker}, first {first}: one completed sequence and one human ACK");
        }

        async Task VerifyInterruptedItem(TurnManager turn, PlayerState human, string item, bool disable)
        {
            var vfx = CombatVFXManager.Instance;
            var position = Camera.main.transform.position;
            uint sequence = turn.DebugPresentForValidation(ResultFor(0, ItemId(item), -1, 0));
            // A local cat borrows the inventory view, then moves to its own temporary view.
            string transient = item == "Cat" ? "CatAnimTemp" : "FeedSprite";
            await Until(() => GameObject.Find(transient) != null, 10, "active transient before interruption: " + item);
            await Capture("interrupted-" + item);
            if (disable) vfx.enabled = false;
            else vfx.ForceSettleMultiPresentation(sequence);
            await Task.Delay(100);
            Check(GameObject.Find(transient) == null && !vfx.IsPlaying
                && Vector3.Distance(position, Camera.main.transform.position) < 0.01f,
                item + ": interruption removes transient and restores camera");
            Check(_settled.Count(x => x == sequence) == (disable ? 0 : 1),
                item + ": disable abandons without ACK; forced settlement acknowledges once");
            if (disable)
            {
                Check(turn.DebugPresentationBarrierState == BarrierState.Waiting,
                    "disable does not acknowledge unfinished presentation");
                human.PresentationAckServerRpc(sequence); // Explicit fixture release, not a product ACK.
                vfx.enabled = true;
            }
            await Until(() => turn.DebugPresentationBarrierState == BarrierState.Completed, 3, "interruption barrier release");
            await VerifyDefense(turn, human, turn.GetPlayer(1), human.GetComponent<AZPlayerVisual>(),
                turn.GetPlayer(1).GetComponent<AZPlayerVisual>(), 0, 0, captureSuffix: "-after-" + item);
        }

        async Task VerifyAck(TurnManager turn, PlayerState human, PlayerState bot)
        {
            human.DebugSuppressPresentationAck = true;
            uint sequence = turn.DebugPresentForValidation(ResultFor(0, ItemId("Hand Fan"), -1, 0));
            bot.PresentationAckServerRpc(sequence);
            await Until(() => !CombatVFXManager.Instance.IsPlaying, 10, "suppressed human ACK sequence");
            Check(turn.DebugPresentationBarrierState == BarrierState.Waiting && turn.DebugPresentationPendingCount == 1,
                "server-owned bot ACK RPC cannot satisfy the human barrier");
            human.DebugSuppressPresentationAck = false;
            human.PresentationAckServerRpc(sequence + 100);
            await Task.Delay(50);
            Check(turn.DebugPresentationPendingCount == 1, "wrong sequence human ACK cannot satisfy current barrier");
            human.PresentationAckServerRpc(sequence);
            await Until(() => turn.DebugPresentationBarrierState == BarrierState.Completed, 3, "explicit human ACK");
            Check(turn.DebugPresentationPendingCount == 0, "matching actual human RPC completes the barrier");
        }

        async Task VerifySpecial(TurnManager turn, int actor, string name)
        {
            Vector3 cameraBefore = Camera.main.transform.position;
            var human = turn.GetPlayer(0);
            var bot = turn.GetPlayer(1);
            var botView = bot.GetComponent<AZPlayerVisual>();
            var humanView = human.GetComponent<AZPlayerVisual>();
            var botRoot = botView.GetVisualRoot();
            Vector3 botBefore = botRoot.position;
            Vector3 humanBefore = human.transform.position;
            int fpsBefore = FPSVisualController.Instance.DebugAnimationCount;
            int botAnimationBefore = botView.DebugCombatAnimationCount;
            int humanThirdBefore = humanView.DebugCombatAnimationCount;
            bool botMoved = false;
            bool humanNetworkObjectMoved = false;
            bool targetFed = false;
            bool captured = false;
            uint sequence = turn.DebugPresentForValidation(ResultFor(actor, ItemId(name), -1, actor));
            double started = Time.realtimeSinceStartupAsDouble;
            while (turn.DebugPresentationBarrierState != BarrierState.Completed || CombatVFXManager.Instance.IsPlaying)
            {
                if (Time.realtimeSinceStartupAsDouble > started + 15)
                    throw new TimeoutException("Special item presentation: " + name);
                botMoved |= Vector3.Distance(botRoot.position, botBefore) > 0.01f;
                humanNetworkObjectMoved |= Vector3.Distance(human.transform.position, humanBefore) > 0.01f;
                if (name == "Samgyetang")
                    targetFed |= actor == 0 ? botView.DebugCombatAnimationCount > botAnimationBefore
                        : FPSVisualController.Instance.DebugAnimationCount > fpsBefore
                            && FPSVisualController.Instance.DebugLastTrigger == "feed";
                bool captureMoment = name == "Hug T-shirt" && actor == 1 ? botMoved
                    : name == "Samgyetang" ? targetFed : Time.realtimeSinceStartupAsDouble > started + 1.1;
                if (!captured && captureMoment)
                {
                    await Capture($"special-{name.Replace(' ', '-')}-actor{actor}");
                    captured = true;
                }
                await Task.Delay(20);
            }
            Check(_settled.Count(x => x == sequence) == 1 && Vector3.Distance(Camera.main.transform.position, cameraBefore) < 0.01f,
                $"{name} actor {actor}: sequence settles once and restores camera");
            if (name == "Hug T-shirt")
            {
                Check(!humanNetworkObjectMoved && Vector3.Distance(botRoot.position, botBefore) < 0.01f,
                    $"Hug actor {actor}: restores opponent visual and never moves the human network object");
                if (actor == 1) Check(botMoved, "bot Hug approaches the human with its actual third-person visual");
            }
            if (name == "Samgyetang")
                Check(targetFed && humanView.DebugCombatAnimationCount == humanThirdBefore,
                    $"feeding actor {actor}: target reacts in correct FPS or opponent view without hidden human 3P animation");
            Check(captured, $"{name} actor {actor}: captured its active presentation");
        }

        short ItemId(string name)
        {
            int index = Array.FindIndex(Catalog, item => item != null && item.ItemName == name);
            if (index < 0) throw new InvalidOperationException("Required current item missing: " + name);
            return (short)index;
        }

        static CombatResultData ResultFor(int actor, short item, short defense, int first)
            => new()
            {
                FirstPlayerIndex = (byte)first, WinnerIndex = -1, EventCount = 1,
                P1MainItemId = actor == 0 ? item : defense, P2MainItemId = actor == 1 ? item : defense,
                P1SubItemId = -1, P2SubItemId = -1,
                P1TempAtTurnStart = 37, P2TempAtTurnStart = 37,
                P1TempBeforeCombat = 37, P2TempBeforeCombat = 37,
                P1TempAfterCombat = 37, P2TempAfterCombat = 37,
                Event0Source = (byte)actor, Event0Target = (byte)(1 - actor), Event0ItemId = item,
                Event0UserTemp = 37, Event0TargetTemp = 37,
                Event0ImpactFlags = defense >= 0 ? CombatImpactFlags.Defense : (byte)0,
                Event0DefenseItemId = defense
            };

        async Task Capture(string name)
        {
            string directory = Path.Combine(Path.GetDirectoryName(_reportPath),
                Path.GetFileNameWithoutExtension(_reportPath) + "-screenshots");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            // Hidden Windows players can return an all-black desktop/backbuffer.
            // Submit a real camera render explicitly and validate the CPU readback.
            // This captures character/item animation, not final screen-space UI.
            await Task.Delay(20);
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("No main camera for " + name);
            const int width = 1280, height = 720;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else
                {
                    camera.targetTexture = target;
                    camera.Render();
                }
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                pixels.Apply(false);
                var colors = pixels.GetPixels32();
                int bright = 0, samples = 0, min = 765, max = 0;
                for (int i = 0; i < colors.Length; i += 31)
                {
                    int value = colors[i].r + colors[i].g + colors[i].b;
                    min = Math.Min(min, value); max = Math.Max(max, value);
                    if (value > 36) bright++;
                    samples++;
                }
                if (bright < samples / 100 || max - min < 30)
                    throw new InvalidOperationException($"Camera capture is black or uniform: {name}, bright={bright}/{samples}, range={max - min}");
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                _report.screenshots.Add(path);
                _report.captures.Add(new CaptureEvidence
                {
                    path = path, width = width, height = height,
                    brightSamples = bright, totalSamples = samples, luminanceRange = max - min
                });
                Debug.Log($"[PLAN036 T04] CAMERA CAPTURE {name}: bright={bright}/{samples}, range={max - min}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Destroy(pixels);
            }
        }
        static async Task ReturnToMenu()
        {
            if (SceneManager.GetActiveScene().path == Menu) return;
            var operation = SceneManager.LoadSceneAsync(Menu);
            while (!operation.isDone) await Task.Delay(20);
        }
        static async Task Until(Func<bool> predicate, double seconds, string label)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (!predicate())
            {
                if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException(label);
                await Task.Delay(20);
            }
        }
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            _report.checks.Add(label);
            Debug.Log("[PLAN036 T04] PASS " + label);
        }
        void OnSettled(uint sequence) => _settled.Add(sequence);
        void OnLog(string message, string stack, LogType type)
        {
            if (_expectMissingView && type == LogType.Error)
            {
                if (message == "[PlayerVisual] Visual binding failed after 5s — seat=0") return;
                if (message == "[MatchInitialization] Solo participant presentation did not finish initialization")
                {
                    _sawMissingViewFailure = true;
                    var match = MatchCompositionRoot.Instance;
                    if (match.InitializationState != MatchInitializationState.Failed
                        || TurnManager.Instance.CurrentPhase.Value != TurnPhase.WaitingForPlayers)
                        _report.errors.Add("Missing view failure entered Prep or did not latch Failed state.");
                    return;
                }
            }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _report.errors.Add(message + "\n" + stack);
        }
        void Update()
        {
            if (!_finished && _deadline > 0 && Time.realtimeSinceStartupAsDouble > _deadline)
            { _report.failure = "Presentation fixture watchdog timeout"; Finish(); }
        }
        void Finish()
        {
            if (_finished) return;
            _finished = true;
            AZPlayerVisual.DebugHoldPresentationBinding = null;
            Application.logMessageReceived -= OnLog;
            CombatVFXManager.OnPresentationSettled -= OnSettled;
            File.WriteAllText(_reportPath, JsonUtility.ToJson(_report, true));
            if (!Application.isEditor) Application.Quit(_report.passed ? 0 : 1);
        }
        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            Application.logMessageReceived -= OnLog;
            CombatVFXManager.OnPresentationSettled -= OnSettled;
        }
    }
}
#endif
