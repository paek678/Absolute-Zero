using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Cosmetic;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AbsoluteZero.UI.LobbyUI
{
    public class AZLobbyUI : MonoBehaviour
    {
        [SerializeField] GameObject _mainPanel;
        [SerializeField] GameObject _modeSelectPanel;
        [SerializeField] GameObject _roomPanel;
        [SerializeField] GameObject _settingsPanel;
        [SerializeField] GameObject _closetPanel;
        [SerializeField] SoloDuelDefinitionSO _soloEncounter;
        [SerializeField] SoloDuelDefinitionSO _soloValidationEncounter;
        [SerializeField] ItemDataSO[] _soloCatalog;
        [SerializeField] CosmeticRegistrySO _soloCosmetics;
        [SerializeField] SoloSelectionBindings _soloSelection;
        [SerializeField] SoloLobbyProfile[] _soloProfiles;

        LobbyPresenter _presenter;

        LobbyMainView _mainView;
        LobbyModeSelectView _modeSelectView;
        LobbyRoomView _roomView;
        LobbySettingsView _settingsView;
        ClosetView _closetView;
        ClosetPresenter _closetPresenter;
        SoloSelectionView _soloView;
        SoloSelectionPresenter _soloPresenter;
        MatchSessionLease _soloAttemptLease;

        void Start()
        {
            var canvasRoot = FindCanvasRoot();
            if (canvasRoot == null)
            {
                Debug.LogError("[AZLobbyUI] MainUI Canvas not found");
                return;
            }

            EnsureCanvasSetup(canvasRoot);

            if (_mainPanel == null) _mainPanel = canvasRoot.Find("MainPanel")?.gameObject;
            if (_modeSelectPanel == null) _modeSelectPanel = canvasRoot.Find("ModeSelectPanel")?.gameObject;
            if (_roomPanel == null) _roomPanel = canvasRoot.Find("RoomPanel")?.gameObject;
            if (_settingsPanel == null) _settingsPanel = canvasRoot.Find("SettingsPanel")?.gameObject;
            if (_closetPanel == null) _closetPanel = canvasRoot.Find("ClosetPanel")?.gameObject;

            if (_mainPanel == null)
            {
                Debug.LogError("[AZLobbyUI] MainPanel not found — run menu: AbsoluteZero > Setup Lobby UI");
                return;
            }

            _mainView = new LobbyMainView(_mainPanel);
            _modeSelectView = new LobbyModeSelectView(_modeSelectPanel);
            _roomView = new LobbyRoomView(_roomPanel);
            _settingsView = new LobbySettingsView(_settingsPanel);
            _closetView = new ClosetView(_closetPanel);

            _closetPresenter = new ClosetPresenter(_closetView, () => CosmeticProfileService.Instance?.Equipment,
                snapshot => NetworkSessionCoordinator.Instance != null
                    ? NetworkSessionCoordinator.Instance.PublishCosmeticsAsync(JsonUtility.ToJson(snapshot.ToDto()), snapshot.Revision)
                    : Task.FromResult(new CosmeticPublicationResult(CosmeticPublicationStatus.NotApplicable, snapshot.Revision, null)));
            if (_soloSelection != null)
            {
                _soloView = new SoloSelectionView(_soloSelection);
                var profiles = _soloProfiles != null && _soloProfiles.Length > 0 ? _soloProfiles : new[] {
                    new SoloLobbyProfile { Title = "기본 봇", Description = "1대1 규칙으로 플레이합니다.", Encounter = _soloEncounter } };
                _soloPresenter = new SoloSelectionPresenter(_soloView, profiles, InspectSolo, StartSoloAsync, StopSoloStartAsync,
                    () => AppBootstrapper.Instance?.IsReady == true && AppBootstrapper.Instance.SessionRouter.Current == null);
            }
            _presenter = new LobbyPresenter(_mainView, _modeSelectView, _roomView, _settingsView, _closetView, _soloPresenter, _closetPresenter);

            StartCoroutine(WaitForManagers());
        }

        System.Collections.IEnumerator WaitForManagers()
        {
            _mainView.SetStatus("초기화 중...");

            while (LobbyManager.Instance == null || NetworkSessionCoordinator.Instance == null
                || AppBootstrapper.Instance == null || !AppBootstrapper.Instance.IsReady)
                yield return null;

            var lobbyManager = LobbyManager.Instance;
            var coordinator = NetworkSessionCoordinator.Instance;

            _presenter.Initialize(lobbyManager, coordinator);

            _mainView.SetStatus(AppBootstrapper.Instance.SoloSession.LastError ?? "준비 완료");
        }

        SoloConfigurationContext SoloContext(SoloDuelDefinitionSO encounter)
            => new(encounter?.SharedOneVsOneRule, _soloCatalog, _soloCosmetics);

        SoloProfileStatus InspectSolo(SoloDuelDefinitionSO encounter)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool valid = SoloSettingsResolver.TryResolveForDevelopment(encounter, SoloContext(encounter), out var settings, out var errors);
#else
            bool valid = SoloSettingsResolver.TryResolve(encounter, SoloContext(encounter), out var settings, out var errors);
#endif
            if (!valid) return new SoloProfileStatus(false, string.Join("\n", errors), "현재 설정으로 시작할 수 없습니다. 상세 내용을 확인하세요.");
            string details = settings.BotDisplayName + "\n\n1대1 규칙 · 내 PC에서 진행\n" +
                "봇은 미니게임 대신 아이템 사용 준비 시간을 가집니다.\n\n" +
                "시작 상태와 아이템 지급은 기존 1대1 규칙을 따릅니다.";
            if (settings.ProvisionalTuning) details += "\n\n개발 조정 중인 기본 설정입니다. 최종 난이도는 추후 적용됩니다.";
            return new SoloProfileStatus(true, details);
        }

        Task<Result<Unit>> StartSoloAsync(SoloDuelDefinitionSO encounter)
        {
            var session = AppBootstrapper.Instance?.SoloSession;
            if (session == null) return Task.FromResult(Result<Unit>.Failure(OperationErrorCode.InvalidState, "로컬 세션을 준비하지 못했습니다."));
            Task<Result<Unit>> task;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--solo-scripted-slice") >= 0)
            {
                encounter = _soloValidationEncounter;
                task = session.StartValidationAsync(encounter, SoloContext(encounter));
            }
            else task = session.StartDevelopmentAsync(encounter, SoloContext(encounter));
#else
            task = session.StartAsync(encounter, SoloContext(encounter));
#endif
            _soloAttemptLease = session.Lease;
            return task;
        }

        Task StopSoloStartAsync()
        {
            var app = AppBootstrapper.Instance;
            return app != null && app.SessionRouter.Owns(_soloAttemptLease) ? app.SoloSession.StopAsync() : Task.CompletedTask;
        }

        void OnDestroy()
        {
            _presenter?.Dispose();
            _mainView?.Dispose();
            _closetPresenter?.Dispose();
            _closetView?.Dispose();
            _settingsView?.Dispose();
            _soloPresenter?.Dispose();
            _soloView?.Dispose();
        }

        Transform FindCanvasRoot()
        {
            var mainUI = GameObject.Find("MainUI");
            return mainUI != null ? mainUI.transform : null;
        }

        void EnsureCanvasSetup(Transform canvasRoot)
        {
            var go = canvasRoot.gameObject;
            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }
            if (go.GetComponent<GraphicRaycaster>() == null)
                go.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<InputSystemUIInputModule>();
            }
        }
    }
}
