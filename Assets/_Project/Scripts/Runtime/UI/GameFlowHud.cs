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
        [SerializeField] TMP_Text phaseLabel;
        [SerializeField] GameObject startBattleButton;
        [SerializeField] GameObject victoryButton;
        [SerializeField] GameObject defeatButton;
        [SerializeField] GameObject nextStageButton;
        [SerializeField] GameObject returnToTitleButton;
        [Tooltip("마지막 스테이지 보상 뒤 엔딩으로 가는 버튼(FLOW-03). 이때는 다음 스테이지 버튼 대신 보인다.")]
        [SerializeField] GameObject finishRunButton;
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] PreparationInteraction interaction;
        [SerializeField] TMP_Text interactionHint;
        [SerializeField] GameObject explorationReticle;
        [SerializeField] GameBattleController battle;
        [SerializeField] TMP_Text battleStatus;
        [SerializeField] CannonOperator cannonOperator;
        [Tooltip("조준점 바로 아래 상호작용 안내(UI-06). 비어 있으면 예전처럼 아래 안내 줄에 키 목록과 함께 적는다.")]
        [SerializeField] TMP_Text contextPrompt;
        CannonReticleView cannonReticle;
        /// <summary>조준점 아래 상호작용 안내 글. 보이지 않으면 빈 글.</summary>
        public string ContextPrompt => contextPrompt != null && contextPrompt.transform.parent.gameObject.activeSelf ? contextPrompt.text : string.Empty;
        public void ConfigureContextPrompt(TMP_Text prompt) => contextPrompt = prompt;
        /// <summary>대포 조작 중 조준선 표시(SIEGE-07). 한 번도 타지 않았으면 null.</summary>
        public CannonReticleView CannonReticle => cannonReticle;
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

        public void Configure(TMP_Text phase, GameObject startBattle, GameObject victory,
            GameObject defeat, GameObject nextStage, GameObject returnToTitle)
        {
            phaseLabel = phase; startBattleButton = startBattle; victoryButton = victory;
            defeatButton = defeat; nextStageButton = nextStage; returnToTitleButton = returnToTitle;
        }

        public void ConfigureFinishRun(GameObject finishRun) => finishRunButton = finishRun;

        /// <summary>마지막 스테이지 보상을 마치고 엔딩으로 간다(FLOW-03).</summary>
        public void FinishRun() => flow.FinishRun();

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
            bool runEnded = flow.Phase == GamePhase.RunResult && rewards != null && rewards.RunEndUnlockPoints >= 0;
            bool ending = flow.Phase == GamePhase.Ending && battle != null;
            interactionHint.transform.parent.gameObject.SetActive(preparation);
            ShowContextPrompt(preparation && phaseView.IsExplorationView && interaction.Target != null);
            if (battleStatus != null) battleStatus.transform.parent.gameObject.SetActive(fighting || rewarding || runEnded || ending);
            if (ending)
            {
                // 엔딩(FLOW-03): 깬 단계·열린 승천 단계 아래에 런 종료 해금 포인트(META-08)를 보여준다.
                string runEnd = rewards != null ? rewards.DescribeRunEnd() : string.Empty;
                if (battleStatus != null) battleStatus.text = EndingText() + (runEnd.Length > 0 ? "\n" + runEnd : string.Empty);
                explorationReticle.SetActive(false);
                return;
            }
            explorationReticle.SetActive(preparation && phaseView.IsExplorationView);

            // 대포에 탄 동안에만 조준선에 발사 가능·재장전 상태를 보여준다.
            if (fighting && cannonOperator != null && cannonOperator.Manned != null)
            {
                if (cannonReticle == null) cannonReticle = CannonReticleView.Attach(explorationReticle);
                cannonReticle.Show(cannonOperator);
            }
            else if (cannonReticle != null) cannonReticle.Hide();
            if (rewarding)
            {
                // 전투 상태 자리에 이번 승리의 보상 내역을 보여준다.
                if (battleStatus != null) battleStatus.text = rewards.Describe();
                return;
            }
            if (runEnded)
            {
                // 패배로 런이 끝나면 같은 자리에 런 종료 해금 포인트를 보여준다(META-08).
                if (battleStatus != null) battleStatus.text = rewards.DescribeRunEnd();
                return;
            }
            if (fighting)
            {
                if (battleStatus == null) return;
                battleStatus.text = $"적 {battle.EnemiesAlive} - 아군 {battle.AlliesAlive}\n성문: " + (battle.GateDestroyed ? "파괴됨" : "온전함");
                if (battle.IsRetreating) battleStatus.text += " - 후퇴 중";
                // 웨이브가 여럿인 전투에서만 진행 상황을 보여 준다(B-7.1).
                if (battle.WaveCount > 1)
                {
                    float next = battle.SecondsUntilNextWave;
                    battleStatus.text += $"\n웨이브 {battle.WavesReleased}/{battle.WaveCount}" + (next >= 0f ? $" - 다음 {Mathf.CeilToInt(next)}초" : " - 마지막");
                }
                foreach (var actor in ProjectNeo.Combat.CharacterActor.ActiveActors)
                {
                    if (actor.gameObject.scene != gameObject.scene || actor.GetComponent<ProjectNeo.Characters.PlayerInputController>() == null) continue;
                    var traveller = actor.GetComponent<ProjectNeo.Siege.LadderTraveller>();
                    if (traveller != null)
                    {
                        if (traveller.PlayerHint.Length > 0) battleStatus.text += "\n" + traveller.PlayerHint;
                        if (traveller.HasInterruptNotice) battleStatus.text += "\n" + "공격을 받아 사다리 밀기가 끊겼습니다!";
                    }
                    break;
                }
                foreach (var ladder in battle.Ladders)
                    if (ladder.PlayerCanPush)
                    { battleStatus.text += $"\n{battle.Settings.Siege.PushLadder.GetBindingDisplayString()} · 사다리 밀기 {Mathf.RoundToInt(ladder.State.PushProgress * 100f)}%"; break; }
                string consumable = battle.Consumables != null ? battle.Consumables.Hint() : string.Empty;
                if (consumable.Length > 0) battleStatus.text += "\n" + consumable;
                string cannon = CannonHint();
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
            // 짧은 안내(UI-06): 탐색 중 아래 줄은 전투 시작·기다리기와 경고만 담고, 키 목록은 H 창(KeyListHud)으로 옮겼다.
            // 예전 씬(조준점 아래 안내 없음)은 PREP-05처럼 키 목록과 부대 키 줄까지 담는다.
            bool shortHints = contextPrompt != null;
            bool tall = phaseView.IsPlacementView || phaseView.IsExplorationView && !shortHints;
            prompt.sizeDelta = new Vector2(-32f, tall ? 96f : 64f);
            interactionHint.text = phaseView.IsBuildView
                ? $"건설 - {interaction.CloseKey} 돌아가기"
                : phaseView.IsPlacementView ? PlacementHint()
                : phaseView.IsPanelView
                ? $"{interaction.CloseKey} 닫기"
                : shortHints ? string.Empty
                : interaction.Target != null
                    ? $"{interaction.InteractKey} · {interaction.Target.Prompt}"
                    : $"WASD 이동 - {interaction.InteractKey} 상호작용 - {interaction.EquipmentKey} 장비 - {interaction.OrganizationKey} 부대 - {interaction.EnemyPreviewKey} 적 정보"
                        + (battle != null && battle.PerkWindow != null ? $" - {battle.PerkWindow.KeyText} 특성" : string.Empty);
            if (shortHints && phaseView.IsExplorationView && interaction.Target != null)
                contextPrompt.text = PrepHudText.ContextPrompt(interaction.InteractKey, interaction.Target.Prompt);
            if (phaseView.IsExplorationView && battle != null)
            {
                string startHint = interaction.CanStartBattle
                    ? $"{interaction.BattleStartKey} 길게 누르기 - 전투 시작"
                    : "전장 준비 중";
                if (interaction.BattleStartProgress > 0f) startHint += $" · {Mathf.FloorToInt(interaction.BattleStartProgress * 100f)}%";
                // 준비 시간(PREP-06): 기다릴 시간이 남아 있으면 같은 줄에 기다리기 키를 붙인다.
                if (interaction.CanWait)
                {
                    startHint += $" - {interaction.WaitKey} 길게 누르기 - {battle.RunLink.State.PrepTime.WaitHours:0.#}시간 기다리기";
                    if (interaction.WaitProgress > 0f) startHint += $" · {Mathf.FloorToInt(interaction.WaitProgress * 100f)}%";
                }
                interactionHint.text = Join(interactionHint.text, startHint);
                // 미완료 작업 경고(Q-77): 전투 시작 줄 바로 아래에 둔다.
                string workWarning = WorkWarning();
                if (workWarning.Length > 0)
                {
                    interactionHint.text += "\n" + workWarning;
                    prompt.sizeDelta = new Vector2(prompt.sizeDelta.x, prompt.sizeDelta.y + 24f);
                }
                string squadHint = shortHints ? string.Empty : PreparationSquadHint();
                if (squadHint.Length > 0) interactionHint.text += "\n" + squadHint;
                string gateMessage = battle.PrepGate != null ? battle.PrepGate.Message : "";
                if (gateMessage.Length > 0) interactionHint.text += "\n" + gateMessage;
            }
        }

        static string Join(string first, string second) => first.Length == 0 ? second : first + "\n" + second;

        void ShowContextPrompt(bool visible)
        {
            // 글과 그 배경(부모)을 함께 켜고 끈다. 아래 안내 줄(interactionHint)과 같은 짜임이다.
            GameObject box = contextPrompt != null ? contextPrompt.transform.parent.gameObject : null;
            if (box != null && box.activeSelf != visible) box.SetActive(visible);
        }

        string WorkWarning()
        {
            var build = battle.GetComponent<GameBuildEconomy>();
            GameConstructionWork work = build != null ? build.Work : null;
            return work != null ? work.BattleStartWarning() : string.Empty;
        }

        /// <summary>
        /// 준비 중 1인칭 탐색에서 보여주는 부대 키 한 줄(PREP-05). 전투용 키 안내 상자 대신 여기 한 곳에서만 만든다.
        /// 나중에 안내 방식을 바꿀 때는 이 함수만 고친다.
        /// </summary>
        string PreparationSquadHint()
        {
            GamePrepPlacement prep = phaseView.PrepPlacement;
            if (prep == null || prep.Center == null) return "";
            string keys = SquadCommandHud.KeyLine(prep.Center.Settings);
            string open = interaction.PlacementKey;
            return $"{keys} - {open} 부대 배치 화면";
        }

        /// <summary>부대 배치 화면 안내(PREP-05). 조작과 고른 부대, 마지막 알림을 보여준다.</summary>
        string PlacementHint()
        {
            GamePrepPlacement prep = phaseView.PrepPlacement;
            string back = $"{interaction.PlacementKey}·{interaction.CloseKey}";
            string keys = prep != null && prep.Center != null ? SquadCommandHud.SelectKeys(prep.Center.Settings) : "F1~F10·`";
            string text = $"부대 배치 - {keys} 또는 유닛 클릭으로 선택 - 좌클릭 이동(끌면 전열) - 우클릭 선택 해제 - {back} 돌아가기";
            if (prep == null) return text;
            text += "\n" + "선택: " + prep.SelectionText();
            if (prep.Message.Length > 0) text += " · " + prep.Message;
            return text;
        }

        /// <summary>대포 안내 한 줄. 타고 있으면 조작법과 장전 상태, 가까이 있으면 타는 키를 보여준다.</summary>
        public string CannonHint()
        {
            if (cannonOperator == null) return string.Empty;
            CannonModule manned = cannonOperator.Manned;
            if (manned != null)
            {
                string reload = manned.Reload.IsReady
                    ? "장전됨"
                    : $"재장전 {Mathf.FloorToInt(manned.Reload.Progress * 100f)}%";
                string text = $"대포 - 클릭 발사 - {cannonOperator.InteractKey}/Esc 내리기 - {reload}";
                if (!string.IsNullOrEmpty(cannonOperator.Message)) text += "\n" + cannonOperator.Message;
                return text;
            }
            return cannonOperator.Nearby != null
                ? $"{cannonOperator.InteractKey} - 대포 잡기"
                : string.Empty;
        }

        void Refresh(GamePhase phase)
        {
            startBattleButton.SetActive(battle == null && phase == GamePhase.Preparation);
            victoryButton.SetActive(battle == null && phase == GamePhase.Battle);
            defeatButton.SetActive(battle == null && phase == GamePhase.Battle);
            bool finalReward = phase == GamePhase.Reward && flow.Run != null && flow.Run.IsFinalStage;
            // 엔딩 버튼이 없는 씬에서는 다음 스테이지 버튼을 그대로 둔다(눌러도 마지막 스테이지에서는 넘어가지 않는다).
            nextStageButton.SetActive(phase == GamePhase.Reward && (!finalReward || finishRunButton == null));
            if (finishRunButton != null) finishRunButton.SetActive(finalReward);
            returnToTitleButton.SetActive(phase == GamePhase.RunResult || phase == GamePhase.Ending);
            int stage = flow.Run != null ? flow.Run.StageNumber : 0;
            // 승천 단계가 있으면 제목 뒤에 붙인다(FLOW-03).
            int ascension = flow.Run != null ? flow.Run.AscensionLevel : 0;
            string ascensionLabel = ascension > 0 ? $" - 승천 {ascension}" : string.Empty;

            // 런 데이터가 붙어 있으면 준비 중 보유 골드를 함께 보여준다.
            int? gold = flow.Run != null && flow.Run.State != null ? flow.Run.State.Wallet.Gold : (int?)null;
            string goldLabel = gold.HasValue ? $" - 골드 {gold}" : string.Empty;
            phaseLabel.text = phase switch
            {
                GamePhase.Preparation => ($"준비 - 스테이지 {stage}") + ascensionLabel + goldLabel,
                GamePhase.Battle => $"전투 - 스테이지 {stage}" + ascensionLabel,
                GamePhase.Reward => $"승리 - 보상 (스테이지 {stage})" + ascensionLabel,
                GamePhase.RunResult => $"패배 - 런 결과 (스테이지 {stage})" + ascensionLabel,
                GamePhase.Ending => "런 완료 - 모든 스테이지 클리어" + ascensionLabel,
                _ => string.Empty,
            };
        }

        /// <summary>엔딩 화면의 내역(FLOW-03): 깬 단계와 새로 열린 승천 단계.</summary>
        public string EndingText()
        {
            int level = flow.Run != null ? flow.Run.AscensionLevel : 0;
            string cleared = level > 0 ? $"승천 {level} 클리어." : "기본 게임 클리어.";
            int unlocked = battle != null ? battle.AscensionUnlockedAtEnding : 0;
            if (unlocked > 0) return cleared + $"\n승천 {unlocked} 해금! 타이틀 화면의 새 게임에서 고를 수 있습니다.";
            return cleared + (battle != null && battle.AscensionTable != null && level >= battle.AscensionTable.MaxLevel
                ? "\n가장 높은 승천입니다."
                : string.Empty);
        }
    }
}
