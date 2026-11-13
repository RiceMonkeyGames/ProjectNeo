using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>B-6.3 비행 적. 실제 SiegePrototype 씬의 성벽 위에서 확인한다.</summary>
    public class FlyerPlayTests
    {
        const int WallRow = 8;
        Scene scene;
        ConstructionWorld world;
        SiegeDirector director;
        BuildPieceDefinition wall;
        SquadCommandCenter center;
        CharacterActor player;
        FlyerBrain flyer;
        float previousCapture;
        CursorLockMode previousLock;
        bool previousVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime;
            previousLock = Cursor.lockState; previousVisible = Cursor.visible;
            Time.captureDeltaTime = 1f / 30f;
            yield return SceneManager.LoadSceneAsync("SiegePrototype", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            world = Find<ConstructionWorld>(); director = Find<SiegeDirector>(); center = Find<SquadCommandCenter>();
            yield return null;
            director.enabled = false;
            var input = Find<PlayerInputController>();
            input.enabled = false;
            player = input.GetComponent<CharacterActor>();
            Find<SquadCommandInput>().enabled = false;
            // 아군은 움직이지 않는 과녁으로 두고, 필요한 시험만 다시 켠다.
            foreach (UnitCombatAI member in Members())
            {
                member.enabled = false;
                member.GetComponent<NavMeshAgent>().enabled = false;
                member.gameObject.SetActive(false);
            }
            wall = world.Structures.First(p => p.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
            foreach (int x in new[] { 4, 11, 12 })
                Assert.IsTrue(world.TryPlace(wall, new Vector2Int(x, WallRow), 0, out _));
            yield return NavigationReady();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.captureDeltaTime = previousCapture;
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
        }

        T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
        IEnumerable<UnitCombatAI> Members() => center.Squads.SelectMany(s => s.Members).Where(m => m != null);
        Vector3 Ground(int x, float z) => world.transform.TransformPoint(new Vector3((x + 0.5f) * world.Grid.CellSize, 0f, z * world.Grid.CellSize));
        Vector3 WallTop(int x) => world.WorldCenter(wall, new Vector2Int(x, WallRow), 0) + Vector3.up * wall.Height;
        static void Kill(CharacterActor actor) => actor.ReceiveDamage(1000000f);

        IEnumerator NavigationReady()
        {
            for (int frame = 0; frame < 600; frame++)
            {
                yield return null;
                if (!world.IsRebuilding && !world.RebuildPending) yield break;
            }
            Assert.Fail("Navigation rebuild did not finish.");
        }

        CharacterActor Place(AttackKind kind, Vector3 at, int skip = 0)
        {
            UnitCombatAI member = Members().Where(m => m.Actor.Profile.AttackKind == kind && !m.gameObject.activeSelf).Skip(skip).First();
            member.transform.position = at;
            member.gameObject.SetActive(true);
            return member.Actor;
        }

        void Spawn(Vector3 at)
        {
            GameObject prefab = director.Settings.EnemyGroups.Select(g => g.unitPrefab)
                .First(p => p != null && p.GetComponent<FlyerBrain>() != null);
            Assert.IsFalse(prefab.activeSelf);
            Assert.IsNull(prefab.GetComponent<NavMeshAgent>(), "비행 적은 길찾기를 쓰지 않는다.");
            GameObject unit = Object.Instantiate(prefab, at, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(unit, scene);
            flyer = unit.GetComponent<FlyerBrain>();
            flyer.Bind(world, new SiegeLadder[0], Vector2Int.up, director.Settings);
            unit.SetActive(true);
        }

        float TopOfCastle => world.transform.position.y + world.Structures.Max(p => p.Placement.Elevation + p.Placement.Definition.Height);

        [UnityTest]
        public IEnumerator ClosedWall_FliesOver_AndAttacksWallTopAllyBeforeNearerGroundAlly()
        {
            player.transform.position = Ground(2, 7f);
            CharacterActor onWall = Place(AttackKind.Melee, WallTop(9));
            float hp = onWall.Health.Current;
            Spawn(Ground(2, 11f));
            float highest = float.NegativeInfinity;
            for (int frame = 0; frame < 900 && onWall.Health.Current >= hp; frame++)
            {
                yield return null;
                highest = Mathf.Max(highest, flyer.transform.position.y);
            }
            Assert.AreEqual(onWall, flyer.Target, "땅의 플레이어가 더 가까워도 성벽 위 아군을 먼저 노린다.");
            Assert.Less(onWall.Health.Current, hp, "성벽 위 아군이 공격받지 않았다.");
            Assert.Greater(highest, TopOfCastle + 1f, "성벽 위로 넘어 날아야 한다.");
            // 대상과 같은 발판 위에 낮게 떠서 근접으로 친다.
            var body = flyer.GetComponent<AirborneBody>();
            Assert.AreEqual(onWall.transform.position.y, body.GroundPoint.y, 0.2f);
            Assert.IsTrue(body.IsMeleeExposed);
        }

        [UnityTest]
        public IEnumerator NoWallTopAlly_TargetsNearestAllyInsideCastle()
        {
            player.transform.position = Ground(2, 2f);
            CharacterActor near = Place(AttackKind.Melee, Ground(9, 6f));
            Spawn(Ground(9, 11f));
            for (int frame = 0; frame < 30 && flyer.Target == null; frame++) yield return null;
            Assert.AreEqual(near, flyer.Target);
            float hp = near.Health.Current;
            for (int frame = 0; frame < 900 && near.Health.Current >= hp; frame++) yield return null;
            Assert.Less(near.Health.Current, hp);
        }

        [UnityTest]
        public IEnumerator TargetDeath_ReselectsWithoutWaitingForDecisionInterval()
        {
            CharacterActor first = Place(AttackKind.Melee, WallTop(9));
            CharacterActor second = Place(AttackKind.Melee, WallTop(5));
            Spawn(Ground(9, 11f));
            for (int frame = 0; frame < 30 && flyer.Target == null; frame++) yield return null;
            Assert.AreEqual(first, flyer.Target);
            // 판단 주기 한가운데에서 대상을 죽인다.
            for (int frame = 0; frame < 3; frame++) yield return null;
            Kill(first);
            yield return null;
            yield return null;
            Assert.AreEqual(second, flyer.Target, "죽은 대상은 바로 다시 골라야 한다.");
        }

        [UnityTest]
        public IEnumerator NoAlly_HoldsHighAboveCourtyard_OutOfMeleeReach()
        {
            player.gameObject.SetActive(false);
            Spawn(Ground(9, 11f));
            for (int frame = 0; frame < 600; frame++) yield return null;
            Assert.IsTrue(flyer.IsHolding);
            Assert.IsNull(flyer.Target);
            Vector3 local = world.LocalPoint(flyer.transform.position);
            Assert.Less(local.z, WallRow * world.Grid.CellSize, "성 안쪽 위에서 기다려야 한다.");
            Assert.AreEqual(flyer.CruiseHeight, flyer.transform.position.y, 0.05f);
            Assert.IsFalse(flyer.GetComponent<AirborneBody>().IsMeleeExposed, "높이 떠 있을 때는 근접으로 닿지 않는다.");
        }

        [UnityTest]
        public IEnumerator HoveringFlyer_IsHitByWallTopMeleeAlly_ViaGroundProjection()
        {
            CharacterActor guard = Place(AttackKind.Melee, WallTop(9));
            var ai = guard.GetComponent<UnitCombatAI>();
            var agent = guard.GetComponent<NavMeshAgent>();
            agent.enabled = true;
            Assert.IsTrue(agent.Warp(WallTop(9)));
            ai.enabled = true;
            ai.SetDirective(UnitDirective.Hold);
            Spawn(Ground(9, 11f));
            // 성벽보다 높이 날아오는 동안에는 근접으로 노리지도, 닿지도 않는다.
            for (int frame = 0; frame < 300 && flyer.transform.position.y < TopOfCastle + 1f; frame++) yield return null;
            Assert.Greater(flyer.transform.position.y, TopOfCastle + 1f);
            Assert.IsFalse(CombatPhysics.IsInRange(guard, flyer.Actor));
            Assert.IsFalse(CombatPhysics.CanTarget(guard, flyer.Actor));
            float hp = -1f;
            for (int frame = 0; frame < 900; frame++)
            {
                yield return null;
                if (flyer.Actor.Health.IsDead) break;
                if (hp < 0f) hp = flyer.Actor.Health.Current;
                if (flyer.Actor.Health.Current < hp) break;
            }
            Assert.IsTrue(flyer.Actor.Health.IsDead || flyer.Actor.Health.Current < hp, "성벽 위 근접 아군이 떠 있는 비행 적을 치지 못했다.");
            Assert.IsTrue(flyer.GetComponent<AirborneBody>().IsMeleeExposed || flyer.Actor.Health.IsDead);
        }

        [UnityTest]
        public IEnumerator UpwardShot_AtFlyer_DealsFullDamage_ButWallTopTargetStillReduced()
        {
            // 위로 쏘는 감소(50%)가 있는 적 궁수 사격으로 확인한다. 비행 적은 이 시험에서만 아군 편으로 둔다.
            SiegeAttacker archer = director.Enemies.First(e => e.Role == SiegeAttackerRole.Archer && !e.gameObject.activeSelf);
            archer.enabled = false;
            archer.GetComponent<UnitCombatAI>().enabled = false;
            archer.GetComponent<NavMeshAgent>().enabled = false;
            archer.transform.position = Ground(9, 12f);
            archer.gameObject.SetActive(true);
            CharacterActor shooter = archer.Actor;
            Assert.Less(shooter.Profile.UpwardShotDamageRatio, 1f, "감소가 있는 궁수로 시험해야 한다.");
            player.gameObject.SetActive(false);
            Spawn(Ground(9, 12f) + Vector3.up * 8f);
            flyer.enabled = false;
            flyer.Actor.SetTeam(CombatTeam.Allied);
            yield return null;
            CharacterActor wallTop = Place(AttackKind.Melee, WallTop(9));
            yield return null;
            float attack = shooter.Stats.GetValue(StatType.Attack);
            float full = DamageRules.Calculate(attack, flyer.Actor.Stats.GetValue(StatType.Defense), shooter.Profile.MinimumDamage);
            yield return Shoot(shooter, flyer.Actor, attack);
            Assert.AreEqual(flyer.Actor.Health.Maximum - full, flyer.Actor.Health.Current, 1e-3f, "공중 대상은 온전한 피해를 받는다.");
            float reduced = DamageRules.Calculate(attack, wallTop.Stats.GetValue(StatType.Defense), shooter.Profile.MinimumDamage)
                * shooter.Profile.UpwardShotDamageRatio;
            yield return Shoot(shooter, wallTop, attack);
            Assert.AreEqual(wallTop.Health.Maximum - reduced, wallTop.Health.Current, 1e-3f, "성벽 위를 쏠 때의 감소는 그대로다.");
        }

        IEnumerator Shoot(CharacterActor shooter, CharacterActor target, float attack)
        {
            GameObject shot = Object.Instantiate(shooter.Profile.ProjectilePrefab, shooter.AimPoint, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(shot, scene);
            Vector3 direction = target.AimPoint - shooter.AimPoint;
            shot.GetComponent<CombatProjectile>().Launch(shooter, attack, direction, direction.magnitude + 2f);
            for (int frame = 0; frame < 120 && shot != null; frame++) yield return null;
        }
    }
}
