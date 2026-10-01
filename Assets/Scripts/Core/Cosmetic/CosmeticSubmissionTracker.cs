using System;

namespace AbsoluteZero.Core.Cosmetic
{
    public enum CosmeticSubmissionStatus
    {
        WaitingForDependencies, AwaitingAcceptance, Accepted, AcceptedLate,
        DependenciesUnavailable, InvalidLocalData, AcceptanceUnconfirmed, AcceptedDifferent, Cancelled
    }

    /// <summary>One binding, one frozen intent. RPC dispatch is not an acceptance signal.</summary>
    public sealed class CosmeticSubmissionTracker : IDisposable
    {
        const double DependencySeconds = 5, AcceptanceSeconds = 10;
        readonly double _started;
        double _sentAt;
        CosmeticDto _frozen;
        public CosmeticSubmissionStatus Status { get; private set; } = CosmeticSubmissionStatus.WaitingForDependencies;
        public CosmeticSubmissionTracker(double now) { _started = now; }
        public void Tick(double now, CosmeticRegistrySO registry, CosmeticDto candidate, string retained, Action<string> send)
        {
            if (Status == CosmeticSubmissionStatus.Cancelled) return;
            if (Status == CosmeticSubmissionStatus.WaitingForDependencies)
            {
                if (now - _started >= DependencySeconds)
                { Status = CosmeticSubmissionStatus.DependenciesUnavailable; return; }
                if (registry == null || candidate == null)
                {
                    return;
                }
                if (!CosmeticCodec.TryEncode(candidate, registry, out var canonical))
                { Status = CosmeticSubmissionStatus.InvalidLocalData; return; }
                _frozen = new CosmeticSnapshot(candidate, 0).ToDto();
                _sentAt = now;
                Status = CosmeticSubmissionStatus.AwaitingAcceptance;
                Observe(retained);
                if (Status != CosmeticSubmissionStatus.AwaitingAcceptance) return;
                try { send(canonical); }
                catch { Status = CosmeticSubmissionStatus.AcceptanceUnconfirmed; }
            }
            Observe(retained);
            if (Status == CosmeticSubmissionStatus.AwaitingAcceptance && now - _sentAt >= AcceptanceSeconds)
                Status = CosmeticSubmissionStatus.AcceptanceUnconfirmed;
        }
        public void Observe(string retained)
        {
            if (_frozen == null || Status == CosmeticSubmissionStatus.Cancelled || string.IsNullOrEmpty(retained)) return;
            if (!CosmeticCodec.TryParse(retained, out var accepted) || !CosmeticCodec.SameIds(_frozen, accepted))
            { Status = CosmeticSubmissionStatus.AcceptedDifferent; return; }
            bool late = Status == CosmeticSubmissionStatus.AcceptanceUnconfirmed || Status == CosmeticSubmissionStatus.AcceptedLate;
            Status = late ? CosmeticSubmissionStatus.AcceptedLate : CosmeticSubmissionStatus.Accepted;
        }
        public void Dispose() { Status = CosmeticSubmissionStatus.Cancelled; _frozen = null; }
    }
}
