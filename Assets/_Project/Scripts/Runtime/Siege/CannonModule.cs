using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Core;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;

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
        readonly List<Vector3> candidatePositions = new List<Vector3>();
        readonly List<CharacterActor> candidateActors = new List<CharacterActor>();
        ICannonTargetSelector selector = new NearestCannonTargetSelector();
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
        }

        public void SetSelector(ICannonTargetSelector value) => selector = value ?? new NearestCannonTargetSelector();
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

        /// <summary>목표 고르기 전략으로 적을 골라, 움직임을 앞질러 흩어짐을 넣어 쏜다. 쐈으면 true.</summary>
        public bool TryAutoFire()
        {
            if (!IsInstalled || settings == null || !Reload.IsReady) return false;
            candidatePositions.Clear();
            candidateActors.Clear();
            foreach (CharacterActor actor in CharacterActor.ActiveActors)
            {
                if (actor == null || actor.Team == team || actor.gameObject.scene != gameObject.scene
                    || actor.Health == null || actor.Health.IsDead) continue;
                candidateActors.Add(actor);
                candidatePositions.Add(actor.transform.position);
            }
            CannonAimContext context = AimContext;
            int index = selector.Select(context, candidatePositions);
            if (index < 0) return false;
            CharacterActor target = candidateActors[index];
            Vector3 aim = Lead(target, candidatePositions[index]);
            aim = CannonScatter.Apply(aim, settings.AutoScatterRadius, random);
            if (!FireAt(aim)) return false;
            LastTarget = target;
            return true;
        }

        // 날아가는 동안 움직일 거리만큼 앞을 겨눈다. 한 번 다시 풀어 비행 시간을 맞춘다.
        Vector3 Lead(CharacterActor target, Vector3 position)
        {
            var agent = target.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.enabled) return position;
            Vector3 motion = agent.velocity;
            motion.y = 0f;
            if (motion.sqrMagnitude < 0.0001f) return position;
            Vector3 aim = position;
            for (int i = 0; i < 2; i++)
            {
                if (!CannonBallistics.TrySolve(Muzzle, aim, settings.ShellSpeed, settings.ShellGravity, out _, out float time))
                    return position;
                aim = position + motion * time;
            }
            return aim;
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
