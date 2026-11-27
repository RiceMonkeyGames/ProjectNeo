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
            serialized.FindProperty("battlementHeightScale").floatValue = KitBattlementScale;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            kit.SetBattlementLevels(KitBattlementDrop, CrenellationScale);
            EditorUtility.SetDirty(kit);
        }

        // 킷 흉벽(5m 모듈)을 잰 값(SIEGE-20). Battlements_01: 빈틈 받침 0.76m, 솟은 돌 1.38m. Battlements_03: 빈틈 받침 0.87m,
        // 솟은 돌 1.61m, 성벽 바깥으로 내밀려 칸 가장자리 바깥 0.10~0.66m에 선다. 둘 다 솟은 돌 셋이 5/3m 주기로 서고
        // 가운데가 솟은 돌이다(폭 약 0.84m, 양 끝은 반쪽 빈틈).
        // 사용자 결정 2026-11-27: Lv1 난간은 1.6배 Battlements_01을 성벽에 묻어 돌 꼭대기를 엄폐 높이(1.216m)에 맞추고,
        // Lv2 성가퀴는 Battlements_03을 받침이 같은 엄폐 높이가 되게 키운다(약 1.4배, 솟은 돌 약 2.25m).
        const float CoverHeight = 1.216f;
        const float KitBattlementScale = 1.6f;
        const float KitSill = 0.76f, KitMerlon = 1.38f, KitMerlonWidth = 0.84f, KitPeriod = 5f / 3f;
        const float HighSill = 0.87f, HighMerlon = 1.61f;
        const float KitBattlementDrop = KitMerlon * KitBattlementScale - CoverHeight;
        const float CrenellationScale = CoverHeight / HighSill;
        // 성가퀴 판정 덩어리(두께 0.4m)를 Battlements_03이 선 자리(칸 가장자리 바깥 0.05~0.45m)로 내민다.
        const float HighOutset = 0.45f;

        /// <summary>
        /// SIEGE-20: 엄폐 규칙(<see cref="BattlementSettings"/>)을 킷 흉벽 모양에 맞춘다. Lv1 난간은 묻은 Battlements_01, Lv2 성가퀴는
        /// Battlements_03이다. 킷 설정에 흉벽 배율·묻는 깊이를 넣고, 새 흉벽 조각 프리팹이 없으면 만든다.
        /// 무늬 폭은 킷이 칸 길이에 맞춰 늘어나는 비율(칸 / 모듈)을 곱한 값이다. 여러 번 실행해도 결과가 같다.
        /// </summary>
        [MenuItem("ProjectNeo/Setup/Apply Kit Battlement Cover (SIEGE-20)")]
        public static void ApplyBattlementCover()
        {
            if (AssetDatabase.LoadAssetAtPath<CastleKitSettings>(SettingsPath) == null)
                throw new InvalidOperationException("Run Apply Castle Kit To Game Scene first.");
            var cover = AssetDatabase.LoadAssetAtPath<BattlementSettings>(BattlementSetup.SettingsPath)
                ?? throw new InvalidOperationException("Run Add Battlements To Game Scene first.");
            CreateOrUpdateSettings();
            var kit = AssetDatabase.LoadAssetAtPath<CastleKitSettings>(SettingsPath);

            // Game 칸(7.5m)이 킷 모듈(5m)보다 넓어 흉벽이 면 방향으로 늘어나는 비율.
            float stretch = GameFlowSetup.CellSize / kit.ModuleSize;
            var serialized = new SerializedObject(cover);
            serialized.FindProperty("parapetHeight").floatValue = CoverHeight;
            serialized.FindProperty("parapetGapSill").floatValue = KitSill * KitBattlementScale - KitBattlementDrop;
            serialized.FindProperty("merlonHeight").floatValue = HighMerlon * CrenellationScale;
            serialized.FindProperty("merlonWidth").floatValue = KitMerlonWidth * stretch;
            serialized.FindProperty("crenelWidth").floatValue = (KitPeriod - KitMerlonWidth) * stretch;
            serialized.FindProperty("merlonAtMiddle").boolValue = true;
            serialized.FindProperty("crenellatedOutset").floatValue = HighOutset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cover);
            AssetDatabase.SaveAssets();
        }
    }
}
