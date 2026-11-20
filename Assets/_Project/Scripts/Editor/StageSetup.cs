using ProjectNeo.Flow;
using ProjectNeo.Stages;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 스테이지·웨이브 데이터(B-7.1, CONTENT-01). 스폰 지점 3개, 스테이지 7개, 스테이지 목록을 만들고 Game 씬의 전투 설정(Battle.asset)에 넣는다.
    /// 씬 파일은 바꾸지 않는다. 이미 있는 에셋은 그대로 두고, 목록에 빠진 스테이지만 뒤에 붙이므로 여러 번 실행해도 같다.
    /// 편성 숫자는 조정용 임시값이며, 만든 뒤에는 각 에셋의 인스펙터에서 바꾼다.
    /// 각 스테이지의 마지막 웨이브는 보스(SIEGE-09)가 들어갈 자리다.
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

            // 1~3: B-7.1 편성(2026-11-16 승인).
            StageDefinition stage1 = Stage(1,
                Wave(0f, Group(ladder, 4, mid), Group(archer, 2, mid)),
                Wave(40f, Group(ladder, 2, left), Group(gate, 2, mid), Group(archer, 2, right)));
            StageDefinition stage2 = Stage(2,
                Wave(0f, Group(ladder, 4, left), Group(ladder, 4, right), Group(archer, 2, mid)),
                Wave(40f, Group(gate, 2, mid), Group(wall, 1, left), Group(archer, 2, right)),
                Wave(80f, Group(ladder, 2, mid), Group(wall, 1, right)));
            StageDefinition stage3 = Stage(3,
                Wave(0f, Group(ladder, 6, mid), Group(archer, 3, left), Group(archer, 3, right)),
                Wave(35f, Group(gate, 3, mid), Group(wall, 2, left), Group(flyer, 2, right)),
                Wave(70f, Group(ladder, 4, left), Group(flyer, 2, mid), Group(highFlyer, 2, right)));

            // 4~7: CONTENT-01 편성(2026-11-19 승인). 적 수 32 → 38 → 44 → 50, 웨이브 간격 35초 → 30초.
            StageDefinition stage4 = Stage(4,   // 양쪽 동시 압박
                Wave(0f, Group(ladder, 6, left), Group(ladder, 6, right), Group(archer, 3, mid)),
                Wave(35f, Group(gate, 3, mid), Group(wall, 2, left), Group(archer, 3, right)),
                Wave(70f, Group(flyer, 3, mid), Group(highFlyer, 2, right), Group(ladder, 4, left, hidden: true)));
            StageDefinition stage5 = Stage(5,   // 성벽 압박
                Wave(0f, Group(ladder, 8, mid), Group(archer, 4, left), Group(archer, 4, right)),
                Wave(30f, Group(wall, 3, left), Group(wall, 3, right), Group(archer, 4, mid)),
                Wave(65f, Group(gate, 3, mid, hidden: true), Group(flyer, 4, left), Group(highFlyer, 2, right), Group(ladder, 3, right)));
            StageDefinition stage6 = Stage(6,   // 공중 압박
                Wave(0f, Group(ladder, 8, left), Group(ladder, 8, right), Group(archer, 4, mid)),
                Wave(30f, Group(flyer, 4, left), Group(flyer, 4, right), Group(highFlyer, 3, mid, hidden: true)),
                Wave(60f, Group(gate, 3, mid), Group(wall, 3, left), Group(archer, 4, right), Group(highFlyer, 3, right)));
            StageDefinition stage7 = Stage(7,   // 모든 종류
                Wave(0f, Group(ladder, 8, mid), Group(ladder, 6, left), Group(ladder, 6, right), Group(archer, 4, left)),
                Wave(30f, Group(gate, 3, mid), Group(wall, 3, left), Group(wall, 3, right), Group(archer, 4, right, hidden: true)),
                Wave(60f, Group(flyer, 4, mid), Group(highFlyer, 3, left), Group(highFlyer, 3, right), Group(ladder, 3, mid)));

            var ordered = new[] { stage1, stage2, stage3, stage4, stage5, stage6, stage7 };
            StageCatalog catalog = CombatPrototypeSetup.Save(StageCatalog.Create(ordered), CatalogPath);
            AppendMissing(catalog, ordered);

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

        /// <summary>이미 있는 목록에서 빠진 스테이지만 순서대로 뒤에 붙인다. 이미 있는 줄과 그 순서는 건드리지 않는다.</summary>
        static void AppendMissing(StageCatalog catalog, StageDefinition[] ordered)
        {
            var data = new SerializedObject(catalog);
            SerializedProperty list = data.FindProperty("stages");
            bool changed = false;
            foreach (StageDefinition stage in ordered)
            {
                bool listed = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == stage) { listed = true; break; }
                if (listed) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = stage;
                changed = true;
            }
            if (!changed) return;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        static StageDefinition Stage(int number, params StageWave[] waves) =>
            CombatPrototypeSetup.Save(StageDefinition.Create("Stage " + number, waves), Folder + "/Stage" + number.ToString("00") + ".asset");

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

        static StageWaveGroup Group(GameObject prefab, int count, SpawnPointDefinition point, bool hidden = false) =>
            new StageWaveGroup { enemyPrefab = prefab, count = count, spawnPoint = point, hidden = hidden };
    }
}
