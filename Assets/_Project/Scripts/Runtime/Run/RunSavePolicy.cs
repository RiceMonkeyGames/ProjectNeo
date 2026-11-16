using System;

namespace ProjectNeo.Run
{
    /// <summary>런을 저장하는 계기(FLOW-02).</summary>
    public enum RunSaveTrigger
    {
        /// <summary>준비 단계에 들어온 직후.</summary>
        PreparationStart = 0,
        /// <summary>전투를 시작하기 직전. 준비에서 한 일(구매·건설·편성)이 모두 담긴다.</summary>
        BattleStart = 1,
        /// <summary>승리 보상을 받은 직후. 다음 스테이지 준비로 이어지게 저장한다.</summary>
        RewardGranted = 2,
        /// <summary>준비 단계에서 게임을 끌 때.</summary>
        QuitDuringPreparation = 3,
    }

    /// <summary>
    /// 언제 런을 저장하고 지울지 정하는 스위치 묶음(FLOW-02). 저장 시점을 바꾸려면 여기 값만 바꾸면 된다.
    /// 전투 중 상태는 저장하지 않으므로, 전투 중에 끄면 마지막 저장(보통 그 전투 직전의 준비)으로 돌아간다.
    /// </summary>
    [Serializable]
    public sealed class RunSavePolicy
    {
        public bool onPreparationStart = true;
        public bool onBattleStart = true;
        public bool onRewardGranted = true;
        public bool onQuitDuringPreparation = true;
        /// <summary>패배로 런이 끝나면 런 저장을 지운다.</summary>
        public bool deleteOnDefeat = true;

        public bool Allows(RunSaveTrigger trigger)
        {
            switch (trigger)
            {
                case RunSaveTrigger.PreparationStart: return onPreparationStart;
                case RunSaveTrigger.BattleStart: return onBattleStart;
                case RunSaveTrigger.RewardGranted: return onRewardGranted;
                case RunSaveTrigger.QuitDuringPreparation: return onQuitDuringPreparation;
                default: return false;
            }
        }
    }
}
