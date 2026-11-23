using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-31 성벽 위 사다리 방어: 밀기·길목 자리, 길목 조건, 밀기 인원 판정, 임무 배정.</summary>
    public class LadderDefenseRulesTests
    {
        // 사다리 바깥 방향은 +z(성 밖), 성벽은 x축을 따라 놓인다.
        static readonly Vector3 Outward = Vector3.forward;
        static readonly Vector3 Top = new Vector3(0f, 5f, 0f);
        const float Radius = 12f;
        const float Tolerance = 1.5f;
        const float Stickiness = 1.5f;
        readonly LadderDefensePlanner planner = new LadderDefensePlanner();
        readonly List<LadderDuty> result = new List<LadderDuty>();

        static LadderDefenseSite Site(Vector3 top, int pushSpots, bool chokepoint, int chokeSpots = 5)
        {
            var site = new LadderDefenseSite();
            site.Reset(top, chokepoint);
            for (int i = 0; i < pushSpots; i++) site.PushSpots.Add(LadderDefenseRules.PushSpot(top, Outward, i, 0.8f));
            for (int i = 0; i < chokeSpots; i++)
                site.ChokeSpots.Add(LadderDefenseRules.ChokepointSlot(top, Outward, 2.5f, chokeSpots, i));
            return site;
        }

        static LadderDefender At(float x, float z = -1f, float y = 5f) =>
            new LadderDefender(new Vector3(x, y, z), new Vector3(x, y, z), LadderDuty.None);

        [Test]
        public void PushSpots_SpreadAlongTheWall_AlternatingSides()
        {
            Assert.AreEqual(Top, LadderDefenseRules.PushSpot(Top, Outward, 0, 0.8f));
            Vector3 first = LadderDefenseRules.PushSpot(Top, Outward, 1, 0.8f);
            Vector3 second = LadderDefenseRules.PushSpot(Top, Outward, 2, 0.8f);
            Assert.AreEqual(0.8f, Mathf.Abs(first.x), 1e-4f);
            Assert.AreEqual(-first.x, second.x, 1e-4f, "두 번째 옆자리는 반대쪽이다.");
            Assert.AreEqual(Top.z, first.z, 1e-4f, "밀기 자리는 성벽을 따라 옆으로만 벌어진다.");
            Assert.AreEqual(1.6f, Mathf.Abs(LadderDefenseRules.PushSpot(Top, Outward, 3, 0.8f).x), 1e-4f);
        }

        [Test]
        public void ChokepointSlots_FormHalfCircleOnTheInnerSide()
        {
            const int count = 5;
            for (int i = 0; i < count; i++)
            {
                Vector3 slot = LadderDefenseRules.ChokepointSlot(Top, Outward, 2.5f, count, i);
                Vector3 offset = slot - Top;
                Assert.AreEqual(0f, offset.y, 1e-4f);
                Assert.AreEqual(2.5f, offset.magnitude, 1e-3f, $"{i}번 자리는 반경 위에 있어야 한다.");
                Assert.LessOrEqual(offset.z, 1e-4f, $"{i}번 자리는 성 안쪽(바깥 반대)에 있어야 한다.");
            }
            Vector3 middle = LadderDefenseRules.ChokepointSlot(Top, Outward, 2.5f, count, 2);
            Assert.AreEqual(-2.5f, (middle - Top).z, 1e-3f, "가운데 자리는 정면 안쪽이다.");
            Vector3 single = LadderDefenseRules.ChokepointSlot(Top, Outward, 2.5f, 1, 0);
            Assert.AreEqual(-2.5f, (single - Top).z, 1e-3f);
        }

        [Test]
        public void NeedsChokepoint_WhenClimberPastThreshold_OrHostileAtTop()
        {
            Assert.IsFalse(LadderDefenseRules.NeedsChokepoint(false, 0f, 0.5f, false));
            Assert.IsFalse(LadderDefenseRules.NeedsChokepoint(true, 0.49f, 0.5f, false));
            Assert.IsTrue(LadderDefenseRules.NeedsChokepoint(true, 0.5f, 0.5f, false));
            Assert.IsTrue(LadderDefenseRules.NeedsChokepoint(false, 0f, 0.5f, true));
            Assert.IsFalse(LadderDefenseRules.NeedsChokepoint(false, 0.9f, 0.5f, false), "오르는 적이 없으면 높이는 보지 않는다.");
        }

        [Test]
        public void ClimbFraction_ClampsBetweenBaseAndEdge()
        {
            Assert.AreEqual(0f, LadderDefenseRules.ClimbFraction(-1f, 0f, 6f));
            Assert.AreEqual(0.5f, LadderDefenseRules.ClimbFraction(3f, 0f, 6f), 1e-4f);
            Assert.AreEqual(1f, LadderDefenseRules.ClimbFraction(7f, 0f, 6f));
            Assert.AreEqual(1f, LadderDefenseRules.ClimbFraction(0f, 6f, 6f), "높이가 없는 사다리는 다 오른 것으로 본다.");
        }

        [Test]
        public void CountsAsAiPusher_DistantTargetDoesNotStopMeleePush()
        {
            Assert.IsTrue(LadderDefenseRules.CountsAsAiPusher(false, false, false, true, false), "대상이 없으면 민다.");
            Assert.IsTrue(LadderDefenseRules.CountsAsAiPusher(false, true, false, true, false), "성벽 아래·먼 대상 때문에 멈추지 않는다.");
            Assert.IsFalse(LadderDefenseRules.CountsAsAiPusher(false, true, false, true, true), "출구 둘레 적과 싸우면 빠진다.");
            Assert.IsFalse(LadderDefenseRules.CountsAsAiPusher(true, false, false, true, false), "공격 동작 중에는 빠진다.");
            Assert.IsFalse(LadderDefenseRules.CountsAsAiPusher(false, true, true, true, false), "원거리 유닛은 대상이 있으면 쏜다(예전과 같다).");
            Assert.IsTrue(LadderDefenseRules.CountsAsAiPusher(false, false, true, true, false), "원거리 유닛도 대상이 없으면 민다.");
            Assert.IsFalse(LadderDefenseRules.CountsAsAiPusher(false, true, false, false, false), "Engage·Move 유닛은 대상이 있으면 빠진다(예전과 같다).");
            Assert.IsTrue(LadderDefenseRules.CountsAsAiPusher(false, false, false, false, false), "Engage·Move 유닛도 대상이 없으면 민다.");
        }

        [Test]
        public void HoldDuty_CoversOnlyEnemiesNearTheExitAtWallLevel()
        {
            var duty = new HoldDuty(Top, Top + Outward, Top, 3f, 1f);
            Assert.IsTrue(duty.Covers(Top + new Vector3(2f, 0.5f, -1f)));
            Assert.IsFalse(duty.Covers(Top + new Vector3(4f, 0f, 0f)), "출구에서 먼 성벽 위 적");
            Assert.IsFalse(duty.Covers(new Vector3(0f, 0f, 2f)), "성벽 아래 적");
            Assert.IsTrue(HoldDutyRules.HasArrived(new Vector3(0.2f, 9f, 0.2f), Vector3.zero, 0.35f), "도착은 수평 거리만 본다.");
        }

        [Test]
        public void Plan_SendsNearestThreeToPush_AndKeepsOthersAtPost()
        {
            var sites = new List<LadderDefenseSite> { Site(Top, 3, false) };
            var defenders = new List<LadderDefender> { At(6f), At(1f), At(9f), At(-2f), At(4f) };
            planner.Plan(sites, defenders, Radius, Tolerance, Stickiness, result);

            Assert.AreEqual(LadderDutyRole.Push, result[1].Role);
            Assert.AreEqual(LadderDutyRole.Push, result[3].Role);
            Assert.AreEqual(LadderDutyRole.Push, result[4].Role);
            Assert.IsTrue(result[0].IsNone, "네 번째로 가까운 수비병은 밀기 인원 상한 밖이다.");
            Assert.IsTrue(result[2].IsNone);
            AssertDistinctSpots(LadderDutyRole.Push);
        }

        [Test]
        public void Plan_IgnoresPostsOutsideRadiusOrOnAnotherLevel()
        {
            var sites = new List<LadderDefenseSite> { Site(Top, 3, true) };
            var defenders = new List<LadderDefender>
            {
                At(13f, -1f),        // 반경 밖
                At(2f, -1f, 0f),     // 성 안마당(다른 높이)
                At(11f, -1f),        // 반경 안
            };
            planner.Plan(sites, defenders, Radius, Tolerance, Stickiness, result);
            Assert.IsTrue(result[0].IsNone);
            Assert.IsTrue(result[1].IsNone);
            Assert.AreEqual(LadderDutyRole.Push, result[2].Role);
        }

        [Test]
        public void Plan_PostDecidesEligibility_EvenWhenUnitStrayed()
        {
            var sites = new List<LadderDefenseSite> { Site(Top, 3, false) };
            var strayed = new LadderDefender(new Vector3(20f, 5f, -1f), new Vector3(2f, 5f, -1f), LadderDuty.None);
            var homeNear = new LadderDefender(new Vector3(5f, 5f, -1f), new Vector3(15f, 5f, -1f), LadderDuty.None);
            planner.Plan(sites, new List<LadderDefender> { strayed, homeNear }, Radius, Tolerance, Stickiness, result);
            Assert.IsTrue(result[0].IsNone, "지킬 자리가 반경 밖이면 지금 가까이 있어도 맡지 않는다.");
            Assert.AreEqual(LadderDutyRole.Push, result[1].Role);
        }

        [Test]
        public void Plan_SurplusDefendersHoldChokepoint_OnlyWhenNeeded()
        {
            var defenders = new List<LadderDefender>();
            for (int i = 0; i < 10; i++) defenders.Add(At(i - 4.5f));

            planner.Plan(new List<LadderDefenseSite> { Site(Top, 3, false) }, defenders, Radius, Tolerance, Stickiness, result);
            Assert.AreEqual(3, Count(LadderDutyRole.Push));
            Assert.AreEqual(0, Count(LadderDutyRole.Chokepoint), "오르는 적이 없으면 남는 수비병은 자리를 지킨다.");

            planner.Plan(new List<LadderDefenseSite> { Site(Top, 3, true) }, defenders, Radius, Tolerance, Stickiness, result);
            Assert.AreEqual(3, Count(LadderDutyRole.Push));
            Assert.AreEqual(5, Count(LadderDutyRole.Chokepoint), "길목 자리 5개만 채운다.");
            Assert.AreEqual(2, Count(LadderDutyRole.None));
            AssertDistinctSpots(LadderDutyRole.Chokepoint);
        }

        [Test]
        public void Plan_DefenderServesOneLadder_NearestFirst()
        {
            Vector3 otherTop = new Vector3(10f, 5f, 0f);
            var sites = new List<LadderDefenseSite> { Site(Top, 3, false), Site(otherTop, 3, false) };
            var defenders = new List<LadderDefender> { At(4f), At(6f), At(1f), At(9f) };
            planner.Plan(sites, defenders, Radius, Tolerance, Stickiness, result);

            Assert.AreEqual(0, result[0].Site, "x=4는 첫 사다리가 더 가깝다.");
            Assert.AreEqual(1, result[1].Site, "x=6은 두 번째 사다리가 더 가깝다.");
            Assert.AreEqual(0, result[2].Site);
            Assert.AreEqual(1, result[3].Site);
        }

        [Test]
        public void Plan_ExcludedLadder_IsSkipped()
        {
            var sites = new List<LadderDefenseSite> { Site(Top, 3, false) };
            var defenders = new List<LadderDefender>
            {
                new LadderDefender(new Vector3(1f, 5f, -1f), new Vector3(1f, 5f, -1f), LadderDuty.None, 1UL),
            };
            planner.Plan(sites, defenders, Radius, Tolerance, Stickiness, result);
            Assert.IsTrue(result[0].IsNone, "길이 없어 거절된 사다리는 다시 맡기지 않는다.");
        }

        [Test]
        public void Plan_CurrentDutyIsSticky_AndKeepsItsSpot()
        {
            var sites = new List<LadderDefenseSite> { Site(Top, 1, false) };
            var current = new LadderDefender(new Vector3(2f, 5f, -1f), new Vector3(2f, 5f, -1f),
                new LadderDuty(0, LadderDutyRole.Push, 0));
            var newcomer = At(1f);
            planner.Plan(sites, new List<LadderDefender> { current, newcomer }, Radius, Tolerance, Stickiness, result);
            Assert.AreEqual(LadderDutyRole.Push, result[0].Role, "조금 더 가까운 수비병이 와도 맡던 임무를 유지한다.");
            Assert.AreEqual(0, result[0].Spot);
            Assert.IsTrue(result[1].IsNone);
        }

        [Test]
        public void Plan_NoSpots_NoDuties()
        {
            var sites = new List<LadderDefenseSite> { Site(Top, 0, true, 0) };
            planner.Plan(sites, new List<LadderDefender> { At(1f) }, Radius, Tolerance, Stickiness, result);
            Assert.IsTrue(result[0].IsNone, "쓸 수 있는 자리가 없으면 임무도 없다.");
            planner.Plan(new List<LadderDefenseSite>(), new List<LadderDefender> { At(1f) }, Radius, Tolerance, Stickiness, result);
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].IsNone);
        }

        int Count(LadderDutyRole role)
        {
            int count = 0;
            foreach (LadderDuty duty in result) if (duty.Role == role) count++;
            return count;
        }

        void AssertDistinctSpots(LadderDutyRole role)
        {
            var spots = new HashSet<int>();
            foreach (LadderDuty duty in result)
            {
                if (duty.Role != role) continue;
                Assert.GreaterOrEqual(duty.Spot, 0);
                Assert.IsTrue(spots.Add(duty.Spot), $"{role} 자리 {duty.Spot}가 겹쳤다.");
            }
        }
    }
}
