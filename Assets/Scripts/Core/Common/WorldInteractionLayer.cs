using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    // World interaction art sits above scene geometry. Screen-space modal UI
    // retains its own canvas ordering and input ownership.
    public static class WorldInteractionLayer
    {
        public const int ItemOrder = 20000;
        public const int SelectionOrder = ItemOrder + 1;
        public const int BanOrder = ItemOrder + 2;
        public const int ButtonOrder = ItemOrder + 10;
        static Material _material;
        public static Material Material => _material != null ? _material
            : (_material = Resources.Load<Material>("UI/WorldInteractionForeground"));

        public static void Apply(SpriteRenderer renderer, int order = ItemOrder)
        {
            renderer.sharedMaterial = Material;
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = order;
            var texture = renderer.GetComponent<ForegroundSpriteTexture>();
            if (texture == null) texture = renderer.gameObject.AddComponent<ForegroundSpriteTexture>();
            texture.Refresh(renderer);
        }

        public static void ApplyHeld(SpriteRenderer renderer)
        {
            Apply(renderer);
            // Character/FPS sorting groups must not trap the held prop behind
            // another actor. Keep its animation transform under the hand rig.
            var group = renderer.GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (group == null) group = renderer.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            group.sortAtRoot = true;
            group.sortingLayerID = 0;
            group.sortingOrder = ItemOrder;
        }
    }
}
