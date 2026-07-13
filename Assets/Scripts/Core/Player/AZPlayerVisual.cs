using AbsoluteZero.Core.Game;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Player
{
    public class AZPlayerVisual : NetworkBehaviour
    {
        private MeshRenderer capsuleRenderer;
        private Material instanceMaterial;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(transform, false);
            capsule.transform.localPosition = Vector3.zero;

            var col = capsule.GetComponent<CapsuleCollider>();
            if (col != null) Destroy(col);

            capsuleRenderer = capsule.GetComponent<MeshRenderer>();
            instanceMaterial = new Material(capsuleRenderer.material);
            capsuleRenderer.material = instanceMaterial;
        }

        private void Update()
        {
            if (instanceMaterial == null) return;

            var tm = AbsoluteZeroTurnManager.Instance;
            if (tm == null) return;

            int playerIndex = tm.GetPlayerIndexForClient(OwnerClientId);
            if (playerIndex < 0) return;

            float temp = tm.GetPlayerTemperature(playerIndex);
            float normalized = Mathf.Clamp01(temp / 37f);
            float hue = Mathf.Lerp(0.6f, 0f, normalized);
            instanceMaterial.color = Color.HSVToRGB(hue, 0.8f, 1f);
        }
    }
}
