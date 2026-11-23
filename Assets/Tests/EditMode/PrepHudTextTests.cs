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
        public void StageTitle_ShowsStageNumber()
        {
            Assert.AreEqual("스테이지 2", PrepHudText.StageTitle(2));
        }

        [Test]
        public void HoldRatio_PrefersBattleStartThenWait()
        {
            Assert.AreEqual(0.4f, PrepHudText.HoldRatio(0.4f, 0.7f, out bool waiting), 1e-5f);
            Assert.IsFalse(waiting);
            Assert.AreEqual(0.7f, PrepHudText.HoldRatio(0f, 0.7f, out waiting), 1e-5f);
            Assert.IsTrue(waiting);
            Assert.AreEqual(0f, PrepHudText.HoldRatio(0f, 0f, out waiting));
            Assert.IsFalse(waiting);
            Assert.AreEqual(1f, PrepHudText.HoldRatio(1.5f, 0f, out _));
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
        public void Gold_UsesThousandsSeparator_MaterialsSkipEmpty()
        {
            Assert.AreEqual("1,250", PrepHudText.Gold(1250));
            Assert.AreEqual("0", PrepHudText.Gold(0));
            var materials = new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("목재", 12),
                new KeyValuePair<string, int>("철", 0),
                new KeyValuePair<string, int>("석재", 5),
            };
            Assert.AreEqual("목재 12   석재 5", PrepHudText.Materials(materials));
            Assert.AreEqual(string.Empty, PrepHudText.Materials(null));
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
            Assert.AreEqual("사기 120/150", PrepHudText.MoraleLine(119.6f, 150f, "정상"));
            Assert.AreEqual("사기 40/150 · 붕괴: 공포", PrepHudText.MoraleLine(40f, 150f, "붕괴: 공포"));
            Assert.AreEqual("사기 30", PrepHudText.MoraleLine(30f, 0f, null), "최대를 모르면 현재만 적는다.");
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
            Assert.AreEqual("B - 길게 누르기 - 전투 시작", PrepHudText.KeyList(new[] { new KeyHint("B", "전투 시작", true) }),
                "길게 누르는 키는 줄 앞에 길게 누르기를 붙인다(UI-12).");
        }

        [Test]
        public void HealthNumber_SameSize_MaxDimmed()
        {
            Assert.AreEqual("80<alpha=#99>/100", PrepHudText.HealthNumber(80, 100));
            Assert.AreEqual("0<alpha=#99>/100", PrepHudText.HealthNumber(-5, 100));
        }

        [Test]
        public void AscensionTag_ShowsZeroToo()
        {
            Assert.AreEqual("승천 0", PrepHudText.AscensionTag(0));
            Assert.AreEqual("승천 3", PrepHudText.AscensionTag(3));
        }

        [Test]
        public void ClockNumber_RemainingOverBudget_BudgetDimmed()
        {
            Assert.AreEqual("5.5<alpha=#99>/8", PrepHudText.ClockNumber(5.5f, 8f));
            Assert.AreEqual("0<alpha=#99>/12", PrepHudText.ClockNumber(-1f, 12f), "음수는 0으로 보인다.");
        }

        [Test]
        public void HealthSegments_OnePerChunk_ClampedToRange()
        {
            Assert.AreEqual(10, PrepHudText.HealthSegments(100f, 10f, 20));
            Assert.AreEqual(11, PrepHudText.HealthSegments(105f, 10f, 20), "남는 체력은 한 칸을 더 쓴다.");
            Assert.AreEqual(20, PrepHudText.HealthSegments(500f, 10f, 20), "칸 수는 최대를 넘지 않는다.");
            Assert.AreEqual(1, PrepHudText.HealthSegments(3f, 10f, 20));
            Assert.AreEqual(0, PrepHudText.HealthSegments(0f, 10f, 20));
        }

        [Test]
        public void HoldRingLabel_ShowsActionAndFlooredPercent()
        {
            Assert.AreEqual("전투 시작 62%", PrepHudText.HoldRingLabel(false, 0.629f, 1f));
            Assert.AreEqual("1시간 기다리기 40%", PrepHudText.HoldRingLabel(true, 0.4f, 1f));
            Assert.AreEqual("전투 시작 100%", PrepHudText.HoldRingLabel(false, 1.5f, 1f));
        }

        [Test]
        public void Notice_JoinsBlockedStartWorkAndGate_EmptyWhenNothing()
        {
            Assert.AreEqual(string.Empty, PrepHudText.Notice("", "", null));
            Assert.AreEqual("잠시 뒤에 다시 누르세요", PrepHudText.Notice(PrepHudText.BlockedStart(true), null, ""));
            Assert.AreEqual("작업 1건 남음 · 성문 수리 중", PrepHudText.Notice(null, "작업 1건 남음", "성문 수리 중"));
            Assert.AreEqual("화면을 클릭한 뒤 다시 누르세요 · 작업 1건 남음",
                PrepHudText.Notice(PrepHudText.BlockedStart(false), "작업 1건 남음", ""));
        }

        [Test]
        public void ContextPrompt_EmptyWithoutTarget()
        {
            Assert.AreEqual("F · 상점 - 사용", PrepHudText.ContextPrompt("F", "상점 - 사용"));
            Assert.AreEqual(string.Empty, PrepHudText.ContextPrompt("F", null));
        }
    }
}
