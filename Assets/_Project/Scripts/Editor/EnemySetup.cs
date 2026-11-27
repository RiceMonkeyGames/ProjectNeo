using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Stages;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 일반 적 종류 6개와 적 종류 목록(CONTENT-04)을 만들고 전투 설정·보상 표에 연결한다. 스테이지 웨이브에서 프리팹만 있는 무리는
    /// 같은 프리팹의 적 종류로 바꾼다. 씬 파일은 바꾸지 않는다. 이미 있는 에셋과 이미 채운 칸은 그대로 두므로 여러 번 실행해도 같다.
    /// 처치 골드·재료는 C-4 보상 표의 값(2026-11-12)을 그대로 옮긴 것이며, 만든 뒤에는 각 적 에셋의 인스펙터에서 바꾼다.
    /// </summary>
    public static class EnemySetup
    {
        public const string Folder = "Assets/_Project/Data/GameFlow/Enemies";
        public const string CatalogPath = Folder + "/EnemyCatalog.asset";
        const string Prefabs = "Assets/_Project/Prefabs/SiegePrototype/";
        const string BattlePath = "Assets/_Project/Data/GameFlow/Battle.asset";
        const string RewardTablePath = "Assets/_Project/Data/GameFlow/Rewards/StageRewards.asset";

        [MenuItem("ProjectNeo/Setup/Create Enemy Definitions (CONTENT-04)")]
        public static void Apply()
        {
            EnemyCatalog catalog = CreateData();
            var battle = AssetDatabase.LoadAssetAtPath<GameBattleSettings>(BattlePath);
            if (battle != null) AssignIfEmpty(battle, "enemyCatalog", catalog);
            var table = AssetDatabase.LoadAssetAtPath<StageRewardTable>(RewardTablePath);
            if (table != null) AssignIfEmpty(table, "enemies", catalog);
            foreach (string guid in AssetDatabase.FindAssets("t:StageDefinition", new[] { StageSetup.Folder }))
                LinkStageGroups(AssetDatabase.LoadAssetAtPath<StageDefinition>(AssetDatabase.GUIDToAssetPath(guid)), catalog);
            AssetDatabase.SaveAssets();
        }

        /// <summary>적 종류와 목록을 만들거나 기존 것을 불러온다. 목록에 빠진 적 종류는 뒤에 붙인다.</summary>
        public static EnemyCatalog CreateData()
        {
            CombatPrototypeSetup.EnsureFolder(Folder);
            MaterialDefinition bone = GameRewardSetup.Material("mat_bone", "뼈 조각");
            MaterialDefinition hide = GameRewardSetup.Material("mat_hide", "질긴 가죽");
            MaterialDefinition feather = GameRewardSetup.Material("mat_feather", "깃털");

            EnemyDefinition[] ordered =
            {
                Enemy("enemy_ladder", "사다리병", EnemyRole.GroundLadder, 3, new EnemyMaterialDrop(bone, 1)),
                Enemy("enemy_gate_breaker", "성문 파괴병", EnemyRole.GateBreaker, 5, new EnemyMaterialDrop(hide, 2)),
                Enemy("enemy_archer", "궁수", EnemyRole.Ranged, 3, new EnemyMaterialDrop(feather, 1)),
                Enemy("enemy_wall_breaker", "성벽 파괴병", EnemyRole.WallBreaker, 0),
                Enemy("enemy_flyer", "비행병", EnemyRole.Flyer, 0),
                Enemy("enemy_flyer_high_archer", "고공 비행 궁수", EnemyRole.FlyingRanged, 0),
            };
            EnemyCatalog catalog = CombatPrototypeSetup.Save(EnemyCatalog.Create(ordered), CatalogPath);
            AppendMissing(catalog, ordered);
            return catalog;
        }

        /// <summary>id(=프리팹 이름)의 적 종류를 불러온다. 없으면 예외를 던진다(먼저 CreateData를 부른다).</summary>
        public static EnemyDefinition Load(string id)
        {
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Folder + "/" + id + ".asset");
            if (enemy == null) throw new System.InvalidOperationException("Missing enemy definition (run Create Enemy Definitions first): " + id);
            return enemy;
        }

        static EnemyDefinition Enemy(string id, string displayName, EnemyRole role, int killGold, params EnemyMaterialDrop[] drops)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + id + ".prefab");
            if (prefab == null) throw new System.InvalidOperationException("Missing enemy prefab: " + id);
            return CombatPrototypeSetup.Save(EnemyDefinition.Create(id, displayName, role, prefab, killGold, drops),
                Folder + "/" + id + ".asset");
        }

        static void AppendMissing(EnemyCatalog catalog, EnemyDefinition[] ordered)
        {
            var data = new SerializedObject(catalog);
            SerializedProperty list = data.FindProperty("enemies");
            bool changed = false;
            foreach (EnemyDefinition enemy in ordered)
            {
                bool listed = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == enemy) { listed = true; break; }
                if (listed) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = enemy;
                changed = true;
            }
            if (!changed) return;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        static void AssignIfEmpty(Object target, string field, Object value)
        {
            var data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(field);
            if (property == null || property.objectReferenceValue != null) return;
            property.objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        // 적 종류가 비어 있고 예비 프리팹만 있는 무리를 같은 프리팹의 적 종류로 바꾸고 예비 프리팹을 비운다.
        // 목록에 없는 프리팹은 그대로 둔다.
        static void LinkStageGroups(StageDefinition stage, EnemyCatalog catalog)
        {
            if (stage == null) return;
            var data = new SerializedObject(stage);
            SerializedProperty waves = data.FindProperty("waves");
            bool changed = false;
            for (int w = 0; w < waves.arraySize; w++)
            {
                SerializedProperty groups = waves.GetArrayElementAtIndex(w).FindPropertyRelative("groups");
                for (int g = 0; g < groups.arraySize; g++)
                {
                    SerializedProperty group = groups.GetArrayElementAtIndex(g);
                    SerializedProperty enemy = group.FindPropertyRelative("enemy");
                    SerializedProperty prefab = group.FindPropertyRelative("enemyPrefab");
                    if (enemy.objectReferenceValue != null || prefab.objectReferenceValue == null) continue;
                    EnemyDefinition found = catalog.FindByPrefab((GameObject)prefab.objectReferenceValue);
                    if (found == null) continue;
                    enemy.objectReferenceValue = found;
                    prefab.objectReferenceValue = null;
                    changed = true;
                }
            }
            if (!changed) return;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
        }
    }
}
