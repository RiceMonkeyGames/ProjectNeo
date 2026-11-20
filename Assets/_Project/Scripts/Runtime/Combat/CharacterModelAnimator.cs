using ProjectNeo.Core;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 캐릭터 모델의 Animator를 게임 상태로만 움직이는 외형 전용 부품. 전투 판단은 하지 않는다.
    /// 기본 레이어: 이동(Speed) · 교전 중 이동 · 웅크리기 · 사망. 동작 레이어(상체): 근접 공격 · 활 장전/유지/발사 · 피격(ART-04).
    /// 이동 속도는 길찾기 에이전트가 있으면 그 속도를, 없으면(비행·사다리 등) 위치 변화를 쓴다.
    /// </summary>
    public sealed class CharacterModelAnimator : MonoBehaviour
    {
        public const string SpeedParameter = "Speed";
        public const string AimPitchParameter = "AimPitch";
        public const string ActionSpeedParameter = "ActionSpeed";
        public const int BaseLayer = 0;
        public const int ActionLayer = 1;
        /// <summary>활 동작을 다리까지 따르는 하체 레이어(동작 레이어와 동기화, 활 묶음에만 있음).</summary>
        public const int BowLegsLayer = 2;
        public const string LocomotionState = "Locomotion";
        public const string CombatLocomotionState = "CombatLocomotion";
        public const string CrouchState = "Crouch";
        public const string HoverState = "Hover";
        public const string EmptyState = "Empty";
        public const string HitState = "Hit";
        public const string BowLoadState = "BowLoad";
        public const string BowHoldState = "BowHold";
        public const string BowReleaseState = "BowRelease";
        public const string UnsheatheState = "Unsheathe";
        public const string SheatheState = "Sheathe";
        public static string AttackState(int index) => "Attack" + index;
        public static string DeathState(int index) => "Death" + index;

        /// <summary>몸 기준 이동 방향(오른쪽 +, 앞 +, m/s). 방향별 걷기·달리기를 섞는 묶음(궁수)이 쓴다.</summary>
        public const string MoveXParameter = "MoveX";
        public const string MoveZParameter = "MoveZ";
        static readonly int Speed = Animator.StringToHash(SpeedParameter);
        static readonly int MoveX = Animator.StringToHash(MoveXParameter);
        static readonly int MoveZ = Animator.StringToHash(MoveZParameter);
        static readonly int AimPitch = Animator.StringToHash(AimPitchParameter);
        static readonly int ActionSpeed = Animator.StringToHash(ActionSpeedParameter);
        const float SpeedDamping = 0.1f;
        const float AimDamping = 0.15f;
        // 쓰러지는 동작 도중 몸이 바닥 가까이 왔을 때 무기를 놓는다(사망 클립 1.1~1.5초).
        const float WeaponDropDelay = 0.6f;
        // 이 속도(m/s) 아래로 서 있으면 다리도 활 자세(옆으로 선 궁술 자세)를 따른다.
        const float BowStandSpeed = 0.3f;
        const float BowLegsBlendSeconds = 0.2f;
        // 모델을 겨눈 쪽으로 돌리는 속도(도/초).
        const float ModelTurnSpeed = 360f;

        /// <summary>공격·사망 동작을 고르는 난수. 테스트에서 고정 시드로 바꿀 수 있다.</summary>
        public static IRandomSource AnimationRandom { get; set; } = new SeededRandom(System.Environment.TickCount);

        [SerializeField] CharacterActor actor;
        [SerializeField] Animator animator;
        [SerializeField] CharacterAnimationProfile profile;
        [Tooltip("손에 든 무기. 죽으면 손에서 떼어 바닥에 눕힌다.")]
        [SerializeField] Transform weapon;
        [Tooltip("허리 칼집 자리. 있으면 평소엔 무기를 여기 차고, 싸울 때 뽑아 손에 쥔다(근접 지상 유닛).")]
        [SerializeField] Transform sheath;
        float dropWeaponAt = float.PositiveInfinity;
        Transform weaponHand;
        Vector3 weaponLocalPosition;
        Quaternion weaponLocalRotation;
        WeaponSheathRule sheathRule;
        float weaponSwapAt = float.PositiveInfinity;
        bool weaponSwapToHand;
        NavMeshAgent agent;
        UnitCombatAI ai;
        IAnimationCueSource cues;
        BowDrawCycle bow;
        Vector3 lastPosition;
        float lastAttackTime = float.NegativeInfinity;
        float actionEndsAt = float.NegativeInfinity;
        int lastAttack = -1;
        float bowLegsWeight;
        float modelYaw;
        CharacterActor bowTarget;
        Transform bowHand;
        Transform stringHand;
        float aimOffset = float.NaN;
        bool sampleAimNextFrame;
        int baseState;
        bool dead;

        /// <summary>웅크린 자세(COMBAT-10에서 성가퀴 뒤 재장전에 쓴다). 웅크리기 클립이 없는 묶음은 무시한다.</summary>
        public bool Crouched { get; set; }
        /// <summary>사망 동작을 직접 재생하는지. 아니면 자세를 멈추고 눕히기는 PlaceholderCombatView가 맡는다.</summary>
        public bool PlaysDeath => profile != null && profile.DeathCount > 0;
        public BowDrawPhase BowPhase => bow != null ? bow.Phase : BowDrawPhase.Lowered;
        public CharacterAnimationProfile Profile => profile;

        public Transform Weapon => weapon;
        public Transform Sheath => sheath;
        /// <summary>무기를 손에 쥐고 있는지(칼집이 없으면 늘 손에 쥔다).</summary>
        public bool WeaponInHand => weapon != null && (sheath == null || weapon.parent != sheath);

        public void Configure(CharacterActor owner, Animator target, CharacterAnimationProfile animationProfile = null,
            Transform heldWeapon = null, Transform weaponSheath = null)
        {
            actor = owner;
            animator = target;
            profile = animationProfile;
            weapon = heldWeapon;
            sheath = weaponSheath;
        }

        void Awake()
        {
            agent = actor.GetComponent<NavMeshAgent>();
            ai = actor.GetComponent<UnitCombatAI>();
            cues = actor.GetComponent<IAnimationCueSource>();
            animator.applyRootMotion = false;
            if (animator.isHuman)
            {
                bowHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                stringHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            if (profile != null && UsesBow)
                bow = new BowDrawCycle(profile.BowLoadSeconds, profile.BowReleaseSeconds);
            if (profile != null && sheath != null && weapon != null && animator.isHuman)
            {
                // 칼집 자리는 뽑기 클립에서 손이 칼자루를 잡는 순간의 손 자세라, 무기는 손과 칼집에서 같은 상대 자세를 쓴다.
                weaponHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                weaponLocalPosition = weapon.localPosition;
                weaponLocalRotation = weapon.localRotation;
                sheathRule = new WeaponSheathRule(profile.SheatheDelay);
                MoveWeapon(toHand: false);
            }
        }

        bool UsesBow => actor.Profile != null && actor.Profile.AttackKind == AttackKind.Projectile;

        void OnEnable()
        {
            lastPosition = actor.transform.position;
            actor.Died += OnDeath;
            actor.AttackStarted += OnAttack;
            actor.HitReceived += OnHit;
        }

        void OnDisable()
        {
            actor.Died -= OnDeath;
            actor.AttackStarted -= OnAttack;
            actor.HitReceived -= OnHit;
        }

        void Update()
        {
            if (dead)
            {
                if (Time.time >= dropWeaponAt) DropWeapon();
                return;
            }
            if (!animator.enabled) return;
            float deltaTime = Time.deltaTime;
            Vector3 position = actor.transform.position;
            Vector3 velocity = agent != null && agent.enabled && agent.isOnNavMesh
                ? agent.velocity
                : deltaTime > 0f ? (position - lastPosition) / deltaTime : Vector3.zero;
            lastPosition = position;
            velocity.y = 0f;
            animator.SetFloat(Speed, velocity.magnitude, SpeedDamping, deltaTime);
            // 활을 겨누며 모델을 돌려 둔 경우도 있으니 모델 기준 방향으로 잰다(옆걸음·앞걸음이 실제 이동과 맞도록).
            Vector3 local = animator.transform.InverseTransformDirection(velocity);
            animator.SetFloat(MoveX, local.x, SpeedDamping, deltaTime);
            animator.SetFloat(MoveZ, local.z, SpeedDamping, deltaTime);
            if (profile == null) return;

            bool holdsDrawnBow = cues != null && cues.HoldsDrawnBow;
            bool engaged = CharacterAnimationRules.IsEngaged(Time.time - lastAttackTime, profile.EngagedSeconds, holdsDrawnBow);
            UpdateBase(engaged);
            CharacterActor target = AimTarget();
            UpdateAim(target, deltaTime);
            if (bow != null)
            {
                bool keepDrawn = CharacterAnimationRules.KeepsBowDrawn(engaged, velocity.magnitude, profile.BowLowerSpeed, holdsDrawnBow);
                if (bow.Tick(deltaTime, keepDrawn)) PlayBowPhase();
                animator.SetFloat(ActionSpeed, bow.PhaseSpeed);
                UpdateBowStance(target, velocity.magnitude, deltaTime);
            }
            if (sheathRule != null) UpdateSheath(target, engaged, deltaTime);
            if (actionEndsAt > float.NegativeInfinity && Time.time >= actionEndsAt)
            {
                actionEndsAt = float.NegativeInfinity;
                // 활 동작이 이미 이어받았으면 그대로 둔다.
                if (bow == null || bow.Phase == BowDrawPhase.Lowered) CrossFade(EmptyState, ActionLayer, 0.2f);
            }
        }

        void UpdateBase(bool engaged)
        {
            string state = !profile.GroundLocomotion ? HoverState
                : Crouched && profile.Crouch ? CrouchState
                : engaged && profile.CombatIdle ? CombatLocomotionState
                : LocomotionState;
            int hash = Animator.StringToHash(state);
            if (hash == baseState) return;
            baseState = hash;
            CrossFade(state, BaseLayer, profile.CrossFadeSeconds);
        }

        /// <summary>
        /// 허리에 찬 무기를 뽑고 넣는다. 대상이 가깝거나 교전 중이면 뽑기 동작을 재생하고 손이 칼자루를 잡는 순간 손으로 옮긴다.
        /// 조용해진 뒤 일정 시간이 지나면 넣기 동작을 재생하고 손이 칼을 놓는 순간 허리로 옮긴다.
        /// </summary>
        void UpdateSheath(CharacterActor target, bool engaged, float deltaTime)
        {
            float distance = target != null ? Vector3.Distance(actor.transform.position, target.transform.position) : float.PositiveInfinity;
            if (sheathRule.Tick(deltaTime, WeaponSheathRule.WantsArmed(engaged, distance, profile.DrawDistance)))
            {
                bool draw = sheathRule.Armed;
                if (!profile.HasSheathMotion) MoveWeapon(draw);
                else
                {
                    CrossFade(draw ? UnsheatheState : SheatheState, ActionLayer, 0.1f);
                    weaponSwapToHand = draw;
                    weaponSwapAt = Time.time + (draw ? profile.UnsheatheGrabSeconds : profile.SheatheReleaseSeconds);
                    actionEndsAt = Time.time + Mathf.Max(0.1f, (draw ? profile.UnsheatheSeconds : profile.SheatheSeconds) - 0.1f);
                }
            }
            if (Time.time >= weaponSwapAt)
            {
                weaponSwapAt = float.PositiveInfinity;
                MoveWeapon(weaponSwapToHand);
            }
        }

        void MoveWeapon(bool toHand)
        {
            Transform parent = toHand ? weaponHand : sheath;
            if (weapon == null || parent == null) return;
            weapon.SetParent(parent, false);
            weapon.localPosition = weaponLocalPosition;
            weapon.localRotation = weaponLocalRotation;
        }

        CharacterActor AimTarget()
        {
            CharacterActor target = cues != null ? cues.AimTarget : null;
            if (target == null && ai != null) target = ai.Target;
            return target != null && target.isActiveAndEnabled ? target : null;
        }

        /// <summary>
        /// 활 클립은 몸을 옆으로 돌린 궁술 자세라, 상체만 따르면 허리가 꺾인다. 거의 서 있으면 다리도 활 자세를 따르게 한다(하체 레이어).
        /// 모델 방향 맞추기는 애니메이션이 끝난 뒤 <see cref="LateUpdate"/>에서 한다.
        /// </summary>
        void UpdateBowStance(CharacterActor target, float speed, float deltaTime)
        {
            bowTarget = bow.Phase != BowDrawPhase.Lowered ? target : null;
            if (animator.layerCount <= BowLegsLayer) return;
            float goal = bow.Phase != BowDrawPhase.Lowered && speed < BowStandSpeed ? 1f : 0f;
            bowLegsWeight = Mathf.MoveTowards(bowLegsWeight, goal, deltaTime / BowLegsBlendSeconds);
            animator.SetLayerWeight(BowLegsLayer, bowLegsWeight);
        }

        /// <summary>
        /// 활을 겨누는 동안 화살 선(시위 손→활 손)이 대상을 향하도록 모델만 돌린다(판정 무관). 활을 내리면 정면으로 돌아온다.
        /// 화살 선이 모델 정면에서 몇 도 돌아가 있는지(<see cref="aimOffset"/>)는 애니메이션이 적용된 실제 자세에서,
        /// 두 손이 화살 선에 맞춰진 순간(당긴 채 유지, 발사 첫 프레임)에만 재서 익힌다. 클립·블렌드·마스크와 관계없이 맞춰진다.
        /// </summary>
        void LateUpdate()
        {
            if (bow == null || dead || !animator.enabled || bowHand == null || stringHand == null) return;
            if (bow.Phase == BowDrawPhase.Holding || sampleAimNextFrame)
            {
                sampleAimNextFrame = false;
                float measured = CharacterAnimationRules.AimOffset(animator.transform.forward, bowHand.position - stringHand.position);
                aimOffset = float.IsNaN(aimOffset) ? measured : Mathf.LerpAngle(aimOffset, measured, 0.5f);
            }
            float goal = 0f;
            if (bowTarget != null && bowTarget.isActiveAndEnabled && !float.IsNaN(aimOffset))
                goal = CharacterAnimationRules.BowModelYaw(actor.transform.forward, bowTarget.transform.position - actor.transform.position, aimOffset);
            modelYaw = Mathf.MoveTowardsAngle(modelYaw, goal, ModelTurnSpeed * Time.deltaTime);
            animator.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
        }

        void UpdateAim(CharacterActor target, float deltaTime)
        {
            float pitch = target != null
                ? CharacterAnimationRules.AimPitch(actor.AimPoint, target.AimPoint, profile.AimPitchRange)
                : 0f;
            animator.SetFloat(AimPitch, pitch, AimDamping, deltaTime);
        }

        void OnAttack()
        {
            lastAttackTime = Time.time;
            if (profile == null || dead) return;
            float impactDelay = actor.Profile.ImpactDelay;
            if (bow != null)
            {
                BowDrawPhase before = bow.Phase;
                bow.Fire(impactDelay);
                if (bow.Phase != before) PlayBowPhase();
                animator.SetFloat(ActionSpeed, bow.PhaseSpeed);
                return;
            }
            // 무기를 허리에 찬 채 공격이 먼저 시작되면 뽑기 동작 없이 곧바로 손에 쥔다.
            if (sheathRule != null && !WeaponInHand)
            {
                sheathRule.ForceArmed();
                weaponSwapAt = float.PositiveInfinity;
                MoveWeapon(toHand: true);
            }
            int count = profile.MeleeCount;
            if (count == 0) return;
            // 공격 동작은 무작위로 고르되 바로 전 동작은 되풀이하지 않는다.
            int index = CharacterAnimationRules.PickOther(AnimationRandom, count, lastAttack);
            lastAttack = index;
            float clip = profile.MeleeClipSeconds(index);
            float start = CharacterAnimationRules.MeleeStartSeconds(profile.MeleeContactSeconds(index), impactDelay, clip);
            animator.CrossFadeInFixedTime(AttackState(index), 0.05f, ActionLayer, start);
            actionEndsAt = Time.time + Mathf.Max(0.1f, clip - start - 0.15f);
        }

        void OnHit()
        {
            if (profile == null || dead) return;
            // 공격·활 동작 중에는 끊지 않는다(번쩍임은 PlaceholderCombatView가 따로 보여 준다).
            if (Time.time < actionEndsAt || (bow != null && bow.Phase != BowDrawPhase.Lowered)) return;
            animator.CrossFadeInFixedTime(HitState, 0.05f, ActionLayer, 0f);
            actionEndsAt = Time.time + profile.HitSeconds * 0.8f;
        }

        void PlayBowPhase()
        {
            switch (bow.Phase)
            {
                case BowDrawPhase.Drawing: CrossFade(BowLoadState, ActionLayer, 0.1f); break;
                case BowDrawPhase.Holding: CrossFade(BowHoldState, ActionLayer, 0.1f); break;
                case BowDrawPhase.Releasing:
                    // 발사 직전 자세(두 손이 화살 선에 있음)로 조준 각도를 잰다.
                    sampleAimNextFrame = true;
                    CrossFade(BowReleaseState, ActionLayer, 0.05f);
                    break;
                default: CrossFade(EmptyState, ActionLayer, 0.3f); break;
            }
        }

        void CrossFade(string state, int layer, float seconds) => animator.CrossFadeInFixedTime(state, seconds, layer);

        void OnDeath()
        {
            if (!PlaysDeath)
            {
                // 사망 동작이 없는 묶음(비행 적)은 현재 자세로 멈추고, 눕히기는 PlaceholderCombatView가 맡는다.
                animator.enabled = false;
                return;
            }
            dead = true;
            // 죽을 때마다 사망 동작을 무작위로 고른다.
            int variant = AnimationRandom.NextInt(profile.DeathCount);
            animator.CrossFadeInFixedTime(DeathState(variant), 0.1f, BaseLayer);
            animator.CrossFadeInFixedTime(EmptyState, 0.1f, ActionLayer);
            // 허리에 차고 있던 무기는 그대로 두고, 손에 쥔 무기만 떨어뜨린다.
            if (WeaponInHand) dropWeaponAt = Time.time + WeaponDropDelay;
        }

        /// <summary>
        /// 무기를 손에서 떼어 바닥에 눕힌다. 손 뼈에 붙은 채면 쓰러진 뒤 손 방향대로 무기가 서 버린다.
        /// 무기 메쉬는 긴 축이 Y, 얇은 축이 X라 X를 위로, Y를 지금 무기가 향한 수평 방향으로 둔다.
        /// </summary>
        void DropWeapon()
        {
            dropWeaponAt = float.PositiveInfinity;
            if (weapon == null) return;
            Vector3 along = CharacterAnimationRules.FlatDirection(weapon.up, actor.transform.right);
            Vector3 position = weapon.position;
            position.y = actor.transform.position.y + 0.03f;
            weapon.SetParent(transform, true);
            weapon.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.Cross(Vector3.up, along), along));
        }
    }
}
