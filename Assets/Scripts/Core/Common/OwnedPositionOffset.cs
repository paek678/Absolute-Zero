using System;
using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    // Remove only an offset still owned by this effect; a camera/layout overwrite wins.
    public sealed class OwnedPositionOffset
    {
        readonly Func<Vector3> _read;
        readonly Action<Vector3> _write;
        readonly Func<bool> _isAlive;
        Vector3 _base, _applied;
        bool _owns;
        public OwnedPositionOffset(Func<Vector3> read, Action<Vector3> write, Func<bool> isAlive = null)
        { _read = read; _write = write; _isAlive = isAlive; }
        public static OwnedPositionOffset ForRectTransform(RectTransform target)
            => new(() => target.anchoredPosition3D, value => target.anchoredPosition3D = value,
                () => target != null);
        public void Apply(Vector3 offset)
        {
            if (_isAlive != null && !_isAlive()) { _owns = false; return; }
            var current = _read();
            _base = _owns && current == _applied ? _base : current;
            _applied = _base + offset; _owns = true; _write(_applied);
        }
        public void Clear()
        {
            bool owned = _owns;
            _owns = false;
            if (owned && (_isAlive == null || _isAlive()) && _read() == _applied) _write(_base);
        }
    }
}
