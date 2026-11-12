using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Squads;

namespace ProjectNeo.Run
{
    /// <summary>전투 결과를 반영하지 못한 이유.</summary>
    public enum BattleResultIssue
    {
        None = 0,
        /// <summary>전투 입력을 만든 적이 없거나 이미 결과를 반영했다.</summary>
        NoBattlePending = 1,
        /// <summary>지금 진행 중인 전투의 번호가 아니다.</summary>
        WrongBattle = 2,
        /// <summary>이 런에 없는 유닛 번호가 들어 있다.</summary>
        UnknownUnit = 3,
    }

    /// <summary>
    /// 런 하나가 소유하는 데이터: 지갑·보관함·영웅·잡졸·부대 편성·성 기록. 씬과 화면을 알지 않는다.
    /// 전투 오브젝트가 사라져도 이 데이터는 남고, 다음 전투는 이것으로 다시 만든다.
    /// 살아남은 유닛은 다음 전투 전에 체력이 모두 회복되므로 체력은 담지 않는다(2026-11-12 사용자 결정).
    /// </summary>
    public sealed class RunState
    {
        readonly Dictionary<int, RunUnit> units = new Dictionary<int, RunUnit>();
        readonly List<RunHero> heroes = new List<RunHero>();
        readonly List<RunSoldier> soldiers = new List<RunSoldier>();
        readonly List<RunHero> fallenHeroes = new List<RunHero>();
        int nextUnitId = 1;
        int pendingBattle;

        public Wallet Wallet { get; } = new Wallet();
        public Inventory Inventory { get; } = new Inventory();
        public Army<RunUnit> Army { get; }
        public RunHero Player { get; private set; }
        /// <summary>살아 있는 영웅. 플레이어도 포함한다.</summary>
        public IReadOnlyList<RunHero> Heroes => heroes;
        public IReadOnlyList<RunSoldier> Soldiers => soldiers;
        /// <summary>
        /// 영구 사망한 영웅. 착용 장비를 그대로 가지고 있다. 장비를 잃는지 돌려받는지는 미정이라 지우지 않는다.
        /// </summary>
        public IReadOnlyList<RunHero> FallenHeroes => fallenHeroes;
        public CastleState Castle { get; private set; } = new CastleState();
        /// <summary>결과까지 반영한 전투 수.</summary>
        public int BattlesFought { get; private set; }
        public BattleResult LastBattleResult { get; private set; }
        public bool HasPendingBattle => pendingBattle > 0;
        /// <summary>보상을 지급한 마지막 전투 번호. 같은 전투의 보상은 두 번 받지 않는다.</summary>
        public int LastRewardedBattle { get; private set; }
        public StageReward LastReward { get; private set; }
        /// <summary>이 런의 랜덤 시드. 보상 등 런 안의 추첨은 이 값과 전투 번호로 시드를 만들어 재현한다.</summary>
        public int Seed { get; }

        public event Action Changed;

        public RunState(SquadConfig squadConfig, int seed = 0)
        {
            Army = new Army<RunUnit>(squadConfig);
            Seed = seed;
        }

        /// <summary>전투 하나에 쓸 랜덤 시드. 같은 런·같은 전투면 같다.</summary>
        public int SeedFor(int battleNumber) => unchecked(Seed * 486187739 + battleNumber * 16777619);

        /// <summary>
        /// 보상을 지급한다. 마지막으로 반영한 승리 전투의 보상이고 아직 받지 않았을 때만 지갑·보관함에 넣고 true다.
        /// 지휘관 경험치·해금 포인트는 메타 진행이라 여기서 다루지 않는다.
        /// </summary>
        public bool ApplyReward(StageReward reward)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            if (LastBattleResult == null || LastBattleResult.Outcome != Flow.BattleOutcome.Victory) return false;
            if (reward.BattleNumber != BattlesFought || LastRewardedBattle >= reward.BattleNumber) return false;
            Wallet.AddGold(reward.Gold);
            foreach (KeyValuePair<MaterialDefinition, int> material in reward.Materials)
                Wallet.AddMaterial(material.Key, material.Value);
            foreach (ItemDefinition item in reward.Items) Inventory.Add(item);
            LastRewardedBattle = reward.BattleNumber;
            LastReward = reward;
            Changed?.Invoke();
            return true;
        }

        /// <summary>설정의 시작 재화·플레이어·부대로 새 런을 만든다.</summary>
        public static RunState CreateNew(RunConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.SquadConfig == null) throw new ArgumentException("부대 설정이 없다.", nameof(config));
            if (config.PlayerHero == null) throw new ArgumentException("플레이어 영웅이 없다.", nameof(config));

            var state = new RunState(config.SquadConfig);
            state.Wallet.AddGold(config.StartingGold);
            foreach (MaterialAmount entry in config.StartingMaterials)
                if (entry.material != null && entry.amount > 0) state.Wallet.AddMaterial(entry.material, entry.amount);
            state.AddHero(config.PlayerHero, true);

            foreach (RunSquadSetup setup in config.StartingSquads)
            {
                Squad<RunUnit> squad = state.Army.TryAddSquad()
                    ?? throw new ArgumentException("시작 부대 수가 부대 수 상한보다 많다.", nameof(config));
                if (setup.soldiers == null) continue;
                foreach (SoldierDefinition definition in setup.soldiers)
                {
                    if (definition == null) continue;
                    if (!state.TryAssign(state.AddSoldier(definition), squad))
                        throw new ArgumentException("시작 부대 인원이 부대당 인원 상한보다 많다.", nameof(config));
                }
            }
            return state;
        }

        public RunUnit Find(int id) => units.TryGetValue(id, out RunUnit unit) ? unit : null;

        /// <summary>영웅을 런에 더한다. 플레이어는 한 명뿐이다.</summary>
        public RunHero AddHero(HeroDefinition definition, bool isPlayer = false)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (isPlayer && Player != null) throw new InvalidOperationException("플레이어 영웅은 이미 있다.");
            var hero = new RunHero(nextUnitId++, new Character(definition), isPlayer);
            units.Add(hero.Id, hero);
            heroes.Add(hero);
            if (isPlayer) Player = hero;
            Changed?.Invoke();
            return hero;
        }

        /// <summary>잡졸을 런에 더한다. 부대에는 <see cref="TryAssign"/>로 넣는다.</summary>
        public RunSoldier AddSoldier(SoldierDefinition definition)
        {
            var soldier = new RunSoldier(nextUnitId++, definition);
            units.Add(soldier.Id, soldier);
            soldiers.Add(soldier);
            Changed?.Invoke();
            return soldier;
        }

        /// <summary>잡졸이 속한 부대. 없으면 null.</summary>
        public Squad<RunUnit> SquadOf(RunSoldier soldier)
        {
            foreach (Squad<RunUnit> squad in Army.Squads)
                foreach (RunUnit member in squad.Members)
                    if (member == soldier) return squad;
            return null;
        }

        /// <summary>
        /// 잡졸을 부대에 넣는다. 다른 부대에 있었으면 옮긴다. 이 런의 잡졸·부대가 아니거나 자리가 없으면 false이고 바뀌지 않는다.
        /// </summary>
        public bool TryAssign(RunSoldier soldier, Squad<RunUnit> squad)
        {
            if (soldier == null || squad == null || Find(soldier.Id) != soldier || !Owns(squad)) return false;
            Squad<RunUnit> previous = SquadOf(soldier);
            if (previous == squad) return true;
            if (squad.IsFull) return false;
            previous?.Remove(soldier);
            squad.TryAdd(soldier);
            Changed?.Invoke();
            return true;
        }

        /// <summary>잡졸을 부대에서 뺀다. 런에는 남는다.</summary>
        public bool Unassign(RunSoldier soldier)
        {
            Squad<RunUnit> squad = soldier == null ? null : SquadOf(soldier);
            if (squad == null) return false;
            squad.Remove(soldier);
            Changed?.Invoke();
            return true;
        }

        /// <summary>영웅을 부대장으로 정한다. null이면 부대장을 비운다. 한 영웅은 한 부대만 맡는다.</summary>
        public bool TrySetLeader(Squad<RunUnit> squad, RunHero hero)
        {
            if (squad == null || !Owns(squad)) return false;
            if (hero != null)
            {
                if (Find(hero.Id) != hero) return false;
                foreach (Squad<RunUnit> other in Army.Squads)
                    if (other != squad && other.Leader == hero) return false;
            }
            squad.Leader = hero;
            Changed?.Invoke();
            return true;
        }

        public void SetCastle(CastleState castle)
        {
            Castle = (castle ?? throw new ArgumentNullException(nameof(castle))).Clone();
            Changed?.Invoke();
        }

        /// <summary>
        /// 다음 전투의 입력을 만든다. 결과를 반영하기 전에 다시 부르면 같은 전투 번호로 다시 만든다.
        /// </summary>
        public BattleInput CreateBattleInput(int stageNumber)
        {
            pendingBattle = BattlesFought + 1;
            var squads = new List<BattleSquadInput>(Army.Squads.Count);
            for (int i = 0; i < Army.Squads.Count; i++)
            {
                Squad<RunUnit> squad = Army.Squads[i];
                var members = new List<RunSoldier>(squad.Members.Count);
                foreach (RunUnit member in squad.Members)
                    if (member is RunSoldier soldier) members.Add(soldier);
                squads.Add(new BattleSquadInput(i, squad.Leader as RunHero, members));
            }
            return new BattleInput(pendingBattle, stageNumber, Player, squads, Castle.Clone());
        }

        /// <summary>전투 결과를 반영할 수 있는지 상태를 바꾸지 않고 확인한다.</summary>
        public BattleResultIssue CheckResult(BattleResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (pendingBattle == 0) return BattleResultIssue.NoBattlePending;
            if (result.BattleNumber != pendingBattle) return BattleResultIssue.WrongBattle;
            foreach (int id in result.Fates.Keys)
                if (!units.ContainsKey(id)) return BattleResultIssue.UnknownUnit;
            return BattleResultIssue.None;
        }

        /// <summary>
        /// 전투 결과를 반영한다. 반영할 수 없으면 아무것도 바꾸지 않고 이유를 돌려준다. 같은 결과는 한 번만 반영된다.
        /// 죽은 잡졸은 런에서 사라지고, 죽은 영웅은 영구 사망 목록으로 옮겨 부대장에서 빠진다. 도망친 유닛은 남는다.
        /// </summary>
        public BattleResultIssue Apply(BattleResult result)
        {
            BattleResultIssue issue = CheckResult(result);
            if (issue != BattleResultIssue.None) return issue;

            foreach (KeyValuePair<int, UnitFate> pair in result.Fates)
            {
                if (pair.Value != UnitFate.Dead) continue;
                RunUnit unit = units[pair.Key];
                units.Remove(pair.Key);
                if (unit is RunSoldier soldier)
                {
                    SquadOf(soldier)?.Remove(soldier);
                    soldiers.Remove(soldier);
                }
                else if (unit is RunHero hero)
                {
                    foreach (Squad<RunUnit> squad in Army.Squads)
                        if (squad.Leader == hero) squad.Leader = null;
                    heroes.Remove(hero);
                    fallenHeroes.Add(hero);
                }
            }
            if (result.CastleAfter != null) Castle = result.CastleAfter.Clone();

            LastBattleResult = result;
            BattlesFought = pendingBattle;
            pendingBattle = 0;
            Changed?.Invoke();
            return BattleResultIssue.None;
        }

        bool Owns(Squad<RunUnit> squad)
        {
            foreach (Squad<RunUnit> own in Army.Squads)
                if (own == squad) return true;
            return false;
        }
    }
}
