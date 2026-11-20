using System.Collections.Generic;
using ProjectNeo.Combat;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 스킬이 분대와 사기를 알기 위한 창구(COMBAT-06). 씬 연결은 <see cref="GameSkillContext"/>가 하고,
    /// 없으면 분대 스킬은 반경 안 아군으로, 사기 효과는 아무 일도 하지 않는 것으로 처리한다.
    /// </summary>
    public interface ISkillContext
    {
        /// <summary>유닛이 속한 분대 번호. 없으면 -1.</summary>
        int SquadOf(CharacterActor actor);

        /// <summary>분대의 살아 있는 구성원을 <paramref name="into"/>에 더한다.</summary>
        void CollectSquad(int squad, List<CharacterActor> into);

        /// <summary>전투 중인 분대 사기를 올리고 실제로 오른 양을 돌려준다.</summary>
        float AddMorale(int squad, float amount);
    }
}
