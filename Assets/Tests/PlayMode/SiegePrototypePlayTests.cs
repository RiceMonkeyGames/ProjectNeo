using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    public class SiegePrototypePlayTests
    {
        const int WallRow = 8;
        static readonly int[] Gaps = { 4, 11, 12 };
        Scene scene;
        SiegeDirector director;
        SiegePrepController prep;
        ConstructionWorld world;
        PlacementController placement;
        SquadCommandCenter center;
        SiegeLadder[] ladders;
        BuildPieceDefinition wall;
        float previousCapture;
        CursorLockMode previousLock;
        bool previousVisible;
        T Find<T>() where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime;
            previousLock = Cursor.lockState; previousVisible = Cursor.visible;
            Time.captureDeltaTime = 1f / 30f;
            yield return SceneManager.LoadSceneAsync("SiegePrototype", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            director = Find<SiegeDirector>(); prep = Find<SiegePrepController>(); world = Find<ConstructionWorld>();
            placement = Find<PlacementController>(); center = Find<SquadCommandCenter>();
            ladders = scene.GetRootGameObjects().Select(root => root.GetComponent<SiegeLadder>())
                .Where(ladder => ladder != null).OrderBy(ladder => ladder.Cell.x).ToArray();
            wall = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(scene);
            Time.captureDeltaTime = previousCapture;
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
        }

        bool Build(BuildPieceDefinition piece, int x, int z)
        {
            placement.Select(piece);
            placement.UpdatePreview(world.WorldCenter(piece, new Vector2Int(x, z), 0));
            bool placed = placement.Confirm();
            placement.Cancel();
            return placed;
        }

        IEnumerator WaitForNavigation()
        {
            int frames = 0;
            do { yield return null; frames++; }
            while ((world.IsRebuilding || world.RebuildPending) && frames < 600);
            Assert.IsFalse(world.IsRebuilding || world.RebuildPending, "길찾기 갱신이 끝나지 않았다.");
        }

        IEnumerator CloseWalls()
        {
            foreach (int x in Gaps) Assert.IsTrue(Build(wall, x, WallRow), "뚫린 칸 " + x + "에 성벽을 짓지 못했다.");
            yield return WaitForNavigation();
        }

        static void Kill(CharacterActor actor) => actor.ReceiveDamage(1000000f);
        CharacterActor[] Hostiles() => director.Enemies.Select(enemy => enemy.Actor).ToArray();
        float InsideZ => world.transform.position.z + WallRow * world.Grid.CellSize - 0.5f;

        [UnityTest]
        public IEnumerator Preparation_SpendsAndRefundsCost_ThenBattleLocksBuilding()
        {
            Assert.AreEqual(SiegePhase.Preparation, director.Phase);
            Assert.IsTrue(director.IsConstructionView && placement.enabled);
            Assert.IsTrue(director.Enemies.All(enemy => !enemy.gameObject.activeSelf), "준비 중에는 적이 나오지 않는다.");
            int total = prep.Budget.Remaining;
            Assert.IsTrue(Build(wall, Gaps[0], WallRow));
            Assert.Less(prep.Budget.Remaining, total);
            PlacedStructure built = world.Structures.Single(piece => piece.Placement.Cell == new Vector2Int(Gaps[0], WallRow));
            PlacedStructure original = world.Structures.First(piece => piece != built
                && piece.Placement.Definition.Kind == BuildPieceKind.Wall);
            Assert.IsFalse(prep.Dismantle(original), "처음부터 있던 조각은 철거할 수 없다.");
            Assert.IsTrue(prep.Dismantle(built), prep.Message);
            Assert.AreEqual(total, prep.Budget.Remaining, "철거하면 전액 돌려받는다.");
            yield return WaitForNavigation();

            Assert.IsTrue(director.StartBattle());
            yield return null;
            Assert.AreEqual(SiegePhase.Battle, director.Phase);
            Assert.IsFalse(director.IsConstructionView || placement.enabled);
            Assert.IsTrue(director.Enemies.All(enemy => enemy.gameObject.activeSelf));
            director.SetConstructionView(true);
            Assert.IsFalse(director.IsConstructionView, "전투 중에는 건설 화면으로 돌아갈 수 없다.");
            Assert.IsFalse(Build(wall, Gaps[0], WallRow), "전투 중에는 지을 수 없다.");
            Assert.IsFalse(prep.PlaceSquad(0, center.Squads[0].Members[0].transform.position));
            Assert.AreEqual(total, prep.Budget.Remaining);
        }

        [UnityTest]
        public IEnumerator Roster_IsBuiltFromSettingsAsset()
        {
            var allies = director.Settings.AllySquads.Where(group => group.unitPrefab != null && group.count > 0).ToArray();
            Assert.AreEqual(allies.Length, center.Squads.Count, "부대 수는 설정 에셋의 줄 수와 같다.");
            for (int i = 0; i < allies.Length; i++)
                Assert.AreEqual(allies[i].count, center.Squads[i].Members.Count);
            Assert.AreEqual(director.Settings.EnemyGroups.Sum(group => group.count), director.Enemies.Count);
            foreach (SiegeAttackerRole role in new[] { SiegeAttackerRole.Ladder, SiegeAttackerRole.GateBreaker, SiegeAttackerRole.Archer })
                Assert.AreEqual(director.Settings.EnemyGroups
                    .Where(group => group.unitPrefab.GetComponent<SiegeAttackerBrain>().Role == role).Sum(group => group.count),
                    director.CountEnemies(role));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeadEnemy_HidesItsRoleMarker()
        {
            director.StartBattle();
            yield return null;
            SiegeAttackerBrain enemy = director.Enemies.First(unit => unit.Role == SiegeAttackerRole.Ladder);
            Transform marker = enemy.transform.Find("Visual/RoleMarker");
            Assert.IsTrue(marker.gameObject.activeSelf);
            Kill(enemy.Actor);
            yield return null;
            Assert.IsFalse(marker.gameObject.activeSelf, "죽은 적의 종류 표시가 남아 있다.");
        }

        [UnityTest]
        public IEnumerator Preparation_PlacesSquadInstantlyOnWallTop()
        {
            Vector3 top = world.WorldCenter(wall, new Vector2Int(9, WallRow), 0) + Vector3.up * wall.Height;
            Assert.IsTrue(prep.PlaceSquad(2, top), prep.Message);
            yield return null;
            foreach (UnitCombatAI member in center.Squads[2].Members)
            {
                Assert.That(member.transform.position.y, Is.EqualTo(wall.Height).Within(0.3f));
                Assert.That(Vector3.Distance(member.transform.position, top), Is.LessThan(3f));
                Assert.AreEqual(UnitDirective.Hold, member.Directive);
            }
            Assert.AreEqual(SquadOrderKind.Hold, center.Commander.OrderOf(2).Kind);
        }

        [UnityTest]
        public IEnumerator OpenGap_EnemiesEnterThroughIt_WithoutLadders()
        {
            director.StartBattle();
            int frames = 0;
            while (!Hostiles().Any(actor => actor.transform.position.z < InsideZ) && frames++ < 1500) yield return null;
            Assert.IsTrue(Hostiles().Any(actor => actor.transform.position.z < InsideZ), "뚫린 곳으로 들어온 적이 없다.");
            Assert.IsTrue(ladders.All(ladder => ladder.State.Phase == LadderPhase.Down), "길이 열려 있으면 사다리를 세우지 않는다.");
        }

        [UnityTest]
        public IEnumerator ClosedWalls_EnemiesRaiseLadderClimbOneAtATime_AndBreakersHitGate()
        {
            yield return CloseWalls();
            PlacedStructure gate = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            float gateHealth = gate.Health.Current;
            director.StartBattle();
            bool raised = false, onTop = false, gateHit = false;
            int frames = 0;
            while (!(raised && onTop && gateHit) && frames++ < 2400)
            {
                yield return null;
                raised |= ladders.Any(ladder => ladder.State.Phase == LadderPhase.Up);
                Assert.LessOrEqual(director.Enemies.Count(enemy => enemy.IsClimbing),
                    ladders.Count(ladder => ladder.HasWall), "사다리 하나에 한 명씩만 오른다.");
                onTop |= director.Enemies.Any(enemy => !enemy.IsClimbing && !enemy.Actor.Health.IsDead
                    && enemy.GetComponent<NavMeshAgent>().enabled && enemy.transform.position.y > wall.Height - 0.4f);
                gateHit |= gate == null || gate.Health.Current < gateHealth;
            }
            Assert.IsTrue(raised, "사다리가 세워지지 않았다.");
            Assert.IsTrue(onTop, "성벽 위로 올라온 적이 없다.");
            Assert.IsTrue(gateHit, "성문이 공격받지 않았다.");
            // 성벽이 다 막혀 있는 동안 궁수와 성문 파괴병은 사다리를 타지 않는다.
            Assert.IsFalse(director.Enemies.Any(enemy => enemy.Role != SiegeAttackerRole.Ladder && enemy.IsClimbing));
        }

        [UnityTest]
        public IEnumerator Ladder_AlliesOnTopPushItDown_AndClimberTakesFallDamage()
        {
            yield return CloseWalls();
            Assert.IsTrue(prep.PlaceSquad(0, ladders[0].TopPoint), prep.Message);
            Assert.IsTrue(prep.PlaceSquad(1, ladders[1].TopPoint), prep.Message);
            director.StartBattle();
            bool toppled = false, fell = false;
            int frames = 0;
            while (!(toppled && fell) && frames++ < 2400)
            {
                yield return null;
                toppled |= ladders.Any(ladder => ladder.State.ToppleCount > 0);
                fell |= director.Enemies.Any(enemy => enemy.GetComponent<UnitFallController>().LastFallDamage > 0f);
            }
            Assert.IsTrue(toppled, "성벽 위 아군이 사다리를 밀어내지 못했다.");
            Assert.IsTrue(fell, "오르던 적이 떨어져 피해를 받지 않았다.");
        }

        IEnumerator KillBreachersAfterArchersDecide()
        {
            SiegeAttackerBrain[] archers = director.Enemies.Where(enemy => enemy.Role == SiegeAttackerRole.Archer).ToArray();
            Assert.IsNotEmpty(archers, "설정 에셋에 궁수가 있어야 하는 시험이다.");
            int frames = 0;
            while (!archers.All(archer => archer.HasDecided) && frames++ < 300) yield return null;
            Assert.IsTrue(archers.All(archer => archer.HasDecided), "궁수가 할 일을 정하지 않았다.");
            foreach (SiegeAttackerBrain enemy in director.Enemies)
                if (enemy.Role != SiegeAttackerRole.Archer) Kill(enemy.Actor);
        }

        [UnityTest]
        public IEnumerator ClosedWalls_BreachersGone_RemainingArchersRetreat_AndVictory()
        {
            yield return CloseWalls();
            director.StartBattle();
            yield return KillBreachersAfterArchersDecide();
            int frames = 0;
            while (director.Phase == SiegePhase.Battle && frames++ < 1500) yield return null;
            Assert.IsTrue(director.RetreatOrdered, "공성 수단을 잃은 적에게 후퇴를 지시하지 않았다.");
            Assert.AreEqual(SiegeOutcome.Victory, director.Outcome);
            Assert.Greater(director.EnemiesFled, 0, "물러나 떠난 적이 없다.");
            Assert.AreEqual(0, director.EnemiesAlive);
            foreach (SiegeAttackerBrain enemy in director.Enemies.Where(enemy => enemy.Fled))
                Assert.IsFalse(enemy.gameObject.activeSelf, "떠난 적은 전장에서 사라져야 한다.");
        }

        [UnityTest]
        public IEnumerator OpenGap_BreachersGone_ArchersKeepFighting()
        {
            director.StartBattle();
            yield return KillBreachersAfterArchersDecide();
            int frames = 0;
            while (!director.Enemies.Any(enemy => enemy.Role == SiegeAttackerRole.Archer && enemy.IsAssaulting)
                && frames++ < 300) yield return null;
            for (int i = 0; i < 60; i++) yield return null;
            Assert.IsFalse(director.RetreatOrdered, "들어올 길이 있으면 물러나지 않는다.");
            Assert.AreEqual(0, director.EnemiesFled);
        }

        [UnityTest]
        public IEnumerator Battle_AllEnemiesDead_IsVictory()
        {
            director.StartBattle();
            yield return null;
            Assert.AreEqual(SiegeOutcome.Ongoing, director.Outcome);
            foreach (CharacterActor enemy in Hostiles()) Kill(enemy);
            yield return null;
            yield return null;
            Assert.AreEqual(SiegeOutcome.Victory, director.Outcome);
            Assert.AreEqual(SiegePhase.Finished, director.Phase);
        }

        [UnityTest]
        public IEnumerator Battle_GateBrokenAndAlliesDead_IsDefeat_ButGateAloneIsNot()
        {
            yield return CloseWalls();
            director.StartBattle();
            yield return null;
            PlacedStructure gate = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            gate.ReceiveDamage(gate.Health.Current);
            yield return null;
            yield return null;
            Assert.IsTrue(director.GateDestroyed);
            Assert.AreEqual(SiegeOutcome.Ongoing, director.Outcome, "성문이 부서져도 아군이 남아 있으면 계속된다.");
            foreach (var squad in center.Squads)
                foreach (UnitCombatAI member in squad.Members) Kill(member.Actor);
            yield return null;
            yield return null;
            Assert.AreEqual(SiegeOutcome.Defeat, director.Outcome);
        }

        [UnityTest]
        public IEnumerator Battle_PlayerDeath_IsDefeat()
        {
            director.StartBattle();
            yield return null;
            Kill(Find<ProjectNeo.Characters.PlayerInputController>().GetComponent<CharacterActor>());
            yield return null;
            yield return null;
            Assert.AreEqual(SiegeOutcome.Defeat, director.Outcome);
            Assert.AreEqual(SiegePhase.Finished, director.Phase);
        }
    }
}
