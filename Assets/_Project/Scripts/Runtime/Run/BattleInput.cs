using System.Collections.Generic;

namespace ProjectNeo.Run
{
    /// <summary>전투에 들어가는 부대 하나. 순번은 런의 부대 순서와 같다.</summary>
    public sealed class BattleSquadInput
    {
        public int SquadIndex { get; }
        /// <summary>부대장 영웅. 없을 수 있다.</summary>
        public RunHero Leader { get; }
        public IReadOnlyList<RunSoldier> Members { get; }

        public BattleSquadInput(int squadIndex, RunHero leader, IReadOnlyList<RunSoldier> members)
        {
            SquadIndex = squadIndex;
            Leader = leader;
            Members = members;
        }
    }

    /// <summary>
    /// 런이 전투에 넘기는 입력. 전투는 이것으로 아군과 성을 만들고, 끝나면 같은 <see cref="BattleNumber"/>로
    /// <see cref="BattleResult"/>를 돌려준다. 성 기록은 복사본이라 전투가 고쳐도 런은 바뀌지 않는다.
    /// 적 편성(웨이브)은 전투 담당의 데이터라 여기에 없다.
    /// </summary>
    public sealed class BattleInput
    {
        public int BattleNumber { get; }
        public int StageNumber { get; }
        public RunHero Player { get; }
        public IReadOnlyList<BattleSquadInput> Squads { get; }
        public CastleState Castle { get; }

        public BattleInput(int battleNumber, int stageNumber, RunHero player,
            IReadOnlyList<BattleSquadInput> squads, CastleState castle)
        {
            BattleNumber = battleNumber;
            StageNumber = stageNumber;
            Player = player;
            Squads = squads;
            Castle = castle;
        }
    }
}
