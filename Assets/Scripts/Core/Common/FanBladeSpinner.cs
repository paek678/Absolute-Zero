using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    /// <summary>
    /// 독립 선풍기 오브젝트의 날개 회전 컨트롤러.
    /// 해당 플레이어의 선풍기가 켜져 있을 때(PlayerState.IsFanActive)만 날개가 돌고,
    /// 준비(Ready) 완료로 선풍기가 꺼지면 부드럽게 감속해 멈춘다. (머리 날림 애니메이션과 동일 신호)
    /// </summary>
    public class FanBladeSpinner : MonoBehaviour
    {
        [Header("=== Refs ===")]
        [Tooltip("회전시킬 날개 Transform (fan_blades 스프라이트)")]
        [SerializeField] private Transform blades;

        [Header("=== Spin ===")]
        [Tooltip("최대 회전 속도 (deg/s)")]
        [SerializeField] private float spinSpeed = 900f;
        [Tooltip("가속률 (deg/s^2)")]
        [SerializeField] private float accel = 1800f;
        [Tooltip("감속 시간 (초)")]
        [SerializeField] private float decelDuration = 0.2f;
        [Tooltip("시계방향(-1) / 반시계(+1)")]
        [SerializeField] private float direction = -1f;

        [Header("=== Player Binding ===")]
        [Tooltip("-1 = 로컬 사람, -2 = 1v1 상대, 0~3 = 논리 seat")]
        [SerializeField] private int playerIndex = -1;
        [Tooltip("플레이어 상태를 아직 못 찾았을 때 기본으로 돌릴지")]
        [SerializeField] private bool spinWhenNoState = true;

        private float _current;
        private float _angle;                                    // 현재 스핀 각 (0~360 wrap — 무한 누적/오버플로우 방지)
        private Quaternion _baseRotation = Quaternion.identity;  // 초기 기울기(Y틸트) 보존용
        private bool _baseCaptured;

        private void Awake()
        {
            // 인스펙터 미할당 시 자식 "Blades" 자동 탐색 (프리팹 자동 조립 대비)
            if (blades == null)
            {
                var t = transform.Find("Blades");
                if (t != null) blades = t;
            }
            CaptureBase();
        }

        private void CaptureBase()
        {
            if (blades != null && !_baseCaptured)
            {
                _baseRotation = blades.localRotation;   // Y 기울기 등 초기 자세
                _angle = 0f;
                _baseCaptured = true;
            }
        }

        private void Update()
        {
            if (blades == null) return;

            if (ShouldSpin())
            {
                _current = Mathf.MoveTowards(_current, spinSpeed, accel * Time.deltaTime);
            }
            else if (_current > 0.5f)
            {
                float t = 1f - Mathf.Pow(0.001f, Time.deltaTime / Mathf.Max(decelDuration, 0.01f));
                _current = Mathf.Lerp(_current, 0f, t);
            }
            else
            {
                _current = 0f;
            }
            if (!Mathf.Approximately(_current, 0f))
            {
                // 각도를 직접 관리해 360으로 wrap → 트랜스폼에 무한 누적 안 됨
                _angle = Mathf.Repeat(_angle + direction * _current * Time.deltaTime, 360f);
                blades.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, _angle);
            }
        }

        private bool ShouldSpin()
        {
            var ps = GetPlayerState();
            if (ps == null) return spinWhenNoState;
            return ps.IsFanActive.Value;
        }

        /// <summary>바인딩 대상 설정 (런타임 생성 시 SpawnStayItemFans에서 호출)</summary>
        public void Bind(Transform bladesTf, int index)
        {
            blades = bladesTf;
            playerIndex = index;
        }

        private PlayerState GetPlayerState()
        {
            if (!LocalMatchPerspective.TryResolveCurrent(out var perspective)) return null;
            if (playerIndex == -1) return perspective.HumanBinding.State;
            if (playerIndex == -2)
                return perspective.TryGetOpponentBinding(out var opponent) ? opponent.State : null;
            return perspective.TryGetBinding(playerIndex, out var binding) ? binding.State : null;
        }
    }
}
