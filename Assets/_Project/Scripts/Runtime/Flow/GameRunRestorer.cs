using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 저장된 런 기록으로 Game 씬을 되살린다(FLOW-02). 씬은 항상 기본 편성·기본 성으로 열리므로, 성을 기록대로 다시 놓고
    /// 부대원을 기록에 맞게 남기거나 새로 만든 뒤 런 데이터를 만들어 씬 유닛과 짝짓는다.
    /// 규칙(무엇을 저장하고 어떻게 되살리는지)은 <see cref="RunState"/>에 있고, 여기서는 씬 오브젝트만 맞춘다.
    /// 씬을 건드리기 전에 기록을 먼저 검사하므로, 되살릴 수 없는 기록이면 씬은 그대로다.
    /// </summary>
    public sealed class GameRunRestorer
    {
        readonly SquadCommandCenter squads;
        readonly CharacterActor player;
        readonly ConstructionWorld world;
        readonly GameUnitSpawner spawner;
        readonly IRunContentLookup content;
        readonly RunConfig startConfig;

        public GameRunRestorer(SquadCommandCenter squads, CharacterActor player, ConstructionWorld world,
            GameUnitSpawner spawner, IRunContentLookup content, RunConfig startConfig)
        {
            this.squads = squads; this.player = player; this.world = world;
            this.spawner = spawner; this.content = content; this.startConfig = startConfig;
        }

        /// <summary>마지막으로 되살리지 못한 이유. 없으면 빈 문자열.</summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// 기록대로 씬과 런 데이터를 만든다. 되살릴 수 없으면 null이고 이유는 <see cref="LastError"/>다.
        /// </summary>
        public RunUnitBinding<CharacterActor> Restore(RunSnapshot snapshot)
        {
            LastError = Precheck(snapshot);
            if (LastError.Length > 0) return null;

            RestoreCastle(snapshot.castle);

            // 씬 유닛을 기록에 맞춘다. 런 번호 → 씬 유닛.
            var actors = new Dictionary<int, CharacterActor>();
            var heroUnits = new Dictionary<int, UnitCombatAI>();
            RestoreSoldiers(snapshot, actors);
            foreach (HeroRecord hero in snapshot.heroes)
            {
                if (hero.isPlayer) { actors[hero.unitId] = player; continue; }
                int captainOf = snapshot.squads.FindIndex(squad => squad.leaderUnitId == hero.unitId);
                Vector3? gather = captainOf >= 0 && squads.TryGetGatherPoint(captainOf, out Vector3 point) ? point : (Vector3?)null;
                UnitCombatAI unit = spawner.SpawnRestoredHero(content.Hero(hero.id), gather);
                CharacterActor actor = unit != null ? unit.GetComponent<CharacterActor>() : null;
                if (actor == null || actor.Character == null)
                {
                    LastError = "could not create the scene unit of hero: " + hero.id;
                    return null;
                }
                actors[hero.unitId] = actor;
                heroUnits[hero.unitId] = unit;
            }

            var options = new RunRestoreOptions
            {
                SquadConfig = squads.Config,
                Economy = startConfig != null ? startConfig.Economy : null,
                EquipmentUpgrades = startConfig != null ? startConfig.EquipmentUpgrades : null,
                PrepTime = startConfig != null ? startConfig.PrepTime : null,
                // 씬 유닛의 캐릭터를 런 영웅과 공유해야 런에서 바꾼 장비가 씬 스탯에 바로 반영된다(D-7).
                CharacterFor = (record, definition) =>
                    actors.TryGetValue(record.unitId, out CharacterActor actor) && actor != null ? actor.Character : null,
            };
            RunState state = RunState.Restore(snapshot, content, options, out string error);
            if (state == null)
            {
                LastError = error;
                return null;
            }

            RunUnitBinding<CharacterActor> binding = RunUnitBinding<CharacterActor>.ForState(state);
            foreach (KeyValuePair<int, CharacterActor> pair in actors)
            {
                RunUnit unit = state.Find(pair.Key);
                if (unit != null && pair.Value != null) binding.BindNew(pair.Value, unit);
            }
            for (int i = 0; i < snapshot.squads.Count; i++)
                squads.SetLeader(i, heroUnits.TryGetValue(snapshot.squads[i].leaderUnitId, out UnitCombatAI leader) ? leader : null);

            // 장비로 최대 체력이 달라졌을 수 있다. 저장은 체력을 담지 않으므로 모두 가득 채운다.
            foreach (CharacterActor actor in binding.ObjectsInRun())
                if (actor != null && actor.Health != null && !actor.Health.IsDead) actor.Health.RestoreFull();
            return binding;
        }

        // 씬을 바꾸기 전에 되살릴 수 있는지 모두 확인한다. 문제가 없으면 빈 문자열.
        string Precheck(RunSnapshot snapshot)
        {
            if (squads == null || squads.Config == null || player == null || world == null || spawner == null)
                return "the scene is missing squads, player, castle or unit spawner";
            if (content == null) return "no run content catalog is assigned";
            if (!RunState.TryValidate(snapshot, content, squads.Config, out string error)) return error;
            foreach (HeroRecord hero in snapshot.heroes)
            {
                HeroDefinition definition = content.Hero(hero.id);
                if (hero.isPlayer)
                {
                    if (player.Character == null || player.Character.Definition != definition)
                        return "the saved player hero is not the scene player: " + hero.id;
                }
                else if (definition.UnitPrefab == null) return "hero has no scene body prefab: " + hero.id;
            }
            foreach (SquadRecord squad in snapshot.squads)
                foreach (SoldierRecord soldier in squad.soldiers)
                    if (content.Soldier(soldier.id).UnitPrefab == null) return "soldier has no scene unit prefab: " + soldier.id;
            return string.Empty;
        }

        /// <summary>되살린 성에 걸려 있던 작업 주문(SIEGE-08). 저장한 순서대로다. 씬의 작업 목록이 가져간다.</summary>
        public List<RestoredWork> RestoredWork { get; } = new List<RestoredWork>();

        void RestoreCastle(CastleRecord castle)
        {
            world.ClearForRestore();
            RestoredWork.Clear();
            var work = new List<(int order, RestoredWork entry)>();
            var placed = new List<PlacedStructure>();
            foreach (CastlePieceRecord piece in castle.pieces)
            {
                BuildPieceDefinition definition = content.Piece(piece.id);
                if (!world.TryPlaceRestored(definition, new Vector2Int(piece.cellX, piece.cellY), piece.rotation,
                        piece.isFixed && definition.Kind == BuildPieceKind.Gate, out PlacedStructure body))
                {
                    Debug.LogWarning($"Saved castle piece could not be placed: {piece.id} at ({piece.cellX}, {piece.cellY}).");
                    placed.Add(null);
                    continue;
                }
                placed.Add(body);
                float lost = definition.MaxHealth - piece.health;
                if (lost > 0f) body.ReceiveDamage(lost);
                CastleWorkState pieceWork = RunState.WorkOf(piece.workKind, piece.workOrder, piece.workHours, piece.workDone,
                    piece.workGold, piece.workStartHealth);
                if (pieceWork.HasWork)
                {
                    if (pieceWork.Kind == WorkKind.Build) body.SetUnderConstruction(true);
                    work.Add((pieceWork.Order, new RestoredWork(pieceWork.ToOrder(WorkTarget.Piece, body.Placement.Id), body, null)));
                }
            }
            foreach (CastleWeaponRecord weapon in castle.weapons)
            {
                PlacedStructure support = placed[weapon.supportIndex];
                SiegeWeaponDefinition definition = content.Weapon(weapon.id);
                if (support == null || !world.TryMount(definition, support, weapon.mountId, out PlacedSiegeWeapon body, weapon.rotation))
                {
                    Debug.LogWarning("Saved siege weapon could not be mounted: " + weapon.id);
                    continue;
                }
                float lost = definition.MaxHealth - weapon.health;
                if (lost > 0f && weapon.health > 0f) body.ReceiveDamage(lost);
                CastleWorkState weaponWork = RunState.WorkOf(weapon.workKind, weapon.workOrder, weapon.workHours, weapon.workDone,
                    weapon.workGold, weapon.workStartHealth);
                body.SetUnfinished(weapon.unfinished || weaponWork.Kind == WorkKind.Build);
                if (weaponWork.HasWork)
                    work.Add((weaponWork.Order, new RestoredWork(weaponWork.ToOrder(WorkTarget.Weapon, body.Mount.Id), null, body)));
            }
            work.Sort((a, b) => a.order.CompareTo(b.order));
            foreach (var (_, entry) in work) RestoredWork.Add(entry);
        }

        // 부대마다: 같은 종류의 기존 씬 유닛은 그대로 쓰고, 남는 유닛은 없애고, 모자란 유닛은 새로 만든다.
        void RestoreSoldiers(RunSnapshot snapshot, Dictionary<int, CharacterActor> actors)
        {
            while (squads.Squads.Count < snapshot.squads.Count && squads.AddSquad() >= 0) { }

            for (int i = 0; i < squads.Squads.Count; i++)
            {
                var available = new List<UnitCombatAI>();
                foreach (UnitCombatAI member in squads.Squads[i].Members)
                    if (member != null) available.Add(member);
                var missing = new List<SoldierRecord>();
                if (i < snapshot.squads.Count)
                    foreach (SoldierRecord soldier in snapshot.squads[i].soldiers)
                    {
                        SoldierDefinition definition = content.Soldier(soldier.id);
                        UnitCombatAI reuse = available.Find(member => ActorOf(member).SoldierDefinition == definition);
                        if (reuse == null) { missing.Add(soldier); continue; }
                        available.Remove(reuse);
                        actors[soldier.unitId] = ActorOf(reuse);
                    }

                // 새 유닛이 설 자리는 남는 유닛을 없애기 전의 부대 위치로 정한다.
                Vector3? gather = squads.TryGetGatherPoint(i, out Vector3 point) ? point : (Vector3?)null;
                foreach (UnitCombatAI extra in available) Remove(extra);
                UnitCombatAI oldLeader = squads.Squads[i].Leader;
                if (oldLeader != null) Remove(oldLeader);

                foreach (SoldierRecord soldier in missing)
                {
                    UnitCombatAI unit = spawner.SpawnRestoredSoldier(content.Soldier(soldier.id), gather);
                    if (unit == null || !squads.AddMember(i, unit))
                    {
                        Debug.LogWarning("Saved soldier could not be created in the scene: " + soldier.id);
                        if (unit != null) Object.Destroy(unit.gameObject);
                        continue;
                    }
                    actors[soldier.unitId] = ActorOf(unit);
                    if (gather == null) gather = unit.transform.position;
                }
            }
            for (int i = squads.Squads.Count - 1; i >= snapshot.squads.Count; i--)
                if (!squads.RemoveSquad(i)) Debug.LogWarning("Extra scene squad could not be removed: " + i);
        }

        void Remove(UnitCombatAI unit)
        {
            squads.RemoveMember(unit);
            unit.gameObject.SetActive(false);
            Object.Destroy(unit.gameObject);
        }

        // 꺼진 유닛은 UnitCombatAI.Actor가 아직 없을 수 있어 컴포넌트를 직접 찾는다.
        static CharacterActor ActorOf(UnitCombatAI unit) => unit.Actor != null ? unit.Actor : unit.GetComponent<CharacterActor>();
    }
}
