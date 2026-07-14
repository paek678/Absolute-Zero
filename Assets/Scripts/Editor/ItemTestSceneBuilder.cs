#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using AbsoluteZero.UI.TestUI;

namespace AbsoluteZero.EditorTools
{
    /// <summary>
    /// 아이템 테스트 씬 자동 생성기 (PLAN_005/006).
    /// [Tools > AbsoluteZero > Build ItemTestScene]     — 로컬 샌드박스 (혼자 양쪽 조작)
    /// [Tools > AbsoluteZero > Build ItemNetTestScene]  — 네트워크 테스트 (ServerRpc 왕복, MPPM 2인/더미 1인)
    /// (MCP execute_menu_item으로도 실행 가능 — 수동/자동 양쪽 지원)
    /// </summary>
    public static class ItemTestSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/ItemTestScene.unity";
        const string NetScenePath = "Assets/Scenes/ItemNetTestScene.unity";
        const string PrefabPath = "Assets/Prefabs/ItemTestPlayer.prefab";
        const string ManagerPrefabPath = "Assets/Prefabs/ItemNetTestManager.prefab";
        const string ItemFolder = "Assets/Data/Items";

        [MenuItem("Tools/AbsoluteZero/Build ItemTestScene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCameraAndLight();

            // NetworkManager + UnityTransport (로컬 호스트 전용, Relay 불필요)
            var nmGO = new GameObject("NetworkManager");
            var nm = nmGO.AddComponent<NetworkManager>();
            var utp = nmGO.AddComponent<UnityTransport>();
            nm.NetworkConfig.NetworkTransport = utp;

            // 플레이어 프리팹 (드라이버가 런타임에 2개 spawn)
            // ※ 씬 배치 NetworkObject는 프로그램 생성 씬에서 GlobalObjectIdHash가 0으로 남아
            //    StartHost가 "same GlobalObjectIdHash value 0" 예외로 실패 → 프리팹 방식 필수
            var playerPrefab = LoadOrCreatePlayerPrefab();

            // 테스트 드라이버 + 프리팹/아이템 SO 참조 연결
            var driverGO = new GameObject("ItemTestDriver");
            var driver = driverGO.AddComponent<ItemSystemTestDriver>();
            var so = new SerializedObject(driver);
            so.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            WireItems(so);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[ItemTestSceneBuilder] ItemTestScene 생성 완료 → {ScenePath} (Play 누르면 자동 호스트 시작)");
        }

        [MenuItem("Tools/AbsoluteZero/Build ItemNetTestScene")]
        public static void BuildNet()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCameraAndLight();

            var nmGO = new GameObject("NetworkManager");
            var nm = nmGO.AddComponent<NetworkManager>();
            var utp = nmGO.AddComponent<UnityTransport>();
            nm.NetworkConfig.NetworkTransport = utp;
            // 씬 동기화 비활성 — MPPM 가상 플레이어는 이미 같은 씬을 로드한 상태로 접속하며,
            // 켜두면 빌드 목록 미등록 씬이라 접속 시 씬 싱크가 실패/경고를 낸다
            nm.NetworkConfig.EnableSceneManagement = false;

            var playerPrefab = LoadOrCreatePlayerPrefab();
            var managerPrefab = CreateManagerPrefab();

            var uiGO = new GameObject("ItemNetTestUI");
            var ui = uiGO.AddComponent<ItemNetTestUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            so.FindProperty("managerPrefab").objectReferenceValue = managerPrefab;
            WireItems(so);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, NetScenePath);
            Debug.Log($"[ItemTestSceneBuilder] ItemNetTestScene 생성 완료 → {NetScenePath} " +
                      "(Play → 호스트 시작 / MPPM 가상 플레이어 → 클라이언트 접속)");
        }

        static void CreateCameraAndLight()
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            camGO.transform.position = new Vector3(0f, 1.5f, -6f);
            camGO.AddComponent<AudioListener>();

            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static GameObject LoadOrCreatePlayerPrefab()
        {
            // 항상 재생성 — 같은 경로에 덮어쓰므로 GUID(=GlobalObjectIdHash)는 유지됨
            // 2.5D 스타일: 캐릭터는 납작한 판 (캡슐 금지 — 기획 지시)
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temp.name = "ItemTestPlayer";
            temp.transform.localScale = new Vector3(1.1f, 1.7f, 0.1f);
            Object.DestroyImmediate(temp.GetComponent<Collider>());   // 아이템 클릭 레이캐스트 방해 방지
            temp.AddComponent<NetworkObject>();   // 프리팹 임포트 시 GlobalObjectIdHash 계산됨
            temp.AddComponent<PlayerState>();
            temp.AddComponent<PlayerInventory>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        static GameObject CreateManagerPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
            if (existing != null) return existing;

            var temp = new GameObject("ItemNetTestManager");
            temp.AddComponent<NetworkObject>();
            temp.AddComponent<ItemNetTestManager>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, ManagerPrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        static void WireItems(SerializedObject so)
        {
            WireItem(so, "fanItem", $"{ItemFolder}/Item_Fan.asset");
            WireItem(so, "windbreakerItem", $"{ItemFolder}/Item_Windbreaker.asset");
            WireItem(so, "warmTeaItem", $"{ItemFolder}/Item_WarmTea.asset");
            WireItem(so, "catItem", $"{ItemFolder}/Item_Cat.asset");
        }

        static void WireItem(SerializedObject so, string field, string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ItemDataSO>(assetPath);
            if (asset == null)
            {
                Debug.LogError($"[ItemTestSceneBuilder] 아이템 SO 없음: {assetPath} — PLAN_004 에셋 확인");
                return;
            }
            so.FindProperty(field).objectReferenceValue = asset;
        }
    }
}
#endif
