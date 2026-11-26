using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Squads;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>COMBAT-08 진형 규칙: 폭→한 줄 인원, 깊이로 줄 수 자르기, 대형 위치 맞추기, 선에서 방향 정하기, 부대별 진형 상태.</summary>
    public class FormationLayoutTests
    {
        [Test]
        public void Width_SetsFilesWithinHeadCount()
        {
            Assert.AreEqual(5, FormationLayout.FilesForWidth(5.6f, 1.4f, 20), "5.6m at 1.4m = 4 gaps = 5 files");
            Assert.AreEqual(1, FormationLayout.FilesForWidth(0f, 1.4f, 20));
            Assert.AreEqual(20, FormationLayout.FilesForWidth(100f, 1.4f, 20), "never wider than one rank");
            Assert.AreEqual(1, FormationLayout.FilesForWidth(5f, 1.4f, 0));
            Assert.AreEqual(5.6f, FormationLayout.WidthForFiles(5, 1.4f), 1e-4f);
        }

        [Test]
        public void OpenGround_KeepsRequestedShape()
        {
            Assert.AreEqual(new Vector2Int(5, 4), FormationLayout.Shape(20, 5, 1.4f, float.PositiveInfinity));
            Assert.AreEqual(new Vector2Int(5, 3), FormationLayout.Shape(11, 5, 1.4f, 100f), "last rank is partial");
        }

        [Test]
        public void NarrowGround_ClampsRanksAndWidensTheLine()
        {
            // 걸을 수 있는 깊이 1.6m(통로 2.6m에서 몸 반지름을 뺀 값), 간격 1.1m → 2줄까지.
            Assert.AreEqual(2, FormationLayout.MaxRanks(1.1f, 1.6f));
            Assert.AreEqual(new Vector2Int(10, 2), FormationLayout.Shape(20, 5, 1.1f, 1.6f), "4 ranks requested -> 2 ranks, twice as long");
            Assert.AreEqual(new Vector2Int(20, 1), FormationLayout.Shape(20, 5, 2.5f, 1.6f), "loose spacing fits one rank");
            Assert.AreEqual(new Vector2Int(7, 2), FormationLayout.Shape(13, 2, 1.1f, 1.6f), "odd head count rounds files up");
            Assert.AreEqual(new Vector2Int(5, 2), FormationLayout.Shape(10, 5, 1.1f, 1.6f), "already fits: unchanged");
            Assert.AreEqual(1, FormationLayout.MaxRanks(1.4f, 0f), "always at least one rank");
            Assert.AreEqual(2, FormationLayout.MaxRanks(1f, 0.96f), "tolerance absorbs rounding");
        }

        [Test]
        public void GameWallTop_RanksFollowTheWiderWalkway()
        {
            // COMBAT-15: Game 7.5m 성벽 통로(SIEGE-18)는 걸을 수 있는 깊이가 약 5.8m(Play 측정).
            // 줄 수 상한은 통로 폭을 그대로 따른다(사용자 결정 2026-11-26: 성벽 위 줄 수를 따로 막지 않음).
            const float depth = 5.8f;
            Assert.AreEqual(6, FormationLayout.MaxRanks(1.1f, depth), "line");
            Assert.AreEqual(7, FormationLayout.MaxRanks(0.9f, depth), "close");
            Assert.AreEqual(3, FormationLayout.MaxRanks(2.5f, depth), "loose");
            Assert.AreEqual(new Vector2Int(2, 6), FormationLayout.Shape(12, 2, 1.1f, depth), "default two files: six ranks still fit");
            Assert.AreEqual(new Vector2Int(4, 5), FormationLayout.Shape(20, 2, 1.1f, depth), "ten ranks requested -> widened to five");
            Assert.AreEqual(new Vector2Int(4, 3), FormationLayout.Shape(12, 2, 2.5f, depth), "loose: three ranks, four wide");
            Assert.AreEqual(new Vector2Int(12, 1), FormationLayout.Shape(12, 12, 1.1f, depth), "a dragged wide line stays one rank");
        }

        [Test]
        public void CenterOffset_FrontLineStaysOnTheDrawnLine_UnlessBlocked()
        {
            // 3줄, 간격 1 → 깊이 2. 앞줄 기준이면 가운데가 1m 뒤.
            Assert.AreEqual(-1f, FormationLayout.CenterOffset(3, 1f, true, float.PositiveInfinity, float.PositiveInfinity), 1e-4f);
            Assert.AreEqual(0f, FormationLayout.CenterOffset(3, 1f, false, float.PositiveInfinity, float.PositiveInfinity), 1e-4f);
            // 뒤로 0.5m밖에 없으면 앞으로 민다: 뒷줄이 -0.5m, 가운데 +0.5m.
            Assert.AreEqual(0.5f, FormationLayout.CenterOffset(3, 1f, true, 5f, 0.5f), 1e-4f);
            // 앞으로 막힌 가운데 기준 대형은 뒤로 민다.
            Assert.AreEqual(-0.7f, FormationLayout.CenterOffset(3, 1f, false, 0.3f, 5f), 1e-4f);
            // 깊이가 모자라면 걸을 수 있는 범위 가운데에 선다.
            Assert.AreEqual(0.25f, FormationLayout.CenterOffset(3, 1f, true, 1f, 0.5f), 1e-4f);
        }

        [Test]
        public void Slots_FirstRankIsTheFrontAndCentred()
        {
            List<Vector3> slots = FormationLayout.Slots(Vector3.zero, 0f, 4, 2, 2f);
            Assert.AreEqual(new Vector3(-1f, 0f, 1f), slots[0]);
            Assert.AreEqual(new Vector3(1f, 0f, 1f), slots[1]);
            Assert.AreEqual(new Vector3(-1f, 0f, -1f), slots[2]);
        }

        [Test]
        public void LineFacing_IsPerpendicularAndAwayFromTheViewer()
        {
            // 동서로 그은 선을 북쪽을 보며 그으면 북쪽(0도)을 본다. 어느 쪽에서 그었는지는 상관없다.
            Assert.AreEqual(0f, FormationLayout.FacingFromLine(new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f), Vector3.forward), 1e-3f);
            Assert.AreEqual(0f, FormationLayout.FacingFromLine(new Vector3(5f, 0f, 0f), new Vector3(-5f, 0f, 0f), Vector3.forward), 1e-3f);
            Assert.AreEqual(180f, Mathf.Abs(FormationLayout.FacingFromLine(Vector3.zero, Vector3.right, Vector3.back)), 1e-3f);
            Assert.AreEqual(90f, FormationLayout.FacingFromLine(Vector3.zero, Vector3.forward, new Vector3(1f, 0f, 0.2f)), 1e-3f);
            Assert.AreEqual(90f, FormationLayout.FacingFromLine(Vector3.zero, Vector3.zero, Vector3.right), 1e-3f, "no line: view yaw");
        }

        [Test]
        public void Segment_SplitsTheLineLeftToRight()
        {
            // 북쪽(0도)을 보면 왼쪽이 서쪽(-x)이다. 끝점 순서와 상관없이 0번 조각이 서쪽.
            FormationLayout.Segment(new Vector3(6f, 0f, 0f), new Vector3(-6f, 0f, 0f), 0f, 2, 0, out Vector3 from, out Vector3 to);
            Assert.AreEqual(new Vector3(-6f, 0f, 0f), from);
            Assert.AreEqual(Vector3.zero, to);
            FormationLayout.Segment(new Vector3(6f, 0f, 0f), new Vector3(-6f, 0f, 0f), 0f, 2, 1, out from, out to);
            Assert.AreEqual(new Vector3(6f, 0f, 0f), to);
        }

        [Test]
        public void AssignSlots_FrontGapIsFilledByOneRearUnit_OthersKeepTheirPosts()
        {
            // 2열 3줄(6명) 중 앞줄 왼쪽(0번 자리)이 죽었다. 남은 5명의 새 자리는 같은 격자의 앞 5칸이다.
            List<Vector3> full = FormationLayout.Slots(Vector3.zero, 0f, 6, 2, 1f);
            var posts = new List<Vector3> { full[1], full[2], full[3], full[4], full[5] };
            var hasPost = new List<bool> { true, true, true, true, true };
            List<Vector3> slots = FormationLayout.Slots(Vector3.zero, 0f, 5, 2, 1f);
            int[] assigned = FormationLayout.AssignSlots(posts, hasPost, slots, 0.3f);
            int moved = 0;
            for (int i = 0; i < posts.Count; i++)
            {
                Assert.GreaterOrEqual(assigned[i], 0);
                if (Vector3.Distance(posts[i], slots[assigned[i]]) > 0.3f) moved++;
            }
            Assert.AreEqual(new HashSet<int>(assigned).Count, posts.Count, "no two units share a slot");
            Assert.LessOrEqual(moved, 2, "only the rear moves up (the partial last rank re-centres)");
            Assert.Contains(0, assigned, "the front gap is filled");
        }

        [Test]
        public void AssignSlots_NewcomersTakeTheRemainingSlots()
        {
            List<Vector3> slots = FormationLayout.Slots(Vector3.zero, 0f, 4, 2, 1f);
            var posts = new List<Vector3> { slots[0], slots[1], slots[2], new Vector3(30f, 0f, 30f) };
            var hasPost = new List<bool> { true, true, true, false };
            int[] assigned = FormationLayout.AssignSlots(posts, hasPost, slots, 0.3f);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, assigned);
            int[] fewer = FormationLayout.AssignSlots(posts, hasPost, FormationLayout.Slots(Vector3.zero, 0f, 3, 2, 1f), 0.3f);
            Assert.AreEqual(-1, fewer[3], "more units than slots leaves the extra one unassigned");
        }

        [Test]
        public void Commander_KeepsFormationAndWidthPerSquad()
        {
            var commander = new SquadCommander(2);
            int changes = 0;
            commander.FormationChanged += _ => changes++;
            Assert.AreEqual(FormationKind.Line, commander.FormationOf(0));
            Assert.IsFalse(commander.HasWidth(0));
            Assert.IsTrue(commander.SetFormation(1, FormationKind.Loose));
            Assert.IsTrue(commander.SetWidth(1, 8f));
            Assert.IsTrue(commander.SetWidth(1, 8f), "same width is accepted without a change event");
            Assert.AreEqual(2, changes);
            Assert.AreEqual(FormationKind.Line, commander.FormationOf(0));
            Assert.AreEqual(8f, commander.WidthOf(1));
            Assert.IsFalse(commander.SetFormation(5, FormationKind.Close));

            int added = commander.AddSquad();
            Assert.AreEqual(FormationKind.Line, commander.FormationOf(added));
            Assert.IsFalse(commander.HasWidth(added));
            Assert.IsTrue(commander.RemoveSquad(0));
            Assert.AreEqual(FormationKind.Loose, commander.FormationOf(0), "state moves with the renumbered squad");
            Assert.AreEqual(8f, commander.WidthOf(0));
            Assert.IsTrue(commander.SetWidth(0, -3f));
            Assert.IsFalse(commander.HasWidth(0), "negative width returns to the default");
        }

        [Test]
        public void Commander_CyclesFormationsInOrder()
        {
            Assert.AreEqual(FormationKind.Close, SquadCommander.Next(FormationKind.Line));
            Assert.AreEqual(FormationKind.Loose, SquadCommander.Next(FormationKind.Close));
            Assert.AreEqual(FormationKind.Column, SquadCommander.Next(FormationKind.Loose));
            Assert.AreEqual(FormationKind.Line, SquadCommander.Next(FormationKind.Column));
        }

        [Test]
        public void Commander_IssueTo_IgnoresSelectionAndKeepsFrontAnchor()
        {
            var commander = new SquadCommander(2);
            Assert.IsTrue(commander.IssueTo(1, new SquadOrder(SquadOrderKind.Move, Vector3.one, 90f, true)));
            Assert.AreEqual(SquadOrderKind.Move, commander.OrderOf(1).Kind);
            Assert.AreEqual(SquadOrderKind.Hold, commander.OrderOf(0).Kind);
            Assert.IsTrue(commander.CompleteMove(1));
            Assert.IsTrue(commander.OrderOf(1).FrontAnchored, "arrival keeps the front-line anchor");
            commander.OrderRequested += (_, __) => false;
            Assert.IsFalse(commander.IssueTo(0, new SquadOrder(SquadOrderKind.Move, Vector3.one)));
            Assert.IsFalse(commander.IssueTo(7, new SquadOrder(SquadOrderKind.Move, Vector3.one)));
        }

        [Test]
        public void Settings_SpacingPerFormation_FallsBackForOldAssets()
        {
            var settings = SquadCommandSettings.Create(2, 1.1f);
            try
            {
                Assert.AreEqual(1.1f, settings.Spacing(FormationKind.Line));
                Assert.AreEqual(1.1f, settings.Spacing(FormationKind.Column));
                Assert.AreEqual(SquadCommandSettings.DefaultCloseSpacing, settings.Spacing(FormationKind.Close));
                Assert.AreEqual(SquadCommandSettings.DefaultLooseSpacing, settings.Spacing(FormationKind.Loose));
                settings.ConfigureFormations(0f, 0f, 0);
                Assert.AreEqual(SquadCommandSettings.DefaultLooseSpacing, settings.Spacing(FormationKind.Loose), "0 reads as default");
                Assert.AreEqual(SquadCommandSettings.DefaultColumnFiles, settings.ColumnFiles);
                Assert.AreEqual("<Keyboard>/g", SquadCommandSettings.DefaultCycleFormationBinding);
                Assert.AreEqual("<Mouse>/rightButton", SquadCommandSettings.DefaultDragFormationBinding);
            }
            finally { Object.DestroyImmediate(settings); }
        }
    }
}
