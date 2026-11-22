using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 망루 지붕 효과(SIEGE-14, 사용자 결정 2026-11-23). 윗면 효과가 있는 조각(<see cref="BuildPieceDefinition.OccupantModifiers"/>)의
    /// 맨 위에 선 아군 원거리 공격자에게 그 조각의 스탯 수정값을 붙이고, 내려오거나 조각이 사라지면 뗀다.
    /// 수정값의 출처는 조각 정의라 같은 종류의 망루를 옮겨 다녀도 한 번만 붙는다.
    /// </summary>
    public sealed class TowerOccupantBuff : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField, Min(0.05f)] float interval = 0.25f;
        [Tooltip("발과 윗면 높이 차이 허용(m).")]
        [SerializeField, Min(0.05f)] float tolerance = 0.6f;

        readonly Dictionary<CharacterActor, BuildPieceDefinition> applied = new Dictionary<CharacterActor, BuildPieceDefinition>();
        readonly List<CharacterActor> stale = new List<CharacterActor>();
        float timer;

        public void Configure(ConstructionWorld owner) => world = owner;

        /// <summary>지금 효과를 받는 유닛과 그 조각 정의(시험·확인용).</summary>
        public IReadOnlyDictionary<CharacterActor, BuildPieceDefinition> Applied => applied;

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = interval;
            Refresh();
        }

        public void Refresh()
        {
            if (world == null || world.Layout == null) return;
            foreach (CharacterActor actor in CharacterActor.ActiveActors)
            {
                if (actor == null || actor.Stats == null) continue;
                BuildPieceDefinition piece = null;
                if (Eligible(actor) && TowerRules.StandingOn(world.Layout, world.LocalPoint(actor.transform.position), tolerance, out BuildPlacement top))
                    piece = top.Definition;
                applied.TryGetValue(actor, out BuildPieceDefinition current);
                if (current == piece) continue;
                if (current != null) actor.Stats.RemoveModifiersFrom(current);
                if (piece == null) { applied.Remove(actor); continue; }
                actor.Stats.AddModifiers(piece.OccupantModifiers, piece);
                applied[actor] = piece;
            }
            // 사라진 유닛은 목록에서만 뺀다(스탯도 함께 사라진다).
            stale.Clear();
            foreach (CharacterActor actor in applied.Keys) if (actor == null || !actor.isActiveAndEnabled) stale.Add(actor);
            foreach (CharacterActor actor in stale)
            {
                if (actor != null && actor.Stats != null) actor.Stats.RemoveModifiersFrom(applied[actor]);
                applied.Remove(actor);
            }
        }

        static bool Eligible(CharacterActor actor) => actor.Team == CombatTeam.Allied && actor.Profile != null
            && actor.Profile.AttackKind == AttackKind.Projectile && actor.Airborne == null;

        void OnDisable()
        {
            foreach (KeyValuePair<CharacterActor, BuildPieceDefinition> entry in applied)
                if (entry.Key != null && entry.Key.Stats != null) entry.Key.Stats.RemoveModifiersFrom(entry.Value);
            applied.Clear();
        }
    }
}
