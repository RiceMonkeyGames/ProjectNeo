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
        public float Distance => distance;
        public InputAction Interact => interact;
        public InputAction Close => close;
        public static PreparationInteractionSettings Create(float distance = 3.5f)
        {
            var value = CreateInstance<PreparationInteractionSettings>();
            value.distance = Mathf.Max(0.1f, distance);
            return value;
        }
    }
}
