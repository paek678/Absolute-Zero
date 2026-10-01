using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using UnityEngine;

namespace AbsoluteZero.Core.Item
{
    [RequireComponent(typeof(BoxCollider))]
    public class ItemWorldView : MonoBehaviour
    {
        public int SlotIndex { get; private set; }
        public HoverEffect Hover { get; private set; }
        public ItemDataSO Item { get; private set; }

        SpriteRenderer _mainSprite;
        SpriteRenderer _bannedOverlay;
        TextMesh _label;
        Sprite _fallbackSprite;

        public void Initialize(int slotIndex, string itemName, Color itemColor)
            => InitializeView(slotIndex, itemName, GameSprites.GetItemSprite(itemName));

        public void InitializeItem(int slotIndex, ItemDataSO item, Color itemColor)
        {
            Item = item;
            InitializeView(slotIndex, item != null ? item.ItemName : string.Empty, GameSprites.GetItemSpriteFor(item));
        }

        void InitializeView(int slotIndex, string itemName, Sprite itemSprite)
        {
            SlotIndex = slotIndex;
            gameObject.name = $"Item_{slotIndex}_{itemName}";

            var cardGO = new GameObject("Card");
            cardGO.transform.SetParent(transform, false);
            _mainSprite = cardGO.AddComponent<SpriteRenderer>();
            _mainSprite.sprite = itemSprite != null ? itemSprite : (_fallbackSprite = CreateFallbackSprite());
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

            var bannedGO = new GameObject("BannedOverlay");
            bannedGO.transform.SetParent(transform, false);
            bannedGO.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            _bannedOverlay = bannedGO.AddComponent<SpriteRenderer>();
            var views = MatchViewBindings.ForScene(gameObject.scene);
            var bannedTex = views != null ? views.GetSprite(MatchSpriteRole.BannedTape) : Resources.Load<Sprite>("banned_tape");
            if (bannedTex != null) _bannedOverlay.sprite = bannedTex;
            _bannedOverlay.sortingOrder = 7;
            bannedGO.SetActive(false);
        }

        public void SetInteractable(bool interactable)
        {
            if (_mainSprite == null) return;
            _mainSprite.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }

        // The same CopyId keeps its object. Reset transient hover/selection before the
        // presenter reapplies current state; never duplicate Card/Label/listeners.
        internal void RefreshItem(int slotIndex, ItemDataSO item, string uses, bool usable)
        {
            SlotIndex = slotIndex;
            Item = item;
            gameObject.name = $"Item_{slotIndex}_{item.ItemName}";
            var sprite = GameSprites.GetItemSpriteFor(item);
            if (sprite != null && _mainSprite != null) _mainSprite.sprite = sprite;
            Hover?.ResetPresentation(_mainSprite);
            SetBanned(false);
            UpdateDisplay(item.ItemName, uses, usable);
        }

        void OnDestroy()
        {
            if (_fallbackSprite == null) return;
            Destroy(_fallbackSprite.texture);
            Destroy(_fallbackSprite);
        }

        public void SetBanned(bool banned)
        {
            Debug.Log($"[ItemView] SetBanned({banned}) on '{gameObject.name}', overlay={_bannedOverlay != null}, sprite={(_bannedOverlay != null ? (_bannedOverlay.sprite != null).ToString() : "N/A")}");
            if (_bannedOverlay != null)
                _bannedOverlay.gameObject.SetActive(banned);
        }

        public void UpdateDisplay(string itemName, string usesText, bool usable)
        {
            if (_label != null)
                _label.text = $"{itemName}\n{usesText}";

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
