using System.Collections;
using AbsoluteZero.Core.Audio;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Network;
using AbsoluteZero.UI.Game.Bridge;
using AbsoluteZero.UI.Game.Build;
using AbsoluteZero.UI.MiniGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Game.Presenters
{
    public sealed class MatchHudPresenter : MonoBehaviour
    {
        IGameDataBridge _bridge;
        ILocalPlayerCommands _commands;

        TextMeshProUGUI _phaseText;
        TextMeshProUGUI _timerText;
        TextMeshProUGUI _statusText;
        TextMeshProUGUI _roundText;

        Image _timerFillImage;
        Image _timerSliderImage;
        RectTransform _clockHandRT;
        bool _countdownWarningActive;
        OwnedPositionOffset _summerOffset;
        LocalSettingsService _settings;

        Canvas _readyCanvas;
        Button _readyButton;
        Image _readyButtonImage;

        GameObject _envPanel;
        TextMeshProUGUI _envText;

        RectTransform _timerContainerRT;
        bool _summerVacShaking;

        bool _hasSelectedItem;

        static readonly WaitForSeconds _waitEnvHide = new(3.5f);

        const int ALARM_TIME_THRESHOLD = 5;

        bool _initialized;

        public void Initialize(IGameDataBridge bridge, ILocalPlayerCommands commands, GameHudRefs refs)
        {
            _bridge = bridge;
            _commands = commands;

            _phaseText = refs.PhaseText;
            _timerText = refs.TimerText;
            _statusText = refs.StatusText;
            _roundText = refs.RoundText;
            _timerFillImage = refs.TimerFillImage;
            _timerSliderImage = refs.TimerSliderImage;
            _clockHandRT = refs.ClockHandRT;
            _timerContainerRT = refs.TimerContainerRT;
            if (_timerContainerRT != null) _summerOffset = OwnedPositionOffset.ForRectTransform(_timerContainerRT);
            _settings = LocalSettingsRuntime.Instance?.Service;
            if (_settings != null) _settings.Changed += OnSettingsChanged;
            _readyCanvas = refs.ReadyCanvas;
            _readyButton = refs.ReadyButton;
            _readyButtonImage = refs.ReadyButtonImage;
            _envPanel = refs.EnvPanel;
            _envText = refs.EnvText;

            _readyButton.onClick.AddListener(OnReadyClicked);

            _bridge.OnPhaseChanged += HandlePhaseChanged;
            _bridge.OnMatchSnapshotChanged += HandleMatchSnapshotChanged;
            _bridge.OnEnvironmentAnnounced += HandleEnvironmentAnnounced;
            _bridge.OnLocalHasSelectedItemChanged += HandleHasSelectedItemChanged;
            _bridge.OnOpponentRevealed += HandleOpponentRevealed;

            var match = _bridge.CurrentMatch;
            HandlePhaseChanged(TurnPhase.WaitingForPlayers, match.CurrentPhase);

            _initialized = true;
            HandleMatchSnapshotChanged(match);
        }

        public void SetStatusText(string text)
        {
            if (_statusText != null)
                _statusText.text = text;
        }

        void HandlePhaseChanged(TurnPhase oldPhase, TurnPhase newPhase)
        {
            if (newPhase != TurnPhase.PrepPhase) SetCountdownWarning(false);
            _phaseText.text = newPhase switch
            {
                TurnPhase.WaitingForPlayers => "참가자 대기",
                TurnPhase.PrepPhase => "준비 단계",
                TurnPhase.AttackPhase => "행동 진행",
                TurnPhase.ResolutionPhase => "결과 정산",
                TurnPhase.RoundOver => "라운드 종료",
                _ => ""
            };

            if (_readyCanvas != null)
            {
                bool isLocalGhost = false;
                var match = _bridge.CurrentMatch;
                byte localSeat = _bridge.LocalSeatIndex;
                if (match.LifeStates != null && localSeat < match.LifeStates.Length)
                    isLocalGhost = match.LifeStates[localSeat] == Core.Player.LifeState.Ghost;

                _readyCanvas.gameObject.SetActive(newPhase == TurnPhase.PrepPhase && !isLocalGhost);
                if (newPhase == TurnPhase.PrepPhase && !isLocalGhost)
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
                _hasSelectedItem = false;
                _statusText.text = "Select an item!";
                GameAudioManager.Instance?.StartFanLoop();
            }
            else if (newPhase == TurnPhase.AttackPhase)
            {
                _statusText.text = "Resolving...";
                GameAudioManager.Instance?.StopFanLoop();
            }
            else if (newPhase == TurnPhase.RoundOver)
            {
                GameAudioManager.Instance?.StopFanLoop();
                GameAudioManager.Instance?.StopEnvironment();
            }

        }

        void HandleMatchSnapshotChanged(MatchSnapshot match)
        {
            if (!isActiveAndEnabled) return;
            UpdateTimerDisplay(match);
            UpdateRoundDisplay(match);
        }

        void UpdateTimerDisplay(MatchSnapshot match)
        {
            if (match.CurrentPhase != TurnPhase.PrepPhase)
            {
                _timerText.text = "";
                if (_timerSliderImage != null) _timerSliderImage.fillAmount = 0f;
                if (_clockHandRT != null) _clockHandRT.localRotation = Quaternion.identity;
                SetCountdownWarning(false);
                return;
            }

            int remaining = match.RemainingTime;
            float total = match.PrepDuration;
            float ratio = total > 0f ? Mathf.Clamp01(remaining / total) : 1f;

            _timerText.text = $"{remaining}";

            if (_timerSliderImage != null)
                _timerSliderImage.fillAmount = 1f - ratio;
            if (_clockHandRT != null)
                _clockHandRT.localRotation = Quaternion.Euler(0f, 0f, ratio * 360f);

            bool shouldAlarm = remaining > 0 && remaining <= ALARM_TIME_THRESHOLD;
            SetCountdownWarning(shouldAlarm);

            _timerText.color = remaining <= ALARM_TIME_THRESHOLD ? Color.red : Color.white;
        }

        void SetCountdownWarning(bool active)
        {
            active &= isActiveAndEnabled;
            if (_countdownWarningActive == active) return;
            _countdownWarningActive = active;
            if (active) GameAudioManager.Instance?.PlayClockTick();
            else GameAudioManager.Instance?.StopClockTick();
        }

        void OnSettingsChanged()
        {
            if (_settings != null && !_settings.Current.Shake) ClearShakeOffsets();
        }
        void ClearShakeOffsets() => _summerOffset?.Clear();
        void OnEnable()
        {
            if (_initialized) HandleMatchSnapshotChanged(_bridge.CurrentMatch);
        }
        void OnDisable()
        {
            SetCountdownWarning(false);
            ClearShakeOffsets();
        }
        void LateUpdate()
        {
            if (!_initialized || (_settings != null && !_settings.Current.Shake)) return;
            if (_summerVacShaking)
            {
                float t = Time.time * 6 * Mathf.PI * 2;
                _summerOffset?.Apply(new Vector3(Mathf.Sin(t) * 3, Mathf.Cos(t * 1.3f) * 3, 0));
            }
        }

        void UpdateRoundDisplay(MatchSnapshot match)
        {
            if (_roundText != null) _roundText.text = $"라운드 {match.RoundNumber}";
        }

        void HandleHasSelectedItemChanged(bool hasItem)
        {
            _hasSelectedItem = hasItem;
            if (hasItem)
                _statusText.text = "Item selected!";
        }

        public bool TryReadyByKey()
        {
            if (_readyButton == null || !_readyButton.interactable) return false;
            if (_readyCanvas == null || !_readyCanvas.gameObject.activeInHierarchy) return false;
            OnReadyClicked();
            return true;
        }

        void OnReadyClicked()
        {
            if (HoverRaycaster.Instance != null && HoverRaycaster.Instance.CurrentHovered != null)
                return;

            GameAudioManager.Instance?.PlayButtonClick();
            if (MiniGameHub.IsRunning) return;

            _commands.PressReady();
            _statusText.text = _hasSelectedItem ? "Ready!" : "Ready! (No action)";

            var pressedSprite = GameSprites.Get(GameSprites.BTN_PRESSED);
            if (_readyButtonImage != null && pressedSprite != null)
                _readyButtonImage.sprite = pressedSprite;

            if (_readyButton != null)
                _readyButton.interactable = false;
        }

        void HandleEnvironmentAnnounced(EnvironmentType env)
        {
            // Reserved legacy ID is not a standalone announcement anymore.
            if (env == EnvironmentType.CicadaSong) return;
            StopSummerVacShake();

            if (_envPanel == null || _envText == null) return;

            string name = env switch
            {
                EnvironmentType.SunnyDay => "햇살이 쨍쨍해집니다.",
                EnvironmentType.CoolBreeze => "시원한 바람이 불어옵니다.",
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
            }
        }

        void StopSummerVacShake()
        {
            if (!_summerVacShaking) return;
            _summerVacShaking = false;
            _summerOffset?.Clear();
            if (_timerFillImage != null)
                _timerFillImage.color = Color.white;
        }

        IEnumerator HideEnvPanelAfterDelay()
        {
            yield return _waitEnvHide;
            if (_envPanel != null)
                _envPanel.SetActive(false);
        }

        void HandleOpponentRevealed(byte forSeatIndex, short opponentItemId)
        {
            if (forSeatIndex != _bridge.LocalSeatIndex) return;

            string itemName = opponentItemId >= 0
                ? (ItemManager.Instance?.GetItemData(opponentItemId)?.ItemName ?? "???")
                : "Not Selected";

            _statusText.text = $"Revealed: {itemName}";
            Debug.Log($"[UI] Tarot reveal: opponent selected '{itemName}' (id={opponentItemId})");
        }

        void OnDestroy()
        {
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
            ClearShakeOffsets();
            if (_readyButton != null)
                _readyButton.onClick.RemoveListener(OnReadyClicked);

            if (_bridge != null)
            {
                _bridge.OnPhaseChanged -= HandlePhaseChanged;
                _bridge.OnMatchSnapshotChanged -= HandleMatchSnapshotChanged;
                _bridge.OnEnvironmentAnnounced -= HandleEnvironmentAnnounced;
                _bridge.OnLocalHasSelectedItemChanged -= HandleHasSelectedItemChanged;
                _bridge.OnOpponentRevealed -= HandleOpponentRevealed;
            }
            StopSummerVacShake();
            SetCountdownWarning(false);

            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.StopFanLoop();
                GameAudioManager.Instance.StopClockTick();
                GameAudioManager.Instance.StopEnvironment();
            }
        }
    }
}
