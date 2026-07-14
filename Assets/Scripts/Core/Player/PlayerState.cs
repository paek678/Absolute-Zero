using Unity.Netcode;

namespace AbsoluteZero.Core.Player
{
    /// <summary>
    /// [STUB — 아이템 시스템 컴파일용 최소 구현]
    /// SYSTEM_ARCHITECTURE.md Section 2.1의 NetworkVariable 필드만 정의.
    /// ServerRpc 핸들러(5.3), ActionQueue(2.2), BuildContext(2.1)는 코어 담당(동업자) 구현으로 병합할 것.
    /// 모든 NV는 서버만 write (기본 권한: Everyone read / Server write).
    /// </summary>
    public class PlayerState : NetworkBehaviour
    {
        // ═══ NetworkVariables (서버 write, 클라이언트 read) ═══
        public NetworkVariable<float> Temperature = new(37f);
        public NetworkVariable<float> FanSpeed = new(1f);
        public NetworkVariable<bool> IsReady = new(false);
        public NetworkVariable<bool> IsFanActive = new(false);

        // ═══ References ═══
        int _playerIndex;                // 0 or 1 (서버가 spawn 시 할당)
        public int PlayerIndex => _playerIndex;

        /// <summary>서버 spawn 시 인덱스 할당용 (코어 시스템에서 호출)</summary>
        public void SetPlayerIndex(int index) => _playerIndex = index;
    }
}
