using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Siege;
using ProjectNeo.Stages;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>B-7.1 Game 씬 웨이브: 시간표대로 한 번씩 등장, 남은 웨이브 중 승리·후퇴 판정, 실제 스테이지 에셋 연결.</summary>
    public class GameWavePlayTests
    {
        static int cleanup;
        GameBattleController battle;
        ConstructionWorld world;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
            world = Object.FindAnyObjectByType<ConstructionWorld>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SingleWaveEncounterFixture.UseSingleWave();
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameWaveCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        IEnumerator Navigation()
        {
            yield return null;
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            Assert.IsFalse(world.IsRebuilding || world.RebuildPending);
            yield return null;
        }

        static void Kill(CharacterActor actor) => actor.ReceiveDamage(actor.Health.Current + 1f);

        GameObject Prefab(SiegeAttackerRole role) => battle.Settings.Enemies
            .First(group => group.unitPrefab != null && group.unitPrefab.GetComponent<SiegeAttacker>().Role == role).unitPrefab;

        Vector2 LeftCell => battle.Settings.SpawnCell + Vector2.left * 15f;

        void Use(EncounterPlan plan)
        {
            GameBattleController.EncounterSourceOverride = (settings, stage) => plan;
            Assert.IsTrue(battle.RebuildEncounter());
        }

        static EncounterWave Wave(float start, GameObject prefab, int count, Vector2 cell) =>
            new EncounterWave(start, new[] { new EncounterGroup(prefab, count, cell) });

        [UnityTest]
        public IEnumerator 웨이브는_정해진_시간에_한_번씩_나오고_남은_웨이브가_있으면_승리하지_않으며_다음_전투에서_처음부터_다시_나온다()
        {
            Use(new EncounterPlan(new[]
            {
                Wave(0f, Prefab(SiegeAttackerRole.Ladder), 2, battle.Settings.SpawnCell),
                Wave(2f, Prefab(SiegeAttackerRole.Archer), 1, LeftCell)
            }));
            var released = new List<int>();
            battle.WaveReleased += released.Add;
            Assert.AreEqual(3, battle.Enemies.Count);
            Assert.IsTrue(battle.Enemies.All(enemy => !enemy.gameObject.activeSelf));
            Assert.AreEqual(2, battle.WaveCount);
            Assert.AreEqual(0, battle.WavesReleased);
            Assert.AreEqual(3, battle.EnemiesPending);

            Assert.IsTrue(battle.TryStartBattle());
            Assert.AreEqual(1, battle.WavesReleased);
            Assert.IsTrue(battle.Enemies[0].gameObject.activeSelf && battle.Enemies[1].gameObject.activeSelf);
            Assert.IsFalse(battle.Enemies[2].gameObject.activeSelf, "둘째 웨이브는 아직 나오지 않는다.");
            Assert.AreEqual(2, battle.EnemiesOnField);
            Assert.AreEqual(1, battle.EnemiesPending);
            Assert.AreEqual(3, battle.EnemiesAlive);

            yield return null;
            Kill(battle.Enemies[0].Actor); Kill(battle.Enemies[1].Actor);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase, "나올 웨이브가 남아 있으면 전장이 비어도 승리가 아니다.");
            Assert.AreEqual(1, battle.WavesReleased, "전장이 비어도 다음 웨이브를 당겨오지 않는다.");
            Assert.Greater(battle.SecondsUntilNextWave, 0f);

            for (int i = 0; i < 120 && battle.WavesReleased < 2; i++) yield return null;
            Assert.AreEqual(2, battle.WavesReleased);
            SiegeAttacker archer = battle.Enemies[2];
            Assert.IsTrue(archer.gameObject.activeSelf);
            Vector3 left = world.transform.TransformPoint(new Vector3(LeftCell.x * world.Grid.CellSize, 0f, LeftCell.y * world.Grid.CellSize));
            Assert.Less(Mathf.Abs(archer.transform.position.x - left.x), 3f, "지정한 스폰 지점에서 나온다.");
            Assert.AreEqual(0, battle.EnemiesPending);
            Assert.AreEqual(-1f, battle.SecondsUntilNextWave);
            for (int i = 0; i < 30; i++) yield return null;
            CollectionAssert.AreEqual(new[] { 0, 1 }, released, "각 웨이브는 한 번만 나온다.");
            Assert.AreEqual(3, battle.Enemies.Count);

            Kill(archer.Actor);
            yield return null; yield return null;
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);

            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return Navigation();
            Assert.AreEqual(3, battle.Enemies.Count, "다음 준비 단계에 적이 겹쳐 만들어지지 않는다.");
            Assert.IsTrue(battle.Enemies.All(enemy => !enemy.gameObject.activeSelf));
            Assert.AreEqual(0, battle.WavesReleased);
            Assert.IsTrue(battle.TryStartBattle());
            Assert.AreEqual(1, battle.WavesReleased);
            Assert.AreEqual(1, battle.EnemiesPending);
            CollectionAssert.AreEqual(new[] { 0, 1, 0 }, released);
        }

        [UnityTest]
        public IEnumerator 나올_웨이브가_남아_있으면_궁수만_남아도_퇴각하지_않고_마지막_웨이브_뒤에_퇴각한다()
        {
            GameObject archer = Prefab(SiegeAttackerRole.Archer);
            Use(new EncounterPlan(new[]
            {
                Wave(0f, archer, 1, battle.Settings.SpawnCell), Wave(2f, archer, 1, LeftCell)
            }));
            Assert.IsFalse(battle.Settings.RetreatWhileWavesPending, "기본값은 웨이브 중 후퇴하지 않음이다.");
            Assert.IsTrue(battle.TryStartBattle());
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                Assert.IsFalse(battle.IsRetreating, "웨이브가 남아 있는 동안에는 후퇴하지 않는다.");
            }
            Assert.AreEqual(1, battle.WavesReleased);
            for (int i = 0; i < 120 && battle.WavesReleased < 2; i++) yield return null;
            Assert.AreEqual(2, battle.WavesReleased);
            for (int i = 0; i < 60 && !battle.IsRetreating; i++) yield return null;
            Assert.IsTrue(battle.IsRetreating, "모든 웨이브가 나온 뒤에는 기존 후퇴 규칙을 따른다.");
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase);
        }

        [UnityTest]
        public IEnumerator 설정을_켜면_웨이브가_남아_있어도_궁수가_퇴각한다()
        {
            GameObject archer = Prefab(SiegeAttackerRole.Archer);
            Use(new EncounterPlan(new[]
            {
                Wave(0f, archer, 1, battle.Settings.SpawnCell), Wave(20f, archer, 1, LeftCell)
            }));
            StageCatalog catalog = battle.Settings.Stages;
            battle.Settings.ConfigureStages(catalog, true);
            try
            {
                Assert.IsTrue(battle.TryStartBattle());
                for (int i = 0; i < 40 && !battle.IsRetreating; i++) yield return null;
                Assert.IsTrue(battle.IsRetreating);
                Assert.AreEqual(1, battle.WavesReleased);
                Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase, "남은 웨이브가 있으므로 전투는 계속된다.");
            }
            finally { battle.Settings.ConfigureStages(catalog, false); }
        }

        [UnityTest]
        public IEnumerator 실제_스테이지_에셋의_1_스테이지_편성대로_만들고_첫_웨이브만_전투_시작에_나온다()
        {
            GameBattleController.EncounterSourceOverride = null;
            Assert.IsTrue(battle.RebuildEncounter());
            StageCatalog catalog = battle.Settings.Stages;
            Assert.IsNotNull(catalog, "Battle.asset에 스테이지 목록이 연결되어 있어야 한다(ProjectNeo/Setup/Create Stage Data).");
            Assert.AreEqual(3, catalog.Count);
            EncounterPlan expected = EncounterPlan.FromStage(catalog.StageFor(1), battle.Settings.SpawnCell);
            Assert.Greater(expected.Waves.Count, 1);
            Assert.AreEqual(expected.Waves.Count, battle.WaveCount);
            Assert.AreEqual(expected.TotalUnits, battle.Enemies.Count);
            Assert.AreNotEqual(GameBattleController.DefaultEncounter(battle.Settings, 2).TotalUnits, 0);

            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            int first = expected.Waves[0].UnitCount;
            Assert.AreEqual(1, battle.WavesReleased);
            Assert.AreEqual(first, battle.Enemies.Count(enemy => enemy.gameObject.activeSelf));
            Assert.AreEqual(expected.TotalUnits - first, battle.EnemiesPending);
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase);
        }
    }
}
