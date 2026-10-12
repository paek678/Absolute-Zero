using System.Collections;
using System.Collections.Generic;
using AbsoluteZero.Core.Audio;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Combat
{
    internal sealed class ItemPresentationSequence
    {
        readonly ItemPresentationServices _services;
        public ItemPresentationSequence(ItemPresentationServices services) => _services = services;
        CombatPresentationContext Context => _services.Context;
        PresentationResources _resources => _services.Resources;
        Coroutine Run(IEnumerator routine) => _services.Run(routine);
        AZPlayerVisual GetPlayerVisual(int seat, NetworkManager _) => Context.GetVisual(seat);
        Vector3 GetPlayerWorldPos(int seat) => Context.GetWorldPosition(seat);
        static void Destroy(Object value) => PresentationResources.DestroyOwned(value);
        ContactItemChoreography _contact => _contactCached ??= new ContactItemChoreography(_services);
        CatItemChoreography _cat => _catCached ??= new CatItemChoreography(_services);
        ContactItemChoreography _contactCached;
        CatItemChoreography _catCached;
        static readonly WaitForSeconds _waitDamageReact = new(0.7f);
        void ApplyMultiEventTemps(IReadOnlyList<CombatEventNetData> events)
        {
            foreach (var evt in events)
            { _services.Temperature(evt.ActorSeat, evt.ActorResultTemp); _services.Temperature(evt.TargetSeat, evt.TargetResultTemp); }
        }
        void ApplyEventTemps(int actor, CombatResultData result)
        {
            if (result.EventCount > 0 && result.Event0Source == actor)
            { _services.Temperature(result.Event0Source, result.Event0UserTemp); _services.Temperature(result.Event0Target, result.Event0TargetTemp); }
            else if (result.EventCount > 1 && result.Event1Source == actor)
            { _services.Temperature(result.Event1Source, result.Event1UserTemp); _services.Temperature(result.Event1Target, result.Event1TargetTemp); }
        }

        public IEnumerator PlayMulti(int actorSeat, short itemId, NetworkManager nm,
            IReadOnlyList<CombatEventNetData> mainEffects)
        {
            var itemData = _services.Item.Item;
            var actorVisual = _services.Item.Actor;

            if (itemData == null || actorVisual == null) yield break;

            bool isLocalUser = actorVisual.IsLocalHuman;
            bool isAttack = itemData.Category == ItemCategory.Attack;
            bool isRecovery = itemData.Category == ItemCategory.Recovery;
            int targetSeat = mainEffects.Count > 0 ? mainEffects[0].TargetSeat : actorSeat;
            var targetVisual = Context.GetVisual(targetSeat);

            if (isLocalUser && itemData.Category == ItemCategory.Buff)
                ScreenVFXManager.Instance.PlayRecoveryVFX();

            string userTrigger = isLocalUser
                ? itemData.AnimTrigger
                : (!string.IsNullOrEmpty(itemData.OpponentAnimTrigger) ? itemData.OpponentAnimTrigger : itemData.AnimTrigger);

            bool fullyBlocked = mainEffects.Count > 0;
            bool recipientDefending = false;
            foreach (var evt in mainEffects)
            {
                fullyBlocked &= CombatVFXManager.IsFullyBlockedImpact(evt.Flags);
                recipientDefending |= (evt.Flags & CombatImpactFlags.Defense) != 0;
            }
            var contact = ItemPresentation.GetChoreography(itemData);
            bool isContact = contact == ItemChoreography.Hug || contact == ItemChoreography.Feed;
            System.Action impact = () =>
            {
                ApplyMultiEventTemps(mainEffects);
                PlayMultiImpactReactions(actorSeat, isAttack, isRecovery,
                    isLocalUser, 0, mainEffects, nm, isContact);
            };

            if (isContact)
            {
                yield return Run(PlayContact(contact, itemData, userTrigger, impact, !fullyBlocked, recipientDefending));
                yield break;
            }

            if (string.IsNullOrEmpty(userTrigger))
            {
                if (ItemPresentation.GetChoreography(itemData) == ItemChoreography.Cat)
                {
                    float catMinDur = itemData.AnimDuration > 0f
                        ? itemData.AnimDuration
                        : CombatVFXManager.MinimumActionDuration;
                    yield return Run(_cat.PlayCatSpriteSequence(
                        actorSeat, targetSeat, isLocalUser, itemData, catMinDur, impact));
                    yield break;
                }
                ApplyMultiEventTemps(mainEffects);
                int fallbackHits = Mathf.Max(1, itemData.EffectHitCount);
                for (int h = 0; h < fallbackHits; h++)
                {
                    PlayMultiImpactReactions(actorSeat, isAttack, isRecovery,
                        isLocalUser, h, mainEffects, nm);
                    if (h < fallbackHits - 1 && itemData.EffectInterval > 0f)
                        yield return new WaitForSeconds(itemData.EffectInterval);
                }
                yield break;
            }

            yield return Run(PlayAnimatedItem(itemData, actorVisual, userTrigger,
                () => ApplyMultiEventTemps(mainEffects),
                hit => PlayMultiImpactReactions(actorSeat, isAttack, isRecovery,
                    isLocalUser, hit, mainEffects, nm)));

            foreach (var evt in mainEffects)
            {
                if ((evt.Flags & CombatImpactFlags.Defense) == 0) continue;
                var defendedVisual = GetPlayerVisual(evt.TargetSeat, nm);
                defendedVisual?.ReturnToIdle();
                if (defendedVisual != null && defendedVisual.IsLocalHuman)
                    Context.Fps?.ReturnToIdle();
            }

            var choreography = ItemPresentation.GetChoreography(itemData);
            if (choreography == ItemChoreography.RedCard && targetVisual != null)
            {
                targetVisual.PlayCombatAnimation("disappoint");
                yield return _waitDamageReact;
                targetVisual.ReturnToIdle();
            }
        }

        void PlayMultiImpactReactions(int actorSeat, bool isAttack, bool isRecovery,
            bool isLocalUser, int hitIndex, IReadOnlyList<CombatEventNetData> events,
            NetworkManager nm, bool preserveTargetPose = false)
        {
            foreach (var evt in events)
            {
                int targetSeat = evt.TargetSeat;
                if (targetSeat == actorSeat) continue;
                var targetVisual = Context.GetVisual(targetSeat);
                if (targetVisual == null) continue;

                bool targetDefending = (evt.Flags & CombatImpactFlags.Defense) != 0;
                bool targetDamaged = (evt.Flags & CombatImpactFlags.Damage) != 0;
                if (!isAttack && !targetDefending && !targetDamaged) continue;

                if (targetDefending && hitIndex == 0)
                    PlayDefenseReaction(targetVisual, evt.DefenseItemId);
                if (!targetDamaged) continue;

                targetVisual.PlayDamageFlash(preserveCombatAnimation: targetDefending || preserveTargetPose);
                if (!isLocalUser)
                {
                    _services.Hit(GetPlayerWorldPos(targetSeat));
                    if (hitIndex == 0)
                    {
                        if (targetVisual.IsLocalHuman)
                        {
                            ScreenVFXManager.Instance.PlayHitVFX();
                            CameraShake.Instance?.Shake(0.15f, 0.1f);
                        }
                        _services.IceBreak(GetPlayerWorldPos(targetSeat));
                    }
                }
                GameAudioManager.Instance?.PlayDamaged();
            }

            if (isRecovery && isLocalUser)
            {
                _services.Hit(GetPlayerWorldPos(actorSeat));
                if (hitIndex == 0) ScreenVFXManager.Instance.PlayRecoveryVFX();
            }
        }

        IEnumerator PlayContact(ItemChoreography choreography, ItemDataSO itemData, string userTrigger,
            System.Action impact, bool contactAllowed, bool recipientDefending)
        {
            if (choreography == ItemChoreography.Hug)
                yield return _contact.PlayHugSequence(_services.Item.ActorSeat, _services.Item.TargetSeat,
                    _services.Item.Actor, _services.Item.Target, itemData,
                    _services.Item.Actor != null && _services.Item.Actor.IsLocalHuman, impact, contactAllowed);
            else
                // One receiving pose: even partial defense must not interrupt a just-started feed/use.
                yield return _contact.PlayFeedSequence(itemData, userTrigger, impact, contactAllowed && !recipientDefending);
        }

        void PlayDefenseReaction(AZPlayerVisual targetVisual, short defenseItemId)
        {
            if (targetVisual == null) return;

            var defenseItem = defenseItemId >= 0
                ? Context.Items?.GetItemData(defenseItemId)
                : null;
            string trigger = targetVisual.IsLocalHuman
                ? defenseItem?.AnimTrigger
                : (!string.IsNullOrEmpty(defenseItem?.OpponentAnimTrigger)
                    ? defenseItem.OpponentAnimTrigger
                    : defenseItem?.AnimTrigger);
            if (string.IsNullOrEmpty(trigger)) trigger = "defence";

            if (!targetVisual.IsLocalHuman)
                targetVisual.PrepareDefenseSprite(GameSprites.GetItemSpriteFor(defenseItem));
            if (!targetVisual.IsLocalHuman) targetVisual.PlayCombatAnimation(trigger);
            GameAudioManager.Instance?.PlayItemSfxFor(trigger, defenseItem);
            if (targetVisual.IsLocalHuman)
                Context.Fps?.PlayFPSItemAnimation(trigger, defenseItem);
        }

        public IEnumerator PlayDuel(int userIdx, short itemId, NetworkManager nm,
            byte impactFlags, short defenseItemId, CombatResultData result)
        {
            var itemData = _services.Item.Item;
            if (itemData == null)
            {
                Debug.LogWarning($"[CombatVFX] PlayItemSequence: itemData NULL for itemId={itemId}");
                yield break;
            }

            int targetIdx = 1 - userIdx;
            var userVisual = _services.Item.Actor;
            var targetVisual = _services.Item.Target;

            bool isAttack = itemData.Category == ItemCategory.Attack;
            bool isRecovery = itemData.Category == ItemCategory.Recovery;
            bool isLocalUser = userVisual != null && userVisual.IsLocalHuman;
            bool targetDefending = (impactFlags & CombatImpactFlags.Defense) != 0;
            bool targetDamaged = (impactFlags & CombatImpactFlags.Damage) != 0;
            bool fullyBlocked = CombatVFXManager.IsFullyBlockedImpact(impactFlags);

            if (isLocalUser && itemData.Category == ItemCategory.Buff)
                ScreenVFXManager.Instance.PlayRecoveryVFX();
            else if (!isLocalUser && itemData.Category == ItemCategory.Debuff && targetDamaged)
                ScreenVFXManager.Instance.PlayHitVFX();

            string userTrigger = isLocalUser
                ? itemData.AnimTrigger
                : (!string.IsNullOrEmpty(itemData.OpponentAnimTrigger) ? itemData.OpponentAnimTrigger : itemData.AnimTrigger);
            bool hasUserAnim = userVisual != null && !string.IsNullOrEmpty(userTrigger);

            Debug.Log($"[CombatVFX] PlayItemSequence: P{userIdx} '{itemData.ItemName}' trigger='{userTrigger}' (1P='{itemData.AnimTrigger}' 3P='{itemData.OpponentAnimTrigger}') isLocal={isLocalUser} cat={itemData.Category}");

            var contact = ItemPresentation.GetChoreography(itemData);
            if (contact == ItemChoreography.Hug || contact == ItemChoreography.Feed)
            {
                yield return Run(PlayContact(contact, itemData, userTrigger, () =>
                {
                    ApplyEventTemps(userIdx, result);
                    if (targetVisual == null) return;
                    if (targetDefending) PlayDefenseReaction(targetVisual, defenseItemId);
                    if (!targetDamaged) return;
                    targetVisual.PlayDamageFlash(preserveCombatAnimation: true);
                    if (targetVisual.IsLocalHuman)
                    {
                        _services.Hit(GetPlayerWorldPos(targetIdx));
                        ScreenVFXManager.Instance.PlayHitVFX();
                        CameraShake.Instance?.Shake(0.15f, 0.1f);
                        _services.IceBreak(GetPlayerWorldPos(targetIdx));
                    }
                    GameAudioManager.Instance?.PlayDamaged();
                }, !fullyBlocked, targetDefending));
                yield break;
            }

            if (hasUserAnim)
            {
                yield return Run(PlayAnimatedItem(itemData, userVisual, userTrigger,
                    () => ApplyEventTemps(userIdx, result), hit =>
                    {
                        if ((isAttack || targetDefending) && targetVisual != null)
                        {
                            if (targetDefending && hit == 0)
                                PlayDefenseReaction(targetVisual, defenseItemId);
                            if (!targetDamaged) return;
                            targetVisual.PlayDamageFlash(preserveCombatAnimation: targetDefending);
                            if (!isLocalUser)
                            {
                                _services.Hit(GetPlayerWorldPos(targetIdx));
                                if (hit == 0)
                                {
                                    ScreenVFXManager.Instance.PlayHitVFX();
                                    CameraShake.Instance?.Shake(0.15f, 0.1f);
                                    _services.IceBreak(GetPlayerWorldPos(targetIdx));
                                }
                            }
                            GameAudioManager.Instance?.PlayDamaged();
                        }
                        else if (isRecovery && isLocalUser && itemData.EffectHitCount > 0 && itemData.EffectDelay > 0f)
                        {
                            _services.Hit(GetPlayerWorldPos(userIdx));
                            if (hit == 0) ScreenVFXManager.Instance.PlayRecoveryVFX();
                        }
                    }));

                if (targetDefending && targetVisual != null)
                {
                    targetVisual.ReturnToIdle();
                    if (targetVisual.IsLocalHuman)
                        Context.Fps?.ReturnToIdle();
                }

                if (ItemPresentation.GetChoreography(itemData) == ItemChoreography.Screwdriver && targetVisual != null)
                    _contact.TintTargetFanBlue(targetVisual);
            }
            else if (ItemPresentation.GetChoreography(itemData) == ItemChoreography.Cat)
            {
                ApplyEventTemps(userIdx, result);
                float catMinDur = itemData.AnimDuration > 0f ? itemData.AnimDuration : 1.5f;
                yield return Run(_cat.PlayCatSpriteSequence(userIdx, targetIdx, isLocalUser, itemData, catMinDur));
            }
            else if ((isAttack || isRecovery || targetDefending) &&
                (itemData.EffectHitCount > 0 || targetDefending))
            {
                ApplyEventTemps(userIdx, result);
                if ((isAttack || targetDefending) && targetVisual != null)
                {
                    if (targetDefending)
                    {
                        PlayDefenseReaction(targetVisual, defenseItemId);
                    }
                    if (targetDamaged)
                    {
                        targetVisual.PlayDamageFlash(preserveCombatAnimation: targetDefending);
                        if (!isLocalUser)
                        {
                            _services.Hit(GetPlayerWorldPos(targetIdx));
                            ScreenVFXManager.Instance.PlayHitVFX();
                            CameraShake.Instance?.Shake(0.15f, 0.1f);
                            _services.IceBreak(GetPlayerWorldPos(targetIdx));
                        }
                        GameAudioManager.Instance?.PlayDamaged();
                    }
                }
                else if (isRecovery)
                {
                    if (isLocalUser)
                    {
                        _services.Hit(GetPlayerWorldPos(userIdx));
                        ScreenVFXManager.Instance.PlayRecoveryVFX();
                    }
                }
                yield return _waitDamageReact;
                if (targetDefending && targetVisual != null)
                {
                    targetVisual.ReturnToIdle();
                    if (targetVisual.IsLocalHuman)
                        Context.Fps?.ReturnToIdle();
                }
            }

            bool isTargetOpponent = isLocalUser;
            var choreography = ItemPresentation.GetChoreography(itemData);

            if (choreography == ItemChoreography.RedCard && targetVisual != null && !isTargetOpponent)
            {
                targetVisual.PlayCombatAnimation("disappoint");
                yield return _waitDamageReact;
                targetVisual.ReturnToIdle();
            }

            Debug.Log($"[CombatVFX] PlayItemSequence DONE: P{userIdx} '{itemData.ItemName}'");
        }

        // Both modes play the authored duel gesture and its full timeline here.
        // Only authoritative outcome application and seat-specific reactions vary.
        IEnumerator PlayAnimatedItem(ItemDataSO item, AZPlayerVisual actor, string trigger,
            System.Action applyOutcome, System.Action<int> react)
        {
            bool local = actor.IsLocalHuman;
            try
            {
                if (local) Context.Fps?.PlayFPSItemAnimation(item.AnimTrigger, item);
                else
                {
                    actor.SetItemSprite(GameSprites.GetItemSpriteFor(item));
                    actor.PlayCombatAnimation(trigger);
                }
                if (ItemPresentation.GetChoreography(item) == ItemChoreography.Buldak)
                    Run(_contact.PlayBuldakSfx(local, item));
                else
                    GameAudioManager.Instance?.PlayItemSfxFor(item.AnimTrigger, item);

                _services.ItemCue("gesture");
                float duration = GetAnimDuration(item, actor.GetAnimator(), trigger);
                bool timed = item.EffectHitCount > 0 && item.EffectDelay > 0f;
                float delay = timed ? item.EffectDelay : duration * 0.5f;
                if (delay > 0f) yield return new WaitForSeconds(delay);
                applyOutcome();
                float remaining = duration - delay;
                int hits = timed ? item.EffectHitCount : 1;
                for (int hit = 0; hit < hits; hit++)
                {
                    react(hit);
                    _services.ItemCue("impact");
                    if (hit < hits - 1 && item.EffectInterval > 0f)
                    {
                        yield return new WaitForSeconds(item.EffectInterval);
                        remaining -= item.EffectInterval;
                    }
                }
                if (remaining > 0f) yield return new WaitForSeconds(remaining);
            }
            finally
            {
                // A cancelled older performance must not reset a newer one.
                // The manager owns cleanup once this execution/binding expires.
                if (_services.Item.IsCurrent)
                {
                    if (actor != null) actor.ReturnToIdle();
                    if (local) Context.Fps?.ReturnToIdle();
                }
            }
            _services.ItemCue("gesture-end");
        }

        float GetAnimDuration(ItemDataSO itemData, Animator animator, string trigger = null)
        {
            if (itemData.AnimDuration > 0f) return itemData.AnimDuration;
            if (animator == null) return 0.8f;
            animator.Update(0f);
            var info = animator.GetCurrentAnimatorStateInfo(0);
            return info.length > 0f ? info.length : 0.8f;
        }

    }
}
