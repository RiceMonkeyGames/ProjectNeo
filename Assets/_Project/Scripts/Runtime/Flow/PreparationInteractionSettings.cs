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
        [Tooltip("준비 중 1인칭 탐색에서 부대 편성 화면을 여는 키(D-8).")]
        [SerializeField] InputAction organization = new InputAction("OpenOrganization", InputActionType.Button, "<Keyboard>/o");
        [SerializeField] InputAction startBattle =new InputAction("StartBattle", InputActionType.Button, "<Keyboard>/b");
        [Tooltip("준비 중 1인칭 탐색에서 부대 배치 화면을 열고 닫는 키(PREP-05).")]
        [SerializeField] InputAction squadPlacement = new InputAction("OpenSquadPlacement", InputActionType.Button, "<Keyboard>/p");
        public InputAction SquadPlacement => squadPlacement;
        [SerializeField, Min(0.1f)] float battleStartHoldSeconds = 1f;
        [Tooltip("준비 중 1인칭 탐색에서 누르고 있으면 준비 시간을 한 번 기다리는 키(PREP-06).")]
        [SerializeField] InputAction wait = new InputAction("WaitPrepTime", InputActionType.Button, "<Keyboard>/t");
        [SerializeField, Min(0.1f)] float waitHoldSeconds = 1f;
        public InputAction Wait => wait;
        [Tooltip("준비 중 1인칭 탐색에서 다음 전투 적 목록 창을 열고 닫는 키(C-5).")]
        [SerializeField] InputAction enemyPreview = new InputAction("ToggleEnemyPreview", InputActionType.Button, "<Keyboard>/v");
        public InputAction EnemyPreview => enemyPreview;
        [Tooltip("준비 중 1인칭 탐색에서 전체 키 목록을 열고 닫는 키(UI-06).")]
        [SerializeField] InputAction keyList = new InputAction("ToggleKeyList", InputActionType.Button, "<Keyboard>/h");
        public InputAction KeyList => keyList;
        public float WaitHoldSeconds => Mathf.Max(0.1f, waitHoldSeconds);
        public float Distance => distance;
        public InputAction Interact => interact;
        public InputAction Close => close;
        public InputAction Equipment => equipment;
        public InputAction Organization => organization;
        public InputAction StartBattle => startBattle;
        public float BattleStartHoldSeconds => Mathf.Max(0.1f, battleStartHoldSeconds);
        public static PreparationInteractionSettings Create(float distance = 3.5f, float battleStartHoldSeconds = 1f)
        {
            var value = CreateInstance<PreparationInteractionSettings>();
            value.distance = Mathf.Max(0.1f, distance);
            value.battleStartHoldSeconds = Mathf.Max(0.1f, battleStartHoldSeconds);
            return value;
        }
    }
}
