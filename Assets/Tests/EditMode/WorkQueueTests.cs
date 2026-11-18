using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Run;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-08 작업 주문 규칙(Q-53): 작업대 동시 진행, 남은 시간 넘기기, 환불, 수리 체력, 성 기록.</summary>
    public class WorkQueueTests
    {
        readonly List<Object> created = new List<Object>();
        T Track<T>(T obj) where T : Object { created.Add(obj); return obj; }

        [TearDown]
        public void TearDown() { foreach (Object obj in created) Object.DestroyImmediate(obj); created.Clear(); }

        static WorkOrder Build(int key, float hours = 1f) => new WorkOrder(WorkKind.Build, WorkTarget.Piece, key, hours);

        [Test]
        public void 작업대_둘이_앞에서부터_함께_짓고_끝나면_남은_시간은_다음_주문으로_간다()
        {
            var queue = new WorkQueue(2);
            WorkOrder a = Build(1), b = Build(2, 0.5f), c = Build(3);
            queue.Add(a); queue.Add(b); queue.Add(c);
            var done = new List<int>();
            queue.Completed += order => done.Add(order.Key);

            queue.Advance(0.75f);
            Assert.AreEqual(0.75f, a.Done, 0.0001f);
            Assert.IsTrue(b.IsComplete, "0.5시간 주문은 끝난다.");
            Assert.AreEqual(0.25f, c.Done, 0.0001f, "끝난 작업대는 남은 0.25시간을 다음 주문에 쓴다.");
            CollectionAssert.AreEqual(new[] { 2 }, done);

            Assert.AreEqual(0.75f, queue.HoursToFinishAll(), 0.0001f, "남은 0.25h와 0.75h를 둘이 함께: 0.75h");
            queue.Advance(5f);
            Assert.IsTrue(a.IsComplete && c.IsComplete);
            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, done);
            queue.RemoveCompleted();
            Assert.AreEqual(0, queue.Orders.Count);
            queue.Advance(1f);
        }

        [Test]
        public void 작업대가_하나면_순서대로_하나씩_짓는다()
        {
            var queue = new WorkQueue(1);
            WorkOrder a = Build(1), b = Build(2);
            queue.Add(a); queue.Add(b);
            queue.Advance(1.5f);
            Assert.IsTrue(a.IsComplete);
            Assert.AreEqual(0.5f, b.Progress, 0.0001f);
            Assert.Throws<System.InvalidOperationException>(() => queue.Add(Build(2)), "같은 대상의 주문은 하나다.");
            Assert.AreSame(b, queue.Remove(WorkTarget.Piece, 2));
            Assert.IsNull(queue.Find(WorkTarget.Piece, 2));
        }

        [Test]
        public void 수리는_시작_체력에서_진행률만큼_오르고_못_한_만큼_골드를_내림해_돌려준다()
        {
            var repair = new WorkOrder(WorkKind.Repair, WorkTarget.Piece, 1, 0.5f, goldPaid: 25, startHealth: 40f);
            Assert.AreEqual(40f, repair.RepairedHealth(100f), 0.0001f);
            Assert.AreEqual(25, repair.UnfinishedRefund);
            repair.Work(0.2f);
            Assert.AreEqual(0.4f, repair.Progress, 0.0001f);
            Assert.AreEqual(64f, repair.RepairedHealth(100f), 0.0001f);
            Assert.AreEqual(15, repair.UnfinishedRefund, "25 × 0.6 = 15");
            repair.Work(1f);
            Assert.IsTrue(repair.IsComplete);
            Assert.AreEqual(100f, repair.RepairedHealth(100f), 0.0001f);
            Assert.AreEqual(0, repair.UnfinishedRefund);

            var odd = new WorkOrder(WorkKind.Repair, WorkTarget.Weapon, 2, 1f, goldPaid: 7);
            odd.Work(0.5f);
            Assert.AreEqual(3, odd.UnfinishedRefund, "7 × 0.5 = 3.5 → 3");
        }

        [Test]
        public void 성_기록에_작업_주문과_미완성_무기가_담기고_저장_칸으로_되돌아온다()
        {
            var wall = Track(BuildPieceDefinition.Create(BuildPieceKind.Wall, "Wall", Vector2Int.one, 3f, 100f, null,
                new BuildMountPoint("top", Vector3.up)));
            var cannon = Track(SiegeWeaponDefinition.Create("cannon", "Cannon", Vector3.one));
            var grid = Track(BuildGridDefinition.Create(10, 10, 3f, 2));
            var layout = new BuildLayout(grid);
            var mounts = new MountLayout(layout);
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(1, 1), 0, out BuildPlacement first));
            Assert.IsTrue(layout.TryPlace(wall, new Vector2Int(3, 1), 0, out BuildPlacement second));
            Assert.IsTrue(mounts.TryMount(cannon, first.Id, "top", out MountedWeapon weapon));

            var order = new WorkOrder(WorkKind.Build, WorkTarget.Piece, second.Id, 1f, done: 0.25f);
            CastleState castle = CastleState.Capture(layout, mounts,
                workOf: placement => placement.Id == second.Id ? CastleWorkState.From(order, 3) : default,
                unfinishedOf: mount => mount.Id == weapon.Id);

            CastleWorkState work = castle.Pieces[1].Work;
            Assert.IsFalse(castle.Pieces[0].Work.HasWork);
            Assert.AreEqual(WorkKind.Build, work.Kind);
            Assert.AreEqual(3, work.Order);
            Assert.AreEqual(0.25f, work.Done, 0.0001f);
            Assert.IsTrue(castle.Weapons[0].Unfinished);

            CastleWorkState read = RunState.WorkOf((int)work.Kind, work.Order, work.Hours, work.Done, work.GoldPaid, work.StartHealth);
            WorkOrder back = read.ToOrder(WorkTarget.Piece, 42);
            Assert.AreEqual(42, back.Key);
            Assert.AreEqual(0.25f, back.Progress, 0.0001f);
            Assert.IsFalse(RunState.WorkOf(0, 0, 1f, 0f, 0, 0f).HasWork, "예전 저장(종류 0)은 주문이 없다.");
            Assert.IsFalse(RunState.WorkOf(99, 0, 1f, 0f, 0, 0f).HasWork, "모르는 종류는 주문이 없다.");
        }
    }
}
