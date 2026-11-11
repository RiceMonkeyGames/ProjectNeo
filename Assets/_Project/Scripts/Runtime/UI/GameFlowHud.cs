using ProjectNeo.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] PreparationInteraction interaction;
        [SerializeField] TMP_Text interactionHint;
        [SerializeField] GameObject explorationReticle;
        [SerializeField] GameBattleController battle;
        [SerializeField] TMP_Text battleStatus;
        GameFlow flow;
        public string InteractionHint => interactionHint != null ? interactionHint.text : string.Empty;
        public void ConfigureBattle(GameBattleController controller, TMP_Text status)
        { battle = controller; battleStatus = status; }

        public void ConfigureInteraction(GamePhaseView view, PreparationInteraction controller, TMP_Text hint, GameObject reticle)
        { phaseView = view; interaction = controller; interactionHint = hint; explorationReticle = reticle; }

        public void Configure(RuntimeSystemFont systemFont, TMP_Text phase, GameObject startBattle, GameObject victory,
            GameObject defeat, GameObject nextStage, GameObject returnToTitle)
        {
            font = systemFont; phaseLabel = phase; startBattleButton = startBattle; victoryButton = victory;
            defeatButton = defeat; nextStageButton = nextStage; returnToTitleButton = returnToTitle;
        }

        public void StartBattle()
        {
            if (battle != null) battle.TryStartBattle();
            else flow.StartBattle();
        }
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

        void LateUpdate()
        {
            if (interactionHint == null || interaction == null || phaseView == null) return;
            bool preparation = flow.Phase == GamePhase.Preparation;
            bool fighting = flow.Phase == GamePhase.Battle && battle != null;
            interactionHint.transform.parent.gameObject.SetActive(preparation);
            if (battleStatus != null) battleStatus.transform.parent.gameObject.SetActive(fighting);
            explorationReticle.SetActive(preparation && phaseView.IsExplorationView);
            bool korean = font != null && font.Available;
            if (fighting)
            {
                if (battleStatus == null) return;
                battleStatus.text = korean
                    ? $"남은 적 {battle.EnemiesAlive} · 부대원 {battle.AlliesAlive}\n성문: " + (battle.GateDestroyed ? "파괴됨" : "정상")
                    : $"Enemies {battle.EnemiesAlive} - Allies {battle.AlliesAlive}\nGate: " + (battle.GateDestroyed ? "Destroyed" : "Intact");
                if (battle.IsRetreating) battleStatus.text += korean ? " · 적 퇴각 중" : " - Retreating";
                foreach (var ladder in battle.Ladders)
                    if (ladder.PlayerCanPush)
                    { battleStatus.text += $"\n{battle.Settings.Siege.PushLadder.GetBindingDisplayString()} · 사다리 밀기 {Mathf.RoundToInt(ladder.State.PushProgress * 100f)}%"; break; }
                return;
            }
            if (!preparation) return;
            // 건설 도구가 있는 왼쪽 공간을 비우고, 탐색 중에는 아래 중앙에 안내한다.
            var prompt = (RectTransform)interactionHint.transform.parent;
            prompt.anchorMin = new Vector2(phaseView.IsBuildView ? 0.32f : 0.15f, 0f);
            prompt.anchorMax = new Vector2(phaseView.IsBuildView ? 0.98f : 0.85f, 0f);
            prompt.pivot = new Vector2(0.5f, 0f);
            prompt.anchoredPosition = new Vector2(0f, 16f);
            prompt.sizeDelta = new Vector2(-32f, 64f);
            interactionHint.text = phaseView.IsBuildView
                ? (korean ? $"건설 모드 · {interaction.CloseKey} 돌아가기" : $"Construction - {interaction.CloseKey} to return")
                : interaction.Target != null
                    ? $"{interaction.InteractKey} · {interaction.Target.Prompt}"
                    : (korean ? $"WASD 이동 · {interaction.InteractKey} 상호작용 · 성문 앞 깃발에서 전투 시작"
                        : $"WASD move - {interaction.InteractKey} interact - Start battle at the flag near the gate");
        }

        void Refresh(GamePhase phase)
        {
            startBattleButton.SetActive(battle == null && phase == GamePhase.Preparation);
            victoryButton.SetActive(battle == null && phase == GamePhase.Battle);
            defeatButton.SetActive(battle == null && phase == GamePhase.Battle);
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
