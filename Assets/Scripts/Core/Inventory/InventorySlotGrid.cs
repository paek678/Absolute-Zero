using UnityEngine;

namespace AbsoluteZero.Core.Inventory
{
    /// <summary>Connected inventory cells using the same painted sprite and material as Duel.</summary>
    public static class InventorySlotGrid
    {
        // Slightly overlap the irregular painted edges without moving slot centers.
        const float BorderContactScale = 1.08f;

        public static void Create(Transform parent, Vector3 center, Vector3 right,
            int columns, int rows, float cellWidth, float cellDepth)
        {
            var sprite = Resources.Load<Sprite>("ItemSprite/itemPlace");
            var material = Resources.Load<Material>("sprite3DMat");
            if (sprite == null || material == null)
            {
                Debug.LogError("[MultiLayout] Missing Duel item-place sprite or material.");
                return;
            }
            var root = new GameObject("SlotGrid").transform;
            root.SetParent(parent, false);
            root.position = center;
            right = Vector3.ProjectOnPlane(right, Vector3.up).normalized;
            root.rotation = Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up);
            // Original 256px Duel art has transparent padding. Its painted alpha
            // bounds are x=[8,250), y=[6,251) in bottom-left texture coordinates.
            // Fit the paint, not the full texture, so adjacent places still touch.
            var inkSize = new Vector2(242f, 245f) / sprite.pixelsPerUnit;
            var inkCenter = new Vector3(1f, .5f, 0f) / sprite.pixelsPerUnit;
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                var cell = new GameObject($"ItemPlace_{row}_{column}").transform;
                cell.SetParent(root, false);
                cell.localPosition = new Vector3((column - (columns - 1) * .5f) * cellWidth,
                    0f, (row - (rows - 1) * .5f) * cellDepth);
                // Horizontal floor artwork stays out of screen-facing sprite discovery.
                cell.localRotation = Quaternion.Euler(90f, 0f, 0f);
                cell.localScale = new Vector3(cellWidth / inkSize.x * BorderContactScale,
                    cellDepth / inkSize.y * BorderContactScale, 1f);
                cell.localPosition -= cell.localRotation * Vector3.Scale(inkCenter, cell.localScale);
                var renderer = cell.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = material;
                renderer.color = Color.white;
                renderer.sortingOrder = -1;
            }
        }
    }
}
