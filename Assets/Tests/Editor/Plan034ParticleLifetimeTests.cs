using System.Reflection;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using NUnit.Framework;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public class Plan034ParticleLifetimeTests
    {
        [Test]
        public void PooledParticlesCannotSelfDestroy_AndPoolDisposalReleasesCopies()
        {
            var go = new GameObject("ParticleLifetimeTest");
            var source = new Material(Shader.Find("Sprites/Default"));
            Material copy = null;
            try
            {
                var particle = go.AddComponent<ParticleSystem>();
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particle.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = source;
                typeof(CombatVFXManager).GetMethod("ConfigureParticleRenderers",
                    BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { go });
                copy = renderer.sharedMaterial;
                Assert.That(particle.main.stopAction, Is.EqualTo(ParticleSystemStopAction.None));
                Assert.That(copy, Is.Not.SameAs(source));
                go.SetActive(false); // Pool release must preserve the owned material for reuse.
                Assert.That(copy != null, Is.True);
                // EditMode does not dispatch this runtime component's destruction lifecycle.
                // Exercise the explicit pool-disposal contract; player runs cover scene teardown.
                go.GetComponent<RuntimeMaterialOwner>().Dispose();
                Object.DestroyImmediate(go);
                Assert.That(copy == null, Is.True);
                Assert.That(source != null, Is.True, "Imported/shared source ownership must not be stolen");
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
                if (copy != null) Object.DestroyImmediate(copy);
                if (source != null) Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void MaterialOwnerDisposalIsIdempotentAndPreservesSharedSource()
        {
            var go = new GameObject("MaterialOwnerTest");
            var source = new Material(Shader.Find("Sprites/Default"));
            try
            {
                var owner = go.AddComponent<RuntimeMaterialOwner>();
                var first = owner.Clone(source);
                var second = owner.Clone(source);
                Assert.That(first, Is.Not.SameAs(second));
                owner.Dispose();
                owner.Dispose();
                Object.DestroyImmediate(go);
                Assert.That(first == null && second == null, Is.True);
                Assert.That(source != null, Is.True);
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
                Object.DestroyImmediate(source);
            }
        }
    }
}
