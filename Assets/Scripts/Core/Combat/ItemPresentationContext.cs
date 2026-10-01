using System;
using System.Collections;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using UnityEngine;

namespace AbsoluteZero.Core.Combat
{
    internal sealed class ItemPresentationContext
    {
        readonly PlayerBinding _actorBinding, _targetBinding;
        public PresentationExecution Execution { get; }
        public ItemDataSO Item { get; }
        public int ActorSeat { get; }
        public int TargetSeat { get; }
        public AZPlayerVisual Actor { get; }
        public AZPlayerVisual Target { get; }
        public CombatPresentationContext Match => Execution.Context;
        public bool IsCurrent => Execution.CanContinue && Match.IsBindingCurrent(_actorBinding)
            && (_targetBinding == null || Match.IsBindingCurrent(_targetBinding));

        public ItemPresentationContext(PresentationExecution execution, int actor, int target, short itemId)
        {
            Execution = execution; ActorSeat = actor; TargetSeat = target;
            Item = Match.Items != null ? Match.Items.GetItemData(itemId) : null;
            _actorBinding = Match.GetBinding(actor); _targetBinding = Match.GetBinding(target);
            Actor = Match.GetVisual(actor); Target = Match.GetVisual(target);
        }
    }

    // Presentation-only port: no network commands, state writes, phase changes or completion ACKs.
    internal sealed class ItemPresentationServices
    {
        readonly CombatVFXManager _owner;
        public ItemPresentationContext Item { get; }
        public CombatPresentationContext Context => Item.Match;
        public PresentationResources Resources => Item.Execution.Resources;
        public ItemPresentationServices(CombatVFXManager owner, ItemPresentationContext item)
        { _owner = owner; Item = item; }
        public Coroutine Run(IEnumerator routine)
            => _owner.RunChoreography(routine, Item.Execution, () => Item.IsCurrent);
        public PresentationTransforms.Lease CaptureTransform(Transform target)
            => _owner.CapturePresentationTransform(target, Resources);
        public void Temperature(int seat, float value) => _owner.ShowTemperature(seat, value);
        public void Hit(Vector3 position) => _owner.PlayHitAt(position);
        public void IceBreak(Vector3 position) => _owner.PlayIceBreakAt(position);
    }
}
