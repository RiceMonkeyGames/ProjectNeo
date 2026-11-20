using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// Game 씬과 런 데이터(<see cref="RunState"/>)를 잇는다(C-1~2). 씬에 미리 놓인 플레이어·부대에 런 번호를 붙이고,
    /// 전투 시작 때 성을 기록해 전투 입력을 만들며, 전투가 끝나면 결과를 한 번 반영한다.
    /// 다음 준비 때 살아남은 유닛을 전부 회복시킨다. 죽은 유닛의 몸은 씬에 그대로 남긴다(2026-11-12 결정).
    /// 준비 중에 모집한 잡졸은 <see cref="TryRecruit"/>로 런과 씬 부대에 함께 넣는다(D-0b). 저장된 런을 씬에 되살리지는 않는다.
    /// </summary>
    public sealed class GameRunLink
    {
        readonly GameFlow flow;
        readonly SquadCommandCenter squads;
        readonly CharacterActor player;
        readonly ConstructionWorld world;
        RunUnitBinding<CharacterActor> binding;
        BattleInput input;

        public GameRunLink(GameFlow flow, SquadCommandCenter squads, CharacterActor player, ConstructionWorld world)
        {
            this.flow = flow; this.squads = squads; this.player = player; this.world = world;
        }

        public RunState State => binding?.State;
        public BattleInput CurrentInput => input;

        /// <summary>저장된 런을 씬에 되살리는 도구(FLOW-02). 없으면 이어 하기를 골라도 새 런으로 시작한다.</summary>
        public GameRunRestorer Restorer { get; set; }
        /// <summary>런 저장에 쓰는 ID 창구(FLOW-02). 없으면 <see cref="CaptureSnapshot"/>이 null이다.</summary>
        public IRunContentLookup Content { get; set; }
        /// <summary>성을 기록할 때 조각·무기에 걸린 작업 주문과 미완성 무기를 알려 준다(SIEGE-08). 없으면 주문 없이 기록한다.</summary>
        public System.Func<BuildPlacement, CastleWorkState> PieceWork { get; set; }
        public System.Func<MountedWeapon, CastleWorkState> WeaponWork { get; set; }
        public System.Func<MountedWeapon, bool> WeaponUnfinished { get; set; }
        /// <summary>이 씬의 런이 저장 기록에서 되살린 것인지.</summary>
        public bool WasRestored { get; private set; }

        RunUnitBinding<CharacterActor> RestoreFrom(RunSnapshot saved)
        {
            RunUnitBinding<CharacterActor> restored = Restorer != null ? Restorer.Restore(saved) : null;
            if (restored != null) return restored;
            Debug.LogWarning("저장된 런을 되살리지 못해 새 런으로 시작한다: "
                + (Restorer != null ? Restorer.LastError : "되살리는 도구가 없다"));
            // 되살릴 수 없는 저장은 지우지 않고 따로 치워, 타이틀에서 다시 이어 하기를 누르지 않게 한다.
            GameSession.DiscardRunSave();
            if (flow.Run != null && flow.Run.StageNumber != 1) flow.RestartRunAtFirstStage();
            return null;
        }

        /// <summary>
        /// 지금 런을 저장 기록으로 옮긴다(FLOW-02). 전투 대기 중이 아니면 씬의 성을 먼저 기록한다.
        /// <paramref name="stageOffset"/>은 보상 뒤처럼 다음 스테이지 준비로 이어지게 저장할 때 1이다.
        /// 런이 연결되지 않았거나 ID 창구가 없으면 null.
        /// </summary>
        public RunSnapshot CaptureSnapshot(int stageOffset = 0)
        {
            if (binding == null || Content == null || flow.Run == null || flow.Run.State != binding.State) return null;
            if (!binding.State.HasPendingBattle) binding.State.SetCastle(CaptureCastle());
            return binding.State.Capture(flow.Run.StageNumber + stageOffset, Content);
        }
        public int IdOf(CharacterActor actor) => binding?.IdOf(actor) ?? 0;

        /// <summary>준비 단계에 들어올 때 부른다. 첫 준비면 런 데이터를 만들어 붙이고, 이후에는 생존 유닛을 회복한다.</summary>
        public void OnPreparation()
        {
            input = null;
            if (flow.Run == null) return;
            if (binding == null)
            {
                if (flow.Run.State != null)
                {
                    Debug.LogWarning("이 런은 이미 다른 씬에서 만든 런 데이터를 가지고 있어 Game 씬과 잇지 않는다.");
                    return;
                }
                // 타이틀에서 이어 하기를 골랐으면 저장 기록으로 씬과 런을 되살린다(FLOW-02). 못 하면 새 런으로 시작한다.
                RunSnapshot saved = GameSession.TakePendingRun();
                if (saved != null) binding = RestoreFrom(saved);
                WasRestored = binding != null;
                if (binding == null) binding = CreateBinding();
                if (binding == null) return;
                binding.State.SetCastle(CaptureCastle());
                flow.AttachRunState(binding.State);
                // 이번 런의 퍽(META-02). 되살린 런이면 저장된 퍽을 다시 찍는다. 찍을 때마다 능력치를 다시 붙인다.
                binding.State.SetupPerks(GameSession.Meta);
                binding.State.PerkAllocated += _ => ApplyPerks();
                ApplyTraining();
                ApplyPerks();
                return;
            }
            foreach (CharacterActor actor in binding.ObjectsInRun())
                if (actor != null && actor.Health != null) actor.Health.RestoreFull();
            CleanUpSquadsAfterBattle();
            SetReserveHeroesActive(true);
            ApplyTraining();
            ApplyPerks();
        }

        readonly HashSet<ProjectNeo.Meta.PerkDefinition> appliedPerks = new HashSet<ProjectNeo.Meta.PerkDefinition>();

        /// <summary>
        /// 찍은 퍽의 능력치 효과를 런의 모든 씬 유닛에 붙인다(META-02): 플레이어는 Player, 다른 영웅은 Heroes, 잡졸은 Soldiers 대상.
        /// 붙였던 퍽 효과를 먼저 떼고 다시 붙이므로 여러 번 불러도 겹치지 않는다. 체력이 가득 찼던 유닛은 늘어난 최대 체력까지 채운다.
        /// 준비 시작, 퍽을 찍었을 때, 모집·고용 뒤에 부른다.
        /// </summary>
        public void ApplyPerks()
        {
            if (binding == null) return;
            ProjectNeo.Meta.PerkLoadout perks = binding.State.Perks;
            foreach (CharacterActor actor in binding.ObjectsInRun())
            {
                if (actor == null || actor.Stats == null) continue;
                RunUnit unit = binding.State.Find(binding.IdOf(actor));
                if (unit == null) continue;
                bool wasFull = actor.Health != null && !actor.Health.IsDead && actor.Health.Current >= actor.Health.Maximum;
                foreach (ProjectNeo.Meta.PerkDefinition old in appliedPerks) actor.Stats.RemoveModifiersFrom(old);
                if (perks != null)
                {
                    ProjectNeo.Meta.PerkTarget target = unit is RunSoldier ? ProjectNeo.Meta.PerkTarget.Soldiers
                        : unit is RunHero hero && hero.IsPlayer ? ProjectNeo.Meta.PerkTarget.Player : ProjectNeo.Meta.PerkTarget.Heroes;
                    perks.ApplyAllStatEffects(actor.Stats, target);
                }
                if (wasFull) actor.Health.RestoreFull();
            }
            appliedPerks.Clear();
            if (perks != null) foreach (ProjectNeo.Meta.PerkDefinition perk in perks.Allocated) appliedPerks.Add(perk);
        }

        /// <summary>
        /// 전투 뒤 다음 준비 시작 때 부대를 정리한다(C-3, Q-50). 죽은 유닛은 씬 부대에서 빠지고(몸은 남는다), 잡졸도 대장도 없는 부대는
        /// 런과 씬에서 함께 없어진다. 런은 이미 전투 결과로 죽은 유닛을 뺐으므로 씬을 런에 맞춘다. 결과는 한 번만 반영되었으므로
        /// 여러 번 불러도 더 바뀌지 않는다.
        /// </summary>
        void CleanUpSquadsAfterBattle()
        {
            if (squads == null || binding == null) return;
            squads.RemoveFallen();
            if (squads.Squads.Count != binding.State.Army.Squads.Count)
            {
                Debug.LogWarning("씬 부대 수와 런 부대 수가 달라 빈 부대를 정리하지 않는다.");
                return;
            }
            foreach (int index in binding.State.RemoveEmptySquads())
                if (!squads.RemoveSquad(index)) Debug.LogError("빈 씬 부대를 없애지 못했다: " + index);
        }

        /// <summary>
        /// 런의 모든 씬 잡졸에게 지금 병영 레벨의 훈련 보너스를 붙인다(D-4, Q-37). 병영 업그레이드·모집 뒤와 준비 시작 때 부른다.
        /// 여러 번 불러도 겹치지 않는다. 체력이 가득 찼던 유닛은 늘어난 최대 체력까지 채운다. 영웅·플레이어는 건드리지 않는다.
        /// </summary>
        public void ApplyTraining()
        {
            if (binding == null) return;
            FacilityState trainingGround = binding.State.TrainingGround;
            foreach (CharacterActor actor in binding.ObjectsInRun())
            {
                if (actor == null || actor.Stats == null || !(binding.State.Find(binding.IdOf(actor)) is RunSoldier)) continue;
                bool wasFull = actor.Health != null && !actor.Health.IsDead && actor.Health.Current >= actor.Health.Maximum;
                SoldierTraining.Apply(actor.Stats, trainingGround);
                if (wasFull) actor.Health.RestoreFull();
            }
        }

        /// <summary>
        /// 준비 중에 잡졸 하나를 모집해 런과 씬 부대에 함께 넣는다(D-0b, Q-36). <paramref name="create"/>는 씬 유닛을
        /// 만들어 돌려준다(위치·활성화는 부르는 쪽). 준비 단계가 아니거나 넣을 부대가 없거나 씬 유닛을 만들지 못하면 null이고
        /// 런·씬 부대는 바뀌지 않는다. <paramref name="gatherPoint"/>는 넣기 전 부대가 모여 있던 곳이며, 빈 부대면 false다.
        /// <paramref name="payAtBarracks"/>면 병영 규칙(모집 가능 종류·비용, D-4)을 따르고 비용을 낸다. 아니면 비용을 받지 않는다.
        /// 훈련 보너스는 유닛이 켜진 뒤 <see cref="ApplyTraining"/>으로 붙인다.
        /// </summary>
        public UnitCombatAI TryRecruit(SoldierDefinition definition, Squad<RunUnit> preferred,
            System.Func<SoldierDefinition, UnitCombatAI> create, out int squadIndex, out Vector3 gatherPoint, out bool hasGatherPoint,
            bool payAtBarracks = false)
        {
            squadIndex = -1; gatherPoint = default; hasGatherPoint = false;
            if (definition == null || create == null || squads == null) return null;
            if (binding == null || flow.Run == null || flow.Run.State != binding.State || flow.Phase != GamePhase.Preparation)
                return null;
            RunState state = binding.State;
            if (squads.Squads.Count != state.Army.Squads.Count)
            {
                Debug.LogWarning("씬 부대 수와 런 부대 수가 달라 모집하지 않는다.");
                return null;
            }
            if (payAtBarracks && state.CheckBarracksRecruit(definition, preferred) != RecruitCheck.Ok) return null;
            if (!state.TryFindRecruitSquad(preferred, out _, out bool needsNewSquad)) return null;

            // 씬 유닛은 아직 꺼진 채 올 수 있어(Awake 전) UnitCombatAI.Actor 대신 컴포넌트를 직접 찾는다.
            UnitCombatAI unit = create(definition);
            if (unit == null) return null;
            var actor = unit.GetComponent<CharacterActor>();
            if (actor == null) { Object.Destroy(unit.gameObject); return null; }
            Squad<RunUnit> squad;
            RunSoldier soldier = payAtBarracks ? state.TryRecruitAtBarracks(definition, preferred, out squad)
                : state.TryRecruit(definition, preferred, out squad);
            if (soldier == null) { Object.Destroy(unit.gameObject); return null; }
            squadIndex = state.IndexOf(squad);
            if (needsNewSquad && squads.AddSquad() != squadIndex)
                Debug.LogError("새 씬 부대 번호가 런 부대 번호와 다르다: " + squadIndex);
            hasGatherPoint = squads.TryGetGatherPoint(squadIndex, out gatherPoint);
            if (!squads.AddMember(squadIndex, unit))
                Debug.LogError("모집한 유닛을 씬 부대에 넣지 못했다: " + squadIndex);
            binding.BindNew(actor, soldier);
            return unit;
        }

        /// <summary>
        /// 준비 중에 주점에서 영웅을 고용해 런과 씬에 함께 넣는다(D-3, Q-36·Q-48). <paramref name="captainOf"/>가 0 이상이면 그 부대의
        /// 대장을 맡기고(원래 대장은 대기), -1이면 대기 영웅이다. <paramref name="create"/>는 이 영웅으로 설정해 제자리에 놓고 켠 씬 유닛을
        /// 만든다(켜져야 <see cref="CharacterActor.Character"/>가 생긴다). 런 영웅은 씬 유닛과 같은 캐릭터를 써서 장비가 씬 스탯에 바로 반영된다.
        /// 못 하면 이유를 돌려주고 런·씬은 바뀌지 않는다.
        /// </summary>
        public HireCheck TryHire(HeroDefinition hero, int captainOf, System.Func<HeroDefinition, UnitCombatAI> create,
            out UnitCombatAI unit, out Vector3 gatherPoint, out bool hasGatherPoint)
        {
            unit = null; gatherPoint = default; hasGatherPoint = false;
            if (hero == null || create == null || !CanOrganize()) return HireCheck.NotOffered;
            RunState state = binding.State;
            if (captainOf < -1 || captainOf >= state.Army.Squads.Count) return HireCheck.NoSquad;
            Squad<RunUnit> squad = captainOf < 0 ? null : state.Army.Squads[captainOf];
            HireCheck check = state.CheckHire(hero, squad);
            if (check != HireCheck.Ok) return check;
            unit = create(hero);
            CharacterActor actor = unit != null ? unit.GetComponent<CharacterActor>() : null;
            if (actor == null || actor.Character == null || actor.Character.Definition != hero)
            {
                if (unit != null) Object.Destroy(unit.gameObject);
                unit = null;
                return HireCheck.NotOffered;
            }
            RunHero hired = state.TryHire(hero, squad, actor.Character);
            if (hired == null) { Object.Destroy(unit.gameObject); unit = null; return HireCheck.NotOffered; }
            binding.BindNew(actor, hired);
            if (captainOf >= 0)
            {
                hasGatherPoint = squads.TryGetGatherPoint(captainOf, out gatherPoint);
                squads.SetLeader(captainOf, unit);
            }
            return HireCheck.Ok;
        }

        /// <summary>
        /// 대기 영웅(대장이 아닌 고용 영웅)의 씬 유닛을 켜거나 끈다(Q-48). 전투 시작 때 끄고 준비로 돌아오면 켠다.
        /// 나중에 대장 없는 부대로 내보낼 때는 이 목록에서 빼면 된다.
        /// </summary>
        void SetReserveHeroesActive(bool active)
        {
            if (binding == null) return;
            foreach (RunHero hero in binding.State.ReserveHeroes)
            {
                CharacterActor actor = binding.ObjectOf(hero.Id);
                if (actor != null && actor.gameObject.activeSelf != active) actor.gameObject.SetActive(active);
            }
        }

        /// <summary>런과 씬 부대가 같은 순서로 맞물려 있고 준비 중이면 true(D-8 편성 조건).</summary>
        bool CanOrganize()
        {
            if (squads == null || binding == null || flow.Run == null || flow.Run.State != binding.State) return false;
            if (flow.Phase != GamePhase.Preparation) return false;
            if (squads.Squads.Count == binding.State.Army.Squads.Count) return true;
            Debug.LogWarning("씬 부대 수와 런 부대 수가 달라 편성을 바꾸지 않는다.");
            return false;
        }

        /// <summary>
        /// 준비 중에 잡졸 하나를 다른 부대로 옮긴다(D-8, Q-45·Q-47). <paramref name="targetSquad"/>가 -1이면 새 부대로 옮긴다.
        /// 런·씬 부대를 함께 바꾸고, 원래 부대가 비면 둘 다에서 없앤다(대장은 대기). 옮긴 유닛은 받은 부대가 모인 곳으로 걸어간다.
        /// 런이 받지 않으면 <see cref="OrganizeCheck"/> 이유를 돌려주고 아무것도 바뀌지 않는다.
        /// </summary>
        public OrganizeCheck TryMoveSoldier(RunSoldier soldier, int targetSquad, out int movedTo)
        {
            movedTo = -1;
            if (!CanOrganize()) return OrganizeCheck.NotInRun;
            RunState state = binding.State;
            if (targetSquad < -1 || targetSquad >= state.Army.Squads.Count) return OrganizeCheck.NotInRun;
            Squad<RunUnit> target = targetSquad < 0 ? null : state.Army.Squads[targetSquad];
            OrganizeCheck check = state.CheckMoveSoldier(soldier, target);
            if (check != OrganizeCheck.Ok) return check;
            CharacterActor actor = binding.ObjectOf(soldier.Id);
            UnitCombatAI unit = actor != null ? actor.GetComponent<UnitCombatAI>() : null;
            if (unit == null) { Debug.LogWarning("옮길 잡졸의 씬 유닛이 없다: " + soldier.Id); return OrganizeCheck.NotInRun; }
            if (targetSquad < 0 && squads.AddSquad() != state.Army.Squads.Count)
                Debug.LogError("새 씬 부대 번호가 런 부대 번호와 다르다.");
            int sceneTarget = targetSquad < 0 ? squads.Squads.Count - 1 : targetSquad;
            bool hasGather = squads.TryGetGatherPoint(sceneTarget, out Vector3 gather);
            if (!squads.MoveMember(unit, sceneTarget))
            {
                if (targetSquad < 0) squads.RemoveSquad(sceneTarget);
                Debug.LogWarning("씬 부대에 자리가 없어 옮기지 않는다: " + sceneTarget);
                return OrganizeCheck.SquadFull;
            }
            state.TryMoveSoldier(soldier, target, out movedTo, out int removed);
            if (removed >= 0 && !squads.RemoveSquad(removed)) Debug.LogError("빈 씬 부대를 없애지 못했다: " + removed);
            if (hasGather) unit.SetDirective(UnitDirective.Move, gather);
            return OrganizeCheck.Ok;
        }

        /// <summary>
        /// 준비 중에 부대 대장을 정한다(D-8). hero가 null이면 비운다. 영웅의 씬 유닛이 있으면 씬 부대장도 바꾸고,
        /// 새 대장은 그 부대가 모인 곳으로 걸어간다(BUG-hired-unit-not-joining-squad).
        /// </summary>
        public OrganizeCheck TrySetCaptain(int squad, RunHero hero)
        {
            if (!CanOrganize()) return OrganizeCheck.NotInRun;
            RunState state = binding.State;
            if (squad < 0 || squad >= state.Army.Squads.Count) return OrganizeCheck.NotInRun;
            OrganizeCheck check = state.CheckSetCaptain(state.Army.Squads[squad], hero);
            if (check != OrganizeCheck.Ok) return check;
            state.TrySetCaptain(state.Army.Squads[squad], hero);
            CharacterActor actor = hero != null ? binding.ObjectOf(hero.Id) : null;
            UnitCombatAI unit = actor != null ? actor.GetComponent<UnitCombatAI>() : null;
            squads.SetLeader(squad, unit);
            if (unit != null && squads.TryGetGatherPoint(squad, out Vector3 gather)) unit.SetDirective(UnitDirective.Move, gather);
            return OrganizeCheck.Ok;
        }

        /// <summary>전투 단계에 들어올 때 부른다. 준비에서 바뀐 성을 기록하고 전투 입력을 만든다.</summary>
        public void OnBattleStart()
        {
            if (binding == null || flow.Run == null || flow.Run.State != binding.State) return;
            binding.State.SetCastle(CaptureCastle());
            input = binding.State.CreateBattleInput(flow.Run.StageNumber);
            SetReserveHeroesActive(false);
        }

        /// <summary>
        /// 전투 결과를 만들어 흐름에 넘긴다. 연결되지 않았거나 흐름이 결과를 받지 않으면 false이며, 그때 부르는 쪽이
        /// 기존 방식(<see cref="GameFlow.FinishBattle(BattleOutcome)"/>)으로 끝낸다.
        /// <paramref name="isFled"/>는 사기로 도망친 아군을, <paramref name="retreats"/>는 퇴각한 적 종류별 수를 적는다(B-8.2).
        /// </summary>
        public bool TryFinish(BattleOutcome outcome, BattleEndReason reason, IReadOnlyDictionary<string, int> kills,
            System.Func<CharacterActor, bool> isFled = null, IReadOnlyDictionary<string, int> retreats = null, bool gateIntact = true,
            IReadOnlyList<ProjectNeo.Stages.BossDefinition> bossKills = null)
        {
            if (binding == null || input == null) return false;
            BattleResult result = binding.CreateResult(input.BattleNumber, outcome, reason,
                actor => actor == null || actor.Health == null || actor.Health.IsDead, CaptureCastle(), kills,
                isFled, retreats);
            // 처치한 보스(SIEGE-09): 보상·런 점수·해금 포인트가 읽는다.
            if (bossKills != null)
                foreach (ProjectNeo.Stages.BossDefinition boss in bossKills)
                    if (boss != null) result.AddBossKill(boss);
            // 런 점수의 성문 유지 점수(META-04)에 쓴다.
            result.GateIntact = gateIntact;
            input = null;
            if (flow.FinishBattle(result)) return true;
            Debug.LogWarning("런 데이터가 전투 결과를 받지 않았다: " + binding.State.CheckResult(result));
            return false;
        }

        RunUnitBinding<CharacterActor> CreateBinding()
        {
            HeroDefinition hero = player != null && player.Character != null ? player.Character.Definition : null;
            if (hero == null || squads == null || squads.Config == null)
            {
                Debug.LogWarning("플레이어 영웅이나 부대 설정이 없어 런 데이터를 만들지 않는다.");
                return null;
            }
            var lines = new List<IReadOnlyList<KeyValuePair<CharacterActor, SoldierDefinition>>>();
            foreach (Squad<UnitCombatAI> squad in squads.Squads)
            {
                var members = new List<KeyValuePair<CharacterActor, SoldierDefinition>>();
                foreach (UnitCombatAI member in squad.Members)
                    if (member != null && member.Actor != null)
                        members.Add(new KeyValuePair<CharacterActor, SoldierDefinition>(member.Actor, member.Actor.SoldierDefinition));
                lines.Add(members);
            }
            // 런마다 다른 시드. 같은 런 안의 추첨은 이 시드로 재현한다.
            // 씬 플레이어의 캐릭터를 런 영웅과 공유한다(D-7 장비 착용이 씬 스탯에 바로 반영).
            return RunUnitBinding<CharacterActor>.Create(squads.Config, hero, player, lines, System.Environment.TickCount,
                player.Character);
        }

        CastleState CaptureCastle()
        {
            if (world == null || world.Layout == null) return new CastleState();
            var health = new Dictionary<int, float>();
            foreach (PlacedStructure piece in world.Structures)
                if (piece != null && piece.Placement != null && piece.Health != null)
                    health[piece.Placement.Id] = piece.Health.Current;
            var weaponHealth = new Dictionary<int, float>();
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
                if (weapon != null && weapon.Mount != null && weapon.Health != null)
                    weaponHealth[weapon.Mount.Id] = weapon.Health.Current;
            return CastleState.Capture(world.Layout, world.Mounts,
                placement => health.TryGetValue(placement.Id, out float value) ? value : placement.Definition.MaxHealth,
                mount => weaponHealth.TryGetValue(mount.Id, out float value) ? value : mount.Definition.MaxHealth,
                PieceWork, WeaponWork, WeaponUnfinished);
        }
    }
}
