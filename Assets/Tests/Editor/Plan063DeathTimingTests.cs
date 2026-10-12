using System.Collections;
using System.Reflection;
using AbsoluteZero.Core.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbsoluteZero.Tests
{
    public class Plan063DeathTimingTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(false)]
        [TestCase(true)]
        public void BreakHidesBodyImmediatelyThenWaitsHalfSecond(bool finalDeath)
        {
            var owner = new GameObject("death fixture");
            var root = new GameObject("visual");
            var body = new GameObject("body", typeof(SpriteRenderer));
            var ice = new GameObject("freezeice", typeof(SpriteRenderer));
            body.transform.SetParent(root.transform); ice.transform.SetParent(root.transform);
            try
            {
                var visual = owner.AddComponent<AZPlayerVisual>();
                void Set(string name, object value) => typeof(AZPlayerVisual).GetField(name, Private).SetValue(visual, value);
                Set("_visualRoot", root.transform);
                Set("_spriteRenderers", new[] { body.GetComponent<SpriteRenderer>(), ice.GetComponent<SpriteRenderer>() });
                Set("_freezeRenderer", ice.GetComponent<SpriteRenderer>());
                Set("_isDead", true); Set("_endsMatch", finalDeath);
                var routine = (IEnumerator)typeof(AZPlayerVisual).GetMethod("DeathRoutine", Private).Invoke(visual, null);
                for (int i = 0; i < 4; i++) Assert.True(routine.MoveNext());
                Assert.True(body.GetComponent<SpriteRenderer>().enabled);
                Assert.True(routine.MoveNext()); // emission and hiding, before the reveal wait
                Assert.False(body.GetComponent<SpriteRenderer>().enabled);
                Assert.False(ice.activeSelf);
                Assert.False(visual.IsGhost);
                Assert.That((float)typeof(WaitForSeconds).GetField("m_Seconds", Private).GetValue(routine.Current), Is.EqualTo(.5f));
                // Runtime cache owns per-renderer materials. EditMode warns about
                // cloning; expect these exact two fixture-only messages and clean up.
                const string cloneWarning = "Instantiating material due to calling renderer.material during edit mode. This will leak materials into the scene. You most likely want to use renderer.sharedMaterial instead.";
                var originalBody = body.GetComponent<SpriteRenderer>().sharedMaterial;
                var originalIce = ice.GetComponent<SpriteRenderer>().sharedMaterial;
                LogAssert.Expect(LogType.Error, cloneWarning);
                LogAssert.Expect(LogType.Error, cloneWarning);
                typeof(AZPlayerVisual).GetMethod("SynchronizeAppearance", Private).Invoke(visual, null);
                if (body.GetComponent<SpriteRenderer>().sharedMaterial != originalBody)
                    Object.DestroyImmediate(body.GetComponent<SpriteRenderer>().sharedMaterial);
                if (ice.GetComponent<SpriteRenderer>().sharedMaterial != originalIce)
                    Object.DestroyImmediate(ice.GetComponent<SpriteRenderer>().sharedMaterial);
                Assert.False(body.GetComponent<SpriteRenderer>().enabled, "cosmetic refresh must preserve the gap");
                Assert.False(routine.MoveNext());
                Assert.That(visual.IsGhost, Is.EqualTo(finalDeath)); // duel non-final has no ghost gameplay
                visual.ReviveVisual();
                Assert.True(body.GetComponent<SpriteRenderer>().enabled);
                Assert.False(visual.IsGhost);
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(root); }
        }
    }
}
