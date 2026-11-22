using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>PERF-ai-spatial-grid: 2D 공간 해시 그리드 격자 좌표, 음수 좌표, 진영 분리, 반경 탐색 일치 검증.</summary>
    public class CombatSpatialGridTests
    {
        [Test]
        public void CellCoords_HandlesPositiveAndNegativeWorldCoordinates()
        {
            var grid = new CombatSpatialGrid<int>(8f);
            grid.CellCoords(new Vector3(0f, 0f, 0f), out int x0, out int z0);
            Assert.AreEqual((0, 0), (x0, z0));

            grid.CellCoords(new Vector3(7.99f, 5f, 15.99f), out int x1, out int z1);
            Assert.AreEqual((0, 1), (x1, z1));

            grid.CellCoords(new Vector3(-0.01f, 0f, -8.01f), out int x2, out int z2);
            Assert.AreEqual((-1, -2), (x2, z2));
        }

        [Test]
        public void QueryCells_FiltersByTeam_AndFindsAllUnitsInOverlappingCells()
        {
            var grid = new CombatSpatialGrid<string>(8f, 4);
            grid.Insert("AllyOrigin", CombatTeam.Allied, new Vector3(1f, 0f, 1f));
            grid.Insert("EnemyNear", CombatTeam.Hostile, new Vector3(2f, 0f, 2f));
            grid.Insert("EnemyNegative", CombatTeam.Hostile, new Vector3(-3f, 0f, -2f));
            grid.Insert("EnemyFar", CombatTeam.Hostile, new Vector3(80f, 0f, 80f));

            var results = new List<string>();
            grid.QueryCells(CombatTeam.Hostile, Vector3.zero, 4f, results);

            CollectionAssert.AreEquivalent(new[] { "EnemyNear", "EnemyNegative" }, results);
            CollectionAssert.DoesNotContain(results, "AllyOrigin");
            CollectionAssert.DoesNotContain(results, "EnemyFar");
        }

        [Test]
        public void QueryCircleXZ_MatchesBruteForceExactRadius_AcrossManyUnits()
        {
            var grid = new CombatSpatialGrid<int>(8f, 32);
            var positions = new Vector3[200];
            for (int i = 0; i < positions.Length; i++)
            {
                float x = ((i * 37) % 160) - 80f + (i & 3) * 0.25f;
                float z = ((i * 53) % 160) - 80f + (i & 7) * 0.125f;
                positions[i] = new Vector3(x, (i % 5) * 3f, z);
                grid.Insert(i, i % 2 == 0 ? CombatTeam.Allied : CombatTeam.Hostile, positions[i]);
            }

            Vector3 center = new Vector3(-12.5f, 0f, 18.2f);
            const float radius = 22f;
            var actual = new List<int>();
            grid.QueryCircleXZ(CombatTeam.Hostile, center, radius, actual);

            var expected = new List<int>();
            for (int i = 0; i < positions.Length; i++)
            {
                if (i % 2 == 0) continue;
                float dx = positions[i].x - center.x;
                float dz = positions[i].z - center.z;
                if (dx * dx + dz * dz <= radius * radius) expected.Add(i);
            }

            CollectionAssert.AreEquivalent(expected, actual);
        }

        [Test]
        public void Clear_AllowsReuseWithoutLeakingPreviousEntries()
        {
            var grid = new CombatSpatialGrid<string>(8f);
            grid.Insert("OldEnemy", CombatTeam.Hostile, Vector3.zero);
            Assert.AreEqual(1, grid.Count);

            grid.Clear();
            Assert.AreEqual(0, grid.Count);
            Assert.AreEqual(0, grid.HostileItems.Count);

            var results = new List<string>();
            grid.QueryCells(CombatTeam.Hostile, Vector3.zero, 10f, results);
            Assert.AreEqual(0, results.Count);

            grid.Insert("NewEnemy", CombatTeam.Hostile, new Vector3(5f, 0f, 5f));
            grid.QueryCells(CombatTeam.Hostile, Vector3.zero, 10f, results);
            CollectionAssert.AreEqual(new[] { "NewEnemy" }, results);
        }
    }
}
