using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Squads
{
    /// <summary>부대 지휘의 키와 대형 수치. 키는 이 에셋의 인스펙터에서 바꾼다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Squad Command Settings")]
    public sealed class SquadCommandSettings : ScriptableObject
    {
        [SerializeField] InputAction[] selectSquad =
        {
            new InputAction("SelectSquad1", InputActionType.Button, "<Keyboard>/f1"),
            new InputAction("SelectSquad2", InputActionType.Button, "<Keyboard>/f2"),
            new InputAction("SelectSquad3", InputActionType.Button, "<Keyboard>/f3"),
            new InputAction("SelectSquad4", InputActionType.Button, "<Keyboard>/f4"),
        };
        [SerializeField] InputAction selectAll = new InputAction("SelectAll", InputActionType.Button, "<Keyboard>/backquote");
        [SerializeField] InputAction move = new InputAction("OrderMove", InputActionType.Button, "<Keyboard>/1");
        [SerializeField] InputAction attack = new InputAction("OrderAttack", InputActionType.Button, "<Keyboard>/2");
        [SerializeField] InputAction hold = new InputAction("OrderHold", InputActionType.Button, "<Keyboard>/3");
        [SerializeField, Min(1)] int formationColumns = 5;
        [SerializeField, Min(0.1f)] float formationSpacing = 1.4f;
        [SerializeField, Min(1f)] float maxAimDistance = 120f;
        [SerializeField, Min(0.1f)] float navigationSnapDistance = 2f;
        [SerializeField, Range(0f, 1f)] float minimumSurfaceUp = 0.5f;
        [SerializeField, Min(0f)] float verticalSnapTolerance = 0.5f;
        public InputAction[] SelectSquad => selectSquad;
        public InputAction SelectAll => selectAll;
        public InputAction Move => move;
        public InputAction Attack => attack;
        public InputAction Hold => hold;
        public int FormationColumns => formationColumns;
        public float FormationSpacing => formationSpacing;
        // 화면 가운데에서 이동 위치를 찾는 최대 거리와, 가리킨 곳을 걸을 수 있는 곳으로 맞추는 허용 거리.
        public float MaxAimDistance => maxAimDistance;
        public float NavigationSnapDistance => navigationSnapDistance;
        public float MinimumSurfaceUp => minimumSurfaceUp;
        public float VerticalSnapTolerance => verticalSnapTolerance;

        /// <summary>부대 선택 키를 통째로 바꾼다. 키 하나가 부대 하나다(예: "&lt;Keyboard&gt;/f5").</summary>
        public void ConfigureSelectKeys(params string[] bindings)
        {
            selectSquad = new InputAction[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
                selectSquad[i] = new InputAction("SelectSquad" + (i + 1), InputActionType.Button, bindings[i]);
        }

        public static SquadCommandSettings Create(int columns = 5, float spacing = 1.4f)
        {
            var value = CreateInstance<SquadCommandSettings>();
            value.formationColumns = Mathf.Max(1, columns);
            value.formationSpacing = Mathf.Max(0.1f, spacing);
            return value;
        }
    }
}
