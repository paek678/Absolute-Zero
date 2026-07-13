using UnityEngine;

namespace AbsoluteZero.Core.Network
{
    [AddComponentMenu("AbsoluteZero/Network/Player Spawn Point 3D")]
    public class PlayerSpawnPoint3D : MonoBehaviour
    {
        [SerializeField] private int order;
        public int Order => order;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);

            Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.2f);
        }
    }
}
