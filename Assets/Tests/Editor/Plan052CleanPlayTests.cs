using AbsoluteZero.Core.Common;
using AbsoluteZero.UI.Game.Build;
using NUnit.Framework;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan052CleanPlayTests
    {
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        public void CleanMultiHidesOnlyDiagnosticText(int seats, bool diagnostics)
        {
            var refs = GameHudBuilder.Build(seats);
            try
            {
                Assert.That(refs.PhaseText.gameObject.activeSelf, Is.EqualTo(diagnostics));
                Assert.That(refs.StatusText.gameObject.activeSelf, Is.EqualTo(diagnostics));
                Assert.That(refs.NameBoxes.Length, Is.EqualTo(seats));
                Assert.That(refs.OverlayCanvas.gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(refs.OverlayCanvas.gameObject);
                foreach (var bar in refs.OppBars) Object.DestroyImmediate(bar.Canvas.gameObject);
                Object.DestroyImmediate(refs.ReadyCanvas.gameObject);
            }
        }

        [TestCase("Fan", "--", "--")]
        [TestCase("Cat", "1", "1")]
        public void CleanLabelsKeepUsesAndDebugLabelsKeepNames(string name, string uses, string clean)
        {
            Assert.That(GameplayDebugDisplay.ItemLabel(name, uses, false), Is.EqualTo(clean));
            Assert.That(GameplayDebugDisplay.ItemLabel(name, uses, true), Is.EqualTo(name + "\n" + uses));
        }
    }
}
