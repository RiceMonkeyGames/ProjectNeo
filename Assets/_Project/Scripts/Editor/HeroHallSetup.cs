using System;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// PREP-10 영웅의 전당 설치. 승급 수치 에셋과 한 단계짜리 시설 정의를 만들어 시작 설정(RunStart)에 넣고,
    /// Game 씬 안뜰에 건물이 없으면 하나 더 세워 F 상호작용을 붙인다. 저장용 콘텐츠 목록도 새로 채운다.
    /// 안뜰 전체를 다시 만들지 않으며, 여러 번 실행해도 에셋·건물을 중복으로 만들지 않는다. 바꾼 수치 에셋은 덮어쓰지 않는다.
    /// </summary>
    public static class HeroHallSetup
    {
        public const string DisplayName = "영웅관";
        public const string BuildingName = "HeroHall";
        public const string SettingsPath = "Assets/_Project/Data/GameFlow/HeroPromotion.asset";
        const string StartPath = "Assets/_Project/Data/GameFlow/Build/RunStart.asset";
        // 병영(Training) 맞은편, 대장간 아래 빈자리. 자리는 SIEGE-36 안뜰 기준(지면 기준 m). 크기는 처음 만들 때의 임시 상자이고,
        // 겉모습과 충돌 상자는 GameCourtyardArtSetup이 모델에 맞춘다.
        public static readonly Vector3 BuildingPosition = new Vector3(231f, 0f, 125f);
        public static readonly Vector3 BuildingSize = new Vector3(13f, 20f, 20f);

        [MenuItem("ProjectNeo/Setup/Add Hero Hall (PREP-10)")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            HeroPromotionSettings settings = CreateSettings();
            FacilityDefinition hall = CreateFacility();
            AssetDatabase.SaveAssets();

            var start = AssetDatabase.LoadAssetAtPath<RunConfig>(StartPath)
                ?? throw new InvalidOperationException("Missing run start config: " + StartPath);
            start.AddFacility(hall);
            start.WithHeroPromotion(settings);
            EditorUtility.SetDirty(start);
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            ConstructionWorld world = GameFacilitySetup.Find<ConstructionWorld>(scene)
                ?? throw new InvalidOperationException("ConstructionWorld is missing in the Game scene.");
            Transform layout = world.transform.Find("Geometry/CourtyardLayout")
                ?? throw new InvalidOperationException("Apply Default Castle first. Missing CourtyardLayout.");
            Transform building = layout.Find(BuildingName);
            if (building == null)
            {
                Material plaster = CombatPrototypeSetup.MaterialAsset("FacilityPlaster", new Color(0.73f, 0.65f, 0.48f));
                Material roof = CombatPrototypeSetup.MaterialAsset("FacilityRoof", new Color(0.28f, 0.32f, 0.43f));
                GameFlowSetup.Facility(layout, BuildingName, BuildingPosition, BuildingSize, plaster, roof, DisplayName);
                building = layout.Find(BuildingName);
                // 다른 건물과 같이 간판에 화면 이름을 쓴다.
                foreach (TMPro.TextMeshPro sign in building.GetComponentsInChildren<TMPro.TextMeshPro>(true)) sign.text = DisplayName;
            }

            GameFacilities facilities = GameFacilitySetup.Find<GameFacilities>(scene)
                ?? throw new InvalidOperationException("Run Connect Facilities To Game Scene first.");
            GamePhaseView view = GameFacilitySetup.Find<GamePhaseView>(scene);
            var station = building.GetComponent<FacilityStation>();
            if (station == null) station = building.gameObject.AddComponent<FacilityStation>();
            station.Configure(view, facilities, FacilityType.HeroHall);
            EditorUtility.SetDirty(station);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RunSaveSetup.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log("Hero Hall added: facility, promotion settings, Game scene building and run save content.");
        }

        static HeroPromotionSettings CreateSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HeroPromotionSettings>(SettingsPath);
            if (existing != null) return existing;
            AssetDatabase.CreateAsset(HeroPromotionSettings.CreateDefault(), SettingsPath);
            return AssetDatabase.LoadAssetAtPath<HeroPromotionSettings>(SettingsPath);
        }

        static FacilityDefinition CreateFacility()
        {
            string path = GameFacilitySetup.PathOf(FacilityType.HeroHall);
            var existing = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(path);
            if (existing != null) return existing;
            AssetDatabase.CreateAsset(FacilityDefinition.Create(FacilityType.HeroHall, DisplayName,
                GameFacilitySetup.Level(0, 0, 0)), path);
            return AssetDatabase.LoadAssetAtPath<FacilityDefinition>(path);
        }
    }
}
