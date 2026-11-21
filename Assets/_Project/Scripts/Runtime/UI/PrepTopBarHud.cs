using System.Collections.Generic;
using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 준비 화면 위 자원 바(UI-06). 왼쪽에 단계·승천, 가운데에 준비 시간 게이지(PrepClockHud), 오른쪽에 골드와 재료를 보여 준다.
    /// 준비 중에는 단계 상자 대신 보이고, 거의 화면 전체를 덮는 창이 열려 있으면 숨는다. 경제 규칙에는 관여하지 않는다.
    /// </summary>
    public sealed class PrepTopBarHud : MonoBehaviour
    {
        [SerializeField] GameBattleController battle;
        [SerializeField] GameObject content;
        [SerializeField] TMP_Text stageLabel;
        [SerializeField] TMP_Text resourcesLabel;
        [Tooltip("준비 중에는 숨기는 기존 단계 상자(GameFlowHud Panel).")]
        [SerializeField] GameObject phaseBox;
        [Tooltip("열려 있는 동안 자원 바를 숨기는 화면(부대 편성 창).")]
        [SerializeField] GameObject[] hideWhileOpen = new GameObject[0];

        readonly List<KeyValuePair<string, int>> materials = new List<KeyValuePair<string, int>>();

        public bool IsShowing => content != null && content.activeSelf;
        public string StageText => stageLabel != null ? stageLabel.text : string.Empty;
        public string ResourcesText => resourcesLabel != null ? resourcesLabel.text : string.Empty;
        public RectTransform Area => content != null ? (RectTransform)content.transform : null;

        public void Configure(GameBattleController controller, GameObject bar, TMP_Text stage, TMP_Text resources,
            GameObject phasePanel, params GameObject[] hidden)
        {
            battle = controller; content = bar; stageLabel = stage; resourcesLabel = resources;
            phaseBox = phasePanel; hideWhileOpen = hidden ?? new GameObject[0];
        }

        bool Covered()
        {
            foreach (GameObject window in hideWhileOpen)
                if (window != null && window.activeInHierarchy) return true;
            return false;
        }

        void LateUpdate()
        {
            if (content == null) return;
            GameFlow flow = GameSession.Flow;
            RunState state = battle != null ? battle.RunLink.State : null;
            bool preparation = flow.Phase == GamePhase.Preparation && flow.Run != null && state != null;
            if (phaseBox != null && phaseBox.activeSelf == preparation) phaseBox.SetActive(!preparation);
            bool show = preparation && !Covered();
            if (content.activeSelf != show) content.SetActive(show);
            if (!show) return;
            if (stageLabel != null) stageLabel.text = PrepHudText.StageLine(flow.Run.StageNumber, flow.Run.AscensionLevel);
            if (resourcesLabel == null) return;
            materials.Clear();
            foreach (KeyValuePair<MaterialDefinition, int> entry in state.Wallet.Materials)
                if (entry.Key != null) materials.Add(new KeyValuePair<string, int>(entry.Key.DisplayName, entry.Value));
            resourcesLabel.text = PrepHudText.Resources(state.Wallet.Gold, materials);
        }

        void OnDisable()
        {
            if (content != null) content.SetActive(false);
            if (phaseBox != null) phaseBox.SetActive(true);
        }
    }
}
