using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;

namespace AbsoluteZero.Core.Player.Identity
{
    public enum PlayerControllerKind : byte { Human, Bot }

    // Match identity is independent of the NGO object owner. An unbound human can
    // remain in a roster, whereas a live local bot never receives a client ID.
    public sealed class MatchParticipantDescriptor : IEquatable<MatchParticipantDescriptor>
    {
        public string ParticipantId { get; }
        public byte Seat { get; }
        public PlayerControllerKind ControllerKind { get; }
        public ulong? ClientId { get; }
        public long Generation { get; }

        public MatchParticipantDescriptor(string participantId, byte seat,
            PlayerControllerKind controllerKind, ulong? clientId, long generation)
        {
            if (string.IsNullOrWhiteSpace(participantId) || Encoding.UTF8.GetByteCount(participantId) > 125)
                throw new ArgumentException("Participant ID must fit FixedString128Bytes.", nameof(participantId));
            if (seat >= 4 || generation < 0) throw new ArgumentOutOfRangeException();
            if (!Enum.IsDefined(typeof(PlayerControllerKind), controllerKind))
                throw new ArgumentOutOfRangeException(nameof(controllerKind));
            if (controllerKind == PlayerControllerKind.Bot && clientId.HasValue)
                throw new ArgumentException("A bot has no transport client ID.", nameof(clientId));
            ParticipantId = participantId;
            Seat = seat;
            ControllerKind = controllerKind;
            ClientId = clientId;
            Generation = generation;
        }

        public bool Equals(MatchParticipantDescriptor other)
            => other != null && ParticipantId == other.ParticipantId && Seat == other.Seat
                && ControllerKind == other.ControllerKind && ClientId == other.ClientId
                && Generation == other.Generation;
        public override bool Equals(object obj) => Equals(obj as MatchParticipantDescriptor);
        public override int GetHashCode() => HashCode.Combine(ParticipantId, Seat, ControllerKind, ClientId, Generation);

        public static bool TryValidateSet(IReadOnlyList<MatchParticipantDescriptor> participants,
            int requiredCount, out MatchParticipantDescriptor[] bySeat, out string error)
        {
            bySeat = null;
            error = null;
            if (participants == null || requiredCount < 1 || requiredCount > 4 || participants.Count != requiredCount)
            { error = "Participant count does not match the required seat set."; return false; }
            var result = new MatchParticipantDescriptor[requiredCount];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var clients = new HashSet<ulong>();
            long? generation = null;
            foreach (var p in participants)
            {
                if (p == null || p.Seat >= requiredCount || result[p.Seat] != null || !ids.Add(p.ParticipantId))
                { error = "Participant, seat or identity is missing or duplicated."; return false; }
                if (generation.HasValue && generation.Value != p.Generation)
                { error = "Participants belong to different session generations."; return false; }
                if (p.ClientId.HasValue && !clients.Add(p.ClientId.Value))
                { error = "A human connection is assigned to multiple participants."; return false; }
                generation = p.Generation;
                result[p.Seat] = p;
            }
            bySeat = result;
            return true;
        }
    }

    // Publish one value so a client never promotes a mixed role/seat/generation.
    // Generation is the server's value, not the receiving client's router counter.
    public struct ParticipantMetadataNetData : INetworkSerializable, IEquatable<ParticipantMetadataNetData>
    {
        public bool Assigned;
        public FixedString128Bytes ParticipantId;
        public byte Seat;
        public PlayerControllerKind ControllerKind;
        public bool HasClientId;
        public ulong ClientId;
        public long Generation;

        public ParticipantMetadataNetData(MatchParticipantDescriptor participant)
        {
            Assigned = true;
            ParticipantId = new FixedString128Bytes(participant.ParticipantId);
            Seat = participant.Seat;
            ControllerKind = participant.ControllerKind;
            HasClientId = participant.ClientId.HasValue;
            ClientId = participant.ClientId.GetValueOrDefault();
            Generation = participant.Generation;
        }

        public MatchParticipantDescriptor ToDescriptor() => Assigned
            ? new MatchParticipantDescriptor(ParticipantId.ToString(), Seat, ControllerKind,
                HasClientId ? ClientId : (ulong?)null, Generation) : null;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Assigned);
            serializer.SerializeValue(ref ParticipantId);
            serializer.SerializeValue(ref Seat);
            serializer.SerializeValue(ref ControllerKind);
            serializer.SerializeValue(ref HasClientId);
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Generation);
        }

        public bool Equals(ParticipantMetadataNetData other)
            => Assigned == other.Assigned && ParticipantId.Equals(other.ParticipantId) && Seat == other.Seat
                && ControllerKind == other.ControllerKind && HasClientId == other.HasClientId
                && (!HasClientId || ClientId == other.ClientId) && Generation == other.Generation;
        public override bool Equals(object obj) => obj is ParticipantMetadataNetData other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Assigned, ParticipantId, Seat, ControllerKind,
            HasClientId, HasClientId ? ClientId : 0, Generation);
    }
}
