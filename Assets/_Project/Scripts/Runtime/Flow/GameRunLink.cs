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
                binding = CreateBinding();
                if (binding == null) return;
                binding.State.SetCastle(CaptureCastle());
                flow.AttachRunState(binding.State);
                ApplyTraining();
                return;
            }
            foreach (CharacterActor actor in binding.ObjectsInRun())
                if (actor != null && actor.Health != null) actor.Health.RestoreFull();
            ApplyTraining();
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

        /// <summary>전투 단계에 들어올 때 부른다. 준비에서 바뀐 성을 기록하고 전투 입력을 만든다.</summary>
        public void OnBattleStart()
        {
            if (binding == null || flow.Run == null || flow.Run.State != binding.State) return;
            binding.State.SetCastle(CaptureCastle());
            input = binding.State.CreateBattleInput(flow.Run.StageNumber);
        }

        /// <summary>
        /// 전투 결과를 만들어 흐름에 넘긴다. 연결되지 않았거나 흐름이 결과를 받지 않으면 false이며, 그때 부르는 쪽이
        /// 기존 방식(<see cref="GameFlow.FinishBattle(BattleOutcome)"/>)으로 끝낸다.
        /// </summary>
        public bool TryFinish(BattleOutcome outcome, BattleEndReason reason, IReadOnlyDictionary<string, int> kills)
        {
            if (binding == null || input == null) return false;
            BattleResult result = binding.CreateResult(input.BattleNumber, outcome, reason,
                actor => actor == null || actor.Health == null || actor.Health.IsDead, CaptureCastle(), kills);
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
            return CastleState.Capture(world.Layout, world.Mounts,
                placement => health.TryGetValue(placement.Id, out float value) ? value : placement.Definition.MaxHealth);
        }
    }
}
