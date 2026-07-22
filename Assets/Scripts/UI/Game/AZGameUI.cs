using System.Collections;
using AbsoluteZero.Core.Audio;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using AbsoluteZero.UI.MiniGame;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Game
{
    public class AZGameUI : MonoBehaviour
    {
        TurnManager _tm;
        MatchManager _mm;
        PlayerState _localPlayer;
        PlayerState _p1Cached;
        PlayerState _p2Cached;

        Canvas _overlayCanvas;
        TextMeshProUGUI _phaseText;
        TextMeshProUGUI _timerText;
        TextMeshProUGUI _statusText;
        TextMeshProUGUI _scoreText;
        TextMeshProUGUI _myTempText;
        Slider _myHpSlider;

        Image _timerFillImage;
        Image _timerSliderImage;
        RectTransform _clockHandRT;
        GameObject _timeAlarmObj;
        Image _timeAlarmImage;
        bool _alarmShaking;
        Coroutine _alarmCoroutine;

        Canvas _oppBarCanvas;
        TextMeshProUGUI _oppTempText;
        Slider _oppHpSlider;
        Image _myHpFillImage;
        Image _oppHpFillImage;

        Canvas _readyCanvas;
        Button _readyButton;
        Image _readyButtonImage;

        GameObject _gameOverPanel;
        TextMeshProUGUI _gameOverText;

        ItemWorldDisplay _worldDisplay;
        GameObject[] _oppItemObjects;
        PlayerInventory _oppInventory;
        bool _oppItemsSpawned;

        TextMeshProUGUI _stackText;
        string _selectedMainName;
        string _selectedSubName;

        GameObject _resultPanel;
        TextMeshProUGUI _resultText;
        Coroutine _resultHideCoroutine;

        GameObject _envPanel;
        TextMeshProUGUI _envText;

        RectTransform _timerContainerRT;
        Vector2 _timerContainerBasePos;
        bool _summerVacShaking;
        Coroutine _summerVacShakeCoroutine;

        bool _oppBarAttached;

        bool _uiBuilt;

        static readonly WaitForSeconds _waitEnvHide = new(3.5f);
        static readonly WaitForSeconds _waitAlarmShake = new(0.05f);
        static readonly WaitForSeconds _waitSummerShake = new(0.016f);

        static readonly Color HP_COLOR_GREEN = new(0.39f, 0.78f, 0.31f, 1f);
        static readonly Color HP_COLOR_PINK = new(0.90f, 0.47f, 0.59f, 1f);
        static readonly Color HP_COLOR_SKY = new(0.39f, 0.71f, 0.92f, 1f);
        static readonly Color HP_COLOR_BLUE = new(0.20f, 0.39f, 0.86f, 1f);

        static readonly Vector3 OPP_BAR_LOCAL_OFFSET = new(0f, 2.2f, 0f);
        static readonly Vector3 READY_BTN_POS = new(0f, 0.35f, 1.2f);
        const float WORLD_CANVAS_SCALE = 0.005f;
        const float OPP_BAR_SCALE = 0.007f;
        const int ALARM_TIME_THRESHOLD = 5;

        void Start()
        {
            EnsureEventSystem();
            BuildOverlayUI();
            BuildOppBarWorldUI();
            BuildReadyWorldUI();
            _uiBuilt = true;

            var worldDisplayGO = new GameObject("ItemWorldDisplay");
            var spawnRoot = GameObject.Find("MyItemSpawnRoot");
            if (spawnRoot != null)
                worldDisplayGO.transform.position = spawnRoot.transform.position;
            _worldDisplay = worldDisplayGO.AddComponent<ItemWorldDisplay>();
            _worldDisplay.OnWorldItemClicked += OnItemClicked;

            var hubGO = new GameObject("MiniGameHub");
            hubGO.AddComponent<MiniGameHub>();
            MiniGameHub.OnFinishedLocal += OnMiniGameFinished;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var debugGranterGO = new GameObject("DebugItemGranter");
            debugGranterGO.AddComponent<Core.Game.DebugItemGranter>();
#endif

            SpawnStayItemFans();
            EnsureAudioManager();
        }

        void SpawnStayItemFans()
        {
            // 새 선풍기 아트 (몸체 + 회전 날개 + 정면 그릴). 없으면 기존 플레이스홀더로 폴백.
            var bodySprite = Resources.Load<Sprite>("Fan/fan_body");
            var bladesSprite = Resources.Load<Sprite>("Fan/fan_blades");
            var grilleSprite = Resources.Load<Sprite>("Fan/fan_grille");
            var fallback = GameSprites.GetStayItemSprite();

            // ── 선풍기 미세조정 값 (FanTestScene에서 맞춘 값) ──
            const float fanLift = 1.4f;                             // 테이블에 안 박히게 위로 올리는 높이
            var bladesPos = new Vector3(-0.23f, 0.6f, -0.01f);      // 날개 위치 (몸체 로컬)
            var bladesScale = new Vector3(1.2f, 1.2f, 1f);          // 날개 크기
            const float bladesTiltY = 35f;                          // 날개 Y 기울기 (3/4 뷰 원근감)
            var grillePos = new Vector3(-0.16f, 0.59f, -0.02f);     // 뚜껑 위치 (몸체 로컬)
            var grilleScale = new Vector3(0.92f, 1.02f, 1f);        // 뚜껑 크기

            var litMat = Resources.Load<Material>("sprite3DMat");
            // 마커별 바인딩: 내 선풍기 = 로컬(-1), 상대 선풍기 = 상대(-2)
            var markers = new (string name, int playerIndex)[]
            {
                ("PlayerStayItem", -1),
                ("EnemyStayItem", -2),
            };

            foreach (var (name, playerIndex) in markers)
            {
                var marker = GameObject.Find(name);
                if (marker == null) continue;

                var go = new GameObject($"{name}_Fan");
                go.transform.SetParent(marker.transform, false);
                go.transform.localPosition = new Vector3(0f, fanLift, 0f);   // 위로 올려 테이블에 안 박히게

                var bodySr = go.AddComponent<SpriteRenderer>();
                bodySr.sprite = bodySprite != null ? bodySprite : fallback;
                bodySr.sortingOrder = 3;
                if (litMat != null) bodySr.material = litMat;

                // 새 아트가 있으면 회전 날개 + 정면 그릴(뚜껑) 조립
                if (bodySprite != null && bladesSprite != null)
                {
                    // 날개 (그릴 뒤에서 회전) — Y 기울기 후 로컬 Z축 회전이라 원근감 유지된 채 깔끔히 돔
                    var blades = new GameObject("Blades");
                    blades.transform.SetParent(go.transform, false);
                    blades.transform.localPosition = bladesPos;
                    blades.transform.localScale = bladesScale;
                    blades.transform.localRotation = Quaternion.Euler(0f, bladesTiltY, 0f);

                    var bladeSr = blades.AddComponent<SpriteRenderer>();
                    bladeSr.sprite = bladesSprite;
                    bladeSr.sortingOrder = 4;
                    if (litMat != null) bladeSr.material = litMat;

                    var spinner = go.AddComponent<AbsoluteZero.Core.Common.FanBladeSpinner>();
                    spinner.Bind(blades.transform, playerIndex);

                    // 그릴(뚜껑) — 날개 앞 정적 커버
                    if (grilleSprite != null)
                    {
                        var grille = new GameObject("Grille");
                        grille.transform.SetParent(go.transform, false);
                        grille.transform.localPosition = grillePos;
                        grille.transform.localScale = grilleScale;

                        var grilleSr = grille.AddComponent<SpriteRenderer>();
                        grilleSr.sprite = grilleSprite;
                        grilleSr.sortingOrder = 5;
                        if (litMat != null) grilleSr.material = litMat;
                    }
                }
            }
        }

        void EnsureAudioManager()
        {
            if (GameAudioManager.Instance != null) return;
            var go = new GameObject("GameAudioManager");
            go.AddComponent<GameAudioManager>();
            DontDestroyOnLoad(go);
            GameAudioManager.Instance?.PlayBGM();
        }

        void Update()
        {
            if (!_uiBuilt) return;

            if (_tm == null)
            {
                _tm = TurnManager.Instance;
                if (_tm == null) return;
                _mm = FindAnyObjectByType<MatchManager>();
                SubscribeToEvents();
            }

            if (_localPlayer == null)
                FindLocalPlayer();

            if (_p1Cached == null || _p2Cached == null)
                FindAllPlayers();

            UpdateTimerDisplay();
            UpdateTempDisplay();
            UpdateScoreDisplay();
            UpdateOppBarTransform();

            if (!_oppItemsSpawned)
                TrySpawnOpponentItems();
        }

        void OnDestroy()
        {
            TurnManager.OnCombatResult -= OnCombatResultReceived;
            TurnManager.OnOpponentRevealed -= OnOpponentRevealed;
            TurnManager.OnEnvironmentAnnounced -= OnEnvironmentAnnounced;
            if (_tm != null)
            {
                _tm.CurrentPhase.OnValueChanged -= OnPhaseChanged;
                _tm.LastRoundWinner.OnValueChanged -= OnWinnerChanged;
            }
            if (_localPlayer != null)
                _localPlayer.HasSelectedItem.OnValueChanged -= OnHasSelectedItemChanged;
            MiniGameHub.OnFinishedLocal -= OnMiniGameFinished;
            StopSummerVacShake();

            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.StopFanLoop();
                GameAudioManager.Instance.StopClockTick();
                GameAudioManager.Instance.StopEnvironment();
                GameAudioManager.Instance.StopBGM();
            }
            if (_worldDisplay != null)
                _worldDisplay.OnWorldItemClicked -= OnItemClicked;
            if (_oppInventory != null)
                _oppInventory.SlotStates.OnListChanged -= OnOppSlotStatesChanged;
            if (_oppItemObjects != null)
            {
                foreach (var go in _oppItemObjects)
                    if (go != null) Destroy(go);
            }
            CancelInvoke();
        }

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
        }

        void FindLocalPlayer()
        {
            if (NetworkManager.Singleton == null) return;

            var localObj = NetworkManager.Singleton.SpawnManager?.GetLocalPlayerObject();
            if (localObj == null) return;

            var ps = localObj.GetComponent<PlayerState>();
            if (ps == null) return;

            var inv = ps.GetInventory();
            if (inv == null || ItemManager.Instance == null) return;

            ItemManager.Instance.InitializeClientRegistry(inv);

            _localPlayer = ps;
            _localPlayer.HasSelectedItem.OnValueChanged += OnHasSelectedItemChanged;
        }

        void FindAllPlayers()
        {
            var players = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
            if (players.Length < 2) return;

            if (players[0].OwnerClientId <= players[1].OwnerClientId)
            {
                _p1Cached = players[0];
                _p2Cached = players[1];
            }
            else
            {
                _p1Cached = players[1];
                _p2Cached = players[0];
            }
        }

        PlayerState GetOpponentPlayer()
        {
            if (_localPlayer == null || _p1Cached == null || _p2Cached == null) return null;
            return _p1Cached.OwnerClientId == _localPlayer.OwnerClientId ? _p2Cached : _p1Cached;
        }

        void SubscribeToEvents()
        {
            _tm.CurrentPhase.OnValueChanged += OnPhaseChanged;
            _tm.LastRoundWinner.OnValueChanged += OnWinnerChanged;
            TurnManager.OnCombatResult += OnCombatResultReceived;
            TurnManager.OnOpponentRevealed += OnOpponentRevealed;
            TurnManager.OnEnvironmentAnnounced += OnEnvironmentAnnounced;
            OnPhaseChanged(TurnPhase.WaitingForPlayers, _tm.CurrentPhase.Value);
        }

        void OnPhaseChanged(TurnPhase oldPhase, TurnPhase newPhase)
        {
            _phaseText.text = newPhase switch
            {
                TurnPhase.WaitingForPlayers => "WAITING",
                TurnPhase.PrepPhase => "PREP PHASE",
                TurnPhase.AttackPhase => "ATTACK",
                TurnPhase.ResolutionPhase => "RESOLUTION",
                TurnPhase.RoundOver => "ROUND OVER",
                _ => ""
            };

            if (_readyCanvas != null)
            {
                _readyCanvas.gameObject.SetActive(newPhase == TurnPhase.PrepPhase);
                if (newPhase == TurnPhase.PrepPhase)
                {
                    var defaultSprite = GameSprites.Get(GameSprites.BTN_DEFAULT);
                    if (_readyButtonImage != null && defaultSprite != null)
                        _readyButtonImage.sprite = defaultSprite;
                    if (_readyButton != null)
                        _readyButton.interactable = true;
                }
            }

            if (newPhase == TurnPhase.PrepPhase)
            {
                _statusText.text = "Select an item!";
                _gameOverPanel.SetActive(false);
                if (_resultPanel != null) _resultPanel.SetActive(false);
                _selectedMainName = null;
                _selectedSubName = null;
                UpdateStackDisplay();
                GameAudioManager.Instance?.StartFanLoop();
            }
            else if (newPhase == TurnPhase.AttackPhase)
            {
                _statusText.text = "Resolving...";
                _gameOverPanel.SetActive(false);
                GameAudioManager.Instance?.StopFanLoop();
            }
            else if (newPhase == TurnPhase.RoundOver)
            {
                GameAudioManager.Instance?.StopFanLoop();
                GameAudioManager.Instance?.StopEnvironment();
                Invoke(nameof(ShowRoundResult), 0.15f);
            }
        }

        void OnWinnerChanged(int oldVal, int newVal)
        {
            if (_tm.CurrentPhase.Value == TurnPhase.RoundOver)
                ShowRoundResult();
        }

        void OnHasSelectedItemChanged(bool oldVal, bool newVal)
        {
            if (newVal)
                _statusText.text = "Item selected!";
        }

        void ShowRoundResult()
        {
            _gameOverPanel.SetActive(true);

            int winner = _tm.LastRoundWinner.Value;
            bool isMatchDone = _mm != null && _mm.IsMatchComplete();

            if (winner < 0)
            {
                _gameOverText.text = "DRAW!";
                _gameOverText.color = Color.yellow;
            }
            else
            {
                bool iAmWinner = _localPlayer != null && _localPlayer.SyncedPlayerIndex.Value == winner;
                if (isMatchDone)
                {
                    _gameOverText.text = iAmWinner ? "MATCH WIN!" : "MATCH LOSE...";
                    _gameOverText.color = iAmWinner ? new Color(1f, 0.85f, 0.2f) : new Color(0.5f, 0.5f, 0.6f);
                }
                else
                {
                    _gameOverText.text = iAmWinner ? "ROUND WIN!" : "ROUND LOSE...";
                    _gameOverText.color = iAmWinner ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.3f, 0.3f);
                    _statusText.text = "Next round starting...";
                }
            }
        }

        void UpdateTimerDisplay()
        {
            if (_tm == null || _tm.CurrentPhase.Value != TurnPhase.PrepPhase)
            {
                _timerText.text = "";
                if (_timerSliderImage != null) _timerSliderImage.fillAmount = 0f;
                if (_clockHandRT != null) _clockHandRT.localRotation = Quaternion.identity;
                SetAlarmActive(false);
                return;
            }

            int remaining = _tm.RemainingTime.Value;
            float total = _tm.PrepDuration.Value;
            float ratio = total > 0f ? Mathf.Clamp01(remaining / total) : 1f;

            _timerText.text = $"{remaining}";

            if (_timerSliderImage != null)
                _timerSliderImage.fillAmount = 1f - ratio;
            if (_clockHandRT != null)
                _clockHandRT.localRotation = Quaternion.Euler(0f, 0f, ratio * 360f);

            bool shouldAlarm = remaining > 0 && remaining <= ALARM_TIME_THRESHOLD;
            SetAlarmActive(shouldAlarm);

            if (remaining <= ALARM_TIME_THRESHOLD)
                _timerText.color = Color.red;
            else
                _timerText.color = Color.white;
        }

        void SetAlarmActive(bool active)
        {
            if (_timeAlarmObj == null) return;
            if (active && !_alarmShaking)
            {
                _timeAlarmObj.SetActive(true);
                _alarmShaking = true;
                _alarmCoroutine = StartCoroutine(AlarmShakeRoutine());
                GameAudioManager.Instance?.PlayClockTick();
            }
            else if (!active && _alarmShaking)
            {
                _alarmShaking = false;
                if (_alarmCoroutine != null) StopCoroutine(_alarmCoroutine);
                _timeAlarmObj.SetActive(false);
                GameAudioManager.Instance?.StopClockTick();
            }
        }

        IEnumerator AlarmShakeRoutine()
        {
            var rt = _timeAlarmObj.GetComponent<RectTransform>();
            var basePos = rt.anchoredPosition;
            while (_alarmShaking)
            {
                float ox = Random.Range(-8f, 8f);
                float oy = Random.Range(-4f, 4f);
                rt.anchoredPosition = basePos + new Vector2(ox, oy);
                yield return _waitAlarmShake;
            }
            rt.anchoredPosition = basePos;
        }

        void UpdateTempDisplay()
        {
            if (_localPlayer != null)
            {
                float myT = _localPlayer.Temperature.Value;
                _myTempText.text = $"{myT:F0}°";
                if (_myHpSlider != null)
                    _myHpSlider.value = Mathf.Clamp01(myT / 37f);
                if (_myHpFillImage != null)
                    _myHpFillImage.color = GetTempColor(myT);
            }

            var opp = GetOpponentPlayer();
            if (opp != null)
            {
                float oppT = opp.Temperature.Value;
                _oppTempText.text = $"{oppT:F0}°";
                if (_oppHpSlider != null)
                    _oppHpSlider.value = Mathf.Clamp01(oppT / 37f);
                if (_oppHpFillImage != null)
                    _oppHpFillImage.color = GetTempColor(oppT);
            }
        }

        static Color GetTempColor(float temp)
        {
            if (temp >= 30f)
            {
                float t = Mathf.InverseLerp(37f, 30f, temp);
                return Color.Lerp(HP_COLOR_GREEN, HP_COLOR_PINK, t);
            }
            if (temp >= 20f)
            {
                float t = Mathf.InverseLerp(30f, 20f, temp);
                return Color.Lerp(HP_COLOR_PINK, HP_COLOR_SKY, t);
            }
            if (temp >= 10f)
            {
                float t = Mathf.InverseLerp(20f, 10f, temp);
                return Color.Lerp(HP_COLOR_SKY, HP_COLOR_BLUE, t);
            }
            return HP_COLOR_BLUE;
        }

        void UpdateScoreDisplay()
        {
            if (_mm == null) return;
            _scoreText.text = $"P1  {_mm.P1RoundWins.Value} : {_mm.P2RoundWins.Value}  P2";
        }

        void UpdateOppBarTransform()
        {
            if (_oppBarCanvas == null) return;

            if (!_oppBarAttached)
            {
                Transform target = null;

                var opp = GetOpponentPlayer();
                if (opp != null)
                    target = opp.transform;

                if (target == null)
                {
                    var enemyGO = GameObject.Find("EnemyPlayer");
                    if (enemyGO != null)
                        target = enemyGO.transform;
                }

                if (target != null)
                {
                    _oppBarCanvas.transform.SetParent(target, false);
                    _oppBarCanvas.transform.localPosition = OPP_BAR_LOCAL_OFFSET;
                    _oppBarAttached = true;
                }
            }

            var cam = Camera.main;
            if (cam == null) return;

            _oppBarCanvas.worldCamera = cam;
            _oppBarCanvas.transform.rotation = cam.transform.rotation;
        }

        void OnMiniGameFinished(byte slotIndex, bool success)
        {
            if (_statusText != null)
                _statusText.text = success ? "Mini-game clear!" : "Mini-game failed...";
        }

        void OnItemClicked(int slotIndex)
        {
            if (_localPlayer == null) return;
            if (_localPlayer.IsReady.Value) return;
            if (MiniGameHub.IsRunning) return;

            var inv = _localPlayer.GetInventory();
            if (inv == null || slotIndex >= inv.SlotStates.Count) return;

            var itemData = inv.GetItemData(slotIndex);
            if (itemData == null) return;

            if (_localPlayer.HasSelectedItem.Value)
                return;

            GameAudioManager.Instance?.PlayButtonClick();
            _localPlayer.SelectItemServerRpc((byte)slotIndex);
            _worldDisplay?.NotifyItemConfirmed(slotIndex);

            _selectedMainName = itemData.ItemName;

            UpdateStackDisplay();

            _statusText.text = $"Selected: {itemData.ItemName}";
        }

        void OnReadyClicked()
        {
            if (HoverRaycaster.Instance != null && HoverRaycaster.Instance.CurrentHovered != null)
                return;

            GameAudioManager.Instance?.PlayButtonClick();
            if (_localPlayer == null) return;
            if (MiniGameHub.IsRunning) return;

            _localPlayer.PressReadyServerRpc();
            _statusText.text = _localPlayer.HasSelectedItem.Value
                ? "Ready!"
                : "Ready! (No action)";

            var pressedSprite = GameSprites.Get(GameSprites.BTN_PRESSED);
            if (_readyButtonImage != null && pressedSprite != null)
                _readyButtonImage.sprite = pressedSprite;

            if (_readyButton != null)
                _readyButton.interactable = false;
        }

        void OnBackToLobbyClicked()
        {
            GameAudioManager.Instance?.PlayButtonClick();
            var sessionManager = Core.Network.SessionManager.Instance;
            if (sessionManager != null)
                sessionManager.Disconnect();
        }

        void TrySpawnOpponentItems()
        {
            var opp = GetOpponentPlayer();
            if (opp == null) return;

            var inv = opp.GetInventory();
            if (inv == null || inv.SlotStates == null || inv.SlotStates.Count == 0) return;
            if (ItemManager.Instance == null) return;

            ItemManager.Instance.InitializeClientRegistry(inv);

            _oppInventory = inv;
            _oppInventory.SlotStates.OnListChanged += OnOppSlotStatesChanged;

            RebuildOpponentItems();
            _oppItemsSpawned = true;
        }

        void OnOppSlotStatesChanged(NetworkListEvent<ItemSlotNetData> changeEvent)
        {
            RebuildOpponentItems();
        }

        void RebuildOpponentItems()
        {
            if (_oppInventory == null) return;

            if (_oppItemObjects != null)
            {
                foreach (var go in _oppItemObjects)
                    if (go != null) Destroy(go);
            }

            int slotCount = _oppInventory.SlotStates.Count;
            var litMat = Resources.Load<Material>("sprite3DMat");
            var activeItems = new System.Collections.Generic.List<GameObject>();

            for (int i = 0; i < slotCount; i++)
            {
                var slot = _oppInventory.SlotStates[i];
                if (slot.IsEmpty) continue;

                var itemData = _oppInventory.GetItemData(i);
                if (itemData == null) continue;

                string itemName = itemData.ItemName;
                var go = new GameObject($"OppItem_{activeItems.Count}_{itemName}");

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GameSprites.GetItemSprite(itemName);
                if (litMat != null) sr.material = litMat;
                sr.sortingOrder = 5;

                activeItems.Add(go);
            }

            for (int i = 0; i < activeItems.Count; i++)
            {
                var marker = GameObject.Find($"EnemyItem{i + 1}");
                if (marker != null)
                    activeItems[i].transform.position = marker.transform.position;
            }

            _oppItemObjects = activeItems.ToArray();
        }

        #region UI Construction

        void BuildOverlayUI()
        {
            var canvasGO = new GameObject("OverlayCanvas");
            _overlayCanvas = canvasGO.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 0;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0f;

            canvasGO.AddComponent<GraphicRaycaster>();

            Transform root = canvasGO.transform;

            // === TOP-LEFT: My HP Bar (sprite) ===
            BuildMyHpBar(root);

            // === TOP-RIGHT: Clock + Timer ===
            BuildClockTimer(root);

            // === TOP-CENTER: Phase + Score ===
            _phaseText = CreateText(root, "PhaseText",
                new Vector2(0, -30), new Vector2(400, 50), "WAITING", 28);
            AnchorTopCenter(_phaseText.GetComponent<RectTransform>());

            _scoreText = CreateText(root, "ScoreText",
                new Vector2(0, -75), new Vector2(300, 30), "P1  0 : 0  P2", 20);
            _scoreText.color = new Color(0.9f, 0.9f, 0.6f);
            AnchorTopCenter(_scoreText.GetComponent<RectTransform>());

            // === RIGHT: Item Stack ===
            BuildItemStack(root);

            // === BOTTOM: Status ===
            _statusText = CreateText(root, "StatusText",
                new Vector2(0, 30), new Vector2(600, 35), "Waiting for players...", 20);
            _statusText.color = new Color(0.8f, 0.8f, 0.8f);
            AnchorBottomCenter(_statusText.GetComponent<RectTransform>());

            _gameOverPanel = new GameObject("GameOverPanel");
            _gameOverPanel.transform.SetParent(root, false);
            var goRect = _gameOverPanel.AddComponent<RectTransform>();
            goRect.anchoredPosition = new Vector2(0, -10);
            goRect.sizeDelta = new Vector2(500, 150);

            CreatePanel(_gameOverPanel.transform, "GOBg", Vector2.zero,
                new Vector2(500, 150), new Color(0.05f, 0.05f, 0.1f, 0.95f));

            _gameOverText = CreateText(_gameOverPanel.transform, "GOText",
                new Vector2(0, 25), new Vector2(400, 60), "", 42);

            var lobbyBtn = CreateButton(_gameOverPanel.transform, "LobbyBtn",
                new Vector2(0, -40), new Vector2(200, 45), "BACK TO LOBBY",
                new Color(0.5f, 0.3f, 0.3f));
            lobbyBtn.onClick.AddListener(OnBackToLobbyClicked);

            _gameOverPanel.SetActive(false);

            BuildResultPanel(root);
            BuildEnvironmentPanel(root);
        }

        void BuildMyHpBar(Transform root)
        {
            var container = new GameObject("MyHpBar");
            container.transform.SetParent(root, false);
            var cRect = container.AddComponent<RectTransform>();
            cRect.anchoredPosition = new Vector2(395.8f, -81.9f);
            cRect.sizeDelta = new Vector2(600, 100);
            cRect.anchorMin = new Vector2(0f, 1f);
            cRect.anchorMax = new Vector2(0f, 1f);
            cRect.pivot = new Vector2(0.5f, 0.5f);

            _myHpSlider = container.AddComponent<Slider>();
            _myHpSlider.minValue = 0f;
            _myHpSlider.maxValue = 1f;
            _myHpSlider.value = 1f;
            _myHpSlider.interactable = false;
            _myHpSlider.direction = Slider.Direction.LeftToRight;

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(container.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.sprite = GameSprites.Get(GameSprites.UI_BAR_BG);
            bgImg.color = Color.white;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(container.transform, false);
            var faRect = fillArea.AddComponent<RectTransform>();
            faRect.anchorMin = new Vector2(0f, 0.25f);
            faRect.anchorMax = new Vector2(1f, 0.75f);
            faRect.offsetMin = Vector2.zero;
            faRect.offsetMax = Vector2.zero;

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillArea.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = GameSprites.Get(GameSprites.UI_BAR_FILL);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.color = HP_COLOR_GREEN;
            _myHpFillImage = fillImg;

            _myHpSlider.fillRect = fillRect;

            var outlineGO = new GameObject("Outline");
            outlineGO.transform.SetParent(container.transform, false);
            var olRect = outlineGO.AddComponent<RectTransform>();
            olRect.anchoredPosition = new Vector2(3.6f, 0f);
            olRect.sizeDelta = new Vector2(620, 80);
            var olImg = outlineGO.AddComponent<Image>();
            olImg.sprite = GameSprites.Get(GameSprites.UI_BAR_OUTLINE);
            olImg.raycastTarget = false;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(container.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(-330.79f, -25.85f);
            iconRect.sizeDelta = new Vector2(100, 200);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = GameSprites.Get(GameSprites.UI_THERMO_ICON);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            BuildGiftLines(container.transform);

            _myTempText = CreateText(container.transform, "MyTemp",
                new Vector2(0, -55), new Vector2(200, 24), "37°", 20);
            _myTempText.alignment = TextAlignmentOptions.Center;
        }

        void BuildGiftLines(Transform parent)
        {
            var giftRoot = new GameObject("GiftLine");
            giftRoot.transform.SetParent(parent, false);
            var grRect = giftRoot.AddComponent<RectTransform>();
            grRect.anchorMin = Vector2.zero;
            grRect.anchorMax = Vector2.one;
            grRect.offsetMin = Vector2.zero;
            grRect.offsetMax = Vector2.zero;

            float[] xPositions = { 159.1f, 324.6f, 485.8f };
            string[] iconNames = { GameSprites.UI_GIFT_ICON_A, GameSprites.UI_GIFT_ICON_B, GameSprites.UI_GIFT_ICON_C };
            Vector2[] iconSizes = { new(70, 70), new(70, 35), new(35, 35) };
            float[] iconYOffsets = { -50f, -30f, -30f };

            for (int i = 0; i < 3; i++)
            {
                var lineGO = new GameObject($"line_{(i + 1) * 10}");
                lineGO.transform.SetParent(giftRoot.transform, false);
                var lineRect = lineGO.AddComponent<RectTransform>();
                lineRect.anchorMin = new Vector2(0f, 0.5f);
                lineRect.anchorMax = new Vector2(0f, 0.5f);
                lineRect.pivot = new Vector2(0.5f, 0.5f);
                lineRect.anchoredPosition = new Vector2(xPositions[i], 0f);
                lineRect.sizeDelta = new Vector2(7.45f, 70f);
                var lineImg = lineGO.AddComponent<Image>();
                lineImg.sprite = GameSprites.Get(GameSprites.UI_GIFT_LINE);
                lineImg.raycastTarget = false;

                var giftGO = new GameObject("Icon_Gift");
                giftGO.transform.SetParent(lineGO.transform, false);
                var giftRect = giftGO.AddComponent<RectTransform>();
                giftRect.anchoredPosition = new Vector2(0f, iconYOffsets[i]);
                giftRect.sizeDelta = iconSizes[i];
                var giftImg = giftGO.AddComponent<Image>();
                giftImg.sprite = GameSprites.Get(iconNames[i]);
                giftImg.raycastTarget = false;
            }
        }

        void BuildClockTimer(Transform root)
        {
            var container = new GameObject("Timer");
            container.transform.SetParent(root, false);
            var cRect = container.AddComponent<RectTransform>();
            cRect.anchoredPosition = new Vector2(-108, -108);
            cRect.sizeDelta = new Vector2(204, 206);
            cRect.localScale = new Vector3(0.7f, 0.7f, 1f);
            AnchorTopRight(cRect);
            _timerContainerRT = cRect;
            _timerContainerBasePos = cRect.anchoredPosition;

            var timerBg = new GameObject("TimerBg");
            timerBg.transform.SetParent(container.transform, false);
            var tbRect = timerBg.AddComponent<RectTransform>();
            tbRect.anchoredPosition = Vector2.zero;
            tbRect.sizeDelta = new Vector2(204, 206);
            _timerFillImage = timerBg.AddComponent<Image>();
            _timerFillImage.sprite = GameSprites.Get(GameSprites.UI_TIMER_BG);
            _timerFillImage.type = Image.Type.Simple;
            _timerFillImage.preserveAspect = true;
            _timerFillImage.color = Color.white;
            _timerFillImage.raycastTarget = false;

            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(container.transform, false);
            var slRect = sliderGO.AddComponent<RectTransform>();
            slRect.anchoredPosition = Vector2.zero;
            slRect.sizeDelta = new Vector2(204, 206);
            _timerSliderImage = sliderGO.AddComponent<Image>();
            _timerSliderImage.sprite = GameSprites.Get(GameSprites.UI_TIMER_BG);
            _timerSliderImage.type = Image.Type.Filled;
            _timerSliderImage.fillMethod = Image.FillMethod.Radial360;
            _timerSliderImage.fillOrigin = 2;
            _timerSliderImage.fillClockwise = true;
            _timerSliderImage.fillAmount = 0f;
            _timerSliderImage.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            _timerSliderImage.raycastTarget = false;

            var outlineGO = new GameObject("Outline");
            outlineGO.transform.SetParent(container.transform, false);
            var olRect = outlineGO.AddComponent<RectTransform>();
            olRect.anchoredPosition = Vector2.zero;
            olRect.sizeDelta = new Vector2(261, 261);
            var olImg = outlineGO.AddComponent<Image>();
            olImg.sprite = GameSprites.Get(GameSprites.UI_TIMER_OUTLINE);
            olImg.raycastTarget = false;

            var lineGO = new GameObject("Line");
            lineGO.transform.SetParent(container.transform, false);
            var liRect = lineGO.AddComponent<RectTransform>();
            liRect.anchoredPosition = Vector2.zero;
            liRect.sizeDelta = new Vector2(191, 193);
            var liImg = lineGO.AddComponent<Image>();
            liImg.sprite = GameSprites.Get(GameSprites.UI_TIMER_LINE);
            liImg.raycastTarget = false;

            var handGO = new GameObject("ClockHand");
            handGO.transform.SetParent(container.transform, false);
            _clockHandRT = handGO.AddComponent<RectTransform>();
            _clockHandRT.anchoredPosition = Vector2.zero;
            _clockHandRT.sizeDelta = new Vector2(33, 114);
            _clockHandRT.pivot = new Vector2(0.5f, 0.15f);
            var handImg = handGO.AddComponent<Image>();
            handImg.sprite = GameSprites.Get(GameSprites.UI_TIMER_HAND);
            handImg.raycastTarget = false;

            _timerText = CreateText(container.transform, "TimerText",
                new Vector2(0, -10), new Vector2(120, 60), "", 36);
            _timerText.color = Color.white;
            _timerText.fontStyle = FontStyles.Bold;

            _timeAlarmObj = new GameObject("TimeAlarm");
            _timeAlarmObj.transform.SetParent(root, false);
            var alarmRect = _timeAlarmObj.AddComponent<RectTransform>();
            alarmRect.anchoredPosition = new Vector2(0, 386);
            alarmRect.sizeDelta = new Vector2(483, 198);
            alarmRect.anchorMin = new Vector2(0.5f, 0.5f);
            alarmRect.anchorMax = new Vector2(0.5f, 0.5f);
            alarmRect.pivot = new Vector2(0.5f, 0.5f);
            _timeAlarmImage = _timeAlarmObj.AddComponent<Image>();
            _timeAlarmImage.sprite = GameSprites.Get(GameSprites.ALARM);
            _timeAlarmImage.preserveAspect = true;
            _timeAlarmImage.raycastTarget = false;
            _timeAlarmObj.SetActive(false);
        }

        void BuildOppBarWorldUI()
        {
            var canvasGO = new GameObject("EnemyCanvas");
            _oppBarCanvas = canvasGO.AddComponent<Canvas>();
            _oppBarCanvas.renderMode = RenderMode.WorldSpace;

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 100);
            canvasGO.transform.localScale = Vector3.one * OPP_BAR_SCALE;

            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var hpBar = new GameObject("HPBar");
            hpBar.transform.SetParent(canvasGO.transform, false);
            var hpRect = hpBar.AddComponent<RectTransform>();
            hpRect.anchoredPosition = Vector2.zero;
            hpRect.sizeDelta = new Vector2(600, 100);

            _oppHpSlider = hpBar.AddComponent<Slider>();
            _oppHpSlider.minValue = 0f;
            _oppHpSlider.maxValue = 1f;
            _oppHpSlider.value = 1f;
            _oppHpSlider.interactable = false;
            _oppHpSlider.direction = Slider.Direction.LeftToRight;

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(hpBar.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.sprite = GameSprites.Get(GameSprites.UI_BAR_BG);
            bgImg.color = Color.white;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(hpBar.transform, false);
            var faRect = fillArea.AddComponent<RectTransform>();
            faRect.anchorMin = new Vector2(0f, 0.25f);
            faRect.anchorMax = new Vector2(1f, 0.75f);
            faRect.offsetMin = Vector2.zero;
            faRect.offsetMax = Vector2.zero;

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillArea.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = GameSprites.Get(GameSprites.UI_BAR_FILL);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.color = HP_COLOR_GREEN;
            _oppHpFillImage = fillImg;

            _oppHpSlider.fillRect = fillRect;

            var outlineGO = new GameObject("Outline");
            outlineGO.transform.SetParent(hpBar.transform, false);
            var olRect = outlineGO.AddComponent<RectTransform>();
            olRect.anchoredPosition = new Vector2(3.6f, 0f);
            olRect.sizeDelta = new Vector2(620, 80);
            var olImg = outlineGO.AddComponent<Image>();
            olImg.sprite = GameSprites.Get(GameSprites.UI_BAR_OUTLINE);
            olImg.raycastTarget = false;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(hpBar.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(-302f, -26f);
            iconRect.sizeDelta = new Vector2(100, 200);
            iconRect.localScale = new Vector3(-1f, 1f, 1f);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = GameSprites.Get(GameSprites.UI_THERMO_ICON);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            BuildOppDividerLines(hpBar.transform);

            _oppTempText = CreateText(canvasGO.transform, "OppTemp",
                new Vector2(0, -70), new Vector2(360, 32), "37°", 26);
        }

        void BuildOppDividerLines(Transform parent)
        {
            var root = new GameObject("DividerLines");
            root.transform.SetParent(parent, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            float[] xPositions = { 159.1f, 324.6f, 485.8f };
            for (int i = 0; i < 3; i++)
            {
                var lineGO = new GameObject($"line_{(i + 1) * 10}");
                lineGO.transform.SetParent(root.transform, false);
                var lineRect = lineGO.AddComponent<RectTransform>();
                lineRect.anchorMin = new Vector2(0f, 0.5f);
                lineRect.anchorMax = new Vector2(0f, 0.5f);
                lineRect.pivot = new Vector2(0.5f, 0.5f);
                lineRect.anchoredPosition = new Vector2(xPositions[i], 0f);
                lineRect.sizeDelta = new Vector2(7.45f, 70f);
                var lineImg = lineGO.AddComponent<Image>();
                lineImg.sprite = GameSprites.Get(GameSprites.UI_GIFT_LINE);
                lineImg.raycastTarget = false;
            }
        }

        void BuildReadyWorldUI()
        {
            var canvasGO = new GameObject("ReadyWorldCanvas");
            _readyCanvas = canvasGO.AddComponent<Canvas>();
            _readyCanvas.renderMode = RenderMode.WorldSpace;

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 180);
            canvasGO.transform.localScale = Vector3.one * WORLD_CANVAS_SCALE;
            canvasGO.transform.position = READY_BTN_POS;
            canvasGO.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

            canvasGO.AddComponent<GraphicRaycaster>();

            var readySprite = GameSprites.Get(GameSprites.BTN_DEFAULT);
            var btnGO = new GameObject("ReadyBtn");
            btnGO.transform.SetParent(canvasGO.transform, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchoredPosition = Vector2.zero;
            btnRect.sizeDelta = new Vector2(560, 320);

            var btnImg = btnGO.AddComponent<Image>();
            if (readySprite != null)
            {
                btnImg.sprite = readySprite;
                btnImg.preserveAspect = true;
            }
            else
            {
                btnImg.color = new Color(0.5f, 0.5f, 0.2f);
            }

            _readyButtonImage = btnImg;
            _readyButton = btnGO.AddComponent<Button>();
            _readyButton.targetGraphic = btnImg;
            _readyButton.onClick.AddListener(OnReadyClicked);
            _readyCanvas.gameObject.SetActive(false);
        }

        void BuildItemStack(Transform root)
        {
            var container = new GameObject("ItemStack");
            container.transform.SetParent(root, false);
            var cRect = container.AddComponent<RectTransform>();
            cRect.anchoredPosition = new Vector2(-20, -150);
            cRect.sizeDelta = new Vector2(200, 200);
            AnchorTopRight(cRect);

            var bg = CreatePanel(container.transform, "StackBg",
                Vector2.zero, new Vector2(200, 200),
                new Color(0.05f, 0.05f, 0.1f, 0.7f));

            var title = CreateText(container.transform, "StackTitle",
                new Vector2(0, 85), new Vector2(180, 24), "ACTION", 16);
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.8f, 0.8f, 0.6f);

            _stackText = CreateText(container.transform, "StackContent",
                new Vector2(0, 10), new Vector2(180, 140), "", 16);
            _stackText.alignment = TextAlignmentOptions.TopLeft;
            _stackText.color = new Color(0.9f, 0.9f, 0.9f);
        }

        void UpdateStackDisplay()
        {
            if (_stackText == null) return;

            string text = "";
            if (_selectedMainName != null)
                text += $"Main: {_selectedMainName}\n";
            if (_selectedSubName != null)
                text += $"Sub: {_selectedSubName}\n";
            if (text == "")
                text = "—";

            _stackText.text = text;
        }

        void BuildResultPanel(Transform root)
        {
            _resultPanel = new GameObject("ResultPanel");
            _resultPanel.transform.SetParent(root, false);
            var rect = _resultPanel.AddComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(500, 280);

            CreatePanel(_resultPanel.transform, "ResultBg",
                Vector2.zero, new Vector2(500, 280),
                new Color(0.05f, 0.05f, 0.1f, 0.9f));

            var title = CreateText(_resultPanel.transform, "ResultTitle",
                new Vector2(0, 115), new Vector2(460, 36), "턴 결과", 26);
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(1f, 0.9f, 0.5f);

            _resultText = CreateText(_resultPanel.transform, "ResultBody",
                new Vector2(0, -15), new Vector2(460, 200), "", 18);
            _resultText.alignment = TextAlignmentOptions.TopLeft;
            _resultText.color = new Color(0.95f, 0.95f, 0.95f);

            _resultPanel.SetActive(false);
        }

        void BuildEnvironmentPanel(Transform root)
        {
            _envPanel = new GameObject("EnvPanel");
            _envPanel.transform.SetParent(root, false);
            var rect = _envPanel.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0, -140);
            rect.sizeDelta = new Vector2(600, 80);
            AnchorTopCenter(rect);

            CreatePanel(_envPanel.transform, "EnvBg",
                Vector2.zero, new Vector2(600, 80),
                new Color(0.1f, 0.05f, 0.2f, 0.9f));

            _envText = CreateText(_envPanel.transform, "EnvText",
                Vector2.zero, new Vector2(560, 60), "", 32);
            _envText.fontStyle = FontStyles.Bold;
            _envText.color = new Color(1f, 0.85f, 0.3f);

            _envPanel.SetActive(false);
        }

        void OnEnvironmentAnnounced(EnvironmentType env)
        {
            StopSummerVacShake();

            if (_envPanel == null || _envText == null) return;

            string name = env switch
            {
                EnvironmentType.SunnyDay => "햇살이 더 쨍쨍해집니다.",
                EnvironmentType.CoolBreeze => "시원한 바람이 불어옵니다.",
                EnvironmentType.CicadaSong => "매미 소리가 들려옵니다.",
                EnvironmentType.Kids => "근처에 어린 친구들이 서성거립니다.",
                EnvironmentType.Ambulance => "근처에 응급구조원이 대기중입니다.",
                EnvironmentType.SummerVacation => "여름방학이 얼마 남지 않았습니다.",
                EnvironmentType.HeatWaveWarning => "폭염경보가 발생했습니다.",
                _ => ""
            };

            _envText.text = name;
            _envPanel.SetActive(true);
            StartCoroutine(HideEnvPanelAfterDelay());

            GameAudioManager.Instance?.PlayEnvironment(env);

            if (env == EnvironmentType.SummerVacation)
            {
                if (_timerFillImage != null)
                    _timerFillImage.color = new Color(1f, 0.35f, 0.25f);
                _summerVacShaking = true;
                _summerVacShakeCoroutine = StartCoroutine(SummerVacShakeRoutine());
            }
        }

        void StopSummerVacShake()
        {
            if (_summerVacShaking)
            {
                _summerVacShaking = false;
                if (_summerVacShakeCoroutine != null) StopCoroutine(_summerVacShakeCoroutine);
                _summerVacShakeCoroutine = null;
                if (_timerContainerRT != null)
                    _timerContainerRT.anchoredPosition = _timerContainerBasePos;
                if (_timerFillImage != null)
                    _timerFillImage.color = Color.white;
            }
        }

        IEnumerator SummerVacShakeRoutine()
        {
            const float amplitude = 3f;
            const float frequency = 6f;
            while (_summerVacShaking && _timerContainerRT != null)
            {
                float t = Time.time * frequency * Mathf.PI * 2f;
                float offsetX = Mathf.Sin(t) * amplitude;
                float offsetY = Mathf.Cos(t * 1.3f) * amplitude;
                _timerContainerRT.anchoredPosition = _timerContainerBasePos + new Vector2(offsetX, offsetY);
                yield return _waitSummerShake;
            }
        }

        IEnumerator HideEnvPanelAfterDelay()
        {
            yield return _waitEnvHide;
            if (_envPanel != null)
                _envPanel.SetActive(false);
        }

        void OnCombatResultReceived(CombatResultData data)
        {
            if (_resultPanel == null || _resultText == null) return;

            string GetItemName(short itemId)
            {
                if (itemId < 0) return "—";
                var item = ItemManager.Instance?.GetItemData(itemId);
                return item != null ? item.ItemName : "—";
            }

            string p1Main = GetItemName(data.P1MainItemId);
            string p2Main = GetItemName(data.P2MainItemId);
            string p1Sub = GetItemName(data.P1SubItemId);
            string p2Sub = GetItemName(data.P2SubItemId);

            float p1TickDelta = data.P1TempBeforeCombat - data.P1TempAtTurnStart;
            float p2TickDelta = data.P2TempBeforeCombat - data.P2TempAtTurnStart;
            float p1ItemDelta = data.P1TempAfterCombat - data.P1TempBeforeCombat;
            float p2ItemDelta = data.P2TempAfterCombat - data.P2TempBeforeCombat;

            string FormatDelta(float d) => d >= 0 ? $"+{d:F1}°" : $"{d:F1}°";

            string text = $"P1:\n" +
                          $"  Main: {p1Main}  |  Sub: {p1Sub}\n" +
                          $"  Tick: {FormatDelta(p1TickDelta)}  |  Item: {FormatDelta(p1ItemDelta)}\n\n" +
                          $"P2:\n" +
                          $"  Main: {p2Main}  |  Sub: {p2Sub}\n" +
                          $"  Tick: {FormatDelta(p2TickDelta)}  |  Item: {FormatDelta(p2ItemDelta)}";

            _resultText.text = text;
            _resultPanel.SetActive(true);

            if (_resultHideCoroutine != null)
                StopCoroutine(_resultHideCoroutine);
            _resultHideCoroutine = StartCoroutine(HideResultAfterDelay());
        }

        static readonly WaitForSeconds _waitResultHide = new(4f);

        IEnumerator HideResultAfterDelay()
        {
            yield return _waitResultHide;
            if (_resultPanel != null)
                _resultPanel.SetActive(false);
        }

        void OnOpponentRevealed(byte forPlayerIndex, short opponentItemId)
        {
            if (_localPlayer == null) return;
            if (_localPlayer.SyncedPlayerIndex.Value != forPlayerIndex) return;

            string itemName = opponentItemId >= 0
                ? (ItemManager.Instance?.GetItemData(opponentItemId)?.ItemName ?? "???")
                : "Not Selected";

            if (_statusText != null)
                _statusText.text = $"Revealed: {itemName}";

            Debug.Log($"[UI] Tarot reveal: opponent selected '{itemName}' (id={opponentItemId})");
        }

        static void AnchorTopCenter(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
        }

        static void AnchorTopLeft(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
        }

        static void AnchorTopRight(RectTransform rt)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
        }

        static void AnchorBottomLeft(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
        }

        static void AnchorBottomCenter(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
        }

        static TextMeshProUGUI CreateText(Transform parent, string name,
            Vector2 pos, Vector2 size, string text, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        static Image CreatePanel(Transform parent, string name,
            Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static Button CreateButton(Transform parent, string name,
            Vector2 pos, Vector2 size, string label, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor = bgColor * 0.7f;
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
            btn.colors = colors;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
        }

        #endregion
    }
}
