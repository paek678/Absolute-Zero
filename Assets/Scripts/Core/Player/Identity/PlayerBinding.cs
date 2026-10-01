using Unity.Netcode;

namespace AbsoluteZero.Core.Player.Identity
{
    public sealed class PlayerBinding
    {
        public PlayerIdentity Identity { get; private set; }
        public MatchParticipantDescriptor Participant { get; private set; }
        public bool HasIdentity => Participant != null;
        public PlayerState State { get; }
        public PlayerInventory Inventory { get; }
        public NetworkObject NetworkObject { get; }
        public ulong NetworkObjectId { get; }

        public bool IsValid => State != null
                               && NetworkObject != null
                               && Inventory != null
                               && NetworkObject.IsSpawned
                               && NetworkObject.NetworkObjectId == NetworkObjectId;

        public PlayerBinding(PlayerState state, PlayerInventory inventory,
                             NetworkObject networkObject)
        {
            State = state;
            Inventory = inventory;
            NetworkObject = networkObject;
            NetworkObjectId = networkObject != null ? networkObject.NetworkObjectId : 0;
        }

        internal void AssignIdentity(MatchParticipantDescriptor participant)
        {
            Participant = participant;
            Identity = new PlayerIdentity(participant, NetworkObjectId);
        }
    }
}
