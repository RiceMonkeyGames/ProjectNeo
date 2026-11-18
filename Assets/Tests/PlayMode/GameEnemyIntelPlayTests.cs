using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Siege;
using ProjectNeo.Stages;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>C-5 Game 씬 적 목록: 준비 단계 미리보기(숨긴 무리 "? × 수"), 전투 중 실시간 남은 수, 등장 시 공개.</summary>
    public class GameEnemyIntelPlayTests
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
            Scene empty = SceneManager.CreateScene("GameEnemyIntelCleanup" + cleanup++);
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

        static Rect ScreenRect(Transform target)
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static void Kill(CharacterActor actor) => actor.ReceiveDamage(actor.Health.Current + 1f);

        GameObject Prefab(SiegeAttackerRole role) => battle.Settings.Enemies
            .First(group => group.unitPrefab != null && group.unitPrefab.GetComponent<SiegeAttacker>().Role == role).unitPrefab;

        [UnityTest]
        public IEnumerator 준비_단계에는_숨긴_무리가_물음표로_보이고_전투_중에는_남은_수가_줄며_나온_무리는_공개된다()
        {
            GameObject ladder = Prefab(SiegeAttackerRole.Ladder), archer = Prefab(SiegeAttackerRole.Archer);
            string ladderName = EnemyIntel.DisplayName(ladder.name), archerName = EnemyIntel.DisplayName(archer.name);
            var plan = new EncounterPlan(new[]
            {
                new EncounterWave(0f, new[] { new EncounterGroup(ladder, 2, battle.Settings.SpawnCell) }),
                new EncounterWave(2f, new[] { new EncounterGroup(archer, 1, battle.Settings.SpawnCell, hidden: true) })
            });
            GameBattleController.EncounterSourceOverride = (settings, stage) => plan;
            Assert.IsTrue(battle.RebuildEncounter());

            Assert.AreEqual($"{ladderName} × 2\n? × 1", battle.Intel.Describe());
            Assert.IsTrue(battle.Intel.AnyHidden);

            var hud = Object.FindAnyObjectByType<EnemyIntelHud>();
            Assert.IsNotNull(hud, "Game 씬에 적 목록 창이 있어야 한다(ProjectNeo/Setup/Add Enemy Intel HUD).");
            yield return null;
            Assert.IsFalse(hud.IsShowing, "준비 단계 창은 키를 누르기 전에는 닫혀 있다.");
            hud.TogglePreview();
            yield return null;
            Assert.IsTrue(hud.IsShowing);
            Assert.AreEqual(EnemyIntelHud.PreviewTitle, hud.Title);
            Assert.AreEqual($"{ladderName} × 2\n? × 1", hud.Body);
            Rect intelArea = ScreenRect(hud.transform.Find("EnemyIntel"));
            foreach (string name in new[] { "SquadsBacking", "BattleStatus" })
            {
                RectTransform other = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).FirstOrDefault(r => r.name == name);
                Assert.IsNotNull(other, name);
                Assert.IsFalse(intelArea.Overlaps(ScreenRect(other)), "적 목록 창이 " + name + "와 겹치지 않는다.");
            }
            hud.TogglePreview();
            yield return null;
            Assert.IsFalse(hud.IsShowing);

            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Assert.IsTrue(hud.IsShowing, "전투 중에는 창을 닫아 두어도 보인다.");
            Assert.AreEqual($"{EnemyIntelHud.BattleTitle} 3", hud.Title);
            Assert.IsTrue(battle.Intel.AnyHidden, "둘째 웨이브는 아직 나오지 않았다.");

            Kill(battle.Enemies[0].Actor);
            yield return null; yield return null;
            Assert.AreEqual($"{ladderName} × 1\n? × 1", battle.Intel.Describe(), "죽은 적은 바로 빠진다.");
            Assert.AreEqual($"{EnemyIntelHud.BattleTitle} 2", hud.Title);
            Assert.AreEqual($"{ladderName} × 1\n? × 1", hud.Body);

            for (int i = 0; i < 120 && battle.WavesReleased < 2; i++) yield return null;
            Assert.AreEqual(2, battle.WavesReleased);
            yield return null;
            Assert.IsFalse(battle.Intel.AnyHidden, "전장에 나온 무리는 공개된다.");
            Assert.AreEqual($"{ladderName} × 1\n{archerName} × 1", hud.Body);

            Kill(battle.Enemies[1].Actor); Kill(battle.Enemies[2].Actor);
            yield return null; yield return null;
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
            Assert.IsFalse(hud.IsShowing, "보상 화면에서는 보이지 않는다.");

            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return Navigation();
            Assert.IsTrue(battle.Intel.AnyHidden, "다음 준비 단계에는 다시 숨겨진다.");
            Assert.AreEqual($"{ladderName} × 2\n? × 1", battle.Intel.Describe());
        }
    }
}
