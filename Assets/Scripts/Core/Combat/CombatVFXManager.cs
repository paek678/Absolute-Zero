using System.Collections;
using System.Collections.Generic;
using AbsoluteZero.Core.Audio;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Turn;
using AbsoluteZero.Core.Inventory;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Combat
{
    public class CombatVFXManager : MonoBehaviour
    {
        public static CombatVFXManager Instance { get; private set; }

        [SerializeField] GameObject _hitEffectPrefab;
        [SerializeField] GameObject _iceBreakEffectPrefab;
        [SerializeField] GameObject _finalBreakEffectPrefab;

        public bool IsPlaying { get; private set; }

        public event System.Action OnTempOverridesClear;
        public event System.Action<float, float> OnTempTargetsOverride;
        public static event System.Action<byte> OnSuppressedItemCue;
        public event System.Action<int, float> OnPlayerTempOverride;
        public static event System.Action<int> OnAttackerChanged;
        public static event System.Action<uint> OnPresentationSettled;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static event System.Action<uint> OnGhostImpactSignaled;
#endif

        readonly Dictionary<int, float> _displayTemperatures = new();
        public bool TryGetDisplayTemperature(int seat, out float value)
            => _displayTemperatures.TryGetValue(seat, out value);

        PresentationExecution _execution;
        uint _lastCompletedDuelSequence;
        Coroutine _multiPresentationQueueRoutine;
        uint _lastCompletedMultiSequence;
        readonly Queue<PendingMultiPresentation> _pendingMultiPresentations = new();
        readonly HashSet<uint> _ghostImpactIds = new();
        readonly Queue<uint> _ghostImpactOrder = new();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool _debugAckFaultArmed;
        bool _debugDropAck;
        float _debugAckDelaySeconds;

        public void DebugSetNextPresentationAckFault(float delaySeconds, bool drop)
        {
            _debugAckFaultArmed = true;
            _debugDropAck = drop;
            _debugAckDelaySeconds = Mathf.Max(0f, delaySeconds);
        }
#endif

        public void SignalGhostImpact(uint castId)
        {
            if (castId == 0 || !_ghostImpactIds.Add(castId)) return;
            _ghostImpactOrder.Enqueue(castId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            OnGhostImpactSignaled?.Invoke(castId);
#endif
            while (_ghostImpactOrder.Count > 32)
                _ghostImpactIds.Remove(_ghostImpactOrder.Dequeue());
        }
        public bool HasPendingPresentation(uint sequence)
        {
            if (_execution != null && !_execution.Settled && _execution.Sequence == sequence) return true;
            foreach (var pending in _pendingMultiPresentations)
                if (pending.Sequence == sequence) return true;
            return false;
        }

        public void ForceSettleMultiPresentation(uint sequence)
        {
            var execution = _execution;
            if (execution == null || execution.Settled || execution.Sequence != sequence) return;
            Debug.LogWarning($"[CombatVFX] Authoritative settlement forced for seq={sequence}");
            CompletePresentationSequence(execution, Settlement.Forced);
            _lastCompletedMultiSequence = sequence;
            if (_pendingMultiPresentations.Count > 0 && isActiveAndEnabled && Application.isPlaying)
                _multiPresentationQueueRoutine = StartCoroutine(DrainMultiPresentationQueue());
        }

        struct PendingMultiPresentation
        {
            public CombatPresentationContext Context;
            public bool IsCombat;
            public CombatResolutionBatchNetData Batch;
            public byte DeathMask;
            public bool EndsRound;
            public uint Sequence;
            public uint GhostCastId;
        }

        readonly PresentationTransforms _transforms = new();
        PresentationResources _resources;

        internal PresentationResources BeginPresentationResources()
        {
            _resources?.Dispose();
            return _resources = new PresentationResources();
        }

        internal PresentationTransforms.Lease CapturePresentationTransform(Transform target, PresentationResources resources = null)
        {
            resources ??= _resources;
            var lease = _transforms.Capture(target, resources);
            resources?.OnRelease(() => lease?.Dispose());
            return lease;
        }

        readonly PresentationParticlePool _particles = new();

        enum Settlement { Normal, Forced, Abandoned }

        internal PresentationExecution BeginPresentation(uint sequence, CombatPresentationContext context)
        {
            if (_execution != null && !_execution.Settled)
                CompletePresentationSequence(_execution, Settlement.Abandoned);
            var resources = BeginPresentationResources();
            PresentationExecution execution = null;
            execution = new PresentationExecution(sequence, context, resources,
                () => ReferenceEquals(_execution, execution) && (context == null || context.IsCurrent));
            _execution = execution;
            return execution;
        }

        Coroutine RunPresentation(IEnumerator routine, PresentationExecution execution)
            => StartCoroutine(RunAndSettle(routine, execution));

        IEnumerator RunAndSettle(IEnumerator routine, PresentationExecution execution)
        {
            try { yield return execution.Guard(routine); }
            finally { CompletePresentationSequence(execution); }
        }

        internal void ShowTemperature(int seat, float value)
        {
            _displayTemperatures[seat] = value;
            OnPlayerTempOverride?.Invoke(seat, value);
        }

        void ShowDuelTemperatures(float first, float second)
        {
            _displayTemperatures[0] = first;
            _displayTemperatures[1] = second;
            OnTempTargetsOverride?.Invoke(first, second);
        }

        void ClearDisplayTemperatures()
        {
            _displayTemperatures.Clear();
            OnTempOverridesClear?.Invoke();
        }

        Coroutine PlayDuelItem(PresentationExecution execution, int actor, short itemId,
            byte flags, short defense, CombatResultData result)
        {
            var context = new ItemPresentationContext(execution, actor, 1 - actor, itemId);
            var family = new ItemPresentationSequence(new ItemPresentationServices(this, context));
            return RunChoreography(family.PlayDuel(actor, itemId, context.Match.Network, flags, defense, result),
                execution, () => context.IsCurrent);
        }

        Coroutine PlayMultiItem(PresentationExecution execution, int actor, short itemId,
            List<CombatEventNetData> events)
        {
            var target = events.Count > 0 ? events[0].TargetSeat : actor;
            var context = new ItemPresentationContext(execution, actor, target, itemId);
            var family = new ItemPresentationSequence(new ItemPresentationServices(this, context));
            return RunChoreography(family.PlayMulti(actor, itemId, context.Match.Network, events.AsReadOnly()),
                execution, () => context.IsCurrent);
        }

        internal Coroutine RunChoreography(IEnumerator routine, PresentationExecution execution,
            System.Func<bool> current = null)
            => StartCoroutine(execution.Guard(routine, current));

        void CompletePresentationSequence(PresentationExecution execution, Settlement reason = Settlement.Normal)
        {
            if (execution == null || !execution.TrySettle()) return;
            if (!ReferenceEquals(_execution, execution)) return;
            bool current = execution.Context == null || execution.Context.IsCurrent;
            if (reason != Settlement.Normal)
            {
                StopAllCoroutines();
                ReturnActiveParticles();
                _multiPresentationQueueRoutine = null;
                if (current && execution.Context != null)
                {
                    var fps = execution.Context.Fps;
                    if (fps != null) fps.ReturnToIdle();
                    foreach (var visual in execution.Context.CurrentVisuals)
                        if (visual.IsGhostTransitionPending || visual.IsDead) visual.SettleDeathPresentation();
                        else visual.ReturnToIdle();
                }
            }
            IsPlaying = false;
            if (!current) return;
            try { ClearDisplayTemperatures(); }
            catch (System.Exception e) { Debug.LogException(e); }
            try { OnAttackerChanged?.Invoke(-1); }
            catch (System.Exception e) { Debug.LogException(e); }
            if (reason == Settlement.Abandoned) return;
            Debug.Log($"[CombatVFX] Presentation complete: seq={execution.Sequence}");
            try { SendLocalPresentationAck(execution.Sequence, execution.Context); }
            catch (System.Exception e) { Debug.LogException(e); }
            try { OnPresentationSettled?.Invoke(execution.Sequence); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        static readonly WaitForSeconds _waitIntro = new(0.5f);
        static readonly WaitForSeconds _waitBriefPause = new(0.3f);

        internal const float MinimumActionDuration = 3f;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        void OnEnable()
        {
            if (Instance != this) return;
            TurnManager.OnCombatResult += OnCombatResult;
            TurnManager.OnMultiCombatResult += OnMultiCombatResult;
            TurnManager.OnMultiDeathPresentation += OnMultiDeathPresentation;
        }

        void OnDisable()
        {
            TurnManager.OnCombatResult -= OnCombatResult;
            TurnManager.OnMultiCombatResult -= OnMultiCombatResult;
            TurnManager.OnMultiDeathPresentation -= OnMultiDeathPresentation;
            CompletePresentationSequence(_execution, Settlement.Abandoned);
            _resources?.Dispose();
            StopAllCoroutines();
            ReturnActiveParticles();
            _pendingMultiPresentations.Clear();
            _multiPresentationQueueRoutine = null;
            ClearDisplayTemperatures();
        }

        void Update()
        {
            if (_execution != null && !_execution.Settled && !_execution.CanContinue)
            {
                CompletePresentationSequence(_execution, Settlement.Abandoned);
                _pendingMultiPresentations.Clear();
            }
        }

        void OnDestroy()
        {
            OnDisable();
            _particles.Dispose();
            if (Instance == this) Instance = null;
        }

        void OnCombatResult(CombatResultData result)
        {
            Debug.Log($"[CombatVFX] OnCombatResult received — winner={result.WinnerIndex}, firstIdx={result.FirstPlayerIndex}, P1Main={result.P1MainItemId}, P2Main={result.P2MainItemId}");
            var context = new CombatPresentationContext(gameObject);
            if (!context.IsCurrent) return;
            if (_execution != null && !_execution.Settled && _execution.Sequence == result.ResultSequence) return;
            if (result.ResultSequence <= _lastCompletedDuelSequence)
            { SendPresentationAck(result.ResultSequence, context); return; }
            var execution = BeginPresentation(result.ResultSequence, context);
            RunPresentation(PlayCombatVFXSequence(result, execution), execution);
        }

        void OnMultiCombatResult(CombatResolutionBatchNetData batch)
        {
            Debug.Log($"[CombatVFX] OnMultiCombatResult received — seats={batch.SeatCount}, events={batch.EventCount}, seq={batch.ResultSequence}");
            EnqueueMultiPresentation(new PendingMultiPresentation
            {
                IsCombat = true,
                Batch = CombatPresentationContext.CopyBatch(batch),
                Sequence = batch.ResultSequence
            });
        }

        void OnMultiDeathPresentation(byte deathMask, bool endsRound,
            uint presentationId, uint ghostCastId)
        {
            Debug.Log($"[CombatVFX] OnMultiDeathPresentation — deathMask={deathMask:X2}, endsRound={endsRound}, seq={presentationId}");
            EnqueueMultiPresentation(new PendingMultiPresentation
            {
                DeathMask = deathMask,
                EndsRound = endsRound,
                Sequence = presentationId,
                GhostCastId = ghostCastId
            });
        }

        void EnqueueMultiPresentation(PendingMultiPresentation pending)
        {
            pending.Context ??= new CombatPresentationContext(gameObject);
            if (!pending.Context.IsCurrent) return;
            if (_execution != null && !_execution.Settled && pending.Sequence <= _execution.Sequence) return;
            if (pending.Sequence <= _lastCompletedMultiSequence)
            { SendPresentationAck(pending.Sequence, pending.Context); return; }
            if (_pendingMultiPresentations.Count >= 8)
            {
                Debug.LogError($"[CombatVFX] Presentation queue overflow at seq={pending.Sequence}; server timeout reconciliation required");
                return;
            }
            foreach (var queued in _pendingMultiPresentations)
                if (queued.Sequence == pending.Sequence) return;
            _pendingMultiPresentations.Enqueue(pending);
            if (_multiPresentationQueueRoutine == null)
                _multiPresentationQueueRoutine = StartCoroutine(DrainMultiPresentationQueue());
        }

        IEnumerator DrainMultiPresentationQueue()
        {
            while (_pendingMultiPresentations.Count > 0)
            {
                var pending = _pendingMultiPresentations.Dequeue();
                if (pending.Context == null || !pending.Context.IsCurrent) continue;
                var execution = BeginPresentation(pending.Sequence, pending.Context);
                if (pending.IsCombat)
                    yield return RunPresentation(PlayMultiCombatVFXSequence(pending.Batch, execution), execution);
                else
                {
                    if (pending.GhostCastId != 0)
                    {
                        float deadline = Time.unscaledTime + 2f;
                        while (execution.CanContinue && !_ghostImpactIds.Contains(pending.GhostCastId)
                            && Time.unscaledTime < deadline)
                            yield return null;
                        _ghostImpactIds.Remove(pending.GhostCastId);
                    }
                    if (execution.CanContinue)
                        yield return RunPresentation(PlayMultiDeathSequence(
                            pending.DeathMask, pending.EndsRound, execution), execution);
                    else CompletePresentationSequence(execution, Settlement.Abandoned);
                }
                _lastCompletedMultiSequence = pending.Sequence;
            }
            _multiPresentationQueueRoutine = null;
        }

        static bool SendPresentationAck(uint sequence, CombatPresentationContext context)
        {
            if (context == null || !context.IsCurrent) return false;
            context.Human.State.PresentationAckServerRpc(sequence);
            return true;
        }

        void SendLocalPresentationAck(uint sequence, CombatPresentationContext context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugAckFaultArmed)
            {
                _debugAckFaultArmed = false;
                if (_debugDropAck)
                {
                    Debug.Log($"[CombatVFX] Development ACK dropped: seq={sequence}");
                    return;
                }
                Debug.Log($"[CombatVFX] Development ACK delayed: seq={sequence}, seconds={_debugAckDelaySeconds:F1}");
                StartCoroutine(SendDelayedPresentationAck(sequence, _debugAckDelaySeconds, context));
                return;
            }
#endif
            SendPresentationAck(sequence, context);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        IEnumerator SendDelayedPresentationAck(uint sequence, float delaySeconds, CombatPresentationContext context)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            if (SendPresentationAck(sequence, context))
                Debug.Log($"[CombatVFX] Development delayed ACK sent: seq={sequence}");
            else
                Debug.Log($"[CombatVFX] Development stale ACK discarded: seq={sequence}");
        }
#endif

        IEnumerator PlayMultiDeathSequence(byte deathMask, bool endsRound, PresentationExecution execution)
        {
            var nm = execution.Context.Network;
            if (nm == null)
            {
                CompletePresentationSequence(execution);
                yield break;
            }

            var runningAnims = new List<Coroutine>();
            for (byte seat = 0; seat < 8; seat++)
            {
                if ((deathMask & (1 << seat)) == 0) continue;
                var visual = execution.Context.GetVisual(seat);
                if (visual != null)
                    runningAnims.Add(visual.PlayDeathSequenceAndWait(endsRound));
            }

            foreach (var anim in runningAnims)
                yield return anim;

            CompletePresentationSequence(execution);
        }

        IEnumerator PlayCombatVFXSequence(CombatResultData result, PresentationExecution execution)
        {
            IsPlaying = true;

            var resources = execution.Resources;
            var inventory = execution.Context.Inventory;
            if (inventory != null)
                resources.HoldInventory(inventory.LockRebuild,
                    () => { if (inventory != null) inventory.UnlockRebuild(); });

            try { ShowDuelTemperatures(result.P1TempBeforeCombat, result.P2TempBeforeCombat); }
            catch (System.Exception e) { Debug.LogException(e); }

            try
            {
                var nm = execution.Context.Network;
                if (nm == null) yield break;

                int firstIdx = result.FirstPlayerIndex;
                int secondIdx = 1 - firstIdx;

                short firstItemId = firstIdx == 0 ? result.P1MainItemId : result.P2MainItemId;
                short secondItemId = secondIdx == 0 ? result.P1MainItemId : result.P2MainItemId;

                // Defense is presented by the incoming attack, never as a separate turn.
                if (execution.Context.Items?.GetItemData(firstItemId)?.Category == ItemCategory.Defense)
                    firstItemId = -1;
                if (execution.Context.Items?.GetItemData(secondItemId)?.Category == ItemCategory.Defense)
                    secondItemId = -1;

                int deadIdx = result.WinnerIndex >= 0 ? 1 - result.WinnerIndex : -1;
                bool firstActionKilled = result.WinnerIndex >= 0
                    && result.EventCount == 1
                    && result.Event0Source == (byte)firstIdx;

                LogAttackTimingSummary(firstIdx, firstItemId, secondIdx, secondItemId, deadIdx);

                float seqStart = Time.time;
                yield return _waitIntro;

                Debug.Log($"[CombatVFX] Sequence seq={execution.Sequence}: first=P{firstIdx}(item={firstItemId}), second=P{secondIdx}(item={secondItemId}), deadIdx={deadIdx}");

                if (firstItemId >= 0)
                {
                    GetImpactData(result, firstIdx, out byte impactFlags, out short defenseItemId);
                    Debug.Log($"[CombatVFX] Playing FIRST item sequence: P{firstIdx} item={firstItemId}, flags={impactFlags}");
                    OnAttackerChanged?.Invoke(firstIdx);
                    yield return PlayDuelItem(execution, firstIdx, firstItemId, impactFlags, defenseItemId, result);
                }

                if (firstActionKilled && deadIdx >= 0)
                {
                    Debug.Log($"[CombatVFX] First action killed P{deadIdx} — playing death sequence");
                    var deadVisual = execution.Context.GetVisual(deadIdx);
                    if (deadVisual != null)
                        yield return deadVisual.PlayDeathSequenceAndWait(result.EndsMatch);
                    yield break;
                }

                if (firstItemId >= 0 && secondItemId >= 0)
                    yield return _waitBriefPause;

                if (secondItemId >= 0)
                {
                    GetImpactData(result, secondIdx, out byte impactFlags, out short defenseItemId);
                    Debug.Log($"[CombatVFX] Playing SECOND item sequence: P{secondIdx} item={secondItemId}, flags={impactFlags}");
                    OnAttackerChanged?.Invoke(secondIdx);
                    yield return PlayDuelItem(execution, secondIdx, secondItemId, impactFlags, defenseItemId, result);
                }

                if (!firstActionKilled && deadIdx >= 0)
                {
                    Debug.Log($"[CombatVFX] Second action killed P{deadIdx} — playing death sequence");
                    var deadVisual = execution.Context.GetVisual(deadIdx);
                    if (deadVisual != null)
                        yield return deadVisual.PlayDeathSequenceAndWait(result.EndsMatch);
                }
                else
                {
                    float elapsed = Time.time - seqStart;
                    float pad = MinimumActionDuration - elapsed;
                    if (pad > 0f)
                    {
                        Debug.Log($"[CombatVFX] Padding {pad:F2}s to meet {MinimumActionDuration}s minimum");
                        yield return new WaitForSeconds(pad);
                    }
                }
            }
            finally
            {
                CompletePresentationSequence(execution);
                if (execution.Context.IsCurrent) _lastCompletedDuelSequence = result.ResultSequence;
            }
        }

        IEnumerator PlayMultiCombatVFXSequence(CombatResolutionBatchNetData batch, PresentationExecution execution)
        {
            IsPlaying = true;

            var resources = execution.Resources;
            var inventory = execution.Context.Inventory;
            if (inventory != null)
                resources.HoldInventory(inventory.LockRebuild,
                    () => { if (inventory != null) inventory.UnlockRebuild(); });

            try
            {
                for (int s = 0; s < batch.SeatCount; s++)
                    ShowTemperature(s, batch.TempBefore[s]);

                var nm = execution.Context.Network;
                if (nm == null) yield break;

                var deathShown = new HashSet<byte>();

                var schedule = MultiPresentationSchedule.Build(batch,
                    id => execution.Context.Items?.GetItemData(id)?.AnimDuration ?? 0f,
                    id => execution.Context.Items?.GetItemData(id)?.Category == ItemCategory.Defense);

                yield return _waitIntro;

                for (int e = 0; e < batch.EventCount; e++)
                {
                    var evt = batch.Events[e];
                    if ((CombatEventType)evt.EventType != CombatEventType.SuppressedItemUse)
                        continue;
                    var item = execution.Context.Items?.GetItemData(evt.ItemId);
                    if (item != null && item.Category == ItemCategory.Defense)
                        OnSuppressedItemCue?.Invoke(evt.ActorSeat);
                }

                for (int orderIdx = 0; orderIdx < batch.SeatCount; orderIdx++)
                {
                    int actorSeat = batch.ActionOrder[orderIdx];
                    short mainItemId = batch.MainItemIds[actorSeat];

                    var mainEffects = new List<CombatEventNetData>();
                    var deathEvents = new List<CombatEventNetData>();
                    for (int e = 0; e < batch.EventCount; e++)
                    {
                        var evt = batch.Events[e];
                        if (evt.ActorSeat != actorSeat) continue;
                        if ((CombatEventType)evt.EventType == CombatEventType.Death)
                            deathEvents.Add(evt);
                        else if ((CombatEventType)evt.EventType == CombatEventType.MainEffect
                            || (CombatEventType)evt.EventType == CombatEventType.DefenseActivated
                            || ((CombatEventType)evt.EventType == CombatEventType.SuppressedItemUse
                                && execution.Context.Items?.GetItemData(evt.ItemId)?.Category != ItemCategory.Defense))
                            mainEffects.Add(evt);
                    }

                    bool actionExecuted = schedule.HasAction(actorSeat);
                    if (!actionExecuted) continue;

                    OnAttackerChanged?.Invoke(actorSeat);
                    Debug.Log($"[CombatVFX-Multi] Action {orderIdx}: P{actorSeat} item={mainItemId} effects={mainEffects.Count} deaths={deathEvents.Count}");

                    float actionStart = Time.time;
                    bool suppressed = mainEffects.Exists(evt =>
                        (CombatEventType)evt.EventType == CombatEventType.SuppressedItemUse);
                    if (suppressed)
                    {
                        OnSuppressedItemCue?.Invoke((byte)actorSeat);
                    }
                    else
                        yield return PlayMultiItem(execution, actorSeat, mainItemId, mainEffects);

                    float actionPad = schedule.ActionDurationSeconds(actorSeat) - (Time.time - actionStart);
                    if (actionPad > 0f)
                        yield return new WaitForSeconds(actionPad);

                    foreach (var dEvt in deathEvents)
                    {
                        byte tgt = dEvt.TargetSeat;
                        if (deathShown.Contains(tgt)) continue;
                        var deadVisual = execution.Context.GetVisual(tgt);
                        if (deadVisual == null) continue;
                        deathShown.Add(tgt);
                        bool endsMatch = batch.MatchWinnerMask != 0;
                        yield return deadVisual.PlayDeathSequenceAndWait(endsMatch);
                    }

                    bool anyDeath = deathEvents.Count > 0;
                    bool laterAction = false;
                    for (int next = orderIdx + 1; next < batch.SeatCount; next++)
                        if (schedule.HasAction(batch.ActionOrder[next]))
                        { laterAction = true; break; }
                    if (laterAction && !anyDeath)
                        yield return _waitBriefPause;
                }

            }
            finally
            {
                for (int s = 0; execution.CanContinue && s < batch.SeatCount; s++)
                {
                    try { ShowTemperature(s, batch.TempAfter[s]); }
                    catch (System.Exception e) { Debug.LogException(e); }
                }

                CompletePresentationSequence(execution);
            }
        }

        public static bool IsFullyBlockedImpact(byte flags)
            => (flags & CombatImpactFlags.Defense) != 0
                && (flags & (CombatImpactFlags.Damage | CombatImpactFlags.Recovery)) == 0;

        static void GetImpactData(CombatResultData result, int actorSeat,
            out byte flags, out short defenseItemId)
        {
            flags = 0;
            defenseItemId = -1;
            if (result.EventCount > 0 && result.Event0Source == actorSeat)
            {
                flags = result.Event0ImpactFlags;
                defenseItemId = result.Event0DefenseItemId;
            }
            else if (result.EventCount > 1 && result.Event1Source == actorSeat)
            {
                flags = result.Event1ImpactFlags;
                defenseItemId = result.Event1DefenseItemId;
            }
        }

        void LogAttackTimingSummary(int firstIdx, short firstItemId, int secondIdx, short secondItemId, int deadIdx)
        {
            Debug.Log("╔══════════════════════════════════════════════════════════════");
            Debug.Log("║ ATTACK PHASE — TIMING SUMMARY");
            Debug.Log("╠══════════════════════════════════════════════════════════════");

            LogItemTiming("FIRST", firstIdx, firstItemId);
            LogItemTiming("SECOND", secondIdx, secondItemId);

            if (deadIdx >= 0)
                Debug.Log($"║ DEATH: P{deadIdx} → freeze(0.33s) + hold(1.5s) + break = ~1.83s");

            Debug.Log("╚══════════════════════════════════════════════════════════════");
        }

        void LogItemTiming(string order, int playerIdx, short itemId)
        {
            if (itemId < 0)
            {
                Debug.Log($"║ {order}: P{playerIdx} — NO ITEM");
                return;
            }

            var itemData = ItemManager.Instance?.GetItemData(itemId);
            if (itemData == null)
            {
                Debug.Log($"║ {order}: P{playerIdx} — itemId={itemId} DATA NOT FOUND");
                return;
            }

            float animDur = itemData.AnimDuration > 0f ? itemData.AnimDuration : 0.8f;
            float totalHitTime = itemData.EffectDelay + (itemData.EffectHitCount - 1) * itemData.EffectInterval;
            float oppDur = !string.IsNullOrEmpty(itemData.OpponentAnimTrigger)
                ? (itemData.AnimDuration > 0f ? itemData.AnimDuration : 0.8f)
                : 0f;

            Debug.Log($"║ {order}: P{playerIdx} '{itemData.ItemName}' (id={itemId})");
            Debug.Log($"║   AnimTrigger='{itemData.AnimTrigger}' | OppTrigger='{itemData.OpponentAnimTrigger}'");
            Debug.Log($"║   AnimDuration={animDur:F2}s | EffectDelay={itemData.EffectDelay:F2}s");
            Debug.Log($"║   HitCount={itemData.EffectHitCount} | HitInterval={itemData.EffectInterval:F2}s | TotalHitTime={totalHitTime:F2}s");
            Debug.Log($"║   OppAnimDuration={oppDur:F2}s | EstTotal={animDur + oppDur:F2}s");
        }

        public void PlayHitAt(Vector3 pos)
        {
            Debug.Log($"[CombatVFX] PlayHitAt({pos}) — prefab={(_hitEffectPrefab != null)}");
            SpawnParticle(_hitEffectPrefab, pos);
        }

        public void PlayIceBreakAt(Vector3 pos)
        {
            Debug.Log($"[CombatVFX] PlayIceBreakAt({pos}) — prefab={(_iceBreakEffectPrefab != null)}");
            SpawnParticle(_iceBreakEffectPrefab, pos);
        }

        public void PlayFinalBreakAt(Vector3 pos)
        {
            Debug.Log($"[CombatVFX] PlayFinalBreakAt({pos}) — prefab={(_finalBreakEffectPrefab != null)}");
            SpawnParticle(_finalBreakEffectPrefab, pos);
        }

        static readonly WaitForSeconds _waitParticleLife = new(3f);

        void SpawnParticle(GameObject prefab, Vector3 pos)
        {
            var lease = _particles.Spawn(prefab, pos);
            if (lease != null) StartCoroutine(ReturnToPoolAfterDelay(lease));
        }

        static void ConfigureParticleRenderers(GameObject go)
            => PresentationParticlePool.ConfigureParticleRenderers(go);

        void ReturnActiveParticles() => _particles.ReleaseAll();

        IEnumerator ReturnToPoolAfterDelay(PresentationParticlePool.Lease lease)
        {
            yield return _waitParticleLife;
            _particles.Release(lease);
        }
    }
}
