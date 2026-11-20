namespace ProjectNeo.Meta
{
    /// <summary>
    /// 유닛 능력치가 아닌 런 전체에 걸리는 퍽 효과(내실 트리, META-02 사용자 결정 2026-11-20).
    /// 숫자는 에셋에 저장되므로 바꾸지 말고 끝에 추가한다.
    /// </summary>
    public enum PerkRunEffectKind
    {
        /// <summary>승리 보상 골드를 이 퍼센트만큼 늘린다(값 25 = +25%).</summary>
        RewardGoldPercent = 0,
        /// <summary>상점 구매가(골드)를 이 퍼센트만큼 바꾼다(값 -20 = 20% 할인).</summary>
        ShopPricePercent = 1,
        /// <summary>준비 시간 예산에 이 시간을 더한다.</summary>
        PrepHours = 2,
    }
}
