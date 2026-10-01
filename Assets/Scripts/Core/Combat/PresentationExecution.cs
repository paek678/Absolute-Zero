using System;
using System.Collections;

namespace AbsoluteZero.Core.Combat
{
    // The manager owns transitions and ACKs; this object owns one execution's lifetime only.
    internal sealed class PresentationExecution
    {
        readonly Func<bool> _isCurrent;
        public uint Sequence { get; }
        public CombatPresentationContext Context { get; }
        public PresentationResources Resources { get; }
        public bool Settled { get; private set; }
        public bool CanContinue => !Settled && _isCurrent();

        internal PresentationExecution(uint sequence, CombatPresentationContext context,
            PresentationResources resources, Func<bool> isCurrent)
        { Sequence = sequence; Context = context; Resources = resources; _isCurrent = isCurrent; }

        public bool TrySettle()
        {
            if (Settled) return false;
            Settled = true;
            Resources.Dispose();
            return true;
        }

        public IEnumerator Guard(IEnumerator routine, Func<bool> bindingsCurrent = null)
        {
            try
            {
                while (CanContinue && (bindingsCurrent == null || bindingsCurrent()))
                {
                    if (!routine.MoveNext()) yield break;
                    // Unity drives nested enumerators; wrap them too, preserving the yielded waits.
                    yield return routine.Current is IEnumerator child
                        ? Guard(child, bindingsCurrent) : routine.Current;
                }
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }
    }
}
