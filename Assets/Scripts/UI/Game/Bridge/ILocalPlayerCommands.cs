using System.Threading.Tasks;

namespace AbsoluteZero.UI.Game.Bridge
{
    public interface ILocalPlayerCommands
    {
        bool TrySelectItem(byte slotIndex, uint displayedCopyId);
        bool TrySelectItemWithTarget(byte slotIndex, byte targetSeat, uint displayedCopyId);
        bool TryCancelSelection();
        void PressReady();
        void UseGhostSkill(byte skillIndex, byte targetSeat, uint requestId);
        Task LeaveMatchAsync();
        Task<bool> ReplaySoloAsync();
        void SubmitRematchDecision(bool accept, uint voteEpoch);
    }
}
