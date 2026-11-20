using System;
using System.Collections.Generic;
using System.Text;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 보상 단계에 들어오면 승리한 전투의 보상을 한 번 정해 런 지갑·보관함과 메타 진행에 넣는다(C-4).
    /// 수치는 <see cref="StageRewardTable"/> 에셋, 지휘관 레벨 규칙은 <see cref="MetaConfig"/> 에셋에서 바꾼다.
    /// <see cref="GameBattleController"/>와 같은 오브젝트에 붙어 그 런 연결을 쓴다.
    /// </summary>
    [RequireComponent(typeof(GameBattleController))]
    public sealed class GameRewardController : MonoBehaviour
    {
        [SerializeField] StageRewardTable table;
        [SerializeField] MetaConfig metaConfig;
        GameBattleController battle;
        GameFlow flow;

        /// <summary>이번 런에서 마지막으로 지급한 보상. 보상 화면이 읽는다.</summary>
        public StageReward LastReward { get; private set; }
        /// <summary>마지막 보상으로 오른 지휘관 레벨 수.</summary>
        public int LevelsGained { get; private set; }
        public StageRewardTable Table => table;
        public event Action<StageReward> RewardGranted;

        public void Configure(StageRewardTable rewardTable, MetaConfig meta) { table = rewardTable; metaConfig = meta; }

        void Awake()
        {
            battle = GetComponent<GameBattleController>();
            flow = GameSession.Flow;
        }

        void OnEnable() => flow.PhaseChanged += OnPhaseChanged;
        void OnDisable() => flow.PhaseChanged -= OnPhaseChanged;

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Reward) TryGrant();
            if (phase == GamePhase.RunResult) TryGrantRunEnd();
        }

        /// <summary>이 런이 끝날 때 받은 해금 포인트. 아직 받지 않았으면 -1.</summary>
        public int RunEndUnlockPoints { get; private set; } = -1;
        /// <summary>런 종료 해금 포인트를 셀 때 쓴 클리어 스테이지 수.</summary>
        public int RunEndStagesCleared { get; private set; }

        /// <summary>
        /// 런이 끝날 때 해금 포인트를 한 번 준다(META-08, Q-76): 클리어한 스테이지와 처치한 보스 수에 따라
        /// <see cref="StageRewardTable"/>의 값만큼 메타 진행에 넣고 바로 저장한다. 지금은 패배가 런의 끝이다
        /// (마지막 스테이지 뒤의 런 종료는 FLOW-03, 보스 처치 수는 SIEGE-09가 넘긴다). 이미 줬거나 연결이 없으면 false.
        /// </summary>
        public bool TryGrantRunEnd(int bossesKilled = 0)
        {
            if (RunEndUnlockPoints >= 0 || table == null || flow.Run == null) return false;
            RunEndStagesCleared = Mathf.Max(0, flow.Run.StageNumber - 1);
            RunEndUnlockPoints = table.RunEndUnlockPoints(RunEndStagesCleared, bossesKilled);
            GameSession.Meta.unlockPoints += RunEndUnlockPoints;
            GameSession.SaveMeta();
            return true;
        }

        /// <summary>런 결과 화면에 보여줄 글. 런 종료 포인트를 아직 주지 않았으면 빈 문자열.</summary>
        public string DescribeRunEnd()
        {
            if (RunEndUnlockPoints < 0) return string.Empty;
            return $"Stages cleared {RunEndStagesCleared}\nUnlock points +{RunEndUnlockPoints} (total {GameSession.Meta.unlockPoints})";
        }

        /// <summary>지급할 보상이 있으면 한 번 지급하고 true. 이미 받았거나 연결·표가 없으면 false.</summary>
        public bool TryGrant()
        {
            RunState state = battle != null ? battle.RunLink.State : null;
            if (state == null || table == null || flow.Run == null) return false;
            BattleResult result = state.LastBattleResult;
            if (result == null || result.Outcome != BattleOutcome.Victory || state.LastRewardedBattle >= result.BattleNumber)
                return false;

            var random = new SeededRandom(state.SeedFor(result.BattleNumber));
            StageReward reward = StageReward.Roll(table, flow.Run.StageNumber, result, random);
            if (!state.ApplyReward(reward)) return false;

            MetaProgress meta = GameSession.Meta;
            LevelsGained = metaConfig != null ? CommanderProgression.AddXp(meta, metaConfig, reward.CommanderXp) : 0;
            if (metaConfig == null) meta.commanderXp += reward.CommanderXp;
            GameSession.SaveMeta();
            // 보상을 받은 런을 다음 스테이지 준비로 이어지게 저장한다. 여기서 꺼도 같은 전투의 보상을 다시 받지 못한다(FLOW-02).
            battle.SaveRun(RunSaveTrigger.RewardGranted);
            LastReward = reward;
            RewardGranted?.Invoke(reward);
            return true;
        }

        /// <summary>보상 화면에 보여줄 글.</summary>
        public string Describe()
        {
            RunState state = battle != null ? battle.RunLink.State : null;
            if (LastReward == null || state == null) return string.Empty;
            var text = new StringBuilder();
            text.Append($"Gold +{LastReward.Gold} (total {state.Wallet.Gold})");
            foreach (KeyValuePair<MaterialDefinition, int> material in LastReward.Materials)
                text.Append($"\n{material.Key.DisplayName} +{material.Value} (total {state.Wallet.GetMaterial(material.Key)})");
            foreach (ItemDefinition item in LastReward.Items)
                text.Append($"\nItem: {item.DisplayName}");
            MetaProgress meta = GameSession.Meta;
            text.Append($"\nCommander XP +{LastReward.CommanderXp} (Lv {meta.commanderLevel})");
            if (LevelsGained > 0) text.Append($" - Level up! Perk points {meta.perkPoints}");
            return text.ToString();
        }
    }
}
