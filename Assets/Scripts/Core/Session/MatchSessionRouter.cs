using System;
using System.Threading.Tasks;
using AbsoluteZero.Core.Network;

namespace AbsoluteZero.Core.Session
{
    public sealed class MatchSessionLease
    {
        public long Generation { get; }
        public GameMode Mode { get; }
        internal MatchSessionLease(long generation, GameMode mode) { Generation = generation; Mode = mode; }
    }

    // One application owns this router. Work validity ends before asynchronous cleanup,
    // while exclusive ownership remains until cleanup succeeds.
    public sealed class MatchSessionRouter
    {
        long _generation;
        Func<Task> _stop;
        Task _stopping;
        public MatchSessionLease Current { get; private set; }
        public MatchLaunchContext LaunchContext { get; private set; }
        public bool IsStopping { get; private set; }
        public bool IsSolo => Current?.Mode == GameMode.Solo;
        public event Action Changed;

        public bool TryBegin(GameMode mode, Func<Task> stop, out MatchSessionLease lease)
        {
            lease = null;
            if (Current != null || _stopping != null || stop == null || !Enum.IsDefined(typeof(GameMode), mode)) return false;
            Current = lease = new MatchSessionLease(checked(++_generation), mode);
            _stop = stop;
            Changed?.Invoke();
            return true;
        }

        public bool Owns(MatchSessionLease lease) => lease != null && ReferenceEquals(Current, lease);
        public bool IsCurrent(MatchSessionLease lease) => Owns(lease) && !IsStopping;

        public bool BindLaunchContext(MatchSessionLease lease, MatchLaunchContext context)
        {
            if (!IsCurrent(lease) || context == null || context.Generation != lease.Generation
                || context.Mode != lease.Mode || LaunchContext != null) return false;
            LaunchContext = context;
            return true;
        }

        public bool End(MatchSessionLease lease)
        {
            if (!Owns(lease)) return false;
            Current = null;
            LaunchContext = null;
            _stop = null;
            IsStopping = false;
            Changed?.Invoke();
            return true;
        }

        public Task StopAsync()
        {
            if (_stopping != null) return _stopping;
            if (Current == null) return Task.CompletedTask;
            var lease = Current;
            var stop = _stop;
            var completion = new TaskCompletionSource<bool>();
            _stopping = completion.Task;
            IsStopping = true;
            Changed?.Invoke();
            _ = StopOwnedAsync(lease, stop, completion);
            return completion.Task;
        }

        async Task StopOwnedAsync(MatchSessionLease lease, Func<Task> stop, TaskCompletionSource<bool> completion)
        {
            try
            {
                await stop();
                End(lease);
                if (ReferenceEquals(_stopping, completion.Task)) _stopping = null;
                completion.TrySetResult(true);
            }
            catch (Exception error)
            {
                // Keep the failed owner invalidated. Starting another network session
                // would be unsafe until a retry has completed shutdown.
                if (ReferenceEquals(_stopping, completion.Task)) _stopping = null;
                completion.TrySetException(error);
            }
            finally
            {
                if (ReferenceEquals(_stopping, completion.Task)) _stopping = null;
            }
        }
    }
}
