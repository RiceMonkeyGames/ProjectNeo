using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using ProjectNeo.Stats;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using ProjectNeo.Characters;

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
                    .Where(group => group.unitPrefab.GetComponent<SiegeAttacker>().Role == role).Sum(group => group.count),
                    director.CountEnemies(role));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeadEnemy_HidesItsRoleMarker()
        {
            director.StartBattle();
            yield return null;
            SiegeAttacker enemy = director.Enemies.First(unit => unit.Role == SiegeAttackerRole.Ladder);
            Transform marker = enemy.transform.Find("Visual/RoleMarker");
            Assert.IsTrue(marker.gameObject.activeSelf);
            Kill(enemy.Actor);
            yield return null;
            Assert.IsFalse(marker.gameObject.activeSelf, "죽은 적의 종류 표시가 남아 있다.");
        }

        // 정해 둔 자리에 적 하나를 꺼내 둔다. 스스로 움직이지 않게 판단과 AI를 끈다.
        CharacterActor PlaceStillEnemy(SiegeAttackerRole role, Vector3 position)
        {
            SiegeAttacker enemy = director.Enemies.First(unit => unit.Role == role && !unit.gameObject.activeSelf);
            enemy.enabled = false;
            enemy.GetComponent<UnitCombatAI>().enabled = false;
            enemy.transform.position = position;
            enemy.gameObject.SetActive(true);
            Assert.IsTrue(enemy.GetComponent<NavMeshAgent>().Warp(position));
            return enemy.Actor;
        }

        int SquadOf(AttackKind kind) => Enumerable.Range(0, center.Squads.Count)
            .First(i => center.Squads[i].Members.All(member => member.Actor.Profile.AttackKind == kind));

        Vector3 WallTop(int x) => world.WorldCenter(wall, new Vector2Int(x, WallRow), 0) + Vector3.up * wall.Height;

        [UnityTest]
        public IEnumerator WallArchers_ShootEnemyRightBelowTheWall()
        {
            int archers = SquadOf(AttackKind.Projectile);
            Assert.IsTrue(prep.PlaceSquad(archers, WallTop(9)), prep.Message);
            yield return WaitForNavigation();
            // 성벽 위 한가운데에서 내려다보면 발밑 성벽 모서리를 스치는 각도의 땅. 3m 칸·3m 성벽에서는 바깥면에서 2m였고,
            // 7.5m 칸·5m 성벽(SIEGE-18)에서는 통로 가운데가 모서리에서 3.75m 안쪽이라 바깥면에서 약 15m다
            // (그보다 가까운 성벽 밑은 통로 가운데에서 보이지 않는다. 성가퀴 사격 자리는 SIEGE-20).
            Vector3 below = world.WorldCenter(wall, new Vector2Int(9, WallRow), 0)
                + Vector3.forward * (world.Grid.CellSize * 0.5f + 15f);
            CharacterActor enemy = PlaceStillEnemy(SiegeAttackerRole.GateBreaker, below);
            int frames = 0;
            while (enemy.Health.Current >= enemy.Health.Maximum && frames++ < 300) yield return null;
            Assert.Less(enemy.Health.Current, enemy.Health.Maximum, "성벽 위 궁수가 성벽 바로 아래의 적을 쏘지 못했다.");
            foreach (UnitCombatAI archer in center.Squads[archers].Members)
                Assert.That(archer.transform.position.y, Is.EqualTo(wall.Height).Within(0.3f), "궁수가 자리를 떠났다.");
        }

        [UnityTest]
        public IEnumerator WallMelee_EngagesEnemyOnWallNearPost_ThenReturns()
        {
            int melee = SquadOf(AttackKind.Melee);
            Assert.IsTrue(prep.PlaceSquad(melee, WallTop(9)), prep.Message);
            yield return WaitForNavigation();
            // 성벽 위로 올라온 적. 근접 사거리 밖이지만 자리 반경(6m) 안이다(7.5m 칸에서는 이웃 칸 가운데가 반경 밖이라 4m에 둔다).
            CharacterActor enemy = PlaceStillEnemy(SiegeAttackerRole.Ladder, WallTop(9) + Vector3.right * 4f);
            int frames = 0;
            while (!enemy.Health.IsDead && frames++ < 900) yield return null;
            Assert.IsTrue(enemy.Health.IsDead, "성벽 위 근접 부대가 다가가 싸우지 않았다.");
            frames = 0;
            while (!center.Squads[melee].Members.All(member => Vector3.Distance(member.transform.position, member.HoldPost) < 0.6f)
                && frames++ < 300) yield return null;
            foreach (UnitCombatAI member in center.Squads[melee].Members)
            {
                Assert.That(Vector3.Distance(member.transform.position, member.HoldPost), Is.LessThan(0.6f), "자리로 돌아오지 않았다.");
                Assert.That(member.transform.position.y, Is.EqualTo(wall.Height).Within(0.3f));
            }
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
                Assert.LessOrEqual(director.Enemies.OfType<SiegeAttackerBrain>().Count(enemy => enemy.IsClimbing),
                    ladders.Count(ladder => ladder.HasWall), "사다리 하나에 한 명씩만 오른다.");
                onTop |= director.Enemies.OfType<SiegeAttackerBrain>().Any(enemy => !enemy.IsClimbing && !enemy.Actor.Health.IsDead
                    && enemy.GetComponent<NavMeshAgent>().enabled && enemy.transform.position.y > wall.Height - 0.4f);
                gateHit |= gate == null || gate.Health.Current < gateHealth;
            }
            Assert.IsTrue(raised, "사다리가 세워지지 않았다.");
            Assert.IsTrue(onTop, "성벽 위로 올라온 적이 없다.");
            Assert.IsTrue(gateHit, "성문이 공격받지 않았다.");
            // 성벽이 다 막혀 있는 동안 궁수와 성문 파괴병은 사다리를 타지 않는다.
            Assert.IsFalse(director.Enemies.OfType<SiegeAttackerBrain>().Any(enemy => enemy.Role != SiegeAttackerRole.Ladder && enemy.IsClimbing));
        }

        [UnityTest]
        public IEnumerator Ladder_AlliesOnTopPushLaddersDown()
        {
            yield return CloseWalls();
            Assert.IsTrue(prep.PlaceSquad(0, ladders[0].TopPoint), prep.Message);
            Assert.IsTrue(prep.PlaceSquad(1, ladders[1].TopPoint), prep.Message);
            Assert.IsTrue(director.StartBattle());
            // 7.5m 칸 판(SIEGE-18)에서는 적이 2.5배 먼 곳(성벽에서 약 60m)에서 걸어온다.
            int frames = 0;
            while (!ladders.Any(ladder => ladder.State.ToppleCount > 0) && frames++ < 1800)
                yield return null;
            Assert.IsTrue(ladders.Any(ladder => ladder.State.ToppleCount > 0),
                "성벽 위 아군이 사다리를 밀어내지 못했다.");
        }

        [UnityTest]
        public IEnumerator Ladder_AlliesOnTopPushItDown_AndClimberTakesFallDamage()
        {
            yield return CloseWalls();
            // 혼합 전투에서는 빈 사다리를 먼저 밀거나 성문 돌파로 적 경로가 바뀔 수 있다.
            // 등반자가 피해를 받을 높이까지 오른 뒤 수비병을 놓아 자동 밀기부터 착지까지 검증한다.
            UnitCombatAI defender = IsolateSharedLadder();
            SiegeLadder ladder = ladders[0];
            CharacterActor enemy = PlaceStillEnemy(SiegeAttackerRole.Ladder, ladder.BasePoint);
            var brain = enemy.GetComponent<SiegeAttackerBrain>();
            var fall = enemy.GetComponent<UnitFallController>();
            brain.Bind(world, new[] { ladder }, Vector2Int.up);
            brain.enabled = true;
            int frames = 0;
            while ((!brain.IsClimbing || enemy.transform.position.y < ladder.BasePoint.y + 1.5f)
                && frames++ < 600) yield return null;
            Assert.IsTrue(brain.IsClimbing, "적이 사다리에 탑승하지 않았다.");
            Assert.AreSame(enemy, ladder.Occupant);
            Assert.GreaterOrEqual(enemy.transform.position.y, ladder.BasePoint.y + 1.5f,
                "낙하 피해를 확인할 높이까지 오르지 못했다.");
            float healthBefore = enemy.Health.Current;
            int topplesBefore = ladder.State.ToppleCount;
            Assert.IsTrue(defender.GetComponent<NavMeshAgent>().Warp(ladder.TopPoint));

            frames = 0;
            while (!fall.IsFalling && frames++ < 300) yield return null;
            Assert.IsTrue(fall.IsFalling, "성벽 위 아군의 자동 밀기가 등반자를 떨어뜨리지 못했다.");
            Assert.IsFalse(brain.IsClimbing);
            Assert.IsNull(ladder.Occupant);
            Assert.IsFalse(enemy.IsOnLadder);
            frames = 0;
            while ((fall.IsFalling || ladder.State.ToppleCount == topplesBefore) && frames++ < 300)
                yield return null;
            Assert.Greater(ladder.State.ToppleCount, topplesBefore, "사다리가 끝까지 쓰러지지 않았다.");
            Assert.IsFalse(fall.IsFalling, "떨어진 적이 착지하지 못했다.");
            Assert.Greater(fall.LastFallDamage, 0f, "오르던 적이 떨어져 피해를 받지 않았다.");
            Assert.That(enemy.Health.Current, Is.EqualTo(healthBefore - fall.LastFallDamage).Within(0.01f));
            Assert.IsTrue(enemy.GetComponent<NavMeshAgent>().enabled, "착지 후 길찾기가 복구되지 않았다.");
        }

        UnitCombatAI IsolateSharedLadder()
        {
            foreach (var unit in center.Squads.SelectMany(squad => squad.Members)) unit.enabled = false;
            return center.Squads[0].Members[0];
        }

        [UnityTest]
        public IEnumerator SharedLadder_LowEnemyPush_StaysAboveGroundAndLands()
        {
            yield return CloseWalls();
            IsolateSharedLadder();
            SiegeLadder ladder = ladders[0];
            Assert.IsTrue(ladder.HasWall);
            var state = ladder.State;
            state.Tick(0.1f, true, false, false);
            state.Tick(ladder.Settings.LadderRaiseSeconds, true, false, false);
            // 적은 발의 한 점이 아니라 사용 반경에 들어온 시점에 등반을 시작한다.
            CharacterActor enemy = PlaceStillEnemy(SiegeAttackerRole.Ladder,
                ladder.BasePoint + ladder.OutwardWorld * ladder.Settings.LadderUseRadius * 0.7f);
            var brain = enemy.GetComponent<SiegeAttackerBrain>();
            var fall = enemy.GetComponent<UnitFallController>();
            brain.Bind(world, new[] { ladder }, Vector2Int.up);
            brain.enabled = true;
            int frames = 0;
            while (!brain.IsClimbing && frames++ < 120) yield return null;
            Assert.IsTrue(brain.IsClimbing);
            float pushToBalance = state.PushToBalanceSeconds(1);
            state.Tick(pushToBalance * 0.75f, false, true, false);
            yield return null;
            Assert.GreaterOrEqual(enemy.transform.position.y, ladder.BasePoint.y - 0.01f,
                "탑승 직후 밀자 접근 위치가 발 축 아래로 회전해 지면을 뚫었다.");
            frames = 0;
            while (state.PushProgress > 0f && frames++ < 60) yield return null;
            Assert.AreEqual(0f, state.PushProgress);
            state.Tick(pushToBalance * 1.25f, false, true, false);
            Assert.IsTrue(fall.IsFalling, "복귀 후 다시 균형점을 넘겼는데 추락하지 않았다.");
            Assert.GreaterOrEqual(enemy.transform.position.y, ladder.BasePoint.y - 0.01f);
            frames = 0;
            while (fall.IsFalling && frames++ < 180) yield return null;
            Assert.IsFalse(fall.IsFalling, "지면 아래에서 낙하가 끝나지 않는다.");
            Assert.GreaterOrEqual(enemy.transform.position.y, ladder.BasePoint.y - 0.01f);
            Assert.IsTrue(enemy.GetComponent<NavMeshAgent>().enabled);
        }

        [UnityTest]
        public IEnumerator SharedLadder_EnemyHangsWithTilt_ResumesAfterReturn_AndFallsAtBalance()
        {
            yield return CloseWalls();
            IsolateSharedLadder();
            SiegeLadder ladder = ladders[0];
            var state = ladder.State;
            Assert.IsTrue(ladder.HasWall);
            state.Tick(0.1f, true, false, false);
            state.Tick(ladder.Settings.LadderRaiseSeconds, true, false, false);
            CharacterActor enemy = PlaceStillEnemy(SiegeAttackerRole.Ladder, ladder.BasePoint);
            var brain = enemy.GetComponent<SiegeAttackerBrain>();
            var fall = enemy.GetComponent<UnitFallController>();
            brain.Bind(world, new[] { ladder }, Vector2Int.up);
            brain.enabled = true;
            int frames = 0;
            while ((!brain.IsClimbing || enemy.transform.position.y < ladder.BasePoint.y + 1.5f)
                && frames++ < 180) yield return null;
            Assert.IsTrue(brain.IsClimbing, "적이 사다리에 탑승하지 않았다.");
            Assert.Greater(enemy.transform.position.y, ladder.BasePoint.y + 1.4f);
            Vector3 attached = enemy.transform.position;
            float pushToBalance = state.PushToBalanceSeconds(1);
            state.Tick(pushToBalance * 0.75f, false, true, false);
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                Assert.Greater(state.PushProgress, 0f);
                Assert.IsTrue(brain.IsClimbing);
                Assert.IsFalse(fall.IsFalling);
                Assert.That(Vector3.Distance(enemy.transform.position, ladder.FollowTilt(attached)), Is.LessThan(0.02f),
                    "기울어진 사다리에 매달리지 않고 기존 경로로 계속 올라갔다.");
            }
            frames = 0;
            while (state.PushProgress > 0f && frames++ < 60) yield return null;
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(brain.IsClimbing);
            Assert.Greater(enemy.transform.position.y, attached.y + 0.05f, "복귀 후 등반을 재개하지 않았다.");

            state.Tick(pushToBalance * 1.25f, false, true, false);
            Assert.AreEqual(LadderPhase.Up, state.Phase, "사다리가 완전히 눕기 전 추락을 검증해야 한다.");
            Assert.IsFalse(brain.IsClimbing);
            Assert.IsTrue(fall.IsFalling, "균형점을 넘었는데 추락하지 않았다.");
            Assert.IsNull(ladder.Occupant);
            Assert.IsFalse(state.HasClimber);
            Assert.IsFalse(enemy.IsOnLadder);
            Assert.AreEqual(0, state.ToppleCount);
            frames = 0;
            while (fall.IsFalling && frames++ < 300) yield return null;
            Assert.IsFalse(fall.IsFalling);
            Assert.Greater(fall.LastFallDamage, 0f, "실제 매달린 높이에서 낙하 피해를 받지 않았다.");
            Assert.IsTrue(enemy.GetComponent<NavMeshAgent>().enabled);
        }

        [UnityTest]
        public IEnumerator SharedLadder_EnemyFall_ClearsTiltingLadderWithoutPenetration()
        {
            yield return CloseWalls();
            IsolateSharedLadder();
            SiegeLadder ladder = ladders[0];
            var state = ladder.State;
            state.Tick(0.1f, true, false, false);
            state.Tick(ladder.Settings.LadderRaiseSeconds, true, false, false);
            CharacterActor enemy = PlaceStillEnemy(SiegeAttackerRole.Ladder, ladder.BasePoint);
            var brain = enemy.GetComponent<SiegeAttackerBrain>();
            var fall = enemy.GetComponent<UnitFallController>();
            brain.Bind(world, new[] { ladder }, Vector2Int.up);
            brain.enabled = true;

            int frames = 0;
            while ((!brain.IsClimbing || enemy.transform.position.y < ladder.BasePoint.y + 1.8f)
                && frames++ < 180) yield return null;
            Assert.IsTrue(brain.IsClimbing);

            float pushToBalance = state.PushToBalanceSeconds(1);
            state.Tick(pushToBalance * 1.1f, false, true, false);
            Assert.IsTrue(fall.IsFalling);

            float minOutwardGap = float.MaxValue;
            frames = 0;
            while (fall.IsFalling && frames++ < 300)
            {
                Vector3 dir = ladder.CurrentDirection;
                if (dir.y > 0.05f)
                {
                    float t = Mathf.Max(0f, (enemy.transform.position.y - ladder.BasePoint.y) / dir.y);
                    Vector3 railAtHeight = ladder.BasePoint + dir * t;
                    float gap = Vector3.Dot(enemy.transform.position - railAtHeight, ladder.OutwardWorld);
                    if (gap < minOutwardGap) minOutwardGap = gap;
                }
                yield return null;
            }

            Assert.IsFalse(fall.IsFalling);
            Assert.Greater(minOutwardGap, 0.35f, "낙하 중 적이 기울어지는 사다리 외형을 관통했다.");
            Assert.Greater(Vector3.Dot(enemy.transform.position - ladder.BasePoint, ladder.OutwardWorld), 0.8f);
            Assert.IsTrue(enemy.GetComponent<NavMeshAgent>().enabled);
        }

        [UnityTest]
        public IEnumerator SharedLadder_BalanceMotion_ReturnsOrCompletes_AndVisualFollowsWithoutSnapping()
        {
            yield return CloseWalls();
            UnitCombatAI unit = IsolateSharedLadder();
            SiegeLadder ladder = ladders[0];
            var view = ladder.GetComponentInChildren<PlaceholderLadderView>();
            Assert.IsNotNull(view);
            Assert.IsTrue(ladder.HasWall);
            unit.GetComponent<NavMeshAgent>().Warp(ladder.BasePoint);
            var state = ladder.State;
            float raiseSeconds = ladder.Settings.LadderRaiseSeconds;

            state.Tick(0.1f, true, false, false);
            state.Tick(raiseSeconds * state.BalancePoint * 0.5f, true, false, false);
            view.SendMessage("LateUpdate");
            float raisingAngle = Vector3.Angle(ladder.OutwardWorld, view.transform.forward);
            yield return null;
            view.SendMessage("LateUpdate");
            float returningAngle = Vector3.Angle(ladder.OutwardWorld, view.transform.forward);
            Assert.That(returningAngle, Is.GreaterThan(0f).And.LessThan(raisingAngle));
            Assert.IsFalse(ladder.TryRaise(unit.Actor), "복귀 중 새 세우기를 허용했다.");
            for (int i = 0; state.Phase != LadderPhase.Down && i < 200; i++) yield return null;
            Assert.AreEqual(LadderPhase.Down, state.Phase);

            state.Tick(0.1f, true, false, false);
            state.Tick(raiseSeconds * (state.BalancePoint + (1f - state.BalancePoint) * 0.5f), true, false, false);
            view.SendMessage("LateUpdate");
            Assert.Less(Vector3.Dot(view.transform.forward, ladder.OutwardWorld), 0f,
                "세우기 자동 완료 기준이 실제 수직 각도와 일치하지 않는다.");
            Assert.IsTrue(state.IsSettling);
            Assert.IsFalse(ladder.TryEnter(unit.Actor), "자동 세우기 도중 탑승했다.");
            for (int i = 0; state.Phase != LadderPhase.Up && i < 200; i++) yield return null;
            Assert.AreEqual(LadderPhase.Up, state.Phase);

            state.Tick(state.PushToBalanceSeconds(1) * 0.75f, false, true, false);
            view.SendMessage("LateUpdate");
            float pushingAngle = Vector3.Angle(ladder.OutwardWorld, view.transform.forward);
            Assert.IsFalse(ladder.TryEnter(unit.Actor), "밀고 있는 사다리에 탑승했다.");
            ladder.Leave(unit.Actor);
            yield return null;
            view.SendMessage("LateUpdate");
            float pushReturningAngle = Vector3.Angle(ladder.OutwardWorld, view.transform.forward);
            float raisedAngle = Vector3.Angle(ladder.OutwardWorld, ladder.EdgePoint - ladder.BasePoint);
            Assert.That(pushReturningAngle, Is.GreaterThan(pushingAngle).And.LessThan(raisedAngle));
            for (int i = 0; state.PushProgress > 0f && i < 200; i++) yield return null;
            Assert.AreEqual(0f, state.PushProgress);

            var enemy = PlaceStillEnemy(SiegeAttackerRole.Ladder, ladder.BasePoint);
            Assert.IsTrue(ladder.TryEnter(enemy));
            state.Tick(state.PushToBalanceSeconds(1) * 1.25f, false, true, false);
            view.SendMessage("LateUpdate");
            Assert.Greater(Vector3.Dot(view.transform.forward, ladder.OutwardWorld), 0f,
                "눕히기 자동 완료 기준이 실제 수직 각도와 일치하지 않는다.");
            Assert.IsTrue(state.IsSettling);
            Assert.IsFalse(state.HasClimber, "균형점을 넘었는데 탑승자를 떨어뜨리지 않았다.");
            Assert.IsNull(ladder.Occupant);
            Assert.IsFalse(ladder.TryEnter(unit.Actor));
            for (int i = 0; state.Phase != LadderPhase.Down && i < 200; i++) yield return null;
            Assert.AreEqual(LadderPhase.Down, state.Phase);
            Assert.AreEqual(1, state.ToppleCount);
            Assert.IsNull(ladder.Occupant);
            Assert.IsFalse(state.HasClimber);
        }

        [UnityTest]
        public IEnumerator SharedLadder_AlliedOrdersGoBothDirections_AndDeferUntilEndpoint()
        {
            yield return CloseWalls();
            UnitCombatAI unit = IsolateSharedLadder();
            SiegeLadder ladder = ladders[0];
            ladder.State.Tick(0.1f, true, false, false);
            ladder.State.Tick(ladder.Settings.LadderRaiseSeconds, true, false, false);
            var agent = unit.GetComponent<NavMeshAgent>();
            Assert.IsTrue(agent.Warp(ladder.BasePoint));
            unit.SetDirective(UnitDirective.Move, ladder.TopPoint);
            unit.enabled = true;
            int frames = 0;
            while (!unit.Actor.IsOnLadder && frames++ < 300) yield return null;
            Assert.IsTrue(unit.Actor.IsOnLadder, "아군 이동 명령이 사다리를 이용하지 않았다.");
            Vector3 original = unit.MoveDestination;
            unit.SetDirective(UnitDirective.Hold);
            Assert.AreEqual(UnitDirective.Move, unit.Directive, "등반 중 새 명령이 즉시 적용됐다.");
            Assert.AreEqual(original, unit.MoveDestination);
            Assert.IsFalse(unit.Actor.TryAttack(), "등반 중 공격을 시작했다.");
            float hp = unit.Actor.Health.Current;
            unit.Actor.ReceiveDamage(1f);
            Assert.Less(unit.Actor.Health.Current, hp, "등반 중 피해를 받지 않았다.");
            frames = 0;
            while (unit.Actor.IsOnLadder && frames++ < 300) yield return null;
            Assert.IsFalse(unit.Actor.IsOnLadder);
            Assert.AreEqual(UnitDirective.Hold, unit.Directive);
            Assert.That(unit.transform.position.y, Is.EqualTo(ladder.TopPoint.y).Within(0.2f));
            unit.SetDirective(UnitDirective.Move, ladder.BasePoint);
            frames = 0;
            while (!unit.Actor.IsOnLadder && frames++ < 300) yield return null;
            Assert.IsTrue(unit.Actor.IsOnLadder, "아군이 사다리 하강을 시작하지 않았다.");
            frames = 0;
            while (unit.Actor.IsOnLadder && frames++ < 300) yield return null;
            Assert.That(unit.transform.position.y, Is.EqualTo(ladder.BasePoint.y).Within(0.2f));
            Assert.IsTrue(agent.enabled && agent.isOnNavMesh);
        }

        [UnityTest]
        public IEnumerator SharedLadder_MixedQueuesKeepArrivalOrder_AndDeadWaiterIsRemoved()
        {
            yield return CloseWalls();
            UnitCombatAI first = IsolateSharedLadder();
            UnitCombatAI second = center.Squads[0].Members[1];
            SiegeLadder ladder = ladders[0];
            first.GetComponent<NavMeshAgent>().Warp(ladder.BasePoint);
            second.GetComponent<NavMeshAgent>().Warp(ladder.TopPoint);
            var enemy = PlaceStillEnemy(SiegeAttackerRole.Ladder, ladder.BasePoint);
            ladder.State.Tick(0.1f, true, false, false);
            ladder.State.Tick(ladder.Settings.LadderRaiseSeconds, true, false, false);
            Assert.IsTrue(ladder.TryEnter(first.Actor));
            Assert.IsFalse(ladder.TryEnter(second.Actor));
            Assert.IsFalse(ladder.TryEnter(enemy));
            ladder.Leave(first.Actor);
            Assert.IsFalse(ladder.TryEnter(enemy), "뒤에 온 적이 앞선 아군을 추월했다.");
            Kill(second.Actor);
            yield return null;
            Assert.IsTrue(ladder.TryEnter(enemy), "죽은 대기자가 대기열을 막았다.");
            Object.Destroy(enemy.gameObject);
            yield return null;
            yield return null;
            Assert.IsFalse(ladder.State.HasClimber, "파괴된 탑승자가 점유를 남겼다.");
            Assert.IsTrue(ladder.TryEnter(first.Actor));
            ladder.Leave(first.Actor);
        }

        [UnityTest]
        public IEnumerator SharedLadder_CollapseReleasesOccupancy_AndRestoresNavigation()
        {
            yield return CloseWalls();
            UnitCombatAI unit = IsolateSharedLadder();
            SiegeLadder ladder = ladders[0];
            unit.GetComponent<NavMeshAgent>().Warp(ladder.BasePoint);
            unit.SetDirective(UnitDirective.Move, ladder.TopPoint);
            unit.enabled = true;
            int frames = 0;
            while (!unit.Actor.IsOnLadder && frames++ < 400) yield return null;
            Assert.IsTrue(unit.Actor.IsOnLadder, "아군이 사다리를 세우고 탑승하지 않았다.");
            for (int i = 0; i < 30; i++) yield return null;
            var support = world.Structures.First(piece => piece.Placement.Cell == ladder.Cell);
            support.ReceiveDamage(1000000f);
            yield return null;
            Assert.IsFalse(unit.Actor.IsOnLadder);
            Assert.IsNull(ladder.Occupant);
            frames = 0;
            while (unit.GetComponent<UnitFallController>().IsFalling && frames++ < 600) yield return null;
            Assert.IsFalse(unit.GetComponent<UnitFallController>().IsFalling);
            Assert.IsTrue(unit.GetComponent<NavMeshAgent>().enabled);
            Assert.That(unit.transform.position.y, Is.EqualTo(ladder.BasePoint.y).Within(0.2f));
        }

        [UnityTest]
        public IEnumerator SharedLadder_PlayerTapBoards_HoldRaises_AndWSMovesBothDirections()
        {
            using var scope = new FocusIndependentInputScope();
            Keyboard original = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return CloseWalls();
                IsolateSharedLadder();
                var player = Find<PlayerInputController>();
                SiegeLadder ladder = ladders[0];
                director.SetConstructionView(false);
                var motor = player.GetComponent<CharacterController>();
                motor.enabled = false; player.transform.position = ladder.BasePoint; motor.enabled = true;
                Cursor.lockState = CursorLockMode.Locked;
                // 자동 안착 중 누른 짧은 F가, 안착 직후 해제됐다고 탑승으로 바뀌면 안 된다.
                ladder.State.Tick(0.1f, true, false, false);
                ladder.State.Tick(ladder.Settings.LadderRaiseSeconds - Time.captureDeltaTime * 1.5f, true, false, false);
                Assert.IsTrue(ladder.State.IsSettling);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.AreEqual(LadderPhase.Up, ladder.State.Phase);
                Assert.IsFalse(player.GetComponent<CharacterActor>().IsOnLadder,
                    "자동 안착 중 누른 키가 안착 직후 해제되자 탑승으로 바뀌었다.");
                ladder.State.Topple();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                int frames = 0;
                while (ladder.State.Phase != LadderPhase.Up && frames++ < 300) yield return null;
                Assert.AreEqual(LadderPhase.Up, ladder.State.Phase, "플레이어 F 유지로 세워지지 않았다.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.IsFalse(player.GetComponent<CharacterActor>().IsOnLadder, "세우기 유지 입력이 탑승으로 이어졌다.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.IsTrue(player.GetComponent<CharacterActor>().IsOnLadder);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                frames = 0;
                while (player.GetComponent<CharacterActor>().IsOnLadder && frames++ < 300) yield return null;
                Assert.That(player.transform.position.y, Is.EqualTo(ladder.TopPoint.y).Within(0.2f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.IsTrue(player.GetComponent<CharacterActor>().IsOnLadder);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                frames = 0;
                while (player.GetComponent<CharacterActor>().IsOnLadder && frames++ < 300) yield return null;
                Assert.That(player.transform.position.y, Is.EqualTo(ladder.BasePoint.y).Within(0.2f));
                Assert.IsTrue(motor.enabled);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (original != null && original.added) original.MakeCurrent();
            }
        }

        [UnityTest]
        public IEnumerator SharedLadder_FriendlyQueuePreventsAutomaticToppling_ThenHoldFTopplesWithoutBoarding()
        {
            using var scope = new FocusIndependentInputScope();
            Keyboard original = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return CloseWalls();
                UnitCombatAI waiting = IsolateSharedLadder();
                var player = Find<PlayerInputController>();
                SiegeLadder ladder = ladders[0];
                var defender = center.Squads[0].Members[1];
                defender.GetComponent<NavMeshAgent>().Warp(ladder.TopPoint);
                waiting.GetComponent<NavMeshAgent>().Warp(ladder.BasePoint);
                ladder.State.Tick(0.1f, true, false, false);
                ladder.State.Tick(ladder.Settings.LadderRaiseSeconds, true, false, false);
                var enemy = PlaceStillEnemy(SiegeAttackerRole.Ladder, ladder.BasePoint);
                Assert.IsTrue(ladder.TryEnter(enemy));
                Assert.IsFalse(ladder.TryEnter(waiting.Actor));
                for (int i = 0; i < 90; i++) yield return null;
                Assert.AreEqual(LadderPhase.Up, ladder.State.Phase, "아군 이용 대기 중 자동으로 밀었다.");
                ladder.Leave(enemy); ladder.Leave(waiting.Actor);
                defender.GetComponent<NavMeshAgent>().Warp(ladder.BasePoint + Vector3.right * 10f);
                enemy.gameObject.SetActive(false);
                director.SetConstructionView(false);
                var motor = player.GetComponent<CharacterController>();
                motor.enabled = false; player.transform.position = ladder.TopPoint; motor.enabled = true;
                Cursor.lockState = CursorLockMode.Locked;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                int frames = 0;
                while (ladder.State.Phase == LadderPhase.Up && frames++ < 180) yield return null;
                Assert.AreEqual(LadderPhase.Down, ladder.State.Phase, "플레이어 F 유지로 넘어뜨리지 못했다.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.IsFalse(player.GetComponent<CharacterActor>().IsOnLadder, "F 유지 해제가 탑승으로 처리됐다.");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (original != null && original.added) original.MakeCurrent();
            }
        }

        [UnityTest]
        public IEnumerator SharedLadder_DeathAndDisabledControllerReleaseOccupancy()
        {
            yield return CloseWalls();
            UnitCombatAI unit = IsolateSharedLadder();
            SiegeLadder ladder = ladders[0];
            unit.GetComponent<NavMeshAgent>().Warp(ladder.BasePoint);
            unit.SetDirective(UnitDirective.Move, ladder.TopPoint);
            unit.enabled = true;
            int frames = 0;
            while (!unit.Actor.IsOnLadder && frames++ < 400) yield return null;
            Assert.IsTrue(unit.Actor.IsOnLadder);
            for (int i = 0; i < 30; i++) yield return null;
            Kill(unit.Actor);
            Assert.IsNull(ladder.Occupant);
            Assert.IsFalse(unit.Actor.IsOnLadder);
            for (int i = 0; i < 120; i++) yield return null;
            Assert.That(unit.transform.position.y, Is.EqualTo(ladder.BasePoint.y).Within(0.2f));
            var other = center.Squads[0].Members[1];
            other.GetComponent<NavMeshAgent>().Warp(ladder.BasePoint);
            other.SetDirective(UnitDirective.Move, ladder.TopPoint); other.enabled = true;
            frames = 0;
            while (!other.Actor.IsOnLadder && frames++ < 400) yield return null;
            Assert.IsTrue(other.Actor.IsOnLadder);
            other.enabled = false;
            Assert.IsNull(ladder.Occupant, "컨트롤러 비활성화가 점유를 남겼다.");
            Assert.IsFalse(other.Actor.IsOnLadder);
        }

        IEnumerator KillBreachersAfterArchersDecide()
        {
            SiegeAttacker[] archers = director.Enemies.Where(enemy => enemy.Role == SiegeAttackerRole.Archer).ToArray();
            Assert.IsNotEmpty(archers, "설정 에셋에 궁수가 있어야 하는 시험이다.");
            int frames = 0;
            while (!archers.All(archer => archer.HasDecided) && frames++ < 300) yield return null;
            Assert.IsTrue(archers.All(archer => archer.HasDecided), "궁수가 할 일을 정하지 않았다.");
            foreach (SiegeAttacker enemy in director.Enemies)
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
            foreach (SiegeAttacker enemy in director.Enemies.Where(enemy => enemy.Fled))
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

        [UnityTest]
        public IEnumerator Ladder_AllyPushInterruptedByRangedHit_SettlesBackAndEnforcesCooldown()
        {
            yield return CloseWalls();
            IsolateSharedLadder();
            SiegeLadder ladder = ladders[0];
            var state = ladder.State;
            state.Tick(0.1f, true, false, false);
            state.Tick(ladder.Settings.LadderRaiseSeconds, true, false, false);
            Assert.AreEqual(LadderPhase.Up, state.Phase);

            // 성벽 위에서 사다리를 미는 아군 1명과, 원거리 경직도를 가진 적 공격자 1명 배치
            int meleeSquad = SquadOf(AttackKind.Melee);
            UnitCombatAI allyAi = center.Squads[meleeSquad].Members[0];
            foreach (var squad in center.Squads)
                foreach (var member in squad.Members)
                    if (member != allyAi)
                    {
                        member.enabled = false;
                        member.GetComponent<NavMeshAgent>().Warp(WallTop(0));
                    }
            CharacterActor ally = allyAi.Actor;
            allyAi.GetComponent<NavMeshAgent>().Warp(ladder.TopPoint);
            allyAi.enabled = false;
            CharacterActor archer = PlaceStillEnemy(SiegeAttackerRole.Archer, ladder.BasePoint + ladder.OutwardWorld * 6f);
            bool interrupted = false;
            ally.ActionInterrupted += (_, __) => interrupted = true;

            int frames = 0;
            while (state.PushProgress < 0.05f && frames++ < 180) yield return null;
            Assert.Greater(state.PushProgress, 0.04f, "아군이 사다리 밀기를 시작하지 못했다.");
            Assert.IsFalse(state.IsSettling, "균형점을 넘기기 전에 피격되어야 한다.");

            float progressAtHit = state.PushProgress;
            ally.ReceiveDamage(1f, archer);
            Assert.IsTrue(interrupted, "원거리 피격으로 사다리 밀기가 중단되지 않았다.");

            // 중단 후 0.3초 재시도 방지 동안에는 밀기 대신 성벽 쪽으로 다시 걸쳐져야 한다.
            yield return new WaitForSeconds(0.15f);
            Assert.Less(state.PushProgress, progressAtHit, "중단 후 사다리가 성벽으로 되돌아가지 않았다.");

            // 0.3초 쿨다운이 지난 뒤에는 다시 밀기를 재개할 수 있어야 한다.
            yield return new WaitForSeconds(0.25f);
            float afterCooldown = state.PushProgress;
            frames = 0;
            while (state.PushProgress <= afterCooldown && frames++ < 60) yield return null;
            Assert.Greater(state.PushProgress, afterCooldown, "0.3초 쿨다운 후 사다리 밀기가 재개되지 않았다.");
        }

        [UnityTest]
        public IEnumerator Ladder_EnemyRaiseNotInterruptedByHit()
        {
            yield return CloseWalls();
            IsolateSharedLadder();
            SiegeLadder ladder = ladders[0];
            var state = ladder.State;
            CharacterActor enemy = PlaceStillEnemy(SiegeAttackerRole.Ladder, ladder.BasePoint);
            var brain = enemy.GetComponent<SiegeAttackerBrain>();
            brain.Bind(world, new[] { ladder }, Vector2Int.up);
            brain.enabled = true;
            CharacterActor ally = center.Squads[SquadOf(AttackKind.Melee)].Members[0].Actor;
            bool interrupted = false;
            enemy.ActionInterrupted += (_, __) => interrupted = true;

            int frames = 0;
            while (state.RaiseProgress < 0.05f && frames++ < 120) yield return null;
            float beforeHit = state.RaiseProgress;
            Assert.Greater(beforeHit, 0.04f, "적이 사다리 올리기를 시작하지 못했다.");

            enemy.ReceiveDamage(1f, ally);
            Assert.IsFalse(interrupted, "사다리 올리기는 피격으로 중단되면 안 된다.");
            yield return null;
            Assert.Greater(state.RaiseProgress, beforeHit, "피격 후 사다리 올리기 진행이 멈췄다.");
        }

        // ── SIEGE-15 돌격 전환·궁수 위치·재장전 웅크림 ───────────────────

        // 아군 하나를 정한 자리에 세워 둔다. 스스로 움직이거나 싸우지 않게 AI를 끈다.
        CharacterActor PlaceStillAlly(Vector3 position)
        {
            UnitCombatAI ally = center.Squads[SquadOf(AttackKind.Melee)].Members[0];
            ally.enabled = false;
            Move(ally.Actor, position);
            return ally.Actor;
        }

        static void Move(CharacterActor actor, Vector3 position)
        {
            NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
            Assert.IsTrue(agent.Warp(position), "유닛을 옮기지 못했다.");
            agent.ResetPath();
        }

        static Vector3 FieldPoint(float x, float z) =>
            NavMesh.SamplePosition(new Vector3(x, 0f, z), out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : new Vector3(x, 0f, z);

        // 판단은 켠 채로 적 궁수 하나를 꺼내 둔다.
        SiegeAttackerBrain PlaceEnemyArcher(Vector3 position)
        {
            SiegeAttackerBrain archer = director.Enemies.OfType<SiegeAttackerBrain>()
                .First(unit => unit.Role == SiegeAttackerRole.Archer && !unit.gameObject.activeSelf);
            archer.transform.position = position;
            archer.gameObject.SetActive(true);
            Assert.IsTrue(archer.GetComponent<NavMeshAgent>().Warp(position));
            return archer;
        }

        [UnityTest]
        public IEnumerator ClosedWalls_FarSortieAlly_DoesNotPullSiegeEnemies_ButLeashDoes()
        {
            yield return CloseWalls();
            // 성 밖, 적의 사다리·성문 행군 길에서 멀리 떨어진 곳(7.5m 칸 판 기준, SIEGE-18에서 3m 판의 (20, 12)를 2.5배로).
            Vector3 far = FieldPoint(50f, 30f);
            CharacterActor sortie = PlaceStillAlly(far);
            director.StartBattle();
            SiegeAttackerBrain[] brains = director.Enemies.OfType<SiegeAttackerBrain>().ToArray();
            float release = director.Settings.AssaultLeashReleaseRadius;
            for (int i = 0; i < 240; i++)
            {
                yield return null;
                sortie.Health.RestoreFull();
                foreach (SiegeAttackerBrain enemy in brains)
                {
                    if (!enemy.IsAssaulting) continue;
                    float distance = Vector3.Distance(enemy.transform.position, sortie.transform.position);
                    Assert.LessOrEqual(distance, release + 2f, $"{enemy.Role} 적이 성 밖 먼 아군 하나에 끌려 돌격했다({distance:F1}m).");
                }
            }
            Assert.IsTrue(brains.Any(enemy => enemy.Role != SiegeAttackerRole.Archer && enemy.HasDecided && !enemy.IsAssaulting),
                "공성병이 공성 임무를 하지 않는다.");

            // 근접 반경 안에 들어오면 그 적은 돌격하고, 풀림 반경 밖으로 멀어지면 공성 임무로 돌아간다.
            SiegeAttackerBrain breaker = brains.First(enemy => enemy.Role == SiegeAttackerRole.GateBreaker
                && !enemy.Actor.Health.IsDead && !enemy.IsAssaulting && !enemy.IsMidAction);
            // 7.5m 칸 판(SIEGE-18)에서는 행군이 길어 8초 뒤에도 성문으로 걷는 중이다. 걷는 동안 뒤에 두면 바로 멀어지므로
            // 3m 판에서처럼 성문 앞에 닿은 뒤 바깥쪽 4m에 아군을 둔다.
            float gateFace = world.transform.position.z + (WallRow + 1) * world.Grid.CellSize;
            int frames = 0;
            while (breaker.transform.position.z > gateFace + 4f && frames++ < 900) { sortie.Health.RestoreFull(); yield return null; }
            Assert.LessOrEqual(breaker.transform.position.z, gateFace + 4f, "성문 파괴병이 성문 앞에 닿지 않았다.");
            Move(sortie, FieldPoint(breaker.transform.position.x, breaker.transform.position.z + 4f));
            frames = 0;
            while (!breaker.IsAssaulting && frames++ < 120) { sortie.Health.RestoreFull(); yield return null; }
            Assert.IsTrue(breaker.IsAssaulting, "근접 반경 안의 성 밖 아군에게 돌격하지 않았다.");
            Move(sortie, far);
            frames = 0;
            while (breaker.IsAssaulting && frames++ < 180) { sortie.Health.RestoreFull(); yield return null; }
            Assert.IsFalse(breaker.IsAssaulting, "성 밖 아군이 멀어졌는데 공성 임무로 돌아가지 않았다.");
        }

        [UnityTest]
        public IEnumerator OpenGap_EnemyArchersKeepShootingFromOutside_WhileBreakersAssault()
        {
            int archers = SquadOf(AttackKind.Projectile);
            Assert.IsTrue(prep.PlaceSquad(archers, WallTop(9)), prep.Message);
            yield return WaitForNavigation();
            director.StartBattle();
            bool breakerAssaulted = false;
            // archerBreachAdvanceDelay(8초)보다 짧게 본다.
            for (int i = 0; i < 180; i++)
            {
                yield return null;
                breakerAssaulted |= director.Enemies.Any(enemy => enemy.Role != SiegeAttackerRole.Archer && enemy.IsAssaulting);
                Assert.IsFalse(director.Enemies.Any(enemy => enemy.Role == SiegeAttackerRole.Archer && enemy.IsAssaulting),
                    "돌파구가 열리자마자 적 궁수가 성 안 난전으로 뛰어들었다.");
            }
            Assert.IsTrue(breakerAssaulted, "뚫린 곳이 있는데 공성병이 돌격하지 않았다.");
            Assert.IsTrue(director.Enemies.Any(enemy => enemy.Role == SiegeAttackerRole.Archer && enemy.HasEntryRoute),
                "궁수가 돌파구를 보지 못했다(후퇴 판정에 쓰임).");
            Assert.IsFalse(director.RetreatOrdered);
        }

        [UnityTest]
        public IEnumerator ProtrudingWall_EnemyArcherApproachesItsSideFace()
        {
            var stubCell = new Vector2Int(3, WallRow + 1);
            Assert.IsTrue(Build(wall, stubCell.x, stubCell.y), prep.Message);
            yield return WaitForNavigation();
            Vector3 stubBase = world.WorldCenter(wall, stubCell, 0);
            int archers = SquadOf(AttackKind.Projectile);
            Assert.IsTrue(prep.PlaceSquad(archers, stubBase + Vector3.up * wall.Height), prep.Message);
            yield return null;
            foreach (UnitCombatAI member in center.Squads[archers].Members) member.enabled = false;
            // 다른 아군과 플레이어는 뒤쪽 들판 끝으로 옮겨, 튀어나온 성벽 위 부대가 가장 가까운 대상이 되게 한다.
            int moved = 0;
            for (int s = 0; s < center.Squads.Count; s++)
            {
                if (s == archers) continue;
                foreach (UnitCombatAI member in center.Squads[s].Members)
                {
                    member.enabled = false;
                    Move(member.Actor, FieldPoint(-20f + moved++ * 1.5f, -27f));
                }
            }
            var player = Find<PlayerInputController>();
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = new Vector3(20f, 0.1f, -27f);
            body.enabled = true;
            Physics.SyncTransforms();
            SiegeAttackerBrain archer = PlaceEnemyArcher(FieldPoint(stubBase.x + 25f, stubBase.z + 2f));
            yield return null;
            float range = archer.Actor.Stats.GetValue(StatType.AttackRange);
            Assert.Less(range + 4f, 26f, "시험 배치: 뒤로 옮긴 아군이 튀어나온 성벽보다 가까워진다.");
            Move(archer.Actor, FieldPoint(stubBase.x + range + 4f, stubBase.z + 2f));
            UnitCombatAI ai = archer.GetComponent<UnitCombatAI>();
            int frames = 0;
            while (ai.Directive != UnitDirective.Move && frames++ < 120) yield return null;
            Assert.AreEqual(UnitDirective.Move, ai.Directive, "궁수가 사격 자리로 가지 않았다.");
            Vector3 offset = ai.MoveDestination - stubBase;
            Assert.Greater(offset.x, range * 0.5f, $"튀어나온 성벽의 옆면(동쪽) 바깥으로 가지 않았다: {offset}");
            Assert.Less(Mathf.Abs(offset.z), 3.5f, $"옆면 법선 대신 성 정면(바깥) 방향으로 섰다: {offset}");
        }

        [UnityTest]
        public IEnumerator EnemyArcher_WaitsInPlace_WhileTargetReloadCrouches()
        {
            int archers = SquadOf(AttackKind.Projectile);
            Assert.IsTrue(prep.PlaceSquad(archers, WallTop(9)), prep.Message);
            yield return WaitForNavigation();
            var members = center.Squads[archers].Members;
            foreach (UnitCombatAI member in members) member.enabled = false;
            float cell = world.Grid.CellSize;
            Vector3 wallBase = world.WorldCenter(wall, new Vector2Int(9, WallRow), 0);
            CharacterActor defender = members[0].Actor;
            Move(defender, wallBase + Vector3.forward * (cell * 0.5f - 0.6f) + Vector3.up * wall.Height);
            for (int i = 1; i < members.Count; i++) Move(members[i].Actor, FieldPoint(-12f + i * 2f, -22f));
            // 성가퀴 난간처럼 성벽 위 바깥 가장자리에 1.1m 높이 벽을 세운다(시험 장면에는 성가퀴가 없다).
            var sill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sill.name = "TestSill";
            SceneManager.MoveGameObjectToScene(sill, scene);
            sill.transform.position = wallBase + Vector3.forward * (cell * 0.5f - 0.15f) + Vector3.up * (wall.Height + 0.55f);
            sill.transform.localScale = new Vector3(cell, 1.1f, 0.3f);
            Physics.SyncTransforms();

            SiegeAttackerBrain archer = PlaceEnemyArcher(FieldPoint(wallBase.x, wallBase.z + cell * 0.5f + 12f));
            yield return null;
            float range = archer.Actor.Stats.GetValue(StatType.AttackRange);
            Assert.Greater(range, 13f, "시험 배치가 궁수 사거리 밖이다.");
            Assert.IsTrue(CombatPhysics.HasClearShot(archer.Actor, defender), "선 수비병이 보여야 한다(시험 배치 확인).");
            defender.SetCrouched(true);
            Assert.IsFalse(CombatPhysics.HasClearShot(archer.Actor, defender), "웅크린 수비병은 가려져야 한다(시험 배치 확인).");
            defender.SetCrouched(false);

            int frames = 0;
            while (!archer.IsArcherFiring && frames++ < 120) { defender.Health.RestoreFull(); yield return null; }
            Assert.IsTrue(archer.IsArcherFiring, "보이는 수비병을 쏘지 않았다.");
            Vector3 stand = archer.transform.position;
            defender.SetCrouched(true);
            // 가려진 시간(archerRepositionDelay 3초)을 넘겨도 참는 시간(4초) 안에는 옆으로 비켜서지 않는다.
            for (int i = 0; i < 105; i++)
            {
                defender.Health.RestoreFull();
                yield return null;
                Assert.IsFalse(archer.IsArcherRepositioning, "재장전하느라 웅크린 수비병 때문에 옆으로 비켜섰다.");
                // 중지(Hold) 중 쫓기도 막으므로 뚫린 곳으로 수비병을 쫓아 들어가지 않는다(SetHoldChaseSuppressed).
                Assert.Less(Vector3.Distance(archer.transform.position, stand), 1f, "웅크린 수비병 때문에 자리를 옮겼다.");
            }
            Assert.IsTrue(archer.IsArcherWaiting, "웅크린 수비병을 겨눈 채 기다리지 않았다.");
            defender.SetCrouched(false);
            frames = 0;
            while (!archer.IsArcherFiring && frames++ < 60) { defender.Health.RestoreFull(); yield return null; }
            Assert.IsTrue(archer.IsArcherFiring, "일어선 수비병을 다시 쏘지 않았다.");
        }
    }
}
