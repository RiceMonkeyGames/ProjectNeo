using System;
using System.Collections.Generic;

namespace ProjectNeo.Squads
{
    /// <summary>
    /// 부대 하나. 부대원은 인원 상한까지만 넣을 수 있다.
    /// 부대장은 없어도 되고, 인원 상한에 포함하지 않는다.
    /// </summary>
    public sealed class Squad<TUnit> where TUnit : class
    {
        readonly List<TUnit> members = new List<TUnit>();

        public int Capacity { get; }
        public TUnit Leader { get; set; }
        public IReadOnlyList<TUnit> Members => members;

        /// <summary>
        /// 부대의 사기(Q-44). 런 부대가 전투 사이에 들고 다닌다. null이면 아직 전투에 나간 적이 없는 새 부대이며,
        /// 처음 쓰는 쪽이 <see cref="ProjectNeo.Morale.MoraleSettings.CreateSquadMorale"/>로 시작값(150)을 넣는다.
        /// 부대가 없어지면 사기도 함께 없어진다.
        /// </summary>
        public ProjectNeo.Morale.SquadMorale Morale { get; set; }

        public Squad(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public bool IsFull => members.Count >= Capacity;

        /// <summary>자리가 있고 아직 이 부대에 없는 유닛이면 넣고 true.</summary>
        public bool TryAdd(TUnit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (IsFull || members.Contains(unit)) return false;
            members.Add(unit);
            return true;
        }

        public bool Remove(TUnit unit) => members.Remove(unit);
    }

    /// <summary>플레이어가 편성한 부대 전체. 부대 수 상한을 지킨다.</summary>
    public sealed class Army<TUnit> where TUnit : class
    {
        readonly List<Squad<TUnit>> squads = new List<Squad<TUnit>>();
        readonly SquadConfig config;

        public IReadOnlyList<Squad<TUnit>> Squads => squads;

        public Army(SquadConfig config)
        {
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
        }

        public bool CanAddSquad => squads.Count < config.MaxSquads;
        /// <summary>부대 수 상한.</summary>
        public int MaxSquads => config.MaxSquads;

        /// <summary>부대 수 상한에 여유가 있으면 빈 부대를 만들어 돌려주고, 없으면 null.</summary>
        public Squad<TUnit> TryAddSquad()
        {
            if (!CanAddSquad) return null;
            var squad = new Squad<TUnit>(config.MaxMembersPerSquad);
            squads.Add(squad);
            return squad;
        }

        public bool RemoveSquad(Squad<TUnit> squad) => squads.Remove(squad);
    }
}
