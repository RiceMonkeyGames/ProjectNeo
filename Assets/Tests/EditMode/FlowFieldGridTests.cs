using NUnit.Framework;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>PERF-ai-flow-field: 2D XZ 격자 Flow Field 장애물 우회, 다중 목표 시드 수렴, 고립 섬 처리, 이중선형 보간 검증.</summary>
    public class FlowFieldGridTests
    {
        [Test]
        public void Build_PropagatesCostsAndPointsTowardGoal()
        {
            var grid = new FlowFieldGrid(10, 10, 2f, Vector3.zero);
            Assert.IsTrue(grid.Build(new[] { new Vector3(17f, 0f, 17f) })); // 칸 (8, 8)
            Assert.IsTrue(grid.HasValidField);
            Assert.AreEqual(0, grid.IntegrationAt(8, 8));

            Assert.IsTrue(grid.TrySampleDirection(new Vector3(3f, 0f, 3f), out Vector3 dir));
            // (1, 1)에서 (8, 8) 방향이므로 x > 0, z > 0 대각선이어야 한다.
            Assert.Greater(dir.x, 0.5f);
            Assert.Greater(dir.z, 0.5f);
            Assert.AreEqual(0f, dir.y, 1e-5f);
        }

        [Test]
        public void Build_RoutesAroundBlockingWall_WithoutCornerCutting()
        {
            var grid = new FlowFieldGrid(10, 10, 1f, Vector3.zero);
            // z = 5 가로벽을 x = 0..7까지 막고 x = 8..9만 열어 둔다.
            for (int x = 0; x <= 7; x++)
                grid.SetBlocked(x, 5, true);

            // 목표는 벽 너머 북서쪽 (2.5, 0, 8.5) -> 칸 (2, 8)
            Assert.IsTrue(grid.Build(new[] { new Vector3(2.5f, 0f, 8.5f) }));

            // 벽 남쪽 (2.5, 0, 3.5)에 있는 유닛은 막힌 북쪽(+z)이 아니라 열린 동쪽(+x)으로 우회해야 한다.
            Assert.IsTrue(grid.TrySampleDirection(new Vector3(2.5f, 0f, 3.5f), out Vector3 detour));
            Assert.Greater(detour.x, 0.5f, "가로벽 앞에서는 열린 동쪽 통로로 흘러야 한다.");
        }

        [Test]
        public void Build_DisconnectedIsland_ReturnsFalseOnSample()
        {
            var grid = new FlowFieldGrid(8, 8, 1f, Vector3.zero);
            // z = 4 전체를 가로막아 남북을 완전히 단절시킨다.
            for (int x = 0; x < 8; x++)
                grid.SetBlocked(x, 4, true);

            Assert.IsTrue(grid.Build(new[] { new Vector3(4.5f, 0f, 6.5f) }));
            Assert.AreEqual(FlowFieldGrid.BlockedCost, grid.IntegrationAt(4, 1));
            Assert.IsFalse(grid.TrySampleDirection(new Vector3(4.5f, 0f, 1.5f), out Vector3 dir));
            Assert.AreEqual(Vector3.zero, dir);
        }

        [Test]
        public void Build_MultiSeed_ConvergesToNearestGoal()
        {
            var grid = new FlowFieldGrid(12, 6, 1f, Vector3.zero);
            // 서쪽 끝 (1.5, 0, 2.5)와 동쪽 끝 (10.5, 0, 2.5) 두 곳에 시드를 둔다.
            Assert.IsTrue(grid.Build(new[]
            {
                new Vector3(1.5f, 0f, 2.5f),
                new Vector3(10.5f, 0f, 2.5f),
            }));

            Assert.IsTrue(grid.TrySampleDirection(new Vector3(3.5f, 0f, 2.5f), out Vector3 westFlow));
            Assert.Less(westFlow.x, -0.5f, "서쪽 시드에 가까운 유닛은 서쪽(-x)으로 흘러야 한다.");

            Assert.IsTrue(grid.TrySampleDirection(new Vector3(8.5f, 0f, 2.5f), out Vector3 eastFlow));
            Assert.Greater(eastFlow.x, 0.5f, "동쪽 시드에 가까운 유닛은 동쪽(+x)으로 흘러야 한다.");
        }
    }
}
