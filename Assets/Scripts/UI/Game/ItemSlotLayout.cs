using UnityEngine;

namespace AbsoluteZero.UI.Game
{
    /// <summary>
    /// 아이템 슬롯 월드 배치 계산 (PLAN_009).
    /// 씬에는 기본 4칸 마커(PlayerItem1~4 / EnemyItem1~4)만 있으므로,
    /// 랜덤 8칸(4×2 — Q16 확정)·아이스박스·준비끝 위치는 마커 행 기준으로 런타임 파생한다.
    /// 배치: [아이스박스][랜덤 4×2 그리드] … [기본 4칸(마커)] — 그리드는 행의 바깥쪽(-x 방향).
    /// </summary>
    public static class ItemSlotLayout
    {
        public const int BASIC_COUNT = 4;
        public const int RANDOM_COUNT = 8;   // 4×2 (기획 Q16)
        public const int TOTAL_SLOTS = BASIC_COUNT + RANDOM_COUNT;

        const float FALLBACK_SPACING = 0.9f;

        public struct SideLayout
        {
            public bool Valid;
            public Vector3[] Slots;        // [0..3] 기본(마커), [4..11] 랜덤 그리드
            public Vector3 IceBoxPos;      // 랜덤 그리드 바깥쪽
            public Vector3 ReadyButtonPos; // 행 중앙 (기본과 그리드 사이 공백 중심)
        }

        /// <summary>markerPrefix: "PlayerItem" 또는 "EnemyItem"</summary>
        public static SideLayout Build(string markerPrefix)
        {
            var layout = new SideLayout { Slots = new Vector3[TOTAL_SLOTS] };

            var basics = new Vector3[BASIC_COUNT];
            for (int i = 0; i < BASIC_COUNT; i++)
            {
                var marker = GameObject.Find($"{markerPrefix}{i + 1}");
                if (marker == null) return layout;   // Valid = false → 호출부 폴백
                basics[i] = marker.transform.position;
                layout.Slots[i] = basics[i];
            }

            // 행 지표: 평균 y/z, x 범위, 이웃 간격
            float y = 0f, z = 0f, xMin = float.MaxValue, xMax = float.MinValue;
            foreach (var p in basics)
            {
                y += p.y; z += p.z;
                xMin = Mathf.Min(xMin, p.x);
                xMax = Mathf.Max(xMax, p.x);
            }
            y /= BASIC_COUNT;
            z /= BASIC_COUNT;
            float spacing = BASIC_COUNT > 1 ? (xMax - xMin) / (BASIC_COUNT - 1) : FALLBACK_SPACING;
            if (spacing < 0.05f) spacing = FALLBACK_SPACING;

            // 랜덤 4×2 그리드 — 행 바깥쪽(-x), 기본 행과 같은 z 중심으로 두 줄
            float colStep = spacing * 0.8f;
            float rowStep = spacing * 0.85f;
            float gridStartX = xMin - spacing * 1.35f;
            for (int r = 0; r < RANDOM_COUNT; r++)
            {
                int col = r % 4;
                int row = r / 4;
                layout.Slots[BASIC_COUNT + r] = new Vector3(
                    gridStartX - col * colStep,
                    y,
                    z + (row == 0 ? rowStep * 0.45f : -rowStep * 0.45f));
            }

            float gridEndX = gridStartX - 3 * colStep;
            layout.IceBoxPos = new Vector3(gridEndX - spacing * 1.2f, y, z);
            layout.ReadyButtonPos = new Vector3((gridStartX + xMin) * 0.5f, y + 0.05f, z);
            layout.Valid = true;
            return layout;
        }

        // ═══ 그레이박스 데코 (클라이언트 로컬 연출 — 네트워크 무관) ═══

        static Sprite _padSprite;

        static Sprite PadSprite()
        {
            if (_padSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _padSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), tex.width);   // 1유닛 크기 스프라이트
            }
            return _padSprite;
        }

        /// <summary>슬롯 위치마다 반투명 바닥 패드 생성 — 빈 랜덤 슬롯도 상시 표시 (Q16)</summary>
        public static void SpawnSlotPads(SideLayout layout, Transform parent, float size = 0.62f)
        {
            if (!layout.Valid) return;

            for (int i = 0; i < layout.Slots.Length; i++)
            {
                var go = new GameObject($"SlotPad_{i}");
                go.transform.SetParent(parent, false);
                go.transform.position = layout.Slots[i] + Vector3.up * 0.01f;
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 바닥에 눕힘
                go.transform.localScale = new Vector3(size, size, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PadSprite();
                // 랜덤 슬롯은 살짝 더 밝게 — 추가 저장 가능함을 노출
                sr.color = i < BASIC_COUNT
                    ? new Color(1f, 1f, 1f, 0.10f)
                    : new Color(1f, 1f, 1f, 0.18f);
                sr.sortingOrder = 1;   // 아이템 스프라이트(5)보다 아래
            }
        }

        /// <summary>아이스박스 그레이박스 (본체 + 뚜껑) — 랜덤 아이템 보관함 표시</summary>
        public static void SpawnIceBox(Vector3 pos, Transform parent)
        {
            var root = new GameObject("IceBox(Greybox)");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            body.transform.localScale = new Vector3(0.62f, 0.44f, 0.48f);
            Object.Destroy(body.GetComponent<Collider>());   // 아이템 클릭 레이캐스트 방해 방지
            body.GetComponent<Renderer>().material.color = new Color(0.92f, 0.95f, 0.97f);

            var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "Lid";
            lid.transform.SetParent(root.transform, false);
            lid.transform.localPosition = new Vector3(-0.08f, 0.5f, -0.14f);
            lid.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            lid.transform.localScale = new Vector3(0.64f, 0.08f, 0.5f);
            Object.Destroy(lid.GetComponent<Collider>());
            lid.GetComponent<Renderer>().material.color = new Color(0.85f, 0.9f, 0.95f);
        }
    }
}
