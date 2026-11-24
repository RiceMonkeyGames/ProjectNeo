using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Core;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 대포 동작 부품(Q-21). 대포 프리팹 루트에 PlacedSiegeWeapon과 함께 붙는다.
    /// 전투 중 무장되어 있고 사람이 타지 않았으면 사거리·회전 범위 안의 적을 자동으로 쏜다.
    /// 정면(Facing)은 설치 때 R로 정한 방향이고, 포신(BarrelFacing)은 그 기준에서 Swivel 각도만큼 돌아 조준을 따라간다(SIEGE-07).
    /// </summary>
    public sealed class CannonModule : MonoBehaviour, ISiegeWeaponModule
    {
        [SerializeField] CannonSettings settings;
        [SerializeField] CannonShell shellPrefab;
        // 프리팹 기준(가로·높이·세로 1) 포구 위치. 실제 크기는 무기 정의의 크기로 늘어난다.
        [SerializeField] Vector3 muzzleLocal = new Vector3(0f, 0.65f, 0.65f);
        [SerializeField] CombatTeam team = CombatTeam.Allied;
        readonly List<CannonTargetCandidate> candidates = new List<CannonTargetCandidate>();
        readonly List<CharacterActor> candidateActors = new List<CharacterActor>();
        // 비워 두면 설정값으로 무리·위협 점수 전략을 만든다(SIEGE-33). SetSelector로 바꿔 끼운 전략은 설정이 바뀌어도 그대로다.
        ICannonTargetSelector selector;
        bool customSelector;
        IRandomSource random;
        CannonReload reload;
        float searchRemaining;
        // 포신 회전. 무기 루트를 세로축으로 돌리고, 설치 방향은 돌린 각도를 되돌려 구한다.
        float swivel, swivelTarget;
        Quaternion swivelledRotation;

        public CannonSettings Settings => settings;
        public PlacedSiegeWeapon Weapon { get; private set; }
        public CombatTeam Team => team;
        public bool IsInstalled => Weapon != null && !Weapon.IsRemoved;
        /// <summary>전투 중에만 켠다. 꺼져 있으면 자동 사격하지 않는다.</summary>
        public bool Armed { get; private set; }
        /// <summary>플레이어가 타고 있으면 자동 사격을 멈춘다(5단계에서 조작과 연결).</summary>
        public bool IsManned { get; private set; }
        public CannonReload Reload => reload ??= new CannonReload(settings != null ? settings.ReloadSeconds : 1f);
        public CharacterActor LastTarget { get; private set; }
        public Vector3 Muzzle => transform.TransformPoint(muzzleLocal);
        /// <summary>설치 때 정한 정면. 포신이 돌아도 바뀌지 않으며 회전 한계의 기준이다.</summary>
        public Vector3 Facing => Quaternion.AngleAxis(-Swivel, Vector3.up) * BarrelFacing;
        /// <summary>지금 포신이 향한 수평 방향.</summary>
        public Vector3 BarrelFacing
        {
            get { Vector3 forward = transform.forward; forward.y = 0f; return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward; }
        }
        /// <summary>설치 방향 기준 포신 각도(도, 왼쪽 음수).</summary>
        public float Swivel
        {
            get
            {
                // 받침이 내려앉는 등 밖에서 자세를 다시 잡았으면 그 자세가 새 설치 방향이다.
                if (swivel != 0f && transform.rotation != swivelledRotation) swivel = swivelTarget = 0f;
                return swivel;
            }
        }
        // 사거리·회전 판정은 포신이 돌기 전 포구 위치를 기준으로 삼아, 포신 방향에 따라 결과가 흔들리지 않게 한다.
        Vector3 RestMuzzle => transform.position + Quaternion.AngleAxis(-Swivel, Vector3.up) * (Muzzle - transform.position);
        public CannonAimContext AimContext => CannonAimContext.From(settings, RestMuzzle, Facing);
        /// <summary>사람이 탔을 때의 조준 조건. 회전 한계가 넓다.</summary>
        public CannonAimContext ManualAimContext => CannonAimContext.Manual(settings, RestMuzzle, Facing);
        /// <summary>포탄을 쏜 순간. 외형(포구 화염·소리·반동)이 듣는다.</summary>
        public event Action<CannonModule, CannonShell> Fired;

        public void Configure(CannonSettings cannon, CannonShell shell)
        {
            settings = cannon;
            shellPrefab = shell;
            reload = null;
            if (!customSelector) selector = null;
        }

        /// <summary>자동 사격 목표 전략을 바꾼다. null이면 기본(무리·위협 점수)으로 돌아간다.</summary>
        public void SetSelector(ICannonTargetSelector value)
        {
            selector = value;
            customSelector = value != null;
        }

        /// <summary>지금 쓰는 목표 전략.</summary>
        public ICannonTargetSelector Selector => selector ??= settings != null
            ? ClusterThreatCannonTargetSelector.From(settings)
            : new NearestCannonTargetSelector();
        public void SetRandom(IRandomSource value) => random = value;

        public void SetArmed(bool armed)
        {
            if (Armed == armed) return;
            Armed = armed;
            searchRemaining = 0f;
            // 전투가 시작될 때는 장전된 상태로 시작한다.
            if (armed) Reload.Reset();
            else { LastTarget = null; SetSwivel(0f, true); }
        }

        /// <summary>포신을 설치 방향 기준 각도로 돌린다. snap이면 바로, 아니면 설정 속도로 따라간다.</summary>
        public void SetSwivel(float yawOffset, bool snap = false)
        {
            float limit = settings != null ? settings.ManualAimHalfAngle : 0f;
            swivelTarget = CannonAim.ClampYawOffset(yawOffset, limit);
            if (snap) ApplySwivel(swivelTarget);
        }

        /// <summary>포신을 정한 지점 쪽으로 돌린다.</summary>
        public void SwivelToward(Vector3 point, bool snap = false) =>
            SetSwivel(CannonAim.YawOffset(Facing, transform.position, point), snap);

        void ApplySwivel(float value)
        {
            float current = Swivel;
            if (Mathf.Approximately(current, value)) return;
            transform.Rotate(0f, value - current, 0f, Space.World);
            swivel = value;
            swivelledRotation = transform.rotation;
        }

        public void SetManned(bool manned) => IsManned = manned;

        public void OnInstalled(PlacedSiegeWeapon weapon)
        {
            Weapon = weapon;
            reload = null;
            random ??= new SeededRandom(weapon.Mount != null ? weapon.Mount.Id * 7919 + 17 : 17);
        }

        public void OnRemoved(PlacedSiegeWeapon weapon, SiegeWeaponRemoval reason)
        {
            Armed = false;
            IsManned = false;
            LastTarget = null;
        }

        void Update()
        {
            if (!IsInstalled || settings == null) return;
            Reload.Tick(Time.deltaTime);
            ApplySwivel(CannonAim.StepSwivel(Swivel, swivelTarget, settings.SwivelSpeed, Time.deltaTime));
            if (!Armed || IsManned || !Reload.IsReady) return;
            searchRemaining -= Time.deltaTime;
            if (searchRemaining > 0f) return;
            searchRemaining = settings.TargetSearchInterval;
            TryAutoFire();
        }

        /// <summary>
        /// 목표 고르기 전략으로 적을 골라, 움직임을 앞질러 흩어짐을 넣어 쏜다. 쐈으면 true(SIEGE-33).
        /// 앞지른 자리가 범위 밖이면 지금 위치를 겨누고, 포물선이 막히거나 닿지 않으면 다음 후보를 시도한다.
        /// </summary>
        public bool TryAutoFire()
        {
            if (!IsInstalled || settings == null || !Reload.IsReady) return false;
            candidates.Clear();
            candidateActors.Clear();
            foreach (CharacterActor actor in CharacterActor.ActiveActors)
            {
                if (actor == null || actor.Team == team || actor.gameObject.scene != gameObject.scene
                    || actor.Health == null || actor.Health.IsDead) continue;
                candidateActors.Add(actor);
                candidates.Add(new CannonTargetCandidate(actor.transform.position, actor.Threat));
            }
            CannonAimContext context = AimContext;
            ICannonTargetSelector strategy = Selector;
            for (int attempt = 0; attempt < settings.MaxTargetAttempts; attempt++)
            {
                int index = strategy.Select(context, candidates);
                if (index < 0) return false;
                CharacterActor target = candidateActors[index];
                if (TryAim(context, target, candidates[index].Position, out Vector3 aim, out Vector3 velocity)
                    && FireWithVelocity(velocity))
                {
                    LastTarget = target;
                    return true;
                }
                candidates[index] = candidates[index].WithExcluded();
            }
            return false;
        }

        // 앞지르기·범위 재확인·흩어짐을 거쳐 조준점과 발사 속도를 정하고, 포물선이 막히지 않았는지 본다.
        bool TryAim(in CannonAimContext context, CharacterActor target, Vector3 position, out Vector3 aim, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            Vector3 led = CannonLead.Predict(Muzzle, position, MotionVelocity.Of(target),
                settings.ShellSpeed, settings.ShellGravity);
            if (!CannonLead.TryResolve(context, position, led, out aim)) return false;
            aim = CannonScatter.Apply(aim, settings.AutoScatterRadius, random);
            // 포신을 목표 쪽으로 맞춘 뒤의 포구에서 풀어, 실제로 포탄이 나가는 자리에서 검사한다.
            float previousSwivel = Swivel;
            SwivelToward(aim, true);
            if (CannonBallistics.TrySolve(Muzzle, aim, settings.ShellSpeed, settings.ShellGravity,
                    out velocity, out float flightTime)
                && IsArcClear(Muzzle, velocity, flightTime, aim))
                return true;
            SetSwivel(previousSwivel, true);
            return false;
        }

        /// <summary>
        /// 풀어 둔 포물선을 구간으로 나눠, 포탄을 멈출 충돌체(아군 탑·보루·지붕·지형)가 있는지 본다(SIEGE-33).
        /// 유닛 몸통과 발밑·엄폐물은 무시하고, 조준점 가까이의 충돌은 목표에 닿은 것으로 본다.
        /// </summary>
        public bool IsArcClear(Vector3 origin, Vector3 velocity, float flightTime, Vector3 aim)
        {
            if (settings == null || !(flightTime > 0f)) return false;
            int segments = settings.TrajectoryCheckSegments;
            float gravity = settings.ShellGravity;
            float tolerance = settings.TrajectoryImpactTolerance;
            Collider footing = CannonShotFilter.Footing(Weapon);
            Vector3 from = origin;
            for (int i = 1; i <= segments; i++)
            {
                Vector3 to = CannonBallistics.Evaluate(origin, velocity, gravity, flightTime * i / segments);
                Vector3 step = to - from;
                float length = step.magnitude;
                if (length > 0.0001f)
                {
                    foreach (RaycastHit hit in Physics.RaycastAll(from, step / length, length, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Collider collider = hit.collider;
                        if (CannonShotFilter.PassesStructure(collider, gameObject.scene, team, footing, Weapon)) continue;
                        if (collider.GetComponentInParent<CharacterActor>() != null) continue;
                        if ((hit.point - aim).sqrMagnitude <= tolerance * tolerance) continue;
                        return false;
                    }
                }
                from = to;
            }
            return true;
        }

        /// <summary>정한 지점에 낮은 포물선으로 쏜다. 장전되지 않았거나 닿을 수 없으면 쏘지 않는다.</summary>
        public bool FireAt(Vector3 point)
        {
            if (!IsInstalled || settings == null || !Reload.IsReady) return false;
            // 쏘는 순간에는 포신을 목표 쪽으로 맞춰, 포탄이 포구에서 나가게 한다.
            SwivelToward(point, true);
            if (!CannonBallistics.TrySolve(Muzzle, point, settings.ShellSpeed, settings.ShellGravity,
                out Vector3 velocity, out _)) return false;
            return FireWithVelocity(velocity);
        }

        /// <summary>정한 발사 속도로 쏜다. 직접 조작이 조준한 방향으로 쏠 때 쓴다.</summary>
        public bool FireWithVelocity(Vector3 velocity)
        {
            if (!IsInstalled || settings == null || !Reload.TryFire()) return false;
            CannonShell shell = shellPrefab != null
                ? Instantiate(shellPrefab)
                : new GameObject("CannonShell").AddComponent<CannonShell>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(shell.gameObject, gameObject.scene);
            shell.gameObject.SetActive(true);
            shell.Launch(Muzzle, velocity, settings, team, Weapon);
            Fired?.Invoke(this, shell);
            return true;
        }
    }
}
