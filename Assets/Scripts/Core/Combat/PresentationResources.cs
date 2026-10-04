using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbsoluteZero.Core.Combat
{
    // Keep part ordering and cosmetic overlays together during a close-up.
    internal sealed class PresentationSortingLease : IDisposable
    {
        readonly SortingGroup _group;
        readonly bool _enabled;
        readonly int _order;
        bool _released;

        public PresentationSortingLease(Transform root, int order)
        {
            _group = root.GetComponent<SortingGroup>();
            if (_group == null)
            {
                _group = root.gameObject.AddComponent<SortingGroup>();
                _group.enabled = false;
            }
            _enabled = _group.enabled;
            _order = _group.sortingOrder;
            _group.sortingOrder = Mathf.Max(order, _order);
            _group.enabled = true;
        }

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            if (_group == null) return;
            _group.sortingOrder = _order;
            _group.enabled = _enabled;
            // Retain a disabled runtime component: deferred Destroy could remove
            // a new borrow of this root in the same frame. The root owns its life.
        }
    }

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
            readonly Quaternion _rotation;
            public object Owner { get; }
            bool _released;

            internal Lease(PresentationTransforms ledger, Transform target, object owner)
            { _ledger = ledger; _target = target; Owner = owner; _position = target.position; _rotation = target.rotation; }

            public void Dispose()
            {
                if (_released) return;
                _released = true;
                if (!_ledger._leases.TryGetValue(_target, out var current)
                    || !ReferenceEquals(current, this)) return;
                _ledger._leases.Remove(_target);
                if (_target != null) _target.SetPositionAndRotation(_position, _rotation);
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
