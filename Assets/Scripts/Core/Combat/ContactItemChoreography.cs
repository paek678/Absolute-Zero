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
        static readonly WaitForSeconds _waitFeedHalf = new(0.5f);
        static readonly WaitForSeconds _waitBuldak07 = new(0.7f);
        static readonly WaitForSeconds _waitBuldak02 = new(0.2f);
        static readonly WaitForSeconds _waitHug03 = new(0.3f);
        static readonly WaitForSeconds _waitHug08 = new(0.8f);

        public IEnumerator PlayFeedReaction(AZPlayerVisual targetVisual, int targetIdx, ItemDataSO itemData,
            System.Action onImpact = null)
        {
            // The viewed participant chooses FPS versus character animation;
            // the optional impact callback only controls result presentation timing.
            if (targetVisual.IsLocalHuman)
                Context.Fps?.PlayFPSItemAnimation("feed", itemData);
            else
                targetVisual.PlayCombatAnimation("feed");

            var sprite = GameSprites.GetItemSpriteFor(itemData);
            GameObject feedSpriteGO = null;
            if (sprite != null)
            {
                feedSpriteGO = _resources.Own(new GameObject("FeedSprite"));
                var sr = feedSpriteGO.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 90;
                feedSpriteGO.transform.position = GetPlayerWorldPos(targetIdx) + new Vector3(-0.05f, 0.5f, 0f);
                feedSpriteGO.transform.localScale = Vector3.one * 0.8f;
            }

            yield return _waitFeedHalf;
            onImpact?.Invoke();
            yield return _waitFeedHalf;

            if (feedSpriteGO != null) Destroy(feedSpriteGO);
            targetVisual.ReturnToIdle();
            if (targetVisual.IsLocalHuman)
                Context.Fps?.ReturnToIdle();
        }

        public IEnumerator PlayHugSequence(int userIdx, int targetIdx,
            AZPlayerVisual userVisual, AZPlayerVisual targetVisual, ItemDataSO itemData,
            bool isLocalUser, System.Action onImpact = null,
            bool contactAllowed = true)
        {
            if (isLocalUser)
            {
                var views = Context.Views;
                var cam = Context.Camera;
                if (cam != null)
                {
                    var startPos = cam.transform.position;
                    var cameraLease = _services.CaptureTransform(cam.transform);
                    var targetPos = GetPlayerWorldPos(targetIdx);
                    var approachPos = Vector3.Lerp(startPos, targetPos, 0.4f);

                    float t = 0f;
                    while (t < 0.5f)
                    {
                        t += Time.deltaTime;
                        cam.transform.position = Vector3.Lerp(startPos, approachPos, Mathf.SmoothStep(0f, 1f, t / 0.5f));
                        yield return null;
                    }

                    if (onImpact != null && contactAllowed)
                        Context.Fps?.PlayFPSItemAnimation("hug", itemData);
                    onImpact?.Invoke();
                    yield return _waitHug03;

                    t = 0f;
                    while (t < 1f)
                    {
                        t += Time.deltaTime;
                        cam.transform.position = Vector3.Lerp(approachPos, startPos, Mathf.SmoothStep(0f, 1f, t / 1f));
                        yield return null;
                    }
                    cam.transform.position = startPos;
                    cameraLease?.Dispose();
                }
                else onImpact?.Invoke();
            }

            // A remote actor approaches its target. A local actor already used
            // the camera branch and must never move its hidden network object.
            if (!isLocalUser && userVisual != null)
            {
                var userTf = userVisual.GetVisualRoot() ?? userVisual.transform;
                var userStartPos = userTf.position;
                var visualLease = _services.CaptureTransform(userTf);
                var targetPos = GetPlayerWorldPos(targetIdx);

                userVisual.PlayCombatAnimation("jump");
                yield return _waitHug03;

                float moveDur = 0.4f;
                float t = 0f;
                while (t < moveDur)
                {
                    t += Time.deltaTime;
                    userTf.position = Vector3.Lerp(userStartPos, targetPos, Mathf.SmoothStep(0f, 1f, t / moveDur));
                    yield return null;
                }

                if (contactAllowed) userVisual.PlayCombatAnimation("hug");
                onImpact?.Invoke();
                yield return _waitHug08;

                t = 0f;
                while (t < 0.5f)
                {
                    t += Time.deltaTime;
                    userTf.position = Vector3.Lerp(targetPos, userStartPos, Mathf.SmoothStep(0f, 1f, t / 0.5f));
                    yield return null;
                }
                userTf.position = userStartPos;
                visualLease?.Dispose();
                userVisual.ReturnToIdle();
            }
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
