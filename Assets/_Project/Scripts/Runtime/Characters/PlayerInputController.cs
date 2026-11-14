using ProjectNeo.Combat;
using ProjectNeo.Stats;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Characters
{
    [RequireComponent(typeof(CharacterController), typeof(CharacterActor))]
    public sealed class PlayerInputController : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] PlayerViewCamera view;
        [SerializeField] PlayerControlSettings settings;
        InputActionAsset actions;
        InputAction move;
        InputAction look;
        InputAction attack;
        InputAction crouch;
        InputAction sprint;
        InputAction jump;
        CharacterController motor;
        CharacterActor actor;
        float verticalSpeed;
        // 점프 중에는 뛸 때의 수평 속도를 기준으로 공중 조작만 섞는다(COMBAT-05).
        Vector3 takeoffVelocity;
        Vector3 lastHorizontalVelocity;
        CursorLockMode previousLock;
        bool previousVisible;
        bool movementSuspended;
        float standingHeight;
        Vector3 standingCenter;
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        /// <summary>점프로 떠 있는 중. 착지하거나 이동이 멈추면 끝난다.</summary>
        public bool IsJumping { get; private set; }
        public float HeightRatio => IsCrouching ? motor.height / standingHeight : 1f;
        public float EyeHeight => settings.CameraHeight * HeightRatio;
        public PlayerControlSettings Settings => settings;

        public void SetMovementSuspended(bool suspended)
        {
            movementSuspended = suspended;
            verticalSpeed = 0f;
            IsJumping = false;
            lastHorizontalVelocity = Vector3.zero;
            if (suspended) IsSprinting = false;
        }

        /// <summary>땅에 서 있고 웅크리지 않았으면 설정한 높이로 뛴다. 사다리·대포·건설·낙하 중에는 이동이 멈춰 있어 뛰지 않는다.</summary>
        public bool TryJump()
        {
            if (actor.Health == null || actor.Health.IsDead) return false;
            if (!PlayerLocomotionRules.CanJump(motor.isGrounded, IsCrouching, movementSuspended, IsJumping)) return false;
            verticalSpeed = PlayerLocomotionRules.JumpSpeed(settings.JumpHeight, settings.Gravity);
            if (verticalSpeed <= 0f) return false;
            takeoffVelocity = lastHorizontalVelocity;
            IsJumping = true;
            return true;
        }

        public void Configure(InputActionAsset source, PlayerViewCamera cameraView, PlayerControlSettings controlSettings)
        {
            inputActions = source;
            view = cameraView;
            settings = controlSettings;
        }

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            standingHeight = motor.height;
            standingCenter = motor.center;
            motor.slopeLimit = settings.MaximumWalkSlope;
            actor = GetComponent<CharacterActor>();
            actions = Instantiate(inputActions);
            move = actions.FindAction("Player/Move", true);
            look = actions.FindAction("Player/Look", true);
            attack = actions.FindAction("Player/Attack", true);
            // Add bindings to the private runtime copy, preserving shared input assets.
            var map = actions.FindActionMap("Player", true);
            crouch = map.FindAction("Crouch") ?? map.AddAction("Crouch", InputActionType.Button);
            sprint = map.FindAction("Sprint") ?? map.AddAction("Sprint", InputActionType.Button);
            jump = map.FindAction("Jump") ?? map.AddAction("Jump", InputActionType.Button);
            // Replace default bindings (the template uses C) only on this instance.
            while (crouch.bindings.Count > 0) crouch.ChangeBinding(0).Erase();
            while (sprint.bindings.Count > 0) sprint.ChangeBinding(0).Erase();
            while (jump.bindings.Count > 0) jump.ChangeBinding(0).Erase();
            jump.AddBinding("<Keyboard>/space");
            crouch.AddBinding("<Keyboard>/leftCtrl");
            crouch.AddBinding("<Keyboard>/rightCtrl");
            sprint.AddBinding("<Keyboard>/leftShift");
            sprint.AddBinding("<Keyboard>/rightShift");
        }

        void OnEnable()
        {
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            actions.FindActionMap("Player", true).Enable();
            SetCaptured(true);
        }

        static void SetCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetCaptured(false);
            if (actor.Health == null || actor.Health.IsDead) { IsSprinting = false; return; }
            bool captured = Cursor.lockState == CursorLockMode.Locked;
            if (!captured && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCaptured(true);
            if (captured) view.Look(look.ReadValue<Vector2>());
            if (ProjectNeo.Siege.LadderTraveller.HandlePlayer(this, captured, move.ReadValue<Vector2>().y)) return;
            if (movementSuspended) return;
            SetLocomotion(captured && crouch.IsPressed(), captured && sprint.IsPressed());
            if (captured && jump.WasPressedThisFrame()) TryJump();
            Move(captured ? move.ReadValue<Vector2>() : Vector2.zero, view.Yaw, Time.deltaTime);
            if (captured && attack.WasPressedThisFrame()) actor.TryAttack();
        }

        // 입력과 실제 이동을 나눠 PlayMode에서 같은 이동 경로를 검증한다.
        public void Move(Vector2 input, float yaw, float deltaTime)
        {
            if (movementSuspended || actor.Health == null || actor.Health.IsDead) return;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector2 clamped = Vector2.ClampMagnitude(input, 1f);
            Vector3 horizontal = transform.rotation * new Vector3(clamped.x, 0f, clamped.y);
            if (motor.isGrounded && verticalSpeed < 0f) { verticalSpeed = 0f; IsJumping = false; }
            verticalSpeed -= settings.Gravity * deltaTime;
            float multiplier = PlayerLocomotionRules.SpeedMultiplier(IsCrouching, IsSprinting, settings);
            Vector3 desired = horizontal * actor.Character.Stats.GetValue(StatType.MoveSpeed) * multiplier;
            Vector3 velocity = IsJumping ? PlayerLocomotionRules.AirHorizontal(takeoffVelocity, desired, settings.AirControl) : desired;
            lastHorizontalVelocity = velocity;
            CollisionFlags hits = motor.Move((velocity + Vector3.up * verticalSpeed) * deltaTime);
            // 머리를 부딪히면 더 올라가지 않고 떨어지기 시작한다.
            if ((hits & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if (IsJumping && motor.isGrounded && verticalSpeed <= 0f) IsJumping = false;
        }

        public void SetLocomotion(bool wantsCrouch, bool wantsSprint)
        {
            if (movementSuspended || actor.Health == null || actor.Health.IsDead) return;
            // 공중에서는 착지할 때까지 웅크리기를 받지 않는다.
            if (IsJumping) wantsCrouch = false;
            bool crouched = wantsCrouch || (IsCrouching && !CanStand());
            if (crouched != IsCrouching)
            {
                IsCrouching = crouched;
                motor.height = crouched ? Mathf.Max(motor.radius * 2f, standingHeight * settings.CrouchHeightRatio) : standingHeight;
                motor.center = standingCenter + Vector3.up * ((motor.height - standingHeight) * 0.5f);
            }
            IsSprinting = wantsSprint && !IsCrouching;
        }

        bool CanStand()
        {
            // Shrink by skin width so contact with the floor does not block standing.
            Vector3 scale = transform.lossyScale;
            float radius = motor.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = Mathf.Max(radius, standingHeight * Mathf.Abs(scale.y) * 0.5f);
            Vector3 center = transform.TransformPoint(standingCenter);
            Vector3 offset = transform.up * (halfHeight - radius);
            float clearanceRadius = Mathf.Max(radius * 0.5f, radius - motor.skinWidth);
            foreach (Collider obstacle in Physics.OverlapCapsule(center - offset, center + offset,
                clearanceRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (obstacle.transform.IsChildOf(transform)) continue;
                if (Physics.GetIgnoreLayerCollision(gameObject.layer, obstacle.gameObject.layer)) continue;
                if (Physics.GetIgnoreCollision(motor, obstacle)) continue;
                return false;
            }
            return true;
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) SetCaptured(false);
            if (!focused) IsSprinting = false;
        }

        void OnDisable()
        {
            GetComponent<ProjectNeo.Siege.LadderTraveller>()?.Interrupt();
            if (actions != null) actions.Disable();
            IsSprinting = false;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }

        void OnDestroy()
        {
            if (actions != null) Destroy(actions);
        }
    }
}
