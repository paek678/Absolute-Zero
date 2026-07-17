using AbsoluteZero.Core.Common;
using UnityEngine;

namespace AbsoluteZero.Core.Item
{
    [RequireComponent(typeof(BoxCollider))]
    public class ItemWorldView : MonoBehaviour
    {
        public int SlotIndex { get; private set; }
        public HoverEffect Hover { get; private set; }

        SpriteRenderer _mainSprite;
        TextMesh _label;
        Material _litMat;

        public void Initialize(int slotIndex, string itemName, Color itemColor)
        {
            SlotIndex = slotIndex;
            gameObject.name = $"Item_{slotIndex}_{itemName}";

            _litMat = Resources.Load<Material>("sprite3DMat");

            var cardGO = new GameObject("Card");
            cardGO.transform.SetParent(transform, false);
            _mainSprite = cardGO.AddComponent<SpriteRenderer>();
            var itemSprite = GameSprites.GetItemSprite(itemName);
            _mainSprite.sprite = itemSprite != null ? itemSprite : CreateFallbackSprite();
            if (_litMat != null)
                _mainSprite.material = _litMat;
            _mainSprite.sortingOrder = 5;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(transform, false);
            labelGO.transform.localPosition = new Vector3(0f, -0.6f, -0.01f);
            _label = labelGO.AddComponent<TextMesh>();
            _label.text = itemName;
            _label.fontSize = 28;
            _label.characterSize = 0.05f;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.color = Color.white;
            _label.fontStyle = FontStyle.Bold;
            labelGO.GetComponent<MeshRenderer>().sortingOrder = 6;

            var col = GetComponent<BoxCollider>();
            col.size = new Vector3(0.75f, 1.2f, 0.5f);
            col.center = Vector3.zero;

            transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);

            gameObject.layer = LayerMask.NameToLayer("Interactable");

            Hover = gameObject.AddComponent<HoverEffect>();
            Hover.Initialize();
        }

        public void SetInteractable(bool interactable)
        {
            if (_mainSprite == null) return;
            _mainSprite.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }

        public void UpdateDisplay(string itemName, string usesText, bool usable)
        {
            if (_label != null)
                _label.text = $"{itemName}\n{usesText}";

            // 지급/리롤/소모로 슬롯 내용물이 바뀌면 스프라이트도 갱신 (라벨만 갱신하던 버그 수정)
            if (_mainSprite != null)
            {
                var sprite = GameSprites.GetItemSprite(itemName);
                _mainSprite.sprite = sprite != null ? sprite : CreateFallbackSprite();
            }

            SetInteractable(usable);
        }

        static Sprite CreateFallbackSprite()
        {
            const int w = 48;
            const int h = 64;
            var tex = new Texture2D(w, h);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
