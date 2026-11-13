using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Siege
{
    /// <summary>플레이어가 대포를 직접 조작하는 키와 거리(Q-21 확정 기본안: F/Esc 타고 내리기, 마우스 조준, 좌클릭 발사).</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Siege/Cannon Operator Settings")]
    public sealed class CannonOperatorSettings : ScriptableObject
    {
        [SerializeField] InputAction interact = new InputAction("ManCannon", InputActionType.Button, "<Keyboard>/f");
        [SerializeField] InputAction leave = new InputAction("LeaveCannon", InputActionType.Button, "<Keyboard>/escape");
        [SerializeField] InputAction fire = new InputAction("FireCannon", InputActionType.Button, "<Mouse>/leftButton");
        [SerializeField] InputAction look = new InputAction("AimCannon", InputActionType.Value, "<Mouse>/delta");
        [SerializeField, Min(0.1f)] float interactDistance = 3f;
        [SerializeField, Range(0f, 180f)] float interactHalfAngle = 60f;
        [SerializeField, Min(1f)] float aimDistance = 150f;

        public InputAction Interact => interact;
        public InputAction Leave => leave;
        public InputAction Fire => fire;
        public InputAction Look => look;
        /// <summary>대포에 탈 수 있는 수평 거리.</summary>
        public float InteractDistance => interactDistance;
        /// <summary>카메라 정면에서 대포까지 허용하는 수평 각도.</summary>
        public float InteractHalfAngle => interactHalfAngle;
        /// <summary>조준선이 땅을 찾는 최대 거리.</summary>
        public float AimDistance => aimDistance;

        public static CannonOperatorSettings Create(float interactDistance = 3f, float interactHalfAngle = 60f)
        {
            var value = CreateInstance<CannonOperatorSettings>();
            value.interactDistance = Mathf.Max(0.1f, interactDistance);
            value.interactHalfAngle = Mathf.Clamp(interactHalfAngle, 0f, 180f);
            return value;
        }
    }
}
