using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 물총 미니게임 (HitTargets) — 불규칙하게 움직이는 과녁 Goal개(3)를 제한시간(5초) 안에 전부 탭.
    /// 과녁 = 동심원, 명중 순간 초록 플래시 + 확인 팝 후 소멸 (빗나간 탭 페널티 없음). 배너: "쏘아라!"
    /// </summary>
    public class WaterGunMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "쏘아라!";

        static readonly Vector2 AreaHalf = new(380f, 210f);
        static readonly Color TargetRed = new(0.85f, 0.2f, 0.15f);
        static readonly Color HitGreen = new(0.35f, 0.95f, 0.45f);

        class MovingTarget
        {
            public RectTransform Rect;
            public Vector2 Velocity;
            public bool Hit;
            public Image Outer;
            public Image Core;
        }

        readonly List<MovingTarget> _targets = new();
        TextMeshProUGUI _countText;
        int _hits;

        protected override void BuildContent(RectTransform content)
        {
            for (int i = 0; i < Goal; i++)
                CreateTarget(content, i);

            _countText = CreateText(content, "Count", new Vector2(0f, -270f), new Vector2(240f, 40f), $"0 / {Goal}", 30);
        }

        void CreateTarget(RectTransform content, int index)
        {
            // 과녁: 빨강-흰-빨강 동심원
            var outer = CreateCircle(content, $"Target{index}",
                new Vector2(Random.Range(-AreaHalf.x, AreaHalf.x), Random.Range(-AreaHalf.y, AreaHalf.y)),
                100f, TargetRed);
            CreateCircle(outer.transform, "Ring", Vector2.zero, 64f, Color.white).raycastTarget = false;
            var core = CreateCircle(outer.transform, "Core", Vector2.zero, 30f, TargetRed);
            core.raycastTarget = false;

            var target = new MovingTarget
            {
                Rect = outer.rectTransform,
                Velocity = Random.insideUnitCircle.normalized * Random.Range(240f, 330f),
                Outer = outer,
                Core = core,
            };

            // 빠른 과녁은 press-release 클릭이 잘 안 잡힘 → 포인터 다운 즉시 명중 처리
            var relay = outer.gameObject.AddComponent<HitRelay>();
            relay.Owner = this;
            relay.Target = target;

            _targets.Add(target);
        }

        protected override void OnTick(float dt)
        {
            foreach (var t in _targets)
            {
                if (t.Hit) continue;

                var pos = t.Rect.anchoredPosition + t.Velocity * dt;
                if (Mathf.Abs(pos.x) > AreaHalf.x)
                {
                    t.Velocity.x = -t.Velocity.x;
                    pos.x = Mathf.Clamp(pos.x, -AreaHalf.x, AreaHalf.x);
                }
                if (Mathf.Abs(pos.y) > AreaHalf.y)
                {
                    t.Velocity.y = -t.Velocity.y;
                    pos.y = Mathf.Clamp(pos.y, -AreaHalf.y, AreaHalf.y);
                }
                t.Rect.anchoredPosition = pos;
            }
        }

        void OnTargetHit(MovingTarget target)
        {
            if (target.Hit || Finished) return;
            target.Hit = true;

            _hits++;
            _countText.text = $"{_hits} / {Goal}";

            // 명중 순간 피드백: 초록 플래시 + 확인 팝 → 소멸
            PlayConfirmPop(target.Rect.anchoredPosition, 150f);
            StartCoroutine(HitFlashRoutine(target));

            if (_hits >= Goal)
                Finish(true);
        }

        IEnumerator HitFlashRoutine(MovingTarget target)
        {
            target.Outer.raycastTarget = false;
            target.Outer.color = HitGreen;
            target.Core.color = HitGreen;

            const float dur = 0.16f;
            float t = 0f;
            while (t < dur && target.Rect != null)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                target.Rect.localScale = Vector3.one * (1f + 0.3f * p);
                yield return null;
            }
            if (target.Rect != null) target.Rect.gameObject.SetActive(false);
        }

        class HitRelay : MonoBehaviour, IPointerDownHandler
        {
            public WaterGunMiniGameUI Owner;
            public MovingTarget Target;
            public void OnPointerDown(PointerEventData eventData) => Owner?.OnTargetHit(Target);
        }
    }
}
