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
        // 조준선 상태 색(SIEGE-07). 임시 외형이라 에셋에서 바꾼다.
        [SerializeField] Color readyColor = new Color(0.35f, 1f, 0.45f);
        [SerializeField] Color reloadingColor = new Color(1f, 0.75f, 0.25f);
        [SerializeField] Color blockedColor = new Color(1f, 0.35f, 0.3f);
        [SerializeField, Min(0f)] float readyPulseSeconds = 0.3f;
        [SerializeField, Min(1f)] float readyPulseScale = 1.8f;

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
        /// <summary>쏠 수 있을 때의 조준선 색.</summary>
        public Color ReadyColor => readyColor;
        /// <summary>재장전 중 조준선·막대 색.</summary>
        public Color ReloadingColor => reloadingColor;
        /// <summary>사거리·회전 한계 밖을 겨눌 때의 조준선 색.</summary>
        public Color BlockedColor => blockedColor;
        /// <summary>장전이 끝난 순간 조준선이 커졌다 돌아오는 시간.</summary>
        public float ReadyPulseSeconds => readyPulseSeconds;
        /// <summary>장전이 끝난 순간 조준선이 커지는 배율.</summary>
        public float ReadyPulseScale => readyPulseScale;

        public Color ColorFor(CannonAimState state) =>
            state == CannonAimState.Ready ? readyColor : state == CannonAimState.Reloading ? reloadingColor : blockedColor;

        public static CannonOperatorSettings Create(float interactDistance = 3f, float interactHalfAngle = 60f)
        {
            var value = CreateInstance<CannonOperatorSettings>();
            value.interactDistance = Mathf.Max(0.1f, interactDistance);
            value.interactHalfAngle = Mathf.Clamp(interactHalfAngle, 0f, 180f);
            return value;
        }
    }
}
