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
    internal sealed class CatItemChoreography
    {
        readonly ItemPresentationServices _services;
        public CatItemChoreography(ItemPresentationServices services) => _services = services;
        CombatPresentationContext Context => _services.Context;
        PresentationResources _resources => _services.Resources;
        Coroutine Run(IEnumerator routine) => _services.Run(routine);
        AZPlayerVisual GetPlayerVisual(int seat, NetworkManager _) => Context.GetVisual(seat);
        Vector3 GetPlayerWorldPos(int seat) => Context.GetWorldPosition(seat);
        static void Destroy(Object value) => PresentationResources.DestroyOwned(value);
        static readonly WaitForSeconds _waitCatWake = new(0.4f);
        static readonly WaitForSeconds _waitCatReady = new(0.3f);

        public IEnumerator PlayCatSpriteSequence(int userIdx, int targetIdx, bool isLocalUser, ItemDataSO itemData, float minDuration = 1.5f,
            System.Action onImpact = null)
        {
            float startTime = Time.time;
            Debug.Log($"[CombatVFX] Cat sequence START — user=P{userIdx} target=P{targetIdx} isLocal={isLocalUser} minDur={minDuration}s");

            var nm = Context.Network;
            var userVisual = nm != null ? GetPlayerVisual(userIdx, nm) : null;

            GameAudioManager.Instance?.PlayItemSfxFor("", itemData);

            var views = Context.Views;
            bool hasSweep = Context.TryGetItemSweep(targetIdx, out var sweepStart, out var sweepEnd);
            var spSleep = views != null ? views.GetSprite(MatchSpriteRole.CatSleep) : Resources.Load<Sprite>("Cat/cat_sleep");
            var spWakeup = views != null ? views.GetSprite(MatchSpriteRole.CatWakeup) : Resources.Load<Sprite>("Cat/cat_wakeup");
            var spJump = views != null ? views.GetSprite(isLocalUser ? MatchSpriteRole.CatJump : MatchSpriteRole.CatJump2) : Resources.Load<Sprite>(isLocalUser ? "Cat/cat_jump" : "Cat/cat_jump2");
            var spRummage = views != null ? views.GetSprite(MatchSpriteRole.CatRummage) : Resources.Load<Sprite>("Cat/cat_rummage");

            if (spSleep == null)
            {
                Debug.LogWarning("[CombatVFX] Cat sprites not found — waiting minDuration");
                yield return new WaitForSeconds(minDuration);
                onImpact?.Invoke();
                yield break;
            }

            Transform catItemTransform = isLocalUser && onImpact == null ? FindCatItemView(itemData) : null;
            SpriteRenderer sr;
            GameObject go;
            Vector3 originalScale;
            BorrowedItemPresentation borrowed = null;

            if (catItemTransform != null)
            {
                go = catItemTransform.gameObject;
                var cardChild = catItemTransform.Find("Card");
                sr = cardChild != null ? cardChild.GetComponent<SpriteRenderer>() : catItemTransform.GetComponentInChildren<SpriteRenderer>();
                if (sr == null)
                {
                    Debug.LogWarning("[CombatVFX] Cat item SpriteRenderer not found — waiting minDuration");
                    yield return new WaitForSeconds(minDuration);
                    onImpact?.Invoke();
                    yield break;
                }
                originalScale = go.transform.localScale;

                borrowed = new BorrowedItemPresentation(go, sr, _resources);

                sr.sprite = spSleep;
                WorldInteractionLayer.Apply(sr);

                var hover = go.GetComponent<HoverEffect>();
                if (hover != null) hover.enabled = false;
                var col = go.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                var label = catItemTransform.Find("Label");
                if (label != null) label.gameObject.SetActive(false);
                var banned = catItemTransform.Find("BannedOverlay");
                if (banned != null) banned.gameObject.SetActive(false);
                var outline = catItemTransform.Find("HoverOutline");
                if (outline == null)
                {
                    var cardOutline = cardChild != null ? cardChild.Find("HoverOutline") : null;
                    if (cardOutline != null) outline = cardOutline;
                }
                if (outline != null) outline.gameObject.SetActive(false);
            }
            else
            {
                go = _resources.Own(new GameObject("CatAnim"));
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = spSleep;
                WorldInteractionLayer.Apply(sr);
                Vector3 userPos = GetPlayerWorldPos(userIdx);
                go.transform.position = Context.TryGetItemSweep(userIdx, out var sourceFirst, out var sourceLast)
                    ? (sourceFirst + sourceLast) * .5f : userPos;
                float size = Mathf.Max(spSleep.bounds.size.x, spSleep.bounds.size.y);
                originalScale = Vector3.one * (size > 0f ? .95f / size : .7f);
                go.transform.localScale = originalScale;
                if (onImpact != null)
                    go.transform.position += Vector3.up * (Mathf.Max(0f, -spSleep.bounds.min.y * originalScale.y) + .10f);
            }
            DuelSpriteFacing.Register(go);

            if (userVisual != null && !userVisual.IsLocalHuman)
                userVisual.PlayCombatAnimation("jump");

            yield return _waitCatReady;

            sr.sprite = spWakeup;
            yield return _waitCatWake;

            sr.sprite = spJump;

            Vector3 destPos = hasSweep ? (sweepStart + sweepEnd) * .5f : GetPlayerWorldPos(targetIdx);
            Vector3 sweep = hasSweep ? (sweepEnd - sweepStart) * .4f : Vector3.zero;

            // Multi's painted slot planes sit at the anchors' height. Stand the cat
            // on that plane instead of putting its centre through the painted cells.
            if (onImpact != null && hasSweep && spRummage != null)
                destPos.y += Mathf.Max(0f, -spRummage.bounds.min.y * (originalScale.x + .05f)) + .10f;

            Vector3 arcStart = go.transform.position;
            bool flipX = destPos.x < arcStart.x;
            float baseScale = originalScale.x;
            go.transform.localScale = new Vector3(flipX ? -baseScale : baseScale, baseScale, originalScale.z);

            float arcDur = 0.8f;
            float arcHeight = .65f;
            float t = 0f;
            while (t < arcDur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / arcDur);
                Vector3 linear = Vector3.Lerp(arcStart, destPos, p);
                float yOffset = arcHeight * 4f * p * (1f - p);
                go.transform.position = linear + new Vector3(0f, yOffset, 0f);
                yield return null;
            }
            go.transform.position = destPos;
            onImpact?.Invoke();

            if (catItemTransform != null)
            {
                var tempGO = _resources.Own(new GameObject("CatAnimTemp"));
                var tempSR = tempGO.AddComponent<SpriteRenderer>();
                tempSR.sprite = sr.sprite;
                tempSR.sortingOrder = sr.sortingOrder;
                tempSR.sharedMaterial = sr.sharedMaterial;
                tempGO.transform.position = go.transform.position;
                tempGO.transform.localScale = go.transform.localScale;
                DuelSpriteFacing.Register(tempGO);
                go = tempGO;
                sr = tempSR;
                catItemTransform = null;
            }

            if (onImpact == null)
            {
                borrowed?.Dispose();
                _resources.ReleaseInventory();
            }

            sr.sprite = spRummage;
            _services.ItemCue("cat-rummage");

            float rumbleDur = minDuration;
            float rumbleSpeed = 7f;
            t = 0f;
            while (t < rumbleDur)
            {
                t += Time.deltaTime;
                go.transform.position = destPos + sweep * Mathf.Sin(t * rumbleSpeed);

                float s = baseScale + Mathf.Sin(t * rumbleSpeed * 2f) * 0.05f;
                float screenDirection = Context.Camera != null ? Vector3.Dot(sweep, Context.Camera.transform.right) : sweep.x;
                float dir = Mathf.Cos(t * rumbleSpeed) * screenDirection >= 0f ? 1f : -1f;
                go.transform.localScale = new Vector3(dir * s, s, originalScale.z);
                yield return null;
            }

            float exitDur = 0.5f;
            Vector3 exitStart = go.transform.position;
            Vector3 exitEnd = exitStart + new Vector3(6f, 3f, 0f);
            t = 0f;
            while (t < exitDur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / exitDur);
                go.transform.position = Vector3.Lerp(exitStart, exitEnd, p);

                float yArc = Mathf.Sin(p * Mathf.PI) * 1.5f;
                go.transform.position += new Vector3(0f, yArc, 0f);

                sr.color = new Color(1f, 1f, 1f, 1f - p);
                yield return null;
            }

            if (catItemTransform == null)
            {
                Destroy(go);
            }
            else
            {
                borrowed?.Dispose();
                go.SetActive(false);
            }

            if (userVisual != null)
                userVisual.ReturnToIdle();

            float elapsed = Time.time - startTime;
            Debug.Log($"[CombatVFX] Cat sequence END — elapsed={elapsed:F2}s");
        }

        Transform FindCatItemView(ItemDataSO itemData)
        {
            return Context.Inventory != null ? Context.Inventory.FindLocalView(itemData) : null;
        }

    }
}
