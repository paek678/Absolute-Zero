using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.Core.Turn
{
    internal readonly struct MultiAttackStep
    {
        internal readonly bool Committed;
        internal readonly bool Failed;
        internal readonly int ActorSeat;
        internal readonly short ItemId;
        internal readonly byte WinnerMask;
        internal MultiAttackStep(bool committed, bool failed, int actor, short item, byte winners = 0)
        { Committed = committed; Failed = failed; ActorSeat = actor; ItemId = item; WinnerMask = winners; }
    }

    // Owns only one synchronous combat calculation/application sequence.
    // Caller retains phase, top-up, winner latch, result sequence and presentation barrier.
    internal sealed class MultiAttackOperation
    {
        readonly MultiCombatCapture _capture;
        readonly ItemEffectRuleSnapshot[] _rules;
        readonly MatchCombatSnapshot _initial;
        readonly CombatResolver _resolver;
        readonly AuthoritativeDeathService _deaths;
        readonly MultiCombatApplicator _applicator;
        readonly ActionIntent[] _intents;
        readonly int[] _order;
        readonly MultiCombatResolution _aggregate;
        int _next;
        internal bool HasNext => _next < _order.Length;

        internal MultiAttackOperation(MultiCombatCapture capture, ItemEffectRuleSnapshot[] rules,
            MatchCombatSnapshot initial, CombatResolver resolver, AuthoritativeDeathService deaths,
            BuffDebuffSystem buffs, ItemDropTable dropTable)
        {
            _capture = capture; _rules = rules; _initial = initial; _resolver = resolver; _deaths = deaths;
            _intents = capture.BuildActionIntents();
            _order = resolver.BuildActionOrder(initial, _intents, initial.SeatCount);
            _aggregate = MultiCombatResolution.Create(initial.SeatCount);
            System.Array.Copy(_order, _aggregate.ActionOrder, _order.Length);
            var inventory = new SeatInventoryMutator(capture.Players, capture.Roster, dropTable, initial.SeatCount);
            _applicator = new MultiCombatApplicator(capture.Roster, deaths, buffs, inventory);
        }

        internal bool TryApplyDefenses()
        {
            var defense = _resolver.ResolveMultiDefenses(_initial, _intents);
            if (!_applicator.Apply(defense, _initial)) return false;
            for (int i = 0; i < defense.EventCount; i++)
            {
                var evt = defense.OrderedEvents[i];
                _aggregate.AddEvent(evt);
                _aggregate.MainItemIds[evt.SourcePlayer] = evt.ItemId;
            }
            return true;
        }

        internal MultiAttackStep ApplyNext()
        {
            int actor = _order[_next++];
            var original = _intents[actor];
            if (original.IsEmpty || _capture.Items.GetItemData(original.ItemId) is DefenseItemDataSO)
                return default;
            var intent = _capture.BuildActionIntentForSeat(actor);
            if (intent.IsEmpty) return default;
            var snapshot = _capture.BuildSnapshot(_rules);
            var resolution = _resolver.ResolveMultiAction(snapshot, intent);
            if (resolution.EventCount == 0) return default;
            int[] before = _capture.CaptureKillScores();
            if (!_applicator.Apply(resolution, snapshot))
                return new MultiAttackStep(false, true, actor, intent.ItemId);
            AppendCommittedResolution(_aggregate, resolution, actor);
            _deaths.ConsumeDeathMask();
            byte winners = _capture.Rule == null ? (byte)0 : MultiVictoryRules.FindThresholdCrossings(
                before, _capture.CaptureKillScores(), _capture.Rule.KillsToWin);
            return new MultiAttackStep(true, false, actor, intent.ItemId, winners);
        }

        internal MultiCombatResolution Finish()
        {
            var final = _capture.BuildSnapshot(_rules);
            for (int i = 0; i < _aggregate.TemperatureDeltas.Length; i++)
                _aggregate.TemperatureDeltas[i] = final.CurrentTemperatures[i] - _initial.CurrentTemperatures[i];
            return _aggregate;
        }

        static void AppendCommittedResolution(MultiCombatResolution aggregate,
            MultiCombatResolution action, int actorSeat)
        {
            aggregate.MainItemIds[actorSeat] = action.MainItemIds[actorSeat];
            aggregate.DeadMask |= action.DeadMask;
            for (int i = 0; i < action.EventCount; i++)
                aggregate.AddEvent(action.OrderedEvents[i]);
        }

    }
}
