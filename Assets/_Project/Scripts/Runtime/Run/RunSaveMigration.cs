using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Core;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 업데이트로 콘텐츠 ID가 바뀌거나 없어진 런 저장을 되살리기 전에 고친다(META-10, 사용자 결정 2026-11-21).
    /// 1) 이름 바꾸기 표로 옛 ID를 새 ID로 바꾼다. 2) 그래도 없는 콘텐츠는 런에서 빼고 저장해 둔 값을 전액 돌려준다
    /// (아이템: 구매가 + 강화 골드, 영웅: 고용비이고 장비는 보관함으로, 병사: 모집비, 성 조각·무기: 건설비).
    /// 상점·주점 매물은 다음 준비에 새로 채우고, 퍽은 빼면 포인트가 돌아오며, 장착 스킬은 이름만 바꾼다(모르는 스킬은 나중에 빠진다).
    /// 없어진 플레이어 영웅은 기본 플레이어 영웅으로 바꾸고 등급·장비를 그대로 둔다. 무엇을 했는지는 log에만 적는다.
    /// 씬을 모르는 순수 규칙이고, 기록을 그 자리에서 고친다.
    /// </summary>
    public static class RunSaveMigration
    {
        /// <summary>고친 곳의 수. 0이면 기록을 바꾸지 않았다.</summary>
        public static int Migrate(RunSnapshot snapshot, IRunContentLookup content, ContentRenameTable renames,
            HeroDefinition defaultPlayer, List<string> log = null)
        {
            if (snapshot == null || content == null) return 0;
            var pass = new Pass(content, renames, log);
            pass.Run(snapshot, defaultPlayer);
            return pass.Changes;
        }

        sealed class Pass
        {
            readonly IRunContentLookup content;
            readonly ContentRenameTable renames;
            readonly List<string> log;
            long refundGold;
            readonly List<MaterialRecord> refundMaterials = new List<MaterialRecord>();

            public int Changes { get; private set; }

            public Pass(IRunContentLookup content, ContentRenameTable renames, List<string> log)
            {
                this.content = content;
                this.renames = renames;
                this.log = log;
            }

            void Note(string line)
            {
                Changes++;
                log?.Add(line);
            }

            string Rename(ContentKind kind, string id)
            {
                if (renames == null || string.IsNullOrEmpty(id)) return id;
                string current = renames.Resolve(kind, id);
                if (current != id) Note($"renamed {kind} '{id}' -> '{current}'");
                return current;
            }

            void Refund(ValueRecord value, long extraGold = 0)
            {
                refundGold += Math.Max(0L, extraGold);
                if (value == null) return;
                refundGold += Math.Max(0, value.gold);
                if (value.materials != null) refundMaterials.AddRange(value.materials);
            }

            static string Describe(ValueRecord value, long extraGold = 0)
            {
                long gold = Math.Max(0L, extraGold) + (value != null ? Math.Max(0, value.gold) : 0);
                int materials = 0;
                if (value?.materials != null) foreach (MaterialRecord material in value.materials) materials += Math.Max(0, material.amount);
                return materials > 0 ? $"{gold} gold and {materials} materials" : $"{gold} gold";
            }

            public void Run(RunSnapshot s, HeroDefinition defaultPlayer)
            {
                Materials(s.materials);
                Items(s.stash, "stash");

                var stash = s.stash ?? (s.stash = new List<ItemRecord>());
                if (s.heroes != null)
                    for (int i = s.heroes.Count - 1; i >= 0; i--)
                    {
                        HeroRecord hero = s.heroes[i];
                        if (hero == null) continue;
                        hero.id = Rename(ContentKind.Hero, hero.id);
                        Items(hero.equipment, "hero " + hero.id);
                        if (content.Hero(hero.id) != null) continue;
                        if (hero.isPlayer)
                        {
                            if (defaultPlayer == null || string.IsNullOrEmpty(defaultPlayer.Id)) continue;
                            Note($"player hero '{hero.id}' no longer exists; replaced by '{defaultPlayer.Id}' with its grade and gear");
                            hero.id = defaultPlayer.Id;
                            continue;
                        }
                        Refund(hero.value);
                        Note($"removed hero '{hero.id}' (unit {hero.unitId}); refunded {Describe(hero.value)}, gear moved to the stash");
                        if (hero.equipment != null) stash.AddRange(hero.equipment);
                        if (s.squads != null)
                            foreach (SquadRecord squad in s.squads)
                                if (squad != null && squad.leaderUnitId == hero.unitId) squad.leaderUnitId = 0;
                        s.heroes.RemoveAt(i);
                    }
                if (s.fallenHeroes != null)
                    for (int i = s.fallenHeroes.Count - 1; i >= 0; i--)
                    {
                        HeroRecord hero = s.fallenHeroes[i];
                        if (hero == null) continue;
                        hero.id = Rename(ContentKind.Hero, hero.id);
                        Items(hero.equipment, "fallen hero " + hero.id);
                        if (content.Hero(hero.id) != null) continue;
                        // 죽은 영웅은 돌려줄 것이 없다. 장비만 보관함으로 옮긴다.
                        Note($"removed fallen hero '{hero.id}'; gear moved to the stash");
                        if (hero.equipment != null) stash.AddRange(hero.equipment);
                        s.fallenHeroes.RemoveAt(i);
                    }

                if (s.squads != null)
                    foreach (SquadRecord squad in s.squads)
                        if (squad != null) Soldiers(squad.soldiers);
                Soldiers(s.looseSoldiers);

                if (s.facilities != null)
                    for (int i = s.facilities.Count - 1; i >= 0; i--)
                    {
                        if (content.Facility((ProjectNeo.Facilities.FacilityType)s.facilities[i].type) != null) continue;
                        Note($"removed facility type {s.facilities[i].type}");
                        s.facilities.RemoveAt(i);
                    }

                Shop(s.shop);
                Tavern(s.tavern);
                Castle(s.castle);

                if (s.perks != null)
                    for (int i = s.perks.Count - 1; i >= 0; i--)
                    {
                        s.perks[i] = Rename(ContentKind.Perk, s.perks[i]);
                        if (content.Perk(s.perks[i]) != null) continue;
                        Note($"removed perk '{s.perks[i]}'; its point is free again");
                        s.perks.RemoveAt(i);
                    }

                PayRefund(s);
            }

            void Materials(List<MaterialRecord> materials)
            {
                if (materials == null) return;
                for (int i = materials.Count - 1; i >= 0; i--)
                {
                    MaterialRecord material = materials[i];
                    material.id = Rename(ContentKind.Material, material.id);
                    materials[i] = material;
                    if (content.Material(material.id) != null) continue;
                    // 재료는 그 자체가 재화라 돌려줄 값이 없다.
                    Note($"removed material '{material.id}' x{material.amount}");
                    materials.RemoveAt(i);
                }
            }

            void Items(List<ItemRecord> items, string where)
            {
                if (items == null) return;
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    ItemRecord item = items[i];
                    item.id = Rename(ContentKind.Item, item.id);
                    items[i] = item;
                    if (content.Item(item.id) != null) continue;
                    Refund(item.value, item.upgradeGoldSpent);
                    Note($"removed item '{item.id}' from {where}; refunded {Describe(item.value, item.upgradeGoldSpent)}");
                    items.RemoveAt(i);
                }
            }

            void Soldiers(List<SoldierRecord> soldiers)
            {
                if (soldiers == null) return;
                for (int i = soldiers.Count - 1; i >= 0; i--)
                {
                    SoldierRecord soldier = soldiers[i];
                    soldier.id = Rename(ContentKind.Soldier, soldier.id);
                    soldiers[i] = soldier;
                    if (content.Soldier(soldier.id) != null) continue;
                    Refund(soldier.value);
                    Note($"removed soldier '{soldier.id}' (unit {soldier.unitId}); refunded {Describe(soldier.value)}");
                    soldiers.RemoveAt(i);
                }
            }

            void Shop(ShopRecord shop)
            {
                if (shop?.stock == null) return;
                bool missing = false;
                for (int i = 0; i < shop.stock.Count; i++)
                {
                    shop.stock[i] = Rename(ContentKind.Item, shop.stock[i]);
                    if (content.Item(shop.stock[i]) == null) missing = true;
                }
                if (!missing) return;
                // 빠진 매물이 있으면 통째로 비워 다음 준비에 새로 채운다(잠금도 풀린다).
                Note("shop offers contained removed items; the shop restocks");
                shop.stock.Clear();
                shop.locked?.Clear();
                shop.stockedFor = -1;
            }

            void Tavern(TavernRecord tavern)
            {
                if (tavern?.offers == null) return;
                bool missing = false;
                for (int i = 0; i < tavern.offers.Count; i++)
                {
                    tavern.offers[i] = Rename(ContentKind.Hero, tavern.offers[i]);
                    if (content.Hero(tavern.offers[i]) == null) missing = true;
                }
                if (!missing) return;
                Note("tavern offers contained removed heroes; the tavern restocks");
                tavern.offers.Clear();
                tavern.locked?.Clear();
                tavern.rerolls = 0;
                tavern.stockedFor = -1;
            }

            void Castle(CastleRecord castle)
            {
                if (castle == null) return;
                List<CastlePieceRecord> pieces = castle.pieces ?? new List<CastlePieceRecord>();
                // 남는 조각의 새 순번. 빠진 조각은 -1이다.
                var newIndex = new int[pieces.Count];
                var kept = new List<CastlePieceRecord>(pieces.Count);
                for (int i = 0; i < pieces.Count; i++)
                {
                    CastlePieceRecord piece = pieces[i];
                    piece.id = Rename(ContentKind.CastlePiece, piece.id);
                    if (content.Piece(piece.id) != null)
                    {
                        newIndex[i] = kept.Count;
                        kept.Add(piece);
                        continue;
                    }
                    newIndex[i] = -1;
                    Refund(piece.value);
                    Note($"removed castle piece '{piece.id}' at ({piece.cellX}, {piece.cellY}); refunded {Describe(piece.value)}");
                }
                if (castle.pieces != null) { castle.pieces.Clear(); castle.pieces.AddRange(kept); }

                if (castle.weapons == null) return;
                for (int i = castle.weapons.Count - 1; i >= 0; i--)
                {
                    CastleWeaponRecord weapon = castle.weapons[i];
                    weapon.id = Rename(ContentKind.SiegeWeapon, weapon.id);
                    bool supported = weapon.supportIndex >= 0 && weapon.supportIndex < newIndex.Length && newIndex[weapon.supportIndex] >= 0;
                    // 받치는 조각에서 그 설치 자리가 없어졌으면(SIEGE-11: 성벽은 더 이상 무기를 받지 않는다) 없어진 콘텐츠로 본다.
                    bool mountable = supported && HasMountPoint(content.Piece(kept[newIndex[weapon.supportIndex]].id), weapon.mountId);
                    bool known = content.Weapon(weapon.id) != null;
                    if (known && mountable)
                    {
                        weapon.supportIndex = newIndex[weapon.supportIndex];
                        castle.weapons[i] = weapon;
                        continue;
                    }
                    Refund(weapon.value);
                    Note(!supported
                        ? $"removed siege weapon '{weapon.id}' whose castle piece was removed; refunded {Describe(weapon.value)}"
                        : !known
                            ? $"removed siege weapon '{weapon.id}'; refunded {Describe(weapon.value)}"
                            : $"removed siege weapon '{weapon.id}' whose castle piece no longer has mount point '{weapon.mountId}'; refunded {Describe(weapon.value)}");
                    castle.weapons.RemoveAt(i);
                }
            }

            static bool HasMountPoint(ProjectNeo.Walls.BuildPieceDefinition piece, string mountId)
            {
                if (piece == null || string.IsNullOrEmpty(mountId)) return false;
                foreach (ProjectNeo.Walls.BuildMountPoint point in piece.MountPoints)
                    if (point.id == mountId) return true;
                return false;
            }

            void PayRefund(RunSnapshot s)
            {
                if (refundGold > 0) s.gold = (int)Math.Min(int.MaxValue, Math.Max(0, s.gold) + refundGold);
                if (refundMaterials.Count == 0) return;
                s.materials ??= new List<MaterialRecord>();
                foreach (MaterialRecord refund in refundMaterials)
                {
                    if (refund.amount <= 0) continue;
                    string id = renames != null ? renames.Resolve(ContentKind.Material, refund.id) : refund.id;
                    // 돌려줄 재료가 그 사이 없어졌으면 돌려주지 않는다.
                    if (content.Material(id) == null) { log?.Add($"refund material '{refund.id}' no longer exists; skipped"); continue; }
                    int index = s.materials.FindIndex(material => material.id == id);
                    if (index < 0) { s.materials.Add(new MaterialRecord { id = id, amount = refund.amount }); continue; }
                    MaterialRecord total = s.materials[index];
                    total.amount = (int)Math.Min(int.MaxValue, (long)total.amount + refund.amount);
                    s.materials[index] = total;
                }
            }
        }
    }
}
