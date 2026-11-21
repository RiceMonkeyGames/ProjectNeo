using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.UI;

namespace ProjectNeo.Tests
{
    public sealed class PrepHudTextTests
    {
        [Test]
        public void StageLine_AddsAscensionOnlyWhenAboveZero()
        {
            Assert.AreEqual("준비 · 스테이지 2", PrepHudText.StageLine(2, 0));
            Assert.AreEqual("준비 · 스테이지 3 · 승천 1", PrepHudText.StageLine(3, 1));
        }

        [Test]
        public void Resources_ListsGoldThenMaterialsInOrder_SkipsEmpty()
        {
            var materials = new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("목재", 12),
                new KeyValuePair<string, int>("철", 0),
                new KeyValuePair<string, int>("석재", 5),
            };
            Assert.AreEqual("골드 340   목재 12   석재 5", PrepHudText.Resources(340, materials));
            Assert.AreEqual("골드 0", PrepHudText.Resources(0, null));
        }

        [Test]
        public void Ratio_ClampsAndHandlesZeroMax()
        {
            Assert.AreEqual(0.5f, PrepHudText.Ratio(6f, 12f), 1e-5f);
            Assert.AreEqual(1f, PrepHudText.Ratio(20f, 12f));
            Assert.AreEqual(0f, PrepHudText.Ratio(-1f, 12f));
            Assert.AreEqual(0f, PrepHudText.Ratio(5f, 0f));
        }

        [Test]
        public void Headcount_AndMoraleLine()
        {
            Assert.AreEqual("8/10명", PrepHudText.Headcount(8, 10));
            Assert.AreEqual("0/10명", PrepHudText.Headcount(-1, 10));
            Assert.AreEqual("사기 120", PrepHudText.MoraleLine(119.6f, "정상"));
            Assert.AreEqual("사기 40 · 붕괴: 공포", PrepHudText.MoraleLine(40f, "붕괴: 공포"));
            Assert.AreEqual("사기 0", PrepHudText.MoraleLine(0f, null));
        }

        [Test]
        public void Band_UsesBreakingPointAndHighThreshold()
        {
            Assert.AreEqual(MoraleBand.Low, PrepHudText.Band(50f, 50f, 150f));
            Assert.AreEqual(MoraleBand.Normal, PrepHudText.Band(51f, 50f, 150f));
            Assert.AreEqual(MoraleBand.High, PrepHudText.Band(150f, 50f, 150f));
        }

        [Test]
        public void KeyList_OneLinePerKey_SkipsEmptyKeys()
        {
            var hints = new[] { new KeyHint("F", "상호작용"), new KeyHint("", "장비"), new KeyHint("H", "키 안내 닫기") };
            Assert.AreEqual("F - 상호작용\nH - 키 안내 닫기", PrepHudText.KeyList(hints));
            Assert.AreEqual(string.Empty, PrepHudText.KeyList(null));
        }

        [Test]
        public void ContextPrompt_EmptyWithoutTarget()
        {
            Assert.AreEqual("F · 상점 - 사용", PrepHudText.ContextPrompt("F", "상점 - 사용"));
            Assert.AreEqual(string.Empty, PrepHudText.ContextPrompt("F", null));
        }
    }
}
