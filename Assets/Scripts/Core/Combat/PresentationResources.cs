using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Combat
{
    // One ledger per presentation manager. A late lease cannot restore a newer owner's pose.
    internal sealed class PresentationTransforms
    {
        readonly Dictionary<Transform, Lease> _leases = new();

        public Lease Capture(Transform target, object owner)
        {
            if (target == null) return null;
            if (_leases.TryGetValue(target, out var previous)) previous.Dispose();
            var lease = new Lease(this, target, owner);
            _leases.Add(target, lease);
            return lease;
        }

        internal sealed class Lease : IDisposable
        {
            readonly PresentationTransforms _ledger;
            readonly Transform _target;
            readonly Vector3 _position;
            public object Owner { get; }
            bool _released;

            internal Lease(PresentationTransforms ledger, Transform target, object owner)
            { _ledger = ledger; _target = target; Owner = owner; _position = target.position; }

            public void Dispose()
            {
                if (_released) return;
                _released = true;
                if (!_ledger._leases.TryGetValue(_target, out var current)
                    || !ReferenceEquals(current, this)) return;
                _ledger._leases.Remove(_target);
                if (_target != null) _target.position = _position;
            }
        }
    }

    // Transient objects and borrowed-state cleanup belong to one sequence, never a scene-wide name.
    internal sealed class PresentationResources : IDisposable
    {
        readonly List<Action> _cleanup = new();
        Action _releaseInventory;
        bool _disposed;
        public bool IsDisposed => _disposed;

        public T Own<T>(T value) where T : UnityEngine.Object
        {
            OnRelease(() => DestroyOwned(value));
            return value;
        }

        public void OnRelease(Action cleanup)
        {
            if (cleanup == null) return;
            if (_disposed) cleanup();
            else _cleanup.Add(cleanup);
        }

        public void HoldInventory(Action acquire, Action release)
        {
            if (_disposed || _releaseInventory != null) return;
            acquire();
            _releaseInventory = release;
        }

        public void ReleaseInventory()
        {
            var release = _releaseInventory;
            _releaseInventory = null;
            release?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // Restore borrowed views before an inventory rebuild can replace them.
            for (int i = _cleanup.Count - 1; i >= 0; i--)
                try { _cleanup[i](); } catch (Exception error) { Debug.LogException(error); }
            _cleanup.Clear();
            try { ReleaseInventory(); } catch (Exception error) { Debug.LogException(error); }
        }

        internal static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
