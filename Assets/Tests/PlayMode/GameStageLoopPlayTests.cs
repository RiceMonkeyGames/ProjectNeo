using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Siege;
using ProjectNeo.Stages;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// B-9.3: 실제 스테이지 에셋으로 1→2→3 스테이지를 이어서 치르며, 매 준비 단계마다 지난 전투의 적·웨이브·투사체·사다리·
    /// 사기 상태가 남지 않고, 보상은 전투당 한 번만 지급되는지 본다(B-7.2의 전투 후 정리 확인을 흡수).
    /// </summary>
    public class GameStageLoopPlayTests
    {
        static int cleanup;
        GameBattleController battle;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            GameBattleController.EncounterSourceOverride = null;
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SingleWaveEncounterFixture.UseSingleWave();
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameStageLoopCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        static void Kill(CharacterActor actor) => actor.ReceiveDamage(actor.Health.Current + 1f);

        void AssertCleanPreparation(int stage)
        {
            string at = "스테이지 " + stage + " 준비: ";
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase, at + "단계");
            Assert.AreEqual(stage, GameSession.Flow.Run.StageNumber, at + "스테이지 번호");
            Assert.IsFalse(battle.IsRunning, at + "전투가 멈춰 있어야 한다.");

            EncounterPlan expected = EncounterPlan.FromStage(battle.Settings.Stages.StageFor(stage), battle.Settings.SpawnCell);
            Assert.AreEqual(expected.TotalUnits, battle.Enemies.Count, at + "이번 스테이지 편성만큼만 적이 있어야 한다.");
            Assert.AreEqual(expected.TotalUnits, battle.GetComponentsInChildren<SiegeAttacker>(true).Length,
                at + "지난 전투의 적 오브젝트가 남으면 안 된다.");
            Assert.IsTrue(battle.Enemies.All(enemy => enemy != null && !enemy.gameObject.activeSelf), at + "적은 전투 전까지 숨어 있다.");
            Assert.AreEqual(expected.Waves.Count, battle.WaveCount, at + "웨이브 수");
            Assert.AreEqual(0, battle.WavesReleased, at + "웨이브 시간표가 처음부터 다시 시작해야 한다.");
            Assert.AreEqual(0, battle.RetreatedCount, at + "후퇴 기록");
            Assert.IsFalse(battle.IsRetreating, at + "후퇴 상태");

            Scene scene = battle.gameObject.scene;
            Assert.IsFalse(Object.FindObjectsByType<CombatProjectile>().Any(shot => shot.gameObject.scene == scene), at + "화살이 남으면 안 된다.");
            Assert.IsFalse(Object.FindObjectsByType<CannonShell>().Any(shell => shell.gameObject.scene == scene), at + "대포탄이 남으면 안 된다.");

            // SIEGE-37: 사다리는 사다리 적마다 하나씩 새로 만들어 숨겨 두고, 지난 전투의 사다리는 남지 않는다.
            Assert.AreEqual(battle.Enemies.Count(enemy => enemy.Role == SiegeAttackerRole.Ladder), battle.Ladders.Count,
                at + "사다리 적마다 사다리 하나");
            foreach (SiegeLadder ladder in battle.Ladders)
            {
                Assert.IsFalse(ladder.gameObject.activeSelf, at + "사다리는 전투 전까지 숨어 있다.");
                Assert.IsNull(ladder.Occupant, at + "사다리 점유자");
                Assert.AreEqual(0, ladder.Waiting.Count, at + "사다리 대기열");
                Assert.AreEqual(0, ladder.Crowd, at + "사다리 주변 인원");
            }
            Assert.AreEqual(battle.Ladders.Count + 1, Object.FindAnyObjectByType<ProjectNeo.Walls.ConstructionWorld>().GetComponentsInChildren<SiegeLadder>(true).Length,
                at + "지난 전투의 사다리 오브젝트가 남으면 안 된다(원본 하나 + 이번 편성).");
            Assert.IsFalse(battle.Morale.BattleActive, at + "사기 전투 상태가 끝나 있어야 한다.");
        }

        IEnumerator WinCurrentBattle(int stage)
        {
            string at = "스테이지 " + stage + " 전투: ";
            Assert.IsTrue(battle.TryStartBattle(), at + "시작");
            Assert.AreEqual(stage, battle.BattleNumber, at + "전투 번호");
            // 남은 화살이 결과 화면 뒤까지 날아가지 않는지 함께 본다.
            yield return null;
            var first = battle.Enemies.First(enemy => enemy.gameObject.activeSelf);
            var shot = new GameObject("LoopPendingShot").AddComponent<CombatProjectile>();
            shot.transform.position = first.Actor.AimPoint;
            shot.Launch(first.Actor, 1f, Vector3.up * 18f);

            int releasedSeen = 0;
            // 마지막 웨이브는 80초 → 1/30초 프레임 기준 약 2400프레임. 여유를 둔다.
            for (int frame = 0; frame < 4000 && GameSession.Flow.Phase == GamePhase.Battle; frame++)
            {
                Assert.LessOrEqual(battle.WavesReleased, battle.WaveCount, at + "웨이브가 정해진 수보다 많이 나오면 안 된다.");
                Assert.GreaterOrEqual(battle.WavesReleased, releasedSeen, at + "웨이브 수는 줄지 않는다.");
                releasedSeen = battle.WavesReleased;
                foreach (SiegeAttacker enemy in battle.Enemies)
                    if (enemy != null && enemy.gameObject.activeSelf && enemy.Actor.Health.Current > 0f) Kill(enemy.Actor);
                yield return null;
            }
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase, at + "모든 웨이브를 처치하면 보상으로 가야 한다.");
            Assert.AreEqual(BattleOutcome.Victory, GameSession.Flow.LastOutcome, at + "결과");
            Assert.AreEqual(battle.WaveCount, battle.WavesReleased, at + "모든 웨이브가 한 번씩 나왔어야 한다.");
            Assert.AreEqual(0, battle.EnemiesPending, at + "나올 적이 남아 있으면 안 된다.");
            Assert.IsTrue(shot == null, at + "결과 화면에서 투사체가 남으면 안 된다.");
        }

        [UnityTest]
        public IEnumerator 실제_모든_스테이지를_이어서_치러도_지난_전투의_상태가_남지_않고_보상은_한_번씩만_받는다()
        {
            Assert.IsTrue(battle.RebuildEncounter());
            yield return null; // 이전 편성 오브젝트의 Destroy는 프레임 끝에 끝난다.
            StageCatalog catalog = battle.Settings.Stages;
            Assert.IsNotNull(catalog, "Battle.asset에 스테이지 목록이 연결되어 있어야 한다.");
            Assert.AreEqual(7, catalog.Count);
            var wallet = battle.RunLink.State.Wallet;

            for (int stage = 1; stage <= catalog.Count; stage++)
            {
                AssertCleanPreparation(stage);
                yield return WinCurrentBattle(stage);

                int paid = wallet.Gold;
                for (int i = 0; i < 10; i++) yield return null;
                Assert.AreEqual(paid, wallet.Gold, "스테이지 " + stage + ": 보상 화면에 머무는 동안 골드가 다시 지급되면 안 된다.");
                if (stage < catalog.Count)
                {
                    Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
                    Assert.IsFalse(GameSession.Flow.ContinueToNextStage(), "보상은 한 번만 넘길 수 있다.");
                }
                else
                {
                    // 마지막 스테이지의 보상 뒤에는 다음 스테이지가 없고 엔딩으로 간다(FLOW-03).
                    Assert.IsFalse(GameSession.Flow.ContinueToNextStage(), "마지막 스테이지 뒤에는 다음 스테이지가 없다.");
                    Assert.IsTrue(GameSession.Flow.FinishRun());
                    Assert.AreEqual(GamePhase.Ending, GameSession.Flow.Phase);
                }
                yield return null;
                if (stage < catalog.Count) Assert.AreEqual(paid, wallet.Gold, "스테이지 " + stage + ": 다음 준비로 넘어가며 골드가 바뀌면 안 된다.");
            }
        }
    }
}
