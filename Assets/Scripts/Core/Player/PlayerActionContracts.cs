using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Turn;

namespace AbsoluteZero.Core.Player
{
    public enum PlayerActionStatus : byte
    {
        Rejected,
        Stale,
        Validated,
        Pending,
        Queued,
        Cancelled,
        ReadyAccepted
    }

    public enum PlayerActionReason : byte
    {
        None,
        NotServer,
        ActorNotReady,
        StaleSession,
        WrongController,
        InputClosed,
        AlreadyReady,
        ActorUnavailable,
        MiniGamePending,
        AlreadySelected,
        InvalidSlot,
        CopyMissing,
        ItemUnavailable,
        InvalidTarget,
        CannotUse,
        StaleCandidate,
        BotUsePending,
        NoSelection
    }

    public readonly struct PlayerActionResult
    {
        public PlayerActionStatus Status { get; }
        public PlayerActionReason Reason { get; }
        public bool Succeeded => Status == PlayerActionStatus.Validated
            || Status == PlayerActionStatus.Queued || Status == PlayerActionStatus.Cancelled
            || Status == PlayerActionStatus.ReadyAccepted;

        internal PlayerActionResult(PlayerActionStatus status, PlayerActionReason reason = PlayerActionReason.None)
        { Status = status; Reason = reason; }
        internal static PlayerActionResult Reject(PlayerActionReason reason)
            => new(PlayerActionStatus.Rejected, reason);
        internal static PlayerActionResult Stale(PlayerActionReason reason)
            => new(PlayerActionStatus.Stale, reason);
        public override string ToString() => Reason == PlayerActionReason.None ? Status.ToString() : $"{Status}: {Reason}";
    }

    // An inspected copy, not a reservation or a command capability. Queueing must
    // revalidate membership, the turn key, CopyId, target and current CanUse state.
    internal readonly struct PlayerActionCandidate
    {
        internal PlayerBinding Binding { get; }
        internal PrepInputKey? PrepKey { get; }
        public byte SlotIndex { get; }
        public uint CopyId { get; }
        public byte TargetSeat { get; }
        public ItemDataSO ItemData { get; }

        internal PlayerActionCandidate(PlayerBinding binding, byte slotIndex, uint copyId,
            byte targetSeat, ItemDataSO itemData, PrepInputKey? prepKey)
        {
            Binding = binding; SlotIndex = slotIndex; CopyId = copyId;
            TargetSeat = targetSeat; ItemData = itemData; PrepKey = prepKey;
        }
    }
}
