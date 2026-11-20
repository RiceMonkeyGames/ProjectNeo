using NUnit.Framework;
using ProjectNeo.Flow;
using ProjectNeo.Stages;

namespace ProjectNeo.Tests
{
    /// <summary>실제 스테이지 데이터(CONTENT-01)가 런 규칙에 맞는지 본다. 편성 숫자 자체는 조정용이라 검사하지 않는다.</summary>
    public class StageCatalogDataTests
    {
        const string CatalogPath = "Assets/_Project/Data/GameFlow/Stages/StageCatalog.asset";
        const int RunStages = 7; // Q-67

        static StageCatalog LoadCatalog()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "Run ProjectNeo/Setup/Create Stage Data (B-7.1).");
            return catalog;
        }

        [Test]
        public void ProjectCatalog_HoldsSevenStagesInOrder_AndBattleSettingsUseIt()
        {
            StageCatalog catalog = LoadCatalog();
            Assert.AreEqual(RunStages, catalog.Count);
            for (int number = 1; number <= RunStages; number++)
            {
                StageDefinition stage = catalog.StageFor(number);
                Assert.IsNotNull(stage, "stage " + number);
                Assert.AreEqual("Stage " + number, stage.DisplayName, "stage " + number + " must sit at row " + number);
            }
            var battle = UnityEditor.AssetDatabase.LoadAssetAtPath<GameBattleSettings>("Assets/_Project/Data/GameFlow/Battle.asset");
            Assert.AreSame(catalog, battle.Stages, "the Game scene battle settings must point at the catalog");
        }

        [Test]
        public void ProjectStages_HaveValidTimetables_AndCompleteGroups()
        {
            StageCatalog catalog = LoadCatalog();
            foreach (StageDefinition stage in catalog.Stages)
            {
                string at = stage.DisplayName + ": ";
                Assert.GreaterOrEqual(stage.Waves.Count, 2, at + "each battle has 2-3 waves (Q-51)");
                Assert.LessOrEqual(stage.Waves.Count, 3, at + "each battle has 2-3 waves (Q-51)");
                Assert.AreEqual(0f, stage.Waves[0].startSeconds, at + "the first wave starts with the battle");
                for (int w = 0; w < stage.Waves.Count; w++)
                {
                    StageWave wave = stage.Waves[w];
                    if (w > 0) Assert.Greater(wave.startSeconds, stage.Waves[w - 1].startSeconds, at + "wave start times rise");
                    Assert.IsNotEmpty(wave.groups, at + "wave " + (w + 1) + " is empty");
                    foreach (StageWaveGroup group in wave.groups)
                    {
                        Assert.IsNotNull(group.enemyPrefab, at + "wave " + (w + 1) + " group without an enemy prefab");
                        Assert.Greater(group.count, 0, at + "wave " + (w + 1) + " group without enemies");
                        Assert.IsNotNull(group.spawnPoint, at + "wave " + (w + 1) + " group without a spawn point");
                    }
                }
            }
        }
    }
}
