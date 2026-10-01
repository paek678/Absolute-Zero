using UnityEngine;

namespace AbsoluteZero.Core.Cosmetic
{
    public sealed class CosmeticPreview : MonoBehaviour
    {
        public CosmeticItemSO[] Items;
        CosmeticVisualController _controller;
        [ContextMenu("Refresh Cosmetic Preview")]
        public void Refresh()
        {
            _controller ??= new CosmeticVisualController();
            _controller.BindCharacter(transform);
            var state = new CosmeticEquipState();
            if (Items != null) foreach (var item in Items) state.Equip(item);
            _controller.ApplyAll(state);
        }
        void Start() => Refresh();
        void OnDestroy() => _controller?.Clear();
    }
}
