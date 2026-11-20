using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Siege;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// SIEGE-08 Game 씬 작업 주문(Q-53): 놓으면 짓는 중, 시계만큼 진행, 작업대 둘, 미완성 위 쌓기 금지, 전투 시작 정리
    /// (0% 환불·진행률 체력·미완성 대포는 쏘지 않음·수리 남은 몫 환불·성문은 완료 때 닫힘), 저장 기록.
    /// </summary>
    public class GameConstructionWorkPlayTests
    {
        static int cleanup;
        GameBuildEconomy economy;
        GameBattleController battle;
        ConstructionWorld world;
        PlacementController controller;
        BuildPieceDefinition wall;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            economy = Object.FindAnyObjectByType<GameBuildEconomy>();
            battle = Object.FindAnyObjectByType<GameBattleController>();
            world = Object.FindAnyObjectByType<ConstructionWorld>();
            controller = Object.FindAnyObjectByType<PlacementController>(FindObjectsInactive.Include);
            wall = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
            Assert.IsNotNull(State.PrepTime, "Game 씬은 준비 시간이 켜져 있어야 한다(PREP-06).");
            State.Wallet.AddGold(1000);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameConstructionWorkCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        RunState State => GameSession.Flow.Run.State;
        GameConstructionWork Work => economy.Work;

        Vector2Int FreeCell(params Vector2Int[] taken)
        {
            for (int x = 30; x < 50; x++)
                for (int z = 31; z < 50; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (taken.Any(used => Mathf.Abs(used.x - x) < 2 && Mathf.Abs(used.y - z) < 2)) continue;
                    if (world.Layout.ColumnHeight(cell) != 0 || world.CheckPlacement(wall, cell, 0) != PlacementIssue.None) continue;
                    Vector3 center = world.WorldCenter(wall, cell, 0);
                    if (ProjectNeo.Combat.CharacterActor.ActiveActors.Any(actor => Vector3.Distance(actor.transform.position, center) < 6f)) continue;
                    return cell;
                }
            Assert.Fail("빈 칸이 없다.");
            return default;
        }

        PlacedStructure Build(Vector2Int cell, int level = 0)
        {
            controller.Select(wall);
            controller.UpdatePreview(world.WorldCenter(wall, cell, 0));
            if (controller.Issue != PlacementIssue.None || !controller.Confirm()) return null;
            return world.Structures.Single(piece => piece.Placement.Cell == cell && piece.Placement.Level == level);
        }

        PlacedSiegeWeapon Mount(SiegeWeaponDefinition cannon, PlacedStructure support)
        {
            controller.SelectWeapon(cannon);
            Assert.IsTrue(support.TryGetMountPose("top", out Pose pose));
            controller.UpdateWeaponPreview(support, pose.position);
            if (controller.MountIssue != MountIssue.None || !controller.Confirm()) return null;
            return world.Weapons.Single(weapon => weapon.Support == support);
        }

        SiegeWeaponDefinition Cannon => economy.Table.Weapons.Select(entry => entry.weapon)
            .First(definition => definition != null && definition.Id == "cannon");

        IEnumerator Navigation()
        {
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
        }

        [UnityTest]
        public IEnumerator 놓은_조각은_짓는_중이고_위에_쌓을_수_없으며_시계가_가면_완성된다()
        {
            yield return null;
            int gold = State.Wallet.Gold, cost = economy.Table.CostOf(wall);
            Vector2Int cell = FreeCell();
            PlacedStructure piece = Build(cell);
            Assert.IsNotNull(piece);
            Assert.AreEqual(gold - cost, State.Wallet.Gold, "골드는 주문 때 낸다.");
            Assert.IsTrue(piece.IsUnderConstruction);
            Assert.AreEqual(piece.Health.Maximum, piece.Health.Current, "준비 중 짓는 조각은 체력이 깎이지 않는다.");
            var view = piece.GetComponent<UnderConstructionView>();
            Assert.IsNotNull(view);
            Assert.IsTrue(view.IsShowing);
            Assert.AreEqual("0%", view.LabelText);
            StringAssert.Contains("작업 주문 1", Work.Summary());

            controller.Select(wall);
            controller.UpdatePreview(world.WorldCenter(wall, cell, 0));
            Assert.AreEqual(PlacementIssue.UnfinishedSupport, controller.Issue, "짓는 중인 조각 위에는 쌓지 않는다.");
            Assert.AreEqual(MountIssue.UnfinishedSupport, world.CheckMount(Cannon, piece, "top"));
            controller.Cancel();

            Assert.IsTrue(State.TrySpendPrepTime(0.4f));
            Assert.AreEqual("40%", view.LabelText);
            Assert.IsTrue(State.TryWait());
            Assert.IsFalse(piece.IsUnderConstruction);
            Assert.IsFalse(view.IsShowing, "완성되면 원래 모습으로 돌아간다.");
            Assert.AreEqual(string.Empty, Work.Summary());
            controller.Select(wall);
            controller.UpdatePreview(world.WorldCenter(wall, cell, 0));
            Assert.AreEqual(PlacementIssue.None, controller.Issue, "완성된 조각 위에는 쌓을 수 있다.");
            controller.Cancel();
        }

        [UnityTest]
        public IEnumerator 작업대_둘이_먼저_놓은_순서로_짓고_전투_시작_때_0퍼센트는_없어지며_진행된_것은_진행률만큼_체력이다()
        {
            yield return null;
            int cost = economy.Table.CostOf(wall);
            Vector2Int a = FreeCell(), b = FreeCell(a), c = FreeCell(a, b), d = FreeCell(a, b, c);
            PlacedStructure first = Build(a), second = Build(b), third = Build(c), fourth = Build(d);
            Assert.IsNotNull(first); Assert.IsNotNull(second); Assert.IsNotNull(third); Assert.IsNotNull(fourth);
            Assert.IsTrue(State.TryWait());
            Assert.IsFalse(first.IsUnderConstruction || second.IsUnderConstruction, "먼저 놓은 둘이 1시간에 끝난다.");
            Assert.IsTrue(third.IsUnderConstruction && fourth.IsUnderConstruction);
            Assert.IsTrue(State.TrySpendPrepTime(0.5f));
            Assert.AreEqual("50%", third.GetComponent<UnderConstructionView>().LabelText);
            Assert.AreEqual("50%", fourth.GetComponent<UnderConstructionView>().LabelText);

            // 다섯 번째는 시작도 못 한 채 전투를 맞는다.
            Vector2Int e = FreeCell(a, b, c, d);
            PlacedStructure fifth = Build(e);
            Assert.IsNotNull(fifth);
            int beforeBattle = State.Wallet.Gold;
            yield return Navigation();
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Assert.AreEqual(0, world.Layout.ColumnHeight(e), "0% 조각은 전투 시작 때 없어진다.");
            Assert.AreEqual(beforeBattle + cost, State.Wallet.Gold, "0% 조각은 전액 환불한다.");
            Assert.IsFalse(third.IsUnderConstruction);
            Assert.AreEqual(third.Health.Maximum * 0.5f, third.Health.Current, 0.01f, "50% 지은 조각은 체력 50%로 선다.");
            Assert.IsFalse(third.GetComponent<UnderConstructionView>().IsShowing);
            Assert.AreEqual(first.Health.Maximum, first.Health.Current);
            Assert.AreEqual(0, Work.Queue.Orders.Count, "전투 시작 뒤 주문은 남지 않는다.");
        }

        [UnityTest]
        public IEnumerator 다_짓지_못한_대포는_전투에서_쏘지_않고_다음_준비에_끝까지_수리하면_완성된다()
        {
            yield return null;
            PlacedStructure support = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall
                && !world.Layout.IsCovered(piece.Placement) && world.CheckMount(Cannon, piece, "top") == MountIssue.None);
            PlacedSiegeWeapon cannon = Mount(Cannon, support);
            Assert.IsNotNull(cannon);
            Assert.IsTrue(cannon.IsUnfinished);
            Assert.IsTrue(State.TrySpendPrepTime(0.5f));
            yield return Navigation();
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Assert.IsTrue(cannon.IsUnfinished, "다 짓지 못한 대포는 전투를 넘겨도 미완성이다.");
            Assert.AreEqual(cannon.Health.Maximum * 0.5f, cannon.Health.Current, 0.01f);
            Assert.IsFalse(cannon.GetComponentInChildren<CannonModule>().Armed, "미완성 대포는 쏘지 않는다.");
            Assert.IsTrue(cannon.GetComponent<UnderConstructionView>().IsShowing);

            foreach (SiegeAttacker enemy in battle.Enemies) enemy.Actor.ReceiveDamage(enemy.Actor.Health.Current + 1f);
            for (int i = 0; i < 10 && GameSession.Flow.Phase == GamePhase.Battle; i++) yield return null;
            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return null; yield return null;
            Assert.IsTrue(cannon.IsUnfinished);
            Assert.AreEqual(1, economy.RepairAll());
            Assert.IsTrue(State.TryWait());
            Assert.IsFalse(cannon.IsUnfinished, "끝까지 수리하면 완성된다.");
            Assert.AreEqual(cannon.Health.Maximum, cannon.Health.Current);
            Assert.IsFalse(cannon.GetComponent<UnderConstructionView>().IsShowing);
            yield return Navigation();
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Assert.IsTrue(cannon.GetComponentInChildren<CannonModule>().Armed);
        }

        [UnityTest]
        public IEnumerator 수리는_주문_때_골드를_내고_전투_시작_때_못_한_만큼_돌려받으며_성문은_다_고쳐야_닫힌다()
        {
            yield return null;
            PlacedStructure damaged = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall);
            damaged.ReceiveDamage(damaged.Health.Maximum * 0.5f);
            PlacedStructure gate = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            gate.ReceiveDamage(gate.Health.Current);
            Assert.IsTrue(gate.Health.IsDead);

            int gold = State.Wallet.Gold, cost = economy.FullRepairCost;
            int wallCost = economy.Economy.RepairCost(damaged.Health.Maximum * 0.5f);
            Assert.AreEqual(2, economy.RepairAll());
            Assert.AreEqual(gold - cost, State.Wallet.Gold, "수리 골드는 주문 때 다 낸다.");
            Assert.AreEqual(0, economy.FullRepairCost, "주문이 걸린 조각은 다시 수리 목록에 오르지 않는다.");
            Assert.AreEqual(0, economy.RepairAll());

            // 성벽 수리는 0.5시간, 성문은 1시간. 0.25시간이면 성벽 50%, 성문 25%.
            Assert.IsTrue(State.TrySpendPrepTime(0.25f));
            Assert.IsTrue(gate.Health.IsDead, "성문은 수리가 끝나야 닫힌다.");
            int beforeBattle = State.Wallet.Gold;
            int gateCost = cost - wallCost;
            yield return Navigation();
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Assert.AreEqual(damaged.Health.Maximum * 0.75f, damaged.Health.Current, 0.01f, "50% → 반만 고쳐 75%");
            Assert.IsFalse(gate.Health.IsDead, "미완성 성문 수리는 전투 시작 때 도달한 체력으로 닫힌다.");
            Assert.AreEqual(gate.Health.Maximum * 0.25f, gate.Health.Current, 0.01f);
            int refund = Mathf.FloorToInt(wallCost * 0.5f + 0.0001f) + Mathf.FloorToInt(gateCost * 0.75f + 0.0001f);
            Assert.AreEqual(beforeBattle + refund, State.Wallet.Gold, "못 한 만큼 내림해 돌려받는다.");
        }

        [UnityTest]
        public IEnumerator 준비_중_저장하면_작업_주문이_성_기록에_순서대로_남는다()
        {
            yield return null;
            Vector2Int a = FreeCell(), b = FreeCell(a);
            PlacedStructure first = Build(a), second = Build(b);
            Assert.IsTrue(State.TrySpendPrepTime(0.25f));
            RunSnapshot saved = battle.RunLink.CaptureSnapshot();
            Assert.IsNotNull(saved);
            CastlePieceRecord One(Vector2Int cell) => saved.castle.pieces.Single(p => p.cellX == cell.x && p.cellY == cell.y && p.level == 0);
            CastlePieceRecord ra = One(a), rb = One(b);
            Assert.AreEqual((int)WorkKind.Build, ra.workKind);
            Assert.AreEqual((int)WorkKind.Build, rb.workKind);
            Assert.Less(ra.workOrder, rb.workOrder, "먼저 놓은 주문이 앞이다.");
            Assert.AreEqual(0.25f, ra.workDone, 0.0001f);
            Assert.AreEqual(0, saved.castle.pieces.Count(p => p.workKind != 0) - 2, "다른 조각에는 주문이 없다.");
            Assert.IsTrue(first.IsUnderConstruction && second.IsUnderConstruction);
        }
    }
}
