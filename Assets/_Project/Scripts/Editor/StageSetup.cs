using ProjectNeo.Flow;
using ProjectNeo.Stages;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// B-7.1 스테이지·웨이브 데이터. 스폰 지점 3개, 스테이지 3개, 스테이지 목록을 만들고 Game 씬의 전투 설정(Battle.asset)에 넣는다.
    /// 씬 파일은 바꾸지 않는다. 이미 있는 에셋과 이미 연결된 목록은 그대로 두므로 여러 번 실행해도 같다.
    /// 편성 숫자는 조정용 임시값이며, 만든 뒤에는 각 에셋의 인스펙터에서 바꾼다.
    /// </summary>
    public static class StageSetup
    {
        public const string Folder = "Assets/_Project/Data/GameFlow/Stages";
        public const string CatalogPath = Folder + "/StageCatalog.asset";
        const string BattlePath = "Assets/_Project/Data/GameFlow/Battle.asset";
        const string Prefabs = "Assets/_Project/Prefabs/SiegePrototype/";
        // 가운데 스폰 칸에서 좌우 지점까지의 거리(칸).
        const float SideOffsetCells = 15f;

        [MenuItem("ProjectNeo/Setup/Create Stage Data (B-7.1)")]
        public static void Apply()
        {
            var battle = AssetDatabase.LoadAssetAtPath<GameBattleSettings>(BattlePath);
            if (battle == null) throw new System.InvalidOperationException("Battle settings are missing: " + BattlePath);
            CombatPrototypeSetup.EnsureFolder(Folder);

            Vector2 center = battle.SpawnCell;
            SpawnPointDefinition left = Point("Left", "SpawnLeft", center + Vector2.left * SideOffsetCells);
            SpawnPointDefinition mid = Point("Center", "SpawnCenter", center);
            SpawnPointDefinition right = Point("Right", "SpawnRight", center + Vector2.right * SideOffsetCells);

            GameObject ladder = Enemy("enemy_ladder"), gate = Enemy("enemy_gate_breaker"), archer = Enemy("enemy_archer");
            GameObject wall = Enemy("enemy_wall_breaker"), flyer = Enemy("enemy_flyer"), highFlyer = Enemy("enemy_flyer_high_archer");

            StageDefinition stage1 = CombatPrototypeSetup.Save(StageDefinition.Create("Stage 1",
                Wave(0f, Group(ladder, 4, mid), Group(archer, 2, mid)),
                Wave(40f, Group(ladder, 2, left), Group(gate, 2, mid), Group(archer, 2, right))), Folder + "/Stage01.asset");
            StageDefinition stage2 = CombatPrototypeSetup.Save(StageDefinition.Create("Stage 2",
                Wave(0f, Group(ladder, 4, left), Group(ladder, 4, right), Group(archer, 2, mid)),
                Wave(40f, Group(gate, 2, mid), Group(wall, 1, left), Group(archer, 2, right)),
                Wave(80f, Group(ladder, 2, mid), Group(wall, 1, right))), Folder + "/Stage02.asset");
            StageDefinition stage3 = CombatPrototypeSetup.Save(StageDefinition.Create("Stage 3",
                Wave(0f, Group(ladder, 6, mid), Group(archer, 3, left), Group(archer, 3, right)),
                Wave(35f, Group(gate, 3, mid), Group(wall, 2, left), Group(flyer, 2, right)),
                Wave(70f, Group(ladder, 4, left), Group(flyer, 2, mid), Group(highFlyer, 2, right))), Folder + "/Stage03.asset");
            StageCatalog catalog = CombatPrototypeSetup.Save(StageCatalog.Create(stage1, stage2, stage3), CatalogPath);

            var data = new SerializedObject(battle);
            SerializedProperty field = data.FindProperty("stages");
            if (field.objectReferenceValue == null)
            {
                field.objectReferenceValue = catalog;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(battle);
            }
            AssetDatabase.SaveAssets();
        }

        static SpawnPointDefinition Point(string displayName, string file, Vector2 cell) =>
            CombatPrototypeSetup.Save(SpawnPointDefinition.Create(displayName, cell), Folder + "/" + file + ".asset");

        static GameObject Enemy(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab");
            if (prefab == null) throw new System.InvalidOperationException("Missing enemy prefab: " + name);
            return prefab;
        }

        static StageWave Wave(float startSeconds, params StageWaveGroup[] groups) =>
            new StageWave { startSeconds = startSeconds, groups = groups };

        static StageWaveGroup Group(GameObject prefab, int count, SpawnPointDefinition point) =>
            new StageWaveGroup { enemyPrefab = prefab, count = count, spawnPoint = point };
    }
}
