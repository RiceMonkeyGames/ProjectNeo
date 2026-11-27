using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// SIEGE-24 Game 씬 망루(사용자 결정 2026-11-27): 성벽 줄 두 칸을 바꿔 끼우고 바깥으로 내밀어 2층을 한 번에 짓고,
    /// 층마다 비용·작업 주문을 내며, 바꾼 성벽은 환불하고, 한 층씩 더 쌓거나 해체하며, 지붕 효과와 런 저장 기록이 붙는다.
    /// </summary>
    public class GameTowerPlayTests
    {
        static int cleanup;
        GameBuildEconomy economy;
        GameBattleController battle;
        ConstructionWorld world;
        PlacementController controller;
        BuildPieceDefinition tower;
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
            tower = Object.FindObjectsByType<ConstructionHud>(FindObjectsInactive.Include)
                .Select(hud => hud.Tower).FirstOrDefault(piece => piece != null);
            Assert.IsNotNull(tower, "Run ProjectNeo/Setup/Apply Towers To Game (SIEGE-24).");
            State.Wallet.AddGold(1000);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameTowerCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        RunState State => GameSession.Flow.Run.State;
        GameConstructionWork Work => economy.Work;

        // 남쪽 성벽(바깥 = 남)부터 망루를 놓을 수 있고 근처에 유닛이 없는 자리를 찾는다.
        Vector2Int FindSpot(out List<BuildPlacement> replaced)
        {
            replaced = new List<BuildPlacement>();
            var size = new Vector2Int(2, 2);
            foreach (Vector2Int anchor in Cells().OrderBy(cell => cell.y).ThenBy(cell => Mathf.Abs(cell.x - world.Grid.Width / 2)))
            {
                if (world.CheckPlacement(tower, anchor, 0) != PlacementIssue.None) continue;
                Vector3 center = world.WorldCenter(tower, anchor, 0);
                if (ProjectNeo.Combat.CharacterActor.ActiveActors.Any(actor => actor != null
                    && Vector2.Distance(new Vector2(actor.transform.position.x, actor.transform.position.z), new Vector2(center.x, center.z)) < 14f)) continue;
                world.CollectReplaced(tower, anchor, 0, replaced);
                Assert.AreEqual(size, BuildLayout.RotatedSize(tower.Footprint, 0));
                return anchor;
            }
            Assert.Fail("No wall-line spot for a tower.");
            return default;
        }

        IEnumerable<Vector2Int> Cells()
        {
            for (int x = 0; x < world.Grid.Width - 1; x++)
                for (int z = 0; z < world.Grid.Depth - 1; z++) yield return new Vector2Int(x, z);
        }

        // 건설 화면처럼 그 자리 가운데를 가리키고 놓는다.
        bool Place(Vector2Int anchor)
        {
            controller.Select(tower);
            controller.UpdatePreview(world.WorldCenter(tower, anchor, 0));
            Assert.AreEqual(anchor, controller.PreviewCell, "the preview snaps to the pointed tower spot");
            bool placed = controller.Issue == PlacementIssue.None && controller.Confirm();
            controller.Cancel();
            return placed;
        }

        // 망루 층 작업이 다 끝날 때까지 기다린다(한 번 기다리면 1시간).
        void FinishWork(Vector2Int anchor)
        {
            for (int i = 0; i < 6 && Tiers(anchor).Any(piece => piece.IsUnderConstruction); i++) Assert.IsTrue(State.TryWait());
        }

        PlacedStructure[] Tiers(Vector2Int anchor) => world.Structures
            .Where(piece => piece.Placement.Definition == tower && piece.Placement.Cell == anchor)
            .OrderBy(piece => piece.Placement.Level).ToArray();

        [UnityTest]
        public IEnumerator 망루는_성벽_줄_두_칸을_바꿔_끼우고_2층을_층마다_값을_내며_짓는다()
        {
            yield return null;
            int cost = economy.Table.CostOf(tower);
            Assert.AreEqual(60, cost, "층마다 60골드(임시 값)");
            Assert.AreEqual(2, tower.InitialTiers);
            Vector2Int anchor = FindSpot(out List<BuildPlacement> replaced);
            Assert.AreEqual(2, replaced.Select(piece => piece.Cell).Distinct().Count(), "성벽 두 칸을 바꿔 끼운다.");
            Assert.IsTrue(replaced.All(piece => piece.Definition.Kind == BuildPieceKind.Wall));
            int refund = replaced.Sum(piece => economy.Economy.RefundFor(piece.Definition, piece.Id));
            HashSet<Vector2Int> wallCells = new HashSet<Vector2Int>(replaced.Select(piece => piece.Cell));
            int gold = State.Wallet.Gold;

            controller.Select(tower);
            controller.UpdatePreview(world.WorldCenter(tower, anchor, 0));
            Assert.AreEqual(2, controller.PreviewCount, "미리보기는 2층을 한 번에 쌓는다고 알린다.");
            controller.Cancel();
            Assert.IsTrue(Place(anchor));

            PlacedStructure[] tiers = Tiers(anchor);
            Assert.AreEqual(2, tiers.Length, "2층을 한 번에 쌓는다.");
            Assert.AreEqual(new[] { 0, 1 }, tiers.Select(piece => piece.Placement.Level).ToArray());
            Assert.AreEqual(400f, tiers[0].Health.Maximum, "층마다 체력 400(임시 값)");
            foreach (Vector2Int cell in wallCells)
                Assert.AreEqual(tower, world.Layout.At(cell, 0).Definition, "바꿔 끼운 칸에는 망루가 선다.");
            Assert.AreEqual(gold - cost * 2 + refund, State.Wallet.Gold, "층마다 값을 내고 바꾼 성벽은 해체처럼 환불한다.");
            Assert.IsTrue(tiers.All(piece => piece.IsUnderConstruction), "층마다 작업 주문을 건다.");
            StringAssert.Contains("작업 주문 2", Work.Summary());
            StringAssert.Contains("망루로 바꿈", economy.Message);

            FinishWork(anchor);
            Assert.IsTrue(tiers.All(piece => !piece.IsUnderConstruction), "두 층 모두 완성된다.");

            // 한 층 더 쌓기: 지붕을 가리키면 그 망루 위 한 층이다.
            controller.Select(tower);
            controller.UpdatePreview(world.WorldCenter(tower, anchor, 0) + Vector3.up * 10f);
            Assert.AreEqual(anchor, controller.PreviewCell);
            Assert.AreEqual(PlacementIssue.None, controller.Issue);
            Assert.AreEqual(2, controller.PreviewLevel);
            Assert.AreEqual(1, controller.PreviewCount, "망루 위에는 한 층씩 쌓는다.");
            gold = State.Wallet.Gold;
            Assert.IsTrue(controller.Confirm());
            controller.Cancel();
            Assert.AreEqual(3, Tiers(anchor).Length);
            Assert.AreEqual(gold - cost, State.Wallet.Gold);

            // 해체(사용자 결정 2026-11-27): 더 쌓은 층은 맨 위부터 한 층씩, 처음 2층만 남으면 2층을 한 번에. 어느 층을 가리켜도 맨 위부터다.
            // 이번 준비에 지은 층은 전액 환불한다.
            gold = State.Wallet.Gold;
            CollectionAssert.AreEqual(new[] { Tiers(anchor)[2] }, economy.DismantleGroup(Tiers(anchor)[0]), "더 쌓은 층은 맨 위 한 층");
            Assert.IsTrue(economy.Dismantle(Tiers(anchor)[0]), "아래층을 가리켜도 맨 위층을 해체한다.");
            Assert.AreEqual(2, Tiers(anchor).Length);
            Assert.AreEqual(gold + cost, State.Wallet.Gold);
            Assert.AreEqual(2, economy.DismantleGroup(Tiers(anchor)[1]).Count, "처음 2층은 함께");
            Assert.IsTrue(economy.Dismantle(Tiers(anchor)[1]));
            Assert.AreEqual(0, Tiers(anchor).Length, "처음 2층은 한 번에 해체한다.");
            Assert.AreEqual(gold + cost * 3, State.Wallet.Gold);
            StringAssert.Contains("2층 해체", economy.Message);
        }

        [UnityTest]
        public IEnumerator 성벽_안쪽이나_성_바깥_빈_땅에는_놓지_않고_골드가_모자라면_놓지_않는다()
        {
            yield return null;
            Vector2Int anchor = FindSpot(out _);
            Vector2Int courtyard = new Vector2Int(world.Grid.Width / 2, world.Grid.Depth / 2);
            Assert.AreEqual(PlacementIssue.TowerNeedsWall, world.CheckPlacement(tower, courtyard, 0), "안뜰 한가운데");
            Assert.AreEqual(PlacementIssue.TowerNeedsWall, world.CheckPlacement(tower, new Vector2Int(1, 1), 0), "성 바깥 빈 땅");

            State.Wallet.TrySpend(new ProjectNeo.Economy.Cost(State.Wallet.Gold));
            State.Wallet.AddGold(economy.Table.CostOf(tower) * 2 - 1);
            Assert.IsFalse(Place(anchor), "2층 값이 모자라면 놓지 않는다.");
            Assert.AreEqual(0, Tiers(anchor).Length);
            StringAssert.Contains("골드가 부족", economy.Message);
        }

        [UnityTest]
        public IEnumerator 망루_지붕_효과가_붙고_런_저장에_층마다_기록되며_되살리면_같은_망루가_선다()
        {
            yield return null;
            Assert.IsNotNull(world.GetComponent<TowerOccupantBuff>(), "Game 성에 망루 지붕 효과가 붙어 있다.");
            Assert.AreEqual(2, tower.OccupantModifiers.Count, "지붕 위 아군 원거리 사거리·공격력 +20%");
            Vector2Int anchor = FindSpot(out _);
            Assert.IsTrue(Place(anchor));
            FinishWork(anchor);

            Vector3 roof = world.WorldCenter(tower, anchor, 0) + world.transform.up * (tower.Height * 2f);
            Assert.IsTrue(TowerRules.StandingOn(world.Layout, world.LocalPoint(roof), 0.6f, out BuildPlacement top));
            Assert.AreEqual(tower, top.Definition, "지붕 위에 서면 망루 효과 판정이 된다.");

            RunSnapshot snapshot = battle.RunLink.CaptureSnapshot();
            RunContentCatalog content = battle.Settings.RunContent;
            List<CastlePieceRecord> records = snapshot.castle.pieces
                .Where(piece => content.Piece(piece.id) == tower).ToList();
            Assert.AreEqual(2, records.Count, "층마다 기록한다.");
            Assert.IsTrue(records.All(piece => piece.value.gold == 60), "층마다 값 60을 기록한다.");

            // 되살리기: 아래층부터 다시 놓으면 망루와 이웃 성벽이 그대로 선다.
            var saved = world.Layout.Placements.OrderBy(piece => piece.Level).ThenBy(piece => piece.Id)
                .Select(piece => (piece.Definition, piece.Cell, piece.Rotation, piece.IsFixed)).ToList();
            world.ClearForRestore();
            foreach (var piece in saved)
                Assert.IsTrue(world.TryPlaceRestored(piece.Definition, piece.Cell, piece.Rotation, piece.IsFixed, out _), piece.Cell.ToString());
            Assert.AreEqual(2, world.Layout.Placements.Count(piece => piece.Definition == tower && piece.Cell == anchor));
        }

        [UnityTest]
        public IEnumerator 무너진_망루는_성벽_줄에_틈을_남기고_다시_성벽을_지으면_망루를_또_놓을_수_있다()
        {
            yield return null;
            Vector2Int anchor = FindSpot(out List<BuildPlacement> replaced);
            List<Vector2Int> line = replaced.Select(piece => piece.Cell).Distinct().ToList();
            Assert.IsTrue(world.TryPlace(tower, anchor, 0, out PlacedStructure _));
            world.Collapse(Tiers(anchor)[0]);
            Assert.AreEqual(0, Tiers(anchor).Length, "아래층이 무너지면 위층도 함께 무너진다.");
            BuildPieceDefinition wall = replaced[0].Definition;
            foreach (Vector2Int cell in line)
                Assert.AreEqual(PlacementIssue.None, world.CheckPlacement(wall, cell, 0), "무너진 자리는 성벽 줄의 빈 틈이다.");
            foreach (Vector2Int cell in line) Assert.IsTrue(world.TryPlace(wall, cell, 0, out _));
            Assert.AreEqual(PlacementIssue.None, world.CheckPlacement(tower, anchor, 0), "성벽을 다시 지으면 망루를 또 놓을 수 있다.");
        }
    }
}
