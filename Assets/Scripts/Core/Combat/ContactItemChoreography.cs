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
    internal sealed class ContactItemChoreography
    {
        readonly ItemPresentationServices _services;
        public ContactItemChoreography(ItemPresentationServices services) => _services = services;
        CombatPresentationContext Context => _services.Context;
        PresentationResources _resources => _services.Resources;
        Coroutine Run(IEnumerator routine) => _services.Run(routine);
        AZPlayerVisual GetPlayerVisual(int seat, NetworkManager _) => Context.GetVisual(seat);
        Vector3 GetPlayerWorldPos(int seat) => Context.GetWorldPosition(seat);
        static void Destroy(Object value) => PresentationResources.DestroyOwned(value);
        static readonly WaitForSeconds _waitFeedTransfer = new(ContactPresentationTiming.FeedTransfer);
        static readonly WaitForSeconds _waitBuldak07 = new(0.7f);
        static readonly WaitForSeconds _waitBuldak02 = new(0.2f);

        public IEnumerator PlayFeedSequence(ItemDataSO itemData, string userTrigger,
            System.Action onImpact, bool contactAllowed)
        {
            var actor = _services.Item.Actor;
            var target = _services.Item.Target;
            bool localActor = actor != null && actor.IsLocalHuman;
            bool localTarget = target != null && target.IsLocalHuman;
            var fps = Context.Fps;
            GameObject food = null;
            _services.ContactCue("begin");
            var fpsLease = localActor || localTarget ? _services.CaptureTransform(fps != null ? fps.transform : null) : null;
            try
            {
                if (fps != null && (localActor || localTarget))
                {
                    var pose = fps.GetContactCameraPose();
                    FollowHands(Context.Camera, fps.transform, pose.position, pose.rotation);
                }
                if (localActor) fps?.PlayFPSItemAnimation("feed", itemData);
                else if (actor != null)
                {
                    actor.SetItemSprite(GameSprites.GetItemSpriteFor(itemData));
                    actor.PlayCombatAnimation(userTrigger);
                }
                GameAudioManager.Instance?.PlayItemSfxFor(itemData.AnimTrigger, itemData);
                _services.ContactCue("gesture");
                yield return _waitFeedTransfer;

                if (contactAllowed && target != null)
                {
                    // Receiving food never replays the giver's two-hand feed gesture.
                    if (localTarget) fps?.PlayFPSItemAnimation("use", itemData);
                    else
                    {
                        target.PlayCombatAnimation("feed");
                        var sprite = GameSprites.GetItemSpriteFor(itemData);
                        if (sprite != null)
                        {
                            food = _resources.Own(new GameObject("FeedSprite"));
                            var renderer = food.AddComponent<SpriteRenderer>();
                            renderer.sprite = sprite;
                            renderer.sortingOrder = 90;
                            food.transform.position = ContactPoint(target, _services.Item.TargetSeat, true);
                            food.transform.localScale = Vector3.one * 0.8f;
                        }
                    }
                }
                onImpact?.Invoke();
                _services.ContactCue("impact");
                float duration = ContactPresentationTiming.FeedDuration(itemData.AnimDuration,
                    fps != null ? fps.GetClipDuration("feed", 1.1667f) : 1.1667f,
                    fps != null ? fps.GetClipDuration("use", 1.35f) : 1.35f);
                yield return new WaitForSeconds(duration - ContactPresentationTiming.FeedTransfer);
            }
            finally
            {
                if (food != null) Destroy(food);
                fpsLease?.Dispose();
                EndContact(actor, target, localActor || localTarget);
            }
            _services.ContactCue("end");
        }

        public IEnumerator PlayHugSequence(int userIdx, int targetIdx,
            AZPlayerVisual userVisual, AZPlayerVisual targetVisual, ItemDataSO itemData,
            bool isLocalUser, System.Action onImpact, bool contactAllowed)
        {
            var cam = isLocalUser ? Context.Camera : null;
            var mover = isLocalUser ? (cam != null ? cam.transform : null)
                : (userVisual != null ? userVisual.GetVisualRoot() : null);
            var fps = Context.Fps;
            var hands = fps != null && (isLocalUser || (targetVisual != null && targetVisual.IsLocalHuman))
                ? fps.transform : null;
            var startPosition = mover != null ? mover.position : Vector3.zero;
            var startRotation = mover != null ? mover.rotation : Quaternion.identity;
            var targetPoint = ContactPoint(targetVisual, targetIdx);
            var approachPosition = isLocalUser ? Vector3.Lerp(startPosition, targetPoint, 0.4f)
                : RemoteHugPosition(userVisual, targetVisual, targetIdx, startPosition);
            var approachRotation = startRotation;
            float hopHeight = !isLocalUser && TryHeadBounds(userVisual, out var movingHead)
                ? movingHead.size.y * 0.16f : 0f;
            if (isLocalUser && (targetPoint - approachPosition).sqrMagnitude > 0.0001f)
                approachRotation = Quaternion.LookRotation(targetPoint - approachPosition, Vector3.up);
            var handsPose = fps != null ? fps.GetContactCameraPose() : new Pose(Vector3.zero, Quaternion.identity);
            var relativeHandsPosition = handsPose.position;
            var relativeHandsRotation = handsPose.rotation;

            _services.ContactCue("begin");
            // Capture hands first: LIFO disposal restores the camera before a camera-child FPS.
            var handsLease = _services.CaptureTransform(hands);
            var fan = !isLocalUser && mover != null ? mover.Find("fan") ?? mover.Find("Fan") : null;
            var fanPosition = fan != null ? fan.position : Vector3.zero;
            var fanLease = _services.CaptureTransform(fan);
            var moverLease = _services.CaptureTransform(mover);
            PresentationSortingLease foreground = null;
            PresentationSortingLease handsForeground = null;
            if (!isLocalUser && mover != null && targetVisual != null && targetVisual.IsLocalHuman)
            {
                // The approaching hands must pass in front of the local icebox.
                foreground = new PresentationSortingLease(mover, 80);
                _resources.OnRelease(foreground.Dispose);
                if (hands != null)
                {
                    handsForeground = new PresentationSortingLease(hands, 90);
                    _resources.OnRelease(handsForeground.Dispose);
                }
            }
            void KeepFanAtSeat() { if (fan != null) fan.position = fanPosition; }
            try
            {
                if (!isLocalUser) FollowHands(Context.Camera, hands, relativeHandsPosition, relativeHandsRotation);
                if (!isLocalUser && userVisual != null)
                {
                    // The authored jump separates the torso from the seated legs.
                    // Move the assembled idle silhouette instead of stretching its parts.
                    userVisual.ReturnToIdle();
                    userVisual.GetAnimator()?.Update(0f);
                }
                float started = Time.time;
                float elapsed;
                while ((elapsed = Time.time - started) < ContactPresentationTiming.HugApproach)
                {
                    float move = isLocalUser ? elapsed / ContactPresentationTiming.HugApproach
                        : (elapsed - 0.15f) / (ContactPresentationTiming.HugApproach - 0.15f);
                    SetContactPose(mover, startPosition, approachPosition, startRotation, approachRotation, move,
                        cam, hands, relativeHandsPosition, relativeHandsRotation, hopHeight);
                    KeepFanAtSeat();
                    yield return null;
                }
                SetContactPose(mover, startPosition, approachPosition, startRotation, approachRotation, 1f,
                    cam, hands, relativeHandsPosition, relativeHandsRotation);
                KeepFanAtSeat();
                if (contactAllowed)
                {
                    if (isLocalUser) fps?.PlayFPSItemAnimation("hug", itemData);
                    else if (userVisual != null)
                        StartRemoteHug(userVisual);
                    GameAudioManager.Instance?.PlayItemSfxFor(itemData.AnimTrigger, itemData);
                }
                _services.ContactCue("gesture");
                float gesture = Mathf.Max(ContactPresentationTiming.HugImpact,
                    fps != null ? fps.GetClipDuration("hug", 1.0167f) : 1.0167f);
                started = Time.time;
                bool impacted = false;
                while (true)
                {
                    elapsed = Time.time - started;
                    if (!impacted && elapsed >= ContactPresentationTiming.HugImpact)
                    {
                        impacted = true;
                        onImpact?.Invoke();
                        _services.ContactCue("impact");
                    }
                    FollowHands(cam, hands, relativeHandsPosition, relativeHandsRotation);
                    KeepFanAtSeat();
                    if (elapsed >= gesture) break;
                    yield return null;
                }
                // Keep the entire retract tail before returning, on every viewing peer.
                if (!isLocalUser && userVisual != null)
                {
                    userVisual.ReturnToIdle();
                    userVisual.GetAnimator()?.Update(0f);
                }
                started = Time.time;
                while ((elapsed = Time.time - started) < ContactPresentationTiming.HugReturn)
                {
                    SetContactPose(mover, approachPosition, startPosition, approachRotation, startRotation,
                        isLocalUser ? elapsed / ContactPresentationTiming.HugReturn
                            : (elapsed - 0.15f) / (ContactPresentationTiming.HugReturn - 0.15f),
                        cam, hands, relativeHandsPosition, relativeHandsRotation, hopHeight);
                    KeepFanAtSeat();
                    yield return null;
                }
                SetContactPose(mover, approachPosition, startPosition, approachRotation, startRotation, 1f,
                    cam, hands, relativeHandsPosition, relativeHandsRotation);
                KeepFanAtSeat();
                float remaining = ContactPresentationTiming.HugDuration(itemData.AnimDuration, gesture)
                    - (ContactPresentationTiming.HugApproach + gesture + ContactPresentationTiming.HugReturn);
                if (remaining > 0f) yield return new WaitForSeconds(remaining);
            }
            finally
            {
                handsForeground?.Dispose();
                foreground?.Dispose();
                moverLease?.Dispose();
                fanLease?.Dispose();
                handsLease?.Dispose();
                EndContact(userVisual, targetVisual, isLocalUser || (targetVisual != null && targetVisual.IsLocalHuman));
            }
            _services.ContactCue("end");
        }

        void EndContact(AZPlayerVisual actor, AZPlayerVisual target, bool localInvolved)
        {
            // Forced settlement owns its cleanup; an old coroutine cannot clear a newer gesture.
            if (!_services.Item.Execution.CanContinue) return;
            if (actor != null) actor.ReturnToIdle();
            if (target != null) target.ReturnToIdle();
            if (localInvolved) Context.Fps?.ReturnToIdle();
        }

        Vector3 ContactPoint(AZPlayerVisual visual, int seat, bool atMouth = false)
        {
            if (TryHeadBounds(visual, out var bounds))
                return bounds.center - (atMouth ? Vector3.up * bounds.size.y * 0.35f : Vector3.zero);
            return GetPlayerWorldPos(seat) + Vector3.up * 0.5f;
        }

        internal static void StartRemoteHug(AZPlayerVisual visual)
        {
            visual.ReturnToIdle();
            visual.GetAnimator()?.Update(0f);
            // Idle has no end transition. Do not carry its unused end trigger
            // into the new hug and immediately exit that state.
            visual.GetAnimator()?.ResetTrigger("end");
            visual.PlayCombatAnimation("hug");
        }

        Vector3 RemoteHugPosition(AZPlayerVisual actor, AZPlayerVisual target, int targetSeat, Vector3 start)
        {
            var camera = Context.Camera;
            var destination = GetPlayerWorldPos(targetSeat);
            if (camera == null) return Vector3.Lerp(start, destination, 0.65f);
            bool hasActorHead = TryHeadBounds(actor, out var actorHead);
            if (target != null && target.IsLocalHuman)
            {
                // The hidden local NetworkObject is not a camera-space seat.
                // Frame the arriving face, keeping its body inside the receiving view.
                float headHeight = hasActorHead ? actorHead.size.y : 1f;
                float depth = Mathf.Max(camera.nearClipPlane + 1f,
                    headHeight / (2f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 0.24f));
                var face = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.6f, depth));
                return face - ((hasActorHead ? actorHead.center : start + Vector3.up * 0.5f) - start);
            }
            // Observers must see both participants. Stop beside the recipient,
            // on the incoming side, instead of merging two sprites at one point.
            float width = hasActorHead ? actorHead.size.x : 1f;
            if (TryHeadBounds(target, out var targetHead)) width = Mathf.Max(width, targetHead.size.x);
            float side = Vector3.Dot(start - destination, camera.transform.right) < 0f ? -1f : 1f;
            return destination + camera.transform.right * (side * width * 0.9f) - camera.transform.forward * 0.15f;
        }

        static bool TryHeadBounds(AZPlayerVisual visual, out Bounds bounds)
        {
            var root = visual != null ? visual.GetVisualRoot() : null;
            if (root != null)
                foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>())
                    if (renderer.sprite != null && string.Equals(renderer.name, "head", System.StringComparison.OrdinalIgnoreCase))
                    { bounds = renderer.bounds; return true; }
            bounds = default;
            return false;
        }

        static void SetContactPose(Transform mover, Vector3 from, Vector3 to, Quaternion fromRotation,
            Quaternion toRotation, float progress, Camera cam, Transform hands, Vector3 handPosition, Quaternion handRotation,
            float hopHeight = 0f)
        {
            float smooth = Mathf.SmoothStep(0f, 1f, progress);
            if (mover != null)
                mover.SetPositionAndRotation(Vector3.Lerp(from, to, smooth) + Vector3.up * (4f * smooth * (1f - smooth) * hopHeight),
                    Quaternion.Slerp(fromRotation, toRotation, smooth));
            FollowHands(cam, hands, handPosition, handRotation);
        }

        static void FollowHands(Camera cam, Transform hands, Vector3 position, Quaternion rotation)
        {
            if (cam != null && hands != null)
                hands.SetPositionAndRotation(cam.transform.TransformPoint(position), cam.transform.rotation * rotation);
        }

        public IEnumerator PlayBuldakSfx(bool isLocalUser, ItemDataSO itemData)
        {
            yield return _waitBuldak07;
            GameAudioManager.Instance?.PlayItemSfxFor("eat", itemData);
            if (isLocalUser)
            {
                yield return _waitBuldak02;
                GameAudioManager.Instance?.PlayItemSfxFor("eat", itemData);
                yield return _waitBuldak02;
                GameAudioManager.Instance?.PlayItemSfxFor("eat", itemData);
            }
        }

        public void TintTargetFanBlue(AZPlayerVisual targetVisual)
        {
            var root = targetVisual.GetVisualRoot();
            if (root == null) return;

            var fan = root.Find("fan");
            if (fan == null) fan = root.Find("Fan");
            if (fan == null)
            {
                Debug.LogWarning("[CombatVFX] TintTargetFanBlue: fan child not found");
                return;
            }

            var sr = fan.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(0.4f, 0.6f, 1f);
                Debug.Log("[CombatVFX] TintTargetFanBlue: fan color set to blue");
            }
        }

    }
}
