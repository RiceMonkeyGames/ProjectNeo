using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Siege;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-37: 운반 단계와 운반병이 걸 자리를 고르는 규칙.</summary>
    public class LadderCarryRulesTests
    {
        [Test]
        public void CarriedLadder_IgnoresRaisingPushingAndToppling_UntilPlaced()
        {
            var ladder = new SiegeLadderState(3f, 2f);
            bool toppled = false;
            ladder.Toppled += _ => toppled = true;
            Assert.IsTrue(ladder.TryLift());
            Assert.AreEqual(LadderPhase.Carried, ladder.Phase);

            ladder.Tick(10f, true, 3, false);
            ladder.Topple();
            Assert.AreEqual(LadderPhase.Carried, ladder.Phase, "들고 가는 사다리는 세우거나 쓰러뜨릴 수 없다.");
            Assert.IsFalse(toppled);
            Assert.IsFalse(ladder.CanBoard);
            Assert.AreEqual(0f, ladder.RaisedFraction);
            Assert.IsFalse(ladder.TryBeginClimb());

            Assert.IsTrue(ladder.TryPlace());
            Assert.AreEqual(LadderPhase.Down, ladder.Phase, "내려놓으면 바닥에 놓인 사다리 규칙을 따른다.");
            Assert.IsFalse(ladder.TryPlace(), "이미 놓인 사다리는 다시 내려놓지 않는다.");
            ladder.Tick(0.1f, true, false, false);
            Assert.AreEqual(LadderPhase.Raising, ladder.Phase);
        }

        [Test]
        public void Lift_OnlyFromALadderLyingOnTheGround()
        {
            var ladder = new SiegeLadderState(3f, 2f);
            ladder.Tick(0.1f, true, false, false);
            Assert.AreEqual(LadderPhase.Raising, ladder.Phase);
            Assert.IsFalse(ladder.TryLift(), "세우는 중인 사다리는 들 수 없다.");
            ladder.Tick(5f, true, false, false);
            Assert.AreEqual(LadderPhase.Up, ladder.Phase);
            Assert.IsFalse(ladder.TryLift(), "세운 사다리는 들 수 없다.");
            ladder.Topple();
            Assert.IsTrue(ladder.TryLift());
            Assert.IsTrue(ladder.TryLift(), "이미 든 사다리를 다시 들어도 그대로다.");
        }

        [Test]
        public void SlotScore_AddsCrowdAndDefenderPenalties_AndKeepsTheCurrentSlot()
        {
            Assert.AreEqual(10f, LadderSlotRules.Score(10f, 0, 0, false, 6f, 6f), 1e-4f);
            Assert.AreEqual(10f + 12f + 6f, LadderSlotRules.Score(10f, 2, 1, false, 6f, 6f), 1e-4f);
            Assert.AreEqual(10f - 6f, LadderSlotRules.Score(10f, 0, 0, true, 6f, 6f), 1e-4f, "고른 자리는 조금 깎는다.");
            // 가깝지만 수비병 둘이 지키는 자리보다, 조금 먼 빈 자리를 고른다.
            Assert.Less(LadderSlotRules.Score(18f, 0, 0, false, 6f, 6f), LadderSlotRules.Score(8f, 0, 2, false, 6f, 6f));
            Assert.AreEqual(5f, LadderSlotRules.Score(5f, -1, -3, false, 6f, 6f), 1e-4f, "음수 개수는 0으로 본다.");
        }

        [Test]
        public void SlotIsFree_OnlyAwayFromOtherLadders_AndNearbyCountsWithinRadius()
        {
            var taken = new List<Vector3> { new Vector3(0f, 5f, 0f), new Vector3(7.5f, 5f, 0f) };
            Assert.IsFalse(LadderSlotRules.IsFree(new Vector3(2.5f, 5f, 0f), taken, 3f), "다른 사다리 위쪽 끝과 3m 안이다.");
            Assert.IsTrue(LadderSlotRules.IsFree(new Vector3(3.75f, 5f, 0f), taken, 3f));
            Assert.IsFalse(LadderSlotRules.IsFree(new Vector3(0f, 0f, 0f), taken, 3f), "높이 차이는 보지 않는다.");
            Assert.IsTrue(LadderSlotRules.IsFree(Vector3.zero, null, 3f));

            Assert.AreEqual(2, LadderSlotRules.CountNearby(new Vector3(3.75f, 5f, 0f), taken, 9f));
            Assert.AreEqual(1, LadderSlotRules.CountNearby(new Vector3(-3f, 5f, 0f), taken, 9f));
            Assert.AreEqual(0, LadderSlotRules.CountNearby(new Vector3(3.75f, 5f, 0f), taken, 0f));
        }
    }
}
