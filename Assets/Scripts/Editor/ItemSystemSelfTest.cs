#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using AbsoluteZero.UI.TestUI;

namespace AbsoluteZero.EditorTools
{
    /// <summary>
    /// 아이템 시스템 로직 자가 검증 (에디터 전용 — 플레이 모드/네트워크 불필요).
    /// [Tools > AbsoluteZero > Run Item SelfTest] 실행 → 콘솔에 PASS/FAIL 요약 출력.
    /// 실제 SO 에셋(부채/바람막이/차/고양이) + 테스트 랜덤 아이템으로 검증한다.
    /// </summary>
    public static class ItemSystemSelfTest
    {
        const string ItemFolder = "Assets/Data/Items";

        static int _pass, _fail;
        static readonly StringBuilder _report = new();

        [MenuItem("Tools/AbsoluteZero/Run Item SelfTest")]
        public static void Run()
        {
            _pass = 0; _fail = 0;
            _report.Clear();

            var fan = Load<AttackItemDataSO>("Item_Fan");
            var windbreaker = Load<DefenseItemDataSO>("Item_Windbreaker");
            var tea = Load<RecoveryItemDataSO>("Item_WarmTea");
            var cat = Load<SabotageItemDataSO>("Item_Cat");
            if (fan == null || windbreaker == null || tea == null || cat == null)
            {
                Debug.LogError("[SelfTest] 기본 아이템 SO 에셋 로드 실패 — Assets/Data/Items 확인");
                return;
            }

            // ── T1. 에셋 값 검증 (Section 9.2) ──
            Test("T1 에셋 값 (9.2)", () =>
            {
                Assert(fan.Damage == 3f && fan.SlotType == ItemSlotType.Main && fan.MaxUses <= 0, "부채: 3°/Main/무한");
                Assert(windbreaker.BlockAmount == 4f && windbreaker.Filter == DamageFilter.Temperature, "바람막이: 4°/Temperature");
                Assert(tea.HealPerUse != null && tea.HealPerUse.Length == 1 && tea.HealPerUse[0] == 7f && tea.MaxUses == 1, "차: [7]/1회");
                Assert(cat.SlotType == ItemSlotType.Sub && cat.SabotageType == SabotageType.Reroll, "고양이: Sub/Reroll");
            });

            // ── T2. 부채 공격: 37 - 3 = 34 ──
            Test("T2 부채 공격 (37→34)", () =>
            {
                using var d = Duel.Create(fan, windbreaker, tea, cat);
                fan.ExecuteEffect(d.Ctx(0, 0));
                Assert(Approximately(d.P2.Temperature.Value, 34f), $"기대 34, 실제 {d.P2.Temperature.Value}");
            });

            // ── T3. 바람막이 방어: 3 - 4 → 완전 차단 (9.3 Scenario B) ──
            Test("T3 바람막이 완전 차단 (부채 3° vs 방어 4°)", () =>
            {
                using var d = Duel.Create(fan, windbreaker, tea, cat);
                d.Mods[1].ActiveDefense = new DefenseInfo { Filter = windbreaker.Filter, BlockAmount = windbreaker.BlockAmount };
                fan.ExecuteEffect(d.Ctx(0, 0));
                Assert(Approximately(d.P2.Temperature.Value, 37f), $"기대 37(무피해), 실제 {d.P2.Temperature.Value}");
            });

            // ── T4. 방어 관통: 7° 공격 vs 4° 방어 → 3° 피해 ──
            Test("T4 방어 관통 (7° - 4° = 3° 피해)", () =>
            {
                using var d = Duel.Create(fan, windbreaker, tea, cat);
                var def = new DefenseInfo { Filter = DamageFilter.Temperature, BlockAmount = 4f };
                float actual = d.Temp.ApplyDamage(d.P2, 7f, DamageFilter.Temperature, def);
                Assert(Approximately(actual, 3f) && Approximately(d.P2.Temperature.Value, 34f),
                    $"기대 피해 3/온도 34, 실제 피해 {actual}/온도 {d.P2.Temperature.Value}");
            });

            // ── T5. 따뜻한 차: +7, 37° 캡 (FIX-13 useIndex 포함) ──
            Test("T5 차 회복 +7 및 37° 캡", () =>
            {
                using var d = Duel.Create(fan, windbreaker, tea, cat);
                d.P1.Temperature.Value = 25f;
                tea.ExecuteEffect(d.Ctx(0, 2));
                Assert(Approximately(d.P1.Temperature.Value, 32f), $"25+7: 기대 32, 실제 {d.P1.Temperature.Value}");
                d.P1.Temperature.Value = 35f;
                tea.ExecuteEffect(d.Ctx(0, 2));
                Assert(Approximately(d.P1.Temperature.Value, 37f), $"35+7 캡: 기대 37, 실제 {d.P1.Temperature.Value}");
            });

            // ── T6. 소모/리필: 차 1회 소모 → 사용 불가 → 라운드 리셋 시 리필 ──
            Test("T6 차 소모 → 리필 (FIX-12)", () =>
            {
                using var d = Duel.Create(fan, windbreaker, tea, cat);
                d.Inv1.ConsumeItem(2);
                Assert(!d.Inv1.SlotStates[2].IsUsable && d.Inv1.SlotStates[2].ItemId == 2,
                    "소모 후: 사용 불가 + ItemId 유지(BasicConsumable)");
                d.Inv1.ResetForNewRound();
                Assert(d.Inv1.SlotStates[2].IsUsable && d.Inv1.SlotStates[2].RemainingUses == 1, "리셋 후 리필");
            });

            // ── T7. 부채 무한 (FIX-03: 255 sentinel) ──
            Test("T7 부채 무한 사용 (255 sentinel)", () =>
            {
                using var d = Duel.Create(fan, windbreaker, tea, cat);
                Assert(d.Inv1.SlotStates[0].IsUnlimited, "부채 RemainingUses == 255");
                d.Inv1.ConsumeItem(0);
                d.Inv1.ConsumeItem(0);
                Assert(d.Inv1.SlotStates[0].IsUnlimited && d.Inv1.SlotStates[0].IsUsable, "소모 후에도 무한 유지");
            });

            // ── T8. 시작 랜덤 4개 + 구간 지급 + 8칸 캡 ──
            Test("T8 랜덤 지급: 시작 4개 + 구간(30/20/10) + 최대 8", () =>
            {
                using var d = Duel.Create(fan, windbreaker, tea, cat);
                d.Inv1.GrantRandomItems(4, d.Table);
                Assert(CountRandom(d.Inv1) == 4, $"시작 지급: 기대 4, 실제 {CountRandom(d.Inv1)}");
                d.P1.Temperature.Value = 29f;
                d.Temp.CheckThresholds(d.P1, d.Inv1, d.Inv1.GetThresholdGranted(), d.Table);
                Assert(CountRandom(d.Inv1) == 5, $"30° 통과 +1: 기대 5, 실제 {CountRandom(d.Inv1)}");
                d.P1.Temperature.Value = 9f;
                d.Temp.CheckThresholds(d.P1, d.Inv1, d.Inv1.GetThresholdGranted(), d.Table);
                Assert(CountRandom(d.Inv1) == 8, $"20°+10° 통과(+2+3, 캡 8): 기대 8, 실제 {CountRandom(d.Inv1)}");
                // 재통과 시 재지급 없음 (1회성)
                d.Inv1.ResetForNewRound();
                d.P1.Temperature.Value = 29f;
                d.Temp.CheckThresholds(d.P1, d.Inv1, d.Inv1.GetThresholdGranted(), d.Table);
                Assert(CountRandom(d.Inv1) == 1, $"리셋 후 30° 재통과: 기대 1, 실제 {CountRandom(d.Inv1)}");
            });

            // ── T9. 고양이 리롤: 상대 랜덤 아이템 유지 개수 + 유효 ItemId ──
            Test("T9 고양이 리롤 (개수 보존 + 유효 ID)", () =>
            {
                using var d = Duel.Create(fan, windbreaker, tea, cat);
                d.Inv2.GrantRandomItems(4, d.Table);
                cat.ExecuteEffect(d.Ctx(0, 3));   // P1이 사용 → P2 리롤
                Assert(CountRandom(d.Inv2) == 4, $"리롤 후 개수: 기대 4, 실제 {CountRandom(d.Inv2)}");
                for (int i = 4; i < d.Inv2.SlotStates.Count; i++)
                {
                    var s = d.Inv2.SlotStates[i];
                    if (s.IsEmpty) continue;
                    Assert(s.ItemId >= ItemTestRegistry.BasicCount && s.ItemId < d.Registry.Length,
                        $"슬롯{i} ItemId {s.ItemId} 유효 범위");
                }
            });

            // ── T10. 훔치기: 상대 랜덤 1개 → 내 빈 슬롯 ──
            Test("T10 훔치기 (상대 -1, 나 +1)", () =>
            {
                using var d = Duel.Create(fan, windbreaker, tea, cat);
                d.Inv2.GrantRandomItems(3, d.Table);
                d.Inv2.StealRandomItem(d.Inv1);
                Assert(CountRandom(d.Inv2) == 2 && CountRandom(d.Inv1) == 1,
                    $"기대 상대2/나1, 실제 상대{CountRandom(d.Inv2)}/나{CountRandom(d.Inv1)}");
            });

            // ── T11. 삼계탕 vs 마스크: 완전 무효 (즉시 +3도, 지연 -7도 차단) ──
            Test("T11 마스크 규칙 (삼계탕 완전 무효)", () =>
            {
                using var d = Duel.Create(fan, windbreaker, tea, cat);
                var samgyetang = MakeSamgyetang();
                d.P2.Temperature.Value = 30f;
                d.Mods[1].ActiveDefense = new DefenseInfo { Filter = DamageFilter.Food, BlockAmount = float.MaxValue };
                samgyetang.ExecuteEffect(d.Ctx(0, 0));
                Assert(Approximately(d.P2.Temperature.Value, 30f), $"즉시 효과 차단: 기대 30, 실제 {d.P2.Temperature.Value}");
                d.Buff.ProcessTurnStart(d.P1, d.P2);
                Assert(Approximately(d.P2.Temperature.Value, 30f), $"지연 효과 차단: 기대 30, 실제 {d.P2.Temperature.Value}");
            });

            // ── T12. 삼계탕 (마스크 없음): 즉시 +3, 다음 턴 -7 ──
            Test("T12 삼계탕 정상 발동 (+3 즉시 / -7 지연)", () =>
            {
                using var d = Duel.Create(fan, windbreaker, tea, cat);
                var samgyetang = MakeSamgyetang();
                d.P2.Temperature.Value = 30f;
                samgyetang.ExecuteEffect(d.Ctx(0, 0));
                Assert(Approximately(d.P2.Temperature.Value, 33f), $"즉시 +3: 기대 33, 실제 {d.P2.Temperature.Value}");
                d.Buff.ProcessTurnStart(d.P1, d.P2);
                Assert(Approximately(d.P2.Temperature.Value, 26f), $"지연 -7: 기대 26, 실제 {d.P2.Temperature.Value}");
            });

            // ── T13. 탄산음료 버프: 즉시 -5 / 다음 턴 +15 ──
            Test("T13 탄산음료 (-5 즉시 / +15 지연)", () =>
            {
                using var d = Duel.Create(fan, windbreaker, tea, cat);
                var soda = ScriptableObject.CreateInstance<BuffItemDataSO>();
                soda.ItemName = "탄산음료(T)"; soda.Category = ItemCategory.Buff;
                soda.ImmediateTempDelta = -5f; soda.DelayedTempDelta = 15f; soda.DelayTurns = 1;
                d.P1.Temperature.Value = 20f;
                soda.ExecuteEffect(d.Ctx(0, 0));
                Assert(Approximately(d.P1.Temperature.Value, 15f), $"즉시 -5: 기대 15, 실제 {d.P1.Temperature.Value}");
                d.Buff.ProcessTurnStart(d.P1, d.P2);
                Assert(Approximately(d.P1.Temperature.Value, 30f), $"지연 +15: 기대 30, 실제 {d.P1.Temperature.Value}");
            });

            // ── T14. 드롭 테이블: 결과가 항상 랜덤 풀 안 ──
            Test("T14 드롭 테이블 (50회 roll 유효성)", () =>
            {
                var registry = ItemTestRegistry.Build(fan, windbreaker, tea, cat, out var table);
                for (int i = 0; i < 50; i++)
                {
                    var rolled = table.Roll();
                    Assert(rolled != null && Array.IndexOf(registry, rolled) >= ItemTestRegistry.BasicCount,
                        $"roll #{i} 랜덤 풀 밖: {(rolled == null ? "null" : rolled.ItemName)}");
                }
            });

            // ── T15. 사망 판정 ──
            Test("T15 사망 판정 (0° 도달)", () =>
            {
                using var d = Duel.Create(fan, windbreaker, tea, cat);
                d.P2.Temperature.Value = 2f;
                d.Temp.ApplyDamage(d.P2, 5f, DamageFilter.Temperature, null);
                Assert(Approximately(d.P2.Temperature.Value, 0f) && d.Temp.IsDead(d.P2), "0° 클램프 + IsDead true");
            });

            string summary = $"[SelfTest] {_pass + _fail}개 테스트: {_pass} PASS / {_fail} FAIL";
            if (_fail == 0) Debug.Log($"{summary} — 전부 통과 ✔\n{_report}");
            else Debug.LogError($"{summary}\n{_report}");
        }

        // ═══ 헬퍼 ═══

        static T Load<T>(string name) where T : ItemDataSO
            => AssetDatabase.LoadAssetAtPath<T>($"{ItemFolder}/{name}.asset");

        static DebuffItemDataSO MakeSamgyetang()
        {
            var s = ScriptableObject.CreateInstance<DebuffItemDataSO>();
            s.ItemName = "삼계탕(T)";
            s.Category = ItemCategory.Debuff;
            s.ImmediateTempDelta = 3f;
            s.DelayedTempDelta = -7f;
            s.DelayTurns = 1;
            s.AttackFilter = DamageFilter.Food;
            return s;
        }

        static int CountRandom(PlayerInventory inv)
        {
            int n = 0;
            for (int i = 4; i < inv.SlotStates.Count; i++)
                if (!inv.SlotStates[i].IsEmpty) n++;
            return n;
        }

        static bool Approximately(float a, float b) => Mathf.Abs(a - b) < 0.001f;

        static void Test(string name, Action body)
        {
            try
            {
                body();
                _pass++;
                _report.AppendLine($"  PASS  {name}");
            }
            catch (Exception e)
            {
                _fail++;
                _report.AppendLine($"  FAIL  {name} — {e.Message}");
            }
        }

        static void Assert(bool condition, string detail)
        {
            if (!condition) throw new Exception(detail);
        }

        /// <summary>테스트용 1:1 대전 환경 — 에디터 모드라 Awake가 안 불리므로 NetworkList를 직접 주입</summary>
        sealed class Duel : IDisposable
        {
            public PlayerState P1, P2;
            public PlayerInventory Inv1, Inv2;
            public PlayerModifiers[] Mods = new PlayerModifiers[2];
            public TemperatureSystem Temp = new();
            public BuffDebuffSystem Buff = new();
            public ItemDropTable Table;
            public ItemDataSO[] Registry;
            readonly List<GameObject> _trash = new();

            public static Duel Create(ItemDataSO fan, ItemDataSO windbreaker, ItemDataSO tea, ItemDataSO cat)
            {
                var d = new Duel();
                d.Registry = ItemTestRegistry.Build(fan, windbreaker, tea, cat, out d.Table);
                (d.P1, d.Inv1) = d.MakePlayer("SelfTest_P1", d.Registry, fan, windbreaker, tea, cat);
                (d.P2, d.Inv2) = d.MakePlayer("SelfTest_P2", d.Registry, fan, windbreaker, tea, cat);
                return d;
            }

            (PlayerState, PlayerInventory) MakePlayer(string name, ItemDataSO[] registry,
                ItemDataSO fan, ItemDataSO windbreaker, ItemDataSO tea, ItemDataSO cat)
            {
                var go = new GameObject(name);
                _trash.Add(go);
                var ps = go.AddComponent<PlayerState>();
                var inv = go.AddComponent<PlayerInventory>();
                inv.SlotStates = new NetworkList<ItemSlotNetData>();   // 에디터 모드: Awake 대체
                inv.Initialize(registry);
                inv.InitializeBasicItems(fan, windbreaker, tea, cat);
                ps.Temperature.Value = TemperatureSystem.MAX_TEMP;
                return (ps, inv);
            }

            public ItemContext Ctx(int user, byte slot)
            {
                var userInv = user == 0 ? Inv1 : Inv2;
                return new ItemContext
                {
                    User = user == 0 ? P1 : P2,
                    Target = user == 0 ? P2 : P1,
                    UserIndex = user,
                    TargetIndex = 1 - user,
                    UserInventory = userInv,
                    TargetInventory = user == 0 ? Inv2 : Inv1,
                    UserSlot = slot < userInv.SlotStates.Count ? userInv.SlotStates[slot] : ItemSlotNetData.Empty,
                    SlotIndex = slot,
                    TempSystem = Temp,
                    BuffSystem = Buff,
                    DropTable = Table,
                    AllModifiers = Mods,
                };
            }

            public void Dispose()
            {
                foreach (var go in _trash)
                    if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
