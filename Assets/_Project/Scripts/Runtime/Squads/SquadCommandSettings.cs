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
        // COMBAT-08 진형. 횡대는 위의 formationSpacing을 쓴다. 예전 에셋에 없는 값은 0으로 읽혀 기본값을 쓴다.
        [SerializeField] InputAction cycleFormation = new InputAction("CycleFormation", InputActionType.Button, "<Keyboard>/g");
        [SerializeField] InputAction dragFormation = new InputAction("DragFormation", InputActionType.Button, "<Mouse>/rightButton");
        // UI-12 준비 화면 분대 카드 페이지 넘기기. 예전 에셋에 없으면 기본 키([ ])를 쓴다.
        [SerializeField] InputAction previousPage = new InputAction("PreviousSquadPage", InputActionType.Button, DefaultPreviousPageBinding);
        [SerializeField] InputAction nextPage = new InputAction("NextSquadPage", InputActionType.Button, DefaultNextPageBinding);
        [SerializeField, Min(0f)] float closeSpacing = DefaultCloseSpacing;
        [SerializeField, Min(0f)] float looseSpacing = DefaultLooseSpacing;
        [SerializeField, Min(0)] int columnFiles = DefaultColumnFiles;
        [SerializeField, Min(0f)] float minDragLength = DefaultMinDragLength;
        [SerializeField, Min(0f)] float depthProbeDistance = DefaultDepthProbeDistance;
        [SerializeField, Min(0f)] float refillDelay = DefaultRefillDelay;
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

        public const string DefaultCycleFormationBinding = "<Keyboard>/g";
        public const string DefaultPreviousPageBinding = "<Keyboard>/leftBracket", DefaultNextPageBinding = "<Keyboard>/rightBracket";
        static InputAction fallbackPreviousPage, fallbackNextPage;

        /// <summary>분대 카드 이전 페이지 키(UI-12). 비어 있으면 기본 키. 쓰는 쪽에서 복제해 쓴다.</summary>
        public InputAction PreviousPage => HasBinding(previousPage) ? previousPage
            : fallbackPreviousPage ??= new InputAction("PreviousSquadPage", InputActionType.Button, DefaultPreviousPageBinding);
        /// <summary>분대 카드 다음 페이지 키(UI-12). 비어 있으면 기본 키. 쓰는 쪽에서 복제해 쓴다.</summary>
        public InputAction NextPage => HasBinding(nextPage) ? nextPage
            : fallbackNextPage ??= new InputAction("NextSquadPage", InputActionType.Button, DefaultNextPageBinding);
        static bool HasBinding(InputAction action) => action != null && action.bindings.Count > 0;
        public const string DefaultDragFormationBinding = "<Mouse>/rightButton";
        public const float DefaultCloseSpacing = 0.9f, DefaultLooseSpacing = 2.5f;
        public const int DefaultColumnFiles = 2;
        public const float DefaultMinDragLength = 1f, DefaultDepthProbeDistance = 20f, DefaultRefillDelay = 2f;

        /// <summary>
        /// 자리를 지키는 분대에서 전사·도망으로 빈자리가 생기면 이만큼(초) 모았다가 한 번에 뒤에서 메운다.
        /// 예전 에셋의 0은 기본값으로 읽는다.
        /// </summary>
        public float RefillDelay => refillDelay > 0f ? refillDelay : DefaultRefillDelay;

        /// <summary>빈자리 메우기 대기 시간을 정한다(테스트용). 0 이하는 기본값.</summary>
        public void ConfigureRefillDelay(float seconds) => refillDelay = seconds;

        /// <summary>선택한 부대의 진형을 다음으로 바꾸는 키(임시, Q-11에서 확정).</summary>
        public InputAction CycleFormation => cycleFormation;
        /// <summary>1인칭에서 누른 채 끌어 전열을 긋는 버튼.</summary>
        public InputAction DragFormation => dragFormation;
        /// <summary>종대의 한 줄 인원(드래그 폭을 쓰지 않는다).</summary>
        public int ColumnFiles => columnFiles > 0 ? columnFiles : DefaultColumnFiles;
        /// <summary>이보다 짧게 끌면 클릭으로 본다(m).</summary>
        public float MinDragLength => minDragLength > 0f ? minDragLength : DefaultMinDragLength;
        /// <summary>대형 앞뒤로 걸을 수 있는 거리를 재는 최대 거리(m). 이보다 넓으면 막히지 않은 것으로 본다.</summary>
        public float DepthProbeDistance => depthProbeDistance > 0f ? depthProbeDistance : DefaultDepthProbeDistance;

        /// <summary>진형별 병사 간격(m). 횡대·종대는 <see cref="FormationSpacing"/>.</summary>
        public float Spacing(FormationKind kind) => kind switch
        {
            FormationKind.Close => closeSpacing > 0f ? closeSpacing : DefaultCloseSpacing,
            FormationKind.Loose => looseSpacing > 0f ? looseSpacing : DefaultLooseSpacing,
            _ => formationSpacing,
        };

        /// <summary>진형 간격을 정한다(테스트·셋업용). 0 이하는 기본값.</summary>
        public void ConfigureFormations(float close, float loose, int column)
        {
            closeSpacing = close; looseSpacing = loose; columnFiles = column;
        }
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
