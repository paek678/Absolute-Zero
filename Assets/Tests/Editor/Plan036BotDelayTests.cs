using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Turn;
using NUnit.Framework;

namespace AbsoluteZero.Tests
{
    public sealed class Plan036BotDelayTests
    {
        sealed class Clock : IBotActionClock { public double Time = 10; public double Now => Time; }
        sealed class Boundary : IBotItemUseBoundary
        {
            public PrepInputKey Key = new(1, 1, 1, 1);
            public bool Current = true, Open = true, Available = true, Queued;
            public int QueueCount, ReadyCount;
            public System.Action OnQueue;
            public double Delay = 2;
            public bool IsCurrent(PrepInputKey key) => Current && key == Key;
            public bool TryWindow(PrepInputKey key, out PrepInputSnapshot snapshot)
            { snapshot = new PrepInputSnapshot(Key, 10, 20, 10, new float[] { 37, 37 }); return Open; }
            public PlayerActionResult Validate(PrepInputKey key, uint copy, byte target, out PlayerActionCandidate candidate)
            {
                candidate = new PlayerActionCandidate(null, 0, copy, target, null, key);
                return new PlayerActionResult(Available && !Queued ? PlayerActionStatus.Validated : PlayerActionStatus.Rejected,
                    Available ? PlayerActionReason.AlreadySelected : PlayerActionReason.CopyMissing);
            }
            public bool TryDelay(PlayerActionCandidate candidate, out double delay) { delay = Delay; return true; }
            public PlayerActionResult Queue(PrepInputKey key, PlayerActionCandidate candidate)
            { QueueCount++; Queued = true; OnQueue?.Invoke(); return new PlayerActionResult(PlayerActionStatus.Queued); }
            public PlayerActionResult CancelSelection(PrepInputKey key)
            { Queued = false; return new PlayerActionResult(PlayerActionStatus.Cancelled); }
            public PlayerActionResult Ready(PrepInputKey key)
            { ReadyCount++; return new PlayerActionResult(PlayerActionStatus.ReadyAccepted); }
        }
        Clock _clock;
        Boundary _boundary;
        BotItemUseOperation _use;
        [SetUp] public void Setup() { _clock = new Clock(); _boundary = new Boundary(); _use = new BotItemUseOperation(_boundary, _clock); }
        [TearDown] public void Cleanup() => _use.Dispose();

        [Test] public void DelayAndDuplicateCompletionQueueExactlyOnce()
        {
            var start = _use.Begin(_boundary.Key, 1, 9, 0);
            Assert.That(start.Status, Is.EqualTo(PlayerActionStatus.Pending));
            Assert.That(start.DueTime, Is.EqualTo(12));
            _clock.Time = 11.999;
            Assert.That(_use.Begin(_boundary.Key, 1, 9, 0).DueTime, Is.EqualTo(12));
            Assert.That(_use.Poll(_boundary.Key, 1).Status, Is.EqualTo(PlayerActionStatus.Pending));
            Assert.That(_boundary.QueueCount, Is.Zero);
            _clock.Time = 12;
            Assert.That(_use.Poll(_boundary.Key, 1).Status, Is.EqualTo(PlayerActionStatus.Queued));
            Assert.That(_use.Begin(_boundary.Key, 1, 9, 0).Status, Is.EqualTo(PlayerActionStatus.Queued));
            _use.Tick();
            Assert.That(_boundary.QueueCount, Is.EqualTo(1));
        }
        [Test] public void PendingCannotBeBypassedByReadyCancelOrSecondRequest()
        {
            _use.Begin(_boundary.Key, 1, 9, 0);
            Assert.That(_use.Ready(_boundary.Key).Reason, Is.EqualTo(PlayerActionReason.BotUsePending));
            Assert.That(_use.CancelSelection(_boundary.Key).Reason, Is.EqualTo(PlayerActionReason.BotUsePending));
            Assert.That(_use.Begin(_boundary.Key, 2, 10, 0).Reason, Is.EqualTo(PlayerActionReason.BotUsePending));
            Assert.That(_boundary.ReadyCount, Is.Zero);
            Assert.That(_boundary.QueueCount, Is.Zero);
        }
        [Test] public void SynchronousQueueNotificationCannotReenterCompletion()
        {
            _use.Begin(_boundary.Key, 1, 9, 0);
            _boundary.OnQueue = () => Assert.That(_use.Poll(_boundary.Key, 1).Status, Is.EqualTo(PlayerActionStatus.Pending));
            _clock.Time = 12;
            Assert.That(_use.Poll(_boundary.Key, 1).Status, Is.EqualTo(PlayerActionStatus.Queued));
            Assert.That(_boundary.QueueCount, Is.EqualTo(1));
        }
        [Test] public void DelayCannotExtendPrepAndLateBeginIsRejected()
        {
            _clock.Time = 19;
            Assert.That(_use.Begin(_boundary.Key, 1, 9, 0).DueTime, Is.EqualTo(21));
            _clock.Time = 20;
            Assert.That(_use.Poll(_boundary.Key, 1).Status, Is.EqualTo(PlayerActionStatus.Cancelled));
            Assert.That(_use.Begin(_boundary.Key, 2, 9, 0).Reason, Is.EqualTo(PlayerActionReason.InputClosed));
            Assert.That(_boundary.QueueCount, Is.Zero);
        }
        [TestCase(19.999, PlayerActionStatus.Queued)]
        [TestCase(20, PlayerActionStatus.Cancelled)]
        [TestCase(20.001, PlayerActionStatus.Cancelled)]
        [TestCase(double.NaN, PlayerActionStatus.Cancelled)]
        [TestCase(double.PositiveInfinity, PlayerActionStatus.Cancelled)]
        [TestCase(9, PlayerActionStatus.Cancelled)]
        public void CompletionHasStrictDeadlineAndFiniteMonotonicClock(double completion, PlayerActionStatus expected)
        {
            _use.Begin(_boundary.Key, 1, 9, 0);
            _clock.Time = completion;
            Assert.That(_use.Poll(_boundary.Key, 1).Status, Is.EqualTo(expected));
            Assert.That(_boundary.QueueCount, Is.EqualTo(expected == PlayerActionStatus.Queued ? 1 : 0));
        }
        [Test] public void MissingCopyCancelsEvenBeforeDueAndNeverResurrects()
        {
            _use.Begin(_boundary.Key, 1, 9, 0);
            _boundary.Available = false;
            Assert.That(_use.Poll(_boundary.Key, 1).Status, Is.EqualTo(PlayerActionStatus.Cancelled));
            _boundary.Available = true; _clock.Time = 15;
            Assert.That(_use.Poll(_boundary.Key, 1).Status, Is.EqualTo(PlayerActionStatus.Cancelled));
            Assert.That(_boundary.QueueCount, Is.Zero);
        }
        [Test] public void ControllerStopCancelsPendingButAllowsFreshRequestWithFullDelay()
        {
            _use.Begin(_boundary.Key, 1, 9, 0);
            _use.AbortPending(_boundary.Key); _clock.Time = 13;
            Assert.That(_use.Poll(_boundary.Key, 1).Status, Is.EqualTo(PlayerActionStatus.Cancelled));
            Assert.That(_boundary.QueueCount, Is.Zero);
            Assert.That(_use.Begin(_boundary.Key, 2, 9, 0).DueTime, Is.EqualTo(15));
        }
        [Test] public void OldControllerKeyCannotCancelNewTurnPending()
        {
            _use.Begin(_boundary.Key, 1, 9, 0);
            _use.AbortPending(new PrepInputKey(9, 9, 9, 9)); _clock.Time = 12;
            Assert.That(_use.Poll(_boundary.Key, 1).Status, Is.EqualTo(PlayerActionStatus.Queued));
        }
        [Test] public void ClosedPrepCancelsWithoutWaitingForDue()
        {
            _use.Begin(_boundary.Key, 1, 9, 0); _boundary.Open = false;
            Assert.That(_use.Poll(_boundary.Key, 1).Status, Is.EqualTo(PlayerActionStatus.Cancelled));
            Assert.That(_boundary.QueueCount, Is.Zero);
        }
        [Test] public void PreviousTurnAndDisposedActorCannotComplete()
        {
            var key = _boundary.Key; _use.Begin(key, 1, 9, 0);
            _boundary.Key = new PrepInputKey(1, 1, 2, 2); _clock.Time = 15;
            Assert.That(_use.Poll(key, 1).Status, Is.EqualTo(PlayerActionStatus.Stale));
            Assert.That(_boundary.QueueCount, Is.Zero);
            _use.Dispose();
            Assert.That(_use.Ready(_boundary.Key).Status, Is.EqualTo(PlayerActionStatus.Stale));
        }
        [Test] public void RequestPayloadCannotChangeAndOldEvictedIdCannotExecute()
        {
            _use.Begin(_boundary.Key, 1, 9, 0);
            Assert.That(_use.Begin(_boundary.Key, 1, 10, 0).Status, Is.EqualTo(PlayerActionStatus.Rejected));
            Assert.That(_use.Begin(_boundary.Key, 1, 9, 1).Status, Is.EqualTo(PlayerActionStatus.Rejected));
            for (ulong id = 2; id < 100; id++) _use.Begin(_boundary.Key, id, 9, 0);
            Assert.That(_use.HistoryCount, Is.EqualTo(64));
            Assert.That(_use.Poll(_boundary.Key, 1).Status, Is.EqualTo(PlayerActionStatus.Pending));
            Assert.That(_use.Begin(_boundary.Key, 2, 9, 0).Status, Is.EqualTo(PlayerActionStatus.Stale));
            Assert.That(_boundary.QueueCount, Is.Zero);
        }
        [Test] public void CancelledQueuedSelectionRequiresFullNewDelay()
        {
            _use.Begin(_boundary.Key, 1, 9, 0); _clock.Time = 12; _use.Tick();
            _use.CancelSelection(_boundary.Key);
            Assert.That(_use.Begin(_boundary.Key, 2, 9, 0).DueTime, Is.EqualTo(14));
            _clock.Time = 13.999; _use.Tick(); Assert.That(_boundary.QueueCount, Is.EqualTo(1));
            _clock.Time = 14; _use.Tick(); Assert.That(_boundary.QueueCount, Is.EqualTo(2));
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void InvalidDelayCannotQueue(double delay)
        {
            _boundary.Delay = delay;
            Assert.That(_use.Begin(_boundary.Key, 1, 9, 0).Status, Is.EqualTo(PlayerActionStatus.Rejected));
            Assert.That(_boundary.QueueCount, Is.Zero);
        }
    }
}
