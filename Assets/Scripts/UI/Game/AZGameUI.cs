using AbsoluteZero.Core.Game;
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
        private AbsoluteZeroTurnManager tm;

        private Canvas canvas;
        private TextMeshProUGUI phaseText;
        private TextMeshProUGUI timerText;
        private TextMeshProUGUI roundText;
        private TextMeshProUGUI p1NameText;
        private TextMeshProUGUI p1TempText;
        private Image p1BarFill;
        private TextMeshProUGUI p2NameText;
        private TextMeshProUGUI p2TempText;
        private Image p2BarFill;
        private Button attackBtn;
        private Button defendBtn;
        private Button chargeBtn;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI resultText;
        private GameObject actionPanel;
        private GameObject gameOverPanel;

        private ActionType submittedAction;
        private bool uiBuilt;

        private void Start()
        {
            BuildUI();
            uiBuilt = true;
        }

        private void Update()
        {
            if (!uiBuilt) return;

            if (tm == null)
            {
                tm = AbsoluteZeroTurnManager.Instance;
                if (tm == null) return;
                SubscribeToEvents();
            }

            UpdateDisplay();
        }

        private void OnDestroy()
        {
            if (tm != null)
            {
                tm.CurrentPhase.OnValueChanged -= OnPhaseChanged;
                tm.OnResultsReceived -= OnResultsReceived;
            }
        }

        private void SubscribeToEvents()
        {
            tm.CurrentPhase.OnValueChanged += OnPhaseChanged;
            tm.OnResultsReceived += OnResultsReceived;
            OnPhaseChanged(TurnPhase.WaitingForPlayers, tm.CurrentPhase.Value);
        }

        #region Event Handlers

        private void OnPhaseChanged(TurnPhase oldPhase, TurnPhase newPhase)
        {
            phaseText.text = GetPhaseName(newPhase);

            switch (newPhase)
            {
                case TurnPhase.WaitingForPlayers:
                    statusText.text = "Waiting for opponent...";
                    SetActionPanelVisible(false);
                    resultText.text = "";
                    break;

                case TurnPhase.PrepTurn:
                    submittedAction = ActionType.None;
                    SetActionPanelVisible(true);
                    SetGameOverPanelVisible(false);
                    SetButtonsInteractable(true);
                    statusText.text = "Choose your action!";
                    resultText.text = "";
                    break;

                case TurnPhase.AttackTurn:
                    SetActionPanelVisible(false);
                    statusText.text = "Resolving...";
                    break;

                case TurnPhase.GameOver:
                    SetActionPanelVisible(false);
                    int winner = tm.WinnerIndex.Value;
                    int myIndex = tm.GetLocalPlayerIndex();
                    if (winner < 0)
                        statusText.text = "DRAW!";
                    else if (winner == myIndex)
                        statusText.text = "YOU WIN!";
                    else
                        statusText.text = "YOU LOSE...";
                    SetGameOverPanelVisible(true);
                    break;
            }
        }

        private void OnResultsReceived(ActionType p1Action, ActionType p2Action,
            float p1Delta, float p2Delta)
        {
            string p1Act = tm.GetActionName(p1Action);
            string p2Act = tm.GetActionName(p2Action);
            string p1Sign = p1Delta >= 0 ? $"+{p1Delta:F0}" : $"{p1Delta:F0}";
            string p2Sign = p2Delta >= 0 ? $"+{p2Delta:F0}" : $"{p2Delta:F0}";

            resultText.text = $"P1: {p1Act} ({p1Sign}°)    P2: {p2Act} ({p2Sign}°)";
        }

        #endregion

        #region Button Handlers

        private void OnAttackClicked()
        {
            SubmitAction(ActionType.Attack);
        }

        private void OnDefendClicked()
        {
            SubmitAction(ActionType.Defend);
        }

        private void OnChargeClicked()
        {
            SubmitAction(ActionType.Charge);
        }

        private void SubmitAction(ActionType action)
        {
            if (submittedAction != ActionType.None || tm == null) return;

            submittedAction = action;
            tm.SubmitActionRpc((byte)action);
            SetButtonsInteractable(false);
            statusText.text = $"Submitted: {tm.GetActionName(action)}";
        }

        private void OnPlayAgainClicked()
        {
            if (tm == null) return;
            tm.RequestRestartRpc();
        }

        private void OnBackToLobbyClicked()
        {
            var sessionManager = AbsoluteZero.Core.Network.SessionManager.Instance;
            if (sessionManager != null)
                sessionManager.Disconnect();
        }

        #endregion

        #region Display Update

        private void UpdateDisplay()
        {
            float p1Temp = tm.Player1Temp.Value;
            float p2Temp = tm.Player2Temp.Value;

            p1TempText.text = $"{p1Temp:F0}°";
            p2TempText.text = $"{p2Temp:F0}°";
            p1BarFill.fillAmount = Mathf.Clamp01(p1Temp / 37f);
            p2BarFill.fillAmount = Mathf.Clamp01(p2Temp / 37f);

            p1BarFill.color = TempToColor(p1Temp / 37f);
            p2BarFill.color = TempToColor(p2Temp / 37f);

            timerText.text = $"{Mathf.CeilToInt(tm.TurnTimer.Value)}";
            roundText.text = $"Round {tm.RoundNumber.Value}";

            int myIndex = tm.GetLocalPlayerIndex();
            p1NameText.text = myIndex == 0 ? "Player 1 (You)" : "Player 1";
            p2NameText.text = myIndex == 1 ? "Player 2 (You)" : "Player 2";
        }

        private static Color TempToColor(float normalized)
        {
            float h = Mathf.Lerp(0.6f, 0f, Mathf.Clamp01(normalized));
            return Color.HSVToRGB(h, 0.8f, 1f);
        }

        private static string GetPhaseName(TurnPhase phase) => phase switch
        {
            TurnPhase.WaitingForPlayers => "WAITING",
            TurnPhase.PrepTurn => "PREP TURN",
            TurnPhase.AttackTurn => "ATTACK TURN",
            TurnPhase.GameOver => "GAME OVER",
            _ => ""
        };

        #endregion

        #region UI Helpers

        private void SetActionPanelVisible(bool visible)
        {
            if (actionPanel != null) actionPanel.SetActive(visible);
        }

        private void SetGameOverPanelVisible(bool visible)
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(visible);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (attackBtn != null) attackBtn.interactable = interactable;
            if (defendBtn != null) defendBtn.interactable = interactable;
            if (chargeBtn != null) chargeBtn.interactable = interactable;
        }

        #endregion

        #region UI Construction

        private void BuildUI()
        {
            var canvasGO = new GameObject("GameCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
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

            CreatePanel(root, "BG", Vector2.zero, new Vector2(800, 700),
                new Color(0.08f, 0.08f, 0.12f, 0.9f));

            phaseText = CreateText(root, "PhaseText",
                new Vector2(0, 300), new Vector2(400, 50), "WAITING", 36);

            roundText = CreateText(root, "RoundText",
                new Vector2(0, 260), new Vector2(300, 35), "Round 0", 22);
            roundText.color = new Color(0.7f, 0.7f, 0.7f);

            timerText = CreateText(root, "TimerText",
                new Vector2(0, 160), new Vector2(200, 100), "20", 72);

            p1NameText = CreateText(root, "P1Name",
                new Vector2(-250, 80), new Vector2(200, 30), "Player 1", 20);
            p1TempText = CreateText(root, "P1Temp",
                new Vector2(-250, 50), new Vector2(200, 30), "37°", 28);
            p1BarFill = CreateBar(root, "P1Bar",
                new Vector2(-250, 15), new Vector2(300, 25), Color.red);

            p2NameText = CreateText(root, "P2Name",
                new Vector2(250, 80), new Vector2(200, 30), "Player 2", 20);
            p2TempText = CreateText(root, "P2Temp",
                new Vector2(250, 50), new Vector2(200, 30), "37°", 28);
            p2BarFill = CreateBar(root, "P2Bar",
                new Vector2(250, 15), new Vector2(300, 25), Color.blue);

            resultText = CreateText(root, "ResultText",
                new Vector2(0, -40), new Vector2(600, 60), "", 20);
            resultText.color = new Color(1f, 0.9f, 0.5f);

            actionPanel = new GameObject("ActionPanel");
            actionPanel.transform.SetParent(root, false);
            var apRect = actionPanel.AddComponent<RectTransform>();
            apRect.anchoredPosition = new Vector2(0, -150);
            apRect.sizeDelta = new Vector2(600, 70);

            attackBtn = CreateButton(actionPanel.transform, "AttackBtn",
                new Vector2(-200, 0), new Vector2(160, 60), "ATTACK",
                new Color(0.8f, 0.2f, 0.2f));
            attackBtn.onClick.AddListener(OnAttackClicked);

            defendBtn = CreateButton(actionPanel.transform, "DefendBtn",
                new Vector2(0, 0), new Vector2(160, 60), "DEFEND",
                new Color(0.2f, 0.5f, 0.8f));
            defendBtn.onClick.AddListener(OnDefendClicked);

            chargeBtn = CreateButton(actionPanel.transform, "ChargeBtn",
                new Vector2(200, 0), new Vector2(160, 60), "CHARGE",
                new Color(0.2f, 0.7f, 0.3f));
            chargeBtn.onClick.AddListener(OnChargeClicked);

            statusText = CreateText(root, "StatusText",
                new Vector2(0, -230), new Vector2(500, 35), "Waiting...", 20);
            statusText.color = new Color(0.8f, 0.8f, 0.8f);

            gameOverPanel = new GameObject("GameOverPanel");
            gameOverPanel.transform.SetParent(root, false);
            var goRect = gameOverPanel.AddComponent<RectTransform>();
            goRect.anchoredPosition = new Vector2(0, -150);
            goRect.sizeDelta = new Vector2(400, 60);

            var playAgainBtn = CreateButton(gameOverPanel.transform, "PlayAgainBtn",
                new Vector2(-100, 0), new Vector2(170, 55), "PLAY AGAIN",
                new Color(0.2f, 0.6f, 0.3f));
            playAgainBtn.onClick.AddListener(OnPlayAgainClicked);

            var lobbyBtn = CreateButton(gameOverPanel.transform, "LobbyBtn",
                new Vector2(100, 0), new Vector2(170, 55), "BACK TO LOBBY",
                new Color(0.5f, 0.3f, 0.3f));
            lobbyBtn.onClick.AddListener(OnBackToLobbyClicked);

            actionPanel.SetActive(false);
            gameOverPanel.SetActive(false);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name,
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

        private static Image CreateBar(Transform parent, string name,
            Vector2 pos, Vector2 size, Color fillColor)
        {
            var bgGO = new GameObject(name + "_BG");
            bgGO.transform.SetParent(parent, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchoredPosition = pos;
            bgRect.sizeDelta = size;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            var fillGO = new GameObject(name + "_Fill");
            fillGO.transform.SetParent(bgGO.transform, false);
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
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
        }

        #endregion
    }
}
