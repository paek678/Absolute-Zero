using AbsoluteZero.Core.Combat;
using UnityEngine;

namespace AbsoluteZero.Core.Inventory
{
    /// <summary>Presentation-only floor contact, including camera-facing and hover poses.</summary>
    [DisallowMultipleComponent]
    public sealed class InventoryFloorContact : MonoBehaviour
    {
        public SpriteRenderer Art { get; private set; }
        public float FloorY { get; private set; }
        public static void Bind(GameObject root, SpriteRenderer art, float floorY)
        {
            var contact = root.GetComponent<InventoryFloorContact>();
            if (contact == null) contact = root.AddComponent<InventoryFloorContact>();
            contact.Art = art;
            contact.FloorY = floorY;
            contact.Apply();
            DuelSpriteFacing.Register(root);
        }
        public void Apply() => MultiItemFacing.ClearFloor(transform, Art, FloorY);
    }
}
