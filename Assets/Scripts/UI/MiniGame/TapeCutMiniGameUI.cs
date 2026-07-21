using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 청테이프 미니게임 (TimingCut) — 반응속도. 롤(왼쪽)에서 테이프가 왼쪽→오른쪽으로 풀려나가고,
    /// 파란 테이프 표면(눈금)이 흐르며 초록 밴드가 연속으로 지나간다. 초록이 커트 라인에 왔을 때 탭 (5초).
    /// 초록에서 탭 = 성공, 파란 부분에서 탭 = 실패, 시간 초과 = 실패. (초록 놓쳐도 다음 초록이 옴)
    /// 배너: "끊어라!"
    /// </summary>
    public class TapeCutMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "끊어라!";

        const float ScrollSpeed = 150f;     // px/s (표면·초록이 오른쪽으로 흐름)
        const float GreenHalf = 48f;        // 초록 밴드 반폭 = 판정 반폭
        const float Period = 260f;          // 초록 밴드 간격
        const int BandCount = 4;
        const int TickCount = 6;
        const float TickGap = 120f;
        const float TapeY = 20f;

        // 마스크 컨테이너 (롤 오른쪽만 노출) — content 로컬
        const float ContainerCenterX = 130f;
        const float ContainerHalfW = 360f;  // 로컬 x 범위 [-360, 360]
        const float JudgeContentX = -70f;   // 커트 라인 (롤 출구 근처)
        float JudgeLocalX => JudgeContentX - ContainerCenterX;   // = -200

        // 시작 연출: 테이프 끝단이 롤에서 오른쪽으로 쭉 뽑혀나감
        const float TipRevealSpeed = 820f;

        static readonly Color TapeBlue = new(0.35f, 0.55f, 0.85f);
        static readonly Color TapeGreen = new(0.25f, 0.8f, 0.35f);
        static readonly Color RollColor = new(0.28f, 0.48f, 0.78f);

        bool _resolved;
        float _rollAngle;
        float _tipX;
        bool _tipActive = true;
        RectTransform _rollRect;
        RectTransform _tip;
        readonly List<RectTransform> _bands = new();
        readonly List<RectTransform> _ticks = new();

        protected override void BuildContent(RectTransform content)
        {
            // ── 마스크 컨테이너 (롤 오른쪽 영역만 노출) ──
            var containerGO = new GameObject("TapeArea");
            containerGO.transform.SetParent(content, false);
            var containerRect = containerGO.AddComponent<RectTransform>();
            containerRect.anchoredPosition = new Vector2(ContainerCenterX, TapeY);
            containerRect.sizeDelta = new Vector2(ContainerHalfW * 2f, 74f);
            containerGO.AddComponent<RectMask2D>();

            // 파란 테이프 바탕 (컨테이너 가득)
            var baseGO = new GameObject("TapeBase");
            baseGO.transform.SetParent(containerRect, false);
            var baseRect = baseGO.AddComponent<RectTransform>();
            baseRect.anchorMin = Vector2.zero;
            baseRect.anchorMax = Vector2.one;
            baseRect.offsetMin = Vector2.zero;
            baseRect.offsetMax = Vector2.zero;
            var baseImg = baseGO.AddComponent<Image>();
            baseImg.color = TapeBlue;
            baseImg.raycastTarget = false;

            // 표면 눈금 (오른쪽으로 흐르며 테이프 이동감 표현)
            for (int i = 0; i < TickCount; i++)
            {
                float localX = -ContainerHalfW + i * TickGap;
                var tick = CreatePanel(containerRect, $"Tick{i}", new Vector2(localX, 0f),
                    new Vector2(5f, 64f), new Color(1f, 1f, 1f, 0.12f));
                tick.raycastTarget = false;
                _ticks.Add(tick.rectTransform);
            }

            // 초록 밴드들 (주기적으로 배치 → 오른쪽으로 흐르며 루프)
            for (int i = 0; i < BandCount; i++)
            {
                float localX = -ContainerHalfW + i * Period;
                var band = CreatePanel(containerRect, $"Green{i}", new Vector2(localX, 0f),
                    new Vector2(GreenHalf * 2f, 74f), TapeGreen);
                band.raycastTarget = false;
                _bands.Add(band.rectTransform);
            }

            // 시작 연출용 테이프 끝단 (밝은 세로 띠 — 롤에서 오른쪽으로 뽑혀나감)
            _tipX = -ContainerHalfW;
            var tip = CreatePanel(containerRect, "TapeTip", new Vector2(_tipX, 0f),
                new Vector2(10f, 74f), new Color(0.85f, 0.95f, 1f));
            tip.raycastTarget = false;
            _tip = tip.rectTransform;

            // ── 롤 (왼쪽, 밴드 위에 그려서 밑에서 나오는 느낌) ──
            var roll = CreateCircle(content, "Roll", new Vector2(-340f, TapeY), 150f, RollColor);
            roll.raycastTarget = false;
            _rollRect = roll.rectTransform;
            CreateCircle(roll.transform, "Hole", Vector2.zero, 44f, new Color(0.15f, 0.15f, 0.2f)).raycastTarget = false;
            CreatePanel(roll.transform, "Sheen", new Vector2(10f, 16f), new Vector2(16f, 70f),
                new Color(1f, 1f, 1f, 0.15f)).raycastTarget = false;

            // ── 커트 라인 (고정, 롤 출구 근처) + "여기!" 안내 ──
            CreatePanel(content, "CutLine", new Vector2(JudgeContentX, TapeY), new Vector2(6f, 104f), Color.white)
                .raycastTarget = false;
            CreateText(content, "CutHint", new Vector2(JudgeContentX, TapeY + 92f), new Vector2(180f, 44f), "여기!", 32)
                .raycastTarget = false;

            // ── 화면 탭 ──
            var tapArea = CreatePanel(content, "TapCatcher", Vector2.zero, new Vector2(1920f, 1080f), new Color(0f, 0f, 0f, 0f));
            var button = tapArea.gameObject.AddComponent<Button>();
            button.targetGraphic = tapArea;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(OnTap);
        }

        protected override void OnTick(float dt)
        {
            if (_resolved) return;

            float move = ScrollSpeed * dt;
            float span = BandCount * Period;
            float bandWrap = ContainerHalfW + GreenHalf;

            // 초록 밴드 스크롤 (오른쪽)
            foreach (var band in _bands)
            {
                var p = band.anchoredPosition;
                p.x += move;
                if (p.x > bandWrap) p.x -= span;
                band.anchoredPosition = p;
            }

            // 표면 눈금 스크롤 (오른쪽)
            float tickSpan = TickCount * TickGap;
            float tickWrap = ContainerHalfW + 5f;
            foreach (var tick in _ticks)
            {
                var p = tick.anchoredPosition;
                p.x += move;
                if (p.x > tickWrap) p.x -= tickSpan;
                tick.anchoredPosition = p;
            }

            // 시작 연출: 테이프 끝단이 왼쪽(롤)→오른쪽으로 쭉 뽑혀나감
            if (_tipActive)
            {
                _tipX += TipRevealSpeed * dt;
                if (_tipX >= ContainerHalfW)
                {
                    _tipActive = false;
                    _tip.gameObject.SetActive(false);
                }
                else
                {
                    _tip.anchoredPosition = new Vector2(_tipX, 0f);
                }
            }

            // 롤 회전 (풀리는 느낌)
            _rollAngle -= move * 1.4f;
            _rollRect.localRotation = Quaternion.Euler(0f, 0f, _rollAngle);
        }

        void OnTap()
        {
            if (_resolved) return;
            _resolved = true;

            bool onGreen = false;
            foreach (var band in _bands)
            {
                if (Mathf.Abs(band.anchoredPosition.x - JudgeLocalX) <= GreenHalf)
                {
                    onGreen = true;
                    band.GetComponent<Image>().color = new Color(0.45f, 1f, 0.5f);
                    break;
                }
            }

            if (onGreen)
            {
                PlayConfirmPop(new Vector2(JudgeContentX, TapeY), 160f);
                Finish(true);
            }
            else
            {
                PlayConfirmPop(new Vector2(JudgeContentX, TapeY), 120f, new Color(1f, 0.3f, 0.25f, 0.7f));
                Finish(false);
            }
        }
    }
}
