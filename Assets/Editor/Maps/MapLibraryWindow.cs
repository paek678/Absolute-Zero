using System;
using System.Linq;
using AbsoluteZero.Core.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.EditorTools.Maps
{
    public sealed class MapLibraryWindow : EditorWindow
    {
        [SerializeField] MapLibraryAsset _library;
        [SerializeField] MapDefinitionSO _selected;
        string _message;
        MessageType _messageType;
        Vector2 _scroll;

        [MenuItem("Absolute Zero/Maps/Map Library")]
        public static void Open() => GetWindow<MapLibraryWindow>("Map Library");

        void OnEnable()
        {
            if (_library == null)
            {
                var guid = AssetDatabase.FindAssets("t:MapLibraryAsset").FirstOrDefault();
                if (guid != null) _library = AssetDatabase.LoadAssetAtPath<MapLibraryAsset>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            try { DrawContent(); }
            finally { EditorGUILayout.EndScrollView(); }
        }

        void DrawContent()
        {
            EditorGUILayout.LabelField("맵 프리셋", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("맵 하나를 선택하면 바닥·정자·배경·등불·조명이 함께 적용됩니다. 적용은 편집 모드에서 진행합니다.", MessageType.Info);
            _library = (MapLibraryAsset)EditorGUILayout.ObjectField("맵 라이브러리", _library, typeof(MapLibraryAsset), false);
            if (_library == null) return;
            var maps = (_library.Maps ?? Array.Empty<MapDefinitionSO>()).Where(m => m != null).ToArray();
            if (maps.Length == 0) { EditorGUILayout.HelpBox("라이브러리에 맵 데이터를 등록하세요.", MessageType.Warning); return; }
            int index = Array.IndexOf(maps, _selected);
            if (index < 0) index = 0;
            index = EditorGUILayout.Popup("선택할 맵", index, maps.Select(m => m.DisplayName + "  [" + m.Id + "]").ToArray());
            _selected = maps[index];
            EditorGUILayout.ObjectField("맵 데이터", _selected, typeof(MapDefinitionSO), false);
            EditorGUILayout.ObjectField("통합 프리팹", _selected.EnvironmentPrefab, typeof(GameObject), false);
            if (GUILayout.Button("선택한 맵 프리팹 열기 · 앵커 편집")) AssetDatabase.OpenAsset(_selected.EnvironmentPrefab);
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("현재 씬에 적용 (Undo 가능)")) Run(() => {
                    MapPresetAuthoring.ValidateLibrary(_library);
                    var binding = MapPresetAuthoring.GetBinding(SceneManager.GetActiveScene());
                    MapPresetAuthoring.Apply(binding, _selected); Selection.activeGameObject = binding.gameObject;
                }, "현재 씬에 적용했습니다. 씬을 저장하면 유지됩니다.");
                if (GUILayout.Button("등록된 전투 씬 모두 적용 · 저장")) Run(() => MapPresetAuthoring.ApplyToRegisteredScenes(_library, _selected), "등록된 전투 씬에 적용하고 저장했습니다.");
            }
            EditorGUILayout.LabelField("전투 씬", string.Join(", ", (_library.GameplayScenes ?? Array.Empty<SceneAsset>()).Where(s => s != null).Select(s => s.name)));
            if (GUILayout.Button("라이브러리 구성 열기")) Selection.activeObject = _library;
            DrawCharacterPlacement();
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, _messageType);
        }

        void DrawCharacterPlacement()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("캐릭터 배치 미리보기", EditorStyles.boldLabel);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            MapCharacterLayout layout = stage != null ? stage.prefabContentsRoot.GetComponent<MapCharacterLayout>() : null;
            if (stage == null) MapCharacterLayout.TryFind(SceneManager.GetActiveScene(), out layout);
            if (layout == null) { EditorGUILayout.HelpBox("앵커가 있는 맵 프리팹 또는 맵 씬을 열어주세요.", MessageType.Info); return; }
            EditorGUI.BeginChangeCheck();
            MapCharacterPreview.Mode = (MapLayoutMode)EditorGUILayout.EnumPopup("배치 모드", MapCharacterPreview.Mode);
            MapCharacterPreview.PlayerCount = MapCharacterPreview.Mode == MapLayoutMode.Multi
                ? EditorGUILayout.IntSlider("참가자 수", MapCharacterPreview.PlayerCount, 3, 4) : 2;
            MapCharacterPreview.LocalSeat = EditorGUILayout.IntSlider("내 좌석 (0부터)", MapCharacterPreview.LocalSeat, 0, MapCharacterPreview.PlayerCount - 1);
            MapCharacterPreview.ShowServerAnchors = EditorGUILayout.Toggle("서버 스폰 앵커 표시", MapCharacterPreview.ShowServerAnchors);
            if (EditorGUI.EndChangeCheck()) {
                if (MapCharacterPreview.IsActive) Run(() => MapCharacterPreview.Start(layout, _library.CharacterPreviewPrefab), "배치 미리보기를 갱신했습니다.");
                SceneView.RepaintAll();
            }
            EditorGUILayout.HelpBox("노란 앵커: 화면의 상대 캐릭터 발 위치. 파란 앵커: 서버의 실제 좌석 스폰 위치. 내 캐릭터는 기존 1인칭 손 연출을 사용합니다. 외형 미리보기는 실제 플레이어의 커스터마이징을 바꾸지 않습니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)) {
                if (GUILayout.Button(MapCharacterPreview.IsActive ? "캐릭터 미리보기 끄기" : "캐릭터 미리보기 켜기")) {
                    if (MapCharacterPreview.IsActive) MapCharacterPreview.Stop();
                    else Run(() => MapCharacterPreview.Start(layout, _library.CharacterPreviewPrefab), "앵커를 이동하면 미리보기 캐릭터가 따라갑니다.");
                }
            }
            int capacity = MapCharacterLayout.Capacity(MapCharacterPreview.Mode);
            for (int slot = 0; slot < capacity - 1; slot++) {
                int seat = MapCharacterPreview.SeatForRemoteSlot(slot, MapCharacterPreview.LocalSeat, MapCharacterPreview.PlayerCount);
                var anchor = layout.GetAnchor(MapCharacterPreview.Mode, MapAnchorRole.RemoteVisual, slot);
                string occupant = seat < 0 ? "비어 있음" : $"P{seat + 1} / 좌석 {seat}";
                if (GUILayout.Button($"표시 슬롯 {slot} → {occupant} · 앵커 선택") && anchor != null) Selection.activeGameObject = anchor.gameObject;
            }
            if (MapCharacterPreview.ShowServerAnchors)
                for (int seat = 0; seat < capacity; seat++) {
                    var anchor = layout.GetAnchor(MapCharacterPreview.Mode, MapAnchorRole.ServerSpawn, seat);
                    if (GUILayout.Button($"P{seat + 1} 서버 스폰 앵커 선택") && anchor != null) Selection.activeGameObject = anchor.gameObject;
                }
            EditorGUILayout.HelpBox("맵 프리팹에서 편집하면 해당 맵의 배치로 저장됩니다. 씬에서 편집하면 프리팹 오버라이드가 되므로 다른 씬에도 쓰려면 Overrides에서 맵 프리팹에 적용하세요. 런타임 로비 선택은 별도 기능입니다.", MessageType.None);
        }

        void Run(Action action, string success)
        {
            try { action(); _message = success; _messageType = MessageType.Info; }
            catch (Exception exception) { _message = exception.Message; _messageType = MessageType.Error; }
        }
    }

    [CustomEditor(typeof(MapSceneBinding))]
    public sealed class MapSceneBindingEditor : UnityEditor.Editor
    {
        MapDefinitionSO _selected;
        string _error;
        public override void OnInspectorGUI()
        {
            var binding = (MapSceneBinding)target;
            using (new EditorGUI.DisabledScope(true)) {
                EditorGUILayout.ObjectField("현재 적용 맵", binding.AppliedMap, typeof(MapDefinitionSO), false);
                EditorGUILayout.ObjectField("맵 인스턴스", binding.EnvironmentInstance, typeof(GameObject), true);
            }
            if (_selected == null) _selected = binding.AppliedMap;
            _selected = (MapDefinitionSO)EditorGUILayout.ObjectField("교체할 맵", _selected, typeof(MapDefinitionSO), false);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("선택한 맵 적용")) {
                    try { MapPresetAuthoring.Apply(binding, _selected); _error = null; }
                    catch (Exception exception) { _error = exception.Message; }
                }
            if (GUILayout.Button("맵 라이브러리 열기")) MapLibraryWindow.Open();
            if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Error);
        }
    }
}
