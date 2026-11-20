using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Run
{
    /// <summary>저장된 런을 되살릴 때 저장 파일에 없는 것(설정 에셋, 씬 캐릭터)을 넘긴다(FLOW-02).</summary>
    public sealed class RunRestoreOptions
    {
        public SquadConfig SquadConfig;
        /// <summary>상점 거래 수치. 없으면 상점 매물을 되살리지 않는다.</summary>
        public EconomyConfig Economy;
        public EquipmentUpgradeSettings EquipmentUpgrades;
        /// <summary>준비 시간 수치(PREP-06). 없으면 저장된 시계를 되살리지 않고 시간 제약도 쓰지 않는다.</summary>
        public PrepTimeSettings PrepTime;
        /// <summary>승천 단계 표(FLOW-03). 없으면 저장된 단계를 되살려도 규칙이 붙지 않는다(단계 0).</summary>
        public AscensionTable Ascension;
        /// <summary>
        /// 살아 있는 영웅이 쓸 캐릭터. 씬 유닛의 캐릭터를 넘기면 런에서 바꾼 장비가 씬 스탯에 바로 반영된다(D-7).
        /// 비우거나 null을 돌려주면 새 캐릭터를 만든다.
        /// </summary>
        public Func<HeroRecord, HeroDefinition, Character> CharacterFor;
    }

    /// <summary>런 데이터를 저장 기록으로 옮기고 되살린다(FLOW-02). 씬을 모르는 순수 규칙이다.</summary>
    public sealed partial class RunState
    {
        /// <summary>
        /// 지금 런을 저장 기록으로 옮긴다. 런을 바꾸지 않는다. <paramref name="stageNumber"/>는 이어 할 때 들어갈 스테이지다.
        /// 잡졸도 대장도 없는 부대는 다음 준비 때 어차피 없어지므로(Q-50) 담지 않는다.
        /// </summary>
        public RunSnapshot Capture(int stageNumber, IRunContentLookup content = null)
        {
            var snapshot = new RunSnapshot
            {
                stageNumber = Math.Max(1, stageNumber), seed = Seed, battlesFought = BattlesFought,
                lastRewardedBattle = LastRewardedBattle, nextUnitId = nextUnitId, gold = Wallet.Gold,
                ascensionLevel = AscensionLevel,
            };
            foreach (KeyValuePair<MaterialDefinition, int> material in Wallet.Materials)
                if (material.Key != null && material.Value > 0)
                    snapshot.materials.Add(new MaterialRecord { id = material.Key.Id, amount = material.Value });
            foreach (ItemInstance item in Inventory.Items) snapshot.stash.Add(Record(item));
            foreach (RunHero hero in heroes) snapshot.heroes.Add(Record(hero));
            foreach (RunHero hero in fallenHeroes) snapshot.fallenHeroes.Add(Record(hero));

            foreach (Squad<RunUnit> squad in Army.Squads)
            {
                if (squad.Members.Count == 0 && squad.Leader == null) continue;
                var record = new SquadRecord { leaderUnitId = squad.Leader != null ? squad.Leader.Id : 0 };
                foreach (RunUnit member in squad.Members)
                    if (member is RunSoldier soldier)
                        record.soldiers.Add(new SoldierRecord { unitId = soldier.Id, id = soldier.Definition.Id });
                if (squad.Morale != null)
                {
                    record.hasMorale = true; record.moraleMax = squad.Morale.Max; record.morale = squad.Morale.Value;
                }
                snapshot.squads.Add(record);
            }
            foreach (RunSoldier soldier in soldiers)
                if (SquadOf(soldier) == null)
                    snapshot.looseSoldiers.Add(new SoldierRecord { unitId = soldier.Id, id = soldier.Definition.Id });

            foreach (FacilityState facility in facilities)
                snapshot.facilities.Add(new FacilityRecord { type = (int)facility.Definition.Type, level = facility.Level });

            snapshot.prepTime.preparation = ClockPreparation;
            snapshot.prepTime.budget = Clock.Budget;
            snapshot.prepTime.used = Clock.Used;

            snapshot.shop.stockedFor = ShopStockedFor;
            snapshot.shop.stockedLevel = ShopStockedLevel;
            snapshot.shop.rerolls = ShopRerolls;
            if (Shop != null)
                for (int i = 0; i < Shop.Stock.Count; i++)
                {
                    if (Shop.Stock[i] == null) continue;
                    snapshot.shop.stock.Add(Shop.Stock[i].Id);
                    snapshot.shop.locked.Add(Shop.IsLocked(i));
                }
            snapshot.tavern.stockedFor = TavernStockedFor;
            foreach (HeroDefinition offer in tavernOffers)
                if (offer != null) snapshot.tavern.offers.Add(offer.Id);

            foreach (CastlePieceState piece in Castle.Pieces)
                snapshot.castle.pieces.Add(new CastlePieceRecord
                {
                    id = content != null ? content.PieceId(piece.Definition) : piece.Definition.name,
                    cellX = piece.Cell.x, cellY = piece.Cell.y, rotation = piece.Rotation, level = piece.Level,
                    isFixed = piece.IsFixed, health = piece.Health,
                    workKind = (int)piece.Work.Kind, workOrder = piece.Work.Order, workHours = piece.Work.Hours,
                    workDone = piece.Work.Done, workGold = piece.Work.GoldPaid, workStartHealth = piece.Work.StartHealth,
                });
            foreach (CastleWeaponState weapon in Castle.Weapons)
                snapshot.castle.weapons.Add(new CastleWeaponRecord
                {
                    id = weapon.Definition.Id, supportIndex = weapon.SupportIndex, mountId = weapon.MountId,
                    rotation = weapon.Rotation, health = weapon.Health, unfinished = weapon.Unfinished,
                    workKind = (int)weapon.Work.Kind, workOrder = weapon.Work.Order, workHours = weapon.Work.Hours,
                    workDone = weapon.Work.Done, workGold = weapon.Work.GoldPaid, workStartHealth = weapon.Work.StartHealth,
                });
            return snapshot;
        }

        static ItemRecord Record(ItemInstance item) => new ItemRecord
        {
            id = item.Definition.Id, upgradeLevel = item.UpgradeLevel, upgradeGoldSpent = item.UpgradeGoldSpent,
        };

        static HeroRecord Record(RunHero hero)
        {
            var record = new HeroRecord { unitId = hero.Id, id = hero.Definition.Id, isPlayer = hero.IsPlayer };
            foreach (ItemInstance item in hero.Character.Equipment) record.equipment.Add(Record(item));
            return record;
        }

        /// <summary>
        /// 저장 기록을 되살릴 수 있는지 아무것도 만들지 않고 확인한다. 모르는 ID, 겹치는 유닛 번호, 부대 상한 초과,
        /// 플레이어가 한 명이 아닌 경우 등을 찾아 처음 걸린 이유를 <paramref name="error"/>에 적는다.
        /// </summary>
        public static bool TryValidate(RunSnapshot snapshot, IRunContentLookup content, SquadConfig squadConfig, out string error)
        {
            error = Problem(snapshot, content, squadConfig);
            return error == null;
        }

        static string Problem(RunSnapshot s, IRunContentLookup content, SquadConfig squadConfig)
        {
            if (s == null) return "no snapshot";
            if (content == null) return "no content lookup";
            if (squadConfig == null) return "no squad config";
            if (s.stageNumber < 1 || s.battlesFought < 0 || s.gold < 0) return "invalid counters";
            if (s.heroes == null || s.squads == null || s.castle == null) return "missing sections";

            foreach (MaterialRecord material in s.materials ?? new List<MaterialRecord>())
                if (content.Material(material.id) == null) return "unknown material: " + material.id;
            foreach (ItemRecord item in s.stash ?? new List<ItemRecord>())
                if (content.Item(item.id) == null) return "unknown item: " + item.id;

            var ids = new HashSet<int>();
            var heroIds = new HashSet<int>();
            int players = 0;
            foreach (HeroRecord hero in s.heroes)
            {
                if (hero == null || content.Hero(hero.id) == null) return "unknown hero: " + hero?.id;
                if (hero.unitId <= 0 || !ids.Add(hero.unitId)) return "invalid hero unit id: " + hero.unitId;
                heroIds.Add(hero.unitId);
                if (hero.isPlayer) players++;
                foreach (ItemRecord item in hero.equipment ?? new List<ItemRecord>())
                    if (content.Item(item.id) == null) return "unknown item: " + item.id;
            }
            if (players != 1) return "the run needs exactly one player hero, found " + players;
            foreach (HeroRecord hero in s.fallenHeroes ?? new List<HeroRecord>())
            {
                if (hero == null || content.Hero(hero.id) == null) return "unknown fallen hero: " + hero?.id;
                foreach (ItemRecord item in hero.equipment ?? new List<ItemRecord>())
                    if (content.Item(item.id) == null) return "unknown item: " + item.id;
            }

            if (s.squads.Count > squadConfig.MaxSquads) return "too many squads: " + s.squads.Count;
            var leaders = new HashSet<int>();
            foreach (SquadRecord squad in s.squads)
            {
                if (squad == null || squad.soldiers == null) return "invalid squad";
                if (squad.soldiers.Count > squadConfig.MaxMembersPerSquad) return "too many squad members: " + squad.soldiers.Count;
                if (squad.leaderUnitId != 0 && (!heroIds.Contains(squad.leaderUnitId) || !leaders.Add(squad.leaderUnitId)))
                    return "invalid squad leader: " + squad.leaderUnitId;
                if (squad.hasMorale && squad.moraleMax <= 0f) return "invalid squad morale";
                foreach (SoldierRecord soldier in squad.soldiers)
                {
                    if (content.Soldier(soldier.id) == null) return "unknown soldier: " + soldier.id;
                    if (soldier.unitId <= 0 || !ids.Add(soldier.unitId)) return "invalid soldier unit id: " + soldier.unitId;
                }
            }
            foreach (SoldierRecord soldier in s.looseSoldiers ?? new List<SoldierRecord>())
            {
                if (content.Soldier(soldier.id) == null) return "unknown soldier: " + soldier.id;
                if (soldier.unitId <= 0 || !ids.Add(soldier.unitId)) return "invalid soldier unit id: " + soldier.unitId;
            }

            var facilityTypes = new HashSet<int>();
            foreach (FacilityRecord facility in s.facilities ?? new List<FacilityRecord>())
            {
                if (content.Facility((FacilityType)facility.type) == null) return "unknown facility: " + facility.type;
                if (!facilityTypes.Add(facility.type)) return "facility appears twice: " + facility.type;
            }
            if (s.shop != null)
                foreach (string item in s.shop.stock ?? new List<string>())
                    if (content.Item(item) == null) return "unknown shop item: " + item;
            if (s.tavern != null)
                foreach (string hero in s.tavern.offers ?? new List<string>())
                    if (content.Hero(hero) == null) return "unknown tavern hero: " + hero;

            List<CastlePieceRecord> pieces = s.castle.pieces ?? new List<CastlePieceRecord>();
            foreach (CastlePieceRecord piece in pieces)
                if (content.Piece(piece.id) == null) return "unknown castle piece: " + piece.id;
            foreach (CastleWeaponRecord weapon in s.castle.weapons ?? new List<CastleWeaponRecord>())
            {
                if (content.Weapon(weapon.id) == null) return "unknown siege weapon: " + weapon.id;
                if (weapon.supportIndex < 0 || weapon.supportIndex >= pieces.Count) return "invalid weapon support: " + weapon.supportIndex;
            }
            return null;
        }

        /// <summary>
        /// 저장 기록으로 런 데이터를 다시 만든다. 유닛 번호·시드·전투 수·상점 매물까지 저장한 때와 같다.
        /// 되살릴 수 없으면 null이고 이유는 <paramref name="error"/>다. 진행 중이던 전투는 저장하지 않으므로 준비 상태로 돌아온다.
        /// </summary>
        public static RunState Restore(RunSnapshot snapshot, IRunContentLookup content, RunRestoreOptions options, out string error)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (!TryValidate(snapshot, content, options.SquadConfig, out error)) return null;

            var state = new RunState(options.SquadConfig, snapshot.seed);
            state.SetupEquipmentUpgrades(options.EquipmentUpgrades);
            state.SetupPrepTime(options.PrepTime);
            state.RestorePrepClock(snapshot.prepTime);
            state.SetupAscension(options.Ascension, snapshot.ascensionLevel);
            if (snapshot.gold > 0) state.Wallet.AddGold(snapshot.gold);
            foreach (MaterialRecord material in snapshot.materials ?? new List<MaterialRecord>())
                if (material.amount > 0) state.Wallet.AddMaterial(content.Material(material.id), material.amount);
            foreach (ItemRecord item in snapshot.stash ?? new List<ItemRecord>())
                state.Inventory.Add(Item(item, content, options));

            int highestId = 0;
            foreach (HeroRecord record in snapshot.heroes)
            {
                HeroDefinition definition = content.Hero(record.id);
                Character character = options.CharacterFor?.Invoke(record, definition) ?? new Character(definition);
                if (character.Definition != definition)
                {
                    error = "character does not match hero: " + record.id;
                    return null;
                }
                state.Equip(character, record, content, options);
                var hero = new RunHero(record.unitId, character, record.isPlayer);
                state.units.Add(hero.Id, hero);
                state.heroes.Add(hero);
                if (hero.IsPlayer) state.Player = hero;
                highestId = Math.Max(highestId, hero.Id);
            }
            foreach (HeroRecord record in snapshot.fallenHeroes ?? new List<HeroRecord>())
            {
                var character = new Character(content.Hero(record.id));
                state.Equip(character, record, content, options);
                // PREP-08 이전 저장에는 죽은 영웅이 장비를 낀 채 남아 있다. Q-31에 따라 보관함으로 돌려놓는다.
                state.ReturnEquipment(character);
                // 영구 사망한 영웅은 런 유닛 목록에 없다. 번호는 0만 아니면 된다.
                state.fallenHeroes.Add(new RunHero(Math.Max(1, record.unitId), character, false));
                highestId = Math.Max(highestId, record.unitId);
            }

            foreach (SquadRecord record in snapshot.squads)
            {
                Squad<RunUnit> squad = state.Army.TryAddSquad();
                foreach (SoldierRecord soldier in record.soldiers)
                {
                    squad.TryAdd(state.RestoreSoldier(soldier, content));
                    highestId = Math.Max(highestId, soldier.unitId);
                }
                if (record.leaderUnitId != 0) squad.Leader = state.units[record.leaderUnitId];
                if (record.hasMorale)
                    squad.Morale = new Morale.SquadMorale(record.moraleMax, Mathf.Clamp(record.morale, 0f, record.moraleMax));
            }
            foreach (SoldierRecord soldier in snapshot.looseSoldiers ?? new List<SoldierRecord>())
            {
                state.RestoreSoldier(soldier, content);
                highestId = Math.Max(highestId, soldier.unitId);
            }

            foreach (FacilityRecord record in snapshot.facilities ?? new List<FacilityRecord>())
                state.AddFacility(content.Facility((FacilityType)record.type)).RestoreLevel(record.level);

            state.SetupShop(options.Economy);
            ShopRecord shop = snapshot.shop ?? new ShopRecord();
            if (state.Shop != null)
            {
                var stock = new List<ItemDefinition>();
                foreach (string id in shop.stock ?? new List<string>()) stock.Add(content.Item(id));
                state.Shop.RestoreStock(stock, shop.locked);
                state.ShopStockedFor = shop.stockedFor;
                state.ShopStockedLevel = shop.stockedLevel;
                state.ShopRerolls = Math.Max(0, shop.rerolls);
            }
            TavernRecord tavern = snapshot.tavern ?? new TavernRecord();
            foreach (string id in tavern.offers ?? new List<string>()) state.tavernOffers.Add(content.Hero(id));
            state.TavernStockedFor = tavern.stockedFor;

            var pieces = new List<CastlePieceState>();
            foreach (CastlePieceRecord piece in snapshot.castle.pieces ?? new List<CastlePieceRecord>())
                pieces.Add(new CastlePieceState(content.Piece(piece.id), new Vector2Int(piece.cellX, piece.cellY),
                    piece.rotation, piece.level, piece.isFixed, piece.health, WorkOf(piece.workKind, piece.workOrder,
                        piece.workHours, piece.workDone, piece.workGold, piece.workStartHealth)));
            var weapons = new List<CastleWeaponState>();
            foreach (CastleWeaponRecord weapon in snapshot.castle.weapons ?? new List<CastleWeaponRecord>())
                weapons.Add(new CastleWeaponState(content.Weapon(weapon.id), weapon.supportIndex, weapon.mountId,
                    weapon.rotation, weapon.health, weapon.unfinished, WorkOf(weapon.workKind, weapon.workOrder,
                        weapon.workHours, weapon.workDone, weapon.workGold, weapon.workStartHealth)));
            state.Castle = CastleState.FromRecords(pieces, weapons);

            state.nextUnitId = Math.Max(snapshot.nextUnitId, highestId + 1);
            state.BattlesFought = snapshot.battlesFought;
            state.LastRewardedBattle = Math.Max(0, snapshot.lastRewardedBattle);
            return state;
        }

        /// <summary>저장 기록의 작업 주문 칸을 읽는다(SIEGE-08). 모르는 종류는 주문 없음이다.</summary>
        public static CastleWorkState WorkOf(int kind, int order, float hours, float done, int gold, float startHealth) =>
            kind == (int)WorkKind.Build || kind == (int)WorkKind.Repair
                ? new CastleWorkState((WorkKind)kind, order, hours, done, gold, startHealth)
                : default;

        RunSoldier RestoreSoldier(SoldierRecord record, IRunContentLookup content)
        {
            var soldier = new RunSoldier(record.unitId, content.Soldier(record.id));
            units.Add(soldier.Id, soldier);
            soldiers.Add(soldier);
            return soldier;
        }

        // 저장된 장비를 보관함을 거쳐 끼운다. 칸이 모자라 못 끼운 장비는 보관함에 남아 잃지 않는다.
        void Equip(Character character, HeroRecord record, IRunContentLookup content, RunRestoreOptions options)
        {
            foreach (ItemRecord equipment in record.equipment ?? new List<ItemRecord>())
            {
                ItemInstance item = Item(equipment, content, options);
                Inventory.Add(item);
                character.TryEquip(item, Inventory);
            }
        }

        static ItemInstance Item(ItemRecord record, IRunContentLookup content, RunRestoreOptions options)
        {
            var item = new ItemInstance(content.Item(record.id));
            item.RestoreUpgrade(record.upgradeLevel, record.upgradeGoldSpent, options.EquipmentUpgrades);
            return item;
        }
    }
}
