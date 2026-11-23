using System;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// SIEGE-19: Game 씬 성을 7.5m 시험 씬(<see cref="SyntyGridSpikeSetup.Create75"/>)과 같은 성 킷 외형(늘림)으로 꾸민다.
    /// 킷 프리팹(<c>Prefabs/CastleKit</c>)은 시험 씬과 함께 쓰고(없는 조각은 <see cref="SyntyGridSpikeSetup.SaveKitPrefabs"/>로 만든다),
    /// 킷 설정만 Game 데이터 폴더에 둔다(있으면 그 자리에서 고쳐 GUID 유지).
    /// 흉벽은 외형만 킷이다(사용자 결정 2026-11-23): 킷 흉벽 충돌체를 떼고, <see cref="CastleBattlements"/>는 상자 외형만 끄고
    /// 충돌·엄폐·사다리 규칙을 그대로 맡는다. 여러 번 실행해도 컴포넌트를 중복으로 만들지 않는다.
    /// </summary>
    public static class GameCastleKitSetup
    {
        public const string SettingsPath = "Assets/_Project/Data/GameFlow/Build/CastleKit.asset";
        const string KitFolder = "Assets/_Project/Prefabs/CastleKit";
        // 7.5m 칸 계단 방 값(SIEGE-35 시험 씬과 같다).
        const float LandingDepth = 2f;
        const float FlightWidth = 2.16f;

        [MenuItem("ProjectNeo/Setup/Apply Castle Kit To Game Scene (SIEGE-19)")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            CreateOrUpdateSettings();
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            var kit = AssetDatabase.LoadAssetAtPath<CastleKitSettings>(SettingsPath);
            ConstructionWorld world = GameFacilitySetup.Find<ConstructionWorld>(scene);
            var dresser = world.GetComponent<CastleKitDresser>();
            if (dresser == null) dresser = world.gameObject.AddComponent<CastleKitDresser>();
            dresser.Configure(world, kit, withBattlementColliders: false);
            EditorUtility.SetDirty(dresser);

            var battlements = world.GetComponent<CastleBattlements>()
                ?? throw new InvalidOperationException("Run Add Battlements To Game Scene first.");
            battlements.ShowVisuals = false;
            EditorUtility.SetDirty(battlements);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Game 씬에 시험 키(F9 골드, F10 작업 바로 끝내기)를 단다. 에디터·개발 빌드에서만 동작한다.</summary>
        [MenuItem("ProjectNeo/Setup/Add Dev Keys To Game Scene (SIEGE-19)")]
        public static void AddDevKeys()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            ConstructionWorld world = GameFacilitySetup.Find<ConstructionWorld>(scene);
            if (world.GetComponent<ProjectNeo.Flow.GameDevKeys>() == null) world.gameObject.AddComponent<ProjectNeo.Flow.GameDevKeys>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>킷 설정을 만들거나 그 자리에서 고친다. 조각 프리팹은 시험 씬 메뉴가 만든 <c>Prefabs/CastleKit</c>에서 이름으로 읽는다.</summary>
        static void CreateOrUpdateSettings()
        {
            var kit = AssetDatabase.LoadAssetAtPath<CastleKitSettings>(SettingsPath);
            if (kit == null)
            {
                CombatPrototypeSetup.EnsureFolder(SettingsPath.Substring(0, SettingsPath.LastIndexOf('/')));
                kit = CastleKitSettings.Create();
                AssetDatabase.CreateAsset(kit, SettingsPath);
            }
            bool missing = false;
            foreach (CastleKitPiece piece in (CastleKitPiece[])Enum.GetValues(typeof(CastleKitPiece)))
            {
                if (piece == CastleKitPiece.RoomCollider || piece == CastleKitPiece.RoomLink) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/" + piece + ".prefab");
                missing |= prefab == null;
                kit.SetPrefab(piece, prefab);
            }
            // 새 조각(예: 모서리 망루)이 아직 없으면 킷 프리팹을 같은 경로에 다시 만든다(GUID 유지).
            if (missing) SyntyGridSpikeSetup.SaveKitPrefabs(kit);
            var serialized = new SerializedObject(kit);
            serialized.FindProperty("roomLandingDepth").floatValue = LandingDepth;
            serialized.FindProperty("roomFlightWidth").floatValue = FlightWidth;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(kit);
        }
    }
}
