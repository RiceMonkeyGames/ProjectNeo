using UnityEngine;

namespace ProjectNeo.Meta
{
    /// <summary>특수 효과가 발동하는 시점. 숫자는 에셋에 저장되므로 바꾸지 말고 끝에 추가한다.</summary>
    public enum PerkTrigger
    {
        BattleStart = 0,
        AllySquadMemberDied = 1,
        PlayerSkillUsed = 2,
    }

    /// <summary>
    /// 특수 효과가 전투에 영향을 줄 때 쓰는 창구. 전투 쪽이 구현한다.
    /// 새 특수 효과가 다른 조작을 필요로 하면 여기에 메서드를 추가한다.
    /// </summary>
    public interface IPerkEffectContext
    {
        void AddMoraleToAllySquads(float amount);
    }

    /// <summary>
    /// 스탯 수정으로 표현할 수 없는 퍽 효과. 효과 종류마다 이 클래스를 상속해 만들고,
    /// 에셋으로 만들어 퍽에 붙인다.
    /// </summary>
    public abstract class PerkSpecialEffect : ScriptableObject
    {
        [SerializeField] PerkTrigger trigger;

        public PerkTrigger Trigger => trigger;

        public abstract void Apply(IPerkEffectContext context);

        protected void SetTrigger(PerkTrigger value) => trigger = value;
    }
}
