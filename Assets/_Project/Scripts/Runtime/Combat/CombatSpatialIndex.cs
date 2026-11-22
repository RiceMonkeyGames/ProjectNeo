using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 활성 <see cref="CharacterActor.ActiveActors"/>를 프레임당 한 번(또는 유닛 추가·사망 시)
    /// <see cref="CombatSpatialGrid{T}"/>에 등록해 근접·중거리 적 탐색을 가속하는 런타임 인덱스(PERF-ai-spatial-grid).
    /// </summary>
    public static class CombatSpatialIndex
    {
        public const float DefaultCellSize = 8f;
        // 오프셋 콜라이더나 큰 몸통 반경을 놓치지 않도록 수평 격자 조회 반경에 더하는 여유 폭(m).
        public const float DefaultBodyPadding = 2f;

        static readonly CombatSpatialGrid<CharacterActor> grid = new CombatSpatialGrid<CharacterActor>(DefaultCellSize, 512);
        static int builtFrame = -1;
        static int builtRevision = -1;

        public static CombatSpatialGrid<CharacterActor> Grid
        {
            get
            {
                EnsureCurrent();
                return grid;
            }
        }

        public static void SetCellSize(float cellSize)
        {
            if (Mathf.Abs(grid.CellSize - cellSize) <= 1e-4f) return;
            grid.SetCellSize(cellSize);
            Invalidate();
        }

        public static void Invalidate()
        {
            builtFrame = -1;
            builtRevision = -1;
        }

        /// <summary>이번 프레임과 현재 활성 유닛 변경 번호에 맞춰 그리드를 최신 상태로 유지한다.</summary>
        public static void EnsureCurrent()
        {
            int frame = Time.frameCount;
            int revision = CharacterActor.RegistryRevision;
            if (builtFrame == frame && builtRevision == revision) return;
            Rebuild();
            builtFrame = frame;
            builtRevision = revision;
        }

        /// <summary>강제로 즉시 그리드를 다시 채운다.</summary>
        public static void Rebuild()
        {
            grid.Clear();
            IReadOnlyList<CharacterActor> all = CharacterActor.ActiveActors;
            for (int i = 0; i < all.Count; i++)
            {
                CharacterActor actor = all[i];
                if (actor == null || !actor.isActiveAndEnabled || actor.Health == null || actor.Health.IsDead) continue;
                Vector3 position = actor.Body != null ? actor.Body.bounds.center : actor.transform.position;
                grid.Insert(actor, actor.Team, position);
            }
        }

        public static CombatTeam EnemyTeamOf(CombatTeam team) =>
            team == CombatTeam.Allied ? CombatTeam.Hostile : CombatTeam.Allied;

        /// <summary>
        /// <paramref name="seekerTeam"/>의 적 진영 유닛 중 <paramref name="center"/>에서 반경 <paramref name="radius"/>
        /// (+ 여유 폭) 안의 격자 칸들에 있는 후보를 <paramref name="results"/>에 채운다.
        /// </summary>
        public static void QueryEnemies(CombatTeam seekerTeam, Vector3 center, float radius, List<CharacterActor> results,
            float padding = DefaultBodyPadding)
        {
            EnsureCurrent();
            grid.QueryCells(EnemyTeamOf(seekerTeam), center, radius + padding, results);
        }

        /// <summary>
        /// <paramref name="team"/> 진영 유닛 중 <paramref name="center"/>에서 반경 <paramref name="radius"/>
        /// (+ 여유 폭) 안의 격자 칸들에 있는 후보를 <paramref name="results"/>에 채운다.
        /// </summary>
        public static void QueryTeam(CombatTeam team, Vector3 center, float radius, List<CharacterActor> results,
            float padding = DefaultBodyPadding)
        {
            EnsureCurrent();
            grid.QueryCells(team, center, radius + padding, results);
        }

        /// <summary>적 진영 전체 목록(전장 전체 스캔이 필요할 때 같은 진영을 처음부터 건너뛴다).</summary>
        public static IReadOnlyList<CharacterActor> AllEnemiesOf(CombatTeam seekerTeam)
        {
            EnsureCurrent();
            return EnemyTeamOf(seekerTeam) == CombatTeam.Allied ? grid.AlliedItems : grid.HostileItems;
        }
    }
}
