using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    public class GameBuildEconomyPlayTests
    {
        static int cleanup;
        GameBuildEconomy economy;
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
            world = Object.FindAnyObjectByType<ConstructionWorld>();
            controller = Object.FindAnyObjectByType<PlacementController>(FindObjectsInactive.Include);
            wall = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameBuildEconomyCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        Wallet Wallet => GameSession.Flow.Run.State.Wallet;

        // 준비 시간이 켜진 Game 씬에서 수리는 작업 주문이다(SIEGE-08). 한 시간 기다려 주문을 끝낸다.
        static void FinishWork()
        {
            var state = GameSession.Flow.Run.State;
            Assert.IsTrue(state.TryWait(), "주문을 끝낼 준비 시간이 남아 있어야 한다.");
        }

        Vector2Int FreeCell()
        {
            for (int x = 30; x < 50; x++)
                for (int z = 31; z < 50; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (world.Layout.ColumnHeight(cell) != 0 || world.CheckPlacement(wall, cell, 0) != PlacementIssue.None) continue;
                    // 유닛과 겹쳐 지으면 유닛이 위로 올라가 철거가 막히므로 유닛에서 떨어진 칸을 쓴다.
                    Vector3 center = world.WorldCenter(wall, cell, 0);
                    if (ProjectNeo.Combat.CharacterActor.ActiveActors.Any(actor =>
                        Vector3.Distance(actor.transform.position, center) < 6f)) continue;
                    return cell;
                }
            Assert.Fail("빈 칸이 없다.");
            return default;
        }

        PlacedStructure Build(Vector2Int cell)
        {
            controller.Select(wall);
            controller.UpdatePreview(world.WorldCenter(wall, cell, 0));
            Assert.AreEqual(PlacementIssue.None, controller.Issue);
            if (!controller.Confirm()) return null;
            return world.Structures.Single(piece => piece.Placement.Cell == cell && piece.Placement.Level == 0);
        }

        [UnityTest]
        public IEnumerator 시작_골드로_짓고_이번_준비에_지은_것은_전액_지난_것은_절반만_돌려받는다()
        {
            Assert.IsNotNull(economy, "Game 씬 전투 오브젝트에 건설 비용 컴포넌트가 있어야 한다.");
            int cost = economy.Table.CostOf(wall);
            Assert.Greater(cost, 0);
            Assert.AreEqual(100, Wallet.Gold, "시작 골드(RunStart.asset)");

            PlacedStructure first = Build(FreeCell());
            Assert.IsNotNull(first);
            Assert.AreEqual(100 - cost, Wallet.Gold);
            Assert.IsTrue(economy.Dismantle(first));
            Assert.AreEqual(100, Wallet.Gold, "이번 준비에 지은 것은 전액 환불");
            yield return null;

            PlacedStructure second = Build(FreeCell());
            economy.Economy.BeginPreparation();
            Assert.IsTrue(economy.Dismantle(second));
            Assert.AreEqual(100 - cost + Mathf.FloorToInt(cost * economy.Table.OlderRefundRatio), Wallet.Gold);

            PlacedStructure gate = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            Assert.IsFalse(economy.Dismantle(gate), "성문은 철거하지 않는다.");
        }

        [UnityTest]
        public IEnumerator 골드가_모자라면_짓지_않고_모두_수리는_깎인_만큼_골드를_내고_가득_채운다()
        {
            yield return null;
            Assert.IsTrue(Wallet.TrySpend(new Cost(Wallet.Gold)));
            Vector2Int cell = FreeCell();
            Assert.IsNull(Build(cell), "골드가 없으면 설치되지 않는다.");
            Assert.AreEqual(0, world.Layout.ColumnHeight(cell));
            controller.Cancel();

            PlacedStructure damaged = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall);
            damaged.ReceiveDamage(50f);
            int repair = economy.FullRepairCost;
            Assert.AreEqual(economy.Economy.RepairCost(50f), repair);
            Assert.AreEqual(0, economy.RepairAll(), "골드가 없으면 수리하지 않는다.");
            Assert.Less(damaged.Health.Current, damaged.Health.Maximum);

            Wallet.AddGold(repair + 3);
            Assert.AreEqual(1, economy.RepairAll());
            Assert.Less(damaged.Health.Current, damaged.Health.Maximum, "수리는 주문이라 시간이 가야 채워진다.");
            FinishWork();
            Assert.AreEqual(damaged.Health.Maximum, damaged.Health.Current);
            Assert.AreEqual(3, Wallet.Gold);
            Assert.AreEqual(0, economy.FullRepairCost);
        }

        [UnityTest]
        public IEnumerator 모두_수리는_깎인_대포도_함께_고친다()
        {
            yield return null;
            Assert.IsTrue(Wallet.TrySpend(new Cost(Wallet.Gold)));
            SiegeWeaponDefinition cannon = economy.Table.Weapons.Select(entry => entry.weapon)
                .FirstOrDefault(definition => definition != null && definition.Id == "cannon");
            Assert.IsNotNull(cannon, "Game 건설 비용표에 대포가 있어야 한다.");
            // 무기는 보루에만 놓는다(SIEGE-11).
            PlacedStructure support = BastionTestSupport.PlaceBesideWalls(world,
                BastionTestSupport.Definition(SceneManager.GetActiveScene()), 1)[0];
            Assert.IsTrue(world.TryMount(cannon, support, "top", out PlacedSiegeWeapon weapon));
            Assert.AreEqual(150f, weapon.Health.Maximum, "Q-21 대포 체력");
            PlacedStructure wallPiece = world.Structures.First(piece => piece != support
                && piece.Placement.Definition.Kind == BuildPieceKind.Wall);
            weapon.ReceiveDamage(60f);
            wallPiece.ReceiveDamage(20f);

            int repair = economy.FullRepairCost;
            Assert.AreEqual(economy.Economy.RepairCost(60f) + economy.Economy.RepairCost(20f), repair);
            Wallet.AddGold(economy.Economy.RepairCost(60f));
            Assert.AreEqual(1, economy.RepairAll(), "골드가 모자라면 더 많이 깎인 대포부터 고친다.");
            FinishWork();
            Assert.AreEqual(150f, weapon.Health.Current);
            Assert.Less(wallPiece.Health.Current, wallPiece.Health.Maximum);

            Wallet.AddGold(economy.Economy.RepairCost(20f));
            Assert.AreEqual(1, economy.RepairAll());
            FinishWork();
            Assert.AreEqual(wallPiece.Health.Maximum, wallPiece.Health.Current);
            Assert.AreEqual(0, economy.FullRepairCost);
        }

        [UnityTest]
        public IEnumerator 전투_중에는_철거와_수리를_하지_않는다()
        {
            yield return null;
            PlacedStructure piece = world.Structures.First(p => p.Placement.Definition.Kind == BuildPieceKind.Wall);
            piece.ReceiveDamage(10f);
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<GameBattleController>().TryStartBattle());
            int gold = Wallet.Gold;
            Assert.IsFalse(economy.Dismantle(piece));
            Assert.AreEqual(0, economy.RepairAll());
            Assert.AreEqual(gold, Wallet.Gold);
        }

        [UnityTest]
        public IEnumerator 모두_수리는_파괴된_성문_문짝도_복구한다()
        {
            yield return null;
            PlacedStructure gate = world.Structures.Single(p => p.Placement.Definition.Kind == BuildPieceKind.Gate);
            gate.ReceiveDamage(gate.Health.Current);
            Assert.IsTrue(gate.Health.IsDead);
            Assert.IsFalse(gate.transform.Find("Visual/Door").gameObject.activeSelf);

            int repairCost = economy.FullRepairCost;
            Assert.Greater(repairCost, 0);
            Wallet.AddGold(repairCost);
            Assert.AreEqual(1, economy.RepairAll());
            Assert.IsTrue(gate.Health.IsDead, "성문은 수리가 끝날 때 닫힌다(SIEGE-08).");
            FinishWork();
            Assert.IsFalse(gate.Health.IsDead);
            Assert.AreEqual(gate.Health.Maximum, gate.Health.Current);
            Assert.IsTrue(gate.transform.Find("Visual/Door").gameObject.activeSelf);
            Assert.AreEqual(0, economy.FullRepairCost);
        }
    }
}
