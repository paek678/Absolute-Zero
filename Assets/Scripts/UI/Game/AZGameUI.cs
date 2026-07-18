using System.Collections;
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
        Image _myBarFill;
        Image _clockImage;

        Canvas _oppBarCanvas;
        TextMeshProUGUI _oppTempText;
        Image _oppBarFill;

        Canvas _readyCanvas;
        Button _readyButton;

        GameObject _gameOverPanel;
        TextMeshProUGUI _gameOverText;

        ItemWorldDisplay _worldDisplay;
        GameObject[] _oppItemObjects;
        bool _oppItemsSpawned;

        TextMeshProUGUI _stackText;
        string _selectedMainName;
        string _selectedSubName;

        GameObject _resultPanel;
        TextMeshProUGUI _resultText;
        Coroutine _resultHideCoroutine;

        GameObject _envPanel;
        TextMeshProUGUI _envText;

        bool _oppBarAttached;

        bool _uiBuilt;

        static readonly WaitForSeconds _waitEnvHide = new(3.5f);

        static readonly Vector3 OPP_BAR_LOCAL_OFFSET = new(0f, 2.2f, 0f);
        static readonly Vector3 READY_BTN_POS = new(0f, 0.05f, 1.2f);
        const float WORLD_CANVAS_SCALE = 0.005f;
        const float OPP_BAR_SCALE = 0.012f;

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

            SpawnStayItemFans();
        }

        void SpawnStayItemFans()
        {
            var fanSprite = GameSprites.GetStayItemSprite();
            if (fanSprite == null) return;

            var litMat = Resources.Load<Material>("sprite3DMat");
            string[] markerNames = { "PlayerStayItem", "EnemyStayItem" };

            foreach (var name in markerNames)
            {
                var marker = GameObject.Find(name);
                if (marker == null) continue;

                var go = new GameObject($"{name}_Fan");
                go.transform.SetParent(marker.transform, false);
                go.transform.localPosition = Vector3.zero;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = fanSprite;
                sr.sortingOrder = 3;
                if (litMat != null) sr.material = litMat;
            }
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
            if (_worldDisplay != null)
                _worldDisplay.OnWorldItemClicked -= OnItemClicked;
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
                _readyCanvas.gameObject.SetActive(newPhase == TurnPhase.PrepPhase);

            if (newPhase == TurnPhase.PrepPhase)
            {
                _statusText.text = "Select an item!";
                _gameOverPanel.SetActive(false);
                if (_resultPanel != null) _resultPanel.SetActive(false);
                _selectedMainName = null;
                _selectedSubName = null;
                UpdateStackDisplay();
            }
            else if (newPhase == TurnPhase.AttackPhase)
            {
                _statusText.text = "Resolving...";
                _gameOverPanel.SetActive(false);
            }
            else if (newPhase == TurnPhase.RoundOver)
            {
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
                return;
            }

            _timerText.text = $"{_tm.RemainingTime.Value}";
        }

        void UpdateTempDisplay()
        {
            if (_localPlayer != null)
            {
                float myT = _localPlayer.Temperature.Value;
                _myTempText.text = $"{myT:F0}°";
                _myBarFill.fillAmount = Mathf.Clamp01(myT / 37f);
                _myBarFill.color = TempToColor(myT / 37f);
            }

            var opp = GetOpponentPlayer();
            if (opp != null)
            {
                float oppT = opp.Temperature.Value;
                _oppTempText.text = $"{oppT:F0}°";
                _oppBarFill.fillAmount = Mathf.Clamp01(oppT / 37f);
                _oppBarFill.color = TempToColor(oppT / 37f);
            }
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
                var enemyGO = GameObject.Find("EnemyPlayer");
                if (enemyGO != null)
                {
                    _oppBarCanvas.transform.SetParent(enemyGO.transform, false);
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

            if (_localPlayer.HasSelectedItem.Value && itemData.SlotType != ItemSlotType.Sub)
                return;

            _localPlayer.SelectItemServerRpc((byte)slotIndex);
            _worldDisplay?.NotifyItemConfirmed(slotIndex);

            if (itemData.SlotType == ItemSlotType.Sub)
                _selectedSubName = itemData.ItemName;
            else
                _selectedMainName = itemData.ItemName;

            UpdateStackDisplay();

            _statusText.text = itemData.IsInstantUse
                ? $"Used: {itemData.ItemName}"
                : $"Selected: {itemData.ItemName}";
        }

        void OnReadyClicked()
        {
            if (_localPlayer == null) return;
            if (MiniGameHub.IsRunning) return;

            _localPlayer.PressReadyServerRpc();
            _statusText.text = _localPlayer.HasSelectedItem.Value
                ? "Ready!"
                : "Ready! (No action)";
            if (_readyCanvas != null)
                _readyCanvas.gameObject.SetActive(false);
        }

        void OnBackToLobbyClicked()
        {
            var sessionManager = Core.Network.SessionManager.Instance;
            if (sessionManager != null)
                sessionManager.Disconnect();
        }

        static Color TempToColor(float normalized)
        {
            float h = Mathf.Lerp(0.6f, 0f, Mathf.Clamp01(normalized));
            return Color.HSVToRGB(h, 0.8f, 1f);
        }

        void TrySpawnOpponentItems()
        {
            var opp = GetOpponentPlayer();
            if (opp == null) return;

            var inv = opp.GetInventory();
            if (inv == null || inv.SlotStates == null || inv.SlotStates.Count == 0) return;
            if (ItemManager.Instance == null) return;

            ItemManager.Instance.InitializeClientRegistry(inv);

            int count = inv.SlotStates.Count;
            _oppItemObjects = new GameObject[count];
            var litMat = Resources.Load<Material>("sprite3DMat");

            for (int i = 0; i < count; i++)
            {
                var itemData = inv.GetItemData(i);
                if (itemData == null) continue;

                var marker = GameObject.Find($"EnemyItem{i + 1}");
                if (marker == null) continue;

                string itemName = itemData.ItemName;

                var go = new GameObject($"OppItem_{i}_{itemName}");
                go.transform.position = marker.transform.position;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GameSprites.GetItemSprite(itemName);
                if (litMat != null) sr.material = litMat;
                sr.sortingOrder = 5;

                _oppItemObjects[i] = go;
            }

            _oppItemsSpawned = true;
        }

        #region UI Construction

        void BuildOverlayUI()
        {
            var canvasGO = new GameObject("OverlayCanvas");
            _overlayCanvas = canvasGO.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

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
            cRect.anchoredPosition = new Vector2(20, -20);
            cRect.sizeDelta = new Vector2(260, 60);
            AnchorTopLeft(cRect);

            var bg = new GameObject("BarBg");
            bg.transform.SetParent(container.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(240, 24);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            var fillGO = new GameObject("BarFill");
            fillGO.transform.SetParent(bg.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);
            _myBarFill = fillGO.AddComponent<Image>();
            _myBarFill.type = Image.Type.Filled;
            _myBarFill.fillMethod = Image.FillMethod.Horizontal;
            _myBarFill.fillAmount = 1f;
            _myBarFill.color = new Color(0.9f, 0.2f, 0.15f);

            _myTempText = CreateText(container.transform, "MyTemp",
                new Vector2(0, -22), new Vector2(240, 24), "37°", 20);
            _myTempText.alignment = TextAlignmentOptions.Left;
        }

        void BuildClockTimer(Transform root)
        {
            var container = new GameObject("ClockTimer");
            container.transform.SetParent(root, false);
            var cRect = container.AddComponent<RectTransform>();
            cRect.anchoredPosition = new Vector2(-20, -20);
            cRect.sizeDelta = new Vector2(120, 120);
            AnchorTopRight(cRect);

            var clockSprite = GameSprites.Get(GameSprites.CLOCK);
            if (clockSprite != null)
            {
                var clockGO = new GameObject("ClockIcon");
                clockGO.transform.SetParent(container.transform, false);
                var clRect = clockGO.AddComponent<RectTransform>();
                clRect.anchoredPosition = Vector2.zero;
                clRect.sizeDelta = new Vector2(100, 100);
                _clockImage = clockGO.AddComponent<Image>();
                _clockImage.sprite = clockSprite;
                _clockImage.preserveAspect = true;
            }

            _timerText = CreateText(container.transform, "TimerText",
                new Vector2(0, -5), new Vector2(100, 60), "", 40);
            _timerText.color = Color.white;
            _timerText.fontStyle = FontStyles.Bold;
        }

        void BuildOppBarWorldUI()
        {
            var canvasGO = new GameObject("OppBarWorldCanvas");
            _oppBarCanvas = canvasGO.AddComponent<Canvas>();
            _oppBarCanvas.renderMode = RenderMode.WorldSpace;

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 100);
            canvasGO.transform.localScale = Vector3.one * OPP_BAR_SCALE;

            var bg = new GameObject("OppBarBg");
            bg.transform.SetParent(canvasGO.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(360, 36);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            var fillGO = new GameObject("OppBarFill");
            fillGO.transform.SetParent(bg.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);
            _oppBarFill = fillGO.AddComponent<Image>();
            _oppBarFill.type = Image.Type.Filled;
            _oppBarFill.fillMethod = Image.FillMethod.Horizontal;
            _oppBarFill.fillAmount = 1f;
            _oppBarFill.color = new Color(0.9f, 0.2f, 0.15f);

            _oppTempText = CreateText(canvasGO.transform, "OppTemp",
                new Vector2(0, -45), new Vector2(360, 32), "37°", 26);
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

            var readySprite = GameSprites.Get(GameSprites.READY_TEXT);
            var btnGO = new GameObject("ReadyBtn");
            btnGO.transform.SetParent(canvasGO.transform, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchoredPosition = Vector2.zero;
            btnRect.sizeDelta = new Vector2(280, 160);

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
            if (_envPanel == null || _envText == null) return;

            string name = env switch
            {
                EnvironmentType.SunnyDay => "[ 햇살쨍쨍 ]",
                EnvironmentType.CoolBreeze => "[ 바람선선 ]",
                EnvironmentType.CicadaSong => "[ 매미울음 ]",
                EnvironmentType.Kids => "[ 잼민이들 ]",
                EnvironmentType.Ambulance => "[ 앰뷸런스 ]",
                EnvironmentType.SummerVacation => "[ 여름방학 ]",
                EnvironmentType.HeatWaveWarning => "[ 폭염경보 ]",
                _ => ""
            };

            _envText.text = name;
            _envPanel.SetActive(true);
            StartCoroutine(HideEnvPanelAfterDelay());
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
            _resultHideCoroutine = StartCoroutine(HideResultAfterDelay(4f));
        }

        IEnumerator HideResultAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
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

        static Image CreateBarFill(Transform parent, string name, Color fillColor)
        {
            var fillGO = new GameObject(name);
            fillGO.transform.SetParent(parent, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            return fillImg;
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
