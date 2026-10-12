#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Core.Solo
{
    [Serializable] public sealed class PlaytestItemRecord
    {
        public int ItemId, RemainingUses, Flags;
        public uint CopyId;
        public string itemName;
        public static PlaytestItemRecord From(Item.ItemSlotNetData slot) => new PlaytestItemRecord
        { ItemId = slot.ItemId, RemainingUses = slot.RemainingUses, Flags = slot.Flags, CopyId = slot.CopyId };
    }
    [Serializable] public sealed class PlaytestSeatRecord
    {
        public int seat, score, wins; public float temperature, fan;
        public bool ready, selected, bot, fanActive, basicsBlocked; public string life;
        public PlaytestItemRecord[] inventory;
    }
    [Serializable] public sealed class PlaytestRecord
    {
        public int schema = 1;
        public long index, receipt; public double time, serverTime;
        public string kind, detail, scene, phase, matchState, mode;
        public bool server; public int actor = -1, turn, round;
        public uint copy, matchEpoch, roundEpoch, inventoryRevision, grantTransaction, resultSequence;
        public byte winnerMask, possessionSpent, possessed, ghostDamage;
        public bool terminalReleased;
        public PlaytestSeatRecord[] seats;
        public BalanceObservation balance;
    }
    // Development-only observer. Records state; never owns gameplay, phase or RNG.
    public sealed partial class PlaytestTelemetry : MonoBehaviour
    {
        public static PlaytestTelemetry Current { get; private set; }
        public string Folder { get; private set; }
        PlaytestJsonlSink _sink;
        long _index, _receipt, _activeReceipt;
        float _next;
        string _lastKey;
        uint _settled;
        bool _closed;
        float _captureAt = -1;
        int _captures;
        string _captureLabel;
        public int Errors { get; private set; }
        public int ObserverFaults { get; private set; }
        public bool IsHealthy => ObserverFaults == 0 && _sink != null && _sink.Error == null && _sink.Dropped == 0;
        public long ActiveReceipt => _activeReceipt;
        public void Transfer(int fromSeat, int toSeat, int item, uint fromCopy, uint toCopy, int fromUses, int toBefore, int toAfter)
            => Record("copy_transfer", $"from={fromSeat};to={toSeat};item={item};fromCopy={fromCopy};toCopy={toCopy};fromUses={fromUses};toBefore={toBefore};toAfter={toAfter}", toSeat, toCopy);
        public void Temperature(string cause, int target, float before, float after, float requested, long receipt = 0)
        {
            Record("temperature_effect", FormattableString.Invariant($"cause={cause};target={target};before={before:R};after={after:R};requested={requested:R}"), target, receipt: receipt);
        }
        public static string Arg(string key, string fallback = "")
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string folder = Arg("--az-playtest");
            if (string.IsNullOrWhiteSpace(folder)) return;
            var go = new GameObject(nameof(PlaytestTelemetry)); DontDestroyOnLoad(go);
            var recorder = go.AddComponent<PlaytestTelemetry>();
            try
            {
                recorder.Folder = Path.GetFullPath(folder);
                recorder._sink = new PlaytestJsonlSink(Path.Combine(recorder.Folder, "events.jsonl"));
                Current = recorder;
                Application.logMessageReceived += recorder.Log;
                CombatVFXManager.OnPresentationSettled += recorder.Settled;
                recorder.Record("recorder_started", "observational; gameplay seed=" + Arg("--az-gameplay-seed", "production RNG"));
            }
            catch (Exception error) { Debug.LogError("[PLAYTEST] recorder initialization failed: " + error.GetType().Name); Application.Quit(3); }
        }
        void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            {
                Errors++;
                // Avoid copying UGS IDs, account names or join tokens from arbitrary messages.
                Record("diagnostic_error", type + ":" + message.Split('\n')[0].Split(':')[0]);
            }
            if (message.StartsWith("[MatchResult] Visible seq=")) Record("result_visible", message);
        }
        void Settled(uint sequence)
        {
            _settled = sequence; Record("presentation_settled", sequence.ToString());
            ScheduleCapture("settled-" + sequence, .05f);
        }
        void ScheduleCapture(string label, float delay)
        {
            if (_captures >= 12 || _captureAt >= 0) return;
            _captureAt = Time.unscaledTime + delay; _captureLabel = label;
        }
        void Update()
        {
            if (!_closed && _captureAt >= 0 && Time.unscaledTime >= _captureAt)
            {
                _captureAt = -1;
                double started = Time.realtimeSinceStartupAsDouble;
                try
                {
                    string path = Path.Combine(Folder, "visual-" + (++_captures) + "-" + _captureLabel + ".png");
                    SoloLaunchProbe.WriteRender(path);
                    Record("capture", Path.GetFileName(path) + ";seconds=" + (Time.realtimeSinceStartupAsDouble - started).ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
                }
                catch { ObserverFaults++; }
            }
            if (_closed || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + .5f;
            var turn = TurnManager.Instance;
            var root = MatchCompositionRoot.Instance;
            string key = SceneManager.GetActiveScene().name + "/" + root?.Generation + "/" + root?.MatchManager?.RoundNumber.Value
                + "/" + turn?.TurnNumber.Value + "/" + turn?.CurrentPhase.Value;
            if (key != _lastKey && turn != null && turn.IsSpawned && turn.CurrentPhase.Value == Common.TurnPhase.AttackPhase)
                ScheduleCapture("attack-" + turn.TurnNumber.Value, 1);
            Record(key != _lastKey ? "phase" : "sample", ""); _lastKey = key;
        }
        public void Record(string kind, string detail, int actor = -1, uint copy = 0, long receipt = 0, BalanceObservation balance = null)
        {
            if (_closed || _sink == null) return;
            try
            {
                var root = MatchCompositionRoot.Instance; var ns = root?.NetworkState;
                var manager = root?.MatchManager; var turn = TurnManager.Instance; var nm = NetworkManager.Singleton;
                var record = new PlaytestRecord
                {
                    index = ++_index, kind = kind, detail = detail, actor = actor, copy = copy, receipt = receipt != 0 ? receipt : _activeReceipt,
                    time = Time.realtimeSinceStartupAsDouble, serverTime = nm != null && nm.IsListening ? nm.ServerTime.Time : 0,
                    server = nm != null && nm.IsServer, scene = SceneManager.GetActiveScene().name,
                    phase = turn != null && turn.IsSpawned ? turn.CurrentPhase.Value.ToString() : "none",
                    turn = turn != null && turn.IsSpawned ? turn.TurnNumber.Value : 0,
                    round = manager != null && manager.IsSpawned ? manager.RoundNumber.Value : 0,
                    matchState = manager != null && manager.IsSpawned ? manager.CurrentMatchState.Value.ToString() : "none",
                    mode = root?.ActiveConfig?.Mode.ToString() ?? "none", resultSequence = _settled,
                    matchEpoch = ns != null && ns.IsSpawned ? ns.GhostMatchEpoch.Value : 0,
                    roundEpoch = ns != null && ns.IsSpawned ? ns.GhostRoundEpoch.Value : 0,
                    inventoryRevision = ns?.InventoryReadModel?.Revision ?? 0,
                    grantTransaction = ns?.InventoryReadModel?.CommittedGrantTransaction ?? 0,
                    seats = CaptureSeats(), balance = balance
                };
                if (ns != null && ns.IsSpawned)
                {
                    record.winnerMask = ns.TerminalResult.Value.WinnerMask;
                    record.terminalReleased = ns.TerminalResult.Value.Released;
                    record.possessionSpent = ns.GhostPossessionSpentMask.Value;
                    record.possessed = ns.GhostPossessedMask.Value;
                    record.ghostDamage = ns.GhostDamageMask.Value;
                }
                _sink.Append(JsonUtility.ToJson(record));
            }
            catch { ObserverFaults++; }
        }
        static PlaytestSeatRecord[] CaptureSeats()
        {
            var root = MatchCompositionRoot.Instance; var ns = root?.NetworkState; var manager = root?.MatchManager;
            return FindObjectsByType<PlayerState>(FindObjectsSortMode.None)
                .Where(p => p.IsSpawned && p.IsParticipantReady).OrderBy(p => p.PlayerIndex).Select(p =>
                {
                    var slots = p.GetInventory()?.SlotStates;
                    var inventory = new List<PlaytestItemRecord>();
                    if (slots != null) for (int i = 0; i < slots.Count; i++)
                    {
                        var record = PlaytestItemRecord.From(slots[i]);
                        record.itemName = p.GetInventory().GetItemData(i)?.ItemName;
                        inventory.Add(record);
                    }
                    return new PlaytestSeatRecord
                    {
                        seat = p.PlayerIndex, temperature = p.Temperature.Value, fan = p.FanSpeed.Value,
                        life = p.CurrentLifeState.Value.ToString(), bot = p.IsBotControlled,
                        ready = p.IsReady.Value, selected = p.HasSelectedItem.Value, inventory = inventory.ToArray(),
                        fanActive = p.IsFanActive.Value, basicsBlocked = p.IsBasicBlocked.Value,
                        score = ns != null && ns.IsSpawned && p.PlayerIndex < ns.KillScores.Count ? ns.KillScores[p.PlayerIndex] : 0,
                        wins = manager != null && manager.IsSpawned ? (p.PlayerIndex == 0 ? manager.P1RoundWins.Value : p.PlayerIndex == 1 ? manager.P2RoundWins.Value : 0) : 0
                    };
                }).ToArray();
        }
        public static CommandObservation Observe(PlayerState player, string command, uint copy = 0)
            => Observe(player != null ? player.PlayerIndex : -1, command, copy);
        public static CommandObservation Observe(int seat, string command, uint copy = 0)
        {
            var recorder = Current;
            if (recorder == null || recorder._closed) return default;
            long receipt = ++recorder._receipt;
            long parent = recorder._activeReceipt;
            recorder._activeReceipt = receipt;
            recorder.Record("command_received", command + ";parentReceipt=" + parent, seat, copy, receipt);
            return new CommandObservation(recorder, seat, command, copy, receipt, parent);
        }
        public readonly struct CommandObservation : IDisposable
        {
            readonly PlaytestTelemetry _recorder; readonly int _seat; readonly string _command; readonly uint _copy; readonly long _receipt, _parent;
            internal CommandObservation(PlaytestTelemetry recorder, int seat, string command, uint copy, long receipt, long parent)
            { _recorder = recorder; _seat = seat; _command = command; _copy = copy; _receipt = receipt; _parent = parent; }
            public void Verdict(string verdict) { if (_recorder != null) _recorder.Record("command_verdict", verdict, _seat, _copy, _receipt); }
            // A return is not an acceptance. The analyzer must use verdict records or mark unknown.
            public void Dispose()
            {
                if (_recorder == null) return;
                _recorder.Record("command_returned", _command, _seat, _copy, _receipt);
                _recorder._activeReceipt = _parent;
            }
        }
        public bool Close()
        {
            if (_closed) return _sink?.Complete == true && ObserverFaults == 0;
            Record("recorder_stopping", "errors=" + Errors + ";observerFaults=" + ObserverFaults);
            _closed = true; Application.logMessageReceived -= Log;
            CombatVFXManager.OnPresentationSettled -= Settled;
            _sink?.Dispose();
            if (Current == this) Current = null;
            return _sink?.Complete == true && ObserverFaults == 0;
        }
        void OnDestroy() => Close();
        void OnApplicationQuit() => Close();
    }
}
#endif
