using ProjectNeo.Economy;
using ProjectNeo.Stages;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 임시 스테이지 보스 7종(SIEGE-09, 2026-11-20 사용자 승인)을 만들고 각 스테이지의 마지막 웨이브에 넣는다.
    /// 씬 파일은 바꾸지 않는다. 이미 있는 보스 에셋과 이미 보스가 들어간 웨이브는 그대로 두므로 여러 번 실행해도 같다.
    /// 수치는 조정용 임시값이며, 만든 뒤에는 각 보스 에셋의 인스펙터에서 바꾼다.
    /// </summary>
    public static class BossSetup
    {
        public const string Folder = StageSetup.Folder + "/Bosses";
        const string Materials = "Assets/_Project/Data/GameFlow/Rewards/";

        [MenuItem("ProjectNeo/Setup/Create Stage Bosses (SIEGE-09)")]
        public static void Apply()
        {
            CombatPrototypeSetup.EnsureFolder(Folder);
            // 바탕 적은 적 종류(CONTENT-04)로 정한다. 없으면 먼저 만든다.
            EnemySetup.CreateData();
            EnemyDefinition ladder = EnemySetup.Load("enemy_ladder"), gate = EnemySetup.Load("enemy_gate_breaker"), archer = EnemySetup.Load("enemy_archer");
            EnemyDefinition wall = EnemySetup.Load("enemy_wall_breaker"), flyer = EnemySetup.Load("enemy_flyer");
            MaterialDefinition bone = Material("mat_bone"), hide = Material("mat_hide"), feather = Material("mat_feather");

            BossDefinition[] bosses =
            {
                Save(BossDefinition.Create("boss_gate_crusher", "성문 분쇄자", BossRole.Siege, gate, 1.5f, 400f, 21f, 4f,
                    structureDamageMultiplier: 2f, rewardGold: 30, rewardMaterials: Reward(hide, 2))),
                Save(BossDefinition.Create("boss_duelist", "결투가", BossRole.Duelist, ladder, 1.4f, 450f, 28f, 4f,
                    moveSpeedMultiplier: 1.3f, rewardGold: 40, rewardMaterials: Reward(bone, 2)).WithHeroPreference(3f)),
                Save(BossDefinition.Create("boss_marksman", "명사수", BossRole.Artillery, archer, 1.4f, 350f, 26f, 2f,
                    attackRange: ArcherRangeMigration.MarksmanRange, rewardGold: 50, rewardMaterials: new[] { Pair(bone, 2), Pair(hide, 1) })
                    .WithAimSpread(ArcherRangeMigration.MarksmanSpread)),
                Save(BossDefinition.Create("boss_war_captain", "전쟁 대장", BossRole.Commander, ladder, 1.6f, 600f, 21f, 5f,
                    rewardGold: 60, rewardMaterials: Reward(hide, 3)).WithAura(12f, 0.2f, 2f, 0.2f)),
                Save(BossDefinition.Create("boss_siege_giant", "공성 거인", BossRole.Siege, wall, 1.9f, 900f, 28f, 6f,
                    moveSpeedMultiplier: 0.8f, structureDamageMultiplier: 3f, rewardGold: 70,
                    rewardMaterials: new[] { Pair(bone, 3), Pair(hide, 2) })),
                Save(BossDefinition.Create("boss_sky_hunter", "하늘 사냥꾼", BossRole.Duelist, flyer, 1.5f, 600f, 28f, 4f,
                    rewardGold: 80, rewardMaterials: Reward(feather, 4)).WithHeroPreference(3f)),
                Save(BossDefinition.Create("boss_warlord", "군벌", BossRole.Commander, gate, 1.8f, 1200f, 28f, 6f,
                    rewardGold: 90, rewardMaterials: new[] { Pair(bone, 3), Pair(hide, 3), Pair(feather, 2) })
                    .WithAura(15f, 0.3f, 3f, 0.3f)),
            };

            var center = AssetDatabase.LoadAssetAtPath<SpawnPointDefinition>(StageSetup.Folder + "/SpawnCenter.asset");
            for (int i = 0; i < bosses.Length; i++)
                AssignToLastWave(StageSetup.Folder + "/Stage" + (i + 1).ToString("00") + ".asset", bosses[i], center);
            AssetDatabase.SaveAssets();
        }

        // 스테이지의 마지막 웨이브가 비어 있을 때만 보스를 넣는다. 이미 다른 보스가 있으면 사람이 바꾼 것으로 보고 그대로 둔다.
        static void AssignToLastWave(string stagePath, BossDefinition boss, SpawnPointDefinition point)
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
            if (stage == null) throw new System.InvalidOperationException("Missing stage (run Create Stage Data first): " + stagePath);
            var data = new SerializedObject(stage);
            SerializedProperty waves = data.FindProperty("waves");
            if (waves.arraySize == 0) throw new System.InvalidOperationException("Stage has no waves: " + stagePath);
            SerializedProperty last = waves.GetArrayElementAtIndex(waves.arraySize - 1);
            SerializedProperty field = last.FindPropertyRelative("boss");
            if (field.objectReferenceValue != null) return;
            field.objectReferenceValue = boss;
            last.FindPropertyRelative("bossSpawnPoint").objectReferenceValue = point;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
        }

        static BossDefinition Save(BossDefinition boss) => CombatPrototypeSetup.Save(boss, Folder + "/" + boss.Id + ".asset");

        static BossMaterialReward[] Reward(MaterialDefinition material, int amount) => new[] { Pair(material, amount) };
        static BossMaterialReward Pair(MaterialDefinition material, int amount) => new BossMaterialReward(material, amount);

        static MaterialDefinition Material(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<MaterialDefinition>(Materials + name + ".asset");
            if (material == null) throw new System.InvalidOperationException("Missing material: " + name);
            return material;
        }
    }
}
