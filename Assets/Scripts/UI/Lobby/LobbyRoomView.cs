using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using AbsoluteZero.UI.Common;
using UnityEngine.UI;

using LobbyModel = Unity.Services.Lobbies.Models.Lobby;
using LobbyPlayer = Unity.Services.Lobbies.Models.Player;

namespace AbsoluteZero.UI.LobbyUI
{
    public class LobbyRoomView
    {
        readonly GameObject _root;

        Button _createBtn;
        TMP_InputField _joinCodeInput;
        Button _joinBtn;
        TextMeshProUGUI _lobbyCodeText;
        Transform _playerListContainer;
        Button _startBtn;
        Button _leaveBtn;
        TextMeshProUGUI _statusText;
        TextMeshProUGUI _logText;
        TextMeshProUGUI _modeLabel;

        readonly List<GameObject> _slotObjects = new();
        int _seatCount = 2;
        bool _inLobby;

        public event Action OnCreateClicked;
        public event Action<string> OnJoinClicked;
        public event Action OnStartClicked;
        public event Action OnLeaveClicked;
        public event Action OnBackClicked;

        public GameObject Root => _root;

        public LobbyRoomView(GameObject root)
        {
            _root = root;
            Bind();
        }

        void Bind()
        {
            var panel = _root.transform.Find("PanelBG");
            if (panel == null) return;
            _modeLabel = panel.Find("ModeLabel")?.GetComponent<TextMeshProUGUI>();

            var dimBtn = _root.transform.Find("Dim")?.GetComponent<Button>();
            if (dimBtn != null)
                dimBtn.onClick.AddListener(() => OnBackClicked?.Invoke());

            _lobbyCodeText = panel.Find("LobbyCode/CodeText")?.GetComponent<TextMeshProUGUI>();
            var codeBtn = panel.Find("LobbyCode")?.GetComponent<Button>();
            if (codeBtn != null)
                codeBtn.onClick.AddListener(CopyLobbyCode);

            _joinCodeInput = panel.Find("JoinInput")?.GetComponent<TMP_InputField>();

            _createBtn = panel.Find("CreateBtn")?.GetComponent<Button>();
            if (_createBtn != null)
                _createBtn.onClick.AddListener(() => OnCreateClicked?.Invoke());

            _joinBtn = panel.Find("JoinBtn")?.GetComponent<Button>();
            if (_joinBtn != null)
                _joinBtn.onClick.AddListener(() =>
                {
                    string code = _joinCodeInput != null ? _joinCodeInput.text.ToUpper().Trim() : "";
                    OnJoinClicked?.Invoke(code);
                });

            _playerListContainer = panel.Find("PlayerList");

            var logScroll = panel.Find("LogScroll");
            if (logScroll != null)
                _logText = logScroll.Find("LogText")?.GetComponent<TextMeshProUGUI>();
            if (_logText != null) _logText.overflowMode = TextOverflowModes.Truncate;

            _statusText = panel.Find("StatusText")?.GetComponent<TextMeshProUGUI>();

            _startBtn = panel.Find("StartBtn")?.GetComponent<Button>();
            if (_startBtn != null)
                _startBtn.onClick.AddListener(() => OnStartClicked?.Invoke());

            _leaveBtn = panel.Find("LeaveBtn")?.GetComponent<Button>();
            if (_leaveBtn != null)
                _leaveBtn.onClick.AddListener(() => OnLeaveClicked?.Invoke());

            var backBtn = panel.Find("BackLink")?.GetComponent<Button>();
            if (backBtn != null)
                backBtn.onClick.AddListener(() => OnBackClicked?.Invoke());
        }

        #region Public Render Methods

        public void SetVisible(bool visible) => _root.SetActive(visible);
        public void SetPlayerCount(int count)
        {
            _seatCount = count;
            if (_modeLabel != null) _modeLabel.text = count == 2 ? "1 대 1 대결" : $"{count}인 대결";
            CasualLobbySkin.RoomLayout(_root.transform, _seatCount, _inLobby);
        }

        public void SetLobbyCode(string code)
        {
            if (_lobbyCodeText != null) _lobbyCodeText.text = code;
        }

        public void SetStatus(string msg)
        {
            if (_statusText != null) _statusText.text = msg;
            AppendLog(msg);
        }

        public void SetStartVisible(bool visible)
        {
            if (_startBtn != null) _startBtn.gameObject.SetActive(visible);
        }

        public void SetStartInteractable(bool interactable)
        {
            if (_startBtn != null) _startBtn.interactable = interactable;
        }

        public void SetCreateInteractable(bool interactable)
        {
            if (_createBtn != null) _createBtn.interactable = interactable;
        }

        public void SetJoinInteractable(bool interactable)
        {
            if (_joinBtn != null) _joinBtn.interactable = interactable;
            if (_joinCodeInput != null) _joinCodeInput.interactable = interactable;
        }

        public void ResetJoinInput()
        {
            if (_joinCodeInput != null) _joinCodeInput.text = "";
        }

        public void ApplyMembership(bool inLobby)
        {
            _inLobby = inLobby;
            CasualLobbySkin.RoomLayout(_root.transform, _seatCount, _inLobby);
            if (_createBtn != null) _createBtn.gameObject.SetActive(!inLobby);
            if (_joinCodeInput != null) _joinCodeInput.gameObject.SetActive(!inLobby);
            if (_joinBtn != null) _joinBtn.gameObject.SetActive(!inLobby);

            if (!inLobby)
            {
                ClearPlayerList();
                if (_lobbyCodeText != null) _lobbyCodeText.text = "------";
            }
        }

        public void RenderPlayerList(LobbyModel lobby, string myPlayerId)
        {
            ClearPlayerList();
            SetPlayerCount(lobby.MaxPlayers);
            // Fit every participant into the authored list rectangle so the fourth
            // nickname does not extend into the connection log below it.
            float rowHeight = 55f;
            var listRect = _playerListContainer as RectTransform;
            var layout = _playerListContainer.GetComponent<VerticalLayoutGroup>();
            int rows = Mathf.Max(lobby.MaxPlayers, lobby.Players.Count);
            if (listRect != null && layout != null && rows > 0)
                rowHeight = Mathf.Min(rowHeight, Mathf.Max(1f,
                    (listRect.rect.height - layout.padding.vertical - layout.spacing * (rows - 1)) / rows));

            foreach (var player in lobby.Players)
            {
                string playerName = GetPlayerName(player);
                bool isMe = player.Id == myPlayerId;
                bool isHost = player.Id == lobby.HostId;

                string label = "";
                label += playerName;
                if (isHost) label += " [호스트]";
                if (isMe) label += " (나)";

                Color bgColor;
                if (isHost && isMe)
                    bgColor = new Color(0.83f, 0.65f, 0.29f, 0.80f);
                else if (isMe)
                    bgColor = new Color(0.29f, 0.55f, 0.36f, 0.80f);
                else if (isHost)
                    bgColor = new Color(0.83f, 0.65f, 0.29f, 0.80f);
                else
                    bgColor = new Color(0.35f, 0.48f, 0.61f, 0.80f);

                var slotGO = UIHelper.CreatePlayerSlot(_playerListContainer, label, bgColor);
                CasualUiSkin.Surface(slotGO.GetComponent<Image>(), isHost ? CasualUiSkin.Butter : isMe ? CasualUiSkin.Mint : CasualUiSkin.Sky, CasualUiSkin.Card, 6);
                CasualUiSkin.Labels(slotGO.transform);
                slotGO.GetComponent<LayoutElement>().preferredHeight = rowHeight;
                slotGO.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeight);
                _slotObjects.Add(slotGO);
            }

            int emptySlots = lobby.MaxPlayers - lobby.Players.Count;
            for (int i = 0; i < emptySlots; i++)
            {
                var slotGO = UIHelper.CreatePlayerSlot(_playerListContainer, "대기 중...",
                    new Color(0.91f, 0.88f, 0.83f, 0.50f));
                CasualUiSkin.Surface(slotGO.GetComponent<Image>(), CasualUiSkin.Cream, CasualUiSkin.Card, 6);
                CasualUiSkin.Labels(slotGO.transform);
                slotGO.GetComponent<LayoutElement>().preferredHeight = rowHeight;
                slotGO.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rowHeight);
                _slotObjects.Add(slotGO);
            }
        }

        public void ResetAll()
        {
            SetCreateInteractable(true);
            SetJoinInteractable(true);
            ResetJoinInput();
            ClearPlayerList();
            ApplyMembership(false);
        }

        #endregion

        #region Private Helpers

        void ClearPlayerList()
        {
            foreach (var go in _slotObjects)
            {
                if (go == null) continue;
                go.SetActive(false);
#if UNITY_EDITOR
                if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(go); continue; }
#endif
                UnityEngine.Object.Destroy(go);
            }
            _slotObjects.Clear();
        }

        static string GetPlayerName(LobbyPlayer player)
        {
            if (player.Data != null && player.Data.TryGetValue("PlayerName", out var nameData))
                return nameData.Value;
            return $"Player_{player.Id[..6]}";
        }

        void CopyLobbyCode()
        {
            string code = _lobbyCodeText != null ? _lobbyCodeText.text : "";
            if (string.IsNullOrEmpty(code) || code == "------")
            {
                SetStatus("복사할 코드 없음");
                return;
            }
            GUIUtility.systemCopyBuffer = code;
            SetStatus($"코드 복사됨: {code}");
        }

        void AppendLog(string msg)
        {
            if (_logText == null) return;
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logText.text += $"[{timestamp}] {msg}\n";

            string[] lines = _logText.text.Split('\n');
            if (lines.Length > 30)
            {
                _logText.text = string.Join("\n",
                    new ArraySegment<string>(lines, lines.Length - 30, 30));
            }
        }

        #endregion
    }
}
