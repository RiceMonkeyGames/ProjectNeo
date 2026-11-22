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
        [Tooltip("깃발 옆 큰 제목(UI-06 A안, \"스테이지 N\"). 비어 있으면 단계 줄만 쓴다.")]
        [SerializeField] TMP_Text stageTitle;
        [Tooltip("동전 아이콘 옆 골드 숫자(UI-06 A안). 있으면 자원 줄에는 재료만 적는다.")]
        [SerializeField] TMP_Text goldLabel;
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

        public string StageTitleText => stageTitle != null ? stageTitle.text : string.Empty;

        [Tooltip("제목 오른쪽의 승천 표시(UI-12). 승천 0이면 숨긴다. 비어 있으면 쓰지 않는다.")]
        [SerializeField] TMP_Text ascensionLabel;
        public string AscensionText => ascensionLabel != null && ascensionLabel.gameObject.activeSelf ? ascensionLabel.text : string.Empty;

        /// <summary>제목 오른쪽 승천 표시를 붙인다(UI-12 사이드바). null이면 뗀다.</summary>
        public void ConfigureAscension(TMP_Text label) => ascensionLabel = label;

        /// <summary>깃발 옆 큰 제목을 붙인다(UI-06 A안).</summary>
        public void ConfigureTitle(TMP_Text title) => stageTitle = title;

        public string GoldText => goldLabel != null ? goldLabel.text : string.Empty;

        /// <summary>골드 숫자를 따로 보여 준다(UI-06 A안). 자원 줄에는 재료만 남는다.</summary>
        public void ConfigureGold(TMP_Text gold) => goldLabel = gold;

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
            if (stageTitle != null) stageTitle.text = PrepHudText.StageTitle(flow.Run.StageNumber);
            if (ascensionLabel != null)
            {
                string tag = PrepHudText.AscensionTag(flow.Run.AscensionLevel);
                ascensionLabel.text = tag;
                if (ascensionLabel.gameObject.activeSelf != tag.Length > 0) ascensionLabel.gameObject.SetActive(tag.Length > 0);
            }
            if (resourcesLabel == null) return;
            materials.Clear();
            foreach (KeyValuePair<MaterialDefinition, int> entry in state.Wallet.Materials)
                if (entry.Key != null) materials.Add(new KeyValuePair<string, int>(entry.Key.DisplayName, entry.Value));
            if (goldLabel == null) { resourcesLabel.text = PrepHudText.Resources(state.Wallet.Gold, materials); return; }
            goldLabel.text = PrepHudText.Gold(state.Wallet.Gold);
            string materialLine = PrepHudText.Materials(materials);
            resourcesLabel.text = materialLine;
            if (resourcesLabel.gameObject.activeSelf != materialLine.Length > 0) resourcesLabel.gameObject.SetActive(materialLine.Length > 0);
        }

        void OnDisable()
        {
            if (content != null) content.SetActive(false);
            if (phaseBox != null) phaseBox.SetActive(true);
        }
    }
}
