namespace ProjectNeo.Core
{
    /// <summary>
    /// 저장 파일에 ID로 적히는 콘텐츠 종류(META-10). 이름 바꾸기 표가 종류별로 옛 ID를 찾는다.
    /// 값은 에셋에 저장되므로 바꾸지 않는다.
    /// </summary>
    public enum ContentKind
    {
        Item = 1,
        Material = 2,
        Hero = 3,
        Soldier = 4,
        CastlePiece = 5,
        SiegeWeapon = 6,
        Perk = 7,
        Skill = 8,
        UnlockEntry = 9,
    }
}
