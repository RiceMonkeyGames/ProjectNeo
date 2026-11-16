using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>플레이어와 영웅이 공유하는 전투 몸통. 입력과 외형은 별도 컴포넌트다.</summary>
    public sealed class CharacterActor : MonoBehaviour
    {
        [SerializeField] HeroDefinition definition;
        [SerializeField] SoldierDefinition soldierDefinition;
        [SerializeField] CombatProfile profile;
        [SerializeField] CombatTeam team = CombatTeam.Allied;
        static readonly List<CharacterActor> activeActors = new List<CharacterActor>();
        readonly List<IInterruptibleAction> interruptibleActions = new List<IInterruptibleAction>();
        float cooldown;
        float impactRemaining;
        bool pendingImpact;
        CharacterActor attackTarget;
        bool targetedAttack;
        public Character Character { get; private set; }
        /// <summary>잡졸이면 그 정의. 영웅이면 null이다.</summary>
        public SoldierDefinition SoldierDefinition => soldierDefinition;
        public StatSheet Stats { get; private set; }
        public Health Health { get; private set; }
        public CombatProfile Profile => profile;
        public CombatTeam Team => team;
        public Collider Body { get; private set; }
        /// <summary>공중 유닛이면 그 몸통 상태. 땅 유닛은 null이다.</summary>
        public AirborneBody Airborne { get; private set; }
        public Vector3 AimPoint => transform.position + Vector3.up * (profile.Height * 0.5f);
        public bool IsAttacking => pendingImpact;
        public bool IsOnLadder { get; private set; }
        public void SetOnLadder(bool value)
        {
            IsOnLadder = value;
            if (value) pendingImpact = false;
        }
        public static IReadOnlyList<CharacterActor> ActiveActors => activeActors;
        public event Action AttackStarted;
        public event Action HitReceived;
        public event Action<CharacterActor> HitReceivedFrom;
        public event Action<IInterruptibleAction, float> ActionInterrupted;
        public event Action Died;

        public void RegisterInterruptible(IInterruptibleAction action)
        {
            if (action != null && !interruptibleActions.Contains(action)) interruptibleActions.Add(action);
        }

        public void UnregisterInterruptible(IInterruptibleAction action) => interruptibleActions.Remove(action);

        public void Configure(HeroDefinition hero, CombatProfile combatProfile, CombatTeam combatTeam = CombatTeam.Allied)
        {
            definition = hero;
            soldierDefinition = null;
            profile = combatProfile;
            team = combatTeam;
        }

        public void Configure(SoldierDefinition soldier, CombatProfile combatProfile, CombatTeam combatTeam = CombatTeam.Allied)
        {
            definition = null;
            soldierDefinition = soldier;
            profile = combatProfile;
            team = combatTeam;
        }

        public void SetTeam(CombatTeam combatTeam) => team = combatTeam;

        void Awake()
        {
            if ((definition == null && soldierDefinition == null) || profile == null)
            {
                Debug.LogError("CharacterActor needs a hero or soldier definition and combat profile.", this);
                enabled = false;
                return;
            }
            if (definition != null)
            {
                Character = new Character(definition);
                Stats = Character.Stats;
            }
            else Stats = soldierDefinition.CreateStats();
            Health = new Health(Stats);
            Health.Died += OnDied;
            Body = GetComponent<Collider>();
            Airborne = GetComponent<AirborneBody>();
        }

        void OnEnable()
        {
            if (Health != null && !Health.IsDead && !activeActors.Contains(this)) activeActors.Add(this);
        }

        void OnDisable()
        {
            activeActors.Remove(this);
            pendingImpact = false;
        }

        public bool IsEnemy(CharacterActor other) => other != null && other != this
            && other.isActiveAndEnabled && other.gameObject.scene == gameObject.scene
            && other.Health != null && !other.Health.IsDead && other.Team != Team;

        public bool CanAttack(CharacterActor target)
        {
            if (IsOnLadder || Health == null || Health.IsDead || !IsEnemy(target)) return false;
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            return Vector3.Angle(transform.forward, direction) <= profile.AttackHalfAngle
                && CombatPhysics.IsInRange(this, target) && CombatPhysics.HasClearShot(this, target);
        }

        void Update()
        {
            if (Health == null || Health.IsDead) return;
            cooldown = Mathf.Max(0f, cooldown - Time.deltaTime);
            if (!pendingImpact) return;
            impactRemaining -= Time.deltaTime;
            if (impactRemaining > 0f) return;
            pendingImpact = false;
            ResolveImpact();
        }

        public bool TryAttack(CharacterActor target = null)
        {
            if (IsOnLadder || Health == null || Health.IsDead || pendingImpact || cooldown > 0f) return false;
            if (target != null && !CanAttack(target)) return false;
            if (profile.AttackKind == AttackKind.Projectile && profile.ProjectilePrefab == null) return false;
            float speed = Stats.GetValue(StatType.AttackSpeed);
            if (speed <= 0f) return false;
            cooldown = Mathf.Max(1f / speed, profile.ImpactDelay);
            impactRemaining = profile.ImpactDelay;
            pendingImpact = true;
            attackTarget = target;
            targetedAttack = target != null;
            AttackStarted?.Invoke();
            return true;
        }

        void ResolveImpact()
        {
            // 입력 공격은 판정 시 전방 대상을 찾는다. AI의 지정 대상은 중간에 바꾸지 않는다.
            CharacterActor nearest = attackTarget;
            float nearestDistance = float.PositiveInfinity;
            if (!targetedAttack)
            {
                foreach (CharacterActor target in activeActors)
                {
                    if (!CanAttack(target)) continue;
                    float distance = (target.Body.ClosestPoint(AimPoint) - AimPoint).sqrMagnitude;
                    if (distance >= nearestDistance) continue;
                    nearest = target;
                    nearestDistance = distance;
                }
            }
            if (!CanAttack(nearest)) return;
            float attack = Stats.GetValue(StatType.Attack);
            if (profile.AttackKind == AttackKind.Melee)
                nearest.ReceiveDamage(DamageRules.Calculate(attack, nearest.Stats.GetValue(StatType.Defense), profile.MinimumDamage), this);
            else
            {
                GameObject shot = Instantiate(profile.ProjectilePrefab, AimPoint,
                    Quaternion.LookRotation(nearest.AimPoint - AimPoint));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(shot, gameObject.scene);
                shot.GetComponent<CombatProjectile>().Launch(this, attack,
                    (nearest.AimPoint - AimPoint).normalized, Stats.GetValue(StatType.AttackRange));
            }
            attackTarget = null;
        }

        public void ReceiveDamage(float damage, CharacterActor attacker = null)
        {
            if (Health != null && Health.TakeDamage(damage) > 0f)
            {
                HitReceived?.Invoke();
                HitReceivedFrom?.Invoke(attacker);
                EvaluateInterrupts(attacker != null && attacker.Profile != null ? attacker.Profile.StaggerPower : 0f);
            }
        }

        void EvaluateInterrupts(float incomingStagger)
        {
            for (int i = interruptibleActions.Count - 1; i >= 0; i--)
            {
                if (i >= interruptibleActions.Count) continue;
                IInterruptibleAction action = interruptibleActions[i];
                if (action == null || !ActionInterruptRules.ShouldInterrupt(action.InterruptPoise, incomingStagger)) continue;
                action.InterruptAction();
                ActionInterrupted?.Invoke(action, incomingStagger);
            }
        }

        void OnDied()
        {
            pendingImpact = false;
            activeActors.Remove(this);
            foreach (Collider body in GetComponents<Collider>()) body.enabled = false;
            Died?.Invoke();
        }

        void OnDestroy()
        {
            if (Health == null) return;
            Health.Died -= OnDied;
            Health.Dispose();
        }
    }
}
