using UnityEngine;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 미니게임 그레이박스용 절차 생성 스프라이트 (아트 에셋 없이 원형 표현).
    /// 전부 흰색으로 생성 — 색은 Image.color 틴트로.
    /// </summary>
    public static class MiniGameArt
    {
        static Sprite _circle;
        static Sprite _arc;

        /// <summary>부드러운 가장자리의 원 (틴트용 흰색, 중앙 피벗)</summary>
        public static Sprite Circle()
        {
            if (_circle != null) return _circle;

            const int size = 96;
            float radius = size * 0.5f - 1.5f;
            var center = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);   // 1px 소프트 엣지
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;

            _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _circle;
        }

        /// <summary>
        /// 집게 팔용 곡선 호 — 상단이 열려있고 하단으로 갈수록 안쪽으로 감기는 C자 형태.
        /// 좌측 팔 기준으로 생성, 우측은 flipX로 미러. 피벗 = 상단 중앙(0.5, 1).
        /// </summary>
        public static Sprite Arc()
        {
            if (_arc != null) return _arc;

            const int w = 48, h = 128;
            const float thickness = 7f;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            for (int py = 0; py < h; py++)
            {
                float t = 1f - (float)py / (h - 1);   // t: 1(top) → 0(bottom)
                float curveX = w * 0.5f + 12f * Mathf.Sin(t * Mathf.PI * 0.95f);
                float curveR = thickness - 2f * (1f - t);

                for (int px = 0; px < w; px++)
                {
                    float dist = Mathf.Abs(px - curveX);
                    float alpha = Mathf.Clamp01(curveR - dist + 0.5f);
                    pixels[py * w + px] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;

            _arc = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 100);
            return _arc;
        }
    }
}
