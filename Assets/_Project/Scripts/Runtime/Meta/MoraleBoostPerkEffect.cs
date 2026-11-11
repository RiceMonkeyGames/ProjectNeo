using UnityEngine;

namespace ProjectNeo.Meta
{
    /// <summary>발동 시점에 아군 부대 전체의 사기를 올린다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Perk Effect/Morale Boost", fileName = "PerkEffect_MoraleBoost")]
    public sealed class MoraleBoostPerkEffect : PerkSpecialEffect
    {
        [SerializeField] float amount = 10f;

        public float Amount => amount;

        public override void Apply(IPerkEffectContext context)
        {
            context.AddMoraleToAllySquads(amount);
        }

        /// <summary>코드에서 효과를 만든다(테스트용).</summary>
        public static MoraleBoostPerkEffect Create(PerkTrigger trigger, float amount)
        {
            var effect = CreateInstance<MoraleBoostPerkEffect>();
            effect.SetTrigger(trigger);
            effect.amount = amount;
            return effect;
        }
    }
}
