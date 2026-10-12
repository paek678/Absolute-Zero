using UnityEngine;

namespace AbsoluteZero.Core.Inventory
{
    /// <summary>Turns sprite tops towards the icebox in the captured seating camera plane.</summary>
    public static class MultiItemFacing
    {
        public static Vector3 RackPosition(Vector3 origin, Vector3 center, float across, float inward)
        {
            var forward = Vector3.ProjectOnPlane(center - origin, Vector3.up).normalized;
            if (forward.sqrMagnitude < .0001f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            return origin + right * across + forward * inward;
        }

        // All cards in a rack share its heading; height must not change that heading.
        public static Quaternion RackFacing(Vector3 origin, Vector3 center, Quaternion seatingView)
            => TowardCenter(origin, new Vector3(center.x, origin.y, center.z), seatingView);

        public static void ClearFloor(Transform root, SpriteRenderer art, float floorY, float margin = 1f)
        {
            if (root == null || art == null || art.sprite == null) return;
            float minRelativeY = 0f;
            foreach (var vertex in art.sprite.vertices)
                minRelativeY = Mathf.Min(minRelativeY, art.transform.TransformPoint(vertex).y - root.position.y);
            float minimum = floorY + .04f - minRelativeY * margin;
            if (root.position.y < minimum)
                root.position = new Vector3(root.position.x, minimum, root.position.z);
        }

        public static Quaternion TowardCenter(Vector3 position, Vector3 center, Quaternion seatingView)
        {
            var direction = Quaternion.Inverse(seatingView) * (center - position);
            if (direction.x * direction.x + direction.y * direction.y < 0.0001f) return seatingView;
            float angle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
            return seatingView * Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }
}
