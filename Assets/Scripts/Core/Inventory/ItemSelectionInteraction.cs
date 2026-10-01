namespace AbsoluteZero.Core.Inventory
{
    public enum ItemSelectionStage { Idle, Aiming, AwaitingServer, Confirmed, ReadyLocked }

    // Local intent only. Server selection/Ready remain the authority. There is
    // one outstanding submission because the existing response has only CopyId.
    internal sealed class ItemSelectionInteraction
    {
        object _binding;
        int _round, _turn;
        bool _prep, _selected;
        public ItemSelectionStage Stage { get; private set; }
        public uint CopyId { get; private set; }
        public ulong Generation { get; private set; }

        public void Observe(object binding, int round, int turn, bool prep, bool selected, bool ready)
        {
            if (!ReferenceEquals(_binding, binding) || _round != round || _turn != turn || _prep != prep)
            {
                Reset();
                _binding = binding; _round = round; _turn = turn; _prep = prep;
            }
            if (binding == null || !prep) return;
            bool wasSelected = _selected;
            _selected = selected;
            if (ready) { Stage = ItemSelectionStage.ReadyLocked; return; }
            if (selected) { Stage = ItemSelectionStage.Confirmed; return; }
            if (wasSelected || Stage == ItemSelectionStage.ReadyLocked) ResetIntent();
        }

        public bool Aim(uint copy)
        {
            if (!CanStart(copy)) return false;
            Generation++; CopyId = copy; Stage = ItemSelectionStage.Aiming;
            return true;
        }

        public ulong Submit(uint copy)
        {
            if (!CanStart(copy)) return 0;
            Generation++; CopyId = copy; Stage = ItemSelectionStage.AwaitingServer;
            return Generation; // Must be set before dispatch: Host callbacks are synchronous.
        }

        bool CanStart(uint copy) => _binding != null && _prep && copy != 0
            && (Stage == ItemSelectionStage.Idle || Stage == ItemSelectionStage.Aiming);

        public void DispatchFailed(ulong generation)
        {
            if (generation == Generation && Stage == ItemSelectionStage.AwaitingServer) ResetIntent();
        }

        public void Reject(uint copy)
        {
            if (copy != 0 && copy == CopyId && Stage == ItemSelectionStage.AwaitingServer) ResetIntent();
        }

        public void CancelAim()
        {
            if (Stage == ItemSelectionStage.Aiming) ResetIntent();
        }

        public void Reset()
        {
            _binding = null; _selected = false; _prep = false;
            ResetIntent();
        }

        void ResetIntent() { Generation++; CopyId = 0; Stage = ItemSelectionStage.Idle; }
    }
}
