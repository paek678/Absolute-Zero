using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Network;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

using LobbyPlayer = Unity.Services.Lobbies.Models.Player;

namespace AbsoluteZero.UI.LobbyUI
{
    public class AZLobbyUI : MonoBehaviour
    {
        private LobbyManager lobbyManager;
        private RelayManager relayManager;
        private SessionManager sessionManager;

        private GameObject mainPanel;
        private GameObject lobbyPanel;

        private Button createBtn;
        private TMP_InputField joinCodeInput;
        private Button joinBtn;
        private TextMeshProUGUI statusText;

        private TextMeshProUGUI lobbyCodeText;
        private Transform playerListContainer;
        private Button startBtn;
        private Button leaveBtn;
        private TextMeshProUGUI lobbyStatusText;
        private TextMeshProUGUI logText;

        private bool isConnectingToRelay;
        private string myPlayerId;
        private readonly Dictionary<string, string> lastKnownActions = new();
        private readonly List<GameObject> playerSlotObjects = new();

        private void Start()
        {
            BuildUI();
            StartCoroutine(WaitForManagers());
        }

        private System.Collections.IEnumerator WaitForManagers()
        {
            while (LobbyManager.Instance == null)
                yield return null;

            lobbyManager = LobbyManager.Instance;
            relayManager = RelayManager.Instance;
            sessionManager = SessionManager.Instance;

            lobbyManager.OnLobbyCreated += OnLobbyEntered;
            lobbyManager.OnLobbyJoined += OnLobbyEntered;
            lobbyManager.OnLobbyUpdated += OnLobbyUpdated;
            lobbyManager.OnLobbyLeft += OnLobbyLeft;
            lobbyManager.OnError += OnError;

            if (relayManager != null)
            {
                relayManager.OnRelayCreated += OnRelayCreated;
                relayManager.OnRelayJoined += OnRelayJoined;
                relayManager.OnError += OnRelayError;
            }

            if (sessionManager != null)
                sessionManager.OnGameStarted += OnGameStarted;

            SetStatus("Ready. Create or join a lobby.");
        }

        private void OnDestroy()
        {
            if (lobbyManager != null)
            {
                lobbyManager.OnLobbyCreated -= OnLobbyEntered;
                lobbyManager.OnLobbyJoined -= OnLobbyEntered;
                lobbyManager.OnLobbyUpdated -= OnLobbyUpdated;
                lobbyManager.OnLobbyLeft -= OnLobbyLeft;
                lobbyManager.OnError -= OnError;
            }

            if (relayManager != null)
            {
                relayManager.OnRelayCreated -= OnRelayCreated;
                relayManager.OnRelayJoined -= OnRelayJoined;
                relayManager.OnError -= OnRelayError;
            }

            if (sessionManager != null)
                sessionManager.OnGameStarted -= OnGameStarted;
        }

        #region Panel Control

        private void ShowMainPanel()
        {
            mainPanel.SetActive(true);
            lobbyPanel.SetActive(false);
        }

        private void ShowLobbyPanel()
        {
            mainPanel.SetActive(false);
            lobbyPanel.SetActive(true);
        }

        #endregion

        #region Button Handlers

        private async void OnCreateClicked()
        {
            if (lobbyManager == null) return;

            createBtn.interactable = false;
            SetStatus("Creating lobby...");

            string lobbyName = $"AZ_{UnityEngine.Random.Range(1000, 9999)}";
            var lobby = await lobbyManager.CreateLobbyAsync(lobbyName);

            if (lobby == null)
            {
                createBtn.interactable = true;
                SetStatus("Failed to create lobby. Try again.");
            }
        }

        private async void OnJoinClicked()
        {
            if (lobbyManager == null) return;

            string code = joinCodeInput.text.ToUpper().Trim();
            if (string.IsNullOrEmpty(code))
            {
                SetStatus("Enter a lobby code first.");
                return;
            }

            joinBtn.interactable = false;
            joinCodeInput.interactable = false;
            SetStatus($"Joining lobby ({code})...");

            try
            {
                var lobby = await lobbyManager.JoinLobbyByCodeAsync(code);
                if (lobby == null)
                {
                    SetStatus("Invalid lobby code. Try again.");
                    joinCodeInput.text = "";
                    joinCodeInput.interactable = true;
                    joinBtn.interactable = true;
                }
            }
            catch (Exception e)
            {
                SetStatus($"Join failed: {e.Message}");
                joinCodeInput.text = "";
                joinCodeInput.interactable = true;
                joinBtn.interactable = true;
            }
        }

        private async void OnLeaveClicked()
        {
            if (lobbyManager == null) return;
            SetLobbyStatus("Leaving lobby...");
            await lobbyManager.LeaveLobbyAsync();
        }

        private async void OnStartClicked()
        {
            if (lobbyManager == null || !lobbyManager.IsHost) return;
            if (relayManager == null || sessionManager == null) return;
            if (isConnectingToRelay) return;

            isConnectingToRelay = true;
            startBtn.interactable = false;
            SetLobbyStatus("Starting Relay...");

            try
            {
                string relayJoinCode = await relayManager.StartHostWithRelayAsync(lobbyManager.MaxPlayers);

                if (string.IsNullOrEmpty(relayJoinCode))
                {
                    SetLobbyStatus("Relay start failed.");
                    isConnectingToRelay = false;
                    startBtn.interactable = true;
                    return;
                }

                await lobbyManager.SetRelayJoinCodeAsync(relayJoinCode);
                SetLobbyStatus($"Relay ready: {relayJoinCode}");

                await lobbyManager.SetGameStartedAsync(true);
                sessionManager.StartGame();
            }
            catch (Exception e)
            {
                SetLobbyStatus($"Start failed: {e.Message}");
                isConnectingToRelay = false;
                startBtn.interactable = true;
            }
        }

        #endregion

        #region Lobby Events

        private void OnLobbyEntered(Unity.Services.Lobbies.Models.Lobby lobby)
        {
            myPlayerId = lobbyManager.PlayerId;

            lobbyCodeText.text = lobby.LobbyCode;

            bool canStart = lobbyManager.IsHost && relayManager != null && sessionManager != null;
            startBtn.gameObject.SetActive(canStart);

            UpdatePlayerList(lobby);
            ShowLobbyPanel();

            string hostTag = lobbyManager.IsHost ? " (Host)" : "";
            SetLobbyStatus($"Joined lobby{hostTag}. Code: {lobby.LobbyCode}");
        }

        private void OnLobbyUpdated(Unity.Services.Lobbies.Models.Lobby lobby)
        {
            UpdatePlayerList(lobby);
            CheckForGameStart(lobby);
        }

        private async void CheckForGameStart(Unity.Services.Lobbies.Models.Lobby lobby)
        {
            if (lobbyManager.IsHost) return;
            if (isConnectingToRelay) return;
            if (relayManager == null) return;
            if (relayManager.IsRelayConnected) return;
            if (!lobbyManager.IsGameStarted()) return;

            string relayJoinCode = lobbyManager.GetRelayJoinCode();
            if (string.IsNullOrEmpty(relayJoinCode)) return;

            isConnectingToRelay = true;
            SetLobbyStatus($"Game starting... Connecting to Relay ({relayJoinCode})");

            try
            {
                bool success = await relayManager.JoinRelayAsync(relayJoinCode);
                if (!success)
                {
                    SetLobbyStatus("Relay connection failed.");
                    isConnectingToRelay = false;
                }
            }
            catch (Exception e)
            {
                SetLobbyStatus($"Relay error: {e.Message}");
                isConnectingToRelay = false;
            }
        }

        private void OnLobbyLeft()
        {
            isConnectingToRelay = false;
            createBtn.interactable = true;
            joinBtn.interactable = true;
            joinCodeInput.interactable = true;
            joinCodeInput.text = "";
            ClearPlayerList();
            ShowMainPanel();
            SetStatus("Left lobby.");
        }

        private void OnError(string error)
        {
            SetStatus($"Error: {error}");
            createBtn.interactable = true;
            joinBtn.interactable = true;
            joinCodeInput.interactable = true;
        }

        #endregion

        #region Relay Events

        private void OnRelayCreated(string joinCode)
        {
            SetLobbyStatus($"Relay created: {joinCode}");
        }

        private void OnRelayJoined()
        {
            SetLobbyStatus("Relay connected. Transitioning...");
            isConnectingToRelay = false;
        }

        private void OnGameStarted()
        {
            SetLobbyStatus("Game started!");
        }

        private void OnRelayError(string error)
        {
            SetLobbyStatus($"Relay error: {error}");
            isConnectingToRelay = false;
            startBtn.interactable = true;
        }

        #endregion

        #region Player List

        private void UpdatePlayerList(Unity.Services.Lobbies.Models.Lobby lobby)
        {
            ClearPlayerList();

            foreach (var player in lobby.Players)
            {
                string playerName = GetPlayerName(player);
                bool isMe = player.Id == myPlayerId;
                bool isHost = player.Id == lobby.HostId;

                string label = playerName;
                if (isHost) label += " [HOST]";
                if (isMe) label += " (You)";

                Color bgColor = isHost
                    ? new Color(0.6f, 0.5f, 0.2f, 0.8f)
                    : isMe
                        ? new Color(0.2f, 0.6f, 0.3f, 0.8f)
                        : new Color(0.2f, 0.4f, 0.6f, 0.8f);

                var slotGO = CreatePlayerSlot(playerListContainer, label, bgColor);
                playerSlotObjects.Add(slotGO);
            }

            int emptySlots = lobby.MaxPlayers - lobby.Players.Count;
            for (int i = 0; i < emptySlots; i++)
            {
                var slotGO = CreatePlayerSlot(playerListContainer, "Empty",
                    new Color(0.3f, 0.3f, 0.3f, 0.5f));
                playerSlotObjects.Add(slotGO);
            }
        }

        private void ClearPlayerList()
        {
            foreach (var go in playerSlotObjects)
                Destroy(go);
            playerSlotObjects.Clear();
        }

        private static string GetPlayerName(LobbyPlayer player)
        {
            if (player.Data != null && player.Data.TryGetValue("PlayerName", out var nameData))
                return nameData.Value;
            return $"Player_{player.Id[..6]}";
        }

        #endregion

        #region Status

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        private void SetLobbyStatus(string msg)
        {
            if (lobbyStatusText != null) lobbyStatusText.text = msg;
            AppendLog(msg);
        }

        void CopyLobbyCode()
        {
            string code = lobbyCodeText != null ? lobbyCodeText.text : "";
            if (string.IsNullOrEmpty(code) || code == "------")
            {
                SetLobbyStatus("No lobby code to copy yet.");
                return;
            }
            GUIUtility.systemCopyBuffer = code;
            SetLobbyStatus($"Copied lobby code: {code}");
        }

        private void AppendLog(string msg)
        {
            if (logText == null) return;
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            logText.text += $"[{timestamp}] {msg}\n";

            string[] lines = logText.text.Split('\n');
            if (lines.Length > 30)
            {
                logText.text = string.Join("\n",
                    new ArraySegment<string>(lines, lines.Length - 30, 30));
            }
        }

        #endregion

        #region UI Construction

        private void BuildUI()
        {
            var canvasGO = new GameObject("LobbyCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<InputSystemUIInputModule>();
            }

            Transform root = canvasGO.transform;

            BuildMainPanel(root);
            BuildLobbyPanel(root);

            ShowMainPanel();
        }

        private void BuildMainPanel(Transform root)
        {
            mainPanel = new GameObject("MainPanel");
            mainPanel.transform.SetParent(root, false);
            var rt = mainPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            CreatePanel(mainPanel.transform, "BG", Vector2.zero,
                new Vector2(500, 450), new Color(0.08f, 0.08f, 0.12f, 0.92f));

            CreateText(mainPanel.transform, "Title",
                new Vector2(0, 160), new Vector2(400, 60), "ABSOLUTE ZERO", 42);

            CreateText(mainPanel.transform, "Subtitle",
                new Vector2(0, 110), new Vector2(400, 30), "1v1 Temperature Deathmatch", 18,
                new Color(0.6f, 0.6f, 0.7f));

            createBtn = CreateButton(mainPanel.transform, "CreateBtn",
                new Vector2(0, 40), new Vector2(300, 55), "CREATE LOBBY",
                new Color(0.2f, 0.5f, 0.8f));
            createBtn.onClick.AddListener(OnCreateClicked);

            CreateText(mainPanel.transform, "OrText",
                new Vector2(0, -10), new Vector2(100, 25), "— or —", 16,
                new Color(0.5f, 0.5f, 0.5f));

            joinCodeInput = CreateInputField(mainPanel.transform, "JoinCodeInput",
                new Vector2(-55, -55), new Vector2(190, 50), "Enter code...");

            joinBtn = CreateButton(mainPanel.transform, "JoinBtn",
                new Vector2(115, -55), new Vector2(110, 50), "JOIN",
                new Color(0.3f, 0.6f, 0.3f));
            joinBtn.onClick.AddListener(OnJoinClicked);

            statusText = CreateText(mainPanel.transform, "Status",
                new Vector2(0, -130), new Vector2(400, 30), "Initializing...", 16,
                new Color(0.7f, 0.7f, 0.7f));
        }

        private void BuildLobbyPanel(Transform root)
        {
            lobbyPanel = new GameObject("LobbyPanel");
            lobbyPanel.transform.SetParent(root, false);
            var rt = lobbyPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            CreatePanel(lobbyPanel.transform, "BG", Vector2.zero,
                new Vector2(600, 650), new Color(0.08f, 0.08f, 0.12f, 0.92f));

            CreateText(lobbyPanel.transform, "LobbyTitle",
                new Vector2(0, 280), new Vector2(400, 40), "LOBBY", 32);

            CreateText(lobbyPanel.transform, "CodeLabel",
                new Vector2(0, 235), new Vector2(300, 25), "Lobby Code (click to copy):", 16,
                new Color(0.6f, 0.6f, 0.7f));

            lobbyCodeText = CreateText(lobbyPanel.transform, "LobbyCode",
                new Vector2(0, 200), new Vector2(300, 50), "------", 40);
            lobbyCodeText.color = new Color(1f, 0.9f, 0.4f);

            var codeBtn = lobbyCodeText.gameObject.AddComponent<Button>();
            codeBtn.targetGraphic = lobbyCodeText;
            codeBtn.transition = Selectable.Transition.None;
            codeBtn.onClick.AddListener(CopyLobbyCode);

            CreateText(lobbyPanel.transform, "PlayersLabel",
                new Vector2(0, 155), new Vector2(200, 25), "Players:", 18,
                new Color(0.6f, 0.6f, 0.7f));

            var listGO = new GameObject("PlayerList");
            listGO.transform.SetParent(lobbyPanel.transform, false);
            var listRT = listGO.AddComponent<RectTransform>();
            listRT.anchoredPosition = new Vector2(0, 85);
            listRT.sizeDelta = new Vector2(400, 120);
            var vlg = listGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.padding = new RectOffset(10, 10, 5, 5);
            playerListContainer = listGO.transform;

            var buttonArea = new GameObject("ButtonArea");
            buttonArea.transform.SetParent(lobbyPanel.transform, false);
            var baRT = buttonArea.AddComponent<RectTransform>();
            baRT.anchoredPosition = new Vector2(0, -40);
            baRT.sizeDelta = new Vector2(400, 55);

            startBtn = CreateButton(buttonArea.transform, "StartBtn",
                new Vector2(-80, 0), new Vector2(150, 50), "START GAME",
                new Color(0.8f, 0.4f, 0.1f));
            startBtn.onClick.AddListener(OnStartClicked);
            startBtn.gameObject.SetActive(false);

            leaveBtn = CreateButton(buttonArea.transform, "LeaveBtn",
                new Vector2(80, 0), new Vector2(150, 50), "LEAVE",
                new Color(0.6f, 0.2f, 0.2f));
            leaveBtn.onClick.AddListener(OnLeaveClicked);

            lobbyStatusText = CreateText(lobbyPanel.transform, "LobbyStatus",
                new Vector2(0, -95), new Vector2(500, 25), "", 16,
                new Color(0.7f, 0.7f, 0.7f));

            var logScrollGO = new GameObject("LogScroll");
            logScrollGO.transform.SetParent(lobbyPanel.transform, false);
            var logScrollRT = logScrollGO.AddComponent<RectTransform>();
            logScrollRT.anchoredPosition = new Vector2(0, -200);
            logScrollRT.sizeDelta = new Vector2(500, 150);

            var logBG = logScrollGO.AddComponent<Image>();
            logBG.color = new Color(0.05f, 0.05f, 0.08f, 0.8f);

            var scrollRect = logScrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(logScrollGO.transform, false);
            var vpRT = viewport.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = new Vector2(5, 5);
            vpRT.offsetMax = new Vector2(-5, -5);
            var vpMask = viewport.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = Color.white;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var cRT = content.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0, 1);
            cRT.anchorMax = new Vector2(1, 1);
            cRT.pivot = new Vector2(0.5f, 1);
            cRT.offsetMin = new Vector2(0, 0);
            cRT.offsetMax = new Vector2(0, 0);

            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            logText = content.AddComponent<TextMeshProUGUI>();
            logText.fontSize = 13;
            logText.color = new Color(0.7f, 0.8f, 0.7f);
            logText.alignment = TextAlignmentOptions.TopLeft;
            logText.text = "";

            scrollRect.viewport = vpRT;
            scrollRect.content = cRT;
        }

        #endregion

        #region UI Helpers

        private static TextMeshProUGUI CreateText(Transform parent, string name,
            Vector2 pos, Vector2 size, string text, int fontSize,
            Color? color = null)
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
            tmp.color = color ?? Color.white;
            return tmp;
        }

        private static Image CreatePanel(Transform parent, string name,
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

        private static Button CreateButton(Transform parent, string name,
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
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name,
            Vector2 pos, Vector2 size, string placeholder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(go.transform, false);
            var taRT = textArea.AddComponent<RectTransform>();
            taRT.anchorMin = Vector2.zero;
            taRT.anchorMax = Vector2.one;
            taRT.offsetMin = new Vector2(10, 5);
            taRT.offsetMax = new Vector2(-10, -5);
            textArea.AddComponent<RectMask2D>();

            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(textArea.transform, false);
            var phRT = placeholderGO.AddComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.offsetMin = Vector2.zero;
            phRT.offsetMax = Vector2.zero;
            var phText = placeholderGO.AddComponent<TextMeshProUGUI>();
            phText.text = placeholder;
            phText.fontSize = 18;
            phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            phText.alignment = TextAlignmentOptions.MidlineLeft;

            var inputTextGO = new GameObject("Text");
            inputTextGO.transform.SetParent(textArea.transform, false);
            var itRT = inputTextGO.AddComponent<RectTransform>();
            itRT.anchorMin = Vector2.zero;
            itRT.anchorMax = Vector2.one;
            itRT.offsetMin = Vector2.zero;
            itRT.offsetMax = Vector2.zero;
            var inputTMP = inputTextGO.AddComponent<TextMeshProUGUI>();
            inputTMP.fontSize = 20;
            inputTMP.color = Color.white;
            inputTMP.alignment = TextAlignmentOptions.MidlineLeft;

            var inputField = go.AddComponent<TMP_InputField>();
            inputField.textViewport = taRT;
            inputField.textComponent = inputTMP;
            inputField.placeholder = phText;
            inputField.characterLimit = 8;
            inputField.contentType = TMP_InputField.ContentType.Alphanumeric;
            inputField.pointSize = 20;

            return inputField;
        }

        private static GameObject CreatePlayerSlot(Transform parent, string label, Color bgColor)
        {
            var go = new GameObject("Slot");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(380, 45);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 45;

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(go.transform, false);
            var trt = textGO.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(15, 0);
            trt.offsetMax = new Vector2(-15, 0);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;

            return go;
        }

        #endregion
    }
}
