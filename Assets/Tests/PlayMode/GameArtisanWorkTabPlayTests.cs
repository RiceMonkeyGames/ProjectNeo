using System.Collections;
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
    /// PREP-11 Game 씬 작업 탭(B안): 끝나지 않은 주문 카드(작업 중 / 대기), 우선순위 옮기기, 장인 고용, 취소 환불,
    /// 대장간 시간이 장인 하나를 빼는 것, 시계 줄 장인 수.
    /// </summary>
    public class GameArtisanWorkTabPlayTests
    {
        static int cleanup;
        GameBuildEconomy economy;
        ConstructionWorld world;
        PlacementController controller;
        ConstructionBarHud bar;
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
            bar = Object.FindAnyObjectByType<ConstructionBarHud>(FindObjectsInactive.Include);
            wall = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
            Assert.IsNotNull(State.PrepTime, "Game 씬은 준비 시간이 켜져 있어야 한다(PREP-06).");
            Assert.IsNotNull(bar, "Game 씬에 건설 막대가 있어야 한다(UI-13).");
            State.Wallet.AddGold(1000);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameArtisanWorkTabCleanup" + cleanup++);
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

        PlacedStructure Build(Vector2Int cell)
        {
            controller.Select(wall);
            controller.UpdatePreview(world.WorldCenter(wall, cell, 0));
            if (controller.Issue != PlacementIssue.None || !controller.Confirm()) return null;
            return world.Structures.Single(piece => piece.Placement.Cell == cell && piece.Placement.Level == 0);
        }

        PlacedStructure[] BuildThree()
        {
            Vector2Int a = FreeCell(), b = FreeCell(a), c = FreeCell(a, b);
            PlacedStructure[] built = { Build(a), Build(b), Build(c) };
            Assert.IsTrue(built.All(piece => piece != null));
            controller.Cancel();
            return built;
        }

        [UnityTest]
        public IEnumerator 작업_탭은_장인이_맡은_주문과_대기_주문을_순서대로_보이고_고용하면_대기가_줄어든다()
        {
            yield return null;
            int start = State.ArtisanCount;
            Assert.AreEqual(2, start, "Game 씬 런은 장인 2명으로 시작한다.");
            BuildThree();
            Assert.IsTrue(Object.FindAnyObjectByType<GamePhaseView>().EnterConstruction());
            yield return null;
            bar.Show(ConstructionBarHud.Category.Work);
            yield return null;

            Assert.AreEqual(3, bar.CardCount);
            Assert.AreEqual("작업 중", bar.CardTag(0));
            Assert.AreEqual("작업 중", bar.CardTag(1));
            Assert.AreEqual("대기", bar.CardTag(2), "장인 2명이면 셋째 주문은 기다린다.");
            Assert.AreEqual("0%", bar.CardSub(0));
            Assert.AreEqual($"작업 {start}/{State.ArtisanCap}", bar.WorkTabCaption);
            Assert.AreEqual($"장인 고용 · {State.NextArtisanHireGold}골드", bar.HireCaption);

            int gold = State.Wallet.Gold, hireGold = State.NextArtisanHireGold;
            Assert.IsTrue(bar.Hire());
            yield return null;
            Assert.AreEqual(start + 1, State.ArtisanCount);
            Assert.AreEqual(gold - hireGold, State.Wallet.Gold);
            Assert.AreEqual("작업 중", bar.CardTag(2), "고용한 장인이 대기 주문을 맡는다.");
            StringAssert.StartsWith("장인 가득", bar.HireCaption, "대장간 Lv1 상한이면 강화 안내를 보인다.");
            Assert.AreEqual(ArtisanHireCheck.AtCap, State.CheckHireArtisan());

            var clock = Object.FindAnyObjectByType<PrepClockHud>();
            Assert.IsNotNull(clock);
            Assert.AreEqual(PrepClockHud.DescribeArtisans(State), clock.ArtisanText, "시계 줄에 장인 수가 보인다.");
        }

        [UnityTest]
        public IEnumerator 고른_주문을_앞으로_옮기면_자리를_맡고_취소하면_전액_환불된다()
        {
            yield return null;
            PlacedStructure[] built = BuildThree();
            Assert.IsTrue(Object.FindAnyObjectByType<GamePhaseView>().EnterConstruction());
            yield return null;
            bar.Show(ConstructionBarHud.Category.Work);
            yield return null;

            WorkOrder last = Work.OrderOf(built[2]);
            Assert.IsFalse(Work.IsAssigned(last));
            Assert.IsTrue(bar.Choose(2));
            Assert.AreSame(last, bar.SelectedOrder);
            Assert.IsTrue(bar.MoveSelected(true));
            Assert.IsTrue(bar.MoveSelected(true));
            yield return null;
            Assert.AreSame(last, Work.Queue.Orders[0], "두 번 앞으로 옮기면 맨 앞이다.");
            Assert.IsTrue(Work.IsAssigned(last));
            Assert.AreEqual("대기", bar.CardTag(2), "밀려난 주문이 대기로 간다.");

            int gold = State.Wallet.Gold, cost = economy.Table.CostOf(wall);
            Assert.IsTrue(bar.CancelSelected(), "고른 주문은 앞으로 옮긴 뒤에도 고른 채다.");
            yield return null;
            Assert.AreEqual(gold + cost, State.Wallet.Gold, "짓는 중인 조각 취소는 전액 환불이다.");
            Assert.IsFalse(world.Structures.Contains(built[2]));
            Assert.AreEqual(2, bar.CardCount);
            Assert.IsNull(bar.SelectedOrder);
        }

        [UnityTest]
        public IEnumerator 대장간_시간은_장인_하나를_빼서_뒤쪽_주문이_쉰다()
        {
            yield return null;
            PlacedStructure[] built = BuildThree();
            State.BeginPreparationClockIfNeeded();
            Assert.IsTrue(State.TryUpgradeFacility(ProjectNeo.Facilities.FacilityType.Shop) || State.TryUpgradeFacility(ProjectNeo.Facilities.FacilityType.Tavern),
                "시설 하나는 올릴 수 있어야 한다.");
            yield return null;
            Assert.IsNull(Work.OrderOf(built[0]), "맨 앞 주문은 시설 강화 한 시간 동안 남은 장인 1명이 끝낸다.");
            Assert.IsFalse(built[0].IsUnderConstruction);
            Assert.IsTrue(built[1].IsUnderConstruction, "장인 하나가 시설 강화에 가서 둘째 주문은 쉰다.");
            Assert.AreEqual(0f, Work.OrderOf(built[1]).Progress, 0.0001f);
        }
    }
}
