using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 원거리(LOD 2) 및 대규모 행군 유닛들이 개별 <see cref="UnityEngine.AI.NavMeshAgent.CalculatePath"/>를 부르지 않고
    /// 하나의 공유 방향장을 따라 이동하도록 2D XZ 격자에서 다중 시드 BFS/Dijkstra 통합장(Integration Field)과
    /// 방향 벡터장(Flow Vector Field)을 GC 할당 없이 계산하는 순수 클래스(PERF-ai-flow-field).
    /// </summary>
    public sealed class FlowFieldGrid
    {
        public const int BlockedCost = int.MaxValue;
        const int CardinalStepCost = 10;
        const int DiagonalStepCost = 14;

        readonly int width;
        readonly int depth;
        readonly float cellSize;
        readonly float inverseCellSize;
        readonly Vector3 origin;
        readonly bool[] blocked;
        readonly int[] integration;
        readonly Vector2[] directions;
        readonly int[] queue;

        public FlowFieldGrid(int width, int depth, float cellSize, Vector3 origin)
        {
            this.width = width > 1 ? width : 1;
            this.depth = depth > 1 ? depth : 1;
            this.cellSize = cellSize >= 0.5f ? cellSize : 0.5f;
            inverseCellSize = 1f / this.cellSize;
            this.origin = origin;

            int total = this.width * this.depth;
            blocked = new bool[total];
            integration = new int[total];
            directions = new Vector2[total];
            queue = new int[total * 4];
            ClearAll();
        }

        public int Width => width;
        public int Depth => depth;
        public float CellSize => cellSize;
        public Vector3 Origin => origin;
        public bool HasValidField { get; private set; }

        /// <summary>
        /// 지상 기준 높이를 계산한다(BUG-ai-core-defects). 성벽 위 수비병이 섞여 평균 높이가 들리지 않도록
        /// 유효한 최소 높이를 지면 기준으로 삼는다. 값이 없으면 0.
        /// </summary>
        public static float ComputeGroundElevation(IReadOnlyList<float> elevations)
        {
            if (elevations == null || elevations.Count == 0) return 0f;
            float min = float.PositiveInfinity;
            for (int i = 0; i < elevations.Count; i++)
            {
                float y = elevations[i];
                if (float.IsNaN(y) || float.IsInfinity(y)) continue;
                if (y < min) min = y;
            }
            return float.IsPositiveInfinity(min) ? 0f : min;
        }

        /// <summary>높이가 지상 Flow Field 대역 안에 있는지(성벽 위·고지대 유닛 제외).</summary>
        public static bool IsWithinGroundBand(float elevation, float groundElevation, float tolerance = 1.5f) =>
            Mathf.Abs(elevation - groundElevation) <= tolerance;

        public void ClearAll()
        {
            Array.Clear(blocked, 0, blocked.Length);
            ResetField();
        }

        public void ClearBlocked()
        {
            Array.Clear(blocked, 0, blocked.Length);
        }

        void ResetField()
        {
            for (int i = 0; i < integration.Length; i++)
            {
                integration[i] = BlockedCost;
                directions[i] = Vector2.zero;
            }
            HasValidField = false;
        }

        public bool InBounds(int x, int z) => x >= 0 && x < width && z >= 0 && z < depth;

        public int IndexOf(int x, int z) => z * width + x;

        public Vector3 CellCenterWorld(int x, int z) =>
            new Vector3(origin.x + (x + 0.5f) * cellSize, origin.y, origin.z + (z + 0.5f) * cellSize);

        public bool WorldToCell(Vector3 worldPosition, out int x, out int z)
        {
            x = Mathf.FloorToInt((worldPosition.x - origin.x) * inverseCellSize);
            z = Mathf.FloorToInt((worldPosition.z - origin.z) * inverseCellSize);
            return InBounds(x, z);
        }

        public bool IsBlocked(int x, int z) => !InBounds(x, z) || blocked[IndexOf(x, z)];

        public void SetBlocked(int x, int z, bool value = true)
        {
            if (InBounds(x, z))
                blocked[IndexOf(x, z)] = value;
        }

        /// <summary>세계 좌표 AABB 영역(<paramref name="minWorld"/> ~ <paramref name="maxWorld"/>)과 겹치는 격자 칸을 막는다.</summary>
        public void BlockWorldBounds(Vector3 minWorld, Vector3 maxWorld)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt((minWorld.x - origin.x) * inverseCellSize), 0, width - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt((maxWorld.x - origin.x) * inverseCellSize), 0, width - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((minWorld.z - origin.z) * inverseCellSize), 0, depth - 1);
            int maxZ = Mathf.Clamp(Mathf.FloorToInt((maxWorld.z - origin.z) * inverseCellSize), 0, depth - 1);
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    blocked[IndexOf(x, z)] = true;
        }

        public int IntegrationAt(int x, int z) => InBounds(x, z) ? integration[IndexOf(x, z)] : BlockedCost;

        public Vector2 DirectionAt(int x, int z) => InBounds(x, z) ? directions[IndexOf(x, z)] : Vector2.zero;

        /// <summary>
        /// 하나 이상의 목표 위치(<paramref name="goalWorldPositions"/>)를 시드(비용 0)로 삼아
        /// 8방향 파동 전파로 통합장(Integration Field)과 각 칸의 진행 방향 벡터를 계산한다.
        /// 목표 칸 자체가 장애물에 묻혀 있으면 가장 가까운 이웃 빈 칸을 시드로 삼는다.
        /// </summary>
        public bool Build(IReadOnlyList<Vector3> goalWorldPositions)
        {
            ResetField();
            if (goalWorldPositions == null || goalWorldPositions.Count == 0) return false;

            int head = 0;
            int tail = 0;

            for (int i = 0; i < goalWorldPositions.Count; i++)
            {
                if (!TryFindSeedCell(goalWorldPositions[i], out int sx, out int sz)) continue;
                int seedIdx = IndexOf(sx, sz);
                if (integration[seedIdx] == 0) continue;
                integration[seedIdx] = 0;
                queue[tail++ % queue.Length] = seedIdx;
            }

            if (tail == 0) return false;

            while (head < tail)
            {
                int currentIdx = queue[head++ % queue.Length];
                int cx = currentIdx % width;
                int cz = currentIdx / width;
                int baseCost = integration[currentIdx];

                for (int dz = -1; dz <= 1; dz++)
                {
                    int nz = cz + dz;
                    if (nz < 0 || nz >= depth) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = cx + dx;
                        if (nx < 0 || nx >= width) continue;
                        int nIdx = nz * width + nx;
                        if (blocked[nIdx]) continue;

                        bool diagonal = dx != 0 && dz != 0;
                        // 대각선 이동 시 양옆 직교 칸 중 하나라도 막혀 있으면 모서리에 끼지 않도록 대각선 전파를 막는다.
                        if (diagonal && (blocked[cz * width + nx] || blocked[nz * width + cx]))
                            continue;

                        int nextCost = baseCost + (diagonal ? DiagonalStepCost : CardinalStepCost);
                        if (nextCost < integration[nIdx])
                        {
                            integration[nIdx] = nextCost;
                            queue[tail++ % queue.Length] = nIdx;
                        }
                    }
                }
            }

            ComputeDirections();
            HasValidField = true;
            return true;
        }

        bool TryFindSeedCell(Vector3 worldPos, out int seedX, out int seedZ)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt((worldPos.x - origin.x) * inverseCellSize), 0, width - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt((worldPos.z - origin.z) * inverseCellSize), 0, depth - 1);
            if (!blocked[IndexOf(cx, cz)])
            {
                seedX = cx;
                seedZ = cz;
                return true;
            }
            for (int radius = 1; radius <= 2; radius++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = cx + dx;
                        int nz = cz + dz;
                        if (!InBounds(nx, nz) || blocked[IndexOf(nx, nz)]) continue;
                        seedX = nx;
                        seedZ = nz;
                        return true;
                    }
                }
            }
            seedX = 0;
            seedZ = 0;
            return false;
        }

        void ComputeDirections()
        {
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = z * width + x;
                    int here = integration[idx];
                    if (here == BlockedCost || here == 0)
                    {
                        directions[idx] = Vector2.zero;
                        continue;
                    }

                    int bestCost = here;
                    int bestDx = 0;
                    int bestDz = 0;

                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int nz = z + dz;
                        if (nz < 0 || nz >= depth) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int nx = x + dx;
                            if (nx < 0 || nx >= width) continue;
                            if (dx != 0 && dz != 0 && (blocked[z * width + nx] || blocked[nz * width + x]))
                                continue;
                            int c = integration[nz * width + nx];
                            if (c < bestCost)
                            {
                                bestCost = c;
                                bestDx = dx;
                                bestDz = dz;
                            }
                        }
                    }

                    if (bestDx == 0 && bestDz == 0)
                        directions[idx] = Vector2.zero;
                    else
                        directions[idx] = new Vector2(bestDx, bestDz).normalized;
                }
            }
        }

        /// <summary>
        /// 세계 좌표 <paramref name="worldPosition"/>에서 주변 4칸의 방향 벡터를 이중선형(Bilinear) 보간해
        /// 부드러운 3D 수평 진행 방향 벡터(<paramref name="flowDirection"/>)를 돌려준다.
        /// 도달 불가능한 섬이거나 목표 칸 위면 false와 <see cref="Vector3.zero"/>를 돌려준다.
        /// </summary>
        public bool TrySampleDirection(Vector3 worldPosition, out Vector3 flowDirection)
        {
            flowDirection = Vector3.zero;
            if (!HasValidField || !WorldToCell(worldPosition, out int cx, out int cz))
                return false;

            int cellIdx = IndexOf(cx, cz);
            if (integration[cellIdx] == BlockedCost)
                return false;

            // 격자 중심 기준 연속 좌표로 주변 4칸 보간
            float fx = (worldPosition.x - origin.x) * inverseCellSize - 0.5f;
            float fz = (worldPosition.z - origin.z) * inverseCellSize - 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, width - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, depth - 1);
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int z1 = Mathf.Min(z0 + 1, depth - 1);
            float tx = Mathf.Clamp01(fx - x0);
            float tz = Mathf.Clamp01(fz - z0);

            Vector2 d00 = directions[IndexOf(x0, z0)];
            Vector2 d10 = directions[IndexOf(x1, z0)];
            Vector2 d01 = directions[IndexOf(x0, z1)];
            Vector2 d11 = directions[IndexOf(x1, z1)];

            Vector2 top = Vector2.Lerp(d00, d10, tx);
            Vector2 bottom = Vector2.Lerp(d01, d11, tx);
            Vector2 blended = Vector2.Lerp(top, bottom, tz);

            if (blended.sqrMagnitude < 1e-4f)
                blended = directions[cellIdx];
            if (blended.sqrMagnitude < 1e-4f)
                return false;

            blended.Normalize();
            flowDirection = new Vector3(blended.x, 0f, blended.y);
            return true;
        }
    }
}
