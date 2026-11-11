using ProjectNeo.Flow;
using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 흐름 뼈대를 확인하는 임시 화면. 현재 단계를 보여주고 단계에 맞는 버튼만 켠다.
    /// 승리·패배 버튼은 실제 전투가 연결되기 전까지 전투 결과를 대신 넣는 자리표시다.
    /// </summary>
    public sealed class GameFlowHud : MonoBehaviour
    {
        [SerializeField] RuntimeSystemFont font;
        [SerializeField] TMP_Text phaseLabel;
        [SerializeField] GameObject startBattleButton;
        [SerializeField] GameObject victoryButton;
        [SerializeField] GameObject defeatButton;
        [SerializeField] GameObject nextStageButton;
        [SerializeField] GameObject returnToTitleButton;
        GameFlow flow;

        public void Configure(RuntimeSystemFont systemFont, TMP_Text phase, GameObject startBattle, GameObject victory,
            GameObject defeat, GameObject nextStage, GameObject returnToTitle)
        {
            font = systemFont; phaseLabel = phase; startBattleButton = startBattle; victoryButton = victory;
            defeatButton = defeat; nextStageButton = nextStage; returnToTitleButton = returnToTitle;
        }

        public void StartBattle() => flow.StartBattle();
        public void Win() => flow.FinishBattle(BattleOutcome.Victory);
        public void Lose() => flow.FinishBattle(BattleOutcome.Defeat);
        public void NextStage() => flow.ContinueToNextStage();
        public void ReturnToTitle() => flow.ReturnToTitle();

        void OnEnable()
        {
            flow = GameSession.Flow;
            flow.PhaseChanged += Refresh;
        }

        void Start() => Refresh(flow.Phase);

        void OnDisable() => flow.PhaseChanged -= Refresh;

        void Refresh(GamePhase phase)
        {
            startBattleButton.SetActive(phase == GamePhase.Preparation);
            victoryButton.SetActive(phase == GamePhase.Battle);
            defeatButton.SetActive(phase == GamePhase.Battle);
            nextStageButton.SetActive(phase == GamePhase.Reward);
            returnToTitleButton.SetActive(phase == GamePhase.RunResult);
            int stage = flow.Run != null ? flow.Run.StageNumber : 0;
            bool korean = font != null && font.Available;
            phaseLabel.text = phase switch
            {
                GamePhase.Preparation => korean ? $"준비 · 스테이지 {stage}" : $"Preparation - Stage {stage}",
                GamePhase.Battle => korean ? $"전투 · 스테이지 {stage}" : $"Battle - Stage {stage}",
                GamePhase.Reward => korean ? $"승리 · 보상 (스테이지 {stage})" : $"Victory - Reward (Stage {stage})",
                GamePhase.RunResult => korean ? $"패배 · 런 결과 (도달 스테이지 {stage})" : $"Defeat - Run result (Stage {stage})",
                _ => string.Empty,
            };
        }
    }
}
