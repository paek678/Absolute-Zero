using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 집게손 미니게임 (ClawGrab) — 좌우로 왕복하는 집게가 인형 위를 지날 때 탭해 내리기 (7초).
    /// 집게: 실린더 본체 + 3갈래 곡선 팔(사진 참조). 잡으면 초록 팝, 미스 = 즉시 실패. 배너: "잡아라!"
    /// </summary>
    public class ClawGrabMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "잡아라!";

        enum State { Swinging, Descending, Lifting, Missed }

        const float SwingHalf = 280f;
        const float SwingSpeed = 320f;
        const float ClawTopY = 175f;
        const float DollY = -130f;
        const float JudgeY = DollY + 52f;
        const float DescendSpeed = 640f;
        const float GrabTolerance = 60f;

        static readonly Color MetalLight = new(0.82f, 0.82f, 0.87f);
        static readonly Color MetalMid = new(0.72f, 0.72f, 0.77f);
        static readonly Color MetalDark = new(0.55f, 0.55f, 0.6f);

        State _state = State.Swinging;
        float _swingT;
        float _missTimer;
        RectTransform _claw;
        RectTransform _doll;
        Image _clawBody;
        readonly Image[] _arms = new Image[3];

        protected override void BuildContent(RectTransform content)
        {
            // 인형 (원형)
            var doll = CreateCircle(content, "Doll", new Vector2(0f, DollY), 104f, new Color(0.85f, 0.6f, 0.68f));
            doll.raycastTarget = false;
            CreateCircle(doll.transform, "EyeL", new Vector2(-19f, 12f), 14f, Color.black).raycastTarget = false;
            CreateCircle(doll.transform, "EyeR", new Vector2(19f, 12f), 14f, Color.black).raycastTarget = false;
            _doll = doll.rectTransform;

            // 집게 루트
            var clawGO = new GameObject("Claw");
            clawGO.transform.SetParent(content, false);
            _claw = clawGO.AddComponent<RectTransform>();
            _claw.anchoredPosition = new Vector2(0f, ClawTopY);
            _claw.sizeDelta = new Vector2(90f, 60f);

            // 로프
            CreatePanel(_claw, "Rope", new Vector2(0f, 260f), new Vector2(6f, 460f), MetalDark).raycastTarget = false;

            // 실린더 본체 (위 원통 + 아래 원통 — 사진의 2단 실린더)
            CreateCircle(_claw, "BodyTop", new Vector2(0f, 16f), 44f, MetalLight).raycastTarget = false;
            _clawBody = CreatePanel(_claw, "BodyMid", Vector2.zero, new Vector2(50f, 44f), MetalLight);
            _clawBody.raycastTarget = false;
            CreateCircle(_claw, "BodyBot", new Vector2(0f, -16f), 52f, MetalMid).raycastTarget = false;
            // 볼트 디테일
            CreateCircle(_claw, "BoltL", new Vector2(-16f, -16f), 8f, MetalDark).raycastTarget = false;
            CreateCircle(_claw, "BoltR", new Vector2(16f, -16f), 8f, MetalDark).raycastTarget = false;

            // 3갈래 곡선 팔 (Arc 스프라이트 — 좌/중앙/우)
            float[] armAngles = { 25f, 0f, -25f };
            float[] armX = { -18f, 0f, 18f };
            for (int i = 0; i < 3; i++)
            {
                var armGO = new GameObject($"Arm{i}");
                armGO.transform.SetParent(_claw, false);
                var armRect = armGO.AddComponent<RectTransform>();
                armRect.pivot = new Vector2(0.5f, 1f);
                armRect.anchoredPosition = new Vector2(armX[i], -36f);
                armRect.sizeDelta = new Vector2(30f, 100f);
                armRect.localRotation = Quaternion.Euler(0f, 0f, armAngles[i]);
                var armImg = armGO.AddComponent<Image>();
                armImg.sprite = MiniGameArt.Arc();
                armImg.color = MetalMid;
                armImg.preserveAspect = false;
                armImg.raycastTarget = false;
                _arms[i] = armImg;

                // 팔 끝 갈고리 (작은 원)
                var tipGO = new GameObject("Tip");
                tipGO.transform.SetParent(armRect, false);
                var tipRect = tipGO.AddComponent<RectTransform>();
                tipRect.anchoredPosition = new Vector2(0f, -100f);
                tipRect.sizeDelta = new Vector2(14f, 14f);
                var tipImg = tipGO.AddComponent<Image>();
                tipImg.sprite = MiniGameArt.Circle();
                tipImg.color = MetalLight;
                tipImg.raycastTarget = false;
            }

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
                    if (pos.y <= JudgeY)
                    {
                        pos.y = JudgeY;
                        if (Mathf.Abs(pos.x - _doll.anchoredPosition.x) <= GrabTolerance)
                        {
                            _doll.SetParent(_claw, worldPositionStays: false);
                            _doll.anchoredPosition = new Vector2(0f, -110f);
                            SetArmColor(new Color(0.45f, 0.95f, 0.5f));
                            PlayConfirmPop(new Vector2(pos.x, pos.y - 40f), 170f);
                            _state = State.Lifting;
                        }
                        else
                        {
                            SetArmColor(new Color(0.9f, 0.3f, 0.25f));
                            PlayConfirmPop(new Vector2(pos.x, pos.y - 40f), 140f, new Color(1f, 0.3f, 0.25f, 0.7f));
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

        void SetArmColor(Color color)
        {
            _clawBody.color = color;
            foreach (var arm in _arms)
                if (arm != null) arm.color = color;
        }
    }
}
