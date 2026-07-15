using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.Netcode.Transports.UTP;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.UI.TestUI
{
    /// <summary>
    /// 2.5D 그레이박스 클라이언트 뷰 (ItemNetTestScene 전용, PLAN_006).
    /// 목업 기준: 마루 바닥 + 내 아이템은 카메라 앞 바닥 타일(클릭), 상대/상대 아이템은 건너편(읽기 전용),
    /// [준비 끝] 팻말 중앙, 온도 바는 화면 상단 오버레이(내 것 좌측 크게, 상대 중앙 작게).
    /// 입력은 전부 ServerRpc로만 전송, 표시는 NV/NetworkList/ClientRpc 수신으로만 갱신.
    /// 클라이언트별 미러링: 각자 자기 진영이 카메라 앞(아래)에 오도록 카메라/타일을 배치.
    /// </summary>
    public class ItemNetTestUI : MonoBehaviour
    {
        [Header("References (씬 셋업 시 연결)")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject managerPrefab;
        [SerializeField] private ItemDataSO fanItem;
        [SerializeField] private ItemDataSO windbreakerItem;
        [SerializeField] private ItemDataSO warmTeaItem;
        [SerializeField] private ItemDataSO catItem;

        const int BASIC = 4;
        const int RANDOM = 8;          // 기획 확정: 4×2 = 8칸
        const int SLOTS = BASIC + RANDOM;
        const ushort TEST_PORT = 7778; // 기본 7777은 이전 세션 소켓 누수와 충돌한 적 있어 테스트 전용 포트 사용

        ItemDataSO[] _registry;
        int _myIndex = -1;
        float _dir = 1f;               // 내 진영이 -z가 되도록 하는 부호 (P1=+1, P2=-1)
        PlayerState _me, _opp;
        PlayerInventory _myInv, _oppInv;
        ItemNetTestManager _mgr;
        Camera _cam;
        bool _gameUiBuilt;

        // ═══ 월드 그레이박스 ═══
        class Tile
        {
            public GameObject Go;
            public Renderer Rend;
            public TextMeshPro Label;
            public int Slot;
        }
        readonly List<Tile> _myTiles = new();
        readonly List<Tile> _oppTiles = new();
        readonly GameObject[] _fanCubes = new GameObject[2];
        Material _matFloor;

        // ═══ 오버레이 UI ═══
        GameObject _lobbyPanel;
        Button _hostBtn, _clientBtn, _dummyBtn;
        TextMeshProUGUI _orText, _lobbyStatusText;
        TextMeshProUGUI _phaseText, _logText, _myTempText, _oppTempText, _myStateText, _oppStateText;
        Image _myBar, _oppBar;
        readonly Queue<string> _logLines = new();
        const int MaxLogLines = 8;

        private void Start()
        {
            _cam = Camera.main;
            BuildLobbyUI();
        }

        private void OnDestroy()
        {
            // RULE-010: 콜백 정리
            if (_mgr != null) _mgr.OnLogReceived -= AddLog;
            if (_me != null) _me.Temperature.OnValueChanged -= OnMyTempChanged;
            if (_opp != null) _opp.Temperature.OnValueChanged -= OnOppTempChanged;
            if (_myInv != null && _myInv.SlotStates != null) _myInv.SlotStates.OnListChanged -= OnSlotsChanged;
            if (_oppInv != null && _oppInv.SlotStates != null) _oppInv.SlotStates.OnListChanged -= OnSlotsChanged;
        }

        #region 접속 흐름

        private void OnHostClicked()
        {
            ConfigureTransport();
            RegisterNetworkPrefabs();
            if (!NetworkManager.Singleton.StartHost())
            {
                // 포트 점유 등으로 트랜스포트 시작 실패 — spawn 진행하면 후속 에러만 쌓임
                _lobbyStatusText.text = $"호스트 시작 실패 — 포트({TEST_PORT}) 사용 중. 에디터 재시작 필요";
                return;
            }
            // 서버: 매니저 spawn + 초기화 (호스트 = 서버 컨텍스트)
            var mgrGo = Instantiate(managerPrefab);
            mgrGo.GetComponent<NetworkObject>().Spawn();
            mgrGo.GetComponent<ItemNetTestManager>()
                 .ServerInitialize(playerPrefab, fanItem, windbreakerItem, warmTeaItem, catItem);
            AfterConnectClicked(isHost: true);
        }

        private void OnClientClicked()
        {
            ConfigureTransport();
            RegisterNetworkPrefabs();   // 클라도 동적 spawn 복제를 받으려면 등록 필요
            if (!NetworkManager.Singleton.StartClient())
            {
                _lobbyStatusText.text = "클라이언트 시작 실패 — 호스트가 먼저 떠 있는지 확인하세요";
                return;
            }
            AfterConnectClicked(isHost: false);
        }

        /// <summary>씬 재생성 없이 코드에서 테스트 포트 지정 (호스트/클라 동일 값이어야 접속됨)</summary>
        private void ConfigureTransport()
        {
            var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            utp.SetConnectionData("127.0.0.1", TEST_PORT);
        }

        /// <summary>
        /// 동적 spawn 프리팹은 서버/클라 양쪽에 등록되어야 복제된다 (Start 전 호출).
        /// 프리팹 임포트 시 NGO가 DefaultNetworkPrefabs 목록에 자동 등록했을 수 있어 중복 등록을 방지.
        /// </summary>
        private void RegisterNetworkPrefabs()
        {
            var nm = NetworkManager.Singleton;
            if (!nm.NetworkConfig.Prefabs.Contains(playerPrefab)) nm.AddNetworkPrefab(playerPrefab);
            if (!nm.NetworkConfig.Prefabs.Contains(managerPrefab)) nm.AddNetworkPrefab(managerPrefab);
        }

        private void AfterConnectClicked(bool isHost)
        {
            _hostBtn.gameObject.SetActive(false);
            _clientBtn.gameObject.SetActive(false);
            _orText.gameObject.SetActive(false);
            if (isHost)
            {
                _dummyBtn.gameObject.SetActive(true);   // 호스트만 더미 추가 가능
                _lobbyStatusText.text = "상대 대기 중... (MPPM 가상 플레이어 접속 가능)";
            }
            else
            {
                _lobbyStatusText.text = "서버 접속 중...";
            }
            StartCoroutine(WaitForGame());
        }

        private void OnDummyClicked()
        {
            if (ItemNetTestManager.Instance != null)
                ItemNetTestManager.Instance.ServerAddDummy();
        }

        private IEnumerator WaitForGame()
        {
            // 매니저 replicate 대기
            while (ItemNetTestManager.Instance == null) yield return null;
            _mgr = ItemNetTestManager.Instance;
            _mgr.OnLogReceived += AddLog;

            // 양쪽 플레이어 spawn 대기 (NetworkObjectReference NV로 전달됨)
            NetworkObject p1 = null, p2 = null;
            while (p1 == null || p2 == null)
            {
                _mgr.P1Ref.Value.TryGet(out p1);
                _mgr.P2Ref.Value.TryGet(out p2);
                yield return null;
            }

            // 내가 P1인가 P2인가 (관전자는 P1 시점)
            ulong myId = NetworkManager.Singleton.LocalClientId;
            _myIndex = _mgr.P2ClientId.Value == myId ? 1 : 0;
            _dir = _myIndex == 0 ? 1f : -1f;

            var ps1 = p1.GetComponent<PlayerState>();
            var ps2 = p2.GetComponent<PlayerState>();
            _me = _myIndex == 0 ? ps1 : ps2;
            _opp = _myIndex == 0 ? ps2 : ps1;
            _myInv = _me.GetComponent<PlayerInventory>();
            _oppInv = _opp.GetComponent<PlayerInventory>();

            // 서버와 동일 순서의 레지스트리 (ItemId = 인덱스 해석용)
            _registry = ItemTestRegistry.Build(fanItem, windbreakerItem, warmTeaItem, catItem, out _);

            _me.Temperature.OnValueChanged += OnMyTempChanged;
            _opp.Temperature.OnValueChanged += OnOppTempChanged;
            _myInv.SlotStates.OnListChanged += OnSlotsChanged;
            _oppInv.SlotStates.OnListChanged += OnSlotsChanged;

            _lobbyPanel.SetActive(false);
            BuildWorld();
            BuildOverlay();
            _gameUiBuilt = true;
            RefreshTiles();
            OnMyTempChanged(0, _me.Temperature.Value);
            OnOppTempChanged(0, _opp.Temperature.Value);
            AddLog($"게임 뷰 준비 완료 — 나 = P{_myIndex + 1}");
        }

        #endregion

        #region 입력 (클릭 → ServerRpc)

        private void Update()
        {
            if (!_gameUiBuilt || _mgr == null) return;
            if (_me == null || _opp == null) return;   // RULE-011: 플레이 종료/연결 해제로 파괴된 오브젝트 접근 방지

            // 페이즈/타이머 (FIX-04: 클라 로컬 계산)
            var phase = (ItemNetTestManager.NetPhase)_mgr.Phase.Value;
            switch (phase)
            {
                case ItemNetTestManager.NetPhase.Prep:
                    float remain = _mgr.PrepDuration.Value -
                        (float)(NetworkManager.Singleton.ServerTime.Time - _mgr.PrepStartServerTime.Value);
                    _phaseText.text = $"턴 {_mgr.TurnNumber.Value} — 준비 페이즈 (남은 {Mathf.Max(0f, remain):F1}s)";
                    break;
                case ItemNetTestManager.NetPhase.Attack:
                    _phaseText.text = $"턴 {_mgr.TurnNumber.Value} — 공격 페이즈";
                    break;
                case ItemNetTestManager.NetPhase.RoundOver:
                    _phaseText.text = $"라운드 종료 — P{_mgr.WinnerIndex.Value + 1} 승리! (리셋으로 재시작)";
                    break;
                default:
                    _phaseText.text = "상대 대기 중...";
                    break;
            }

            // 상태 표시 (Ready/선풍기 — NV 기반)
            _myStateText.text = StateOf(_me);
            _oppStateText.text = StateOf(_opp);

            // 선풍기 회전 (IsFanActive NV 기반 — 시각 신호): [0]=내 쪽, [1]=상대 쪽
            SpinFan(0, _me);
            SpinFan(1, _opp);

            // 클릭 → 물리 레이캐스트 → 아이템 타일 (준비 끝은 월드스페이스 캔버스 버튼이 처리)
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            var ray = _cam.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 50f)) return;

            foreach (var tile in _myTiles)
            {
                if (hit.collider.gameObject != tile.Go) continue;
                OnTileClicked(tile.Slot);
                return;
            }
        }

        private static string StateOf(PlayerState ps)
        {
            if (ps.IsReady.Value) return "준비 완료 (회복 중)";
            return ps.IsFanActive.Value ? "선택 중 (선풍기 ON)" : "대기";
        }

        private void SpinFan(int fanIdx, PlayerState owner)
        {
            if (_fanCubes[fanIdx] == null) return;
            if (owner.IsFanActive.Value)
                _fanCubes[fanIdx].transform.Rotate(0f, 360f * Time.deltaTime, 0f);
        }

        private void OnTileClicked(int slot)
        {
            if (slot >= _myInv.SlotStates.Count) return;
            var slotData = _myInv.SlotStates[slot];
            if (slotData.IsEmpty) return;

            // 타입만 보고 알맞은 ServerRpc 선택 — 판정/검증은 전부 서버 (5.3)
            var item = _registry[slotData.ItemId];
            if (item.SlotType == ItemSlotType.Sub)
                _mgr.UseSubItemRpc((byte)slot);
            else
                _mgr.SelectMainItemRpc((byte)slot);
        }

        #endregion

        #region NV 콜백

        private void OnMyTempChanged(float _, float v)
        {
            _myTempText.text = $"{v:F1}°";
            _myBar.fillAmount = v / TemperatureSystem.MAX_TEMP;
            _myBar.color = TestUiKit.TempColor(v);
        }

        private void OnOppTempChanged(float _, float v)
        {
            _oppTempText.text = $"{v:F1}°";
            _oppBar.fillAmount = v / TemperatureSystem.MAX_TEMP;
            _oppBar.color = TestUiKit.TempColor(v);
        }

        private void OnSlotsChanged(NetworkListEvent<ItemSlotNetData> _) => RefreshTiles();

        private void AddLog(string line)
        {
            _logLines.Enqueue(line);
            while (_logLines.Count > MaxLogLines) _logLines.Dequeue();
            if (_logText == null) return;
            var sb = new StringBuilder();
            foreach (var l in _logLines) sb.AppendLine(l);
            _logText.text = sb.ToString();
        }

        #endregion

        #region 월드 그레이박스 구성

        private void BuildWorld()
        {
            // 마루 바닥
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor(마루)";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            _matFloor = MakeMat(new Color(0.45f, 0.30f, 0.18f));   // 나무색
            floor.GetComponent<Renderer>().sharedMaterial = _matFloor;

            // 카메라: 앉은 눈높이로 낮추고 시선은 들어올려 — 마루 건너 상대와 마주 보는 구도 (목업)
            _cam.transform.position = new Vector3(0f, 1.7f, -5.4f * _dir);
            _cam.transform.LookAt(new Vector3(0f, 1.3f, 2.5f * _dir));

            // 내 캐릭터는 내 화면에서 숨김 — 카메라 시야를 가림 (목업: 상대만 보임). 로컬 연출일 뿐 네트워크와 무관
            foreach (var r in _me.GetComponentsInChildren<Renderer>())
                r.enabled = false;

            // 선풍기 그레이박스 — 아이템 필드 바깥, 각 캐릭터 옆 (정보 가림 방지) + 식별 라벨
            _fanCubes[0] = MakeFanPanel("Fan_Mine", new Vector3(-4.0f * _dir, 0.5f, -1.9f * _dir), "선풍기 (나)");
            _fanCubes[1] = MakeFanPanel("Fan_Opp", new Vector3(3.8f * _dir, 0.5f, 1.9f * _dir), "선풍기 (상대)");

            // 내 아이템 타일: 기본 4개 왼쪽 2×2 | [준비 끝] 중앙 | 랜덤 8개 오른쪽 4×2 (기획 배치)
            // ※ x에도 _dir을 곱해 P1/P2 모두 "기본=왼쪽, 랜덤=오른쪽"으로 동일한 구성을 보게 함
            for (int s = 0; s < BASIC; s++)
            {
                float x = (-3.05f + (s % 2) * 0.95f) * _dir;
                float z = (-1.8f - (s / 2) * 0.7f) * _dir;
                _myTiles.Add(MakeTile($"My_Basic{s}", new Vector3(x, 0f, z), s, clickable: true, small: false));
            }
            for (int r = 0; r < RANDOM; r++)
            {
                float x = (1.15f + (r % 4) * 0.9f) * _dir;
                float z = (-1.8f - (r / 4) * 0.7f) * _dir;
                _myTiles.Add(MakeTile($"My_Random{r}", new Vector3(x, 0f, z), BASIC + r, clickable: true, small: false));
            }

            // 준비 끝 — 마루에 눕힌 월드스페이스 캔버스 버튼 (기획: 사물 느낌)
            BuildReadyWorldCanvas();

            // 상대 체온 바 — 상대 머리 위 월드스페이스 캔버스 (기획 지시)
            BuildOpponentWorldCanvas();

            // 상대 아이템 타일 (건너편, 읽기 전용 — 기획: 상대 아이템은 마루에 보임)
            for (int s = 0; s < SLOTS; s++)
            {
                float x = (s < BASIC ? (3.05f - (s % 2) * 0.95f) : (-1.15f - ((s - BASIC) % 4) * 0.9f)) * _dir;
                int row = s < BASIC ? s / 2 : (s - BASIC) / 4;
                float z = (1.5f + row * 0.7f) * _dir;
                _oppTiles.Add(MakeTile($"Opp_{s}", new Vector3(x, 0f, z), s, clickable: false, small: true));
            }
        }

        /// <summary>월드스페이스 캔버스 공통 생성 (worldCamera 지정 + GraphicRaycaster)</summary>
        private Canvas MakeWorldCanvas(string name, Vector3 pos, Quaternion rot, Vector2 size, float scale)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = Vector3.one * scale;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _cam;
            canvas.GetComponent<RectTransform>().sizeDelta = size;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>준비 끝 — 마루 위에 눕힌 월드스페이스 버튼 (Euler(90°) = 바닥에 밀착)</summary>
        private void BuildReadyWorldCanvas()
        {
            // 마루 바닥에 완전히 밀착 (기획: 바닥에 놓인 팻말)
            var canvas = MakeWorldCanvas("ReadyWorldCanvas",
                new Vector3(0f, 0.02f, -2.1f * _dir),
                Quaternion.Euler(90f, _dir > 0 ? 0f : 180f, 0f),
                new Vector2(300, 170), 0.01f);

            var readyBtn = TestUiKit.CreateButton(canvas.transform, "ReadyBtn", Vector2.zero,
                new Vector2(280, 150), "준비 끝", new Color(0.93f, 0.88f, 0.70f), out var label);
            label.color = Color.black;
            label.fontSize = 40;
            readyBtn.onClick.AddListener(() => _mgr.PressReadyRpc());
        }

        /// <summary>상대 체온 바 — 상대 머리 위 월드스페이스 캔버스 (카메라를 향하도록 회전)</summary>
        private void BuildOpponentWorldCanvas()
        {
            var pos = new Vector3(0f, 2.05f, 1.9f * _dir);   // 캐릭터 판(높이 1.7) 머리 위
            // 캔버스 +z가 카메라 반대편을 향해야 카메라에서 정상으로 읽힘
            var rot = Quaternion.LookRotation(pos - _cam.transform.position);
            var canvas = MakeWorldCanvas("OppHpWorldCanvas", pos, rot, new Vector2(340, 110), 0.01f);

            TestUiKit.CreateText(canvas.transform, "OppName", new Vector2(0, 38), new Vector2(300, 30),
                $"상대 (P{2 - _myIndex})", 20);
            _oppBar = TestUiKit.CreateBar(canvas.transform, "OppBar", new Vector2(-25, 5), new Vector2(240, 24));
            _oppTempText = TestUiKit.CreateText(canvas.transform, "OppTemp", new Vector2(135, 5), new Vector2(90, 28), "37.0°", 20);
            _oppStateText = TestUiKit.CreateText(canvas.transform, "OppState", new Vector2(0, -30), new Vector2(320, 26), "", 15);
            _oppStateText.color = new Color(1f, 0.85f, 0.75f);
        }

        private GameObject MakeFanPanel(string name, Vector3 pos, string label)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.8f, 0.8f, 0.1f);   // 납작한 판 — 회전이 눈에 보임
            go.GetComponent<Renderer>().sharedMaterial = MakeMat(new Color(0.85f, 0.85f, 0.9f));
            Object.Destroy(go.GetComponent<Collider>());   // 클릭 방해 방지
            MakeBillboardLabel(pos + Vector3.up * 0.85f, label, 1.8f);
            return go;
        }

        /// <summary>카메라를 향해 세워진 월드 텍스트 (선풍기 등 오브젝트 식별용)</summary>
        private TextMeshPro MakeBillboardLabel(Vector3 pos, string text, float fontSize)
        {
            var go = new GameObject("BillboardLabel");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(pos - _cam.transform.position);
            var tmp = go.AddComponent<TextMeshPro>();
            var font = TestUiKit.GetKoreanFont();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.GetComponent<RectTransform>().sizeDelta = new Vector2(3f, 0.6f);
            return tmp;
        }

        private Tile MakeTile(string name, Vector3 pos, int slot, bool clickable, bool small)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos + Vector3.up * 0.03f;
            go.transform.localScale = small ? new Vector3(0.8f, 0.05f, 0.6f) : new Vector3(0.85f, 0.06f, 0.65f);
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = MakeMat(Color.gray);
            if (!clickable) Object.Destroy(go.GetComponent<Collider>());

            var label = MakeFloorLabel(go.transform.position + Vector3.up * 0.05f, "", small ? 1.6f : 2.0f, Color.white);
            return new Tile { Go = go, Rend = rend, Label = label, Slot = slot };
        }

        /// <summary>바닥 위 라벨 — 낮은 카메라 앵글에서도 읽히게 살짝 세운 팻말 형태</summary>
        private TextMeshPro MakeFloorLabel(Vector3 pos, string text, float fontSize, Color color)
        {
            var go = new GameObject("Label");
            go.transform.position = pos + Vector3.up * 0.08f;
            go.transform.rotation = Quaternion.Euler(60f, _dir > 0 ? 0f : 180f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            var font = TestUiKit.GetKoreanFont();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            var rect = tmp.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(3f, 1f);
            return tmp;
        }

        private static Material MakeMat(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");   // URP 미사용 폴백
            return new Material(shader) { color = c };
        }

        private static readonly Color EmptyColor = new(0.35f, 0.35f, 0.38f);

        private Color CategoryColor(ItemDataSO item)
        {
            return item.Category switch
            {
                ItemCategory.Attack => new Color(0.75f, 0.25f, 0.2f),
                ItemCategory.Defense => new Color(0.2f, 0.45f, 0.75f),
                ItemCategory.Recovery => new Color(0.25f, 0.6f, 0.3f),
                ItemCategory.Sabotage => new Color(0.6f, 0.4f, 0.7f),
                _ => Color.gray,
            };
        }

        private void RefreshTiles()
        {
            if (!_gameUiBuilt) return;
            RefreshSide(_myTiles, _myInv, mine: true);
            RefreshSide(_oppTiles, _oppInv, mine: false);
        }

        private void RefreshSide(List<Tile> tiles, PlayerInventory inv, bool mine)
        {
            var slots = inv.SlotStates;
            if (slots == null) return;
            foreach (var tile in tiles)
            {
                if (tile.Slot >= slots.Count || slots[tile.Slot].IsEmpty)
                {
                    tile.Rend.sharedMaterial.color = EmptyColor;
                    tile.Label.text = mine ? "(빈 슬롯)" : "";
                    continue;
                }
                var slot = slots[tile.Slot];
                var item = _registry[slot.ItemId];
                string uses = slot.IsUnlimited ? "∞" : slot.RemainingUses.ToString();
                tile.Rend.sharedMaterial.color = slot.IsUsable
                    ? CategoryColor(item)
                    : Color.Lerp(CategoryColor(item), EmptyColor, 0.7f);
                tile.Label.text = $"{item.ItemName}\n({uses})";
            }
        }

        #endregion

        #region 오버레이 UI 구성

        /// <summary>접속 로비 — AZLobbyUI 메인 패널과 동일한 디자인 언어 (중앙 패널 + 타이틀 + 버튼 스택)</summary>
        private void BuildLobbyUI()
        {
            var root = TestUiKit.CreateCanvas("NetTestCanvas");
            _lobbyPanel = new GameObject("LobbyPanel");
            _lobbyPanel.transform.SetParent(root, false);
            var rt = _lobbyPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            TestUiKit.CreatePanel(_lobbyPanel.transform, "BG", Vector2.zero,
                new Vector2(500, 450), new Color(0.08f, 0.08f, 0.12f, 0.92f));

            TestUiKit.CreateText(_lobbyPanel.transform, "Title",
                new Vector2(0, 160), new Vector2(460, 60), "ABSOLUTE ZERO", 42);

            var subtitle = TestUiKit.CreateText(_lobbyPanel.transform, "Subtitle",
                new Vector2(0, 110), new Vector2(460, 30), "아이템 네트워크 테스트", 18);
            subtitle.color = new Color(0.6f, 0.6f, 0.7f);

            _hostBtn = TestUiKit.CreateButton(_lobbyPanel.transform, "HostBtn",
                new Vector2(0, 40), new Vector2(300, 55), "호스트 시작", new Color(0.2f, 0.5f, 0.8f), out _);
            _hostBtn.onClick.AddListener(OnHostClicked);

            _orText = TestUiKit.CreateText(_lobbyPanel.transform, "OrText",
                new Vector2(0, -10), new Vector2(120, 25), "— 또는 —", 16);
            _orText.color = new Color(0.5f, 0.5f, 0.5f);

            _clientBtn = TestUiKit.CreateButton(_lobbyPanel.transform, "ClientBtn",
                new Vector2(0, -55), new Vector2(300, 55), "클라이언트 접속", new Color(0.3f, 0.6f, 0.3f), out _);
            _clientBtn.onClick.AddListener(OnClientClicked);

            // 호스트 시작 후 같은 자리에 노출 (1인 테스트용)
            _dummyBtn = TestUiKit.CreateButton(_lobbyPanel.transform, "DummyBtn",
                new Vector2(0, -55), new Vector2(300, 55), "더미 상대 추가", new Color(0.8f, 0.4f, 0.1f), out _);
            _dummyBtn.onClick.AddListener(OnDummyClicked);
            _dummyBtn.gameObject.SetActive(false);

            _lobbyStatusText = TestUiKit.CreateText(_lobbyPanel.transform, "Status",
                new Vector2(0, -130), new Vector2(460, 30), "모드를 선택하세요", 16);
            _lobbyStatusText.color = new Color(0.7f, 0.7f, 0.7f);
        }

        private void BuildOverlay()
        {
            var root = TestUiKit.CreateCanvas("NetTestOverlay");

            // 내 온도 바 — 좌상단 크게, Overlay 캔버스 (기획 지시: 내 HP바만 오버레이)
            // 화면 잘림 방지를 위해 상단에서 약간 내림
            TestUiKit.CreateText(root, "MyName", new Vector2(-700, 440), new Vector2(200, 32), $"나 (P{_myIndex + 1})", 22);
            _myBar = TestUiKit.CreateBar(root, "MyBar", new Vector2(-560, 400), new Vector2(480, 30));
            _myTempText = TestUiKit.CreateText(root, "MyTemp", new Vector2(-260, 400), new Vector2(120, 32), "37.0°", 24);
            _myStateText = TestUiKit.CreateText(root, "MyState", new Vector2(-560, 365), new Vector2(480, 26), "", 15);
            _myStateText.color = new Color(0.75f, 0.9f, 1f);

            // 페이즈/타이머 + 리셋
            _phaseText = TestUiKit.CreateText(root, "PhaseText", new Vector2(600, 440), new Vector2(560, 36), "", 20);
            var resetBtn = TestUiKit.CreateButton(root, "ResetBtn", new Vector2(760, 390), new Vector2(140, 44),
                "리셋", new Color(0.5f, 0.3f, 0.3f), out _);
            resetBtn.onClick.AddListener(() => _mgr.RequestResetRpc());

            // 로그 — 좌하단 반투명
            TestUiKit.CreatePanel(root, "LogBG", new Vector2(-640, -330), new Vector2(560, 300), new Color(0f, 0f, 0f, 0.45f));
            _logText = TestUiKit.CreateText(root, "LogText", new Vector2(-640, -330), new Vector2(530, 280), "", 14);
            _logText.alignment = TextAlignmentOptions.TopLeft;
        }

        #endregion
    }
}
