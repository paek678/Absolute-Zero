using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AbsoluteZero.Core.Game
{
    /// <summary>
    /// 개발용 아이템 지급 치트 (에디터/개발 빌드 전용, 호스트에서만 동작).
    /// F1 레드카드 / F2 불닭 / F3 드라이버 / F4 랜덤 / F6 집게손 / F7 청테이프 / F8 스마트폰 / F9 핫팩 / F10 안아줘요 / F11 물총
    /// — 양쪽 플레이어 모두에게 지급. 미니게임을 드랍운과 무관하게 즉시 테스트하기 위한 도구.
    /// (주의: F5는 TurnManager의 디버그 일시정지 토글이라 아이템 지급에 쓰지 않음 — timeScale=0이 미니게임을 멈춤)
    /// </summary>
    public class DebugItemGranter : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void Update()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame) GrantByName("Red Card");
            if (kb.f2Key.wasPressedThisFrame) GrantByName("Buldak Noodles");
            if (kb.f3Key.wasPressedThisFrame) GrantByName("Screwdriver");
            if (kb.f4Key.wasPressedThisFrame) GrantRandom();
            // F5 = TurnManager 디버그 일시정지 토글 → 아이템 지급에 쓰지 않음 (물총은 F11)
            if (kb.f6Key.wasPressedThisFrame) GrantByName("Claw Machine");
            if (kb.f7Key.wasPressedThisFrame) GrantByName("Blue Tape");
            if (kb.f8Key.wasPressedThisFrame) GrantByName("Smartphone");
            if (kb.f9Key.wasPressedThisFrame) GrantByName("Hot Pack");
            if (kb.f10Key.wasPressedThisFrame) GrantByName("Hug T-shirt");
            if (kb.f11Key.wasPressedThisFrame) GrantByName("Water Gun");
        }

        static void GrantByName(string itemName)
        {
            var itemManager = ItemManager.Instance;
            if (itemManager == null) return;

            var items = itemManager.GetAllItems();
            short itemId = -1;
            for (short i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].ItemName == itemName)
                {
                    itemId = i;
                    break;
                }
            }
            if (itemId < 0)
            {
                Debug.LogWarning($"[DebugItemGranter] '{itemName}' not found in registry");
                return;
            }

            foreach (var inv in FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None))
            {
                bool granted = inv.GrantSpecificItem(itemId);
                Debug.Log($"[DebugItemGranter] {itemName} → {inv.gameObject.name}: {(granted ? "지급" : "실패")}");
            }
        }

        static void GrantRandom()
        {
            var itemManager = ItemManager.Instance;
            if (itemManager == null) return;

            foreach (var inv in FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None))
                inv.GrantRandomItems(1, itemManager.GetDropTable());
            Debug.Log("[DebugItemGranter] 랜덤 아이템 +1 (양쪽)");
        }
#endif
    }
}
