using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Common;
using UnityEngine;
using UnityEngine.Pool;

namespace AbsoluteZero.Core.Combat
{
    internal sealed class PresentationParticlePool : IDisposable
    {
        readonly Dictionary<GameObject, ObjectPool<GameObject>> _particlePools = new();
        readonly Dictionary<GameObject, Lease> _active = new();
        bool _disposed;
        internal sealed class Lease
        {
            internal readonly GameObject Prefab;
            public GameObject Instance { get; }
            internal Lease(GameObject prefab, GameObject instance) { Prefab = prefab; Instance = instance; }
        }
        public Lease Spawn(GameObject prefab, Vector3 position)
        {
            if (_disposed || prefab == null) return null;
            var go = GetOrCreatePool(prefab).Get();
            var lease = new Lease(prefab, go);
            _active.Add(go, lease);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play(true);
            return lease;
        }
        public void Release(Lease lease)
        {
            if (lease == null || !_active.TryGetValue(lease.Instance, out var current)
                || !ReferenceEquals(current, lease)) return;
            _active.Remove(lease.Instance);
            if (lease.Instance != null && _particlePools.TryGetValue(lease.Prefab, out var pool))
                pool.Release(lease.Instance);
        }
        public void ReleaseAll()
        {
            foreach (var lease in new List<Lease>(_active.Values)) Release(lease);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseAll();
            foreach (var pool in _particlePools.Values) pool.Dispose();
            _particlePools.Clear();
        }
        ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
        {
            if (_particlePools.TryGetValue(prefab, out var pool))
                return pool;

            var captured = prefab;
            pool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    var go = UnityEngine.Object.Instantiate(captured);
                    ConfigureParticleRenderers(go);
                    go.SetActive(false);
                    return go;
                },
                actionOnGet: go => go.SetActive(true),
                actionOnRelease: go =>
                {
                    var ps = go.GetComponent<ParticleSystem>();
                    if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    go.SetActive(false);
                },
                actionOnDestroy: go =>
                {
                    if (go == null) return;
                    go.GetComponent<RuntimeMaterialOwner>()?.Dispose();
                    PresentationResources.DestroyOwned(go);
                },
                defaultCapacity: 2,
                maxSize: 6
            );
            _particlePools[prefab] = pool;
            return pool;
        }

        internal static void ConfigureParticleRenderers(GameObject go)
        {
            // The pool owns lifetime; authored StopAction.Destroy bypasses Release and leaks copies.
            foreach (var particle in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.stopAction = ParticleSystemStopAction.None;
            }
            var owner = go.AddComponent<RuntimeMaterialOwner>();
            foreach (var psr in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                psr.sortingLayerName = "Default";
                psr.sortingOrder = 100;
                if (psr.sharedMaterial != null)
                {
                    var material = owner.Clone(psr.sharedMaterial);
                    psr.sharedMaterial = material;
                    material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                    material.SetInt("_ZWrite", 0);
                }
            }
        }

    }
}
