using System.Collections.Generic;
using System.Text;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using TMPro;
using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// Game 씬 준비 단계의 시설 공통 틀(D-0). 런 데이터에 시설 상태를 붙이고, 건물에서 F로 시설 팝업을 열며
    /// Esc나 닫기 버튼으로 닫는다. 팝업이 열린 동안은 <see cref="GamePhaseView.IsPanelView"/>로 이동·시점을 멈춘다.
    /// 시설 목록은 <see cref="RunConfig"/> 에셋에서 바꾼다. 각 시설의 기능(고용·구매·모집·제작·업그레이드)은 후속 작업이다.
    /// <see cref="GameBattleController"/>와 같은 오브젝트에 붙어 그 런 연결을 쓴다.
    /// </summary>
    [RequireComponent(typeof(GameBattleController))]
    public sealed class GameFacilities : MonoBehaviour
    {
        [SerializeField] RunConfig startConfig;
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text status;
        [SerializeField] TMP_Text body;
        GameBattleController battle;
        GameFlow flow;

        /// <summary>지금 열린 시설. 닫혀 있으면 null.</summary>
        public FacilityType? OpenFacility { get; private set; }
        public bool IsOpen => OpenFacility.HasValue;
        public string TitleText => title != null ? title.text : string.Empty;
        public string StatusText => status != null ? status.text : string.Empty;
        public string BodyText => body != null ? body.text : string.Empty;

        public void Configure(RunConfig start, GamePhaseView view, GameObject popup, TMP_Text titleLabel, TMP_Text statusLabel,
            TMP_Text bodyLabel)
        {
            startConfig = start; phaseView = view; panel = popup; title = titleLabel; status = statusLabel; body = bodyLabel;
        }

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            flow = GameSession.Flow;
            if (panel != null) panel.SetActive(false);
        }

        RunState State => battle != null ? battle.RunLink.State : null;

        /// <summary>런 데이터의 시설 상태. 아직 없으면 설정의 시설을 시작 레벨로 붙인다. 런이 없으면 null.</summary>
        public FacilityState Facility(FacilityType type)
        {
            RunState state = State;
            if (state == null) return null;
            if (startConfig != null) state.AddFacilities(startConfig.Facilities);
            return state.Facility(type);
        }

        public string NameOf(FacilityType type)
        {
            if (startConfig != null)
                foreach (FacilityDefinition definition in startConfig.Facilities)
                    if (definition != null && definition.Type == type) return definition.DisplayName;
            return type.ToString();
        }

        /// <summary>준비 중 1인칭 탐색에서 시설 팝업을 연다. 전투 중·건설 중·시설이 없으면 false.</summary>
        public bool Open(FacilityType type)
        {
            if (flow.Phase != GamePhase.Preparation || phaseView == null || panel == null) return false;
            if (Facility(type) == null)
            {
                Debug.LogWarning("런 데이터에 이 시설이 없어 팝업을 열지 않는다: " + type);
                return false;
            }
            if (!phaseView.EnterPanel()) return false;
            OpenFacility = type;
            panel.SetActive(true);
            Refresh();
            return true;
        }

        /// <summary>팝업을 닫고 1인칭 탐색으로 돌아간다. 닫기 버튼도 이것을 부른다.</summary>
        public void Close()
        {
            if (phaseView != null) phaseView.ExitPanel();
            Hide();
        }

        void Hide()
        {
            OpenFacility = null;
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            if (!OpenFacility.HasValue) return;
            // Esc(PreparationInteraction)나 단계 전환으로 팝업 화면이 끝나면 함께 닫는다.
            if (phaseView == null || !phaseView.IsPanelView) { Hide(); return; }
            Refresh();
        }

        void Refresh()
        {
            FacilityState facility = OpenFacility.HasValue ? Facility(OpenFacility.Value) : null;
            if (facility == null) return;
            if (title != null) title.text = facility.Definition.DisplayName;
            if (status != null) status.text = $"레벨 {facility.Level + 1} / {facility.Definition.Levels.Count}\n" + DescribeWallet(State.Wallet);
            if (body != null) body.text = Purpose(facility.Definition.Type) + "\n\n이 시설의 기능은 다음 작업에서 붙습니다.";
        }

        static string DescribeWallet(Wallet wallet)
        {
            var text = new StringBuilder($"골드 {wallet.Gold}");
            foreach (KeyValuePair<MaterialDefinition, int> material in wallet.Materials)
                if (material.Key != null && material.Value > 0) text.Append($" · {material.Key.DisplayName} {material.Value}");
            return text.ToString();
        }

        static string Purpose(FacilityType type) => type switch
        {
            FacilityType.Tavern => "영웅 고용",
            FacilityType.Shop => "아이템 구매·판매",
            FacilityType.TrainingGround => "잡졸 모집·강화",
            FacilityType.Smithy => "장비 생산·강화·판매",
            FacilityType.Defense => "성벽·무기 준비",
            _ => string.Empty,
        };
    }
}
