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
        [SerializeField] ThirdPersonCamera view;
        [SerializeField] PlayerControlSettings settings;
        InputActionAsset actions;
        InputAction move;
        InputAction look;
        InputAction attack;
        CharacterController motor;
        CharacterActor actor;
        float verticalSpeed;
        CursorLockMode previousLock;
        bool previousVisible;
        bool movementSuspended;

        public void SetMovementSuspended(bool suspended)
        {
            movementSuspended = suspended;
            verticalSpeed = 0f;
        }

        public void Configure(InputActionAsset source, ThirdPersonCamera cameraView, PlayerControlSettings controlSettings)
        {
            inputActions = source;
            view = cameraView;
            settings = controlSettings;
        }

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            actor = GetComponent<CharacterActor>();
            actions = Instantiate(inputActions);
            move = actions.FindAction("Player/Move", true);
            look = actions.FindAction("Player/Look", true);
            attack = actions.FindAction("Player/Attack", true);
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
            if (movementSuspended || actor.Health == null || actor.Health.IsDead) return;
            bool captured = Cursor.lockState == CursorLockMode.Locked;
            if (!captured && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCaptured(true);
            if (captured) view.Look(look.ReadValue<Vector2>());
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
            if (motor.isGrounded && verticalSpeed < 0f) verticalSpeed = 0f;
            verticalSpeed -= settings.Gravity * deltaTime;
            motor.Move((horizontal * actor.Character.Stats.GetValue(StatType.MoveSpeed)
                + Vector3.up * verticalSpeed) * deltaTime);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) SetCaptured(false);
        }

        void OnDisable()
        {
            if (actions != null) actions.Disable();
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }

        void OnDestroy()
        {
            if (actions != null) Destroy(actions);
        }
    }
}
