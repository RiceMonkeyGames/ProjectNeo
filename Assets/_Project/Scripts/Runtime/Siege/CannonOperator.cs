using System;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 플레이어의 대포 직접 조작(Q-21). 플레이어 오브젝트에 붙는다.
    /// 전투 중 가까운 대포 앞에서 F로 타고, F나 Esc로 내린다. 탄 동안 걷기·근접 공격을 멈추고,
    /// 마우스로 회전 범위 안에서 조준해 좌클릭으로 조준선이 가리킨 땅에 흩어짐 없이 쏜다.
    /// </summary>
    [RequireComponent(typeof(CharacterActor))]
    public sealed class CannonOperator : MonoBehaviour
    {
        [SerializeField] CannonOperatorSettings settings;
        [SerializeField] ConstructionWorld world;
        [SerializeField] FirstPersonCamera view;
        [SerializeField] Camera viewCamera;
        readonly RaycastHit[] hits = new RaycastHit[32];
        CharacterActor actor;
        PlayerInputController input;
        InputAction interact, leave, fire, look;
        bool relockCursor;

        /// <summary>지금 타고 있는 대포. 없으면 null.</summary>
        public CannonModule Manned { get; private set; }
        /// <summary>F로 탈 수 있는 가장 가까운 대포. 없으면 null.</summary>
        public CannonModule Nearby { get; private set; }
        /// <summary>마지막으로 쏘지 못한 이유 등 안내 문구. HUD가 읽는다.</summary>
        public string Message { get; private set; } = string.Empty;
        /// <summary>타고 있는 동안 지금 조준한 곳에 쏠 수 있는지. 조준선 표시가 읽는다(SIEGE-07).</summary>
        public CannonAimState AimState { get; private set; } = CannonAimState.NoTarget;
        public CannonOperatorSettings Settings => settings;
        public string InteractKey => interact != null ? interact.GetBindingDisplayString() : "F";
        public event Action<CannonModule> MannedChanged;

        public void Configure(CannonOperatorSettings config, ConstructionWorld castle, FirstPersonCamera cameraView, Camera aimCamera)
        {
            settings = config; world = castle; view = cameraView; viewCamera = aimCamera;
            CreateActions();
        }

        void Awake()
        {
            actor = GetComponent<CharacterActor>();
            input = GetComponent<PlayerInputController>();
            CreateActions();
        }

        void CreateActions()
        {
            if (settings == null || interact != null) return;
            interact = settings.Interact.Clone();
            leave = settings.Leave.Clone();
            fire = settings.Fire.Clone();
            look = settings.Look.Clone();
            if (isActiveAndEnabled) EnableActions(true);
        }

        void EnableActions(bool enable)
        {
            foreach (InputAction action in new[] { interact, leave, fire, look })
            {
                if (action == null) continue;
                if (enable) action.Enable(); else action.Disable();
            }
        }

        void OnEnable() => EnableActions(true);

        void OnDisable()
        {
            Leave();
            Nearby = null;
            EnableActions(false);
        }

        void OnDestroy()
        {
            foreach (InputAction action in new[] { interact, leave, fire, look }) action?.Dispose();
        }

        bool Alive => actor != null && actor.Health != null && !actor.Health.IsDead;
        bool IsManning => !ReferenceEquals(Manned, null);

        void Update()
        {
            if (settings == null || interact == null) return;
            // 파괴된 대포는 Unity 비교에서 null처럼 보이므로 참조 자체로 탑승 여부를 본다.
            if (IsManning)
            {
                if (!CanKeep(Manned)) { Leave(); return; }
                if (leave.WasPressedThisFrame()) { Leave(); relockCursor = true; return; }
                if (interact.WasPressedThisFrame()) { Leave(); return; }
                ApplyLook(look.ReadValue<Vector2>());
                if (fire.WasPressedThisFrame()) TryFireAtCrosshair();
                RefreshAim();
                return;
            }
            Nearby = FindNearby();
            if (Nearby != null && interact.WasPressedThisFrame()) TryMan(Nearby);
        }

        // Esc는 플레이어 입력이 커서를 풀어 버리므로, 대포에서 내린 프레임 끝에 다시 잠근다.
        void LateUpdate()
        {
            if (!relockCursor) return;
            relockCursor = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        bool CanKeep(CannonModule cannon) => cannon != null && cannon.IsInstalled && cannon.Armed && Alive;

        bool InReach(CannonModule cannon)
        {
            Vector3 forward = viewCamera != null ? viewCamera.transform.forward : transform.forward;
            return CannonOperatorRules.IsWithinReach(transform.position, forward, cannon.transform.position,
                settings.InteractDistance, settings.InteractHalfAngle);
        }

        CannonModule FindNearby()
        {
            if (world == null || !Alive) return null;
            CannonModule best = null;
            float nearest = float.PositiveInfinity;
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
            {
                if (weapon == null) continue;
                var cannon = weapon.GetComponent<CannonModule>();
                if (cannon == null || !cannon.IsInstalled || !cannon.Armed || cannon.IsManned || !InReach(cannon)) continue;
                float distance = (cannon.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                best = cannon;
            }
            return best;
        }

        /// <summary>대포에 탄다. 전투 중이고, 가깝고, 다른 사람이 타지 않은 대포만 탈 수 있다.</summary>
        public bool TryMan(CannonModule cannon)
        {
            if (IsManning || cannon == null || !CanKeep(cannon) || cannon.IsManned || !InReach(cannon)) return false;
            Manned = cannon;
            Nearby = null;
            Message = string.Empty;
            cannon.SetManned(true);
            // 철거·파괴·받침 붕괴로 무기가 사라지는 순간 바로 내린다.
            cannon.Weapon.Removed += OnWeaponRemoved;
            input?.SetMovementSuspended(true);
            if (view != null) view.SetOrientation(ClampedYaw(view.Yaw), view.Pitch);
            RefreshAim();
            MannedChanged?.Invoke(cannon);
            return true;
        }

        /// <summary>대포에서 내린다. 탄 대포가 없으면 아무 일도 하지 않는다.</summary>
        public void Leave()
        {
            if (!IsManning) return;
            CannonModule cannon = Manned;
            Manned = null;
            if (!ReferenceEquals(cannon.Weapon, null)) cannon.Weapon.Removed -= OnWeaponRemoved;
            if (cannon != null) cannon.SetManned(false);
            input?.SetMovementSuspended(false);
            Message = string.Empty;
            AimState = CannonAimState.NoTarget;
            MannedChanged?.Invoke(null);
        }

        void OnWeaponRemoved(PlacedSiegeWeapon weapon, SiegeWeaponRemoval reason) => Leave();

        float ClampedYaw(float yaw)
        {
            Vector3 facing = Manned.Facing;
            float facingYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            return CannonOperatorRules.ClampViewYaw(yaw, facingYaw, Manned.Settings.ManualAimHalfAngle);
        }

        /// <summary>조작 중 마우스 움직임으로 시점을 돌리되, 대포 회전 범위 밖으로는 나가지 않는다.</summary>
        public void ApplyLook(Vector2 delta)
        {
            if (Manned == null || view == null) return;
            view.Look(delta);
            view.SetOrientation(ClampedYaw(view.Yaw), view.Pitch);
            // 포신이 시점을 따라 돈다.
            Vector3 facing = Manned.Facing;
            Manned.SetSwivel(Mathf.DeltaAngle(Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, view.Yaw));
        }

        /// <summary>조준선이 가리킨 곳에 지금 쏠 수 있는지 다시 본다. 타고 있는 동안 매 프레임 부른다.</summary>
        public void RefreshAim()
        {
            if (Manned == null) { AimState = CannonAimState.NoTarget; return; }
            bool hasPoint = TryGetAimPoint(out Vector3 point);
            AimState = CannonOperatorRules.EvaluateAim(Manned.Reload.IsReady, hasPoint, Manned.ManualAimContext, point);
        }

        bool TryGetAimPoint(out Vector3 point)
        {
            point = default;
            if (viewCamera == null) return false;
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int count = Physics.RaycastNonAlloc(ray, hits, settings.AimDistance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (hits[i].distance >= nearest || collider.gameObject.scene != gameObject.scene
                    || collider.transform.IsChildOf(transform)) continue;
                nearest = hits[i].distance;
                point = hits[i].point;
            }
            return nearest != float.PositiveInfinity;
        }

        /// <summary>화면 가운데 조준선이 가리킨 곳을 찾아 쏜다.</summary>
        public bool TryFireAtCrosshair()
        {
            if (Manned == null || viewCamera == null) return false;
            if (!TryGetAimPoint(out Vector3 point)) { Message = "조준한 곳이 너무 멉니다."; return false; }
            return FireAt(point);
        }

        /// <summary>정한 지점에 흩어짐 없이 쏜다. 사거리·회전 범위 밖이거나 장전 중이면 쏘지 않는다.</summary>
        public bool FireAt(Vector3 point)
        {
            if (Manned == null) return false;
            if (!Manned.Reload.IsReady) { Message = "재장전 중입니다."; return false; }
            if (!CannonAim.CanTarget(Manned.ManualAimContext, point)) { Message = "사거리나 회전 범위 밖입니다."; return false; }
            bool fired = Manned.FireAt(point);
            Message = fired ? string.Empty : "포탄이 닿지 않는 곳입니다.";
            return fired;
        }
    }
}
