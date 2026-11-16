using NUnit.Framework;
using ProjectNeo.Flow;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// PlayMode 테스트 전체의 기본 적 편성. 기존 Game 씬 테스트는 전투 규칙을 보는 것이라, 스테이지 웨이브 대신
    /// 전투 설정의 단일 적 목록(전투 시작과 함께 모두 등장)을 쓴다. 웨이브 자체는 GameWavePlayTests가
    /// 이 기본값을 잠시 바꿔서 확인한다. 에셋은 건드리지 않는다.
    /// </summary>
    [SetUpFixture]
    public class SingleWaveEncounterFixture
    {
        public static void UseSingleWave() =>
            GameBattleController.EncounterSourceOverride = GameBattleController.SingleWaveEncounter;

        [OneTimeSetUp]
        public void Apply() => UseSingleWave();

        [OneTimeTearDown]
        public void Restore() => GameBattleController.EncounterSourceOverride = null;
    }
}
