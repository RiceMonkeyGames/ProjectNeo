#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>B-6.4: 실제 생성기로 만든 혼합 편성 씬에서 스폰/행동/변종/집계를 검증한다.</summary>
    public class EnemyCompositionPlayTests
    {
        const string ScenePath = "Assets/_Project/Scenes/EnemyCompositionVerification.unity";
        Scene scene;
        ConstructionWorld world;
        SiegeDirector director;
        SquadCommandCenter center;
        BuildPieceDefinition wall;
        float previousCapture;
        CursorLockMode previousLock;
        bool previousVisible;

        T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
        FlyerBrain[] Flyers => director.Enemies.OfType<FlyerBrain>().ToArray();
        Vector3 Ground(int x, float z) => world.transform.TransformPoint(new Vector3((x + 0.5f) * world.Grid.CellSize, 0f, z * world.Grid.CellSize));

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime;
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            Time.captureDeltaTime = 1f / 30f;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            world = Find<ConstructionWorld>(); director = Find<SiegeDirector>(); center = Find<SquadCommandCenter>();
            yield return NavigationReady();
            wall = world.Structures.First(s => s.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
            foreach (var member in center.Squads.SelectMany(s => s.Members))
            {
                member.enabled = false;
                member.GetComponent<NavMeshAgent>().enabled = false;
                member.Actor.Stats.SetBase(StatType.MaxHealth, 10000f);
                member.Actor.Health.RestoreFull();
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.captureDeltaTime = previousCapture;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }

        IEnumerator NavigationReady()
        {
            for (int i = 0; i < 600; i++)
            {
                yield return null;
                if (!world.IsRebuilding && !world.RebuildPending) yield break;
            }
            Assert.Fail("Navigation did not settle.");
        }

        void Start()
        {
            Assert.IsTrue(director.StartBattle());
            Find<PlayerInputController>().enabled = false;
            Find<SquadCommandInput>().enabled = false;
        }

        void Warp(SiegeAttacker attacker, Vector3 point)
        {
            var agent = attacker.GetComponent<NavMeshAgent>();
            if (agent != null) Assert.IsTrue(agent.Warp(point));
            else attacker.transform.position = point;
        }

        [UnityTest]
        public IEnumerator Roster_BindsSceneSettings_StartsOnce_AndCountsAllDeaths()
        {
            Assert.AreEqual(5, director.Enemies.Count);
            Assert.AreEqual(2, director.CountEnemies(SiegeAttackerRole.Ladder));
            Assert.AreEqual(1, director.CountEnemies(SiegeAttackerRole.WallBreaker));
            Assert.AreEqual(2, director.CountEnemies(SiegeAttackerRole.Flyer));
            foreach (var enemy in director.Enemies)
            {
                Assert.IsFalse(enemy.gameObject.activeSelf);
                // 복사한 씬 설정을 연결해야 한다. 프리팹의 원본 설정이 남는 회귀를 잡는다.
                Assert.AreSame(director.Settings, new SerializedObject(enemy).FindProperty("settings").objectReferenceValue);
                Assert.AreEqual(enemy.UsesFallController, enemy.GetComponent<UnitFallController>() != null);
                Assert.AreEqual(enemy.UsesFallController, enemy.GetComponent<NavMeshAgent>() != null);
            }
            Start();
            Assert.IsFalse(director.StartBattle());
            Assert.AreEqual(5, director.Enemies.Count);
            Assert.IsTrue(director.Enemies.All(e => e.gameObject.activeSelf));
            foreach (var enemy in director.Enemies) enemy.Actor.ReceiveDamage(1000000f);
            for (int i = 0; i < 60 && director.Phase != SiegePhase.Finished; i++) yield return null;
            Assert.AreEqual(0, director.EnemiesAlive);
            Assert.AreEqual(0, director.EnemiesFled);
            Assert.AreEqual(SiegeOutcome.Victory, director.Outcome);
            yield return null;
            Assert.AreEqual(SiegeOutcome.Victory, director.Outcome);
        }

        [UnityTest]
        public IEnumerator ClosedWall_MixedEnemies_BreakClimbAndFlySimultaneously()
        {
            var ladder = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SiegeLadder>()).First(l => l.HasWall);
            Start();
            var breaker = director.Enemies.Single(e => e.Role == SiegeAttackerRole.WallBreaker);
            Warp(breaker, Ground(2, 9.5f));
            breaker.Actor.Stats.SetBase(StatType.Attack, 1f); // 등반 관측 전에 성벽을 전부 부수지 않도록 둔다.
            foreach (var climber in director.Enemies.Where(e => e.Role == SiegeAttackerRole.Ladder))
                Warp(climber, ladder.BasePoint + Vector3.forward * 1.5f);
            foreach (var flyer in Flyers) Warp(flyer, Ground(14, 11f));
            var support = world.Structures.Single(s => s.Placement.Cell == new Vector2Int(2, 8) && s.Placement.Level == 0);
            float hp = support.Health.Current;
            bool climbed = false, wallHit = false, flewOver = false, flyerHit = false;
            var guards = center.Squads[0].Members.Select(m => m.Actor).ToArray();
            float guardHp = guards.Sum(g => g.Health.Current);
            float castleTop = world.transform.position.y + world.Structures.Max(s => s.Placement.Elevation + s.Placement.Definition.Height);
            for (int i = 0; i < 1200 && !(climbed && wallHit && flewOver && flyerHit); i++)
            {
                yield return null;
                climbed |= director.Enemies.OfType<SiegeAttackerBrain>().Any(e => e.Role == SiegeAttackerRole.Ladder && e.IsClimbing);
                wallHit |= support == null || support.Health.Current < hp;
                flewOver |= Flyers.Any(f => f.transform.position.y > castleTop + 1f);
                // 비행 적이 성벽 위 부대를 겨누는 동안 받은 피해를 기록한다.
                flyerHit |= guards.Sum(g => g.Health.Current) < guardHp && Flyers.Any(f => guards.Contains(f.Target));
            }
            Assert.IsTrue(wallHit, "Wall breaker never damaged the closed wall.");
            Assert.IsTrue(climbed, "Ladder troops never climbed while other roles were active.");
            Assert.IsTrue(flewOver, "Flyers never cleared the castle.");
            Assert.IsTrue(flyerHit, "Flyers never attacked wall-top defenders.");
        }

        [UnityTest]
        public IEnumerator OpenBreach_GroundRolesEngage_WhileFlyersKeepWallTopPriority()
        {
            var gap = world.Structures.Single(s => s.Placement.Cell == new Vector2Int(2, 8) && s.Placement.Level == 0);
            gap.ReceiveDamage(gap.Health.Maximum);
            yield return NavigationReady();
            float hp = world.Structures.Sum(s => s.Health.Current);
            Start();
            foreach (var enemy in director.Enemies) Warp(enemy, Ground(2, 10f));
            var ground = director.Enemies.OfType<SiegeAttackerBrain>().ToArray();
            for (int i = 0; i < 120 && !ground.All(e => e.IsAssaulting); i++) yield return null;
            Assert.IsTrue(ground.All(e => e.IsAssaulting));
            Assert.IsNull(ground.Single(e => e.Role == SiegeAttackerRole.WallBreaker).GetComponent<WallBreakerAttack>().Target);
            Assert.AreEqual(hp, world.Structures.Sum(s => s.Health.Current));
            Assert.IsTrue(Flyers.All(f => f.Target != null && f.Target.transform.position.y >= wall.Height - 0.1f));
            for (int i = 0; i < 600 && !ground.Any(e => world.LocalPoint(e.transform.position).z < 8f * world.Grid.CellSize); i++) yield return null;
            Assert.IsTrue(ground.Any(e => world.LocalPoint(e.transform.position).z < 8f * world.Grid.CellSize));
        }

        [UnityTest]
        public IEnumerator DataOnlyHighArcherVariant_ReusesModules_HoversAboveMeleeAndDealsProjectileDamage()
        {
            Start();
            var low = Flyers.Single(f => f.Actor.Profile.AttackKind == AttackKind.Melee);
            var high = Flyers.Single(f => f.Actor.Profile.AttackKind == AttackKind.Projectile);
            Assert.AreSame(low.Settings.TargetStrategy, high.Settings.TargetStrategy);
            Assert.AreSame(low.Settings.Movement, high.Settings.Movement);
            Assert.AreEqual(low.Settings.Engagement.GetType(), high.Settings.Engagement.GetType());
            Assert.AreNotSame(low.Settings.Engagement, high.Settings.Engagement);
            foreach (var enemy in director.Enemies.Where(e => e != high)) enemy.gameObject.SetActive(false);
            Warp(high, Ground(14, 11f));
            CharacterActor target = center.Squads[0].Members[0].Actor;
            foreach (var member in center.Squads[0].Members.Skip(1)) member.gameObject.SetActive(false);
            float hp = target.Health.Current;
            for (int i = 0; i < 900; i++)
            {
                yield return null;
                if (target.Health.Current < hp && Vector3.Distance(high.transform.position, high.Destination) < 0.2f) break;
            }
            Assert.AreEqual(target, high.Target);
            Assert.Less(target.Health.Current, hp, "Data-only projectile variant did not damage its target.");
            Assert.AreEqual(3f, high.transform.position.y - target.transform.position.y, 0.2f);
            Assert.IsFalse(high.GetComponent<AirborneBody>().IsMeleeExposed);
            Assert.IsFalse(CombatPhysics.CanTarget(target, high.Actor));
            Assert.IsFalse(CombatPhysics.IsInRange(target, high.Actor));
        }
    }
}
#endif
