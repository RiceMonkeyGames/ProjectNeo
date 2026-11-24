using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>SIEGE-16 비행 적의 대포 노리기와 같은 대상 둘레 자리 나누기. 실제 SiegePrototype 씬의 보루 위 대포로 확인한다.</summary>
    public class FlyerSiegePlayTests
    {
        Scene scene;
        ConstructionWorld world;
        SiegeDirector director;
        CharacterActor player;
        GameObject template;
        SiegeWeaponDefinition cannonDefinition;
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
            world = Find<ConstructionWorld>(); director = Find<SiegeDirector>();
            yield return null;
            director.enabled = false;
            Find<SquadCommandInput>().enabled = false;
            var input = Find<PlayerInputController>();
            input.enabled = false;
            player = input.GetComponent<CharacterActor>();
            // 아군은 움직이지 않는 과녁으로 두고, 필요한 시험만 다시 켠다.
            foreach (UnitCombatAI member in Allies())
            {
                member.enabled = false;
                member.GetComponent<NavMeshAgent>().enabled = false;
                member.gameObject.SetActive(false);
            }
            template = new GameObject("TestCannonTemplate");
            template.SetActive(false);
            SceneManager.MoveGameObjectToScene(template, scene);
            template.AddComponent<PlacedSiegeWeapon>();
            cannonDefinition = SiegeWeaponDefinition.Create("test_cannon", "test_cannon", new Vector3(1.2f, 1.1f, 1.8f),
                template, 150f);
            yield return NavigationReady();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (cannonDefinition != null) Object.Destroy(cannonDefinition);
            Time.captureDeltaTime = previousCapture;
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
        }

        T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
        System.Collections.Generic.IEnumerable<UnitCombatAI> Allies() =>
            Find<SquadCommandCenter>().Squads.SelectMany(s => s.Members).Where(m => m != null);
        static float Horizontal(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        IEnumerator NavigationReady()
        {
            for (int frame = 0; frame < 600; frame++)
            {
                yield return null;
                if (!world.IsRebuilding && !world.RebuildPending) yield break;
            }
            Assert.Fail("Navigation rebuild did not finish.");
        }

        // 바깥 줄 성벽 옆에 보루를 짓고 대포를 올린다(SIEGE-11: 수성 무기는 보루와 탑 지붕에만 놓는다).
        IEnumerator MountCannon(System.Action<PlacedSiegeWeapon> done)
        {
            PlacedStructure bastion = BastionTestSupport.PlaceInFrontOfOutermostWall(world, BastionTestSupport.Definition(scene),
                Vector2Int.up);
            Assert.IsNotNull(bastion, "바깥 줄 성벽 앞에 보루를 지을 수 있어야 한다.");
            yield return NavigationReady();
            Assert.IsTrue(world.TryMount(cannonDefinition, bastion, "top", out PlacedSiegeWeapon weapon));
            weapon.gameObject.SetActive(true);
            done(weapon);
        }

        Vector3 Outside(Vector3 from, float distance)
        {
            Vector3 outward = world.transform.TransformDirection(Vector3.forward);
            Vector3 point = from + outward * distance;
            point.y = world.transform.position.y;
            return point;
        }

        // 성벽 위에 서 있는 아군 자리. 바닥층 성벽 윗면 가운데.
        Vector3 WallTop(out PlacedStructure wall)
        {
            wall = world.Structures.First(p => p.Placement.Definition.Kind == BuildPieceKind.Wall
                && p.Placement.Level == 0 && !world.Layout.IsCovered(p.Placement));
            return wall.transform.position + Vector3.up * wall.Placement.Definition.Height;
        }

        FlyerBrain Spawn(Vector3 at, GameObject prefab = null)
        {
            if (prefab == null)
                prefab = director.Settings.EnemyGroups.Select(g => g.unitPrefab)
                    .First(p => p != null && p.GetComponent<FlyerBrain>() != null);
            GameObject unit = Object.Instantiate(prefab, at, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(unit, scene);
            var brain = unit.GetComponent<FlyerBrain>();
            brain.Bind(world, new SiegeLadder[0], Vector2Int.up, director.Settings);
            unit.SetActive(true);
            return brain;
        }

        CharacterActor PlaceAlly(Vector3 at)
        {
            UnitCombatAI member = Allies().First(m => !m.gameObject.activeSelf);
            member.transform.position = at;
            member.gameObject.SetActive(true);
            Physics.SyncTransforms();
            return member.Actor;
        }

        [UnityTest]
        public IEnumerator NoWallTopAlly_FlyerStrikesCannon_BeforeCourtyardPlayer()
        {
            PlacedSiegeWeapon weapon = null;
            yield return MountCannon(w => weapon = w);
            // 플레이어는 성 안 땅에 둔다(성벽 위 아군 아님).
            player.transform.position = world.transform.TransformPoint(new Vector3(world.Grid.CellSize * 2.5f, 0f, world.Grid.CellSize * 2f));
            Physics.SyncTransforms();
            FlyerBrain flyer = Spawn(Outside(weapon.transform.position, 12f));
            for (int frame = 0; frame < 60 && flyer.WeaponTarget != weapon; frame++) yield return null;
            Assert.AreSame(weapon, flyer.WeaponTarget, "성벽 위 아군이 없으면 안마당 아군보다 대포를 먼저 노린다.");
            Assert.IsNull(flyer.Target);
            Assert.IsTrue(flyer.IsAssaulting);
            for (int frame = 0; frame < 900 && weapon.Health.Current >= weapon.Health.Maximum; frame++) yield return null;
            Assert.Less(weapon.Health.Current, weapon.Health.Maximum, "대포 옆에 떠서 친다.");

            weapon.ReceiveDamage(weapon.Health.Current + 1f);
            for (int frame = 0; frame < 30 && !ReferenceEquals(flyer.WeaponTarget, null); frame++) yield return null;
            Assert.IsNull(flyer.WeaponTarget, "부서진 대포는 바로 놓는다.");
            for (int frame = 0; frame < 60 && flyer.Target != player; frame++) yield return null;
            Assert.AreSame(player, flyer.Target, "대포가 없으면 남은 아군을 노린다.");
        }

        [UnityTest]
        public IEnumerator WallTopAlly_IsPreferredOverCannon()
        {
            PlacedSiegeWeapon weapon = null;
            yield return MountCannon(w => weapon = w);
            player.gameObject.SetActive(false);
            CharacterActor onWall = PlaceAlly(WallTop(out _));
            FlyerBrain flyer = Spawn(Outside(weapon.transform.position, 6f));
            for (int frame = 0; frame < 60 && flyer.Target == null; frame++) yield return null;
            Assert.AreSame(onWall, flyer.Target, "대포가 더 가까워도 성벽 위 아군을 먼저 노린다.");
            Assert.IsNull(flyer.WeaponTarget);
            for (int frame = 0; frame < 300; frame++) yield return null;
            Assert.AreEqual(weapon.Health.Maximum, weapon.Health.Current);
        }

        [UnityTest]
        public IEnumerator TwoFlyers_OnSameTarget_HoverApart()
        {
            player.gameObject.SetActive(false);
            Vector3 top = WallTop(out _);
            CharacterActor onWall = PlaceAlly(top);
            Vector3 start = Outside(top, 12f);
            FlyerBrain first = Spawn(start);
            FlyerBrain second = Spawn(start + Vector3.right * 0.1f);
            for (int frame = 0; frame < 60 && !(first.HasHoverPoint && second.HasHoverPoint); frame++) yield return null;
            Assert.AreSame(onWall, first.Target);
            Assert.AreSame(onWall, second.Target);
            Assert.GreaterOrEqual(Horizontal(first.Destination, second.Destination),
                first.Settings.HoverSeparation - 0.01f, "같은 쪽에서 다가와도 다른 자리에 뜬다.");
            // 둘 다 자리에 닿을 때까지 기다린다. 대상이 쓰러지면 둘 다 같은 대기 자리로 가므로 그 전에 잰다.
            for (int frame = 0; frame < 600 && onWall.Health.Current > 0f
                && (Horizontal(first.transform.position, first.Destination) > 0.2f
                    || Horizontal(second.transform.position, second.Destination) > 0.2f); frame++) yield return null;
            Assert.IsTrue(first.HasHoverPoint && second.HasHoverPoint, "둘 다 같은 대상을 쫓는 중이어야 한다.");
            Assert.Greater(Horizontal(first.transform.position, second.transform.position),
                first.Actor.Profile.Radius * 2f - 0.05f, "두 비행 적 몸통이 겹치지 않는다.");
        }

        [UnityTest]
        public IEnumerator RoofedWallTopAlly_IsSkipped_UntilRoofIsGone()
        {
            Vector3 top = WallTop(out _);
            CharacterActor sheltered = PlaceAlly(top);
            // 계단방·탑 안처럼 머리 위가 막힌 자리. 머리 위 1m에 얇은 판을 둔다.
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(roof, scene);
            roof.transform.position = top + Vector3.up * (sheltered.Profile.Height + 1f);
            roof.transform.localScale = new Vector3(4f, 0.2f, 4f);
            player.transform.position = world.transform.TransformPoint(new Vector3(world.Grid.CellSize * 2.5f, 0f, world.Grid.CellSize * 2f));
            Physics.SyncTransforms();
            FlyerBrain flyer = Spawn(Outside(top, 12f));
            Assert.IsTrue(flyer.IsSheltered(sheltered));
            Assert.IsFalse(flyer.IsSheltered(player));
            for (int frame = 0; frame < 60 && flyer.Target == null; frame++) yield return null;
            Assert.AreSame(player, flyer.Target, "지붕 아래 아군은 성벽 위여도 노리지 않는다.");

            Object.Destroy(roof);
            yield return null;
            Physics.SyncTransforms();
            Assert.IsFalse(flyer.IsSheltered(sheltered));
            float interval = director.Settings.AttackerDecisionInterval;
            for (int frame = 0; frame < Mathf.CeilToInt(interval * 30f) + 30 && flyer.Target != sheltered; frame++) yield return null;
            Assert.AreSame(sheltered, flyer.Target, "지붕에서 나오면 다시 성벽 위 아군을 먼저 노린다.");
        }

        GameObject Roof(Vector3 feet, CharacterActor under)
        {
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(roof, scene);
            roof.transform.position = feet + Vector3.up * (under.Profile.Height + 1f);
            roof.transform.localScale = new Vector3(4f, 0.2f, 4f);
            Physics.SyncTransforms();
            return roof;
        }

        [UnityTest]
        public IEnumerator AllAlliesSheltered_LiveCannonKeepsFlyerReachable_UntilDestroyed()
        {
            PlacedSiegeWeapon weapon = null;
            yield return MountCannon(w => weapon = w);
            // 살아 있는 아군은 지붕 아래 플레이어 하나뿐이다.
            Vector3 feet = world.transform.TransformPoint(new Vector3(world.Grid.CellSize * 2.5f, 0f, world.Grid.CellSize * 2f));
            player.transform.position = feet;
            Roof(feet, player);
            FlyerBrain flyer = Spawn(Outside(weapon.transform.position, 12f));
            Assert.IsTrue(flyer.IsSheltered(player));
            for (int frame = 0; frame < 60 && flyer.WeaponTarget != weapon; frame++) yield return null;
            Assert.AreSame(weapon, flyer.WeaponTarget, "아군이 모두 지붕 아래면 대포를 노린다.");
            Assert.IsTrue(flyer.CanReachAnyEnemy(), "노릴 대포가 남았으면 후퇴 판단에서 닿는 것으로 친다(SIEGE-32).");

            weapon.ReceiveDamage(weapon.Health.Current + 1f);
            yield return null;
            Assert.IsFalse(flyer.CanReachAnyEnemy(), "대포도 없고 아군이 모두 지붕 아래면 닿지 않는다.");
        }

#if UNITY_EDITOR
        const string HighArcherPath = "Assets/_Project/Prefabs/SiegePrototype/enemy_flyer_high_archer.prefab";

        GameObject HighArcherPrefab()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HighArcherPath);
            Assert.IsNotNull(prefab, "높이 나는 궁수 비행 적 프리팹이 있어야 한다.");
            Assert.AreEqual(AttackKind.Projectile, prefab.GetComponent<CharacterActor>().Profile.AttackKind);
            return prefab;
        }

        [UnityTest]
        public IEnumerator RangedFlyer_StrikesCannon_WithVisibleProjectile_DamageOnArrival()
        {
            PlacedSiegeWeapon weapon = null;
            yield return MountCannon(w => weapon = w);
            player.gameObject.SetActive(false);
            FlyerBrain flyer = Spawn(Outside(weapon.transform.position, 14f), HighArcherPrefab());
            for (int frame = 0; frame < 60 && flyer.WeaponTarget != weapon; frame++) yield return null;
            Assert.AreSame(weapon, flyer.WeaponTarget);
            bool sawProjectile = false;
            for (int frame = 0; frame < 900 && weapon.Health.Current >= weapon.Health.Maximum; frame++)
            {
                if (scene.GetRootGameObjects().Any(r => r.GetComponent<CombatProjectile>() != null)) sawProjectile = true;
                yield return null;
            }
            Assert.Less(weapon.Health.Current, weapon.Health.Maximum, "원거리 비행 적도 대포에 피해를 준다.");
            Assert.IsTrue(sawProjectile, "대포를 칠 때 눈에 보이는 발사체를 날리고, 피해는 그것이 닿은 뒤에 들어간다(SIEGE-32).");
        }

        [UnityTest]
        public IEnumerator RangedFlyer_OccludedHoverPoint_IsRecomputedWithClearShot()
        {
            player.gameObject.SetActive(false);
            CharacterActor onWall = PlaceAlly(WallTop(out _));
            onWall.Stats.SetBase(ProjectNeo.Stats.StatType.MaxHealth, 100000f);
            onWall.Health.RestoreFull();
            FlyerBrain flyer = Spawn(Outside(onWall.transform.position, 14f), HighArcherPrefab());
            for (int frame = 0; frame < 600 && !(flyer.HasHoverPoint
                && Horizontal(flyer.transform.position, flyer.Destination) < 0.2f); frame++) yield return null;
            Assert.AreSame(onWall, flyer.Target);
            Assert.IsTrue(flyer.HasHoverPoint, "공격 자리에 닿아야 한다.");
            Vector3 before = flyer.Destination;
            Assert.IsTrue(CombatPhysics.HasClearShot(flyer.Actor, onWall));

            // 공격 자리와 대상 사이 가운데에 기둥을 세워 사선을 막는다.
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(pillar, scene);
            Vector3 middle = (flyer.Actor.AimPoint + onWall.AimPoint) * 0.5f;
            pillar.transform.position = middle;
            pillar.transform.rotation = Quaternion.LookRotation(onWall.AimPoint - flyer.Actor.AimPoint);
            pillar.transform.localScale = new Vector3(0.8f, 4f, 0.2f);
            Physics.SyncTransforms();
            Assert.IsFalse(CombatPhysics.HasClearShotFrom(before, flyer.Actor, onWall), "기둥이 처음 자리의 사선을 막아야 한다.");

            float wait = flyer.Settings.SightlineRecheckInterval + 0.5f;
            for (int frame = 0; frame < Mathf.CeilToInt(wait * 30f) && Horizontal(flyer.Destination, before) < 0.05f;
                 frame++) yield return null;
            Assert.AreSame(onWall, flyer.Target, "사선이 막혀도 대상은 그대로다.");
            Assert.Greater(Horizontal(flyer.Destination, before), 0.05f, "가려진 공격 자리는 다시 구한다(SIEGE-32).");
            Assert.IsTrue(CombatPhysics.HasClearShotFrom(flyer.Destination, flyer.Actor, onWall),
                "새 공격 자리에서는 대상이 보인다.");
        }
#endif
    }
}
