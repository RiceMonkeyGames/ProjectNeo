using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Squads;
using UnityEngine;

namespace ProjectNeo.Tests
{
    public class SquadCommanderTests
    {
        [Test]
        public void RejectedOrder_PreservesState_AndArrivalDoesNotReroll()
        {
            var commander = new SquadCommander(2);
            commander.SelectAll();
            int calls = 0, changes = 0;
            System.Func<int, SquadOrder, bool> filter = (squad, order) => { calls++; return squad == 1; };
            commander.OrderRequested += filter;
            commander.OrderChanged += (_, __) => changes++;
            Assert.AreEqual(1, commander.Issue(new SquadOrder(SquadOrderKind.Move, Vector3.forward)));
            Assert.AreEqual(2, calls);
            Assert.AreEqual(1, changes);
            Assert.AreEqual(SquadOrderKind.Hold, commander.OrderOf(0).Kind);
            Assert.AreEqual(SquadOrderKind.Move, commander.OrderOf(1).Kind);
            Assert.IsTrue(commander.CompleteMove(1));
            Assert.AreEqual(2, calls, "도착 처리는 새 명령 판정을 하지 않는다.");
            commander.OrderRequested -= filter;
            Assert.AreEqual(2, commander.Issue(new SquadOrder(SquadOrderKind.Attack)));
        }

        [Test]
        public void Squads_StartHolding_WithNothingSelected()
        {
            var commander = new SquadCommander(3);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(SquadOrderKind.Hold, commander.OrderOf(i).Kind);
                Assert.IsFalse(commander.IsSelected(i));
            }
            Assert.AreEqual(0, commander.Issue(new SquadOrder(SquadOrderKind.Attack)));
            Assert.AreEqual(SquadOrderKind.Hold, commander.OrderOf(0).Kind);
        }

        [Test]
        public void RemoveSquad_ShiftsLaterOrdersAndSelection_AndRejectsMissingSquad()
        {
            var commander = new SquadCommander(3);
            commander.Select(2);
            commander.Issue(new SquadOrder(SquadOrderKind.Attack));
            int removed = -1;
            commander.SquadRemoved += index => removed = index;

            Assert.IsTrue(commander.RemoveSquad(1));
            Assert.AreEqual(1, removed);
            Assert.AreEqual(2, commander.SquadCount);
            Assert.AreEqual(SquadOrderKind.Attack, commander.OrderOf(1).Kind, "3번 부대의 지시가 2번으로 당겨진다.");
            Assert.IsTrue(commander.IsSelected(1));
            Assert.AreEqual(SquadOrderKind.Hold, commander.OrderOf(0).Kind);
            Assert.IsFalse(commander.RemoveSquad(2));
            Assert.IsFalse(commander.RemoveSquad(-1));
        }

        [Test]
        public void AddSquad_AppendsHoldingUnselectedSquad_AndKeepsExistingOrders()
        {
            var commander = new SquadCommander(2);
            commander.Select(0);
            commander.Issue(new SquadOrder(SquadOrderKind.Attack));
            int added = -1;
            commander.SquadAdded += index => added = index;

            Assert.AreEqual(2, commander.AddSquad());
            Assert.AreEqual(2, added);
            Assert.AreEqual(3, commander.SquadCount);
            Assert.AreEqual(SquadOrderKind.Attack, commander.OrderOf(0).Kind);
            Assert.IsTrue(commander.IsSelected(0));
            Assert.AreEqual(SquadOrderKind.Hold, commander.OrderOf(2).Kind);
            Assert.IsFalse(commander.IsSelected(2));
            Assert.IsTrue(commander.Select(2));
        }

        [Test]
        public void Select_PicksExactlyOneSquad_AndRejectsMissingSquad()
        {
            var commander = new SquadCommander(3);
            Assert.IsTrue(commander.Select(1));
            Assert.IsTrue(commander.Select(2));
            Assert.IsFalse(commander.IsSelected(1));
            Assert.IsTrue(commander.IsSelected(2));
            Assert.IsFalse(commander.Select(3));
            Assert.IsFalse(commander.Select(-1));
            Assert.IsTrue(commander.IsSelected(2), "없는 부대를 골라도 기존 선택은 유지한다.");
            Assert.AreEqual(1, commander.SelectedCount);
        }

        [Test]
        public void Issue_ChangesOnlySelectedSquads_AndReportsEachChange()
        {
            var commander = new SquadCommander(3);
            var changed = new List<int>();
            commander.OrderChanged += (squad, _) => changed.Add(squad);
            commander.Select(1);
            var destination = new Vector3(4f, 0f, 9f);
            Assert.AreEqual(1, commander.Issue(new SquadOrder(SquadOrderKind.Move, destination, 90f)));
            Assert.AreEqual(SquadOrderKind.Hold, commander.OrderOf(0).Kind);
            Assert.AreEqual(SquadOrderKind.Move, commander.OrderOf(1).Kind);
            Assert.AreEqual(destination, commander.OrderOf(1).Destination);
            Assert.AreEqual(90f, commander.OrderOf(1).Facing);
            Assert.IsTrue(commander.OrderOf(1).HasDestination);
            commander.SelectAll();
            Assert.AreEqual(3, commander.Issue(new SquadOrder(SquadOrderKind.Attack)));
            Assert.IsFalse(commander.OrderOf(1).HasDestination, "지점 없는 공격은 제자리 교전이다.");
            commander.Issue(new SquadOrder(SquadOrderKind.Attack, destination));
            Assert.IsTrue(commander.OrderOf(2).HasDestination);
            Assert.AreEqual(SquadOrderKind.Attack, commander.OrderOf(2).Kind);
            changed.RemoveRange(4, 3);
            CollectionAssert.AreEqual(new[] { 1, 0, 1, 2 }, changed);
            commander.ClearSelection();
            Assert.AreEqual(0, commander.SelectedCount);
        }

        [Test]
        public void MoveBecomesHoldOnArrival_ButAttackStaysUntilReplaced()
        {
            var commander = new SquadCommander(2);
            commander.SelectAll();
            commander.Issue(new SquadOrder(SquadOrderKind.Attack));
            Assert.IsFalse(commander.CompleteMove(0), "공격 지시는 도착으로 끝나지 않는다.");
            Assert.AreEqual(SquadOrderKind.Attack, commander.OrderOf(0).Kind);
            commander.Select(0);
            var destination = new Vector3(1f, 2f, 3f);
            commander.Issue(new SquadOrder(SquadOrderKind.Move, destination));
            Assert.IsTrue(commander.CompleteMove(0));
            Assert.AreEqual(SquadOrderKind.Hold, commander.OrderOf(0).Kind);
            Assert.AreEqual(destination, commander.OrderOf(0).Destination);
            Assert.IsFalse(commander.CompleteMove(0));
            Assert.IsFalse(commander.CompleteMove(5));
            Assert.AreEqual(SquadOrderKind.Attack, commander.OrderOf(1).Kind);
        }

        [TestCase(1, 5)] [TestCase(5, 5)] [TestCase(7, 5)] [TestCase(10, 4)]
        public void Formation_GivesEveryMemberASeparateSlot_CenteredOnTheDestination(int count, int columns)
        {
            var center = new Vector3(10f, 3f, -4f);
            List<Vector3> slots = SquadFormation.Slots(center, 0f, count, columns, 1.5f);
            Assert.AreEqual(count, slots.Count);
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(center.y, slots[i].y);
                Assert.LessOrEqual(Vector3.Distance(center, slots[i]), 1.5f * (columns + count / columns));
                for (int j = i + 1; j < count; j++)
                    Assert.GreaterOrEqual(Vector3.Distance(slots[i], slots[j]), 1.49f);
            }
            if (count <= columns)
            {
                Vector3 sum = Vector3.zero;
                foreach (Vector3 slot in slots) sum += slot;
                Assert.That(Vector3.Distance(center, sum / count), Is.LessThan(0.001f));
            }
        }

        [Test]
        public void Formation_TurnsWithFacing_AndHandlesEmptySquads()
        {
            List<Vector3> forward = SquadFormation.Slots(Vector3.zero, 0f, 2, 5, 2f);
            List<Vector3> turned = SquadFormation.Slots(Vector3.zero, 90f, 2, 5, 2f);
            Assert.That(Mathf.Abs(forward[0].x - forward[1].x), Is.EqualTo(2f).Within(0.001f));
            Assert.That(Mathf.Abs(turned[0].z - turned[1].z), Is.EqualTo(2f).Within(0.001f));
            Assert.IsEmpty(SquadFormation.Slots(Vector3.zero, 0f, 0, 5, 2f));
        }
    }
}
