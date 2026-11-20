using NUnit.Framework;
using ProjectNeo.Siege;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-10 적 궁수 자리 판단: 한 번만 찾기, 고른 자리 지키기, 제자리 기다리기.</summary>
    public class ArcherCoverPlanTests
    {
        const float Delay = 3f, Cooldown = 10f, Commit = 5f;
        static ArcherCoverPlan Plan() => new ArcherCoverPlan(Delay, Cooldown, Commit);

        [Test]
        public void SearchesOnlyAfterBeingBlockedInRangeForTheDelay()
        {
            ArcherCoverPlan plan = Plan();
            Assert.IsFalse(plan.ShouldSearch(10f));
            Assert.IsFalse(plan.ShouldSearch(12.9f));
            Assert.IsTrue(plan.ShouldSearch(13f));
        }

        [Test]
        public void ApproachingResetsTheBlockedTime()
        {
            ArcherCoverPlan plan = Plan();
            plan.ShouldSearch(10f);
            plan.Approaching();
            Assert.IsFalse(plan.ShouldSearch(14f), "the clock restarts once back in range");
            Assert.IsTrue(plan.ShouldSearch(17f));
        }

        [Test]
        public void FailedSearch_WaitsInPlace_AndDoesNotSearchAgainBeforeTheCooldown()
        {
            ArcherCoverPlan plan = Plan();
            Assert.IsFalse(plan.ShouldSearch(0f), "the first blocked moment starts the clock");
            Assert.IsTrue(plan.ShouldSearch(3f));
            plan.SearchResult(3f, false);
            plan.Wait();
            Assert.IsTrue(plan.IsWaiting);
            Assert.IsFalse(plan.HasSpot);
            for (float t = 3.5f; t < 13f; t += 0.5f) Assert.IsFalse(plan.ShouldSearch(t), "no left-right searching at " + t);
            Assert.IsTrue(plan.ShouldSearch(13f));
        }

        [Test]
        public void FoundSpot_IsKeptDuringCommitEvenIfTargetsHide()
        {
            ArcherCoverPlan plan = Plan();
            plan.ShouldSearch(3f);
            plan.SearchResult(3f, true);
            Assert.IsTrue(plan.HasSpot);
            Assert.IsFalse(plan.IsWaiting);
            Assert.IsTrue(plan.IsCommitted(7.9f));
            Assert.IsTrue(plan.KeepSpot(7.9f, false), "kept while committed");
            Assert.IsTrue(plan.KeepSpot(9f, true), "kept afterwards while still useful");
            Assert.IsFalse(plan.KeepSpot(9f, false), "dropped once useless after the commit time");
            Assert.IsFalse(plan.HasSpot);
            Assert.IsFalse(plan.ShouldSearch(9f), "dropping the spot does not trigger an immediate new search");
            Assert.IsTrue(plan.ShouldSearch(13f));
        }

        [Test]
        public void ShootingClearsEverything()
        {
            ArcherCoverPlan plan = Plan();
            plan.ShouldSearch(3f);
            plan.SearchResult(3f, true);
            plan.Shooting();
            Assert.IsFalse(plan.HasSpot);
            Assert.IsFalse(plan.IsWaiting);
            Assert.IsFalse(plan.ShouldSearch(4f), "a new blocked episode waits the full delay again");
            Assert.IsTrue(plan.ShouldSearch(7f));
        }
    }
}
