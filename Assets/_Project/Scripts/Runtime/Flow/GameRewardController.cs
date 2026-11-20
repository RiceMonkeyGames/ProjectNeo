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
        /// <summary>마지막 보상 골드 중 퍽(보상 골드 효과)으로 더해진 몫(META-02).</summary>
        public int PerkGoldBonus { get; private set; }
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
            // 런은 패배(런 결과) 또는 마지막 스테이지 뒤 엔딩(FLOW-03)으로 끝난다.
            if (phase == GamePhase.RunResult || phase == GamePhase.Ending) TryGrantRunEnd(RunBossesKilled);
        }

        // 이 런에서 처치한 보스 수(SIEGE-09). 전투 결과를 반영할 때 런 점수 기록에 쌓인다.
        int RunBossesKilled
        {
            get
            {
                RunState state = battle != null ? battle.RunLink.State : null;
                return state != null ? state.ScoreTally.BossesKilled : 0;
            }
        }

        /// <summary>이 런이 끝날 때 받은 해금 포인트. 아직 받지 않았으면 -1.</summary>
        public int RunEndUnlockPoints { get; private set; } = -1;
        /// <summary>런 종료 해금 포인트를 셀 때 쓴 클리어 스테이지 수.</summary>
        public int RunEndStagesCleared { get; private set; }

        /// <summary>
        /// 런이 끝날 때 해금 포인트를 한 번 준다(META-08, Q-76): 클리어한 스테이지와 처치한 보스 수에 따라
        /// <see cref="StageRewardTable"/>의 값만큼 메타 진행에 넣고 바로 저장한다. 런은 패배 또는 마지막 스테이지 뒤
        /// 엔딩(FLOW-03)으로 끝나며, 엔딩이면 지금 스테이지까지 깬 것으로 센다. 승천 단계의 배율(Q-78)을 곱하고 올림한다.
        /// 단계가 바뀔 때는 런 점수 기록의 보스 처치 수(SIEGE-09)를 넘긴다. 이미 줬거나 연결이 없으면 false.
        /// </summary>
        public bool TryGrantRunEnd(int bossesKilled = 0)
        {
            if (RunEndUnlockPoints >= 0 || table == null || flow.Run == null) return false;
            int cleared = flow.Phase == GamePhase.Ending ? flow.Run.StageNumber : flow.Run.StageNumber - 1;
            RunEndStagesCleared = Mathf.Max(0, cleared);
            int basePoints = table.RunEndUnlockPoints(RunEndStagesCleared, bossesKilled);
            RunState state = battle != null ? battle.RunLink.State : null;
            RunEndUnlockPoints = state != null ? state.Ascension.ScaleReward(basePoints) : basePoints;
            RunEndMultiplier = state != null ? state.Ascension.RewardMultiplier : 1f;
            GameSession.Meta.unlockPoints += RunEndUnlockPoints;
            SettleScore(state);
            GameSession.SaveMeta();
            return true;
        }

        /// <summary>이 런의 점수(META-04). 런이 끝나기 전이거나 런 데이터가 없으면 null.</summary>
        public RunScore LastScore { get; private set; }
        /// <summary>이번 런 점수가 슬롯의 최고 점수를 넘었는지.</summary>
        public bool IsNewBest { get; private set; }

        // 런 점수를 한 번 계산하고, 슬롯 최고 점수보다 높으면 바꾼다(저장은 부르는 쪽이 한다).
        void SettleScore(RunState state)
        {
            if (state == null || table == null) return;
            LastScore = RunScore.Calculate(table, state.ScoreTally, state.LivingHeroCount, state.LivingGruntCount,
                state.Wallet.Gold, flow.Phase == GamePhase.Ending, state.Ascension.RewardMultiplier);
            MetaProgress meta = GameSession.Meta;
            IsNewBest = LastScore.Total > meta.bestRunScore;
            if (IsNewBest) meta.bestRunScore = LastScore.Total;
        }

        /// <summary>런 종료 해금 포인트에 곱한 승천 배율(FLOW-03). 기본 게임이면 1.</summary>
        public float RunEndMultiplier { get; private set; } = 1f;

        /// <summary>런 결과·엔딩 화면에 보여줄 글. 런 종료 포인트를 아직 주지 않았으면 빈 문자열.</summary>
        public string DescribeRunEnd()
        {
            if (RunEndUnlockPoints < 0) return string.Empty;
            string multiplier = RunEndMultiplier > 1f ? $" (Ascension x{RunEndMultiplier:0.0#})" : string.Empty;
            string text = $"Stages cleared {RunEndStagesCleared}\nUnlock points +{RunEndUnlockPoints}{multiplier} (total {GameSession.Meta.unlockPoints})";
            if (LastScore != null)
                text += $"\nRun score {RunScore.Number(LastScore.Total)}" + (IsNewBest ? " - New best!" : $" (best {RunScore.Number(GameSession.Meta.bestRunScore)})")
                    + $"\n{LastScore.Breakdown()}";
            return text;
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
            // 승천의 승리 골드 감소(FLOW-03)를 먼저 적용한다. 단계 0이면 그대로다.
            reward.ScaleGold(state.Ascension.VictoryGold);
            // 내실 퍽의 보상 골드 효과(META-02)는 (승천 적용 뒤) 굴린 골드 전체에 붙는다.
            PerkGoldBonus = state.PerkRewardGoldBonus(reward.Gold);
            reward.AddGold(PerkGoldBonus);
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
            if (PerkGoldBonus > 0) text.Append($" - perk bonus +{PerkGoldBonus}");
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
