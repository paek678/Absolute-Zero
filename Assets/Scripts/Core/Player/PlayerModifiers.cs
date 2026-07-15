using AbsoluteZero.Core.Item;

namespace AbsoluteZero.Core.Player
{
    public struct PlayerModifiers
    {
        public bool BasicItemsBlocked;
        public bool ActionNeutralized;
        public DefenseInfo? ActiveDefense;
        public bool HasExtraAction;
        public bool OpponentRevealed;

        public void Reset()
        {
            BasicItemsBlocked = false;
            ActionNeutralized = false;
            ActiveDefense = null;
            HasExtraAction = false;
            OpponentRevealed = false;
        }
    }

    public struct DefenseInfo
    {
        public DamageFilter Filter;
        public float BlockAmount;
    }
}
