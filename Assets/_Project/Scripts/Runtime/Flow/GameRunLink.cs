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
    /// Game 씬과 런 데이터(<see cref="RunState"/>)를 잇는다(C-1~2). 씬에 미리 놓인 플레이어·부대에 런 번호를 붙이고,
    /// 전투 시작 때 성을 기록해 전투 입력을 만들며, 전투가 끝나면 결과를 한 번 반영한다.
    /// 다음 준비 때 살아남은 유닛을 전부 회복시킨다. 죽은 유닛의 몸은 씬에 그대로 남긴다(2026-11-12 결정).
    /// 씬 유닛을 새로 만들거나 저장된 런을 씬에 되살리지는 않는다.
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
                return;
            }
            foreach (CharacterActor actor in binding.ObjectsInRun())
                if (actor != null && actor.Health != null) actor.Health.RestoreFull();
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
