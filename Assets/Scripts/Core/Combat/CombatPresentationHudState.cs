using System;

namespace AbsoluteZero.Core.Combat
{
    public readonly struct CombatPresentationHudSnapshot
    {
        public readonly long Generation;
        public readonly uint Sequence;
        public readonly int FirstSeat, CurrentSeat;
        public readonly bool Active;
        public CombatPresentationHudSnapshot(long generation, uint sequence, int first, int current, bool active)
        { Generation = generation; Sequence = sequence; FirstSeat = first; CurrentSeat = current; Active = active; }
    }

    /// <summary>Retains only presentation state. The actual playback supplies actions, never a second order calculation.</summary>
    public sealed class CombatPresentationHudState
    {
        object _owner;
        public CombatPresentationHudSnapshot Current { get; private set; }
        public event Action<CombatPresentationHudSnapshot> Changed;
        public void Begin(object owner, long generation, uint sequence)
        {
            _owner = owner;
            Publish(new CombatPresentationHudSnapshot(generation, sequence, -1, -1, false));
        }
        public void Action(object owner, int seat)
        {
            if (owner == null || !ReferenceEquals(_owner, owner) || seat < 0 || seat > 3) return;
            Publish(new CombatPresentationHudSnapshot(Current.Generation, Current.Sequence,
                Current.Active ? Current.FirstSeat : seat, seat, true));
        }
        public void Clear(object owner)
        {
            if (owner == null || !ReferenceEquals(_owner, owner)) return;
            _owner = null; Publish(default);
        }
        void Publish(CombatPresentationHudSnapshot value) { Current = value; Changed?.Invoke(value); }
    }
}
