using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;

namespace AbsoluteZero.Core.Solo
{
    internal interface IBotActionClock { double Now { get; } }

    internal readonly struct BotUseResult
    {
        public PlayerActionStatus Status { get; }
        public PlayerActionReason Reason { get; }
        public ulong OperationId { get; }
        public double DueTime { get; }
        public BotUseResult(PlayerActionStatus status, PlayerActionReason reason = PlayerActionReason.None,
            ulong operationId = 0, double dueTime = 0)
        { Status = status; Reason = reason; OperationId = operationId; DueTime = dueTime; }
    }

    // The boundary reuses PlayerState's legality checks. This object owns only
    // delayed requests, never phases, item effects, inventory or AI decisions.
    internal interface IBotItemUseBoundary
    {
        bool IsCurrent(PrepInputKey key);
        bool TryWindow(PrepInputKey key, out PrepInputSnapshot snapshot);
        PlayerActionResult Validate(PrepInputKey key, uint copy, byte target, out PlayerActionCandidate candidate);
        bool TryDelay(PlayerActionCandidate candidate, out double delay);
        PlayerActionResult Queue(PrepInputKey key, PlayerActionCandidate candidate);
        PlayerActionResult CancelSelection(PrepInputKey key);
        PlayerActionResult Ready(PrepInputKey key);
    }

    internal sealed class BotItemUseOperation : IDisposable
    {
        sealed class Record
        {
            internal PrepInputKey Key;
            internal ulong Id;
            internal uint Copy;
            internal byte Target;
            internal PlayerActionCandidate Candidate;
            internal BotUseResult Result;
        }
        const int HistoryLimit = 64;
        readonly IBotItemUseBoundary _boundary;
        readonly IBotActionClock _clock;
        readonly Dictionary<ulong, Record> _records = new();
        readonly Queue<ulong> _order = new();
        Record _pending;
        ulong _highWater;
        bool _disposed;
        bool _advancing;
        double _lastTime = double.NegativeInfinity;
        public bool HasPending => _pending != null;
        internal int HistoryCount => _records.Count;

        internal BotItemUseOperation(IBotItemUseBoundary boundary, IBotActionClock clock)
        { _boundary = boundary ?? throw new ArgumentNullException(nameof(boundary)); _clock = clock ?? throw new ArgumentNullException(nameof(clock)); }

        public BotUseResult Begin(PrepInputKey key, ulong requestId, uint copy, byte target)
        {
            Tick();
            if (_disposed || !_boundary.IsCurrent(key)) return Stale();
            if (_records.TryGetValue(requestId, out var old))
                return old.Key == key && old.Copy == copy && old.Target == target
                    ? old.Result : Reject(PlayerActionReason.StaleCandidate);
            if (requestId == 0 || requestId <= _highWater) return Stale();
            _highWater = requestId;
            var record = new Record { Key = key, Id = requestId, Copy = copy, Target = target };
            BotUseResult result;
            if (HasPending) result = Reject(PlayerActionReason.BotUsePending);
            else if (!ReadTime(out double now) || !_boundary.TryWindow(key, out var window)
                || now < window.StartTime || now >= window.Deadline)
                result = Reject(PlayerActionReason.InputClosed);
            else
            {
                var valid = _boundary.Validate(key, copy, target, out var candidate);
                if (!valid.Succeeded) result = new BotUseResult(valid.Status, valid.Reason);
                else if (!_boundary.TryDelay(candidate, out double delay) || !Finite(delay) || delay <= 0
                    || !Finite(now + delay)) result = Reject(PlayerActionReason.ItemUnavailable);
                else
                {
                    record.Candidate = candidate;
                    result = new BotUseResult(PlayerActionStatus.Pending, operationId: requestId, dueTime: now + delay);
                    _pending = record;
                }
            }
            record.Result = result;
            _records.Add(requestId, record);
            _order.Enqueue(requestId);
            while (_records.Count > HistoryLimit)
            {
                var oldest = _order.Dequeue();
                if (_records[oldest] == _pending) { _order.Enqueue(oldest); continue; }
                _records.Remove(oldest);
            }
            return result;
        }

        public BotUseResult Poll(PrepInputKey key, ulong operationId)
        {
            Tick();
            if (_disposed || !_boundary.IsCurrent(key)) return Stale();
            return _records.TryGetValue(operationId, out var record) && record.Key == key ? record.Result : Stale();
        }

        public void Tick()
        {
            if (_advancing) return;
            _advancing = true;
            try { Advance(); }
            finally { _advancing = false; }
        }

        void Advance()
        {
            var record = _pending;
            if (record == null || _disposed) return;
            if (!_boundary.IsCurrent(record.Key)) { End(record, PlayerActionStatus.Stale, PlayerActionReason.StaleSession); return; }
            if (!ReadTime(out double now) || !_boundary.TryWindow(record.Key, out var window)
                || now < window.StartTime || now >= window.Deadline)
            { End(record, PlayerActionStatus.Cancelled, PlayerActionReason.InputClosed); return; }
            var valid = _boundary.Validate(record.Key, record.Copy, record.Target, out var current);
            if (!valid.Succeeded || current.ItemData != record.Candidate.ItemData)
            { End(record, PlayerActionStatus.Cancelled, valid.Succeeded ? PlayerActionReason.StaleCandidate : valid.Reason); return; }
            if (now < record.Result.DueTime) return;
            // Revalidation resolves CopyId again after compaction. The production
            // queue boundary checks it again immediately before mutation.
            var queued = _boundary.Queue(record.Key, current);
            End(record, queued.Status == PlayerActionStatus.Queued ? PlayerActionStatus.Queued : PlayerActionStatus.Cancelled, queued.Reason);
        }

        public BotUseResult CancelSelection(PrepInputKey key)
        {
            Tick();
            if (_disposed || !_boundary.IsCurrent(key)) return Stale();
            if (HasPending) return Reject(PlayerActionReason.BotUsePending);
            var result = _boundary.CancelSelection(key);
            return new BotUseResult(result.Status, result.Reason);
        }
        public BotUseResult Ready(PrepInputKey key)
        {
            Tick();
            if (_disposed || !_boundary.IsCurrent(key)) return Stale();
            if (HasPending) return Reject(PlayerActionReason.BotUsePending);
            var result = _boundary.Ready(key);
            return new BotUseResult(result.Status, result.Reason);
        }
        void End(Record record, PlayerActionStatus status, PlayerActionReason reason)
        {
            record.Result = new BotUseResult(status, reason, record.Id, record.Result.DueTime);
            _pending = null;
        }
        bool ReadTime(out double now)
        {
            now = _clock.Now;
            if (!Finite(now) || now < _lastTime) return false;
            _lastTime = now;
            return true;
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static BotUseResult Reject(PlayerActionReason reason) => new(PlayerActionStatus.Rejected, reason);
        static BotUseResult Stale() => new(PlayerActionStatus.Stale, PlayerActionReason.StaleSession);
        internal void AbortPending(PrepInputKey key)
        {
            if (_pending != null && _pending.Key == key)
                End(_pending, PlayerActionStatus.Cancelled, PlayerActionReason.InputClosed);
        }
        public void Dispose() { _disposed = true; _pending = null; _records.Clear(); _order.Clear(); }
    }
}
