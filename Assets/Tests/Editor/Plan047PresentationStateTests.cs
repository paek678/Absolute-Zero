using AbsoluteZero.Core.Combat;
using NUnit.Framework;

namespace AbsoluteZero.Tests
{
    public sealed class Plan047PresentationStateTests
    {
        [Test] public void ActualFirstActionRetainedAcrossActionsAndStaleCompletion()
        {
            var state = new CombatPresentationHudState(); var a = new object(); var b = new object();
            state.Begin(a, 8, 21);
            Assert.That(state.Current.Active, Is.False);
            state.Action(a, 3); state.Action(a, 1);
            Assert.That(state.Current.FirstSeat, Is.EqualTo(3));
            Assert.That(state.Current.CurrentSeat, Is.EqualTo(1));
            state.Begin(b, 9, 1); state.Action(b, 2);
            state.Clear(a); state.Action(a, 0);
            Assert.That(state.Current.Generation, Is.EqualTo(9));
            Assert.That(state.Current.FirstSeat, Is.EqualTo(2));
            state.Clear(b); Assert.That(state.Current.Active, Is.False);
            state.Action(b, 1); Assert.That(state.Current.Active, Is.False);
        }
        [Test] public void NoActionAndLateReadDoNotInventInitiative()
        {
            var state = new CombatPresentationHudState(); var owner = new object();
            state.Begin(owner, 3, 7); state.Action(owner, -1); state.Action(owner, 255);
            Assert.That(state.Current.Active, Is.False);
            state.Action(owner, 1);
            var lateRead = state.Current;
            Assert.That(lateRead.FirstSeat, Is.EqualTo(1));
            Assert.That(lateRead.Sequence, Is.EqualTo(7));
            state.Clear(owner); Assert.That(state.Current.Active, Is.False);
        }
    }
}
