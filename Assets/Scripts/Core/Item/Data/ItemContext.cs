using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Buff;

namespace AbsoluteZero.Core.Item.Data
{
    public class ItemContext
    {
        public PlayerState User;
        public PlayerState Target;
        public PlayerInventory UserInventory;
        public PlayerInventory TargetInventory;

        public int UserIndex;
        public int TargetIndex;

        public ItemSlotNetData UserSlot;
        public byte SlotIndex;

        public TemperatureSystem TempSystem;
        public BuffDebuffSystem BuffSystem;
        public ItemDropTable DropTable;
        public EnvironmentType ActiveEnvironment;
        public PlayerModifiers[] AllModifiers;

        public ref PlayerModifiers UserModifiers => ref AllModifiers[UserIndex];
        public ref PlayerModifiers TargetModifiers => ref AllModifiers[TargetIndex];
    }
}
