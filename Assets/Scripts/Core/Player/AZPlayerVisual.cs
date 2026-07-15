using AbsoluteZero.Core.Common;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Player
{
    public class AZPlayerVisual : NetworkBehaviour
    {
        SpriteRenderer _bodyRenderer;
        PlayerState _playerState;

        const string ENEMY_SPAWN_MARKER = "EnemySpawn";

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _playerState = GetComponent<PlayerState>();

            if (IsOwner)
                return;

            var marker = GameObject.Find(ENEMY_SPAWN_MARKER);
            if (marker != null)
                transform.position = marker.transform.position;

            var litMat = Resources.Load<Material>("sprite3DMat");

            var body = new GameObject("Body");
            body.transform.SetParent(transform, false);

            _bodyRenderer = body.AddComponent<SpriteRenderer>();
            _bodyRenderer.sprite = GameSprites.Get(GameSprites.PLAYER);
            if (litMat != null)
                _bodyRenderer.material = litMat;
            _bodyRenderer.sortingOrder = 10;
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
