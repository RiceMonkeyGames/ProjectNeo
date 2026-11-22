using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Morale;
using ProjectNeo.Squads;

namespace ProjectNeo.Skills
{
    /// <summary>게임 씬의 분대(<see cref="SquadCommandCenter"/>)와 사기(<see cref="GameSquadMorale"/>)를 스킬에 연결한다(COMBAT-06).</summary>
    public sealed class GameSkillContext : ISkillContext
    {
        readonly SquadCommandCenter squads;
        readonly GameSquadMorale morale;

        public GameSkillContext(SquadCommandCenter squads, GameSquadMorale morale)
        {
            this.squads = squads;
            this.morale = morale;
        }

        public int SquadOf(CharacterActor actor)
        {
            if (squads == null || actor == null) return -1;
            UnitCombatAI unit = actor.GetComponent<UnitCombatAI>();
            if (unit == null) return -1;
            IReadOnlyList<Squad<UnitCombatAI>> list = squads.Squads;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Leader == unit || Contains(list[i].Members, unit)) return i;
            return -1;
        }

        public void CollectSquad(int squad, List<CharacterActor> into)
        {
            if (squads == null || into == null || squad < 0 || squad >= squads.Squads.Count) return;
            Squad<UnitCombatAI> target = squads.Squads[squad];
            AddAlive(target.Leader, into);
            foreach (UnitCombatAI member in target.Members) AddAlive(member, into);
        }

        public float AddMorale(int squad, float amount) => morale != null ? morale.AddMorale(squad, amount) : 0f;

        public bool TryGetMoraleRatio(int squad, out float ratio)
        {
            ratio = 1f;
            BattleSquadMorale battle = morale != null ? morale.BattleOf(squad) : null;
            if (battle == null || battle.IsRouted) return false;
            ratio = SkillAutoRules.MoraleRatio(battle.Morale.Value, battle.Morale.Max);
            return true;
        }

        static void AddAlive(UnitCombatAI unit, List<CharacterActor> into)
        {
            if (unit == null) return;
            CharacterActor actor = unit.GetComponent<CharacterActor>();
            if (actor == null || actor.Health == null || actor.Health.IsDead || !actor.isActiveAndEnabled) return;
            if (!into.Contains(actor)) into.Add(actor);
        }

        static bool Contains(IReadOnlyList<UnitCombatAI> members, UnitCombatAI unit)
        {
            for (int i = 0; i < members.Count; i++)
                if (members[i] == unit) return true;
            return false;
        }
    }
}
