namespace ProjectNeo.Run
{
    /// <summary>
    /// 승천 단계 규칙의 종류(FLOW-03, Q-78). 에셋에 숫자로 저장되므로 기존 값의 숫자는 바꾸지 말고 새 값은 끝에 추가한다.
    /// 값의 뜻은 종류마다 다르다: Percent는 0.15가 +15%, Hours·Morale은 더하는 양이다.
    /// </summary>
    public enum AscensionRuleKind
    {
        /// <summary>적 최대 체력 비율(+0.15 = +15%).</summary>
        EnemyHealthPercent = 0,
        /// <summary>적 공격력 비율.</summary>
        EnemyAttackPercent = 1,
        /// <summary>적 이동 속도 비율.</summary>
        EnemyMoveSpeedPercent = 2,
        /// <summary>런 시작 골드 비율(-0.25 = -25%).</summary>
        StartingGoldPercent = 3,
        /// <summary>준비 시간 예산에 더하는 시간(-2 = 2시간 줄어듦).</summary>
        PrepBudgetHours = 4,
        /// <summary>승리 보상 골드 비율.</summary>
        VictoryGoldPercent = 5,
        /// <summary>웨이브 무리마다 적 수 비율. 늘어난 수는 올림한다.</summary>
        EnemyCountPercent = 6,
        /// <summary>새 부대의 시작 사기에 더하는 양.</summary>
        StartingMorale = 7,
        /// <summary>보스 최대 체력 비율. 보스가 생기기 전(SIEGE-09)에는 효과가 없다.</summary>
        BossHealthPercent = 8,
        /// <summary>보스 공격력 비율. 보스가 생기기 전(SIEGE-09)에는 효과가 없다.</summary>
        BossAttackPercent = 9,
        /// <summary>성벽 수리 시간 비율(+1 = 2배).</summary>
        RepairTimePercent = 10,
    }
}
