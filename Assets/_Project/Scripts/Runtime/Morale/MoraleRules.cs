using System;

namespace ProjectNeo.Morale
{
    /// <summary>전투 사이의 사기 규칙(Q-39).</summary>
    public static class MoraleRules
    {
        /// <summary>
        /// 전투가 끝난 뒤 회복: max(현재 + 회복량, 최소값), 최대 사기를 넘지 않는다.
        /// 도망친 부대(0)도 이 규칙으로 최소값에서 다음 전투를 시작한다.
        /// </summary>
        public static void RecoverAfterBattle(SquadMorale morale, MoraleSettings settings)
        {
            if (morale == null) throw new ArgumentNullException(nameof(morale));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            float target = Math.Max(morale.Value + settings.PostBattleRecovery, settings.PostBattleFloor);
            if (target > morale.Max) target = morale.Max;
            morale.Add(target - morale.Value);
        }
    }
}
