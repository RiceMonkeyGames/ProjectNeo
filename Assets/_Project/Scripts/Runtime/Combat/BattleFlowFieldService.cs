using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 아군·적군 진영의 전열 목표들을 시드로 삼는 공유 <see cref="FlowFieldGrid"/>를 관리하고,
    /// 원거리(<see cref="AILodTier.Lod2"/>) 지상 유닛들에게 개별 <see cref="NavMeshAgent.CalculatePath"/> 없이
    /// 짧은 전방 웨이포인트와 진행 방향을 제공하는 런타임 서비스(PERF-ai-flow-field).
    /// </summary>
    public static class BattleFlowFieldService
    {
        public const float DefaultCellSize = 3f;
        public const float RebuildIntervalSeconds = 0.5f;
        public const int MaxGoalSeedsPerField = 24;

        static FlowFieldGrid toHostileField;
        static FlowFieldGrid toAlliedField;
        static readonly List<Vector3> seedBuffer = new List<Vector3>(MaxGoalSeedsPerField);
        static Scene activeScene;
        static float nextRebuildTime = -1f;
        static Scene obstaclesBuiltScene;
        static float groundElevation;

        public static FlowFieldGrid ToHostileField => toHostileField;
        public static FlowFieldGrid ToAlliedField => toAlliedField;
        public static int FlowStepCount { get; private set; }

        public static void ResetStats()
        {
            FlowStepCount = 0;
        }

        public static void Invalidate()
        {
            nextRebuildTime = -1f;
            obstaclesBuiltScene = default;
        }

        /// <summary>
        /// <paramref name="seekerTeam"/> 진영 유닛이 적 진영을 향해 나아갈 공유 <see cref="FlowFieldGrid"/>를 돌려준다.
        /// 일정 주기(<see cref="RebuildIntervalSeconds"/>)마다 한 번만 시드와 방향장을 갱신한다.
        /// </summary>
        public static FlowFieldGrid GetAssaultField(CombatTeam seekerTeam)
        {
            EnsureUpdated();
            return seekerTeam == CombatTeam.Allied ? toHostileField : toAlliedField;
        }

        public static void EnsureUpdated()
        {
            IReadOnlyList<CharacterActor> actors = CharacterActor.ActiveActors;
            if (actors.Count == 0) return;

            Scene currentScene = actors[0].gameObject.scene;
            float now = Time.time;
            if (currentScene == activeScene && toHostileField != null && toAlliedField != null && now < nextRebuildTime)
                return;

            if (currentScene != activeScene || toHostileField == null || toAlliedField == null)
            {
                activeScene = currentScene;
                CreateGridsForActiveBattlefield(actors);
                obstaclesBuiltScene = default;
            }

            if (obstaclesBuiltScene != currentScene)
            {
                BakeStaticNavMeshObstacles(toHostileField);
                CopyBlocked(toHostileField, toAlliedField);
                obstaclesBuiltScene = currentScene;
            }

            CollectTeamSeeds(CombatTeam.Hostile, seedBuffer);
            toHostileField.Build(seedBuffer);

            CollectTeamSeeds(CombatTeam.Allied, seedBuffer);
            toAlliedField.Build(seedBuffer);

            nextRebuildTime = now + RebuildIntervalSeconds;
        }

        static void CreateGridsForActiveBattlefield(IReadOnlyList<CharacterActor> actors)
        {
            Vector3 min = new Vector3(float.PositiveInfinity, 0f, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, 0f, float.NegativeInfinity);
            float ySum = 0f;
            int count = 0;

            for (int i = 0; i < actors.Count; i++)
            {
                CharacterActor actor = actors[i];
                if (actor == null) continue;
                Vector3 p = actor.transform.position;
                if (p.x < min.x) min.x = p.x;
                if (p.z < min.z) min.z = p.z;
                if (p.x > max.x) max.x = p.x;
                if (p.z > max.z) max.z = p.z;
                ySum += p.y;
                count++;
            }

            groundElevation = count > 0 ? ySum / count : 0f;
            const float margin = 36f;
            Vector3 origin = new Vector3(
                Mathf.Floor((min.x - margin) / DefaultCellSize) * DefaultCellSize,
                groundElevation,
                Mathf.Floor((min.z - margin) / DefaultCellSize) * DefaultCellSize);

            int width = Mathf.Clamp(Mathf.CeilToInt((max.x + margin - origin.x) / DefaultCellSize), 16, 120);
            int depth = Mathf.Clamp(Mathf.CeilToInt((max.z + margin - origin.z) / DefaultCellSize), 16, 120);

            toHostileField = new FlowFieldGrid(width, depth, DefaultCellSize, origin);
            toAlliedField = new FlowFieldGrid(width, depth, DefaultCellSize, origin);
        }

        static void BakeStaticNavMeshObstacles(FlowFieldGrid grid)
        {
            grid.ClearBlocked();
            float sampleRadius = grid.CellSize * 0.45f;
            for (int z = 0; z < grid.Depth; z++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Vector3 center = grid.CellCenterWorld(x, z);
                    // 해당 격자 중심 근처에 지상 NavMesh가 없으면 장애물(벽·기둥·외곽 밖) 칸으로 표시한다.
                    bool walkable = NavMesh.SamplePosition(center, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas)
                        && Mathf.Abs(hit.position.y - groundElevation) <= 1.2f;
                    if (!walkable)
                        grid.SetBlocked(x, z, true);
                }
            }
        }

        static void CopyBlocked(FlowFieldGrid source, FlowFieldGrid dest)
        {
            dest.ClearBlocked();
            for (int z = 0; z < source.Depth; z++)
                for (int x = 0; x < source.Width; x++)
                    if (source.IsBlocked(x, z)) dest.SetBlocked(x, z, true);
        }

        static void CollectTeamSeeds(CombatTeam targetTeam, List<Vector3> seeds)
        {
            seeds.Clear();
            IReadOnlyList<CharacterActor> teamActors = targetTeam == CombatTeam.Allied
                ? CombatSpatialIndex.Grid.AlliedItems
                : CombatSpatialIndex.Grid.HostileItems;
            if (teamActors.Count == 0) return;

            int stride = Mathf.Max(1, teamActors.Count / MaxGoalSeedsPerField);
            for (int i = 0; i < teamActors.Count && seeds.Count < MaxGoalSeedsPerField; i += stride)
            {
                CharacterActor actor = teamActors[i];
                if (actor == null || actor.Health == null || actor.Health.IsDead) continue;
                Vector3 pos = actor.transform.position;
                // 지상층(높이 차 1.5m 안)에 있는 유닛을 우선 시드로 넣는다.
                if (Mathf.Abs(pos.y - groundElevation) <= 1.5f)
                    seeds.Add(pos);
            }
            if (seeds.Count == 0 && teamActors[0] != null)
                seeds.Add(teamActors[0].transform.position);
        }

        /// <summary>
        /// 지상 유닛이 목표까지 아직 멀 때 공유 Flow Field에서 진행 방향(<paramref name="flowDirection"/>)을 샘플링한다.
        /// </summary>
        public static bool TryGetFlowDirection(
            CombatTeam seekerTeam,
            Vector3 currentPosition,
            out Vector3 flowDirection)
        {
            flowDirection = Vector3.zero;
            FlowFieldGrid field = GetAssaultField(seekerTeam);
            if (field == null || !field.TrySampleDirection(currentPosition, out flowDirection))
                return false;
            FlowStepCount++;
            return true;
        }

        /// <summary>
        /// 지상 원거리 유닛이 목표까지 아직 멀 때 공유 Flow Field에서 진행 방향을 샘플링해
        /// 앞쪽 짧은 걸음 지점(<paramref name="stepTarget"/>)을 돌려준다.
        /// </summary>
        public static bool TryGetFlowStep(
            CombatTeam seekerTeam,
            Vector3 currentPosition,
            float stepDistance,
            out Vector3 flowDirection,
            out Vector3 stepTarget)
        {
            flowDirection = Vector3.zero;
            stepTarget = currentPosition;
            FlowFieldGrid field = GetAssaultField(seekerTeam);
            if (field == null || !field.TrySampleDirection(currentPosition, out flowDirection))
                return false;

            Vector3 candidate = currentPosition + flowDirection * Mathf.Max(2f, stepDistance);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                stepTarget = hit.position;
                FlowStepCount++;
                return true;
            }
            return false;
        }
    }
}
