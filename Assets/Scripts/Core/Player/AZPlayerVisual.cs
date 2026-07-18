using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Player
{
    public class AZPlayerVisual : NetworkBehaviour
    {
        SpriteRenderer _bodyRenderer;
        PlayerState _playerState;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _playerState = GetComponent<PlayerState>();

            var bodyTransform = transform.Find("body");
            if (bodyTransform != null)
                _bodyRenderer = bodyTransform.GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            if (_bodyRenderer == null || _playerState == null) return;

            float temp = _playerState.Temperature.Value;
            float normalized = Mathf.Clamp01(temp / 37f);
            _bodyRenderer.color = Color.Lerp(new Color(0.5f, 0.7f, 1f), Color.white, normalized);
        }
    }
}
