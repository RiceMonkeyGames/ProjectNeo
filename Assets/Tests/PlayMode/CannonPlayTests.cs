using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>대포 자동 사격·포탄 폭발(SIEGE-02 4단계). 건설 시험 씬에 시험용 대포와 허수아비를 놓는다.</summary>
    public class CannonPlayTests
    {
        Scene scene;
        ConstructionWorld world;
        ConstructionSandbox sandbox;
        float previousCapture;
        readonly List<Object> owned = new List<Object>();
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            yield return SceneManager.LoadSceneAsync("ConstructionPrototype", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            var roots = scene.GetRootGameObjects();
            world = roots.Select(root => root.GetComponent<ConstructionWorld>()).Single(value => value != null);
            sandbox = roots.Select(root => root.GetComponent<ConstructionSandbox>()).Single(value => value != null);
            roots.Select(root => root.GetComponent<PlacementController>()).Single(value => value != null).enabled = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object value in owned) if (value != null) Object.Destroy(value);
            owned.Clear();
            yield return SceneManager.UnloadSceneAsync(scene);
            Time.captureDeltaTime = previousCapture;
        }

        CannonModule MountCannon(CannonSettings settings)
        {
            var template = Own(new GameObject("TestCannonTemplate"));
            template.SetActive(false);
            SceneManager.MoveGameObjectToScene(template, scene);
            template.AddComponent<PlacedSiegeWeapon>();
            template.AddComponent<CannonModule>().Configure(settings, null);
            SiegeWeaponDefinition definition = Own(SiegeWeaponDefinition.Create("test_cannon", "test_cannon",
                new Vector3(1.2f, 1.1f, 1.8f), template, 150f));
            // 정면 70m 안에 다른 성 조각이 없는 성벽과 방향을 고른다. 포탄과 허수아비가 성벽에 막히지 않게 한다.
            Physics.SyncTransforms();
            foreach (PlacedStructure support in world.Structures.Where(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall
                && !world.Layout.IsCovered(piece.Placement)).ToArray())
            {
                if (!support.TryGetMountPose("top", out Pose top)) continue;
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    Vector3 facing = ConstructionWorld.RotateMountPose(top, rotation).rotation * Vector3.forward;
                    facing.y = 0f;
                    facing.Normalize();
                    if (!ClearAhead(support, top.position, facing)) continue;
                    Assert.IsTrue(world.TryMount(definition, support, "top", out PlacedSiegeWeapon weapon, rotation));
                    weapon.gameObject.SetActive(true);
                    return weapon.GetComponent<CannonModule>();
                }
            }
            Assert.Fail("정면이 트인 성벽이 없다.");
            return null;
        }

        static bool ClearAhead(PlacedStructure support, Vector3 top, Vector3 facing)
        {
            foreach (float height in new[] { 0.5f, 2.5f })
                foreach (float side in new[] { -3f, 0f, 3f })
                {
                    Vector3 origin = new Vector3(top.x, height, top.z) + Vector3.Cross(Vector3.up, facing) * side;
                    foreach (RaycastHit hit in Physics.RaycastAll(origin, facing, 70f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var piece = hit.collider.GetComponentInParent<PlacedStructure>();
                        if (piece != null && piece != support) return false;
                    }
                }
            return true;
        }

        CharacterActor Dummy(Vector3 groundPosition, CombatTeam team)
        {
            var root = Own(new GameObject("Dummy_" + team));
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var actor = root.AddComponent<CharacterActor>();
            actor.Configure(sandbox.Unit.Character.Definition, sandbox.Unit.Profile, team);
            var body = root.AddComponent<CapsuleCollider>();
            body.height = 2f; body.radius = 0.4f; body.center = Vector3.up;
            root.transform.position = groundPosition;
            root.SetActive(true);
            return actor;
        }

        static Vector3 Ground(CannonModule cannon, float forward, float side = 0f)
        {
            Vector3 right = Vector3.Cross(Vector3.up, cannon.Facing);
            Vector3 point = cannon.Muzzle + cannon.Facing * forward + right * side;
            point.y = 0f;
            return point;
        }

        static IEnumerator WaitForShells(int frames = 300)
        {
            for (int i = 0; i < frames && CannonShell.Active.Count > 0; i++) yield return null;
            Assert.AreEqual(0, CannonShell.Active.Count, "포탄이 제때 터지거나 사라져야 한다.");
        }

        [UnityTest]
        public IEnumerator ArmedCannon_AutoFiresAtNearestEnemy_AndBlastSparesAllies()
        {
            CannonModule cannon = MountCannon(Own(CannonSettings.Create(autoScatterRadius: 0f)));
            CharacterActor near = Dummy(Ground(cannon, 12f), CombatTeam.Hostile);
            CharacterActor far = Dummy(Ground(cannon, 25f), CombatTeam.Hostile);
            CharacterActor ally = Dummy(Ground(cannon, 12f, 1.2f), CombatTeam.Allied);
            Physics.SyncTransforms();
            float expected = DamageRules.Calculate(40f, near.Stats.GetValue(StatType.Defense), 1f);
            int fired = 0;
            cannon.Fired += (_, _) => fired++;

            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(0, fired, "무장 전에는 쏘지 않는다.");

            cannon.SetArmed(true);
            for (int i = 0; i < 300 && near.Health.Current >= near.Health.Maximum; i++) yield return null;
            Assert.AreEqual(1, fired);
            Assert.AreSame(near, cannon.LastTarget, "가장 가까운 적을 노린다.");
            Assert.AreEqual(near.Health.Maximum - expected, near.Health.Current, 0.01f, "포탄 중심 피해에서 방어력을 뺀다.");
            Assert.AreEqual(far.Health.Maximum, far.Health.Current, "반경 밖 적은 다치지 않는다.");
            Assert.AreEqual(ally.Health.Maximum, ally.Health.Current, "아군 피해는 꺼져 있다.");
            Assert.IsFalse(cannon.Reload.IsReady, "쏜 뒤에는 재장전한다.");
            Assert.IsFalse(cannon.TryAutoFire());
            yield return WaitForShells();
        }

        [UnityTest]
        public IEnumerator Cannon_IgnoresEnemiesBehind_InsideMinimumRange_OrBeyondMaximumRange()
        {
            CannonModule cannon = MountCannon(Own(CannonSettings.Create(autoScatterRadius: 0f)));
            CharacterActor behind = Dummy(Ground(cannon, -15f), CombatTeam.Hostile);
            CharacterActor close = Dummy(Ground(cannon, 3f), CombatTeam.Hostile);
            CharacterActor distant = Dummy(Ground(cannon, 70f), CombatTeam.Hostile);
            CharacterActor wide = Dummy(Ground(cannon, 10f, 14f), CombatTeam.Hostile); // 약 54도
            Physics.SyncTransforms();
            cannon.SetArmed(true);
            Assert.IsFalse(cannon.TryAutoFire());
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(0, CannonShell.Active.Count);
            foreach (CharacterActor actor in new[] { behind, close, distant, wide })
                Assert.AreEqual(actor.Health.Maximum, actor.Health.Current);
            Assert.IsTrue(cannon.Reload.IsReady, "쏘지 않았으면 장전 상태 그대로다.");
        }

        [UnityTest]
        public IEnumerator Disarming_StopsAutoFire_AndRemovedCannonNeverFires()
        {
            CannonModule cannon = MountCannon(Own(CannonSettings.Create(autoScatterRadius: 0f, reloadSeconds: 0.2f)));
            CharacterActor target = Dummy(Ground(cannon, 15f), CombatTeam.Hostile);
            Physics.SyncTransforms();
            int fired = 0;
            cannon.Fired += (_, _) => fired++;
            cannon.SetArmed(true);
            cannon.SetArmed(false);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(0, fired, "무장을 풀면 쏘지 않는다.");

            cannon.SetManned(true);
            cannon.SetArmed(true);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(0, fired, "사람이 타고 있으면 자동 사격을 멈춘다.");
            cannon.SetManned(false);

            Assert.IsTrue(world.RemoveWeapon(cannon.Weapon));
            Assert.IsFalse(cannon.Armed);
            Assert.IsFalse(cannon.FireAt(target.transform.position));
            yield return null;
            Assert.AreEqual(0, fired);
            Assert.AreEqual(target.Health.Maximum, target.Health.Current);
        }

        CannonOperator Operator(CannonModule cannon, float back, float turn = 0f)
        {
            var root = Own(new GameObject("TestOperator"));
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var actor = root.AddComponent<CharacterActor>();
            actor.Configure(sandbox.Unit.Character.Definition, sandbox.Unit.Profile, CombatTeam.Allied);
            var body = root.AddComponent<CapsuleCollider>();
            body.height = 2f; body.radius = 0.4f; body.center = Vector3.up;
            var op = root.AddComponent<CannonOperator>();
            op.Configure(Own(CannonOperatorSettings.Create()), world, null, null);
            Vector3 position = cannon.transform.position - cannon.Facing * back;
            root.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(Quaternion.Euler(0f, turn, 0f) * cannon.Facing));
            root.SetActive(true);
            return op;
        }

        [Test]
        public void OperatorSettings_DefaultBindingsMatchConfirmedControls()
        {
            CannonOperatorSettings settings = Own(CannonOperatorSettings.Create());
            Assert.AreEqual("<Keyboard>/f", settings.Interact.bindings[0].path);
            Assert.AreEqual("<Keyboard>/escape", settings.Leave.bindings[0].path);
            Assert.AreEqual("<Mouse>/leftButton", settings.Fire.bindings[0].path);
            Assert.AreEqual("<Mouse>/delta", settings.Look.bindings[0].path);
        }

        [UnityTest]
        public IEnumerator Operator_MansOnlyNearbyArmedCannon_PausesAutoFire_AndLeaves()
        {
            CannonModule cannon = MountCannon(Own(CannonSettings.Create(autoScatterRadius: 0f)));
            CannonOperator far = Operator(cannon, 5f);
            CannonOperator away = Operator(cannon, 1.5f, 180f);
            CannonOperator op = Operator(cannon, 1.5f);
            Physics.SyncTransforms();

            Assert.IsFalse(op.TryMan(cannon), "전투 전(무장 전)에는 탈 수 없다.");
            cannon.SetArmed(true);
            Assert.IsFalse(far.TryMan(cannon), "멀면 탈 수 없다.");
            Assert.IsFalse(away.TryMan(cannon), "등지고 있으면 탈 수 없다.");
            yield return null;
            Assert.AreSame(cannon, op.Nearby, "정면 가까이의 대포를 탈 후보로 보여준다.");
            Assert.IsTrue(op.TryMan(cannon));
            Assert.IsTrue(cannon.IsManned);
            Assert.AreSame(cannon, op.Manned);
            Assert.IsFalse(away.TryMan(cannon), "이미 사람이 탄 대포에는 탈 수 없다.");

            // 탄 뒤에 적을 놓는다. 탈 때까지 기다리는 프레임에 자동 사격이 먼저 나가지 않게 한다.
            CharacterActor enemy = Dummy(Ground(cannon, 15f), CombatTeam.Hostile);
            Physics.SyncTransforms();
            int fired = 0;
            cannon.Fired += (_, _) => fired++;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(0, fired, "타고 있는 동안 자동 사격은 멈춘다.");
            Assert.AreEqual(enemy.Health.Maximum, enemy.Health.Current);

            op.Leave();
            Assert.IsFalse(cannon.IsManned);
            Assert.IsNull(op.Manned);
            for (int i = 0; i < 300 && fired == 0; i++) yield return null;
            Assert.AreEqual(1, fired, "내리면 자동 사격을 다시 한다.");
            yield return WaitForShells();
        }

        [UnityTest]
        public IEnumerator Operator_ManualFire_HitsAimPointWithoutScatter_AndRespectsReloadAndArc()
        {
            // 자동 사격 흩어짐은 2m지만 직접 쏘면 흩어지지 않는다.
            CannonModule cannon = MountCannon(Own(CannonSettings.Create()));
            CharacterActor enemy = Dummy(Ground(cannon, 18f), CombatTeam.Hostile);
            CannonOperator op = Operator(cannon, 1.5f);
            Physics.SyncTransforms();
            cannon.SetArmed(true);
            Assert.IsTrue(op.TryMan(cannon));

            Assert.IsFalse(op.FireAt(Ground(cannon, -15f)), "회전 범위 밖은 쏘지 않는다.");
            StringAssert.Contains("사거리", op.Message);
            Assert.IsFalse(op.FireAt(Ground(cannon, 3f)), "최소 사거리 안은 쏘지 않는다.");
            Assert.IsTrue(cannon.Reload.IsReady);

            Assert.IsTrue(op.FireAt(enemy.transform.position + Vector3.up));
            Assert.IsFalse(op.FireAt(enemy.transform.position), "재장전 중에는 다시 쏠 수 없다.");
            StringAssert.Contains("재장전", op.Message);
            float expected = DamageRules.Calculate(40f, enemy.Stats.GetValue(StatType.Defense), 1f);
            for (int i = 0; i < 300 && enemy.Health.Current >= enemy.Health.Maximum; i++) yield return null;
            Assert.AreEqual(enemy.Health.Maximum - expected, enemy.Health.Current, 0.01f, "겨눈 곳에 정확히 떨어진다.");
            yield return WaitForShells();
        }

        [UnityTest]
        public IEnumerator Operator_ManualFire_ReachesWideArc_AndBarrelSwivelsWithoutMovingFacing()
        {
            // 옆 약 63도: 자동 사격(±45도)은 못 쏘지만 직접 조작(±90도)은 쏜다(SIEGE-07).
            CannonModule cannon = MountCannon(Own(CannonSettings.Create(autoScatterRadius: 0f)));
            Vector3 facing = cannon.Facing;
            Vector3 muzzle = cannon.Muzzle;
            Vector3 wide = Ground(cannon, 10f, 20f);
            CharacterActor enemy = Dummy(wide, CombatTeam.Hostile);
            CannonOperator op = Operator(cannon, 1.5f);
            Physics.SyncTransforms();
            cannon.SetArmed(true);
            Assert.IsFalse(cannon.TryAutoFire(), "자동 사격 회전 범위는 넓어지지 않는다.");
            Assert.IsTrue(op.TryMan(cannon));

            // 조준만 하면 포신이 설정 속도로 따라 돌고, 설치 방향과 판정은 그대로다.
            cannon.SetSwivel(60f);
            yield return null;
            Assert.Greater(cannon.Swivel, 0f);
            Assert.Less(cannon.Swivel, 60f, "한 프레임에 다 돌지 않는다.");
            for (int i = 0; i < 60 && cannon.Swivel < 59.9f; i++) yield return null;
            Assert.AreEqual(60f, cannon.Swivel, 0.01f);
            Assert.AreEqual(0f, Vector3.Angle(facing, cannon.Facing), 0.05f, "설치 방향은 포신이 돌아도 그대로다.");
            Assert.AreEqual(60f, Vector3.SignedAngle(facing, cannon.BarrelFacing, Vector3.up), 0.05f);
            Assert.AreEqual(0f, Vector3.Distance(muzzle, cannon.AimContext.Origin), 0.01f, "판정 기준점은 포신 방향에 흔들리지 않는다.");
            cannon.SetSwivel(170f);
            Assert.IsFalse(cannon.TryAutoFire());
            for (int i = 0; i < 60; i++) yield return null;
            Assert.AreEqual(90f, cannon.Swivel, 0.01f, "회전 한계 밖으로는 돌지 않는다.");

            Assert.IsTrue(op.FireAt(enemy.transform.position + Vector3.up));
            float expectedYaw = Vector3.SignedAngle(facing, wide - cannon.transform.position, Vector3.up);
            Assert.AreEqual(expectedYaw, cannon.Swivel, 1f, "쏘는 순간 포신이 목표를 향한다.");
            float expected = DamageRules.Calculate(40f, enemy.Stats.GetValue(StatType.Defense), 1f);
            for (int i = 0; i < 300 && enemy.Health.Current >= enemy.Health.Maximum; i++) yield return null;
            Assert.AreEqual(enemy.Health.Maximum - expected, enemy.Health.Current, 0.01f, "넓은 각도에서도 겨눈 곳에 떨어진다.");
            yield return WaitForShells();

            cannon.SetArmed(false);
            Assert.AreEqual(0f, cannon.Swivel, "전투가 끝나면 포신이 설치 방향으로 돌아온다.");
            Assert.AreEqual(0f, Vector3.Angle(facing, cannon.BarrelFacing), 0.05f);
        }

        [UnityTest]
        public IEnumerator Operator_AimState_TracksReloadAndUnmannedState()
        {
            CannonModule cannon = MountCannon(Own(CannonSettings.Create(autoScatterRadius: 0f, reloadSeconds: 0.5f)));
            CannonOperator op = Operator(cannon, 1.5f);
            Physics.SyncTransforms();
            cannon.SetArmed(true);
            Assert.IsTrue(op.TryMan(cannon));
            op.RefreshAim();
            Assert.AreEqual(CannonAimState.NoTarget, op.AimState, "조준 카메라가 없으면 겨눈 곳이 없다.");

            Assert.IsTrue(op.FireAt(Ground(cannon, 20f)));
            op.RefreshAim();
            Assert.AreEqual(CannonAimState.Reloading, op.AimState);
            for (int i = 0; i < 60 && !cannon.Reload.IsReady; i++) yield return null;
            yield return null;
            Assert.AreNotEqual(CannonAimState.Reloading, op.AimState, "장전이 끝나면 재장전 표시가 풀린다.");

            // 받침이 다시 자세를 잡으면(내려앉기 등) 그 자세가 새 설치 방향이 된다.
            cannon.SetSwivel(40f, true);
            Quaternion reset = cannon.transform.rotation * Quaternion.Euler(0f, 90f, 0f);
            cannon.transform.rotation = reset;
            Assert.AreEqual(0f, cannon.Swivel);
            Assert.AreEqual(0f, Quaternion.Angle(reset, cannon.transform.rotation), 0.01f);

            op.Leave();
            Assert.AreEqual(CannonAimState.NoTarget, op.AimState);
            yield return WaitForShells();
        }

        [UnityTest]
        public IEnumerator Operator_LeavesAutomatically_WhenBattleEndsOrCannonIsDestroyed()
        {
            CannonModule cannon = MountCannon(Own(CannonSettings.Create(autoScatterRadius: 0f)));
            CannonOperator op = Operator(cannon, 1.5f);
            Physics.SyncTransforms();
            cannon.SetArmed(true);
            Assert.IsTrue(op.TryMan(cannon));
            cannon.SetArmed(false);
            yield return null;
            Assert.IsNull(op.Manned, "전투가 끝나 무장이 풀리면 내린다.");
            Assert.IsFalse(cannon.IsManned);

            cannon.SetArmed(true);
            Assert.IsTrue(op.TryMan(cannon));
            cannon.Weapon.ReceiveDamage(cannon.Weapon.Health.Current);
            Assert.IsTrue(ReferenceEquals(op.Manned, null), "대포가 부서지는 순간 내린다.");
            yield return null;
            Assert.IsTrue(ReferenceEquals(op.Manned, null));
        }

        [UnityTest]
        public IEnumerator Blast_DamagesEveryEnemyInRadius_WithDistanceFalloff()
        {
            CannonSettings settings = Own(CannonSettings.Create(autoScatterRadius: 0f));
            CannonModule cannon = MountCannon(settings);
            CharacterActor center = Dummy(Ground(cannon, 20f), CombatTeam.Hostile);
            CharacterActor edge = Dummy(Ground(cannon, 20f, 2.4f), CombatTeam.Hostile);
            Physics.SyncTransforms();
            var shell = new GameObject("ManualShell").AddComponent<CannonShell>();
            SceneManager.MoveGameObjectToScene(shell.gameObject, scene);
            Vector3 impact = center.transform.position + Vector3.up * 0.5f;
            shell.Launch(impact + Vector3.up * 5f, Vector3.down, settings, CombatTeam.Allied);
            int exploded = 0;
            shell.Exploded += (_, _) => exploded++;
            shell.Explode(impact);
            shell.Explode(impact);
            Assert.AreEqual(1, exploded, "한 번만 터진다.");
            float defense = center.Stats.GetValue(StatType.Defense);
            Assert.AreEqual(center.Health.Maximum - DamageRules.Calculate(40f, defense, 1f), center.Health.Current, 0.01f);
            float edgeDistance = Vector3.Distance(edge.Body.ClosestPoint(impact), impact);
            float edgeDamage = CannonBlast.Damage(40f, edgeDistance, 3f, 0.5f, defense, 1f);
            Assert.Greater(edgeDamage, 0f);
            Assert.AreEqual(edge.Health.Maximum - edgeDamage, edge.Health.Current, 0.01f, "가장자리일수록 덜 다친다.");
            yield return null;
        }
    }
}
