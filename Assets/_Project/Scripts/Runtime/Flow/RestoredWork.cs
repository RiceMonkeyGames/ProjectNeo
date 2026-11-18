using ProjectNeo.Run;
using ProjectNeo.Walls;

namespace ProjectNeo.Flow
{
    /// <summary>저장에서 되살린 작업 주문 하나와 그 대상(SIEGE-08). 조각 또는 무기 중 하나만 있다.</summary>
    public readonly struct RestoredWork
    {
        public WorkOrder Order { get; }
        public PlacedStructure Piece { get; }
        public PlacedSiegeWeapon Weapon { get; }

        public RestoredWork(WorkOrder order, PlacedStructure piece, PlacedSiegeWeapon weapon)
        {
            Order = order; Piece = piece; Weapon = weapon;
        }
    }
}
