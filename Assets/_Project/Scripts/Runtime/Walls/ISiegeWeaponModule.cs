namespace ProjectNeo.Walls
{
    public enum SiegeWeaponRemoval { SupportDestroyed = 0, Dismantled = 1 }

    /// <summary>
    /// 무기별 동작을 붙이는 자리. 발사·조준·충전 같은 동작은 이 인터페이스를 구현한 컴포넌트로 만들어
    /// 무기 프리팹에 붙인다. 공통 몸통(PlacedSiegeWeapon)과 설치 규칙은 무기 종류를 알지 않는다.
    /// </summary>
    public interface ISiegeWeaponModule
    {
        void OnInstalled(PlacedSiegeWeapon weapon);
        void OnRemoved(PlacedSiegeWeapon weapon, SiegeWeaponRemoval reason);
    }
}
