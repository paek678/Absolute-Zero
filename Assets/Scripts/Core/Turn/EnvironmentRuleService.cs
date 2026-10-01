using System.Collections.Generic;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using UnityEngine;

namespace AbsoluteZero.Core.Turn
{
    public class EnvironmentRuleService
    {
        public const float KIDS_STEAL_STAGING_SECONDS = 3.2f;
        public const float AMBULANCE_BLANKET_STAGING_SECONDS = 3f;

        public EnvironmentType SelectRandom()
        {
            return EnvironmentCalculations.Select(Random.Range(0, EnvironmentCalculations.PoolCount));
        }

        public float GetPrepDuration(EnvironmentType env, float baseDuration)
        {
            if (env == EnvironmentType.SummerVacation)
            {
                Debug.Log($"[ENV] SummerVacation: prep duration {baseDuration}s → 10s");
            }
            return EnvironmentCalculations.PrepDuration(env, baseDuration);
        }

        public float GetRecoveryRate(EnvironmentType env)
        {
            return EnvironmentCalculations.RecoveryRate(env);
        }

        public bool ShouldApplyKidsEffect(EnvironmentType env, int turnNumber)
        {
            return EnvironmentCalculations.KidsTurn(env, turnNumber);
        }

        public bool ShouldApplyAmbulanceEffect(EnvironmentType env, int turnNumber)
        {
            return EnvironmentCalculations.AmbulanceTurn(env, turnNumber);
        }

        public int DetermineAmbulanceTarget(float p1Temp, float p2Temp)
        {
            if (p1Temp < p2Temp) return 0;
            if (p2Temp < p1Temp) return 1;
            return -1;
        }

        public int DetermineAmbulanceTargetMulti(PlayerState[] players, ISeatStateAccessor roster)
        {
            System.Span<float> temperatures = stackalloc float[players.Length];
            System.Span<bool> eligible = stackalloc bool[players.Length];
            eligible.Clear();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                if (roster != null && roster.GetLifeState((byte)i) != LifeState.Alive) continue;
                eligible[i] = true;
                temperatures[i] = players[i].Temperature.Value;
            }
            return EnvironmentCalculations.MultiAmbulanceTarget(temperatures, eligible);
        }

        public void LogActiveEnvironment(EnvironmentType env, int turnNumber)
        {
            if (env == EnvironmentType.None) return;

            Debug.Log($"[ENV] ===== Turn {turnNumber} — active: {env} ({GetName(env)}) =====");
            switch (env)
            {
                case EnvironmentType.SunnyDay:
                    Debug.Log("[ENV] SunnyDay: recovery rate 1 → 2°/sec (fan-off recovery doubled)");
                    break;
                case EnvironmentType.CoolBreeze:
                    Debug.Log("[ENV] CoolBreeze: recovery rate 1 → 0°/sec (no fan-off recovery)");
                    break;
                case EnvironmentType.CicadaSong:
                    Debug.Log("[ENV] CicadaSong: audio/visual distraction (no gameplay effect yet)");
                    break;
                case EnvironmentType.HeatWaveWarning:
                    Debug.Log("[ENV] HeatWave: lower-temp player acts first this turn");
                    break;
            }
        }

        public void RemoveRandomUnusedItem(PlayerInventory inventory)
        {
            var candidates = new List<int>();
            for (int i = 0; i < inventory.SlotStates.Count; i++)
            {
                var slot = inventory.SlotStates[i];
                if (slot.IsEmpty) continue;
                var itemData = inventory.GetItemData(i);
                if (!EnvironmentCalculations.KidsCandidate(slot.IsEmpty,
                    itemData != null ? itemData.Persistence : (ItemPersistence?)null)) continue;
                candidates.Add(i);
            }

            if (candidates.Count == 0) return;

            int targetSlot = candidates[Random.Range(0, candidates.Count)];
            var targetItem = inventory.GetItemData(targetSlot);
            string itemName = targetItem != null ? targetItem.ItemName : "?";

            var removedSlot = inventory.SlotStates[targetSlot];
            removedSlot.ItemId = -1;
            removedSlot.RemainingUses = 0;
            inventory.SlotStates[targetSlot] = removedSlot;
            inventory.CompactSlots();

            Debug.Log($"[ENV] Kids: removed '{itemName}' from slot {targetSlot}");
        }

        public static string GetName(EnvironmentType env)
        {
            return env switch
            {
                EnvironmentType.SunnyDay => "햇살쨍쨍",
                EnvironmentType.CoolBreeze => "바람선선",
                EnvironmentType.CicadaSong => "매미울음",
                EnvironmentType.Kids => "잼민이들",
                EnvironmentType.Ambulance => "앰뷸런스",
                EnvironmentType.SummerVacation => "여름방학",
                EnvironmentType.HeatWaveWarning => "폭염경보",
                _ => ""
            };
        }
    }
}
