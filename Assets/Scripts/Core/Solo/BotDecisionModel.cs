using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    public enum BotTactic { Survival, Finish, Counter, Resource, General }

    // Immutable own/public data only. No PlayerState, NetworkVariable, opponent
    // inventory/selection or mutable SO enters the observation or score function.
    internal readonly struct BotCandidate
    {
        public readonly uint Copy;
        public readonly byte Target;
        public readonly short Item;
        public readonly double Delay;
        public readonly float Damage, Heal, Defense, Utility;
        public readonly bool Permanent;
        public BotCandidate(uint copy, byte target, short item, double delay,
            float damage, float heal, float defense, float utility, bool permanent)
        { Copy = copy; Target = target; Item = item; Delay = delay; Damage = damage; Heal = heal; Defense = defense; Utility = utility; Permanent = permanent; }
    }
    internal sealed class BotObservation
    {
        public readonly PrepInputKey Key;
        public readonly byte Seat, Opponent;
        public readonly float Temperature, OpponentTemperature, FanSpeed, OpponentFanSpeed;
        public readonly bool OpponentReady;
        public readonly double Remaining;
        public readonly IReadOnlyList<BotCandidate> Candidates;
        public BotObservation(PrepInputKey key, byte seat, byte opponent, float temperature,
            float opponentTemperature, float fan, float opponentFan, bool opponentReady,
            double remaining, IList<BotCandidate> candidates)
        {
            Key = key; Seat = seat; Opponent = opponent; Temperature = temperature;
            OpponentTemperature = opponentTemperature; FanSpeed = fan; OpponentFanSpeed = opponentFan;
            OpponentReady = opponentReady; Remaining = remaining;
            Candidates = new ReadOnlyCollection<BotCandidate>(new List<BotCandidate>(candidates));
        }
    }
    internal static class BotDecisionModel
    {
        internal static BotCandidate Estimate(ItemEffectRuleSnapshot rule, ItemSlotNetData slot,
            byte target, double delay, float ownTemperature, float opponentTemperature, bool permanent)
        {
            float damage = rule.EqualizeToUserTemp ? Mathf.Max(0, opponentTemperature - ownTemperature) : Mathf.Max(0, rule.BaseDamage);
            float heal = 0;
            if (rule.HealPerUse?.Length > 0)
                heal = rule.HealPerUse[Mathf.Clamp(rule.MaxUses - slot.RemainingUses, 0, rule.HealPerUse.Length - 1)];
            if (rule.IsSelfTarget) heal += Mathf.Max(0, rule.ImmediateTempDelta);
            else damage += Mathf.Max(0, -rule.ImmediateTempDelta);
            float utility = rule.InventoryAction != InventoryMutationType.None || rule.BlocksTargetBasics || rule.NeutralizesTarget ? 2 : 0;
            if (rule.WritesFanSpeed || rule.WritesTargetFanSpeed || rule.HasScheduledEffect) utility += 1;
            // Estimates never promise hidden-opponent defense or delayed-effect outcomes.
            return new BotCandidate(slot.CopyId, target, slot.ItemId, delay, damage,
                Mathf.Min(Mathf.Max(0, 37 - ownTemperature), Mathf.Max(0, heal)),
                rule.IsDefense ? Mathf.Min(10, Mathf.Max(0, rule.DefenseReduction)) : 0, utility, permanent);
        }
        internal static bool TryChoose(BotObservation observation, BotTactic tactic,
            BotTacticalWeights weights, float noise, System.Random random, double reserve, out BotCandidate chosen)
        {
            chosen = default;
            float weight = tactic switch { BotTactic.Survival => weights.Survival, BotTactic.Finish => weights.Finish,
                BotTactic.Counter => weights.Counter, BotTactic.Resource => weights.Resource, _ => weights.General };
            if (weight <= 0) return false;
            double best = double.NegativeInfinity;
            foreach (var candidate in observation.Candidates)
            {
                if (candidate.Delay + reserve + 0.05 >= observation.Remaining) continue;
                float score = tactic switch
                {
                    BotTactic.Survival => observation.Temperature <= 12 ? candidate.Heal : 0,
                    BotTactic.Finish => candidate.Damage >= observation.OpponentTemperature ? candidate.Damage : 0,
                    BotTactic.Counter => observation.Temperature <= 20 && observation.OpponentFanSpeed > 1 ? candidate.Defense : 0,
                    BotTactic.Resource => candidate.Permanent && observation.Temperature > 20 && observation.OpponentTemperature > 20 ? candidate.Damage : 0,
                    _ => candidate.Damage + candidate.Heal * 0.7f + candidate.Utility + candidate.Defense * 0.1f
                };
                if (score <= 0) continue;
                double ranked = score * weight + (noise > 0 ? random.NextDouble() * noise : 0);
                if (ranked > best) { best = ranked; chosen = candidate; }
            }
            return !double.IsNegativeInfinity(best);
        }
        // Stable FNV-1a seed, independent of process string hashes and gameplay RNG.
        internal static int Seed(string botId, PrepInputKey key)
        {
            uint hash = 2166136261;
            foreach (char ch in botId + "/" + key.Generation + "/" + key.Round + "/" + key.Turn)
                hash = unchecked((hash ^ ch) * 16777619);
            return unchecked((int)hash);
        }
    }
}
