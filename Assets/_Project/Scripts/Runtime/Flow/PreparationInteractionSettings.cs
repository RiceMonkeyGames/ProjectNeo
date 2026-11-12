using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Flow
{
    [CreateAssetMenu(menuName = "ProjectNeo/Flow/Preparation Interaction")]
    public sealed class PreparationInteractionSettings : ScriptableObject
    {
        [SerializeField, Min(0.1f)] float distance = 3.5f;
        [SerializeField] InputAction interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/f");
        [SerializeField] InputAction close = new InputAction("CloseConstruction", InputActionType.Button, "<Keyboard>/escape");
        [Tooltip("준비 중 1인칭 탐색에서 장비 화면을 여는 키(D-7).")]
        [SerializeField] InputAction equipment = new InputAction("OpenEquipment", InputActionType.Button, "<Keyboard>/i");
        public float Distance => distance;
        public InputAction Interact => interact;
        public InputAction Close => close;
        public InputAction Equipment => equipment;
        public static PreparationInteractionSettings Create(float distance = 3.5f)
        {
            var value = CreateInstance<PreparationInteractionSettings>();
            value.distance = Mathf.Max(0.1f, distance);
            return value;
        }
    }
}
