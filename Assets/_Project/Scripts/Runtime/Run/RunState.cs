using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
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
    public sealed partial class RunState
    {
        readonly Dictionary<int, RunUnit> units = new Dictionary<int, RunUnit>();
        readonly List<RunHero> heroes = new List<RunHero>();
        readonly List<RunSoldier> soldiers = new List<RunSoldier>();
        readonly List<RunHero> fallenHeroes = new List<RunHero>();
        readonly List<FacilityState> facilities = new List<FacilityState>();
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
        /// <summary>이 런의 시설 상태. 새 런은 모두 시작 레벨(0)에서 시작한다(D-0).</summary>
        public IReadOnlyList<FacilityState> Facilities => facilities;
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
            state.AddFacilities(config.Facilities);
            state.SetupShop(config.Economy);
            state.SetupEquipmentUpgrades(config.EquipmentUpgrades);

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

        /// <summary>이 런의 상점 매물·거래 창구. <see cref="SetupShop"/> 전에는 null.</summary>
        public ItemShop Shop { get; private set; }
        /// <summary>상점 매물을 마지막으로 뽑은 준비 번호(그때까지 치른 전투 수). 아직 안 뽑았으면 -1.</summary>
        public int ShopStockedFor { get; private set; } = -1;

        /// <summary>거래 수치로 상점 창구를 만든다. 이미 있거나 설정이 없으면 아무것도 하지 않는다.</summary>
        public void SetupShop(EconomyConfig config)
        {
            if (Shop != null || config == null) return;
            Shop = new ItemShop(config);
            Shop.Changed += () => Changed?.Invoke();
        }

        /// <summary>준비 번호별 상점 시드. 보상 시드와 겹치지 않게 섞는다.</summary>
        public int ShopSeedFor(int preparation) => unchecked(SeedFor(preparation) * 92821 + 1442695);

        /// <summary>
        /// 이번 준비의 상점 매물을 아직 뽑지 않았으면 상점 시설의 현재 레벨 품목 풀에서 매물 수만큼 뽑는다(D-5).
        /// 준비마다 한 번 새로 뽑고(Q-34) 같은 시드면 같은 매물이다. isAvailable로 해금 안 된 아이템을 뺀다.
        /// 뽑았으면 true. 시설 업그레이드는 다음 준비의 매물부터 반영된다.
        /// </summary>
        public bool RestockShopIfNeeded(Func<ItemDefinition, bool> isAvailable = null)
        {
            FacilityState shop = Facility(FacilityType.Shop);
            if (Shop == null || shop == null || ShopStockedFor == BattlesFought) return false;
            ShopStockedFor = BattlesFought;
            ShopStockedLevel = shop.Level;
            ShopRerolls = 0;
            Shop.Restock(shop.Current.itemPool, shop.Current.offerCount,
                new Core.SeededRandom(ShopSeedFor(BattlesFought)), isAvailable);
            return true;
        }

        /// <summary>이번 준비의 매물을 뽑은 상점 레벨. 리롤도 이 레벨의 풀을 쓴다(업그레이드는 다음 준비부터).</summary>
        public int ShopStockedLevel { get; private set; }
        /// <summary>이번 준비 단계에서 리롤한 횟수. 준비마다 매물을 새로 뽑을 때 0으로 돌아간다.</summary>
        public int ShopRerolls { get; private set; }
        /// <summary>다음 리롤의 골드(Q-46). 리롤할 때마다 오르고 준비마다 처음 값으로 돌아간다. 상점이 없으면 0.</summary>
        public int ShopRerollCost => Shop != null ? Shop.GetRerollCost(ShopRerolls) : 0;

        /// <summary>리롤 시드. 같은 런·같은 준비·같은 리롤 순번이면 같다. 준비 매물 시드와 겹치지 않게 섞는다.</summary>
        public int ShopRerollSeedFor(int preparation, int reroll) =>
            unchecked(ShopSeedFor(preparation) * 1000003 + (reroll + 1) * 7919);

        /// <summary>
        /// 지금 리롤할 수 있는지(PREP-02). 전투 대기 → 이번 준비 매물을 뽑았는지 → 골드 순으로 처음 걸린 이유를 돌려준다.
        /// 런을 바꾸지 않는다.
        /// </summary>
        public ShopRerollCheck CheckShopReroll()
        {
            if (HasPendingBattle) return ShopRerollCheck.BattlePending;
            if (Shop == null || Facility(FacilityType.Shop) == null || ShopStockedFor != BattlesFought)
                return ShopRerollCheck.NotStocked;
            if (!Wallet.CanAfford(new Cost(ShopRerollCost))) return ShopRerollCheck.NotEnoughFunds;
            return ShopRerollCheck.Ok;
        }

        /// <summary>
        /// 골드를 내고 이번 준비의 매물을 새로 뽑는다(PREP-02, Q-46). 이미 산 칸까지 매물 수만큼 다시 채운다.
        /// 이번 준비 매물을 뽑은 레벨의 풀을 쓰고, 같은 시드·같은 리롤 순번이면 같은 매물이다.
        /// <see cref="CheckShopReroll"/>이 Ok가 아니면 false이고 런은 바뀌지 않는다.
        /// </summary>
        public bool TryRerollShop(Func<ItemDefinition, bool> isAvailable = null)
        {
            if (CheckShopReroll() != ShopRerollCheck.Ok) return false;
            FacilityState shop = Facility(FacilityType.Shop);
            IReadOnlyList<FacilityLevel> levels = shop.Definition.Levels;
            FacilityLevel level = levels[Math.Min(Math.Max(ShopStockedLevel, 0), levels.Count - 1)];
            if (!Wallet.TrySpend(new Cost(ShopRerollCost))) return false;
            int reroll = ShopRerolls++;
            Shop.Restock(level.itemPool, level.offerCount,
                new Core.SeededRandom(ShopRerollSeedFor(BattlesFought, reroll)), isAvailable);
            return true;
        }

        /// <summary>현재 상점 레벨에 따른 최대 잠금 가능 매물 수(PREP-03). 업그레이드 직후부터 바로 늘어난다.</summary>
        public int ShopMaxLocked
        {
            get
            {
                FacilityState shop = Facility(FacilityType.Shop);
                return shop != null ? shop.Level + 1 : 0;
            }
        }

        /// <summary>index번째 상점 매물의 잠금을 토글할 수 있는지 확인한다(PREP-03). 런을 바꾸지 않는다.</summary>
        public ShopLockCheck CheckShopLock(int index)
        {
            if (HasPendingBattle) return ShopLockCheck.BattlePending;
            if (Shop == null || Facility(FacilityType.Shop) == null || ShopStockedFor != BattlesFought)
                return ShopLockCheck.NotStocked;
            if (index < 0 || index >= Shop.Stock.Count) return ShopLockCheck.InvalidSlot;
            if (!Shop.IsLocked(index) && Shop.LockedCount >= ShopMaxLocked) return ShopLockCheck.LimitReached;
            return ShopLockCheck.Ok;
        }

        /// <summary>index번째 상점 매물의 잠금을 무료로 토글한다(PREP-03). <see cref="CheckShopLock"/>이 Ok가 아니면 false.</summary>
        public bool TryToggleShopLock(int index)
        {
            if (CheckShopLock(index) != ShopLockCheck.Ok) return false;
            return Shop.TryToggleLock(index, ShopMaxLocked);
        }

        readonly List<HeroDefinition> tavernOffers = new List<HeroDefinition>();

        /// <summary>이번 준비의 주점 영웅 매물(D-3). 고용하면 빠진다.</summary>
        public IReadOnlyList<HeroDefinition> TavernOffers => tavernOffers;
        /// <summary>주점 매물을 마지막으로 뽑은 준비 번호. 아직 안 뽑았으면 -1.</summary>
        public int TavernStockedFor { get; private set; } = -1;

        /// <summary>준비 번호별 주점 시드. 상점·보상 시드와 겹치지 않게 섞는다.</summary>
        public int TavernSeedFor(int preparation) => unchecked(SeedFor(preparation) * 7919 + 104729);

        /// <summary>
        /// 이번 준비의 주점 매물을 아직 뽑지 않았으면 주점 시설의 현재 레벨 영웅 풀에서 매물 수만큼 뽑는다(D-3, Q-34).
        /// 이 런에 이미 있거나 영구 사망한 영웅, isAvailable(해금)을 통과하지 못한 영웅은 빼고, 같은 시드면 같은 매물이다.
        /// 뽑았으면 true. 시설 업그레이드는 다음 준비의 매물부터 반영된다.
        /// </summary>
        public bool RestockTavernIfNeeded(Func<HeroDefinition, bool> isAvailable = null)
        {
            FacilityState tavern = Facility(FacilityType.Tavern);
            if (tavern == null || TavernStockedFor == BattlesFought) return false;
            tavernOffers.Clear();
            tavernOffers.AddRange(Core.WeightedPool.Roll(tavern.Current.heroPool, tavern.Current.offerCount,
                new Core.SeededRandom(TavernSeedFor(BattlesFought)),
                hero => !InRunOrFallen(hero) && (isAvailable == null || isAvailable(hero))));
            TavernStockedFor = BattlesFought;
            Changed?.Invoke();
            return true;
        }

        bool InRunOrFallen(HeroDefinition definition)
        {
            foreach (RunHero hero in heroes) if (hero.Definition == definition) return true;
            foreach (RunHero hero in fallenHeroes) if (hero.Definition == definition) return true;
            return false;
        }

        /// <summary>
        /// 주점에서 이 영웅을 고용할 수 있는지(D-3). <paramref name="captainOf"/>가 있으면 그 부대의 대장을 맡기고, null이면 대기 영웅이다.
        /// 전투 대기 → 매물 → 부대 → 비용 순으로 처음 걸린 이유를 돌려준다. 런을 바꾸지 않는다.
        /// </summary>
        public HireCheck CheckHire(HeroDefinition hero, Squad<RunUnit> captainOf)
        {
            if (HasPendingBattle) return HireCheck.BattlePending;
            if (hero == null || !tavernOffers.Contains(hero)) return HireCheck.NotOffered;
            if (captainOf != null && !Owns(captainOf)) return HireCheck.NoSquad;
            if (!Wallet.CanAfford(hero.HireCost)) return HireCheck.NotEnoughFunds;
            return HireCheck.Ok;
        }

        /// <summary>
        /// 영웅을 고용한다(D-3, Q-36·Q-48). 비용을 내고 매물에서 빼며 런에 더한다. <paramref name="captainOf"/>가 있으면 대장을 맡기고
        /// 원래 대장은 대기 영웅이 된다. <paramref name="character"/>는 씬 유닛과 같은 캐릭터(장비·스탯 공유)이며 null이면 새로 만든다.
        /// <see cref="CheckHire"/>가 Ok가 아니면 null이고 런은 바뀌지 않는다.
        /// </summary>
        public RunHero TryHire(HeroDefinition hero, Squad<RunUnit> captainOf, Character character = null)
        {
            if (CheckHire(hero, captainOf) != HireCheck.Ok) return null;
            if (character != null && character.Definition != hero)
                throw new ArgumentException("캐릭터가 고용할 영웅의 것이 아니다.", nameof(character));
            if (!Wallet.TrySpend(hero.HireCost)) return null;
            tavernOffers.Remove(hero);
            RunHero hired = AddHero(character ?? new Character(hero));
            if (captainOf != null) captainOf.Leader = hired;
            Changed?.Invoke();
            return hired;
        }

        /// <summary>매물을 런 지갑으로 사서 보관함에 넣는다.</summary>
        public bool TryBuy(ItemDefinition item) => Shop != null && Shop.TryBuy(item, Wallet, Inventory);

        /// <summary>index번째 매물을 런 지갑으로 사서 보관함에 넣는다(중복 품목 구분).</summary>
        public bool TryBuyAt(int index) => Shop != null && Shop.TryBuyAt(index, Wallet, Inventory);

        /// <summary>보관함의 아이템을 판다. 보상 등 다른 경로로 얻은 아이템도 팔 수 있다.</summary>
        public bool TrySell(ItemInstance item) => Shop != null && Shop.TrySell(item, Wallet, Inventory);

        /// <summary>
        /// 보관함의 장비를 런 영웅에게 끼운다(D-7). 이 런의 영웅·보관함 아이템이고 장비이며 빈 칸이 있어야 한다.
        /// 스탯 수정값은 영웅의 <see cref="Character"/>에 붙는다.
        /// </summary>
        public bool TryEquip(RunHero hero, ItemInstance item)
        {
            if (hero == null || Find(hero.Id) != hero || item == null || !Inventory.Contains(item)) return false;
            if (!hero.Character.TryEquip(item, Inventory)) return false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>런 영웅이 착용한 장비를 벗겨 보관함에 돌려놓는다(D-7).</summary>
        public bool TryUnequip(RunHero hero, ItemInstance item)
        {
            if (hero == null || Find(hero.Id) != hero) return false;
            if (!hero.Character.TryUnequip(item, Inventory)) return false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>시설을 런 지갑으로 한 레벨 올린다(D-1). 없는 시설·최대 레벨·비용 부족이면 false.</summary>
        public bool TryUpgradeFacility(FacilityType type)
        {
            FacilityState facility = Facility(type);
            return facility != null && facility.TryUpgrade(Wallet);
        }

        /// <summary>종류로 시설 상태를 찾는다. 없으면 null.</summary>
        public FacilityState Facility(FacilityType type)
        {
            foreach (FacilityState facility in facilities) if (facility.Definition.Type == type) return facility;
            return null;
        }

        /// <summary>시설을 시작 레벨로 더한다. 같은 종류가 이미 있으면 예외를 던진다.</summary>
        public FacilityState AddFacility(FacilityDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (Facility(definition.Type) != null)
                throw new ArgumentException("같은 종류의 시설이 이미 있다: " + definition.Type, nameof(definition));
            var facility = new FacilityState(definition);
            facility.Upgraded += () => Changed?.Invoke();
            facilities.Add(facility);
            Changed?.Invoke();
            return facility;
        }

        /// <summary>목록의 시설 중 아직 없는 종류만 더한다. 빈 칸은 건너뛴다.</summary>
        public void AddFacilities(IEnumerable<FacilityDefinition> definitions)
        {
            if (definitions == null) return;
            foreach (FacilityDefinition definition in definitions)
                if (definition != null && Facility(definition.Type) == null) AddFacility(definition);
        }

        public RunUnit Find(int id) => units.TryGetValue(id, out RunUnit unit) ? unit : null;

        /// <summary>영웅을 런에 더한다. 플레이어는 한 명뿐이다.</summary>
        public RunHero AddHero(HeroDefinition definition, bool isPlayer = false)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return AddHero(new Character(definition), isPlayer);
        }

        /// <summary>
        /// 이미 있는 캐릭터를 영웅으로 런에 더한다. 씬 캐릭터와 같은 <see cref="Character"/>를 쓰면 런에서 바꾼 장비·스탯이
        /// 씬에 바로 반영된다(D-7). 같은 캐릭터를 두 번 더하지 않는다.
        /// </summary>
        public RunHero AddHero(Character character, bool isPlayer = false)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (isPlayer && Player != null) throw new InvalidOperationException("플레이어 영웅은 이미 있다.");
            foreach (RunHero existing in heroes)
                if (existing.Character == character) throw new ArgumentException("이미 런에 있는 캐릭터다.", nameof(character));
            var hero = new RunHero(nextUnitId++, character, isPlayer);
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

        /// <summary>
        /// 새 잡졸이 들어갈 부대를 고른다(Q-36). 지정한 부대가 있으면 그 부대에 자리가 있어야 하고,
        /// 없으면 자리가 남은 첫 부대다. 모든 부대가 꽉 찼으면 부대 수 상한까지 새 부대가 들어갈 자리가 된다(이때 null이고
        /// <paramref name="needsNewSquad"/>가 true). 넣을 수 없으면 false. 런을 바꾸지 않는다.
        /// </summary>
        public bool TryFindRecruitSquad(Squad<RunUnit> preferred, out Squad<RunUnit> squad, out bool needsNewSquad)
        {
            squad = null; needsNewSquad = false;
            if (HasPendingBattle) return false;
            if (preferred != null)
            {
                if (!Owns(preferred) || preferred.IsFull) return false;
                squad = preferred;
                return true;
            }
            foreach (Squad<RunUnit> own in Army.Squads)
                if (!own.IsFull) { squad = own; return true; }
            needsNewSquad = Army.CanAddSquad;
            return needsNewSquad;
        }

        /// <summary>
        /// 잡졸을 런에 더하고 Q-36 규칙으로 부대에 넣는다. 전투 중이거나 넣을 부대가 없으면 null이고 런은 바뀌지 않는다.
        /// 비용은 받지 않는다(병영 D-4가 낸다). 들어간 부대는 <paramref name="squad"/>, 새로 만든 부대면 그 부대다.
        /// </summary>
        public RunSoldier TryRecruit(SoldierDefinition definition, Squad<RunUnit> preferred, out Squad<RunUnit> squad)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!TryFindRecruitSquad(preferred, out squad, out bool needsNewSquad)) return null;
            if (needsNewSquad) squad = Army.TryAddSquad();
            RunSoldier soldier = AddSoldier(definition);
            TryAssign(soldier, squad);
            return soldier;
        }

        /// <summary>병영(훈련장) 상태. 시설을 아직 붙이지 않았으면 null.</summary>
        public FacilityState TrainingGround => Facility(FacilityType.TrainingGround);

        /// <summary>지금 병영 레벨에서 모집할 수 있는 잡졸(D-4). 병영이 없으면 빈 목록.</summary>
        public IReadOnlyList<SoldierDefinition> RecruitableSoldiers
        {
            get
            {
                List<SoldierDefinition> soldiers = TrainingGround?.Current.recruitableSoldiers;
                return soldiers != null ? (IReadOnlyList<SoldierDefinition>)soldiers : Array.Empty<SoldierDefinition>();
            }
        }

        /// <summary>
        /// 병영에서 이 잡졸을 <paramref name="preferred"/> 부대(null이면 Q-36 자동 배치)로 모집할 수 있는지(D-4).
        /// 전투 대기 → 모집 가능 종류 → 넣을 부대 → 비용 순으로 처음 걸린 이유를 돌려준다. 런을 바꾸지 않는다.
        /// </summary>
        public RecruitCheck CheckBarracksRecruit(SoldierDefinition definition, Squad<RunUnit> preferred)
        {
            if (HasPendingBattle) return RecruitCheck.BattlePending;
            if (definition == null) return RecruitCheck.NotRecruitable;
            bool recruitable = false;
            foreach (SoldierDefinition soldier in RecruitableSoldiers)
                if (soldier == definition) { recruitable = true; break; }
            if (!recruitable) return RecruitCheck.NotRecruitable;
            if (!TryFindRecruitSquad(preferred, out _, out _)) return RecruitCheck.NoSquad;
            if (!Wallet.CanAfford(definition.RecruitCost)) return RecruitCheck.NotEnoughFunds;
            return RecruitCheck.Ok;
        }

        /// <summary>
        /// 병영에서 잡졸을 모집하고 모집 비용을 런 지갑에서 낸다(D-4). <see cref="CheckBarracksRecruit"/>가 Ok가 아니면 null이고
        /// 런은 바뀌지 않는다. 들어간 부대는 <paramref name="squad"/>다.
        /// </summary>
        public RunSoldier TryRecruitAtBarracks(SoldierDefinition definition, Squad<RunUnit> preferred, out Squad<RunUnit> squad)
        {
            squad = null;
            if (CheckBarracksRecruit(definition, preferred) != RecruitCheck.Ok) return null;
            if (!Wallet.TrySpend(definition.RecruitCost)) return null;
            return TryRecruit(definition, preferred, out squad);
        }

        /// <summary>대장간 상태. 시설을 아직 붙이지 않았으면 null.</summary>
        public FacilityState Smithy => Facility(FacilityType.Smithy);

        /// <summary>
        /// 지금 대장간 레벨에서 항상 만들 수 있는 레시피(D-6, Q-35). 장비가 비었거나 isAvailable(해금)을 통과하지 못한 칸은 뺀다.
        /// 대장간이 없으면 빈 목록.
        /// </summary>
        public IReadOnlyList<CraftingRecipe> CraftableRecipes(Func<ItemDefinition, bool> isAvailable = null)
        {
            var result = new List<CraftingRecipe>();
            List<CraftingRecipe> recipes = Smithy?.Current.recipes;
            if (recipes == null) return result;
            foreach (CraftingRecipe recipe in recipes)
                if (recipe.item != null && (isAvailable == null || isAvailable(recipe.item))) result.Add(recipe);
            return result;
        }

        /// <summary>
        /// 대장간에서 이 장비를 만들 수 있는지(D-6). 전투 대기 → 레시피·해금 → 비용 순으로 처음 걸린 이유를 돌려준다.
        /// 런을 바꾸지 않는다.
        /// </summary>
        public CraftCheck CheckCraft(ItemDefinition item, Func<ItemDefinition, bool> isAvailable = null)
        {
            if (HasPendingBattle) return CraftCheck.BattlePending;
            if (!TryFindRecipe(item, isAvailable, out CraftingRecipe recipe)) return CraftCheck.NotCraftable;
            if (!Wallet.CanAfford(recipe.cost)) return CraftCheck.NotEnoughFunds;
            return CraftCheck.Ok;
        }

        /// <summary>
        /// 대장간에서 장비를 만들어 보관함에 넣고 레시피 비용을 런 지갑에서 낸다(D-6). <see cref="CheckCraft"/>가 Ok가 아니면
        /// null이고 런은 바뀌지 않는다. 레시피는 고정이라 만든 뒤에도 목록에 남는다.
        /// </summary>
        public ItemInstance TryCraft(ItemDefinition item, Func<ItemDefinition, bool> isAvailable = null)
        {
            if (CheckCraft(item, isAvailable) != CraftCheck.Ok) return null;
            TryFindRecipe(item, isAvailable, out CraftingRecipe recipe);
            if (!Wallet.TrySpend(recipe.cost)) return null;
            ItemInstance crafted = Inventory.Add(item);
            Changed?.Invoke();
            return crafted;
        }

        public EquipmentUpgrade EquipmentUpgrades { get; private set; }
        public int MaxEquipmentUpgrade => Smithy != null && EquipmentUpgrades != null
            ? EquipmentUpgrades.GetMaxLevel(Smithy.Level) : 0;

        public void SetupEquipmentUpgrades(EquipmentUpgradeSettings settings)
        {
            if (EquipmentUpgrades == null && settings != null) EquipmentUpgrades = new EquipmentUpgrade(settings);
        }

        public EquipmentUpgradeCheck CheckUpgradeEquipment(ItemInstance item)
        {
            if (HasPendingBattle) return EquipmentUpgradeCheck.BattlePending;
            if (EquipmentUpgrades == null) return EquipmentUpgradeCheck.NotConfigured;
            if (Smithy == null) return EquipmentUpgradeCheck.NoSmithy;
            return EquipmentUpgrades.Check(item, Inventory, Wallet, Smithy.Level);
        }

        public bool TryUpgradeEquipment(ItemInstance item)
        {
            if (CheckUpgradeEquipment(item) != EquipmentUpgradeCheck.Allowed) return false;
            if (!EquipmentUpgrades.TryUpgrade(item, Inventory, Wallet, Smithy.Level)) return false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>대장간에서는 장비만 판다. 판매가는 상점과 같고 실제 강화 지출을 포함한다(Q-35).</summary>
        public bool TrySellAtSmithy(ItemInstance item)
        {
            if (item == null || item.Definition.Kind != ItemKind.Equipment || Smithy == null) return false;
            return TrySell(item);
        }

        bool TryFindRecipe(ItemDefinition item, Func<ItemDefinition, bool> isAvailable, out CraftingRecipe found)
        {
            found = default;
            if (item == null) return false;
            foreach (CraftingRecipe recipe in CraftableRecipes(isAvailable))
                if (recipe.item == item) { found = recipe; return true; }
            return false;
        }

        /// <summary>이 런 부대의 순번. 씬 부대와 같은 순서다. 없으면 -1.</summary>
        public int IndexOf(Squad<RunUnit> squad)
        {
            for (int i = 0; i < Army.Squads.Count; i++) if (Army.Squads[i] == squad) return i;
            return -1;
        }

        /// <summary>
        /// 플레이어가 부대 대장을 맡을 수 있는지(Q-32). 지금은 막아 두고(2026-11-13 임시 결정), 나중에 켤 수 있게 남겨 둔다.
        /// </summary>
        public bool AllowPlayerCaptain { get; set; }

        /// <summary>
        /// 대기 중인 영웅: 플레이어가 아니고 어느 부대의 대장도 아닌 살아 있는 영웅(D-8, Q-36). 지금은 전투에 나가지 않는다(Q-48).
        /// </summary>
        public IReadOnlyList<RunHero> ReserveHeroes
        {
            get
            {
                var reserve = new List<RunHero>();
                foreach (RunHero hero in heroes)
                    if (!hero.IsPlayer && LeadingSquadOf(hero) == null) reserve.Add(hero);
                return reserve;
            }
        }

        /// <summary>이 영웅이 대장인 부대. 없으면 null.</summary>
        public Squad<RunUnit> LeadingSquadOf(RunHero hero)
        {
            if (hero == null) return null;
            foreach (Squad<RunUnit> squad in Army.Squads)
                if (squad.Leader == hero) return squad;
            return null;
        }

        /// <summary>
        /// 잡졸 하나를 <paramref name="target"/> 부대로 옮길 수 있는지(D-8, Q-45·Q-47). target이 null이면 새 부대로 옮긴다.
        /// 전투 대기 → 런 소속 → 같은 부대 → 자리 순으로 처음 걸린 이유를 돌려준다. 런을 바꾸지 않는다.
        /// </summary>
        public OrganizeCheck CheckMoveSoldier(RunSoldier soldier, Squad<RunUnit> target)
        {
            if (HasPendingBattle) return OrganizeCheck.BattlePending;
            if (soldier == null || Find(soldier.Id) != soldier || (target != null && !Owns(target))) return OrganizeCheck.NotInRun;
            Squad<RunUnit> source = SquadOf(soldier);
            if (source == target || (target == null && source != null && source.Members.Count == 1)) return OrganizeCheck.SameSquad;
            if (target == null ? !Army.CanAddSquad : target.IsFull) return OrganizeCheck.SquadFull;
            return OrganizeCheck.Ok;
        }

        /// <summary>
        /// 잡졸 하나를 옮긴다(D-8). target이 null이면 맨 뒤에 새 부대를 만들어 넣는다. 받는 부대의 사기는 그대로다.
        /// 원래 부대가 비면 그 부대를 없애고(사기도 함께 없어진다) 대장은 대기 영웅이 된다(Q-45).
        /// <paramref name="targetIndex"/>는 옮긴 뒤 받은 부대 순번, <paramref name="removedIndex"/>는 없앤 부대의 옮기기 전 순번(없으면 -1)이다.
        /// <see cref="CheckMoveSoldier"/>가 Ok가 아니면 false이고 런은 바뀌지 않는다.
        /// </summary>
        public bool TryMoveSoldier(RunSoldier soldier, Squad<RunUnit> target, out int targetIndex, out int removedIndex)
        {
            targetIndex = -1; removedIndex = -1;
            if (CheckMoveSoldier(soldier, target) != OrganizeCheck.Ok) return false;
            Squad<RunUnit> source = SquadOf(soldier);
            if (target == null) target = Army.TryAddSquad();
            source?.Remove(soldier);
            target.TryAdd(soldier);
            if (source != null && source.Members.Count == 0)
            {
                removedIndex = IndexOf(source);
                source.Leader = null;
                Army.RemoveSquad(source);
            }
            targetIndex = IndexOf(target);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 부대 대장을 정할 수 있는지(D-8). hero가 null이면 대장을 비우는 것이다. 대기 중인 영웅만 대장이 되고, 플레이어는
        /// <see cref="AllowPlayerCaptain"/>이 켜졌을 때만 된다. 런을 바꾸지 않는다.
        /// </summary>
        public OrganizeCheck CheckSetCaptain(Squad<RunUnit> squad, RunHero hero)
        {
            if (HasPendingBattle) return OrganizeCheck.BattlePending;
            if (squad == null || !Owns(squad) || (hero != null && Find(hero.Id) != hero)) return OrganizeCheck.NotInRun;
            if (squad.Leader == hero) return OrganizeCheck.SameSquad;
            if (hero == null) return OrganizeCheck.Ok;
            if (hero.IsPlayer && !AllowPlayerCaptain) return OrganizeCheck.PlayerNotAllowed;
            if (LeadingSquadOf(hero) != null) return OrganizeCheck.NotReserve;
            return OrganizeCheck.Ok;
        }

        /// <summary>
        /// 부대 대장을 정한다(D-8). 원래 대장은 대기 영웅이 된다. hero가 null이면 대장을 비운다.
        /// <see cref="CheckSetCaptain"/>이 Ok가 아니면 false이고 런은 바뀌지 않는다.
        /// </summary>
        public bool TrySetCaptain(Squad<RunUnit> squad, RunHero hero)
        {
            if (CheckSetCaptain(squad, hero) != OrganizeCheck.Ok) return false;
            squad.Leader = hero;
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

        /// <summary>
        /// 전투 뒤 정리(C-3, Q-50): 잡졸도 대장도 없는 부대를 없앤다(사기도 함께 없어진다). 대장만 남은 부대는 사기와 함께 남는다.
        /// 없앤 부대의 원래 순번을 큰 것부터 돌려준다(씬 부대를 같은 순서로 지우면 번호가 맞는다). 전투 대기 중이면 아무것도 하지 않는다.
        /// </summary>
        public List<int> RemoveEmptySquads()
        {
            var removed = new List<int>();
            if (HasPendingBattle) return removed;
            for (int i = Army.Squads.Count - 1; i >= 0; i--)
            {
                Squad<RunUnit> squad = Army.Squads[i];
                if (squad.Members.Count > 0 || squad.Leader != null) continue;
                Army.RemoveSquad(squad);
                removed.Add(i);
            }
            if (removed.Count > 0) Changed?.Invoke();
            return removed;
        }

        bool Owns(Squad<RunUnit> squad)
        {
            foreach (Squad<RunUnit> own in Army.Squads)
                if (own == squad) return true;
            return false;
        }
    }
}
