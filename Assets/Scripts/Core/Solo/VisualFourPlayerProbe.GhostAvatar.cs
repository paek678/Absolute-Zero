#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Player;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    public sealed partial class VisualFourPlayerProbe
    {
        IEnumerator CheckGhostGrowth(int seat)
        {
            if (_localSeat == seat) yield break;
            float deadline = Time.unscaledTime + 8f;
            Transform root = null;
            while (Time.unscaledTime < deadline)
            {
                var marker = FindObjectsByType<PlayerSeatMarker>(FindObjectsSortMode.None)
                    .SingleOrDefault(m => m.Player != null && m.SeatIndex == seat);
                root = marker != null ? marker.transform.Find("GhostPlaceholder") : null;
                if (root != null && root.gameObject.activeInHierarchy) break;
                yield return null;
            }
            if (root == null || !root.gameObject.activeInHierarchy)
            { Fail("Ghost growth never appeared"); yield break; }
            var sprite = root.GetComponentInChildren<SpriteRenderer>();
            var animator = root.GetComponent<Animator>();
            float start = Time.unscaledTime, initial = root.localScale.x;
            float previous = initial;
            float bottom = root.localPosition.y + sprite.sprite.bounds.min.y * initial;
            int samples = 0, captures = 0;
            Debug.Log($"[GHOST_GROW] START local={_localSeat} actor={seat} time={start:F3} scale={initial:F3}");
            while (Time.unscaledTime - start < 1.1f)
            {
                yield return new WaitForEndOfFrame();
                if (root == null || !root.gameObject.activeInHierarchy)
                { Fail("Ghost growth vanished"); yield break; }
                Vector3 scale = root.localScale;
                if (Mathf.Abs(scale.x - scale.y) > .0001f || scale.x < previous - .001f
                    || scale.x > .781f)
                { Fail("Ghost growth distorted, shrank or overshot"); yield break; }
                if (scale.x < .779f)
                {
                    float currentBottom = root.localPosition.y + sprite.sprite.bounds.min.y * scale.y;
                    if (Mathf.Abs(currentBottom - bottom) > .003f
                        || Vector3.Distance(sprite.transform.localScale, Vector3.one) > .001f)
                    { Fail("Ghost growth bottom drifted or artwork deformed"); yield break; }
                    samples++;
                    if (captures < 3 && scale.x >= new[] { .12f, .36f, .65f }[captures])
                        Capture($"ghost-grow-{++captures}");
                }
                previous = scale.x;
            }
            if (initial > .2f || samples < 3 || Mathf.Abs(root.localScale.x - .78f) > .001f
                || Vector3.Distance(root.localPosition, new Vector3(0f, .25f, -.08f)) > .001f
                || animator.speed != 1f || !animator.GetCurrentAnimatorStateInfo(0).IsName("Ghost_Idle"))
            { Fail("Ghost growth did not start small and finish in full-size idle"); yield break; }
            Capture("ghost-grow-complete");
            Debug.Log($"[GHOST_GROW] PASS local={_localSeat} actor={seat} samples={samples} bottomFixed=true ratioPreserved=true");
        }

        IEnumerator CheckGhostAvatar(byte seat, byte skill)
        {
            if (_localSeat == seat)
            {
                Debug.Log($"[GHOST_ART] LOCAL_FPS seat={seat} skill={skill}");
                yield break;
            }
            yield return null; // Sequenced event is dispatched after the legacy VFX event.
            var marker = FindObjectsByType<PlayerSeatMarker>(FindObjectsSortMode.None)
                .SingleOrDefault(m => m.Player != null && m.SeatIndex == seat);
            var root = marker != null ? marker.transform.Find("GhostPlaceholder") : null;
            var animator = root != null ? root.GetComponent<Animator>() : null;
            var sprite = root != null ? root.GetComponentInChildren<SpriteRenderer>() : null;
            if (root == null || !root.gameObject.activeInHierarchy || animator == null || sprite == null
                || animator.runtimeAnimatorController?.name != "GhostA")
            { Fail("Ghost avatar missing or wrong controller"); yield break; }
            var seen = new HashSet<string>();
            float end = Time.unscaledTime + .8f;
            bool attack = false;
            while (Time.unscaledTime < end)
            {
                if (sprite == null || sprite.sprite == null || sprite.sprite.texture.name != "ghost"
                    || !sprite.enabled || sprite.color.a < .99f)
                { Fail("Ghost art missing, wrong texture or hidden"); yield break; }
                attack |= animator.GetCurrentAnimatorStateInfo(0).IsName("Ghost_Attack");
                seen.Add(sprite.sprite.name);
                yield return null;
            }
            if (!attack || seen.Count < 2 || !animator.GetCurrentAnimatorStateInfo(0).IsName("Ghost_Idle"))
            { Fail("Ghost attack did not animate the authored sprites and return to idle"); yield break; }
            yield return new WaitForSecondsRealtime(1.05f);
            if (animator == null || !animator.GetCurrentAnimatorStateInfo(0).IsName("Ghost_Idle")
                || !animator.GetCurrentAnimatorStateInfo(0).loop)
            { Fail("Ghost idle did not keep looping"); yield break; }
            Capture($"ghost-art-{seat}-{skill}-idle");
            Debug.Log($"[GHOST_ART] PASS local={_localSeat} actor={seat} skill={skill} sprites={string.Join(",",seen)} idleLoop=true");
        }
    }
}
#endif
