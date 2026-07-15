using AbsoluteZero.Core.Item.Data;

namespace AbsoluteZero.Core.Player
{
    public class ActionQueue
    {
        public QueuedAction? selectedAction = null;
        public QueuedAction? subAction = null;
        public float readyTimestamp;
        public bool isReady;
        public bool hasUsedSub;

        public void SetSelected(byte slotIndex, ItemDataSO itemData)
        {
            selectedAction = new QueuedAction(slotIndex, itemData);
        }

        public void SetSub(byte slotIndex, ItemDataSO itemData)
        {
            subAction = new QueuedAction(slotIndex, itemData);
            hasUsedSub = true;
        }

        public void SetReady(float timestamp)
        {
            readyTimestamp = timestamp;
            isReady = true;
        }

        public void Clear()
        {
            selectedAction = null;
            subAction = null;
            readyTimestamp = 0f;
            isReady = false;
            hasUsedSub = false;
        }
    }

    public struct QueuedAction
    {
        public byte SlotIndex;
        public ItemDataSO ItemData;

        public QueuedAction(byte slotIndex, ItemDataSO itemData)
        {
            SlotIndex = slotIndex;
            ItemData = itemData;
        }
    }
}
