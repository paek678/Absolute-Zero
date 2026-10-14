using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    // Score-sheet category. A mortal-danger turn keeps Recovery and Defense only.
    internal enum BotItemCategory : byte { None, Attack, Defense, Recovery, Disrupt, NextTurn }
    internal enum BotItemFormula : byte
    { None, DirectAttack, Equalize, Guard, Heal, DelayedHeal, DelayedDamage, Reroll, Steal, BlockBasics, Neutralize, FanBoost }
    // Windbreaker guards tool (Temperature filter) hits; Mask guards food.
    internal enum BotAttackKind : byte { None, Tool, Food }

    // One catalog item as the bot judges it, derived once per launch from the item asset
    // plus the bot's knowledge entry. Unknown items are never chosen by the bot.
    internal readonly struct BotItemProfile
    {
        public readonly short Item;
        public readonly bool Known;
        public readonly BotItemFormula Formula;
        public readonly BotItemCategory Category;
        public readonly BotAttackKind Kind;
        public readonly float DefenseAmount;
        public readonly DamageFilter Filter;
        public readonly int ScheduledDelay;
        public readonly float Amount;   // damage, scheduled amount or boosted fan speed
        public readonly float Offset;   // own immediate cost (Soda) or target immediate heal (Samgyetang)
        public readonly float Success;  // 1 unless the human path plays a mini-game
        public readonly float OpponentValue;
        public readonly bool Finite, IsRandomItem;
        public readonly int Order;
        internal BotItemProfile(short item, bool known, BotItemFormula formula, BotItemCategory category,
            BotAttackKind kind, float amount, float offset, float success, float opponentValue,
            bool finite, bool isRandomItem, int order, float defenseAmount = float.PositiveInfinity, DamageFilter? filter = null, int scheduledDelay = 1)
        {
            Item = item; Known = known; Formula = formula; Category = category; Kind = kind; Amount = amount;
            Offset = offset; Success = success; OpponentValue = opponentValue; Finite = finite;
            IsRandomItem = isRandomItem; Order = order; DefenseAmount = defenseAmount;
            Filter = filter ?? (kind == BotAttackKind.Tool ? DamageFilter.Temperature : kind == BotAttackKind.Food ? DamageFilter.Food : DamageFilter.All);
            ScheduledDelay = scheduledDelay;
        }
    }

    internal readonly struct BotOpponentItem
    {
        public readonly short Item;
        public readonly bool Usable;
        public BotOpponentItem(short item, bool usable) { Item = item; Usable = usable; }
    }

    // Only explicitly public, already scheduled temperature effects may enter this snapshot.
    // The live game does not currently expose a public due-effect feed; live observations leave it empty.
    internal readonly struct BotPublicDueEffect
    {
        public readonly bool Own;
        public readonly double DueSeconds;
        public readonly float Delta;
        public BotPublicDueEffect(bool own, double dueSeconds, float delta)
        {
            if (double.IsNaN(dueSeconds) || double.IsInfinity(dueSeconds) || dueSeconds < 0 || float.IsNaN(delta) || float.IsInfinity(delta))
                throw new ArgumentOutOfRangeException(nameof(dueSeconds));
            Own = own; DueSeconds = dueSeconds; Delta = delta;
        }
    }

    internal readonly struct BotCandidate
    {
        public readonly uint Copy;
        public readonly byte Target;
        public readonly short Item;
        public readonly double Delay;
        public readonly float Heal;   // this copy's current-use heal (Smartphone 4 -> 6 -> 7)
        public BotCandidate(uint copy, byte target, short item, double delay, float heal = 0)
        { Copy = copy; Target = target; Item = item; Delay = delay; Heal = heal; }
    }

    // Immutable own and public data only. Held opponent item identities are public (Q15);
    // the opponent's selection, queue and use counts never enter the model (Q22).
    internal sealed class BotObservation
    {
        public readonly PrepInputKey Key;
        public readonly byte Seat, Opponent;
        public readonly float Temperature, OpponentTemperature, FanSpeed, OpponentFanSpeed;
        public readonly bool OpponentReady, OwnFanActive, OpponentFanActive, CanReceiveStolenCopy;
        public readonly double Remaining;
        public readonly float OpponentPrepLoss;
        public readonly IReadOnlyList<BotOpponentItem> OpponentItems;
        public readonly IReadOnlyList<BotCandidate> Candidates;
        public readonly IReadOnlyList<BotPublicDueEffect> PublicDueEffects;
        public BotObservation(PrepInputKey key, byte seat, byte opponent, float temperature,
            float opponentTemperature, float fan, float opponentFan, bool opponentReady, double remaining,
            float opponentPrepLoss,
            IEnumerable<BotOpponentItem> opponentItems, IEnumerable<BotCandidate> candidates,
            bool ownFanActive = true, bool opponentFanActive = true, bool canReceiveStolenCopy = true, IEnumerable<BotPublicDueEffect> publicDueEffects = null)
        {
            Key = key; Seat = seat; Opponent = opponent; Temperature = temperature;
            OpponentTemperature = opponentTemperature; FanSpeed = fan; OpponentFanSpeed = opponentFan;
            OpponentReady = opponentReady; Remaining = remaining; OpponentPrepLoss = opponentPrepLoss;
            OwnFanActive = ownFanActive; OpponentFanActive = opponentFanActive;
            CanReceiveStolenCopy = canReceiveStolenCopy;
            var due = new List<BotPublicDueEffect>(publicDueEffects ?? Array.Empty<BotPublicDueEffect>());
            due.Sort((a, b) => a.DueSeconds.CompareTo(b.DueSeconds));
            PublicDueEffects = new ReadOnlyCollection<BotPublicDueEffect>(due);
            OpponentItems = new ReadOnlyCollection<BotOpponentItem>(new List<BotOpponentItem>(opponentItems ?? Array.Empty<BotOpponentItem>()));
            Candidates = new ReadOnlyCollection<BotCandidate>(new List<BotCandidate>(candidates ?? Array.Empty<BotCandidate>()));
        }
        public BotObservation WithCandidates(IEnumerable<BotCandidate> candidates)
            => new(Key, Seat, Opponent, Temperature, OpponentTemperature, FanSpeed, OpponentFanSpeed, OpponentReady,
                Remaining, OpponentPrepLoss, OpponentItems, candidates, OwnFanActive, OpponentFanActive, CanReceiveStolenCopy, PublicDueEffects);
    }

    // Conservative one-second tick bound, not an exact simulation of private clock phase.
    internal readonly struct BotCandidateProjection
    {
        public readonly double Wait;
        public readonly float OwnLoss, OwnTemperature, OpponentTemperature, Threat;
        public readonly bool Affordable, Crisis;
        public BotCandidateProjection(double wait, float loss, float own, float other, float threat, bool affordable)
        { Wait = wait; OwnLoss = loss; OwnTemperature = own; OpponentTemperature = other; Threat = threat;
          Affordable = affordable; Crisis = own - threat <= 0; }
    }

    internal readonly struct BotScoredItem
    {
        public readonly BotCandidate Candidate;
        public readonly BotCandidateProjection Projection;
        public readonly float Immediate, Future, ImmediateRescue;
        public readonly BotItemCategory Category;
        public readonly float Score, Final, Success;
        public readonly bool Lethal;
        public readonly int Order;
        public BotScoredItem(BotCandidate candidate, BotItemCategory category, float score, float final,
            float success, bool lethal, int order, BotCandidateProjection projection = default, float immediate = 0, float future = 0, float immediateRescue = 0)
        { Candidate = candidate; Category = category; Score = score; Final = final; Success = success; Lethal = lethal; Order = order; Projection = projection; Immediate = immediate; Future = future; ImmediateRescue = immediateRescue; }
    }

    internal sealed class BotScoreSheet
    {
        public readonly bool Crisis;
        public readonly float Care, OwnLoss, Threat;
        // Positive final scores only, best first; ties keep the knowledge table order.
        public readonly IReadOnlyList<BotScoredItem> Ranked;
        public BotScoreSheet(bool crisis, float care, float ownLoss, float threat, IList<BotScoredItem> ranked)
        {
            Crisis = crisis; Care = care; OwnLoss = ownLoss; Threat = threat;
            Ranked = new ReadOnlyCollection<BotScoredItem>(ranked);
        }
        public bool HasLethal
        {
            get { foreach (var item in Ranked) if (item.Lethal) return true; return false; }
        }
    }

    // Pure scoring for the approved bot design (PLAN046). No PlayerState, NetworkVariable,
    // scene search or mutable SO enters this class; RNG streams are owned by the caller.
    internal static class BotDecisionModel
    {
        internal static BotItemProfile Profile(ItemDataSO item, short id, BotItemKnowledgeEntry? knowledge,
            int order, BotJudgementSettings judgement)
        {
            var rule = ItemEffectRuleSnapshot.From(item, id);
            var formula = BotItemFormula.None;
            var kind = BotAttackKind.None;
            float amount = 0, offset = 0;
            switch (rule.EffectKind)
            {
                case ItemEffectKind.DirectDamage:
                    formula = BotItemFormula.DirectAttack; amount = Mathf.Max(0, rule.BaseDamage); kind = Kind(rule.AttackFilter); break;
                case ItemEffectKind.Equalize:
                    formula = BotItemFormula.Equalize; kind = Kind(rule.AttackFilter); break;
                case ItemEffectKind.Defense:
                    formula = BotItemFormula.Guard; kind = Kind(rule.AttackFilter); break;
                case ItemEffectKind.Recovery:
                    formula = BotItemFormula.Heal; break;
                case ItemEffectKind.Buff when ScheduledTemperature(rule) && rule.ScheduledValue > 0:
                    formula = BotItemFormula.DelayedHeal; amount = rule.ScheduledValue;
                    offset = -rule.ImmediateTempDelta; break;
                case ItemEffectKind.Debuff when ScheduledTemperature(rule) && rule.ScheduledValue < 0:
                    formula = BotItemFormula.DelayedDamage; amount = -rule.ScheduledValue;
                    offset = rule.ImmediateTempDelta; kind = Kind(rule.AttackFilter); break;
                case ItemEffectKind.Sabotage:
                    formula = rule.InventoryAction == InventoryMutationType.RerollTarget ? BotItemFormula.Reroll
                        : rule.InventoryAction == InventoryMutationType.StealFromTarget ? BotItemFormula.Steal
                        : rule.BlocksTargetBasics ? BotItemFormula.BlockBasics
                        : rule.NeutralizesTarget ? BotItemFormula.Neutralize : BotItemFormula.None;
                    break;
                case ItemEffectKind.FanControl when rule.WritesTargetFanSpeed:
                    formula = BotItemFormula.FanBoost; amount = rule.TargetFanSpeedValue; break;
            }
            bool known = knowledge.HasValue;
            float success = known && item.RequiresMiniGame ? Mathf.Clamp01(judgement.SuccessFor(knowledge.Value.MiniGameTier)) : 1;
            return new BotItemProfile(id, known, formula, CategoryOf(formula), kind, amount, offset, success,
                known ? Mathf.Max(0, knowledge.Value.OpponentValue) : 0, item.MaxUses > 0,
                item.SlotType == ItemSlotType.Sub, known ? order : int.MaxValue, rule.DefenseReduction, rule.AttackFilter, rule.ScheduledDelay);
        }

        internal static float CurrentHeal(ItemEffectRuleSnapshot rule, ItemSlotNetData slot)
        {
            if (rule.HealPerUse == null || rule.HealPerUse.Length == 0) return 0;
            return Mathf.Max(0, rule.HealPerUse[Mathf.Clamp(rule.MaxUses - slot.RemainingUses, 0, rule.HealPerUse.Length - 1)]);
        }

        internal static BotScoreSheet Score(BotObservation o, IReadOnlyList<BotItemProfile> profiles,
            BotJudgementSettings j, BotScoringCoefficients c, double readyReserve)
        {
            float randomWorth = 0, bestWorth = 0, bestStealable = 0;
            foreach (var held in o.OpponentItems)
            {
                if (!held.Usable || !TryProfile(profiles, held.Item, out var p) || !p.Known) continue;
                if (p.IsRandomItem) randomWorth += p.OpponentValue;
                bestWorth = Mathf.Max(bestWorth, p.OpponentValue);
                if (p.Finite) bestStealable = Mathf.Max(bestStealable, p.OpponentValue);
            }
            double shortest = double.PositiveInfinity;
            foreach (var candidate in o.Candidates)
                if (Affordable(o, candidate, readyReserve)) shortest = Math.Min(shortest, candidate.Delay);
            var screen = ProjectCandidate(o, new BotCandidate(0, 0, 0, double.IsPositiveInfinity(shortest) ? 0 : shortest), profiles, readyReserve);
            bool crisis = screen.Crisis;
            float care = crisis ? j.CrisisCareMultiplier
                : o.Temperature - o.OpponentTemperature >= j.ComfortMargin ? j.ComfortCareMultiplier : 1;
            const float maxTemp = TemperatureSystem.MAX_TEMP;
            var copies = new Dictionary<short, BotScoredItem>();
            foreach (var candidate in o.Candidates)
            {
                if (candidate.Copy == 0 || !TryProfile(profiles, candidate.Item, out var p) || !p.Known) continue;
                var projection = ProjectCandidate(o, candidate, profiles, readyReserve);
                if (!projection.Affordable || projection.OwnTemperature <= 0) continue;
                float own = projection.OwnTemperature, other = projection.OpponentTemperature;
                float immediate = 0, future = 0, rescue = 0, score, s = p.Success;
                float candidateCare = projection.Crisis ? j.CrisisCareMultiplier : care;
                float discount = Mathf.Pow(j.LookaheadFactor, Math.Max(1, p.ScheduledDelay));
                bool lethal = false;
                switch (p.Formula)
                {
                    case BotItemFormula.DirectAttack:
                        immediate = ExpectedDamage(p.Amount, p.Filter, o, profiles, j.OpponentBlockChance) * s;
                        score = immediate; lethal = score > 0 && score >= other; break;
                    case BotItemFormula.Equalize:
                        immediate = ExpectedDamage(Mathf.Max(0, other - own), p.Filter, o, profiles, j.OpponentBlockChance) * s;
                        score = immediate; break;
                    case BotItemFormula.Guard:
                        foreach (var held in o.OpponentItems)
                        {
                            if (!held.Usable || !TryProfile(profiles, held.Item, out var attack) || !Matches(p.Filter, attack.Filter)) continue;
                            float now = ImmediateThreat(attack, other, own), later = 0;
                            if (attack.Formula == BotItemFormula.DelayedDamage)
                            {
                                now = -Mathf.Min(attack.Offset, maxTemp - own);
                                later = attack.Amount * Mathf.Pow(j.LookaheadFactor, Math.Max(1, attack.ScheduledDelay));
                            }
                            else now = Mathf.Min(now, Mathf.Max(0, p.DefenseAmount));
                            rescue = Mathf.Max(rescue, now);
                            // One plausible opponent item, not the sum of mutually exclusive actions.
                            if (now + later > immediate + future) { immediate = now; future = later; }
                        }
                        score = (immediate + future) * j.OpponentAttackChance * candidateCare * s; break;
                    case BotItemFormula.Heal:
                        immediate = Mathf.Min(candidate.Heal, maxTemp - own); rescue = immediate;
                        score = immediate * s * candidateCare; break;
                    case BotItemFormula.DelayedHeal:
                        if (own - p.Offset - projection.Threat <= 0) continue;
                        immediate = -p.Offset;
                        future = Mathf.Min(p.Amount, maxTemp - (own - p.Offset)) * discount;
                        score = (immediate + future) * s * candidateCare; break;
                    case BotItemFormula.DelayedDamage:
                        float unblocked = HasMatchingGuard(p.Filter, o, profiles) ? 1 - j.OpponentBlockChance : 1;
                        immediate = -Mathf.Min(p.Offset, maxTemp - other) * s * unblocked;
                        future = p.Amount * discount * s * unblocked;
                        score = immediate + future; break;
                    case BotItemFormula.Reroll:
                        score = randomWorth * c.RerollValueFactor * j.LookaheadFactor * s; break;
                    case BotItemFormula.Steal:
                        if (!o.CanReceiveStolenCopy) continue;
                        score = bestStealable * c.StealValueFactor * j.LookaheadFactor * s; break;
                    case BotItemFormula.BlockBasics:
                        score = c.BlockBasicsValue * j.LookaheadFactor * s; break;
                    case BotItemFormula.Neutralize:
                        score = bestWorth * c.NeutralizeValueFactor * s; break;
                    case BotItemFormula.FanBoost:
                        if (!o.OpponentFanActive || o.OpponentFanSpeed <= 0 || o.OpponentFanSpeed >= p.Amount) continue;
                        score = o.OpponentPrepLoss * (p.Amount / o.OpponentFanSpeed - 1) * c.FanBoostTurns * j.LookaheadFactor * s; break;
                    default: continue;
                }
                float final = score + (lethal ? j.KillBonus * s : 0);
                if (!(final > 0) || float.IsInfinity(final)) continue;
                var scored = new BotScoredItem(candidate, p.Category, score, final, s, lethal, p.Order, projection, immediate, future, rescue);
                if (!copies.TryGetValue(candidate.Item, out var previous) || CompareRank(scored, previous) < 0)
                    copies[candidate.Item] = scored;
            }
            var ranked = new List<BotScoredItem>(copies.Values);
            ranked.Sort(CompareRank);
            return new BotScoreSheet(crisis, care, screen.OwnLoss, screen.Threat, ranked);
        }

        internal static BotCandidateProjection ProjectCandidate(BotObservation o, BotCandidate candidate,
            IReadOnlyList<BotItemProfile> profiles, double reserve)
        {
            double wait = o.OpponentReady ? 0 : candidate.Delay;
            float ticks = (float)Math.Ceiling(Math.Max(0, wait) / TemperatureSystem.TICK_INTERVAL);
            float loss = o.OwnFanActive ? Mathf.Max(0, o.FanSpeed) * ticks : 0;
            float own = ProjectTemperature(o.Temperature, o.OwnFanActive ? Mathf.Max(0, o.FanSpeed) : 0, wait, true, o.PublicDueEffects);
            // Once Ready, the opponent's fan is stopped by the current game's authoritative path.
            float other = ProjectTemperature(o.OpponentTemperature, o.OpponentFanActive && !o.OpponentReady ? Mathf.Max(0, o.OpponentFanSpeed) : 0, wait, false, o.PublicDueEffects), threat = 0;
            foreach (var held in o.OpponentItems)
                if (held.Usable && TryProfile(profiles, held.Item, out var profile))
                    threat = Mathf.Max(threat, ImmediateThreat(profile, other, own));
            return new BotCandidateProjection(wait, loss, own, other, threat, Affordable(o, candidate, reserve));
        }
        static float ProjectTemperature(float temperature, float speed, double wait, bool own, IReadOnlyList<BotPublicDueEffect> effects)
        {
            float previousTicks = 0;
            foreach (var effect in effects)
            {
                if (effect.Own != own || effect.DueSeconds > wait) continue;
                float ticks = (float)Math.Ceiling(effect.DueSeconds / TemperatureSystem.TICK_INTERVAL);
                temperature -= (ticks - previousTicks) * speed; previousTicks = ticks;
                if (temperature <= 0) return 0; // A later heal cannot resurrect death before it is due.
                temperature = Mathf.Clamp(temperature + effect.Delta, 0, TemperatureSystem.MAX_TEMP);
                if (temperature <= 0) return 0;
            }
            return Mathf.Max(0, temperature - ((float)Math.Ceiling(wait / TemperatureSystem.TICK_INTERVAL) - previousTicks) * speed);
        }
        internal static bool CanSubmitChoice(BotScoreSheet current)
        {
            if (current == null || current.Ranked.Count != 1) return false;
            var selected = current.Ranked[0];
            bool care = selected.ImmediateRescue > 0 && (selected.Category is BotItemCategory.Recovery or BotItemCategory.Defense);
            return selected.Projection.Affordable && selected.Projection.OwnTemperature > 0 && (!selected.Projection.Crisis || care);
        }
        internal static bool NeedsCrisisRedirect(BotScoreSheet sheet, BotCandidate chosen)
        {
            foreach (var item in sheet.Ranked)
                if (item.Candidate.Copy == chosen.Copy) return item.Projection.Crisis;
            return true;
        }
        internal static bool ChooseCrisisOrReady(BotScoreSheet sheet, BotJudgementSettings judgement, System.Random random,
            out BotCandidate chosen, out bool noItemReady)
        {
            bool found = TryPickCrisis(sheet, judgement, random, out chosen, true);
            noItemReady = !found; return true;
        }
        static float ImmediateThreat(BotItemProfile p, float actor, float target)
            => p.Formula == BotItemFormula.DirectAttack ? Mathf.Max(0, p.Amount)
             : p.Formula == BotItemFormula.Equalize ? Mathf.Max(0, target - actor)
             : p.Formula == BotItemFormula.DelayedDamage ? Mathf.Max(0, -p.Offset) : 0;
        static bool Matches(DamageFilter guard, DamageFilter attack) => guard == DamageFilter.All || guard == attack;
        static bool HasMatchingGuard(DamageFilter filter, BotObservation o, IReadOnlyList<BotItemProfile> profiles)
        {
            foreach (var held in o.OpponentItems)
                if (held.Usable && TryProfile(profiles, held.Item, out var p) && p.Formula == BotItemFormula.Guard && Matches(p.Filter, filter)) return true;
            return false;
        }
        static float ExpectedDamage(float damage, DamageFilter filter, BotObservation o, IReadOnlyList<BotItemProfile> profiles, float probability)
        {
            float blocked = 0;
            foreach (var held in o.OpponentItems)
                if (held.Usable && TryProfile(profiles, held.Item, out var p) && p.Formula == BotItemFormula.Guard && Matches(p.Filter, filter))
                    blocked = Mathf.Max(blocked, Mathf.Min(damage, Mathf.Max(0, p.DefenseAmount)));
            return Mathf.Max(0, damage - Mathf.Clamp01(probability) * blocked);
        }

        internal static bool MustTakeLethal(BotScoreSheet sheet, BotJudgementSettings j)
            => sheet != null && !sheet.Crisis && !j.MayMissLethal && sheet.HasLethal;

        internal static bool TryPickCrisis(BotScoreSheet sheet, BotJudgementSettings j, System.Random random, out BotCandidate chosen, bool forceCrisis = false)
        {
            chosen = default;
            if (sheet == null || (!sheet.Crisis && !forceCrisis)) return false;
            var care = new List<BotScoredItem>();
            foreach (var item in sheet.Ranked)
                if ((item.Category is BotItemCategory.Recovery or BotItemCategory.Defense) && item.ImmediateRescue > 0 && item.Projection.OwnTemperature > 0) care.Add(item);
            if (care.Count == 0) return false;
            if (!j.MayMissCrisis) { chosen = care[0].Candidate; return true; }
            return TryPickByOdds(care, j, random, out chosen);
        }

        internal static bool TryPickLethal(BotScoreSheet sheet, BotJudgementSettings j, out BotCandidate chosen)
        {
            chosen = default;
            if (!MustTakeLethal(sheet, j)) return false;
            foreach (var item in sheet.Ranked)
                if (item.Lethal) { chosen = item.Candidate; return true; }
            return false;
        }

        internal static bool TryPickWeighted(BotScoreSheet sheet, BotJudgementSettings j, System.Random random, out BotCandidate chosen)
        {
            chosen = default;
            return sheet != null && !sheet.Crisis && TryPickByOdds(sheet.Ranked, j, random, out chosen);
        }

        // Stable FNV-1a seed, independent of process string hashes and gameplay RNG.
        internal static int Seed(string botId, PrepInputKey key)
        {
            uint hash = 2166136261;
            foreach (char ch in botId + "/" + key.Generation + "/" + key.Round + "/" + key.Turn)
                hash = unchecked((hash ^ ch) * 16777619);
            return unchecked((int)hash);
        }

        static bool TryPickByOdds(IReadOnlyList<BotScoredItem> ranked, BotJudgementSettings j, System.Random random, out BotCandidate chosen)
        {
            chosen = default;
            if (ranked.Count == 0) return false;
            double roll = random.NextDouble();
            if (roll < j.BestPickChance || ranked.Count == 1) chosen = ranked[0].Candidate;
            else if (roll < j.BestPickChance + j.SecondPickChance) chosen = ranked[1].Candidate;
            else chosen = ranked[random.Next(ranked.Count)].Candidate;
            return true;
        }

        static bool Affordable(BotObservation o, BotCandidate candidate, double readyReserve)
            => !double.IsNaN(candidate.Delay) && !double.IsInfinity(candidate.Delay) && candidate.Delay >= 0
                && (o.OpponentReady ? 0 : candidate.Delay) + readyReserve + 0.05 < o.Remaining;

        static int CompareRank(BotScoredItem a, BotScoredItem b)
        {
            int byScore = Math.Round(b.Final, 4).CompareTo(Math.Round(a.Final, 4));
            if (byScore != 0) return byScore;
            int byOrder = a.Order.CompareTo(b.Order);
            if (byOrder != 0) return byOrder;
            int byItem = a.Candidate.Item.CompareTo(b.Candidate.Item);
            return byItem != 0 ? byItem : a.Candidate.Copy.CompareTo(b.Candidate.Copy);
        }

        static bool TryProfile(IReadOnlyList<BotItemProfile> profiles, short item, out BotItemProfile profile)
        {
            profile = default;
            if (profiles == null || item < 0 || item >= profiles.Count) return false;
            profile = profiles[item];
            return profile.Item == item;
        }

        static bool ScheduledTemperature(ItemEffectRuleSnapshot rule)
            => rule.HasScheduledEffect && rule.ScheduledType == EffectType.TempChange;

        static BotAttackKind Kind(DamageFilter filter) => filter switch
        {
            DamageFilter.Temperature => BotAttackKind.Tool,
            DamageFilter.Food => BotAttackKind.Food,
            _ => BotAttackKind.None
        };

        static BotItemCategory CategoryOf(BotItemFormula formula) => formula switch
        {
            BotItemFormula.None => BotItemCategory.None,
            BotItemFormula.DirectAttack or BotItemFormula.Equalize => BotItemCategory.Attack,
            BotItemFormula.Guard => BotItemCategory.Defense,
            BotItemFormula.Heal => BotItemCategory.Recovery,
            BotItemFormula.DelayedHeal or BotItemFormula.DelayedDamage => BotItemCategory.NextTurn,
            _ => BotItemCategory.Disrupt
        };
    }
}
