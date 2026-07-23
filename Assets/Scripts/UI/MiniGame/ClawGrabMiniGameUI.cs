using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 집게손 미니게임 (ClawGrab) — 좌우로 왕복하는 집게가 상자 위를 지날 때 탭해 내려 집기 (7초).
    /// 집게 비주얼: Resources/ItemSprite/Claw_Minigame2(열림) → Claw_Minigame1(닫힘) 스프라이트 스왑.
    /// 잡으면 초록 팝 + 상자 들어올리기, 미스 = 빈 집게 닫힘 + 즉시 실패. 배너: "잡아라!"
    /// </summary>
    public class ClawGrabMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "잡아라!";

        enum State { Swinging, Descending, Lifting, Missed }

        const float SwingHalf = 280f;
        const float SwingSpeed = 320f;
        const float ClawTopY = 200f;
        const float BoxY = -150f;
        const float GrabReach = 112f;                 // 집게 중심 → 집게발 끝
        const float JudgeClawY = BoxY + GrabReach;    // 내려가 멈추는 집게 중심 Y
        const float DescendSpeed = 720f;
        const float GrabTolerance = 62f;

        static readonly Vector2 ClawSize = new(212f, 280f);

        State _state = State.Swinging;
        float _swingT;
        float _missTimer;
        RectTransform _claw;
        RectTransform _box;
        Image _clawImg;
        Sprite _clawOpen;
        Sprite _clawClosed;

        protected override void BuildContent(RectTransform content)
        {
            _clawOpen = Resources.Load<Sprite>("ItemSprite/Claw_Minigame2");
            _clawClosed = Resources.Load<Sprite>("ItemSprite/Claw_Minigame1");

            // 잡을 대상 — 단순 사각형 상자 (테두리 + 채움 + 하이라이트)
            var box = CreatePanel(content, "Box", new Vector2(0f, BoxY), new Vector2(122f, 122f),
                new Color(0.18f, 0.36f, 0.58f));
            box.raycastTarget = false;
            CreatePanel(box.transform, "BoxFill", Vector2.zero, new Vector2(98f, 98f),
                new Color(0.33f, 0.62f, 0.9f)).raycastTarget = false;
            CreatePanel(box.transform, "BoxShine", new Vector2(-22f, 22f), new Vector2(30f, 12f),
                new Color(1f, 1f, 1f, 0.35f)).raycastTarget = false;
            _box = box.rectTransform;

            // 집게 루트 (이동용, 그래픽 없음)
            var clawGO = new GameObject("Claw");
            clawGO.transform.SetParent(content, false);
            _claw = clawGO.AddComponent<RectTransform>();
            _claw.anchoredPosition = new Vector2(0f, ClawTopY);
            _claw.sizeDelta = ClawSize;

            // 집게 본체 스프라이트 (열림 상태로 시작)
            var bodyGO = new GameObject("ClawBody");
            bodyGO.transform.SetParent(_claw, false);
            var bodyRect = bodyGO.AddComponent<RectTransform>();
            bodyRect.anchoredPosition = Vector2.zero;
            bodyRect.sizeDelta = ClawSize;
            _clawImg = bodyGO.AddComponent<Image>();
            _clawImg.sprite = _clawOpen;
            _clawImg.preserveAspect = true;
            _clawImg.raycastTarget = false;

            // 화면 탭
            var tapArea = CreatePanel(content, "TapArea", Vector2.zero, new Vector2(1920f, 1080f), new Color(0f, 0f, 0f, 0f));
            var button = tapArea.gameObject.AddComponent<Button>();
            button.targetGraphic = tapArea;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(OnTap);
        }

        void OnTap()
        {
            if (_state != State.Swinging) return;
            _state = State.Descending;
        }

        protected override void OnTick(float dt)
        {
            var pos = _claw.anchoredPosition;

            switch (_state)
            {
                case State.Swinging:
                    _swingT += dt * SwingSpeed;
                    pos.x = Mathf.PingPong(_swingT, SwingHalf * 2f) - SwingHalf;
                    break;

                case State.Descending:
                    pos.y -= DescendSpeed * dt;
                    if (pos.y <= JudgeClawY)
                    {
                        pos.y = JudgeClawY;
                        _clawImg.sprite = _clawClosed;   // 집게 닫힘
                        if (Mathf.Abs(pos.x - _box.anchoredPosition.x) <= GrabTolerance)
                        {
                            _box.SetParent(_claw, worldPositionStays: false);
                            _box.anchoredPosition = new Vector2(0f, -GrabReach + 22f);
                            PlayConfirmPop(new Vector2(pos.x, pos.y - GrabReach + 10f), 170f);
                            _state = State.Lifting;
                        }
                        else
                        {
                            PlayConfirmPop(new Vector2(pos.x, pos.y - GrabReach + 10f), 140f,
                                new Color(1f, 0.3f, 0.25f, 0.7f));
                            _missTimer = 0.4f;
                            _state = State.Missed;
                        }
                    }
                    break;

                case State.Lifting:
                    pos.y += DescendSpeed * 0.8f * dt;
                    if (pos.y >= ClawTopY)
                        Finish(true);
                    break;

                case State.Missed:
                    _missTimer -= dt;
                    if (_missTimer <= 0f)
                        Finish(false);
                    break;
            }

            _claw.anchoredPosition = pos;
        }
    }
}
