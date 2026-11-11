using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.AI.Navigation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// B-1 성능 확인. 임시 유닛을 지면과 성벽 위 두 층에서 길찾기로 움직이며 프레임 비용을 잰다.
    /// 평소 테스트에는 끼지 않도록 Explicit로 두고, 필요할 때만 이름으로 골라 돌린다.
    /// 결과는 Logs/benchmark_b1.txt에 쓴다.
    /// </summary>
    public class UnitScaleBenchmark
    {
        const float WallHeight = 4f;
        const int WarmupFrames = 60;
        const int MeasureFrames = 240;

        struct Scenario
        {
            public int count;
            public ObstacleAvoidanceType avoidance;
        }

        [UnityTest, Explicit, Category("Benchmark"), Timeout(900000)]
        public IEnumerator MeasureAgentsOnGroundAndWallTop()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            // 프레임마다 게임 시간이 1/30초씩 흐르게 고정한다. 실제 프레임이 아무리 빨라도 유닛이 충분히 걸어간다.
            Time.captureDeltaTime = 1f / 30f;

            var report = new StringBuilder();
            var geometryRoot = new GameObject("BenchGeometry");
            var agentRoot = new GameObject("BenchAgents");
            var wallPieces = BuildArena(geometryRoot.transform);
            CreateCameraAndLight();

            NavMeshSurface surface = geometryRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            var buildTimer = Stopwatch.StartNew();
            surface.BuildNavMesh();
            buildTimer.Stop();
            report.AppendLine($"navmesh full build: {buildTimer.Elapsed.TotalMilliseconds:F1} ms");
            yield return null;

            // 성벽 위가 실제로 길찾기에 포함되고, 성 안쪽 지면에서 걸어 올라갈 수 있는지 확인한다.
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(0f, WallHeight, 0f), out NavMeshHit wallTop, 1f, NavMesh.AllAreas), "성벽 위에 길이 없다");
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(0f, 0f, -20f), out NavMeshHit inside, 2f, NavMesh.AllAreas), "성 안쪽 지면에 길이 없다");
            var path = new NavMeshPath();
            NavMesh.CalculatePath(inside.position, wallTop.position, NavMesh.AllAreas, path);
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "지면에서 성벽 위로 가는 길이 이어지지 않는다");
            report.AppendLine($"wall top reachable: yes (wall top y={wallTop.position.y:F2})");

            var scenarios = new[]
            {
                new Scenario { count = 100, avoidance = ObstacleAvoidanceType.HighQualityObstacleAvoidance },
                new Scenario { count = 300, avoidance = ObstacleAvoidanceType.HighQualityObstacleAvoidance },
                new Scenario { count = 500, avoidance = ObstacleAvoidanceType.HighQualityObstacleAvoidance },
                new Scenario { count = 500, avoidance = ObstacleAvoidanceType.LowQualityObstacleAvoidance },
                new Scenario { count = 500, avoidance = ObstacleAvoidanceType.NoObstacleAvoidance },
                new Scenario { count = 1000, avoidance = ObstacleAvoidanceType.LowQualityObstacleAvoidance },
            };

            string[] markerNames = { "PlayerLoop", "NavMeshManager", "Physics.Simulate", "RenderPipelineManager.DoRenderLoop_Internal" };
            var recorders = new List<ProfilerRecorder>();
            foreach (string name in markerNames)
                recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Internal, name));

            var random = new System.Random(12345);
            foreach (Scenario scenario in scenarios)
            {
                List<NavMeshAgent> agents = SpawnAgents(agentRoot.transform, scenario, random);
                var sums = new double[recorders.Count];
                double maxPlayerLoopMs = 0;
                double wallClockSum = 0;
                int onWallTop = 0;

                for (int frame = 0; frame < WarmupFrames + MeasureFrames; frame++)
                {
                    DriveAgents(agents, random, frame);
                    yield return null;
                    if (frame < WarmupFrames) continue;

                    for (int i = 0; i < recorders.Count; i++)
                        sums[i] += recorders[i].Valid ? recorders[i].LastValue / 1e6 : 0;
                    double playerLoopMs = recorders[0].Valid ? recorders[0].LastValue / 1e6 : 0;
                    if (playerLoopMs > maxPlayerLoopMs) maxPlayerLoopMs = playerLoopMs;
                    wallClockSum += Time.unscaledDeltaTime * 1000.0;
                }

                foreach (NavMeshAgent agent in agents)
                    if (agent.transform.position.y > WallHeight - 0.5f) onWallTop++;

                var line = new StringBuilder();
                line.Append($"agents={scenario.count} avoidance={scenario.avoidance}");
                for (int i = 0; i < recorders.Count; i++)
                    line.Append(recorders[i].Valid ? $" | {markerNames[i]} {sums[i] / MeasureFrames:F2} ms" : $" | {markerNames[i]} n/a");
                line.Append($" | PlayerLoop max {maxPlayerLoopMs:F2} ms");
                line.Append($" | wall-clock frame {wallClockSum / MeasureFrames:F2} ms");
                line.Append($" | agents on wall top at end {onWallTop}");
                report.AppendLine(line.ToString());

                foreach (NavMeshAgent agent in agents) Object.Destroy(agent.gameObject);
                yield return null;
            }

            foreach (ProfilerRecorder recorder in recorders) recorder.Dispose();

            // 성벽 조각 하나가 부서졌을 때 길찾기를 다시 만드는 비용.
            Object.DestroyImmediate(wallPieces[wallPieces.Count / 2]);
            var rebuildTimer = Stopwatch.StartNew();
            surface.BuildNavMesh();
            rebuildTimer.Stop();
            report.AppendLine($"navmesh full rebuild after removing one wall piece: {rebuildTimer.Elapsed.TotalMilliseconds:F1} ms");

            Object.DestroyImmediate(wallPieces[wallPieces.Count / 2 + 1]);
            var asyncTimer = Stopwatch.StartNew();
            AsyncOperation update = surface.UpdateNavMesh(surface.navMeshData);
            int asyncFrames = 0;
            double worstFrameMs = 0;
            while (!update.isDone)
            {
                var frameTimer = Stopwatch.StartNew();
                yield return null;
                frameTimer.Stop();
                asyncFrames++;
                if (frameTimer.Elapsed.TotalMilliseconds > worstFrameMs) worstFrameMs = frameTimer.Elapsed.TotalMilliseconds;
            }
            asyncTimer.Stop();
            report.AppendLine($"navmesh async update after removing one wall piece: {asyncTimer.Elapsed.TotalMilliseconds:F1} ms over {asyncFrames} frames");

            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(0f, 0f, 0f), out _, 1f, NavMesh.AllAreas), "성벽이 부서진 자리에 지면 길이 생기지 않았다");

            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "benchmark_b1.txt");
            File.WriteAllText(outputPath, report.ToString());
            UnityEngine.Debug.Log("B-1 benchmark\n" + report);

            Time.captureDeltaTime = 0f;
            Object.Destroy(geometryRoot);
            Object.Destroy(agentRoot);
        }

        /// <summary>지면, 가로로 긴 성벽 한 줄, 성 안쪽(z &lt; 0)에서 성벽 위로 오르는 경사로 두 개.</summary>
        static List<GameObject> BuildArena(Transform parent)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(parent);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(120f, 1f, 120f);

            var wallPieces = new List<GameObject>();
            for (int i = 0; i < 12; i++)
            {
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Wall_" + i;
                piece.transform.SetParent(parent);
                piece.transform.position = new Vector3(-27.5f + 5f * i, WallHeight / 2f, 0f);
                piece.transform.localScale = new Vector3(5f, WallHeight, 3f);
                wallPieces.Add(piece);
            }

            const float rampLength = 10f;
            float angle = Mathf.Asin(WallHeight / rampLength) * Mathf.Rad2Deg;
            float run = Mathf.Cos(angle * Mathf.Deg2Rad) * rampLength;
            foreach (float x in new[] { -20f, 20f })
            {
                GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ramp.name = "Ramp";
                ramp.transform.SetParent(parent);
                ramp.transform.localScale = new Vector3(3f, 0.5f, rampLength);
                ramp.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
                ramp.transform.position = new Vector3(x, WallHeight / 2f - 0.25f, -1.5f - run / 2f);
            }

            return wallPieces;
        }

        static void CreateCameraAndLight()
        {
            var cameraObject = new GameObject("BenchCamera");
            cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 45f, -70f);
            cameraObject.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

            var lightObject = new GameObject("BenchLight");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static List<NavMeshAgent> SpawnAgents(Transform parent, Scenario scenario, System.Random random)
        {
            var agents = new List<NavMeshAgent>();
            for (int i = 0; i < scenario.count; i++)
            {
                GameObject unit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                unit.name = "Unit_" + i;
                unit.transform.SetParent(parent);
                // 절반은 성 안쪽(아군 자리), 절반은 성 바깥(적 자리)에서 시작한다.
                bool ally = i % 2 == 0;
                unit.transform.position = RandomGroundPoint(random, ally);
                unit.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);

                Rigidbody body = unit.AddComponent<Rigidbody>();
                body.isKinematic = true;

                NavMeshAgent agent = unit.AddComponent<NavMeshAgent>();
                agent.radius = 0.5f;
                agent.height = 2f;
                agent.speed = 4f;
                agent.obstacleAvoidanceType = scenario.avoidance;
                agents.Add(agent);
            }
            return agents;
        }

        /// <summary>도착한 유닛에게 새 목적지를 주고, 매 프레임 일부 유닛은 가장 가까운 상대편을 찾는다.</summary>
        static void DriveAgents(List<NavMeshAgent> agents, System.Random random, int frame)
        {
            for (int i = 0; i < agents.Count; i++)
            {
                NavMeshAgent agent = agents[i];
                if (!agent.isOnNavMesh || agent.pathPending) continue;
                if (agent.hasPath && agent.remainingDistance > 1.5f) continue;

                bool ally = i % 2 == 0;
                Vector3 destination;
                int roll = random.Next(3);
                if (roll == 0) destination = new Vector3(Range(random, -28f, 28f), WallHeight, Range(random, -1f, 1f));
                else destination = RandomGroundPoint(random, roll == 1 ? ally : !ally);
                agent.SetDestination(destination);
            }

            // 대상 탐색 비용 흉내: 유닛의 10분의 1이 매 프레임 가장 가까운 상대편을 전수 조사한다.
            for (int i = frame % 10; i < agents.Count; i += 10)
            {
                Vector3 self = agents[i].transform.position;
                float best = float.MaxValue;
                for (int j = (i + 1) % 2; j < agents.Count; j += 2)
                {
                    float distance = (agents[j].transform.position - self).sqrMagnitude;
                    if (distance < best) best = distance;
                }
            }
        }

        static Vector3 RandomGroundPoint(System.Random random, bool inside)
        {
            float z = inside ? Range(random, -50f, -12f) : Range(random, 5f, 50f);
            return new Vector3(Range(random, -50f, 50f), 0f, z);
        }

        static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
