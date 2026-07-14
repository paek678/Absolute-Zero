using System.Collections.Generic;
using Unity.Netcode;
using AbsoluteZero.Core.Item;

namespace AbsoluteZero.Core.Player
{
    /// <summary>
    /// 플레이어 인벤토리 (SYSTEM_ARCHITECTURE.md Section 4.4 — Player Prefab에 부착).
    /// 슬롯 0~3 = 기본 아이템, 4~11 = 랜덤 아이템. 모든 변경은 서버에서만 수행.
    /// </summary>
    public class PlayerInventory : NetworkBehaviour
    {
        // ═══ 동기화 ═══
        // FIX-02: NetworkList는 Awake()에서 반드시 초기화 — Spawn 이후 초기화하면 NRE
        public NetworkList<ItemSlotNetData> SlotStates;

        void Awake()
        {
            SlotStates = new NetworkList<ItemSlotNetData>();
        }

        // ═══ Server-local ═══
        const int BASIC_SLOT_COUNT = 4;
        const int MAX_RANDOM_SLOTS = 8;
        const int MAX_SLOTS = BASIC_SLOT_COUNT + MAX_RANDOM_SLOTS;

        // _itemRegistry는 ItemManager(코어 담당)가 주입 (OnNetworkSpawn 시)
        ItemDataSO[] _itemRegistry;
        bool[] _thresholdGranted = new bool[3];  // 30/20/10 지급 이력

        public void Initialize(ItemDataSO[] registry)
        {
            _itemRegistry = registry;
        }

        /// <summary>ServerRpc에서 SO 데이터 접근 시 사용</summary>
        public ItemDataSO GetItemData(int slotIndex)
        {
            return _itemRegistry[SlotStates[slotIndex].ItemId];
        }

        // ═══ Server Methods ═══

        /// <summary>초기화: 4개 기본 아이템 세팅 (서버 전용)</summary>
        public void InitializeBasicItems(ItemDataSO fan, ItemDataSO windbreaker,
                                         ItemDataSO warmTea, ItemDataSO cat)
        {
            SlotStates.Add(MakeSlot(fan));          // slot 0: 부채
            SlotStates.Add(MakeSlot(windbreaker));  // slot 1: 바람막이
            SlotStates.Add(MakeSlot(warmTea));      // slot 2: 따뜻한 차
            SlotStates.Add(MakeSlot(cat));          // slot 3: 고양이
            // slot 4~11: empty (랜덤 슬롯)
        }

        /// <summary>
        /// 아이템 소모 (서버 전용). 반드시 ExecuteEffect 이후 호출 (FIX-13 useIndex 계산 의존).
        /// FIX-03: Permanent(RemainingUses==255) 아이템은 소모하지 않음.
        /// </summary>
        public void ConsumeItem(byte slotIndex)
        {
            var slot = SlotStates[slotIndex];
            if (slot.IsUnlimited) return;          // Permanent → 소모 안 함
            if (slot.RemainingUses == 0) return;   // 방어적 가드: byte 언더플로(0→255=무한) 방지

            slot.RemainingUses--;

            if (slot.RemainingUses <= 0)
            {
                var item = _itemRegistry[slot.ItemId];
                if (item.Persistence == ItemPersistence.RandomConsumable)
                {
                    slot.ItemId = -1;  // 슬롯에서 완전 제거
                }
                // BasicConsumable: ItemId 유지, RemainingUses=0 → UI에서 회색 처리
                slot.RemainingUses = 0;
            }

            SlotStates[slotIndex] = slot;  // NetworkList 갱신 트리거
        }

        /// <summary>랜덤 아이템 지급 (서버 전용) — 구간 통과 보상</summary>
        public void GrantRandomItems(int count, ItemDropTable table)
        {
            for (int i = 0; i < count; i++)
            {
                int emptySlot = FindEmptyRandomSlot();
                if (emptySlot == -1) break;  // 슬롯 풀 (최대 8개)

                ItemDataSO item = table.Roll();
                SlotStates[emptySlot] = MakeSlot(item);
            }
        }

        /// <summary>
        /// 상대의 모든 랜덤 아이템 리롤 (고양이 효과, 서버 전용).
        /// 데모에서는 랜덤 아이템/드롭 테이블이 없어 자연스럽게 no-op (Section 9.2).
        /// </summary>
        public void RerollAllRandom(ItemDropTable table)
        {
            if (table == null) return;  // 데모: 드롭 테이블 미구성 → no-op

            for (int i = BASIC_SLOT_COUNT; i < SlotStates.Count; i++)
            {
                if (SlotStates[i].IsEmpty) continue;
                SlotStates[i] = MakeSlot(table.Roll());
            }
        }

        /// <summary>
        /// 내 랜덤 아이템 1개를 무작위로 골라 thief 인벤토리로 이동 (집게손/잼민이들 효과, 서버 전용).
        /// [기획 확정 2026-07-14] 집게손을 소모하며 비는 자리에 뺏어온 아이템이 들어가므로
        /// thief 슬롯이 꽉 차는 상황은 설계상 발생하지 않음 — dest == -1 가드는 방어 코드일 뿐.
        /// </summary>
        public void StealRandomItem(PlayerInventory thief)
        {
            var occupied = new List<int>();
            for (int i = BASIC_SLOT_COUNT; i < SlotStates.Count; i++)
            {
                if (!SlotStates[i].IsEmpty) occupied.Add(i);
            }
            if (occupied.Count == 0) return;  // 훔칠 랜덤 아이템 없음

            int pick = occupied[UnityEngine.Random.Range(0, occupied.Count)];
            int dest = thief.FindEmptyRandomSlot();
            if (dest == -1) return;  // 도둑 슬롯 가득참 → 실패

            thief.SlotStates[dest] = SlotStates[pick];
            SlotStates[pick] = ItemSlotNetData.Empty;
        }

        int FindEmptyRandomSlot()
        {
            for (int i = BASIC_SLOT_COUNT; i < MAX_SLOTS; i++)
            {
                if (i >= SlotStates.Count) { SlotStates.Add(ItemSlotNetData.Empty); return i; }
                if (SlotStates[i].ItemId == -1) return i;
            }
            return -1;
        }

        /// <summary>FIX-03: MaxUses ≤ 0 (Permanent) → RemainingUses = 255 (unlimited sentinel)</summary>
        ItemSlotNetData MakeSlot(ItemDataSO item)
        {
            short id = (short)System.Array.IndexOf(_itemRegistry, item);
            byte uses = item.MaxUses <= 0 ? (byte)255 : (byte)item.MaxUses;
            return new ItemSlotNetData
            {
                ItemId = id,
                RemainingUses = uses,
                Flags = (byte)(item.SlotType == ItemSlotType.Sub ? 0b10 : 0)
                // bit 0 = 청테이프 봉쇄, bit 1 = Sub 타입
            };
        }

        /// <summary>FIX-12: 라운드 간 리셋 — BasicConsumable 리필 + 랜덤 슬롯 비우기 (서버 전용)</summary>
        public void ResetForNewRound()
        {
            _thresholdGranted = new bool[3];

            // BasicConsumable 아이템 리필 (차, 고양이)
            for (int i = 0; i < BASIC_SLOT_COUNT && i < SlotStates.Count; i++)
            {
                var slot = SlotStates[i];
                if (slot.ItemId == -1) continue;
                var item = _itemRegistry[slot.ItemId];
                if (item.Persistence == ItemPersistence.BasicConsumable)
                {
                    slot.RemainingUses = (byte)item.MaxUses;
                    SlotStates[i] = slot;
                }
            }

            // 랜덤 슬롯 초기화 (새 라운드에서 다시 지급)
            for (int i = BASIC_SLOT_COUNT; i < SlotStates.Count; i++)
            {
                SlotStates[i] = ItemSlotNetData.Empty;
            }
        }

        /// <summary>구간 지급 이력 배열 (TemperatureSystem.CheckThresholds에 전달용)</summary>
        public bool[] GetThresholdGranted() => _thresholdGranted;
    }
}
