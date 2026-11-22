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
        public Vector3 AimPoint => transform.position + Vector3.up * (CurrentHeight * 0.5f);
        /// <summary>지금 키. 웅크렸으면 <see cref="CombatProfile.CrouchHeightRatio"/>만큼 낮다(COMBAT-10).</summary>
        public float CurrentHeight => profile.Height * (IsCrouched ? profile.CrouchHeightRatio : 1f);
        /// <summary>성가퀴 뒤에 웅크려 있는지(COMBAT-10). 판정 몸통과 조준점이 낮아진다.</summary>
        public bool IsCrouched { get; private set; }
        /// <summary>다음 공격까지 남은 시간(초).</summary>
        public float CooldownRemaining => cooldown;
        public bool IsAttacking => pendingImpact;
        public bool IsOnLadder { get; private set; }
        public void SetOnLadder(bool value)
        {
            IsOnLadder = value;
            if (!value) return;
            pendingImpact = false;
            // 웅크린 채 사다리에 오르지 않는다(COMBAT-10). 사다리 중에는 AI의 웅크리기 판단이 돌지 않는다.
            SetCrouched(false);
        }
        /// <summary>지금 하는 일로 위협 종류를 알려 주는 판단(COMBAT-14). 공성 적의 판단이 스스로 등록한다. 없으면 null.</summary>
        public ITargetThreatSource ThreatSource { get; set; }
        /// <summary>원거리 유닛이 대상을 고를 때 보는 위협 종류(COMBAT-14).</summary>
        public TargetThreat Threat => RangedTargetPriorityRules.Classify(IsOnLadder,
            ThreatSource != null ? ThreatSource.Threat : TargetThreat.None,
            profile != null && profile.AttackKind == AttackKind.Projectile);
        public static IReadOnlyList<CharacterActor> ActiveActors => activeActors;
        /// <summary>활성 유닛 목록이 바뀌거나 위치가 바뀔 때 증가하는 변경 번호(PERF-ai-spatial-grid).</summary>
        public static int RegistryRevision { get; private set; }
        /// <summary>원거리 탄퍼짐에 쓰는 난수(COMBAT-09). 테스트는 고정 난수로 바꿔 끼운다.</summary>
        public static Core.IRandomSource ShotRandom { get; set; } = new Core.SeededRandom(Environment.TickCount);
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

        public void SetTeam(CombatTeam combatTeam)
        {
            team = combatTeam;
            RegistryRevision++;
        }

        /// <summary>
        /// 웅크리거나 일어난다(COMBAT-10). 캡슐 판정 몸통을 발을 고정한 채 키 비율만큼 줄이고 되돌린다.
        /// 플레이어 몸통(CharacterController)은 플레이어 입력이 따로 맡으므로 건드리지 않는다.
        /// </summary>
        public void SetCrouched(bool value)
        {
            if (IsCrouched == value || profile == null) return;
            IsCrouched = value;
            if (Body is CapsuleCollider capsule)
            {
                float height = CurrentHeight;
                capsule.height = height;
                capsule.center = Vector3.up * (height * 0.5f);
            }
        }

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
            if (Health != null && !Health.IsDead && !activeActors.Contains(this))
            {
                activeActors.Add(this);
                RegistryRevision++;
            }
        }

        void OnDisable()
        {
            if (activeActors.Remove(this)) RegistryRevision++;
            pendingImpact = false;
            SetCrouched(false);
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
                // 몸 가운데가 가려졌으면 보이는 위쪽 몸통을 겨눈다(SIEGE-10, 낮은 난간 뒤 수비병).
                CombatPhysics.TryGetShotPoint(this, nearest, out Vector3 aimAt);
                // 조준점 둘레로 흩어진 지점을 낮은 포물선으로 노린다(COMBAT-09). 움직이는 대상의 앞을 노리지는 않는다.
                Vector3 landing = AimSpreadRules.Apply(AimPoint, aimAt, Stats.GetValue(StatType.AimSpread), ShotRandom);
                Vector3 launch = ArrowBallistics.LaunchVelocity(AimPoint, landing, profile.ProjectileSpeed, profile.ProjectileGravity);
                GameObject shot = Instantiate(profile.ProjectilePrefab, AimPoint, Quaternion.LookRotation(launch));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(shot, gameObject.scene);
                shot.GetComponent<CombatProjectile>().Launch(this, attack, launch);
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

        /// <summary>피해 없이 경직만 건다(COMBAT-06 스킬). 강인도보다 강하면 진행 중인 행동이 끊긴다.</summary>
        public void ApplyStagger(float power)
        {
            if (Health == null || Health.IsDead || power <= 0f) return;
            EvaluateInterrupts(power);
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
            SetCrouched(false);
            if (activeActors.Remove(this)) RegistryRevision++;
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
