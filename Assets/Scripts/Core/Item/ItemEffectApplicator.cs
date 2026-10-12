using AbsoluteZero.Core.Item.Data;

namespace AbsoluteZero.Core.Item
{
    public static class ItemEffectApplicator
    {
        public static void Apply(ItemContext ctx, in ItemEffectOutcome outcome)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var balanceAction = Solo.PlaytestTelemetry.BeginItem(ctx.UserIndex, ctx.TargetIndex, ctx.UserSlot.ItemId, ctx.UserSlot.CopyId);
            using var observation = Solo.PlaytestTelemetry.Observe(ctx.User, "effect_apply:item=" + ctx.UserSlot.ItemId + ";target=" + ctx.TargetIndex, ctx.UserSlot.CopyId);
#endif
            if (outcome.Blocked)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                observation.Verdict("Blocked");
                Solo.PlaytestTelemetry.Current?.Balance("item_result", new Solo.BalanceObservation {
                    action = Solo.PlaytestTelemetry.Current.CurrentBalanceAction, reason = "Blocked", target = ctx.TargetIndex });
#endif
                return;
            }

            if (outcome.UserHeal > 0f)
                ctx.TempSystem.ApplyHeal(ctx.User, outcome.UserHeal);
            if (outcome.UserDamage > 0f)
                ctx.TempSystem.ApplyDamage(ctx.User, outcome.UserDamage, outcome.UserDamageFilter, null);

            if (outcome.TargetHeal > 0f)
                ctx.TempSystem.ApplyHeal(ctx.Target, outcome.TargetHeal);
            if (outcome.TargetDamage > 0f)
                ctx.TempSystem.ApplyDamage(ctx.Target, outcome.TargetDamage,
                    outcome.TargetDamageFilter, outcome.TargetDefenseCheck);

            if (outcome.SetUserDefense.HasValue)
                ctx.UserModifiers.ActiveDefense = outcome.SetUserDefense;
            if (outcome.NeutralizeTarget)
                ctx.TargetModifiers.ActionNeutralized = true;
            if (outcome.GrantExtraAction)
                ctx.UserModifiers.HasExtraAction = true;
            if (outcome.RevealOpponent)
                ctx.UserModifiers.OpponentRevealed = true;

            if (outcome.BlockTargetBasics)
                ctx.Target.IsBasicBlocked.Value = true;
            if (outcome.WriteUserFanSpeed)
                ctx.User.FanSpeed.Value = outcome.UserFanSpeedValue;
            if (outcome.WriteTargetFanSpeed)
                ctx.Target.FanSpeed.Value = outcome.TargetFanSpeedValue;

            if (outcome.HasScheduledEffect)
                ctx.BuffSystem.Schedule(outcome.ScheduledTargetIndex, outcome.ScheduledType,
                    outcome.ScheduledValue, outcome.ScheduledDelayTurns);

            switch (outcome.InventoryAction)
            {
                case InventoryMutationType.RerollTarget:
                    ctx.TargetInventory.RerollAllRandom(ctx.DropTable);
                    break;
                case InventoryMutationType.StealFromTarget:
                    ctx.UserInventory.StealRandomItem(ctx.TargetInventory);
                    break;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            observation.Verdict("Applied");
            Solo.PlaytestTelemetry.Current?.Balance("item_result", new Solo.BalanceObservation {
                action = Solo.PlaytestTelemetry.Current.CurrentBalanceAction, reason = "Applied", target = ctx.TargetIndex });
#endif
        }
    }
}
