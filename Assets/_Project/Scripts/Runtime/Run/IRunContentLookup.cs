using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Walls;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 저장 파일의 ID를 정의 에셋으로, 정의 에셋을 ID로 바꾼다(FLOW-02). 런 저장·복원은 이 창구만 알고
    /// 에셋이 어디서 오는지(목록 에셋, 테스트용 메모리 목록)는 모른다. 없는 ID는 null을 돌려준다.
    /// </summary>
    public interface IRunContentLookup
    {
        ItemDefinition Item(string id);
        MaterialDefinition Material(string id);
        HeroDefinition Hero(string id);
        SoldierDefinition Soldier(string id);
        FacilityDefinition Facility(FacilityType type);
        BuildPieceDefinition Piece(string id);
        SiegeWeaponDefinition Weapon(string id);

        /// <summary>성 조각의 저장용 ID. 조각 정의에는 ID 필드가 없어 창구가 정한다.</summary>
        string PieceId(BuildPieceDefinition piece);
    }
}
