using System;
using ProjectNeo.Walls;

namespace ProjectNeo.Run
{
    /// <summary>성 조각 한 종류를 지을 때 드는 골드.</summary>
    [Serializable]
    public struct PieceGoldCost
    {
        public BuildPieceDefinition piece;
        public int gold;

        public PieceGoldCost(BuildPieceDefinition piece, int gold) { this.piece = piece; this.gold = gold; }
    }
}
