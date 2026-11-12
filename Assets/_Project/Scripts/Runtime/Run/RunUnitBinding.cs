using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Flow;
using ProjectNeo.Squads;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 씬에 미리 놓인 유닛(<typeparamref name="T"/>)과 런 유닛 번호를 짝짓는다. 씬 편성으로 런 데이터를 만들고,
    /// 전투가 끝나면 씬 유닛의 생사로 <see cref="BattleResult"/>를 만든다. 씬을 알지 않아 일반 C#으로 검증한다.
    /// 런에서 이미 빠진 유닛(앞 전투에서 죽은 유닛)은 결과에 넣지 않는다.
    /// </summary>
    public sealed class RunUnitBinding<T> where T : class
    {
        readonly Dictionary<int, T> objects = new Dictionary<int, T>();
        readonly Dictionary<T, int> ids = new Dictionary<T, int>();

        public RunState State { get; }

        RunUnitBinding(RunState state) => State = state;

        /// <summary>
        /// 씬 편성으로 새 런 데이터를 만든다. <paramref name="squads"/>의 줄 하나가 부대 하나이고, 칸마다 씬 유닛과 잡졸 정의다.
        /// 정의가 없는 유닛은 런에 넣을 수 없어 예외다.
        /// </summary>
        public static RunUnitBinding<T> Create(SquadConfig config, HeroDefinition playerHero, T player,
            IReadOnlyList<IReadOnlyList<KeyValuePair<T, SoldierDefinition>>> squads, int seed = 0)
        {
            if (playerHero == null) throw new ArgumentNullException(nameof(playerHero));
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (squads == null) throw new ArgumentNullException(nameof(squads));

            var binding = new RunUnitBinding<T>(new RunState(config, seed));
            binding.Bind(player, binding.State.AddHero(playerHero, true));
            foreach (IReadOnlyList<KeyValuePair<T, SoldierDefinition>> members in squads)
            {
                Squad<RunUnit> squad = binding.State.Army.TryAddSquad()
                    ?? throw new ArgumentException("씬 부대 수가 부대 수 상한보다 많다.", nameof(squads));
                foreach (KeyValuePair<T, SoldierDefinition> member in members)
                {
                    if (member.Key == null) continue;
                    if (member.Value == null) throw new ArgumentException("잡졸 정의가 없는 씬 유닛이 있다.", nameof(squads));
                    RunSoldier soldier = binding.State.AddSoldier(member.Value);
                    if (!binding.State.TryAssign(soldier, squad))
                        throw new ArgumentException("씬 부대 인원이 부대당 인원 상한보다 많다.", nameof(squads));
                    binding.Bind(member.Key, soldier);
                }
            }
            return binding;
        }

        void Bind(T target, RunUnit unit)
        {
            if (ids.ContainsKey(target)) throw new ArgumentException("같은 씬 유닛이 두 번 들어 있다.");
            ids.Add(target, unit.Id);
            objects.Add(unit.Id, target);
        }

        /// <summary>씬 유닛의 런 번호. 짝이 없으면 0.</summary>
        public int IdOf(T target) => target != null && ids.TryGetValue(target, out int id) ? id : 0;

        public T ObjectOf(int id) => objects.TryGetValue(id, out T target) ? target : null;

        /// <summary>아직 런에 남아 있는 유닛의 씬 오브젝트.</summary>
        public List<T> ObjectsInRun()
        {
            var list = new List<T>();
            foreach (KeyValuePair<int, T> pair in objects)
                if (State.Find(pair.Key) != null) list.Add(pair.Value);
            return list;
        }

        /// <summary>
        /// 씬 유닛의 생사로 전투 결과를 만든다. 런에 남아 있는 유닛만 담는다.
        /// <paramref name="kills"/>는 처치한 적 종류별 수이며 null이면 비운다.
        /// </summary>
        public BattleResult CreateResult(int battleNumber, BattleOutcome outcome, BattleEndReason reason,
            Func<T, bool> isDead, CastleState castleAfter, IReadOnlyDictionary<string, int> kills = null)
        {
            if (isDead == null) throw new ArgumentNullException(nameof(isDead));
            var result = new BattleResult(battleNumber, outcome, reason) { CastleAfter = castleAfter };
            foreach (KeyValuePair<int, T> pair in objects)
                if (State.Find(pair.Key) != null)
                    result.SetFate(pair.Key, isDead(pair.Value) ? UnitFate.Dead : UnitFate.Alive);
            if (kills != null)
                foreach (KeyValuePair<string, int> kill in kills)
                    if (!string.IsNullOrEmpty(kill.Key) && kill.Value > 0) result.AddKill(kill.Key, kill.Value);
            return result;
        }
    }
}
