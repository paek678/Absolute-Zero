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
        public readonly float Amount;   // damage, scheduled amount or boosted fan speed
        public readonly float Offset;   // own immediate cost (Soda) or target immediate heal (Samgyetang)
        public readonly float Success;  // 1 unless the human path plays a mini-game
        public readonly float OpponentValue;
        public readonly bool Finite, IsRandomItem;
        public readonly int Order;
        internal BotItemProfile(short item, bool known, BotItemFormula formula, BotItemCategory category,
            BotAttackKind kind, float amount, float offset, float success, float opponentValue,
            bool finite, bool isRandomItem, int order)
        {
            Item = item; Known = known; Formula = formula; Category = category; Kind = kind; Amount = amount;
            Offset = offset; Success = success; OpponentValue = opponentValue; Finite = finite;
            IsRandomItem = isRandomItem; Order = order;
        }
    }

    internal readonly struct BotOpponentItem
    {
        public readonly short Item;
        public readonly bool Usable;
        public BotOpponentItem(short item, bool usable) { Item = item; Usable = usable; }
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
        public readonly bool OpponentReady;
        public readonly double Remaining;
        public readonly float OpponentPrepLoss;
        public readonly int OwnSlots, SlotCapacity;
        public readonly IReadOnlyList<BotOpponentItem> OpponentItems;
        public readonly IReadOnlyList<BotCandidate> Candidates;
        public BotObservation(PrepInputKey key, byte seat, byte opponent, float temperature,
            float opponentTemperature, float fan, float opponentFan, bool opponentReady, double remaining,
            float opponentPrepLoss, int ownSlots, int slotCapacity,
            IEnumerable<BotOpponentItem> opponentItems, IEnumerable<BotCandidate> candidates)
        {
            Key = key; Seat = seat; Opponent = opponent; Temperature = temperature;
            OpponentTemperature = opponentTemperature; FanSpeed = fan; OpponentFanSpeed = opponentFan;
            OpponentReady = opponentReady; Remaining = remaining; OpponentPrepLoss = opponentPrepLoss;
            OwnSlots = ownSlots; SlotCapacity = slotCapacity;
            OpponentItems = new ReadOnlyCollection<BotOpponentItem>(new List<BotOpponentItem>(opponentItems ?? Array.Empty<BotOpponentItem>()));
            Candidates = new ReadOnlyCollection<BotCandidate>(new List<BotCandidate>(candidates ?? Array.Empty<BotCandidate>()));
        }
        public BotObservation WithCandidates(IEnumerable<BotCandidate> candidates)
            => new(Key, Seat, Opponent, Temperature, OpponentTemperature, FanSpeed, OpponentFanSpeed, OpponentReady,
                Remaining, OpponentPrepLoss, OwnSlots, SlotCapacity, OpponentItems, candidates);
    }

    internal readonly struct BotScoredItem
    {
        public readonly BotCandidate Candidate;
        public readonly BotItemCategory Category;
        public readonly float Score, Final, Success;
        public readonly bool Lethal;
        public readonly int Order;
        public BotScoredItem(BotCandidate candidate, BotItemCategory category, float score, float final,
            float success, bool lethal, int order)
        { Candidate = candidate; Category = category; Score = score; Final = final; Success = success; Lethal = lethal; Order = order; }
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
                    offset = Mathf.Max(0, -rule.ImmediateTempDelta); break;
                case ItemEffectKind.Debuff when ScheduledTemperature(rule) && rule.ScheduledValue < 0:
                    formula = BotItemFormula.DelayedDamage; amount = -rule.ScheduledValue;
                    offset = Mathf.Max(0, rule.ImmediateTempDelta); kind = Kind(rule.AttackFilter); break;
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
                item.SlotType == ItemSlotType.Sub, known ? order : int.MaxValue);
        }

        internal static float CurrentHeal(ItemEffectRuleSnapshot rule, ItemSlotNetData slot)
        {
            if (rule.HealPerUse == null || rule.HealPerUse.Length == 0) return 0;
            return Mathf.Max(0, rule.HealPerUse[Mathf.Clamp(rule.MaxUses - slot.RemainingUses, 0, rule.HealPerUse.Length - 1)]);
        }

        internal static BotScoreSheet Score(BotObservation o, IReadOnlyList<BotItemProfile> profiles,
            BotJudgementSettings j, BotScoringCoefficients c, double readyReserve)
        {
            float toolThreat = 0, foodThreat = 0, randomWorth = 0, bestWorth = 0, bestStealable = 0;
            bool toolGuard = false, foodGuard = false;
            foreach (var held in o.OpponentItems)
            {
                if (!TryProfile(profiles, held.Item, out var p)) continue;
                if (p.Known)
                {
                    if (p.IsRandomItem) randomWorth += p.OpponentValue;
                    bestWorth = Mathf.Max(bestWorth, p.OpponentValue);
                    if (p.Finite) bestStealable = Mathf.Max(bestStealable, p.OpponentValue);
                }
                if (!held.Usable) continue;
                if (p.Formula is BotItemFormula.DirectAttack or BotItemFormula.DelayedDamage)
                {
                    if (p.Kind == BotAttackKind.Tool) toolThreat = Mathf.Max(toolThreat, p.Amount);
                    else if (p.Kind == BotAttackKind.Food) foodThreat = Mathf.Max(foodThreat, p.Amount);
                }
                else if (p.Formula == BotItemFormula.Guard)
                {
                    toolGuard |= p.Kind == BotAttackKind.Tool;
                    foodGuard |= p.Kind == BotAttackKind.Food;
                }
            }
            float block = Mathf.Clamp01(j.OpponentBlockChance);
            float Blocked(BotAttackKind kind)
                => kind == BotAttackKind.Tool && toolGuard || kind == BotAttackKind.Food && foodGuard ? block : 0;
            float threat = Mathf.Max(toolThreat, foodThreat);

            // Cooling until this bot readies: its delay, or nothing once the opponent readied first.
            double shortest = double.PositiveInfinity;
            foreach (var candidate in o.Candidates)
                if (Affordable(o, candidate, readyReserve)) shortest = Math.Min(shortest, candidate.Delay);
            float ownLoss = o.OpponentReady || double.IsPositiveInfinity(shortest) ? 0
                : Mathf.Max(0, o.FanSpeed) * (float)Math.Min(o.Remaining, shortest);
            bool crisis = o.Temperature - ownLoss - threat <= 0;
            float care = crisis ? j.CrisisCareMultiplier
                : o.Temperature - o.OpponentTemperature >= j.ComfortMargin ? j.ComfortCareMultiplier : 1;
            const float maxTemp = TemperatureSystem.MAX_TEMP;

            var ranked = new List<BotScoredItem>();
            var seen = new HashSet<short>();
            foreach (var candidate in o.Candidates)
            {
                if (!TryProfile(profiles, candidate.Item, out var p) || !p.Known || !seen.Add(candidate.Item)) continue;
                if (!Affordable(o, candidate, readyReserve)) continue;
                float s = p.Success, score;
                bool lethal = false;
                switch (p.Formula)
                {
                    case BotItemFormula.DirectAttack:
                        score = p.Amount * s * (1 - Blocked(p.Kind));
                        lethal = score > 0 && score >= o.OpponentTemperature;
                        break;
                    case BotItemFormula.Equalize:
                        score = (o.OpponentTemperature - o.Temperature) * s; break;
                    case BotItemFormula.Guard:
                        score = (p.Kind == BotAttackKind.Tool ? toolThreat : p.Kind == BotAttackKind.Food ? foodThreat : 0)
                            * j.OpponentAttackChance * care;
                        break;
                    case BotItemFormula.Heal:
                        // Score unit is temperature; heals above the maximum are lost.
                        score = Mathf.Min(candidate.Heal, maxTemp - o.Temperature) * s * care; break;
                    case BotItemFormula.DelayedHeal:
                        // Soda's immediate self damage must not create a mortal danger.
                        if (p.Offset > 0 && o.Temperature - ownLoss - p.Offset - threat <= 0) continue;
                        score = (Mathf.Min(p.Amount, maxTemp - (o.Temperature - p.Offset)) * j.LookaheadFactor - p.Offset) * s * care;
                        break;
                    case BotItemFormula.DelayedDamage:
                        score = (p.Amount * j.LookaheadFactor - p.Offset) * s * (1 - Blocked(p.Kind)); break;
                    case BotItemFormula.Reroll:
                        score = randomWorth * c.RerollValueFactor * j.LookaheadFactor * s; break;
                    case BotItemFormula.Steal:
                        if (o.OwnSlots >= o.SlotCapacity) continue;
                        score = bestStealable * c.StealValueFactor * j.LookaheadFactor * s; break;
                    case BotItemFormula.BlockBasics:
                        score = c.BlockBasicsValue * j.LookaheadFactor * s; break;
                    case BotItemFormula.Neutralize:
                        score = bestWorth * c.NeutralizeValueFactor * s; break;
                    case BotItemFormula.FanBoost:
                        // Re-applying an existing boost has no effect.
                        if (o.OpponentFanSpeed <= 0 || o.OpponentFanSpeed >= p.Amount) continue;
                        score = o.OpponentPrepLoss * (p.Amount / o.OpponentFanSpeed - 1) * c.FanBoostTurns * j.LookaheadFactor * s;
                        break;
                    default: continue;
                }
                float final = score + (lethal ? j.KillBonus * s : 0);
                if (!(final > 0) || float.IsInfinity(final)) continue;
                ranked.Add(new BotScoredItem(candidate, p.Category, score, final, s, lethal, p.Order));
            }
            ranked.Sort(CompareRank);
            return new BotScoreSheet(crisis, care, ownLoss, threat, ranked);
        }

        internal static bool MustTakeLethal(BotScoreSheet sheet, BotJudgementSettings j)
            => sheet != null && !j.MayMissLethal && sheet.HasLethal;

        internal static bool TryPickCrisis(BotScoreSheet sheet, BotJudgementSettings j, System.Random random, out BotCandidate chosen)
        {
            chosen = default;
            if (sheet == null || !sheet.Crisis) return false;
            var care = new List<BotScoredItem>();
            foreach (var item in sheet.Ranked)
                if (item.Category is BotItemCategory.Recovery or BotItemCategory.Defense) care.Add(item);
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
            return sheet != null && TryPickByOdds(sheet.Ranked, j, random, out chosen);
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
            => (o.OpponentReady ? 0 : candidate.Delay) + readyReserve + 0.05 < o.Remaining;

        static int CompareRank(BotScoredItem a, BotScoredItem b)
        {
            int byScore = Math.Round(b.Final, 4).CompareTo(Math.Round(a.Final, 4));
            if (byScore != 0) return byScore;
            int byOrder = a.Order.CompareTo(b.Order);
            return byOrder != 0 ? byOrder : a.Candidate.Item.CompareTo(b.Candidate.Item);
        }

        static bool TryProfile(IReadOnlyList<BotItemProfile> profiles, short item, out BotItemProfile profile)
        {
            profile = default;
            if (profiles == null || item < 0 || item >= profiles.Count) return false;
            profile = profiles[item];
            return true;
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
