using System;

namespace AbsoluteZero.Core.Player.Identity
{
    public readonly struct PlayerIdentity : IEquatable<PlayerIdentity>
    {
        public byte PlayerIndex { get; }
        public string ParticipantId { get; }
        public PlayerControllerKind ControllerKind { get; }
        public ulong? ClientId { get; }
        public long Generation { get; }
        public ulong NetworkObjectId { get; }

        public PlayerIdentity(MatchParticipantDescriptor participant, ulong networkObjectId)
        {
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            PlayerIndex = participant.Seat;
            ParticipantId = participant.ParticipantId;
            ControllerKind = participant.ControllerKind;
            ClientId = participant.ClientId;
            Generation = participant.Generation;
            NetworkObjectId = networkObjectId;
        }

        public bool Equals(PlayerIdentity other)
            => PlayerIndex == other.PlayerIndex && ParticipantId == other.ParticipantId
                && ControllerKind == other.ControllerKind && ClientId == other.ClientId
                && Generation == other.Generation && NetworkObjectId == other.NetworkObjectId;

        public override bool Equals(object obj)
            => obj is PlayerIdentity other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(PlayerIndex, ParticipantId, ControllerKind, ClientId, Generation, NetworkObjectId);

        public override string ToString()
            => $"P{PlayerIndex}/{ParticipantId}/{ControllerKind}@{ClientId?.ToString() ?? "local"}:g{Generation}/o{NetworkObjectId}";

        public static bool operator ==(PlayerIdentity a, PlayerIdentity b) => a.Equals(b);
        public static bool operator !=(PlayerIdentity a, PlayerIdentity b) => !a.Equals(b);
    }
}
