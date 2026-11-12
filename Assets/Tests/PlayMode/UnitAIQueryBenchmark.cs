using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Stats;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// PERF-nav-query: 실제 AI Update의 CPU 시간을 분리해서 측정한다.
    /// 자동 Update를 끄고 캐시한 delegate로 프레임당 한 번 호출한다.
    /// NavMesh 내부 시뮬레이션/렌더링은 AI 측정에 포함되지 않는다.
    /// 일반 회귀 테스트와 분리된 Explicit 벤치마크다.
    /// </summary>
    public class UnitAIQueryBenchmark
    {
        const int WarmupFrames = 30;
        const int MeasureFrames = 120;
        readonly Vector3 origin = new Vector3(2000f, 0f, 2000f);
        readonly List<Object> assets = new List<Object>();
        readonly List<UnitCombatAI> units = new List<UnitCombatAI>();
        readonly List<Action> updates = new List<Action>();
        Scene scene;
        Scene originalScene;
        float previousCapture;
        int previousVSync;
        int previousTargetRate;
        bool previousBackground;
        SoldierDefinition soldier;
        CombatProfile profile;
        UnitAISettings settings;
        GameObject unitRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            previousCapture = Time.captureDeltaTime;
            previousVSync = QualitySettings.vSyncCount;
            previousTargetRate = Application.targetFrameRate;
            previousBackground = Application.runInBackground;
            Time.captureDeltaTime = 1f / 30f;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
            scene = SceneManager.CreateScene("UnitAIQueryBenchmark");
            SceneManager.SetActiveScene(scene);
            var geometry = new GameObject("BenchmarkGeometry");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(geometry.transform);
            ground.transform.position = origin + Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(140f, 1f, 140f);
            var surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            assets.Add(surface.navMeshData);
            soldier = Track(SoldierDefinition.Create("benchmark", new Cost(0),
                new StatValue(StatType.MaxHealth, 1000000f),
                new StatValue(StatType.Attack, 0f),
                new StatValue(StatType.AttackSpeed, 1f),
                new StatValue(StatType.AttackRange, 2f),
                new StatValue(StatType.MoveSpeed, 3.5f)));
            profile = Track(CombatProfile.Create(minimumDamage: 0f));
            settings = Track(UnitAISettings.Create());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = previousCapture;
            QualitySettings.vSyncCount = previousVSync;
            Application.targetFrameRate = previousTargetRate;
            Application.runInBackground = previousBackground;
            if (originalScene.IsValid() && originalScene.isLoaded)
                SceneManager.SetActiveScene(originalScene);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (Object asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
            units.Clear();
            updates.Clear();
            yield return null;
        }

        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        [UnityTest, Explicit, Category("Benchmark"), Timeout(900000)]
        public IEnumerator MeasureRealAIAt50v50And200v200()
        {
            var report = new StringBuilder();
            report.AppendLine("PERF-nav-query: real UnitCombatAI.Update, cached delegate dispatch, flat connected arena");
            report.AppendLine($"UTC={DateTime.UtcNow:O} Unity={Application.unityVersion} OS={SystemInfo.operatingSystem}");
            report.AppendLine($"CPU={SystemInfo.processorType} logicalCores={SystemInfo.processorCount} editor={Application.isEditor}");
            report.AppendLine($"warmup={WarmupFrames} measure={MeasureFrames} captureDelta=1/30 decisionInterval={settings.DecisionInterval}");
            report.AppendLine("AI-only ms excludes engine NavMesh steering, physics, rendering and frame waiting; no performance pass threshold.");
            report.AppendLine("case,perTeam,AI_mean_ms,AI_p50_ms,AI_p95_ms,AI_max_ms,wall_mean_ms,mean_targets,mean_movers,mean_Move_directives");
            string output = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "benchmark_nav_query.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            foreach (int count in new[] { 50, 200 })
            foreach (string scenario in new[] { "Move", "HoldIdle", "HoldContact", "Engage" })
            {
                Spawn(count, scenario);
                yield return null;
                foreach (UnitCombatAI unit in units)
                    Assert.IsTrue(unit.GetComponent<NavMeshAgent>().isOnNavMesh, unit.name);
                var samples = new double[MeasureFrames];
                double wallSum = 0, targets = 0, movers = 0, moveDirectives = 0;
                var wall = new Stopwatch();
                var cpu = new Stopwatch();
                for (int frame = 0; frame < WarmupFrames + MeasureFrames; frame++)
                {
                    wall.Restart();
                    cpu.Restart();
                    foreach (Action update in updates) update();
                    cpu.Stop();
                    double elapsed = cpu.Elapsed.TotalMilliseconds;
                    yield return null;
                    wall.Stop();
                    if (frame < WarmupFrames) continue;
                    samples[frame - WarmupFrames] = elapsed;
                    wallSum += wall.Elapsed.TotalMilliseconds;
                    foreach (UnitCombatAI unit in units)
                    {
                        if (unit.Target != null) targets++;
                        if (unit.GetComponent<NavMeshAgent>().velocity.sqrMagnitude > 0.01f) movers++;
                        if (unit.Directive == UnitDirective.Move) moveDirectives++;
                        Assert.IsFalse(unit.Actor.Health.IsDead, "Benchmark population must remain constant.");
                    }
                }
                double sum = 0;
                foreach (double sample in samples) sum += sample;
                Array.Sort(samples);
                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7:F1},{8:F1},{9:F1}",
                    scenario, count, sum / MeasureFrames, samples[MeasureFrames / 2],
                    samples[(int)Math.Ceiling(MeasureFrames * 0.95) - 1], samples[MeasureFrames - 1],
                    wallSum / MeasureFrames, targets / MeasureFrames, movers / MeasureFrames, moveDirectives / MeasureFrames));
                File.WriteAllText(output, report.ToString());
                Object.Destroy(unitRoot);
                units.Clear();
                updates.Clear();
                yield return null;
            }
            UnityEngine.Debug.Log(report.ToString());
            TestContext.WriteLine("Report: " + output);
        }

        void Spawn(int perTeam, string scenario)
        {
            unitRoot = new GameObject("BenchmarkUnits");
            MethodInfo updateMethod = typeof(UnitCombatAI).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(updateMethod, "AI Update method changed; update the benchmark adapter.");
            for (int team = 0; team < 2; team++)
            for (int i = 0; i < perTeam; i++)
            {
                float sign = team == 0 ? -1f : 1f;
                // Contact uses interleaved pairs, each 4m apart, with clear 10m lanes.
                // Idle groups are over 6m apart; Move/Engage start at the same separated positions.
                Vector3 point = scenario == "HoldContact"
                    ? new Vector3((i % 20 - 9.5f) * 6f, 0f, (i / 20 - 4.5f) * 10f + sign * 2f)
                    : new Vector3((i % 20 - 9.5f) * 2f, 0f, sign * (18f + i / 20 * 2f));
                var root = new GameObject($"Unit_{team}_{i}");
                root.SetActive(false);
                root.transform.SetParent(unitRoot.transform);
                root.transform.position = origin + point;
                var body = root.AddComponent<CapsuleCollider>();
                body.radius = profile.Radius;
                body.height = profile.Height;
                body.center = Vector3.up;
                var actor = root.AddComponent<CharacterActor>();
                actor.Configure(soldier, profile, team == 0 ? CombatTeam.Allied : CombatTeam.Hostile);
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = profile.Radius;
                agent.height = profile.Height;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                var ai = root.AddComponent<UnitCombatAI>();
                ai.Configure(settings);
                root.SetActive(true);
                // Prevent automatic MonoBehaviour.Update while retaining the production logic unmodified.
                ai.enabled = false;
                if (scenario == "Move") ai.SetDirective(UnitDirective.Move, origin + new Vector3(point.x, 0f, -sign * 52f));
                else ai.SetDirective(scenario == "Engage" ? UnitDirective.Engage : UnitDirective.Hold);
                units.Add(ai);
                updates.Add((Action)Delegate.CreateDelegate(typeof(Action), ai, updateMethod));
            }
            Physics.SyncTransforms();
        }
    }
}
