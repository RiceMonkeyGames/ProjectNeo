using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// FLOW-02 런 저장. 프로젝트의 정의 에셋(아이템·재료·영웅·잡졸·시설·성 조각·수성 무기)을 모두 모아 ID 목록 에셋을 채우고
    /// Game 씬의 전투 설정(Battle.asset)에 넣는다. 씬 파일은 바꾸지 않는다. 목록은 실행할 때마다 다시 채우므로,
    /// 정의 에셋을 새로 만들거나 지운 뒤에 다시 실행한다. 시설은 새 런 설정(RunStart)에 있는 것만 넣는다.
    /// </summary>
    public static class RunSaveSetup
    {
        public const string CatalogPath = "Assets/_Project/Data/GameFlow/RunContent.asset";
        const string BattlePath = "Assets/_Project/Data/GameFlow/Battle.asset";
        const string Root = "Assets/_Project";

        [MenuItem("ProjectNeo/Setup/Refresh Run Save Content (FLOW-02)")]
        public static void Apply()
        {
            var battle = AssetDatabase.LoadAssetAtPath<GameBattleSettings>(BattlePath);
            if (battle == null) throw new System.InvalidOperationException("Battle settings are missing: " + BattlePath);

            RunContentCatalog catalog = CombatPrototypeSetup.Save(RunContentCatalog.Create(), CatalogPath);
            Fill(catalog);
            EditorUtility.SetDirty(catalog);
            foreach (string problem in catalog.FindIdProblems()) Debug.LogError("Run save content: " + problem, catalog);

            var data = new SerializedObject(battle);
            SerializedProperty field = data.FindProperty("runContent");
            if (field.objectReferenceValue != catalog)
            {
                field.objectReferenceValue = catalog;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(battle);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>프로젝트의 정의 에셋으로 목록을 다시 채운다. 테스트가 목록이 낡았는지 볼 때도 쓴다.</summary>
        public static void Fill(RunContentCatalog catalog)
        {
            catalog.Set(All<ItemDefinition>(), All<MaterialDefinition>(), All<HeroDefinition>(), All<SoldierDefinition>(),
                RunFacilities(), All<BuildPieceDefinition>(), All<SiegeWeaponDefinition>());
            // 성 조각은 ID 필드가 없고 이름이 겹치는 에셋이 있어, 바뀌지 않는 에셋 GUID를 저장용 ID로 쓴다.
            var pieceIds = new List<string>();
            foreach (BuildPieceDefinition piece in catalog.Pieces)
                pieceIds.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(piece)));
            catalog.SetPieceIds(pieceIds);
            // 런에서 찍은 퍽도 저장에 id로 남는다(META-02).
            catalog.SetPerks(All<ProjectNeo.Meta.PerkDefinition>());
        }

        // 같은 종류의 시설 정의가 테스트용으로 여럿 있을 수 있어, 실제 새 런 설정이 쓰는 것만 넣는다.
        static List<FacilityDefinition> RunFacilities()
        {
            var result = new List<FacilityDefinition>();
            foreach (RunConfig config in All<RunConfig>())
                foreach (FacilityDefinition facility in config.Facilities)
                    if (facility != null && !result.Exists(known => known.Type == facility.Type)) result.Add(facility);
            return result;
        }

        static List<T> All<T>() where T : Object
        {
            var result = new List<T>();
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { Root });
            System.Array.Sort(guids, (a, b) => string.CompareOrdinal(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));
            foreach (string guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && !result.Contains(asset)) result.Add(asset);
            }
            return result;
        }
    }
}
