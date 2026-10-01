using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Session
{
    // NGO preserves every enabled manager in DontDestroyOnLoad, not just its
    // Singleton. Reject the lobby's new copy before NGO OnEnable on menu return.
    [DefaultExecutionOrder(-10000)]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class PersistentNetworkRoot : MonoBehaviour
    {
        static PersistentNetworkRoot _owner;
        void Awake()
        {
            var manager = GetComponent<NetworkManager>();
            if ((_owner != null && _owner != this)
                || (NetworkManager.Singleton != null && NetworkManager.Singleton != manager))
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            _owner = this;
        }
        void OnDestroy() { if (_owner == this) _owner = null; }
    }
}
