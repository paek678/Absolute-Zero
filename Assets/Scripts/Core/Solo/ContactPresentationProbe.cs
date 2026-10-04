#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Inventory;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace AbsoluteZero.Core.Solo
{
    // Explicit opt-in presentation fixture. Uses real player commands, authoritative
    // combat and presentation ACKs. Synthetic mini-game success does not test its puzzle.
    [DefaultExecutionOrder(-500)]
    public sealed class ContactPresentationProbe : MonoBehaviour
    {
        [Serializable] sealed class Cue
        {
            public string stage, fpsTrigger;
            public float time;
            public Vector3 cameraPosition, fpsPosition, targetViewport;
        }
        [Serializable] sealed class Report
        {
            public bool passed, solo;
            public int seat, actor, target, count, fpsAnimationDelta, sampledFrames;
            public string item, defense, failure;
            public bool defenseMatched, authoritativeResultReceived;
            public bool incomingHugForeground;
            public byte impactFlags;
            public float targetBefore, targetAfter, expectedTargetAfter, targetNetworkAtImpact, targetDisplayAtImpact;
            public string captureKind = "Diagnostic camera and canvas rendering, not desktop framebuffer";
            public List<string> checks = new(), captures = new();
            public List<Cue> cues = new();
            public List<ClipSample> clipSamples = new();
            public List<MotionFrame> motionFrames = new();
            public float maximumFpsRelativePositionError, maximumFpsRelativeRotationError;
            public float maximumCameraTravel, hugGestureToImpactSeconds;
            public float cameraRestorePositionError, cameraRestoreRotationError, fpsRestorePositionError;
        }
        [Serializable] sealed class ClipSample
        {
            public string role, clip;
            public int frames;
            public float maximumNormalizedTime, maximumWeight, firstSampleTime, lastSampleTime;
        }
        [Serializable] sealed class MotionFrame { public string path; public float time; }
        readonly Report _report = new();
        readonly Dictionary<Transform, Vector3> _remotePositions = new();
        readonly Dictionary<Transform, (bool enabled, int order)> _remoteSorting = new();
        readonly Queue<string> _captures = new();
        Camera _camera;
        FPSVisualController _fps;
        Vector3 _cameraPosition, _fpsPosition, _fpsRelativePosition;
        Quaternion _cameraRotation, _fpsRotation, _fpsRelativeRotation;
        bool _solo, _active, _finished;
        int _fpsCount;
        uint _settled;
        string _folder;
        AttackItemDataSO _attack;
        DefenseItemDataSO _defense;
        AZPlayerVisual _actorVisual, _targetVisual;
        bool _captureMotion;
        float _lastMotionFrame = -10f;
        static Type MiniGameHubType => Type.GetType("AbsoluteZero.UI.MiniGame.MiniGameHub, AbsoluteZero.UI");
        static bool IsMiniGameRunning => MiniGameHubType?.GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null) is true;

        static string Arg(string key, string fallback = "")
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Arg("--az-contact-item").Length > 0)
                new GameObject("Contact presentation validation").AddComponent<ContactPresentationProbe>();
        }
        async void Start()
        {
            DontDestroyOnLoad(gameObject); Application.runInBackground = true;
            Application.targetFrameRate = 30;
            _folder = Path.GetDirectoryName(Application.consoleLogPath);
            _solo = _report.solo = Arg("--az-contact-solo", "0") == "1";
            _report.item = Arg("--az-contact-item");
            _report.defense = Arg("--az-contact-defense");
            _captureMotion = Arg("--az-contact-capture-motion", "0") == "1";
            _report.actor = int.Parse(Arg("--az-contact-actor", "0"));
            _report.target = int.Parse(Arg("--az-contact-target", "1"));
            _report.count = _solo ? 2 : int.Parse(Arg("--az-count", "4"));
            CombatVFXManager.OnContactCue += OnCue;
            CombatVFXManager.OnPresentationSettled += OnSettled;
            try { await Run(); _report.passed = true; }
            catch (Exception error)
            {
                _report.failure = error.ToString();
                Debug.LogError("[CONTACT] FAIL " + error.Message);
                try { Capture("failure"); } catch { }
            }
            _finished = true;
            CombatVFXManager.OnContactCue -= OnCue;
            CombatVFXManager.OnPresentationSettled -= OnSettled;
            TurnManager.OnCombatResult -= OnDuelResult;
            TurnManager.OnMultiCombatResult -= OnMultiResult;
            File.WriteAllText(Path.ChangeExtension(Application.consoleLogPath, ".contact.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("[CONTACT] " + (_report.passed ? "PASS" : "FAILED") + " checks=" + _report.checks.Count);
            await Task.Delay(300);
            Application.Quit(_report.passed ? 0 : 2);
        }
        void Update()
        {
            if (!_solo || _finished) return;
            // Isolate this single action; the normal bot and human submission
            // boundaries remain in use, without a competing random BT action.
            foreach (var controller in FindObjectsByType<BotTurnController>(FindObjectsSortMode.None))
                if (controller.enabled) controller.enabled = false;
        }
        void LateUpdate()
        {
            if (_active)
            {
                if (_camera != null)
                    _report.maximumCameraTravel = Mathf.Max(_report.maximumCameraTravel,
                        Vector3.Distance(_cameraPosition, _camera.transform.position));
                SampleAnimator(_fps != null ? _fps.GetComponent<Animator>() : null, "FPS");
                if (_actorVisual != null && !_actorVisual.IsLocalHuman)
                    SampleAnimator(_actorVisual.GetAnimator(), "ActorWorld");
                if (_targetVisual != null && !_targetVisual.IsLocalHuman)
                    SampleAnimator(_targetVisual.GetAnimator(), "TargetWorld");
            }
            if (_active && _camera != null && _fps != null && _report.item == "Hug T-shirt" && _report.seat == _report.actor)
            {
                _report.sampledFrames++;
                _report.maximumFpsRelativePositionError = Mathf.Max(_report.maximumFpsRelativePositionError,
                    Vector3.Distance(_fpsRelativePosition, _camera.transform.InverseTransformPoint(_fps.transform.position)));
                _report.maximumFpsRelativeRotationError = Mathf.Max(_report.maximumFpsRelativeRotationError,
                    Quaternion.Angle(_fpsRelativeRotation, Quaternion.Inverse(_camera.transform.rotation) * _fps.transform.rotation));
            }
            if (_captures.Count > 0) Capture(_captures.Dequeue());
            if (_active && _captureMotion && Time.realtimeSinceStartup - _lastMotionFrame >= 0.125f)
            {
                float time = Time.realtimeSinceStartup;
                string path = Path.ChangeExtension(Application.consoleLogPath, $".motion{_report.motionFrames.Count:D3}.png");
                SoloLaunchProbe.WriteRender(path);
                _report.motionFrames.Add(new MotionFrame { path = path, time = time });
                _lastMotionFrame = time;
            }
        }
        PlayerState[] Players() => FindObjectsByType<PlayerState>(FindObjectsSortMode.None)
            .Where(p => p.IsSpawned && p.IsParticipantReady).OrderBy(p => p.PlayerIndex).ToArray();
        async Task Run()
        {
            if (_solo)
            {
                await Until(() => AppBootstrapper.Instance?.IsReady == true && GameObject.Find("MainUI/MainPanel/SoloBtn") != null, "lobby ready");
                GameObject.Find("MainUI/MainPanel/SoloBtn").GetComponent<Button>().onClick.Invoke();
                await Task.Delay(100);
                GameObject.Find("MainUI/SoloSelectionPanel/Body/StartBtn").GetComponent<Button>().onClick.Invoke();
            }
            await Until(() => MatchCompositionRoot.Instance?.IsSessionCurrent == true && Players().Length == _report.count
                && Players().All(p => p.GetComponent<AZPlayerVisual>().IsPresentationReady)
                && TurnManager.Instance.CurrentPhase.Value == TurnPhase.PrepPhase
                && IceboxController.Instance?.IsAnimating != true && GameObject.Find("LoadingScreenCanvas") == null,
                "all participants and gameplay views ready", 70);
            var players = Players();
            var local = players.First(p => p.IsOwner && !p.IsBotControlled);
            var actor = players.First(p => p.PlayerIndex == _report.actor);
            var target = players.First(p => p.PlayerIndex == _report.target);
            _actorVisual = actor.GetComponent<AZPlayerVisual>();
            _targetVisual = target.GetComponent<AZPlayerVisual>();
            _report.seat = local.PlayerIndex;
            Check(_report.actor != _report.target && players.Any(p => p.PlayerIndex == _report.target), "valid separate actor and target seats");
            var views = MatchCompositionRoot.Instance.ViewBindings;
            _camera = views.GameplayCamera; _fps = FPSVisualController.Instance;
            Check(_camera != null && _fps?.IsPresentationUsable == true, "bound gameplay camera and FPS ready");
            if (_report.defense.Length > 0)
            {
                _attack = ItemManager.Instance.GetAllItems().FirstOrDefault(i => i.ItemName == _report.item) as AttackItemDataSO;
                _defense = ItemManager.Instance.GetAllItems().FirstOrDefault(i => i.ItemName == _report.defense) as DefenseItemDataSO;
                Check(_defense != null && _attack != null && !_attack.EqualizeToUserTemp,
                    "defense fixture uses an existing defense and ordinary direct damage item");
                _report.defenseMatched = _defense.Filter == DamageFilter.All || _defense.Filter == _attack.AttackFilter;
                TurnManager.OnCombatResult += OnDuelResult;
                TurnManager.OnMultiCombatResult += OnMultiResult;
            }
            if (NetworkManager.Singleton.IsServer)
            {
                short id = (short)Array.FindIndex(ItemManager.Instance.GetAllItems(), i => i.ItemName == _report.item);
                Check(id >= 0, "fixture item exists in current catalog");
                foreach (var p in players)
                {
                    p.FanSpeed.Value = 0; // Prevent cooling/round end from obscuring this presentation case.
                    p.Temperature.Value = 37;
                    p.GetInventory().SlotStates.Clear();
                }
                Check(actor.GetInventory().GrantSpecificItem(id), "server grants one test item without changing SO data");
                if (_defense != null)
                {
                    short defenseId = (short)Array.FindIndex(ItemManager.Instance.GetAllItems(), i => i == _defense);
                    Check(target.GetInventory().GrantSpecificItem(defenseId), "server grants original defense item to target");
                }
                File.WriteAllText(Path.Combine(_folder, "fixture.ready"), "ready");
            }
            await Until(() => File.Exists(Path.Combine(_folder, "fixture.ready"))
                && actor.GetInventory().SlotStates.Count == 1
                && ItemManager.Instance.GetItemData(actor.GetInventory().SlotStates[0].ItemId)?.ItemName == _report.item
                && (_defense == null || (target.GetInventory().SlotStates.Count == 1
                    && ItemManager.Instance.GetItemData(target.GetInventory().SlotStates[0].ItemId) == _defense)),
                "fixture replicated to every peer");
            await Barrier("ready");
            if (local == actor || (_solo && actor.IsBotControlled)) await Select(actor, (byte)_report.target);
            if (_defense != null && (local == target || (_solo && target.IsBotControlled))) await Select(target, (byte)_report.target);
            await Until(() => actor.HasSelectedItem.Value && (_defense == null || target.HasSelectedItem.Value), "real authoritative item selections accepted");
            Check(players.Where(p => p != actor && (_defense == null || p != target)).All(p => !p.HasSelectedItem.Value), "only designated actor and optional defender have actions");
            _cameraPosition = _camera.transform.position; _cameraRotation = _camera.transform.rotation;
            _fpsPosition = _fps.transform.position; _fpsRotation = _fps.transform.rotation;
            // Contact framing temporarily uses the authored duel-like camera pose;
            // the original world pose above remains the independent restoration oracle.
            Pose contactPose = _fps.GetContactCameraPose();
            _fpsRelativePosition = contactPose.position;
            _fpsRelativeRotation = contactPose.rotation;
            _fpsCount = _fps.DebugAnimationCount;
            foreach (var p in players.Where(p => p != local))
            {
                var root = p.GetComponent<AZPlayerVisual>().GetVisualRoot();
                if (root != null)
                {
                    _remotePositions[root] = root.position;
                    var group = root.GetComponent<SortingGroup>();
                    _remoteSorting[root] = (group != null && group.enabled, group != null ? group.sortingOrder : 0);
                }
            }
            Capture("before");
            int turn = TurnManager.Instance.TurnNumber.Value;
            await Barrier("selected");
            if (_solo)
            {
                Check(TurnManager.Instance.TryGetPrepInputSnapshot(out var snapshot), "bot Ready has live prep key");
                Check(players.First(p => p.IsBotControlled).GetBotCommands().Ready(snapshot.Key).Status == PlayerActionStatus.ReadyAccepted,
                    "bot Ready accepted through trusted boundary");
            }
            ProbeInventoryCommands.Ready(local);
            await Until(() => _report.cues.Any(c => c.stage == "end"), "contact choreography completed", 45);
            await Until(() => _settled != 0 && !CombatVFXManager.Instance.IsPlaying
                && TurnManager.Instance.CurrentPhase.Value == TurnPhase.PrepPhase
                && TurnManager.Instance.TurnNumber.Value > turn, "presentation ACK and next turn completed", 45);
            Check(_report.cues.Select(c => c.stage).SequenceEqual(new[] { "begin", "gesture", "impact", "end" }), "exactly one ordered begin, gesture, impact and end on this peer");
            if (_defense != null)
            {
                Check(_report.authoritativeResultReceived, "authoritative item result observed on this peer");
                Check(Mathf.Abs(_report.targetAfter - _report.expectedTargetAfter) < .01f, "original defense filter and block amount preserve damage outcome");
                Check(((_report.impactFlags & CombatImpactFlags.Defense) != 0) == _report.defenseMatched, "defense cue matches actual damage filter");
                Check(Mathf.Abs(_report.targetNetworkAtImpact - _report.targetAfter) < .01f
                    && Mathf.Abs(_report.targetDisplayAtImpact - _report.targetAfter) < .01f,
                    "replicated temperature and presentation result agree at impact");
            }
            _report.cameraRestorePositionError = Vector3.Distance(_cameraPosition, _camera.transform.position);
            _report.cameraRestoreRotationError = Quaternion.Angle(_cameraRotation, _camera.transform.rotation);
            _report.fpsRestorePositionError = Vector3.Distance(_fpsPosition, _fps.transform.position);
            Check(_report.cameraRestorePositionError < .002f && _report.cameraRestoreRotationError < .1f, "camera position and rotation restored");
            Check(_report.fpsRestorePositionError < .002f && Quaternion.Angle(_fpsRotation, _fps.transform.rotation) < .1f, "FPS root position and rotation restored");
            Check(_remotePositions.All(pair => pair.Key != null && Vector3.Distance(pair.Key.position, pair.Value) < .002f), "remote character roots restored");
            Check(_remoteSorting.All(pair =>
            {
                var group = pair.Key != null ? pair.Key.GetComponent<SortingGroup>() : null;
                return (group != null && group.enabled) == pair.Value.enabled
                    && (group != null ? group.sortingOrder : 0) == pair.Value.order;
            }), "remote character foreground sorting restored");
            _report.fpsAnimationDelta = _fps.DebugAnimationCount - _fpsCount;
            bool hug = _report.item == "Hug T-shirt";
            int expectedFps = _report.seat == _report.actor || (!hug && _report.seat == _report.target) ? 1 : 0;
            Check(_report.fpsAnimationDelta == expectedFps, "FPS action plays exactly once for the correct local role");
            if (_report.seat == _report.actor)
            {
                Check(_fps.DebugLastTrigger == (hug ? "hug" : "feed"), "actor uses original gesture clip");
                CheckClip("FPS", hug ? "FPS_hug" : "FPS_feed");
                float elapsed = _report.cues.First(c => c.stage == "end").time - _report.cues.First(c => c.stage == "gesture").time;
                Check(elapsed + .03f >= _fps.GetClipDuration(hug ? "hug" : "feed", 1f), "actor gesture is allowed its authored clip duration");
            }
            else if (!hug && _report.seat == _report.target)
            {
                string targetTrigger = _report.defenseMatched ? _defense.AnimTrigger : "use";
                Check(_fps.DebugLastTrigger == targetTrigger, _report.defenseMatched
                    ? "recipient plays defense once without overlapping receiving motion"
                    : "recipient uses receiving prop motion without giver hands");
                CheckClip("FPS", targetTrigger == "use" ? "FPS_useItem" : "FPS_" + targetTrigger);
                float elapsed = _report.cues.First(c => c.stage == "end").time - _report.cues.First(c => c.stage == "impact").time;
                Check(elapsed + .03f >= _fps.GetClipDuration(targetTrigger, 1f), "recipient reaction is allowed its authored clip duration");
            }
            if (hug && _report.seat == _report.actor)
            {
                Check(_report.sampledFrames >= 10 && _report.maximumFpsRelativePositionError < .01f
                    && _report.maximumFpsRelativeRotationError < .2f, "FPS root follows the configured contact camera pose without slipping");
                Check(_report.maximumCameraTravel > .1f, "hug approach visibly moves the camera");
                var impact = _report.cues.First(c => c.stage == "impact");
                Check(impact.targetViewport.z > 0 && Mathf.Abs(impact.targetViewport.x - .5f) < .2f,
                    "hug target centered horizontally at contact");
            }
            if (hug)
            {
                Check(!_report.clipSamples.Any(s => s.role == "ActorWorld" && s.clip == "playerA_jump"),
                    "contact approach and return preserve the assembled silhouette without the split-torso jump clip");
                if (_report.seat == _report.target)
                    Check(_report.incomingHugForeground, "incoming hug stays in front of the local icebox");
                _report.hugGestureToImpactSeconds = _report.cues.First(c => c.stage == "impact").time
                    - _report.cues.First(c => c.stage == "gesture").time;
                Check(_report.hugGestureToImpactSeconds >= .49f, "hug impact waits for the authored half-second contact pose");
            }
            if (!_actorVisual.IsLocalHuman)
                CheckClip("ActorWorld", "playerA_" + _actorVisual.DebugLastCombatTrigger);
            if (!hug && !_targetVisual.IsLocalHuman)
                CheckClip("TargetWorld", "playerA_" + _targetVisual.DebugLastCombatTrigger);
            Capture("after");
            await Barrier("finished");
            if (NetworkManager.Singleton.IsServer)
                await MatchCompositionRoot.Instance.GetComponent<MatchDepartureController>().LeaveAsync();
            await Until(() => SceneManager.GetActiveScene().name == "LobbyScene"
                && AppBootstrapper.Instance.SessionRouter.Current == null && !NetworkManager.Singleton.IsListening,
                "normal shutdown and lobby return", 25);
        }
        async Task Select(PlayerState actor, byte selectedTarget)
        {
            if (actor.IsBotControlled)
            {
                Check(TurnManager.Instance.TryGetPrepInputSnapshot(out var snapshot), "bot action has live prep key");
                var result = actor.GetBotCommands().BeginUse(snapshot.Key, 0x4401, actor.GetInventory().SlotStates[0].CopyId, selectedTarget);
                Check(result.Status == PlayerActionStatus.Pending, "bot uses configured delay boundary");
                return;
            }
            MiniGameTicket ticket = default;
            Action<MiniGameTicket> started = value => ticket = value;
            actor.OnMiniGameStart += started;
            try
            {
                ProbeInventoryCommands.SelectItem(actor, 0, selectedTarget);
                if (actor.GetInventory().GetItemData(0).RequiresMiniGame)
                {
                    await Until(() => ticket.AttemptId != 0 && IsMiniGameRunning, "real mini-game ticket and UI opened");
                    await Task.Delay(750);
                    // Only the puzzle outcome is simulated. Finish invokes MiniGameHub's
                    // ordinary ticketed result RPC and its normal outro/cleanup.
                    // The fixture stays in Core and does not add a Core -> UI assembly dependency.
                    var miniGameType = Type.GetType("AbsoluteZero.UI.MiniGame.MiniGameUIBase, AbsoluteZero.UI", true);
                    var active = FindObjectsByType(miniGameType, FindObjectsSortMode.None).First();
                    miniGameType.GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(active, new object[] { true });
                    await Until(() => !IsMiniGameRunning, "simulated success completed ordinary mini-game UI and RPC");
                }
            }
            finally { if (actor != null) actor.OnMiniGameStart -= started; }
        }
        void OnCue(int actor, int target, string item, string stage)
        {
            if (actor != _report.actor || target != _report.target || item != _report.item) return;
            var targetPlayer = Players().FirstOrDefault(p => p.PlayerIndex == target);
            var targetRoot = targetPlayer?.GetComponent<AZPlayerVisual>().GetVisualRoot();
            _report.cues.Add(new Cue
            {
                stage = stage, time = Time.realtimeSinceStartup,
                fpsTrigger = _fps?.DebugLastTrigger,
                cameraPosition = _camera != null ? _camera.transform.position : Vector3.zero,
                fpsPosition = _fps != null ? _fps.transform.position : Vector3.zero,
                targetViewport = _camera != null && targetRoot != null ? _camera.WorldToViewportPoint(targetRoot.position) : Vector3.zero
            });
            _active = stage != "end";
            if (stage == "impact" && item == "Hug T-shirt" && _report.seat == target)
            {
                var group = _actorVisual?.GetVisualRoot()?.GetComponent<SortingGroup>();
                _report.incomingHugForeground = group != null && group.enabled && group.sortingOrder >= 80;
            }
            if (_defense != null && stage == "impact" && targetPlayer != null)
            {
                _report.targetNetworkAtImpact = targetPlayer.Temperature.Value;
                _report.targetDisplayAtImpact = CombatVFXManager.Instance.TryGetDisplayTemperature(target, out float display) ? display : -999f;
            }
            if (stage == "impact" || stage == "end") _captures.Enqueue(stage);
            Debug.Log($"[CONTACT] CUE actor={actor} target={target} item={item} stage={stage}");
        }
        void OnSettled(uint sequence) => _settled = sequence;
        void SampleAnimator(Animator animator, string role)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;
            SampleClips(animator.GetCurrentAnimatorClipInfo(0), animator.GetCurrentAnimatorStateInfo(0), role);
            if (animator.IsInTransition(0))
                SampleClips(animator.GetNextAnimatorClipInfo(0), animator.GetNextAnimatorStateInfo(0), role);
        }
        void SampleClips(AnimatorClipInfo[] clips, AnimatorStateInfo state, string role)
        {
            foreach (var info in clips)
            {
                if (info.clip == null || info.weight < .05f) continue;
                var sample = _report.clipSamples.FirstOrDefault(s => s.role == role && s.clip == info.clip.name);
                if (sample == null)
                {
                    sample = new ClipSample { role = role, clip = info.clip.name, firstSampleTime = Time.realtimeSinceStartup };
                    _report.clipSamples.Add(sample);
                }
                sample.frames++;
                sample.maximumNormalizedTime = Mathf.Max(sample.maximumNormalizedTime, state.normalizedTime);
                sample.maximumWeight = Mathf.Max(sample.maximumWeight, info.weight);
                sample.lastSampleTime = Time.realtimeSinceStartup;
            }
        }
        void CheckClip(string role, string clip)
        {
            var sample = _report.clipSamples.FirstOrDefault(s => s.role == role && s.clip == clip);
            Check(sample != null && sample.frames >= 2 && sample.maximumWeight > .5f
                && sample.maximumNormalizedTime >= .9f,
                role + " actually samples " + clip + " through at least 90% of its authored duration");
        }
        void OnDuelResult(CombatResultData result)
        {
            for (int i = 0; i < result.EventCount; i++)
            {
                int actor = i == 0 ? result.Event0Source : result.Event1Source;
                int target = i == 0 ? result.Event0Target : result.Event1Target;
                short item = i == 0 ? result.Event0ItemId : result.Event1ItemId;
                if (actor != _report.actor || target != _report.target || ItemManager.Instance.GetItemData(item) != _attack) continue;
                RecordResult(target == 0 ? result.P1TempBeforeCombat : result.P2TempBeforeCombat,
                    i == 0 ? result.Event0TargetTemp : result.Event1TargetTemp,
                    i == 0 ? result.Event0ImpactFlags : result.Event1ImpactFlags);
            }
        }
        void OnMultiResult(CombatResolutionBatchNetData batch)
        {
            for (int i = 0; i < batch.EventCount; i++)
            {
                var evt = batch.Events[i];
                if (evt.ActorSeat == _report.actor && evt.TargetSeat == _report.target && ItemManager.Instance.GetItemData(evt.ItemId) == _attack)
                    RecordResult(batch.TempBefore[_report.target], evt.TargetResultTemp, evt.Flags);
            }
        }
        void RecordResult(float before, float after, byte flags)
        {
            _report.authoritativeResultReceived = true;
            _report.targetBefore = before; _report.targetAfter = after; _report.impactFlags = flags;
            float damage = Mathf.Max(0f, _attack.Damage - (_report.defenseMatched ? _defense.BlockAmount : 0f));
            _report.expectedTargetAfter = Mathf.Max(0f, before - damage);
        }
        async Task Barrier(string stage)
        {
            File.WriteAllText(Path.Combine(_folder, stage + "-" + _report.seat + ".ready"), "observed");
            await Until(() => Enumerable.Range(0, _solo ? 1 : _report.count)
                .All(s => File.Exists(Path.Combine(_folder, stage + "-" + s + ".ready"))), "peer barrier " + stage);
        }
        void Capture(string label)
        {
            string path = Path.ChangeExtension(Application.consoleLogPath, "." + label + ".png");
            SoloLaunchProbe.WriteRender(path); _report.captures.Add(path);
        }
        async Task Until(Func<bool> condition, string label, float seconds = 25)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) await Task.Delay(30);
            Check(condition(), label);
        }
        void Check(bool pass, string label)
        {
            if (!pass) throw new InvalidOperationException(label);
            _report.checks.Add(label);
        }
        void OnDestroy()
        {
            CombatVFXManager.OnContactCue -= OnCue;
            CombatVFXManager.OnPresentationSettled -= OnSettled;
            TurnManager.OnCombatResult -= OnDuelResult;
            TurnManager.OnMultiCombatResult -= OnMultiResult;
        }
    }
}
#endif
