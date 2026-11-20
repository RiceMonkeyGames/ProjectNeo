using System;
using ProjectNeo.Characters;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 런 안에서 살아 있는 유닛 하나. <see cref="Id"/>는 이 런 안에서만 쓰는 고유 번호이고,
    /// 콘텐츠 종류(정의 에셋)와 구분한다. 전투 결과는 이 번호로 누가 죽고 도망쳤는지 돌려준다.
    /// </summary>
    public abstract class RunUnit
    {
        public int Id { get; }

        protected RunUnit(int id)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            Id = id;
        }
    }

    /// <summary>런의 영웅. 플레이어 캐릭터도 조종하는 영웅이라 같은 클래스를 쓴다.</summary>
    public sealed class RunHero : RunUnit
    {
        public Character Character { get; }
        public HeroDefinition Definition => Character.Definition;
        public bool IsPlayer { get; }
        /// <summary>
        /// 영웅 등급(PREP-10, Q-73). 1부터이고 고용 등급(<see cref="HeroDefinition.Tier"/>)에서 시작한다. 등급마다 스킬 칸이 하나씩 열린다.
        /// 플레이어는 등급을 쓰지 않는다. 바꾸는 것은 <see cref="RunState"/>의 승급 규칙뿐이다.
        /// </summary>
        public int Grade { get; internal set; } = 1;
        /// <summary>다음 등급까지 모은 전투 경험치(PREP-10). 다음 등급에 필요한 값을 넘지 않는다.</summary>
        public int Exp { get; internal set; }

        public RunHero(int id, Character character, bool isPlayer) : base(id)
        {
            Character = character ?? throw new ArgumentNullException(nameof(character));
            IsPlayer = isPlayer;
        }
    }

    /// <summary>런의 잡졸 한 명. 죽으면 런에서 사라지고, 도망치면 남아서 다음 전투에 돌아온다.</summary>
    public sealed class RunSoldier : RunUnit
    {
        public SoldierDefinition Definition { get; }

        public RunSoldier(int id, SoldierDefinition definition) : base(id)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
        }
    }
}
