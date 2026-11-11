using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using ProjectNeo.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    public class GameBattlePlayTests
    {
        static int cleanup;
        GameBattleController battle;
        ConstructionWorld world;
        CharacterActor player;
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
            player = Object.FindAnyObjectByType<PlayerInputController>().GetComponent<CharacterActor>();
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameBattleCleanup" + cleanup++);
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
        static Rect Bounds(Transform target)
        {
            var corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [UnityTest]
        public IEnumerator 전투_HUD의_부대_단계_상태_조작_영역은_겹치지_않고_건설_안내도_도구를_가리지_않는다()
        {
            Assert.IsTrue(battle.TryStartBattle());
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var hud = Object.FindAnyObjectByType<GameFlowHud>();
            var command = Object.FindAnyObjectByType<SquadCommandHud>();
            Assert.IsFalse(hud.transform.Find("InteractionPrompt").gameObject.activeSelf);
            Assert.IsTrue(hud.transform.Find("BattleStatus").gameObject.activeSelf);
            Transform[] panels = { hud.transform.Find("Panel"), hud.transform.Find("BattleStatus"),
                command.transform.Find("SquadsBacking"), command.transform.Find("HelpBacking") };
            foreach (string name in new[] { "Squads", "Help" })
            {
                Rect backing = Bounds(command.transform.Find(name + "Backing"));
                Rect label = Bounds(command.transform.Find(name));
                Assert.IsTrue(backing.Contains(label.min) && backing.Contains(label.max), name + " 배경이 글자를 감싸야 한다.");
            }
            for (int a = 0; a < panels.Length; a++)
                for (int b = a + 1; b < panels.Length; b++)
                    Assert.IsFalse(Bounds(panels[a]).Overlaps(Bounds(panels[b])), panels[a].name + " / " + panels[b].name);
            GameSession.Flow.FinishBattle(BattleOutcome.Victory);
            GameSession.Flow.ContinueToNextStage();
            yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<GamePhaseView>().EnterConstruction());
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var construction = Object.FindAnyObjectByType<ConstructionHud>();
            Assert.IsFalse(Bounds(hud.transform.Find("InteractionPrompt")).Overlaps(Bounds(construction.transform.Find("BuildPanel"))));
        }

        [UnityTest]
        public IEnumerator 성문_앞_F_대상에서_시작하면_먼_들판의_적이_활성화되고_성을_향해_온다()
        {
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
            Assert.Greater(battle.Enemies.Count, 0);
            Assert.IsTrue(battle.Enemies.All(enemy => !enemy.gameObject.activeSelf));
            var station = Object.FindAnyObjectByType<BattleStartStation>();
            var interaction = Object.FindAnyObjectByType<PreparationInteraction>();
            var bounds = station.GetComponent<BoxCollider>().bounds;
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            player.transform.position = new Vector3(bounds.center.x, 0f, bounds.min.z - 2f);
            motor.enabled = true;
            yield return null;
            Cursor.lockState = CursorLockMode.Locked;
            var camera = Object.FindAnyObjectByType<FirstPersonCamera>();
            camera.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Physics.SyncTransforms();
            interaction.RefreshTarget();
            Assert.AreSame(station, interaction.Target);
            Assert.IsTrue(interaction.TryInteract());
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase);
            Assert.IsTrue(battle.IsRunning);
            Assert.IsTrue(battle.Enemies.All(enemy => enemy.gameObject.activeSelf));
            Assert.IsFalse(station.Interact(), "중복 전투 시작을 막는다.");
            Assert.IsFalse(Object.FindAnyObjectByType<GamePhaseView>().EnterConstruction());
            Vector3 start = battle.Enemies[0].transform.position;
            Assert.Greater(start.z - world.transform.TransformPoint(new Vector3(120f, 0f, 180f)).z, 70f);
            for (int i = 0; i < 120; i++) yield return null;
            Assert.Less(battle.Enemies[0].transform.position.z, start.z - 1f, "적이 먼 들판에서 성으로 접근해야 한다.");
        }

        [UnityTest]
        public IEnumerator 실제_적_전멸로_보상에_가고_전투를_멈춘_뒤_같은_성에서_다음_준비를_한다()
        {
            Assert.IsTrue(battle.TryStartBattle());
            var ally = Object.FindAnyObjectByType<SquadCommandCenter>().Squads[0].Members[0];
            var wall = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall);
            wall.ReceiveDamage(12f);
            float health = wall.Health.Current;
            int enemyCount = battle.Enemies.Count;
            var shot = new GameObject("PendingBattleShot").AddComponent<CombatProjectile>();
            shot.transform.position = battle.Enemies[0].Actor.AimPoint;
            shot.Launch(battle.Enemies[0].Actor, 1f, Vector3.up, 100f);
            foreach (var enemy in battle.Enemies) Kill(enemy.Actor);
            yield return null; yield return null;
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
            Assert.AreEqual(BattleOutcome.Victory, GameSession.Flow.LastOutcome);
            Assert.IsFalse(battle.IsRunning || ally.enabled || player.enabled);
            Assert.IsTrue(shot == null, "결과 화면에서 투사체가 계속 날아가지 않아야 한다.");
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return null;
            Assert.AreEqual(2, GameSession.Flow.Run.StageNumber);
            Assert.AreEqual(health, wall.Health.Current, "성벽 피해를 유지한다.");
            Assert.IsTrue(ally.enabled && player.enabled);
            Assert.AreEqual(enemyCount, battle.Enemies.Count);
            Assert.IsTrue(battle.Enemies.All(enemy => !enemy.gameObject.activeSelf));
            Assert.AreEqual(enemyCount, battle.GetComponentsInChildren<SiegeAttackerBrain>(true).Length);
            Assert.IsTrue(battle.TryStartBattle());
            Assert.AreEqual(2, battle.BattleNumber);
        }

        [UnityTest]
        public IEnumerator 플레이어_사망과_적_전멸이_동시에_일어나면_패배하고_타이틀로_돌아갈_수_있다()
        {
            Assert.IsTrue(battle.TryStartBattle());
            foreach (var enemy in battle.Enemies) Kill(enemy.Actor);
            Kill(player);
            yield return null; yield return null;
            Assert.AreEqual(GamePhase.RunResult, GameSession.Flow.Phase);
            Assert.AreEqual(BattleOutcome.Defeat, GameSession.Flow.LastOutcome);
            Assert.IsTrue(GameSession.Flow.ReturnToTitle());
            for (int i = 0; i < 300 && SceneManager.GetActiveScene().name != "Title"; i++) yield return null;
            Assert.AreEqual("Title", SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator 공성병이_없고_진입로가_없으면_남은_궁수가_퇴각하고_승리한다()
        {
            Assert.IsTrue(battle.TryStartBattle());
            var archer = battle.Enemies.Last(enemy => enemy.Role == SiegeAttackerRole.Archer);
            foreach (var enemy in battle.Enemies) if (enemy != archer) Kill(enemy.Actor);
            for (int i = 0; i < 20 && !battle.IsRetreating; i++) yield return null;
            Assert.IsTrue(battle.IsRetreating);
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase, "퇴각 이동 없이 즉시 승리하지 않는다.");
            Vector3 start = archer.transform.position;
            for (int i = 0; i < 900 && GameSession.Flow.Phase == GamePhase.Battle; i++) yield return null;
            Assert.Greater(archer.transform.position.z, start.z + 1f);
            Assert.AreEqual(1, battle.RetreatedCount);
            Assert.IsFalse(archer.Actor.Health.IsDead, "퇴각은 사망과 구분한다.");
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
        }

        [UnityTest]
        public IEnumerator 성문이_열려_진입로가_있으면_궁수만_남아도_퇴각하지_않는다()
        {
            Assert.IsTrue(battle.TryStartBattle());
            var gate = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            gate.ReceiveDamage(gate.Health.Current);
            var archer = battle.Enemies.Last(enemy => enemy.Role == SiegeAttackerRole.Archer);
            foreach (var enemy in battle.Enemies) if (enemy != archer) Kill(enemy.Actor);
            yield return Navigation();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsFalse(battle.IsRetreating);
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase);
        }

        [UnityTest]
        public IEnumerator 사다리는_삼층_성벽_맨_위까지_연결되어_적이_오른다()
        {
            SiegeLadder ladder = battle.Ladders[0];
            var wall = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
            Assert.IsTrue(world.TryPlace(wall, ladder.Cell, 0, out _));
            Assert.IsTrue(world.TryPlace(wall, ladder.Cell, 0, out _));
            yield return Navigation();
            Assert.IsTrue(ladder.HasWall);
            Assert.AreEqual(wall.Height * 3f, ladder.WallHeight);
            Assert.IsTrue(battle.TryStartBattle());
            ladder.State.Tick(0.1f, true, false, false);
            ladder.State.Tick(battle.Settings.Siege.LadderRaiseSeconds, true, false, false);
            var climber = battle.Enemies.First(enemy => enemy.Role == SiegeAttackerRole.Ladder);
            Assert.IsTrue(climber.GetComponent<NavMeshAgent>().Warp(ladder.BasePoint));
            for (int i = 0; i < 600 && climber.transform.position.y < wall.Height * 3f - 0.2f; i++) yield return null;
            Assert.Greater(climber.transform.position.y, wall.Height * 3f - 0.2f);
        }

        [UnityTest]
        public IEnumerator 성문_위에서_내려온_성벽으로_입구가_막히면_성문_파괴병이_계속_공격한다()
        {
            var gate = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            var wall = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
            Assert.IsTrue(world.TryPlace(wall, gate.Placement.Cell, 0, out PlacedStructure left));
            Assert.IsTrue(world.TryPlace(wall, gate.Placement.Cell + Vector2Int.right, 0, out PlacedStructure right));
            yield return Navigation();
            Assert.IsTrue(battle.TryStartBattle());
            var breaker = battle.Enemies.First(enemy => enemy.Role == SiegeAttackerRole.GateBreaker);
            foreach (var enemy in battle.Enemies) if (enemy != breaker) Kill(enemy.Actor);
            Vector3 position = gate.transform.position + Vector3.forward * 3f;
            gate.ReceiveDamage(gate.Health.Current);
            yield return Navigation();
            Assert.AreEqual(0, left.Placement.Level); Assert.AreEqual(0, right.Placement.Level);
            Assert.IsTrue(breaker.GetComponent<NavMeshAgent>().Warp(position));
            float total = left.Health.Current + right.Health.Current;
            for (int i = 0; i < 600 && left != null && right != null
                && left.Health.Current + right.Health.Current == total; i++) yield return null;
            Assert.IsTrue(left == null || right == null || left.Health.Current + right.Health.Current < total);
        }
    }
}
