using NUnit.Framework;
using ProjectNeo.Morale;
using ProjectNeo.UI;

namespace ProjectNeo.Tests
{
    public sealed class SquadCardPagesTests
    {
        [Test]
        public void PageCount_SplitsByPerPage_AndIsOneWhenEverythingFits()
        {
            Assert.AreEqual(1, SquadCardPages.PageCount(0, 5));
            Assert.AreEqual(1, SquadCardPages.PageCount(5, 5));
            Assert.AreEqual(2, SquadCardPages.PageCount(6, 5));
            Assert.AreEqual(2, SquadCardPages.PageCount(10, 5));
            Assert.AreEqual(1, SquadCardPages.PageCount(10, 0), "0 이하는 나누지 않는다.");
            Assert.AreEqual(1, SquadCardPages.PageOf(7, 5));
            Assert.IsTrue(SquadCardPages.OnPage(4, 0, 5));
            Assert.IsFalse(SquadCardPages.OnPage(5, 0, 5));
        }

        [Test]
        public void Step_WrapsAround_AndClampKeepsPageInRange()
        {
            Assert.AreEqual(1, SquadCardPages.Step(0, 1, 2));
            Assert.AreEqual(0, SquadCardPages.Step(1, 1, 2), "마지막에서 다음은 처음.");
            Assert.AreEqual(1, SquadCardPages.Step(0, -1, 2), "처음에서 이전은 마지막.");
            Assert.AreEqual(0, SquadCardPages.Step(0, 1, 1));
            Assert.AreEqual(0, SquadCardPages.Clamp(3, 1), "분대가 줄어 페이지가 사라지면 남은 마지막 페이지로.");
            Assert.AreEqual(1, SquadCardPages.Clamp(5, 2));
        }

        [Test]
        public void FollowSelection_MovesToNewlySelectedSquadOnlyWhenItIsHidden()
        {
            var none = new bool[7];
            var seventh = new bool[7]; seventh[6] = true;
            Assert.AreEqual(1, SquadCardPages.FollowSelection(0, 5, none, seventh), "F7로 고르면 2쪽으로.");
            Assert.AreEqual(1, SquadCardPages.FollowSelection(1, 5, seventh, seventh), "이미 골라 둔 분대는 다시 옮기지 않는다.");
            var all = new bool[] { true, true, true, true, true, true, true };
            Assert.AreEqual(1, SquadCardPages.FollowSelection(1, 5, none, all), "새로 고른 분대 중 하나라도 지금 쪽에 있으면 그대로.");
            var first = new bool[7]; first[0] = true;
            Assert.AreEqual(0, SquadCardPages.FollowSelection(1, 5, none, first));
        }

        [Test]
        public void HiddenRisk_CountsOnlyLowMoraleSquadsOnOtherPages()
        {
            var flags = new bool[7]; flags[6] = SquadCardPages.AtRisk(MoraleBand.Low);
            Assert.IsTrue(SquadCardPages.AnyOutside(flags, 0, 5));
            Assert.IsFalse(SquadCardPages.AnyOutside(flags, 1, 5), "보이는 페이지의 분대는 알리지 않는다.");
            Assert.IsFalse(SquadCardPages.AtRisk(MoraleBand.Normal));
            Assert.AreEqual("[  2/3  ]", SquadCardPages.Label(1, 3));
        }
    }
}
