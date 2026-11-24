using NUnit.Framework;
using ProjectNeo.UI;

namespace ProjectNeo.Tests
{
    /// <summary>전투 상황판 글(UI-13 전투 HUD A안).</summary>
    public sealed class BattleHudTextTests
    {
        [Test]
        public void WaveNumber_ClampsToOneAndTotal()
        {
            Assert.AreEqual("웨이브 2<alpha=#99>/3", BattleHudText.WaveNumber(2, 3));
            Assert.AreEqual("웨이브 1<alpha=#99>/1", BattleHudText.WaveNumber(0, 0), "웨이브가 하나뿐인 전투");
            Assert.AreEqual("웨이브 3<alpha=#99>/3", BattleHudText.WaveNumber(5, 3));
        }

        [Test]
        public void WaveState_ShowsRetreatThenNextThenLast()
        {
            Assert.AreEqual("후퇴 중", BattleHudText.WaveState(4f, true));
            Assert.AreEqual("다음 14초", BattleHudText.WaveState(13.2f, false));
            Assert.AreEqual("마지막 웨이브", BattleHudText.WaveState(-1f, false));
        }

        [Test]
        public void AlliesAndGate()
        {
            Assert.AreEqual("아군 30", BattleHudText.Allies(30));
            Assert.AreEqual("아군 0", BattleHudText.Allies(-2));
            Assert.AreEqual("성문", BattleHudText.Gate(false));
            Assert.AreEqual("성문 파괴", BattleHudText.Gate(true));
        }

        [Test]
        public void Consumable_ShowsNameCountAndCooldown()
        {
            Assert.AreEqual("회복 물약 ×2", BattleHudText.Consumable("회복 물약", 2, 0f));
            Assert.AreEqual("회복 물약 ×1 (3초)", BattleHudText.Consumable("회복 물약", 1, 2.4f));
            Assert.AreEqual("소모품 없음", BattleHudText.Consumable(string.Empty, 0, 0f));
        }
    }
}
