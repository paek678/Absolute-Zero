using UnityEngine;

namespace AbsoluteZero.Core.Maps
{
    public enum MapLayoutMode { Duel, Multi }
    public enum MapAnchorRole { ServerSpawn, RemoteVisual }

    [DisallowMultipleComponent]
    public sealed class MapCharacterAnchor : MonoBehaviour
    {
        [SerializeField] MapLayoutMode _mode;
        [SerializeField] MapAnchorRole _role;
        [Min(0), SerializeField] int _index;
        [Tooltip("Offset from the floor anchor to the character sprite pivot.")]
        [SerializeField] Vector3 _visualPivotOffset = new(0f, 1.6f, 0f);
        [SerializeField] Vector3 _visualScale = new(1.3163f, 1.3f, 1f);

        public MapLayoutMode Mode => _mode;
        public MapAnchorRole Role => _role;
        public int Index => _index;
        public Vector3 VisualPosition => transform.position + transform.rotation * _visualPivotOffset;
        public Vector3 VisualScale => _visualScale;

        public bool IsUsable(Transform owner)
        {
            if (!transform.IsChildOf(owner) || !Finite(transform.position) || !Finite(_visualPivotOffset)
                || !Finite(_visualScale) || _visualScale.x <= 0 || _visualScale.y <= 0 || _visualScale.z <= 0) return false;
            for (var node = transform; node != null; node = node.parent)
            {
                if (!node.gameObject.activeSelf) return false;
                if (node == owner) return true;
            }
            return false;
        }

        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void ApplyVisualPose(Transform target)
        {
            target.SetPositionAndRotation(VisualPosition, transform.rotation);
            var parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
            target.localScale = new Vector3(_visualScale.x / parentScale.x, _visualScale.y / parentScale.y, _visualScale.z / parentScale.z);
        }
    }
}
