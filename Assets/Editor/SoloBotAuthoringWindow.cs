using System;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.UI.LobbyUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbsoluteZero.EditorTools
{
    public sealed class SoloBotAuthoringWindow : EditorWindow
    {
        const string LobbyPath = "Assets/Scenes/LobbyScene.unity";
        const string RulePath = "Assets/Data/GameModeRules/OneVsOneRule.asset";
        [SerializeField] SoloDuelDefinitionSO _encounter;
        ObjectField _selection;
        ScrollView _content;

        [MenuItem("Absolute Zero/Solo/Bot Authoring")]
        public static void Open() => GetWindow<SoloBotAuthoringWindow>("봇 설정 검증");

        public static void Inspect(SoloDuelDefinitionSO encounter)
        {
            var window = GetWindow<SoloBotAuthoringWindow>("봇 설정 검증");
            window._encounter = encounter;
            window._selection?.SetValueWithoutNotify(encounter);
            window.RefreshReport();
        }

        public void CreateGUI()
        {
            minSize = new Vector2(560, 420);
            var root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = root.style.paddingBottom = 10;
            var heading = new Label("봇 설정 · 실행 전 검사");
            heading.style.fontSize = 20;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(heading);
            root.Add(new HelpBox("시작 온도·선풍기·초기 아이템은 현재 1대1 규칙을 그대로 사용합니다. " +
                "사용자 지정 시작값은 정책 확정 전 실행할 수 없습니다. 이 도구는 데이터를 고치거나 경기를 시작하지 않습니다.", HelpBoxMessageType.Info));
            _selection = new ObjectField("검사할 경기 설정") { objectType = typeof(SoloDuelDefinitionSO), allowSceneObjects = false };
            _selection.SetValueWithoutNotify(_encounter);
            _selection.RegisterValueChangedCallback(e => { _encounter = e.newValue as SoloDuelDefinitionSO; RefreshReport(); });
            root.Add(_selection);
            root.Add(new Button(RefreshReport) { text = "저장된 로비 연결로 다시 검사", name = "refreshReport" });
            root.Add(new Button(() => UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal("Docs/BOT_AUTHORING_TOOL_GUIDE.md", 1))
                { text = "작성 안내서 열기" });
            _content = new ScrollView { name = "authoringReport" };
            _content.style.flexGrow = 1;
            root.Add(_content);
            RefreshReport();
        }

        void RefreshReport()
        {
            if (_content == null) return;
            _content.Clear();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Message("편집 모드에서 검사하세요. 실행 중인 데이터는 변경하지 않습니다.", HelpBoxMessageType.Info);
                return;
            }
            try
            {
                var context = ReadSavedLobbyContext(out var defaultEncounter);
                if (_encounter == null) { _encounter = defaultEncounter; _selection.SetValueWithoutNotify(_encounter); }
                var library = AssetDatabase.FindAssets("t:SoloConfigurationSO", new[] { "Assets" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<SoloConfigurationSO>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(a => a != null).ToArray();
                var encounters = library.OfType<SoloDuelDefinitionSO>().OrderBy(a => AssetDatabase.GetAssetPath(a), StringComparer.Ordinal).ToArray();
                var choices = new Foldout { text = "등록된 경기 설정 (검사용 데이터 별도 표시)", value = true };
                foreach (var entry in encounters)
                {
                    var captured = entry;
                    string tag = entry.Readiness == SoloConfigurationReadiness.ValidationFixture ? "검사 전용" : entry.Readiness.ToString();
                    choices.Add(new Button(() => { _encounter = captured; _selection.SetValueWithoutNotify(captured); RefreshReport(); })
                        { text = $"[{tag}] {entry.name} · {entry.ConfigId}" });
                }
                _content.Add(choices);
                foreach (var issue in SoloAuthoringPreview.FindDuplicateIds(library)) Message(issue, HelpBoxMessageType.Warning);
                Message("저장된 LobbyScene의 아이템 목록·외형 목록과 현재 Build Settings의 활성 씬을 검사합니다. " +
                    "씬의 미저장 변경은 포함하지 않습니다. 결과는 검사 시점의 정보이며 시작 시 다시 검증합니다.", HelpBoxMessageType.Info);
                var report = SoloAuthoringPreview.Inspect(_encounter, context);
                DrawGate("개발판 일반 Solo", report.Development);
                DrawGate("배포판 Solo", report.Release);
                DrawGate("명시적 검사 전용 진입", report.Fixture);
                foreach (var warning in report.Warnings) Message(warning, HelpBoxMessageType.Warning);
                DrawReferences();
                var settings = report.PreviewSettings;
                if (settings == null) return;
                Text($"유효 기본값: 시작 {settings.StartingTemperature:0.##}° / 선풍기 {settings.FanBaseline:0.##} / 아이템은 1대1 지급 유지");
                Text($"생각 {settings.MinimumThinkSeconds:0.###}~{settings.MaximumThinkSeconds:0.###}초 / " +
                    $"Ready 여유 {settings.ReadyReserveSeconds:0.###}초 / 재시도 한도 {settings.RetryBudget}");
                var weights = settings.TacticalWeights;
                Text($"판단 가중치: 생존 {weights.Survival} · 마무리 {weights.Finish} · 대응 {weights.Counter} · 자원 {weights.Resource} · 일반 {weights.General}");
                Text("가중치는 후보 점수에 적용합니다. 그래프 가지의 우선순위 자체를 변경하지는 않습니다.");
                var versions = new Foldout { text = "설정 ID / 버전 (경기 시작 시 복사)", value = false };
                foreach (var stamp in settings.ConfigurationVersions) versions.Add(new Label($"{stamp.Kind}: {stamp.Id} v{stamp.Version}"));
                _content.Add(versions);
                var times = new Foldout { text = $"아이템 시간표 · 준비 {settings.RuleSnapshot.PrepPhaseDuration:0.##}초", value = true };
                times.Add(Wrapped("아래 합계는 생각 + 사용 지연 + Ready 여유입니다. 실제 생각 시간은 남은 시간에 맞춰 줄어들 수 있습니다. " +
                    "프레임 여유·관측 시점·대상 유효성도 영향을 주므로 성공을 보장하는 표는 아닙니다. 비활성 아이템은 카탈로그 번호 보존용입니다."));
                foreach (var row in report.ItemTiming)
                    times.Add(Wrapped($"[{row.CatalogIndex}] {row.ItemName} · " +
                        (row.Enabled ? (row.NeedsTimingReview ? "시간 검토" : "사용 가능 항목") : "사용 제외") +
                        $" · 지연 {row.Delay:0.###}초 · 합계 {row.NominalMinimum:0.###}~{row.NominalMaximum:0.###}초"));
                _content.Add(times);
            }
            catch (Exception error) { Message("검사 실패: " + error.Message, HelpBoxMessageType.Error); }
        }

        public static SoloConfigurationContext ReadSavedLobbyContext(out SoloDuelDefinitionSO defaultEncounter)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Saved lobby inspection is available in Edit Mode only.");
            var scene = EditorSceneManager.OpenPreviewScene(LobbyPath);
            try
            {
                var ui = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AZLobbyUI>(true)).Single();
                using var serialized = new SerializedObject(ui);
                defaultEncounter = serialized.FindProperty("_soloEncounter").objectReferenceValue as SoloDuelDefinitionSO;
                var items = serialized.FindProperty("_soloCatalog");
                var catalog = new ItemDataSO[items.arraySize];
                for (int i = 0; i < catalog.Length; i++) catalog[i] = items.GetArrayElementAtIndex(i).objectReferenceValue as ItemDataSO;
                var cosmetics = serialized.FindProperty("_soloCosmetics").objectReferenceValue as CosmeticRegistrySO;
                var enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToHashSet(StringComparer.Ordinal);
                return new SoloConfigurationContext(AssetDatabase.LoadAssetAtPath<GameModeRuleSO>(RulePath), catalog, cosmetics,
                    path => enabledScenes.Contains(path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        void DrawGate(string title, SoloLaunchPreview gate)
        {
            Message(title + (gate.CanLaunch ? " · 구성 검사 통과" : " · 실행 불가"), gate.CanLaunch ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning);
            foreach (var issue in gate.Errors) Text("• " + issue);
        }

        void DrawReferences()
        {
            var refs = new Foldout { text = "수정 위치 · 해당 자산을 Inspector에서 열기", value = true };
            Link(refs, "경기 / 준비 상태 / 씬", _encounter);
            if (_encounter != null)
            {
                var bot = _encounter.Bot;
                var difficulty = _encounter.Difficulty;
                Link(refs, "봇 / 외형 / 기본 판단", bot);
                Link(refs, "난이도 / 생각 시간 / Ready / 가중치", difficulty);
                if (bot != null) { Link(refs, "Behavior 그래프", bot.Graph); Link(refs, "기본 시작 설정 (상속만 가능)", bot.BaseStats); Link(refs, "기본 아이템 지연", bot.ItemUsePolicy); }
                if (difficulty != null) { Link(refs, "난이도 시작 설정 (상속만 가능)", difficulty.StatOverride); Link(refs, "난이도 아이템 지연 (우선 적용)", difficulty.ItemUseOverride); }
                Link(refs, "공용 1대1 규칙 (기존 게임에도 영향)", _encounter.SharedOneVsOneRule);
            }
            _content.Add(refs);
        }

        static void Link(VisualElement parent, string label, UnityEngine.Object asset)
        {
            var field = new ObjectField(label) { value = asset, allowSceneObjects = false, objectType = typeof(UnityEngine.Object) };
            field.SetEnabled(false);
            parent.Add(field);
            if (asset != null) parent.Add(new Button(() => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }) { text = "열기: " + asset.name });
        }

        static Label Wrapped(string text) { var label = new Label(text); label.style.whiteSpace = WhiteSpace.Normal; label.style.marginBottom = 5; return label; }
        void Text(string text) => _content.Add(Wrapped(text));
        void Message(string text, HelpBoxMessageType type) => _content.Add(new HelpBox(text, type));
    }

    [CustomEditor(typeof(SoloConfigurationSO), true)]
    public sealed class SoloConfigurationInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            string help = target switch
            {
                BotStatProfileSO => "현재는 상속 전용입니다. Override를 켜거나 Initial Items를 채우면 실행이 거부됩니다. 시작 온도·선풍기·아이템은 기존 1대1 규칙을 사용합니다.",
                BotDifficultyProfileSO => "생각 시간·Ready 여유·판단 가중치·지연 프로필을 설정합니다. Provisional Tuning은 개발 조정 중이라는 뜻이며 배포 승인이 아닙니다. 가중치는 후보 점수에 적용하고 그래프 가지 순서는 바꾸지 않습니다.",
                BotItemUsePolicySO => "봇은 미니게임 대신 양수인 사용 지연을 적용합니다. 아이템 참조는 로비 카탈로그에 있어야 하며 중복 재정의는 불가합니다. 난이도의 정책이 있으면 그 정책 전체를 우선 사용합니다.",
                BotDefinitionSO => "Behavior 그래프를 연결합니다. 봇은 자기 정보·공개 정보로 판단하며 행동 제출·효과 적용은 기존 서버 경계를 사용합니다. 그래프와 외형 원본은 실행 중 수정하지 않습니다.",
                _ => "Draft는 실행 불가, ValidationFixture는 명시적 검사 진입만 가능합니다. Ready라도 그래프·값·배포 승인 검사를 통과해야 합니다. 복제한 자산은 고유 Config Id와 Version을 지정하세요."
            };
            root.Add(new HelpBox(help, HelpBoxMessageType.Info));
            root.Add(new Button(() => SoloBotAuthoringWindow.Inspect(target as SoloDuelDefinitionSO)) { text = "봇 설정 검증 창 열기" });
            var fields = new VisualElement();
            InspectorElement.FillDefaultInspector(fields, serializedObject, this);
            fields.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            root.Add(fields);
            root.RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.playModeStateChanged += OnPlayMode);
            root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.playModeStateChanged -= OnPlayMode);
            void OnPlayMode(PlayModeStateChange _) => fields.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            return root;
        }
    }
}
