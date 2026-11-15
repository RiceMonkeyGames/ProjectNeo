using ProjectNeo.Flow;
using ProjectNeo.Siege;
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
        [SerializeField] CannonOperator cannonOperator;
        GameFlow flow;
        public string InteractionHint => interactionHint != null ? interactionHint.text : string.Empty;
        public string PhaseText => phaseLabel != null ? phaseLabel.text : string.Empty;
        /// <summary>전투 상태 또는 보상 내역 칸의 글.</summary>
        public string StatusText => battleStatus != null ? battleStatus.text : string.Empty;
        public void ConfigureBattle(GameBattleController controller, TMP_Text status)
        { battle = controller; battleStatus = status; }

        /// <summary>전투 중 대포 탑승·조작 안내(SIEGE-02).</summary>
        public void ConfigureCannon(CannonOperator controller) => cannonOperator = controller;

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
            // 준비 중에는 건설·상점·업그레이드로 골드가 바뀌므로 제목의 골드를 매 프레임 맞춘다.
            if (flow.Phase == GamePhase.Preparation) Refresh(flow.Phase);
            if (interactionHint == null || interaction == null || phaseView == null) return;
            bool preparation = flow.Phase == GamePhase.Preparation;
            bool fighting = flow.Phase == GamePhase.Battle && battle != null;
            GameRewardController rewards = battle != null ? battle.GetComponent<GameRewardController>() : null;
            bool rewarding = flow.Phase == GamePhase.Reward && rewards != null && rewards.LastReward != null;
            interactionHint.transform.parent.gameObject.SetActive(preparation);
            if (battleStatus != null) battleStatus.transform.parent.gameObject.SetActive(fighting || rewarding);
            explorationReticle.SetActive(preparation && phaseView.IsExplorationView);
            bool korean = font != null && font.Available;
            if (rewarding)
            {
                // 전투 상태 자리에 이번 승리의 보상 내역을 보여준다.
                if (battleStatus != null) battleStatus.text = rewards.Describe(korean);
                return;
            }
            if (fighting)
            {
                if (battleStatus == null) return;
                battleStatus.text = korean
                    ? $"남은 적 {battle.EnemiesAlive} · 부대원 {battle.AlliesAlive}\n성문: " + (battle.GateDestroyed ? "파괴됨" : "정상")
                    : $"Enemies {battle.EnemiesAlive} - Allies {battle.AlliesAlive}\nGate: " + (battle.GateDestroyed ? "Destroyed" : "Intact");
                if (battle.IsRetreating) battleStatus.text += korean ? " · 적 퇴각 중" : " - Retreating";
                foreach (var actor in ProjectNeo.Combat.CharacterActor.ActiveActors)
                {
                    if (actor.gameObject.scene != gameObject.scene || actor.GetComponent<ProjectNeo.Characters.PlayerInputController>() == null) continue;
                    var traveller = actor.GetComponent<ProjectNeo.Siege.LadderTraveller>();
                    if (traveller != null && traveller.PlayerHint.Length > 0) battleStatus.text += "\n" + traveller.PlayerHint;
                    break;
                }
                foreach (var ladder in battle.Ladders)
                    if (ladder.PlayerCanPush)
                    { battleStatus.text += $"\n{battle.Settings.Siege.PushLadder.GetBindingDisplayString()} · 사다리 밀기 {Mathf.RoundToInt(ladder.State.PushProgress * 100f)}%"; break; }
                string consumable = battle.Consumables != null ? battle.Consumables.Hint(korean) : string.Empty;
                if (consumable.Length > 0) battleStatus.text += "\n" + consumable;
                string cannon = CannonHint(korean);
                if (cannon.Length > 0) battleStatus.text += "\n" + cannon;
                explorationReticle.SetActive(cannonOperator != null && cannonOperator.Manned != null);
                return;
            }
            if (!preparation) return;
            // 건설 도구가 있는 왼쪽 공간을 비우고, 탐색 중에는 아래 중앙에 안내한다.
            var prompt = (RectTransform)interactionHint.transform.parent;
            prompt.anchorMin = new Vector2(phaseView.IsBuildView ? 0.32f : 0.15f, 0f);
            prompt.anchorMax = new Vector2(phaseView.IsBuildView ? 0.98f : 0.85f, 0f);
            prompt.pivot = new Vector2(0.5f, 0f);
            prompt.anchoredPosition = new Vector2(0f, 16f);
            // 탐색 안내는 부대 키 줄과 성문 알림까지 담는다(PREP-05).
            prompt.sizeDelta = new Vector2(-32f, phaseView.IsExplorationView || phaseView.IsPlacementView ? 96f : 64f);
            interactionHint.text = phaseView.IsBuildView
                ? (korean ? $"건설 모드 · {interaction.CloseKey} 돌아가기" : $"Construction - {interaction.CloseKey} to return")
                : phaseView.IsPlacementView ? PlacementHint(korean)
                : phaseView.IsPanelView
                ? (korean ? $"{interaction.CloseKey} 닫기" : $"{interaction.CloseKey} to close")
                : interaction.Target != null
                    ? $"{interaction.InteractKey} · {interaction.Target.Prompt}"
                    : (korean ? $"WASD 이동 · {interaction.InteractKey} 상호작용 · {interaction.EquipmentKey} 장비 · {interaction.OrganizationKey} 편성"
                        : $"WASD move - {interaction.InteractKey} interact - {interaction.EquipmentKey} gear - {interaction.OrganizationKey} squads");
            if (phaseView.IsExplorationView && battle != null)
            {
                string startHint = interaction.CanStartBattle
                    ? (korean ? $"{interaction.BattleStartKey} 길게 누르기 · 전투 시작" : $"Hold {interaction.BattleStartKey} - Start battle")
                    : (korean ? "전장 준비 중" : "Preparing battlefield");
                if (interaction.BattleStartProgress > 0f) startHint += $" · {Mathf.FloorToInt(interaction.BattleStartProgress * 100f)}%";
                interactionHint.text += "\n" + startHint;
                string squadHint = PreparationSquadHint(korean);
                if (squadHint.Length > 0) interactionHint.text += "\n" + squadHint;
                string gateMessage = battle.PrepGate != null ? battle.PrepGate.Message : "";
                if (gateMessage.Length > 0) interactionHint.text += "\n" + gateMessage;
            }
        }

        /// <summary>
        /// 준비 중 1인칭 탐색에서 보여주는 부대 키 한 줄(PREP-05). 전투용 키 안내 상자 대신 여기 한 곳에서만 만든다.
        /// 나중에 안내 방식을 바꿀 때는 이 함수만 고친다.
        /// </summary>
        string PreparationSquadHint(bool korean)
        {
            GamePrepPlacement prep = phaseView.PrepPlacement;
            if (prep == null || prep.Center == null) return "";
            string keys = SquadCommandHud.KeyLine(prep.Center.Settings);
            string open = interaction.PlacementKey;
            return korean ? $"{keys} · {open} 부대 배치 화면" : $"{keys} - {open} squad placement view";
        }

        /// <summary>부대 배치 화면 안내(PREP-05). 조작과 고른 부대, 마지막 알림을 보여준다.</summary>
        string PlacementHint(bool korean)
        {
            GamePrepPlacement prep = phaseView.PrepPlacement;
            string back = $"{interaction.PlacementKey}·{interaction.CloseKey}";
            string text = korean
                ? $"부대 배치 · F1~F4·` 또는 유닛 클릭으로 선택 · 좌클릭 이동 · 우클릭 해제 · {back} 돌아가기"
                : $"Squad placement - F1-F4/` or click a unit to select - left click to move - right click to clear - {back} to return";
            if (prep == null) return text;
            text += "\n" + (korean ? "선택: " : "Selected: ") + prep.SelectionText();
            if (prep.Message.Length > 0) text += " · " + prep.Message;
            return text;
        }

        /// <summary>대포 안내 한 줄. 타고 있으면 조작법과 장전 상태, 가까이 있으면 타는 키를 보여준다.</summary>
        public string CannonHint(bool korean)
        {
            if (cannonOperator == null) return string.Empty;
            CannonModule manned = cannonOperator.Manned;
            if (manned != null)
            {
                string reload = manned.Reload.IsReady
                    ? (korean ? "장전 완료" : "Loaded")
                    : (korean ? $"재장전 {Mathf.FloorToInt(manned.Reload.Progress * 100f)}%" : $"Reloading {Mathf.FloorToInt(manned.Reload.Progress * 100f)}%");
                string text = korean
                    ? $"대포 조작 · 좌클릭 발사 · {cannonOperator.InteractKey}/Esc 내리기 · {reload}"
                    : $"Cannon - Click fire - {cannonOperator.InteractKey}/Esc leave - {reload}";
                if (!string.IsNullOrEmpty(cannonOperator.Message)) text += "\n" + cannonOperator.Message;
                return text;
            }
            return cannonOperator.Nearby != null
                ? (korean ? $"{cannonOperator.InteractKey} · 대포 조작" : $"{cannonOperator.InteractKey} - Man cannon")
                : string.Empty;
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
            // 런 데이터가 붙어 있으면 준비 중 보유 골드를 함께 보여준다.
            int? gold = flow.Run != null && flow.Run.State != null ? flow.Run.State.Wallet.Gold : (int?)null;
            string goldLabel = gold.HasValue ? (korean ? $" · 골드 {gold}" : $" - Gold {gold}") : string.Empty;
            phaseLabel.text = phase switch
            {
                GamePhase.Preparation => (korean ? $"준비 · 스테이지 {stage}" : $"Preparation - Stage {stage}") + goldLabel,
                GamePhase.Battle => korean ? $"전투 · 스테이지 {stage}" : $"Battle - Stage {stage}",
                GamePhase.Reward => korean ? $"승리 · 보상 (스테이지 {stage})" : $"Victory - Reward (Stage {stage})",
                GamePhase.RunResult => korean ? $"패배 · 런 결과 (도달 스테이지 {stage})" : $"Defeat - Run result (Stage {stage})",
                _ => string.Empty,
            };
        }
    }
}
