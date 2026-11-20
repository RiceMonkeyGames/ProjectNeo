using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Facilities;
using ProjectNeo.Stats;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 영웅 등급·경험치·승급(PREP-10, Q-73). 고용 등급에서 시작하고, 승리마다 경험치를 받으며, 영웅의 전당(Hero Hall)에서
    /// 비용과 준비 시간을 내고 한 등급씩 올린다. 등급 능력치는 영웅 캐릭터에 붙어 씬 유닛에도 바로 반영된다.
    /// </summary>
    public sealed partial class RunState
    {
        /// <summary>등급 능력치를 붙일 때 쓰는 출처. 캐릭터마다 스탯표가 따로라 하나를 같이 써도 된다.</summary>
        static readonly object GradeBonusSource = new object();

        /// <summary>승급 수치. 없으면 등급은 고용 등급 그대로이고 경험치·승급·등급 능력치를 쓰지 않는다.</summary>
        public HeroPromotionSettings HeroPromotion { get; private set; }

        /// <summary>승급 수치를 붙이고 이미 있는 영웅에게 등급 능력치를 붙인다. 이미 있거나 설정이 없으면 하지 않는다.</summary>
        public void SetupHeroPromotion(HeroPromotionSettings settings)
        {
            if (HeroPromotion != null || settings == null) return;
            HeroPromotion = settings;
            foreach (RunHero hero in heroes)
            {
                if (hero.IsPlayer) continue;
                hero.Grade = ClampGrade(hero.Grade);
                hero.Exp = ClampExp(hero, hero.Exp);
                ApplyGradeBonus(hero);
            }
        }

        /// <summary>영웅의 전당 상태. 시설을 아직 붙이지 않았으면 null.</summary>
        public FacilityState HeroHall => Facility(FacilityType.HeroHall);

        /// <summary>등급을 쓰는(승급할 수 있는) 살아 있는 영웅: 플레이어를 뺀 모든 영웅. 대기 영웅도 들어간다.</summary>
        public IReadOnlyList<RunHero> GradedHeroes
        {
            get
            {
                var result = new List<RunHero>();
                foreach (RunHero hero in heroes) if (!hero.IsPlayer) result.Add(hero);
                return result;
            }
        }

        /// <summary>영웅이 고용될 때의 등급: 고용 등급(주점 tier)이며 최고 등급을 넘지 않는다.</summary>
        public int StartingGrade(HeroDefinition definition) => ClampGrade(definition != null ? definition.Tier : 1);

        /// <summary>다음 등급에 필요한 경험치. 최고 등급이거나 수치가 없으면 0.</summary>
        public int ExpToNextGrade(RunHero hero) =>
            hero != null && HeroPromotion != null && HeroPromotion.TryGetStep(hero.Grade, out HeroGradeStep step)
                ? Math.Max(0, step.expRequired) : 0;

        /// <summary>다음 등급의 승급 단계. 최고 등급이거나 수치가 없으면 false.</summary>
        public bool TryGetNextGradeStep(RunHero hero, out HeroGradeStep step)
        {
            step = default;
            return hero != null && HeroPromotion != null && HeroPromotion.TryGetStep(hero.Grade, out step);
        }

        /// <summary>승급 한 번에 드는 준비 시간. 수치가 없으면 0.</summary>
        public float PromotionHours => HeroPromotion != null ? HeroPromotion.PromotionHours : 0f;

        /// <summary>
        /// 영웅을 지금 승급할 수 있는지. 전투 대기 → 설정·시설 → 대상 → 최고 등급 → 경험치 → 비용 → 시간 순으로 처음 걸린 이유를
        /// 돌려준다. 런을 바꾸지 않는다.
        /// </summary>
        public HeroPromoteCheck CheckPromote(RunHero hero)
        {
            if (HasPendingBattle) return HeroPromoteCheck.BattlePending;
            if (HeroPromotion == null || HeroHall == null) return HeroPromoteCheck.NotConfigured;
            if (hero == null || hero.IsPlayer || Find(hero.Id) != hero) return HeroPromoteCheck.NotPromotable;
            if (!HeroPromotion.TryGetStep(hero.Grade, out HeroGradeStep step)) return HeroPromoteCheck.MaxGrade;
            if (hero.Exp < step.expRequired) return HeroPromoteCheck.NotEnoughExp;
            if (!Wallet.CanAfford(step.cost)) return HeroPromoteCheck.NotEnoughFunds;
            if (!HasPrepTime(PromotionHours)) return HeroPromoteCheck.NotEnoughTime;
            return HeroPromoteCheck.Ok;
        }

        /// <summary>
        /// 영웅을 한 등급 올린다. 비용과 준비 시간을 내고, 모은 경험치는 0부터 다시 센다. 등급 능력치를 새 등급으로 바꿔 붙인다.
        /// <see cref="CheckPromote"/>가 Ok가 아니면 false이고 런은 바뀌지 않는다.
        /// </summary>
        public bool TryPromote(RunHero hero)
        {
            if (CheckPromote(hero) != HeroPromoteCheck.Ok) return false;
            HeroPromotion.TryGetStep(hero.Grade, out HeroGradeStep step);
            if (!Wallet.TrySpend(step.cost)) return false;
            TrySpendPrepTime(PromotionHours);
            hero.Grade++;
            hero.Exp = 0;
            ApplyGradeBonus(hero);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 승리한 전투의 경험치를 준다. 전투에 대장으로 나가 살아남은(도망도 포함) 영웅만 받는다. 대기 영웅·플레이어는 받지 않는다.
        /// 다음 등급에 필요한 값을 넘겨 쌓이지 않는다. <see cref="Apply"/>가 죽은 유닛을 정리한 뒤 부른다.
        /// </summary>
        void GrantVictoryExp(BattleResult result)
        {
            if (HeroPromotion == null || result.Outcome != Flow.BattleOutcome.Victory || HeroPromotion.ExpPerVictory <= 0) return;
            foreach (RunHero hero in heroes)
            {
                if (hero.IsPlayer || LeadingSquadOf(hero) == null) continue;
                hero.Exp = ClampExp(hero, hero.Exp + HeroPromotion.ExpPerVictory);
            }
        }

        /// <summary>고용·되살리기로 런에 들어온 영웅의 등급·경험치를 정하고 등급 능력치를 붙인다.</summary>
        void InitGrade(RunHero hero, int grade, int exp)
        {
            if (hero.IsPlayer) return;
            hero.Grade = ClampGrade(grade);
            hero.Exp = ClampExp(hero, exp);
            ApplyGradeBonus(hero);
        }

        int ClampGrade(int grade)
        {
            int max = HeroPromotion != null ? HeroPromotion.MaxGrade : 3;
            return Math.Min(Math.Max(1, grade), Math.Max(1, max));
        }

        int ClampExp(RunHero hero, int exp) => HeroPromotion == null ? Math.Max(0, exp)
            : Math.Min(Math.Max(0, exp), ExpToNextGrade(hero));

        /// <summary>등급 능력치를 지금 등급에 맞게 다시 붙인다. 여러 번 불러도 겹치지 않는다.</summary>
        void ApplyGradeBonus(RunHero hero)
        {
            StatSheet stats = hero.Character.Stats;
            stats.RemoveModifiersFrom(GradeBonusSource);
            if (HeroPromotion == null || hero.IsPlayer || hero.Grade <= 1) return;
            int levels = hero.Grade - 1;
            foreach (StatModifier bonus in HeroPromotion.BonusPerGrade)
                stats.AddModifier(new StatModifier(bonus.stat, bonus.kind, bonus.value * levels), GradeBonusSource);
        }
    }
}
