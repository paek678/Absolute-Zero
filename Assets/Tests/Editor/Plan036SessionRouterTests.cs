using System;
using System.Threading.Tasks;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Session;
using NUnit.Framework;

namespace AbsoluteZero.Tests
{
    public class Plan036SessionRouterTests
    {
        [Test]
        public void AcquisitionIsExclusiveAndModesShareOneOwner()
        {
            var router = new MatchSessionRouter();
            int cleanupCalls = 0;
            Assert.That(router.TryBegin(GameMode.Solo, () => { cleanupCalls++; return Task.CompletedTask; }, out var solo), Is.True);
            Assert.That(solo.Mode, Is.EqualTo(GameMode.Solo));
            Assert.That(router.Current.Generation, Is.EqualTo(solo.Generation));
            Assert.That(router.IsSolo, Is.True); Assert.That(router.IsStopping, Is.False);
            Assert.That(router.Owns(solo) && router.IsCurrent(solo), Is.True);
            Assert.That(router.TryBegin(GameMode.OneVsOne, () => Task.CompletedTask, out _), Is.False);
            Assert.That(router.TryBegin(GameMode.Multi, () => Task.CompletedTask, out _), Is.False);
            Assert.That(cleanupCalls, Is.Zero);
            router.End(solo);
            Assert.That(router.TryBegin(GameMode.OneVsOne, () => Task.CompletedTask, out var online), Is.True);
            Assert.That(router.IsSolo, Is.False); Assert.That(router.IsCurrent(online), Is.True);
            Assert.That(online.Generation, Is.GreaterThan(solo.Generation));
        }

        [Test]
        public async Task StopInvalidatesImmediatelyRetainsOwnershipUntilCleanupAndRunsOnce()
        {
            var router = new MatchSessionRouter();
            var cleanup = new TaskCompletionSource<bool>();
            int cleanupCalls = 0;
            Assert.That(router.TryBegin(GameMode.Solo, () =>
            {
                cleanupCalls++;
                Assert.That(router.IsCurrent(router.Current), Is.False, "No completion may apply once stopping starts.");
                Assert.That(router.Owns(router.Current), Is.True, "Cleanup still owns its session resources.");
                return cleanup.Task;
            }, out var lease), Is.True);

            var first = router.StopAsync();
            var second = router.StopAsync();
            try
            {
                Assert.That(router.IsStopping, Is.True);
                Assert.That(router.IsCurrent(lease), Is.False); Assert.That(router.Owns(lease), Is.True);
                Assert.That(first.IsCompleted || second.IsCompleted, Is.False);
                Assert.That(cleanupCalls, Is.EqualTo(1));
                Assert.That(router.TryBegin(GameMode.Multi, () => Task.CompletedTask, out _), Is.False);
            }
            finally { cleanup.TrySetResult(true); }
            await Task.WhenAll(first, second);
            Assert.That(router.Owns(lease) || router.IsCurrent(lease) || router.IsStopping, Is.False);
            Assert.That(router.TryBegin(GameMode.Multi, () => Task.CompletedTask, out _), Is.True);
            Assert.That(cleanupCalls, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task FailedCleanupRetainsInvalidatedOwnerAndCanBeRetried(bool synchronousFailure)
        {
            var router = new MatchSessionRouter();
            int calls = 0;
            Assert.That(router.TryBegin(GameMode.Solo, () =>
            {
                if (++calls > 1) return Task.CompletedTask;
                if (synchronousFailure) throw new InvalidOperationException("Cleanup failed.");
                return Task.FromException(new InvalidOperationException("Cleanup failed."));
            }, out var lease), Is.True);

            Assert.ThrowsAsync<InvalidOperationException>(async () => await router.StopAsync());
            Assert.That(router.IsCurrent(lease), Is.False);
            Assert.That(router.Owns(lease), Is.True);
            Assert.That(router.TryBegin(GameMode.OneVsOne, () => Task.CompletedTask, out _), Is.False);
            await router.StopAsync();
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(router.Owns(lease) || router.IsCurrent(lease) || router.IsStopping, Is.False);
            Assert.That(router.TryBegin(GameMode.Solo, () => Task.CompletedTask, out _), Is.True);
        }

        [Test]
        public async Task ConcurrentFailedStopsDoNotMultiplyCleanupAndNextStopRetries()
        {
            var router = new MatchSessionRouter();
            var failedCleanup = new TaskCompletionSource<bool>();
            int calls = 0;
            Assert.That(router.TryBegin(GameMode.Solo, () => ++calls == 1 ? failedCleanup.Task : Task.CompletedTask, out var lease), Is.True);
            Task first = router.StopAsync(); Task second = router.StopAsync();
            Assert.That(calls, Is.EqualTo(1));
            failedCleanup.SetException(new InvalidOperationException("First cleanup attempt failed."));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await first);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await second);
            Assert.That(router.Owns(lease), Is.True); Assert.That(router.IsCurrent(lease), Is.False);
            await router.StopAsync();
            Assert.That(calls, Is.EqualTo(2)); Assert.That(router.Owns(lease), Is.False);
        }

        [Test]
        public async Task OldLeaseCannotReleaseOrBecomeCurrentForANewerSession()
        {
            var router = new MatchSessionRouter();
            Assert.That(router.TryBegin(GameMode.Solo, () => Task.CompletedTask, out var old), Is.True);
            await router.StopAsync();
            Assert.That(router.TryBegin(GameMode.OneVsOne, () => Task.CompletedTask, out var current), Is.True);
            router.End(old); router.End(old);
            Assert.That(router.IsCurrent(old) || router.Owns(old), Is.False);
            Assert.That(router.Owns(current) && router.IsCurrent(current), Is.True);
            Assert.That(current.Generation, Is.GreaterThan(old.Generation));
            Assert.That(router.TryBegin(GameMode.Solo, () => Task.CompletedTask, out _), Is.False);
        }

        [Test]
        public async Task IdleAndRepeatedStopsAreNoOpsAfterSuccessfulCleanup()
        {
            var router = new MatchSessionRouter();
            await router.StopAsync();
            int calls = 0;
            Assert.That(router.TryBegin(GameMode.Solo, () => { calls++; return Task.CompletedTask; }, out var lease), Is.True);
            await router.StopAsync(); await router.StopAsync();
            router.End(lease);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(router.IsStopping || router.IsSolo || router.Owns(lease), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task StopCompletionAllowsImmediateStartOrRetryFromItsContinuation(bool failure)
        {
            var router = new MatchSessionRouter();
            var cleanup = new TaskCompletionSource<bool>();
            int calls = 0;
            Assert.That(router.TryBegin(GameMode.Solo, () => ++calls == 1 ? cleanup.Task : Task.CompletedTask, out _), Is.True);
            Task stopped = router.StopAsync();
            bool acquired = false;
            Task retry = null;
            Task continuation = stopped.ContinueWith(completed =>
            {
                if (failure)
                {
                    _ = completed.Exception;
                    retry = router.StopAsync();
                }
                else acquired = router.TryBegin(GameMode.OneVsOne, () => Task.CompletedTask, out _);
            }, default, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            if (failure) cleanup.SetException(new InvalidOperationException("Retry immediately after failure."));
            else cleanup.SetResult(true);
            await continuation;
            if (failure)
            {
                Assert.That(calls, Is.EqualTo(2), "Completed failed stop must not be returned as the next cleanup attempt.");
                await retry;
            }
            else Assert.That(acquired, Is.True, "Successful stop completion must expose an available router immediately.");
        }
    }
}
