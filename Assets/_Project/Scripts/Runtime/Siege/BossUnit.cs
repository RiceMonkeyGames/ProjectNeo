using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Stages;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 기존 적 위에 붙어 보스(SIEGE-09)로 만든다: 겉모습 크기, 능력치, 결투형 대상 선호, 공성형 구조물 피해,
    /// 지휘형 강화와 죽을 때 웨이브 약화. 수치 규칙은 <see cref="BossRules"/>에 있고 값은 <see cref="BossDefinition"/> 에셋에 있다.
    /// 전투가 만든 적에 실행 중에 붙이므로 프리팹은 바꾸지 않는다.
    /// </summary>
    public sealed class BossUnit : MonoBehaviour
    {
        const float AuraRefreshSeconds = 0.5f;
        // 보스 능력치·강화·약화를 따로 떼어 낼 수 있게 출처를 나눈다.
        readonly object statSource = new object();
        readonly object auraSource = new object();
        readonly object penaltySource = new object();
        readonly HashSet<CharacterActor> buffed = new HashSet<CharacterActor>();
        readonly List<CharacterActor> leaving = new List<CharacterActor>();
        Func<IReadOnlyList<SiegeAttacker>> allEnemies;
        IReadOnlyList<SiegeAttacker> waveMates;
        List<StatModifier> auraModifiers;
        float auraTimer;
        bool penaltyApplied;

        public BossDefinition Definition { get; private set; }
        public CharacterActor Actor { get; private set; }
        public bool IsDead => Actor == null || Actor.Health == null || Actor.Health.IsDead;
        /// <summary>지금 강화를 받는 적 수(지휘형).</summary>
        public int BuffedCount => buffed.Count;
        /// <summary>죽어서 같은 웨이브에 약화를 걸었는지(지휘형).</summary>
        public bool PenaltyApplied => penaltyApplied;
        /// <summary>보스가 죽었을 때 한 번 알린다.</summary>
        public event Action<BossUnit> Died;

        /// <summary>
        /// 보스 정의를 붙인다. 겉모습 크기는 바로 바꾸고 능력치는 <see cref="ApplyStats"/>에서 붙인다.
        /// <paramref name="enemies"/>는 강화 대상이 될 전투의 적 목록, <paramref name="mates"/>는 죽을 때 약해질 같은 웨이브 적이다.
        /// </summary>
        public void Setup(BossDefinition definition, Func<IReadOnlyList<SiegeAttacker>> enemies, IReadOnlyList<SiegeAttacker> mates)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            allEnemies = enemies;
            waveMates = mates;
            Actor = GetComponent<CharacterActor>();
            auraModifiers = BossRules.AuraModifiers(definition);
            Transform visual = transform.Find("Visual");
            if (visual != null) visual.localScale *= definition.VisualScale;
            ConfigureTargeting();
        }

        /// <summary>보스 능력치를 붙이고 체력을 채운다. 여러 번 불러도 한 번만 붙는다.</summary>
        public void ApplyStats()
        {
            if (Definition == null || Actor == null || Actor.Stats == null) return;
            StatSheet stats = Actor.Stats;
            stats.RemoveModifiersFrom(statSource);
            stats.AddModifiers(BossRules.StatModifiers(Definition, stats.GetBase), statSource);
            Actor.Health?.RestoreFull();
        }

        // 결투형은 플레이어·영웅(Character가 있는 유닛)을 먼저 노린다. 걷는 적과 나는 적 모두에 건다.
        void ConfigureTargeting()
        {
            if (!Definition.PrefersHeroes) return;
            BossDefinition boss = Definition;
            var ai = GetComponent<UnitCombatAI>();
            if (ai != null) ai.TargetDistanceScale = target => BossRules.TargetDistanceScale(boss, IsHero(target));
            var flyer = GetComponent<FlyerBrain>();
            if (flyer != null) flyer.PriorityTarget = IsHero;
        }

        static bool IsHero(CharacterActor target) => target != null && target.Character != null;

        /// <summary>
        /// 구조물(성문·성벽·공성 무기)에 주는 피해. 보스가 아니면 그대로, 공성형이면 배율을 곱한다.
        /// 공용 공성 코드는 피해를 줄 때 이 함수를 거친다.
        /// </summary>
        public static float StructureDamage(CharacterActor attacker, float damage)
        {
            if (attacker == null) return damage;
            var boss = attacker.GetComponent<BossUnit>();
            return boss != null ? BossRules.StructureDamage(damage, boss.Definition) : damage;
        }

        void OnEnable()
        {
            if (Actor == null) Actor = GetComponent<CharacterActor>();
            if (Actor != null) Actor.Died += OnActorDied;
        }

        void OnDisable()
        {
            if (Actor != null) Actor.Died -= OnActorDied;
            ClearAura();
        }

        void Update()
        {
            if (Definition == null || !Definition.HasAura || IsDead) return;
            auraTimer -= Time.deltaTime;
            if (auraTimer > 0f) return;
            auraTimer = AuraRefreshSeconds;
            RefreshAura();
        }

        /// <summary>강화 대상을 지금 위치로 다시 고른다. 반경 밖으로 나가거나 죽은 적은 강화가 빠진다.</summary>
        public void RefreshAura()
        {
            if (Definition == null || !Definition.HasAura) return;
            IReadOnlyList<SiegeAttacker> enemies = allEnemies?.Invoke();
            leaving.Clear();
            leaving.AddRange(buffed);
            if (enemies != null && !IsDead)
                foreach (SiegeAttacker enemy in enemies)
                {
                    if (enemy == null || !enemy.isActiveAndEnabled) continue;
                    CharacterActor other = enemy.Actor;
                    if (other == null || other == Actor || other.Stats == null || other.Health == null || other.Health.IsDead) continue;
                    if (!BossRules.InAura(Definition, (other.transform.position - transform.position).sqrMagnitude)) continue;
                    leaving.Remove(other);
                    if (buffed.Add(other)) other.Stats.AddModifiers(auraModifiers, auraSource);
                }
            foreach (CharacterActor other in leaving)
            {
                if (other != null && other.Stats != null) other.Stats.RemoveModifiersFrom(auraSource);
                buffed.Remove(other);
            }
        }

        void ClearAura()
        {
            foreach (CharacterActor other in buffed)
                if (other != null && other.Stats != null) other.Stats.RemoveModifiersFrom(auraSource);
            buffed.Clear();
        }

        void OnActorDied()
        {
            ClearAura();
            ApplyDeathPenalty();
            Died?.Invoke(this);
        }

        // 지휘형이 죽으면 같은 웨이브의 살아 있는 적이 남은 전투 동안 약해진다.
        void ApplyDeathPenalty()
        {
            if (penaltyApplied || waveMates == null) return;
            List<StatModifier> penalty = BossRules.DeathPenaltyModifiers(Definition);
            if (penalty.Count == 0) return;
            penaltyApplied = true;
            foreach (SiegeAttacker mate in waveMates)
            {
                CharacterActor other = mate != null ? mate.Actor : null;
                if (other == null || other == Actor || other.Stats == null || other.Health == null || other.Health.IsDead) continue;
                other.Stats.AddModifiers(penalty, penaltySource);
            }
        }
    }
}
