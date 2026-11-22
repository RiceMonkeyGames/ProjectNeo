using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 대규모 전투(PERF-ai-spatial-grid)에서 XZ 평면을 정사각형 칸으로 나누어
    /// 근접·중거리 범위 안의 유닛을 GC 할당 없이 빠르게 찾는 순수 2D 공간 해시 그리드.
    /// </summary>
    public sealed class CombatSpatialGrid<T>
    {
        readonly Dictionary<long, int> alliedHeads = new Dictionary<long, int>(128);
        readonly Dictionary<long, int> hostileHeads = new Dictionary<long, int>(128);
        readonly List<T> alliedAll = new List<T>(256);
        readonly List<T> hostileAll = new List<T>(256);
        T[] items;
        Vector3[] positions;
        int[] next;
        int count;
        float cellSize;
        float inverseCellSize;

        public CombatSpatialGrid(float cellSize = 8f, int initialCapacity = 512)
        {
            SetCellSize(cellSize);
            int cap = initialCapacity > 16 ? initialCapacity : 16;
            items = new T[cap];
            positions = new Vector3[cap];
            next = new int[cap];
        }

        public float CellSize => cellSize;
        public int Count => count;
        public IReadOnlyList<T> AlliedItems => alliedAll;
        public IReadOnlyList<T> HostileItems => hostileAll;

        /// <summary>칸 크기(m)를 바꾼다. 0.5m보다 작으면 0.5m로 맞춘다.</summary>
        public void SetCellSize(float size)
        {
            cellSize = size >= 0.5f ? size : 0.5f;
            inverseCellSize = 1f / cellSize;
        }

        /// <summary>그리드에 등록된 모든 항목을 비운다(배열 메모리는 재사용한다).</summary>
        public void Clear()
        {
            alliedHeads.Clear();
            hostileHeads.Clear();
            alliedAll.Clear();
            hostileAll.Clear();
            Array.Clear(items, 0, count);
            count = 0;
        }

        /// <summary>세계 좌표 <paramref name="position"/>이 속하는 격자 칸 좌표 (cellX, cellZ).</summary>
        public void CellCoords(Vector3 position, out int cellX, out int cellZ)
        {
            cellX = Mathf.FloorToInt(position.x * inverseCellSize);
            cellZ = Mathf.FloorToInt(position.z * inverseCellSize);
        }

        public static long PackKey(int cellX, int cellZ) =>
            ((long)cellX << 32) | (uint)cellZ;

        /// <summary>유닛 하나를 진영(<paramref name="team"/>)과 위치(<paramref name="position"/>)에 맞춰 칸에 넣는다.</summary>
        public void Insert(T item, CombatTeam team, Vector3 position)
        {
            if (count == items.Length) Grow();
            int index = count++;
            items[index] = item;
            positions[index] = position;

            CellCoords(position, out int cx, out int cz);
            long key = PackKey(cx, cz);
            Dictionary<long, int> heads = team == CombatTeam.Allied ? alliedHeads : hostileHeads;
            List<T> teamList = team == CombatTeam.Allied ? alliedAll : hostileAll;
            teamList.Add(item);

            if (heads.TryGetValue(key, out int previousHead))
                next[index] = previousHead;
            else
                next[index] = -1;
            heads[key] = index;
        }

        void Grow()
        {
            int newCap = items.Length * 2;
            Array.Resize(ref items, newCap);
            Array.Resize(ref positions, newCap);
            Array.Resize(ref next, newCap);
        }

        /// <summary>
        /// <paramref name="targetTeam"/> 진영 유닛 중 <paramref name="center"/>에서 수평(XZ) 반경 <paramref name="radius"/>와
        /// 겹치는 모든 격자 칸의 유닛을 <paramref name="results"/>에 담는다(기존 내용은 비운다).
        /// 칸 개수가 전체 유닛을 훑는 것보다 많으면 진영 전체 목록을 바로 복사해 돌려준다.
        /// </summary>
        public void QueryCells(CombatTeam targetTeam, Vector3 center, float radius, List<T> results)
        {
            results.Clear();
            List<T> teamList = targetTeam == CombatTeam.Allied ? alliedAll : hostileAll;
            if (teamList.Count == 0) return;

            float r = radius > 0f ? radius : 0f;
            int minX = Mathf.FloorToInt((center.x - r) * inverseCellSize);
            int maxX = Mathf.FloorToInt((center.x + r) * inverseCellSize);
            int minZ = Mathf.FloorToInt((center.z - r) * inverseCellSize);
            int maxZ = Mathf.FloorToInt((center.z + r) * inverseCellSize);

            long spanX = (long)maxX - minX + 1;
            long spanZ = (long)maxZ - minZ + 1;
            // 전장 전체를 덮는 넓은 탐색(칸 64개 초과이면서 진영 전체 인원보다 조회 칸 수가 많을 때)은
            // 해시 조회 대신 진영 목록을 바로 돌려줘 불필요한 빈 칸 순회를 막는다.
            if (spanX * spanZ > 64 && spanX * spanZ > teamList.Count)
            {
                results.AddRange(teamList);
                return;
            }

            Dictionary<long, int> heads = targetTeam == CombatTeam.Allied ? alliedHeads : hostileHeads;
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!heads.TryGetValue(PackKey(x, z), out int node)) continue;
                    while (node >= 0)
                    {
                        results.Add(items[node]);
                        node = next[node];
                    }
                }
            }
        }

        /// <summary>
        /// <paramref name="targetTeam"/> 진영 유닛 중 <paramref name="center"/>에서 수평(XZ) 거리 <paramref name="radius"/> 안에
        /// 실제로 들어오는 유닛만 <paramref name="results"/>에 담는다.
        /// </summary>
        public void QueryCircleXZ(CombatTeam targetTeam, Vector3 center, float radius, List<T> results)
        {
            results.Clear();
            List<T> teamList = targetTeam == CombatTeam.Allied ? alliedAll : hostileAll;
            if (teamList.Count == 0) return;

            float r = radius > 0f ? radius : 0f;
            float sqrRadius = r * r;
            int minX = Mathf.FloorToInt((center.x - r) * inverseCellSize);
            int maxX = Mathf.FloorToInt((center.x + r) * inverseCellSize);
            int minZ = Mathf.FloorToInt((center.z - r) * inverseCellSize);
            int maxZ = Mathf.FloorToInt((center.z + r) * inverseCellSize);

            Dictionary<long, int> heads = targetTeam == CombatTeam.Allied ? alliedHeads : hostileHeads;
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!heads.TryGetValue(PackKey(x, z), out int node)) continue;
                    while (node >= 0)
                    {
                        Vector3 pos = positions[node];
                        float dx = pos.x - center.x;
                        float dz = pos.z - center.z;
                        if (dx * dx + dz * dz <= sqrRadius)
                            results.Add(items[node]);
                        node = next[node];
                    }
                }
            }
        }
    }
}
