using System.Collections.Generic;

namespace AbsoluteZero.Core.Turn
{
    // RPC authority/epoch checks remain in TurnManager before consulting this cache.
    // Bounded response history is cleared at new-match/despawn, not on new rounds.
    internal sealed class GhostRequestHistory
    {
        const int HistoryLimit = 32;
        readonly Dictionary<ulong, Dictionary<uint, GhostSkillRequestResult>> _results = new();
        readonly Dictionary<ulong, Queue<uint>> _order = new();
        readonly Dictionary<ulong, uint> _highWater = new();
        internal bool TryGet(ulong sender, uint request, out GhostSkillRequestResult result)
        {
            result = default;
            return _results.TryGetValue(sender, out var results) && results.TryGetValue(request, out result);
        }
        internal bool IsStale(ulong sender, uint request)
            => _highWater.TryGetValue(sender, out uint highWater) && request <= highWater;
        internal void Record(ulong sender, uint request, GhostSkillRequestResult result)
        {
            if (!_results.TryGetValue(sender, out var results))
                _results[sender] = results = new Dictionary<uint, GhostSkillRequestResult>();
            if (!_order.TryGetValue(sender, out var order))
                _order[sender] = order = new Queue<uint>();
            results[request] = result;
            order.Enqueue(request);
            _highWater[sender] = request;
            while (order.Count > HistoryLimit) results.Remove(order.Dequeue());
        }
        internal void Clear()
        { _results.Clear(); _order.Clear(); _highWater.Clear(); }
    }
}
