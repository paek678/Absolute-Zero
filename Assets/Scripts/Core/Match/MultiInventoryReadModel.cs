using System;
using AbsoluteZero.Core.Item;
using Unity.Netcode;

namespace AbsoluteZero.Core.Match
{
    /// <summary>Adopts whole validated snapshots before notifying scene presenters.</summary>
    public sealed class MultiInventoryReadModel
    {
        MultiInventorySnapshotNetData _current;
        public event Action Changed;
        public bool HasView => _current.Revision != 0;
        public uint MatchEpoch => _current.MatchEpoch;
        public uint RoundEpoch => _current.RoundEpoch;
        public uint Revision => _current.Revision;
        public uint CommittedGrantTransaction => _current.CommittedGrantTransaction;
        public bool IsFaulted => _current.Faulted != 0;
        public bool HasCommittedGrant(uint roundEpoch, uint transactionId)
            => HasView && _current.RoundEpoch == roundEpoch
                && _current.CommittedGrantTransaction >= transactionId;

        public bool IsBlockedForGrant(uint matchEpoch, uint roundEpoch,
            DeathmatchGrantNetData grant)
            => !HasView || IsFaulted || MatchEpoch != matchEpoch || RoundEpoch != roundEpoch
                || (grant.RoundEpoch == roundEpoch
                    && (grant.Stage == DeathmatchGrantStage.Pending
                        || grant.Stage == DeathmatchGrantStage.Failed
                        || (grant.Stage == DeathmatchGrantStage.Completed
                            && !HasCommittedGrant(roundEpoch, grant.TransactionId))));

        public void Clear()
        {
            _current = default;
            Changed?.Invoke();
        }

        // A grant descriptor can arrive after its complete inventory snapshot.
        // Re-evaluate presenters when that external part of the gate changes.
        public void NotifyGrantChanged() => Changed?.Invoke();

        public bool Adopt(MultiInventorySnapshotNetData incoming, int requiredSeats)
        {
            if (!incoming.IsValid(requiredSeats)) return false;
            if (HasView)
            {
                if (incoming.MatchEpoch < _current.MatchEpoch) return false;
                if (incoming.MatchEpoch == _current.MatchEpoch)
                {
                    if (incoming.RoundEpoch < _current.RoundEpoch) return false;
                    if (incoming.RoundEpoch == _current.RoundEpoch
                        && incoming.Revision <= _current.Revision) return false;
                }
            }
            _current = incoming;
            Changed?.Invoke();
            return true;
        }

        public bool HasSeat(int seat) => HasView && _current.HasSeat(seat);
        public int GetCount(int seat) => HasSeat(seat) ? _current.GetCount(seat) : 0;
        public ItemSlotNetData GetSlot(int seat, int index)
            => HasView ? _current.GetSlot(seat, index) : ItemSlotNetData.Empty;
    }
}
