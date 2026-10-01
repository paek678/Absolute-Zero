using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AbsoluteZero.Core.Session
{
    // An SDK request cannot be cancelled by closing a screen/session. Keep its remote
    // identity reserved until it really settles, including re-entry through a new owner.
    internal sealed class LobbyWriteReservations
    {
        internal static readonly LobbyWriteReservations Shared = new();
        readonly Dictionary<(string lobby, string player), Task> _tails = new();

        internal async Task<T> Run<T>(string lobby, string player, Func<Task<T>> send)
        {
            var key = (lobby, player);
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task previous;
            lock (_tails)
            {
                _tails.TryGetValue(key, out previous);
                _tails[key] = done.Task;
            }
            try
            {
                if (previous != null) await previous;
                return await send();
            }
            finally
            {
                lock (_tails)
                {
                    if (_tails.TryGetValue(key, out var tail) && ReferenceEquals(tail, done.Task))
                        _tails.Remove(key);
                }
                done.TrySetResult(true);
            }
        }
    }
}
