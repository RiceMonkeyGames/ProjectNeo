using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Siege;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>Game 씬 성벽 맨 위 자동 성가퀴(SIEGE-10): 바깥 면, 사다리 빈틈, 쌓기, 방어 시설 업그레이드, 아군만 통과.</summary>
    public class GameBattlementPlayTests
    {
        const int North = 1 << 0;
        static int cleanup;
        GameBattleController battle;
        ConstructionWorld world;
        CastleBattlements battlements;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
            world = Object.FindAnyObjectByType<ConstructionWorld>();
            battlements = world.GetComponent<CastleBattlements>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameBattlementCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        // 북쪽 성벽 가운데 줄 가운데쯤의 사다리 없는 칸.
        PlacedStructure NorthWallWithoutLadder()
        {
            int north = world.Structures.Where(p => p.Placement.Definition.Kind == BuildPieceKind.Wall).Max(p => p.Placement.Cell.y);
            return world.Structures
                .Where(p => p.Placement.Definition.Kind == BuildPieceKind.Wall && p.Placement.Cell.y == north && p.Placement.Level == 0
                    && battle.Ladders.All(l => l.Cell != p.Placement.Cell))
                .OrderBy(p => p.Placement.Cell.x).Skip(3).First();
        }

        static Transform Battlement(PlacedStructure structure) => structure.transform.Find("Battlement");

        [UnityTest]
        public IEnumerator DefaultCastle_HasParapetsOnOuterFacesOnly_WithShotCoverForAlliesOnly()
        {
            Assert.IsNotNull(battlements, "Game 씬 성에 자동 성가퀴가 붙어 있어야 한다.");
            yield return null;
            Assert.AreEqual(BattlementStyle.Parapet, battlements.Style, "새 런은 방어 시설 1레벨 = 낮은 난간이다.");
            Assert.Greater(battlements.BuiltCount, 100, "성벽 고리의 맨 위 칸 대부분에 선다.");

            PlacedStructure wall = NorthWallWithoutLadder();
            Assert.AreEqual(North, battlements.FacesAt(wall.Placement.Cell), "북쪽 성벽은 바깥(북) 면에만 선다.");
            Transform root = Battlement(wall);
            Assert.IsNotNull(root);
            Collider[] parts = root.GetComponentsInChildren<Collider>();
            Assert.AreEqual(1, parts.Length, "난간은 면 하나에 한 덩어리다.");
            Assert.IsTrue(parts.All(part => part.name == "Parapet"));
            Assert.IsTrue(ShotCover.Passes(parts[0], CombatTeam.Allied), "아군 사격은 통과한다.");
            Assert.IsFalse(ShotCover.Passes(parts[0], CombatTeam.Hostile), "적 사격은 막힌다.");
            Assert.AreEqual(battlements.Settings.ParapetHeight, parts[0].bounds.size.y, 0.01f);
            float top = wall.Placement.Elevation + wall.Placement.Definition.Height;
            Assert.AreEqual(world.transform.TransformPoint(new Vector3(0f, top, 0f)).y, parts[0].bounds.min.y, 0.01f,
                "성벽 위 걷는 면에 놓인다.");

            // 성 밖 땅에서 난간 아래쪽을 노리면 난간에 맞는다.
            Vector3 target = parts[0].bounds.center;
            Vector3 origin = target + world.transform.forward * 20f;
            origin.y = world.transform.position.y + 1f;
            Assert.IsTrue(Physics.Raycast(origin, (target - origin).normalized, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore));
            Assert.IsNotNull(hit.collider.GetComponent<ShotCover>(), "바깥에서 쏜 선이 성가퀴에 먼저 닿는다: " + hit.collider.name);
        }

        [UnityTest]
        public IEnumerator Ladders_RestOnTheCrenelSill_AndThePatternContinues()
        {
            yield return null;
            SiegeLadder ladder = battle.Ladders.First(l => l.HasWall);
            PlacedStructure wall = world.Structures.First(p => world.Layout.Top(ladder.Cell) == p.Placement);
            float top = world.transform.TransformPoint(new Vector3(0f, wall.Placement.Elevation + wall.Placement.Definition.Height, 0f)).y;
            Transform root = Battlement(wall);
            Assert.IsNotNull(root, "사다리 칸도 다른 칸과 똑같이 성가퀴가 선다.");
            Assert.AreEqual(1, root.GetComponentsInChildren<Collider>().Length, "난간은 끊기지 않은 한 덩어리다.");
            Assert.AreEqual(top + battlements.Settings.ParapetHeight, ladder.EdgePoint.y, 0.01f, "사다리 꼭대기는 난간 높이에 걸린다.");

            battlements.SetStyle(BattlementStyle.Crenellated);
            root = Battlement(wall);
            Collider[] parts = root.GetComponentsInChildren<Collider>();
            Assert.AreEqual(1, parts.Count(part => part.name == "Parapet"), "받침은 사다리 자리에서도 이어진다.");
            Assert.AreEqual(2, parts.Count(part => part.name == "Merlon"));
            Assert.AreEqual(top + battlements.Settings.ParapetHeight, ladder.EdgePoint.y, 0.01f, "사다리는 빈틈의 받침에 걸린다.");
            Vector3 edge = ladder.EdgePoint;
            foreach (Collider merlon in parts.Where(part => part.name == "Merlon"))
            {
                Vector3 closest = merlon.ClosestPoint(new Vector3(edge.x, merlon.bounds.center.y, edge.z));
                Vector3 lateral = Vector3.ProjectOnPlane(closest - edge, ladder.OutwardWorld);
                lateral.y = 0f;
                Assert.Greater(lateral.magnitude, 0.35f, "사다리 폭(0.7m) 안에 솟은 부분이 없다.");
            }
        }

        [UnityTest]
        public IEnumerator StackingAWall_MovesTheBattlementToTheNewTop()
        {
            yield return null;
            PlacedStructure lower = NorthWallWithoutLadder();
            Assert.IsNotNull(Battlement(lower));
            Assert.IsTrue(world.TryPlace(lower.Placement.Definition, lower.Placement.Cell, 0, out PlacedStructure upper));
            yield return null;
            Assert.IsNull(Battlement(lower), "덮인 아래 칸의 성가퀴는 사라진다.");
            Assert.IsNotNull(Battlement(upper), "새 맨 위 칸에 성가퀴가 선다.");
            Assert.AreEqual(North, battlements.FacesAt(upper.Placement.Cell));
            Assert.IsTrue(world.TryDismantle(upper));
            yield return null;
            Assert.IsNotNull(Battlement(lower), "위 칸을 철거하면 다시 아래 칸에 선다.");
        }

        CharacterActor Unit(string name, CombatTeam team, Vector3 position, AttackKind kind)
        {
            var definition = SoldierDefinition.Create(name, new Cost(0),
                new StatValue(StatType.MaxHealth, 100f), new StatValue(StatType.Attack, 10f),
                new StatValue(StatType.Defense, 0f), new StatValue(StatType.AttackSpeed, 1f),
                new StatValue(StatType.AttackRange, 30f), new StatValue(StatType.MoveSpeed, 3.5f));
            var profile = CombatProfile.Create(0.05f, 70f, 0.4f, 2f, kind);
            var root = new GameObject(name);
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, world.gameObject.scene);
            root.transform.position = position;
            var body = root.AddComponent<CapsuleCollider>();
            body.radius = 0.4f; body.height = 2f; body.center = Vector3.up;
            var actor = root.AddComponent<CharacterActor>();
            actor.Configure(definition, profile, team);
            root.SetActive(true);
            Physics.SyncTransforms();
            return actor;
        }

        // 북쪽 성벽 칸 위(바깥 가장자리에서 back만큼 안쪽, 칸 왼쪽 끝에서 along만큼)의 걷는 면 위 점.
        Vector3 WallTop(PlacedStructure wall, float along, float back)
        {
            float size = world.Grid.CellSize;
            float top = wall.Placement.Elevation + wall.Placement.Definition.Height;
            return world.transform.TransformPoint(new Vector3(wall.Placement.Cell.x * size + along, top,
                (wall.Placement.Cell.y + 1) * size - back));
        }

        [UnityTest]
        public IEnumerator EnemyArchers_ShootTheUpperBodyOverTheParapet_ButNotThroughMerlons()
        {
            yield return null;
            PlacedStructure wall = NorthWallWithoutLadder();
            float size = world.Grid.CellSize;
            // 걷는 면 가운데에 선 수비병. 몸 가운데(1m)는 0.8m 난간에 가려지고 상체는 보인다.
            CharacterActor defender = Unit("Defender", CombatTeam.Allied, WallTop(wall, size * 0.5f, size * 0.5f), AttackKind.Melee);
            Vector3 ground = WallTop(wall, size * 0.5f, -12f);
            ground.y = world.transform.position.y;
            CharacterActor archer = Unit("EnemyArcher", CombatTeam.Hostile, ground, AttackKind.Projectile);
            yield return null;

            Assert.IsTrue(CombatPhysics.TryGetShotPoint(archer, defender, out Vector3 point), "상체가 보이면 쏠 수 있다.");
            Assert.Greater(point.y, defender.AimPoint.y + 0.5f, "몸 가운데 대신 위쪽 몸통을 겨눈다.");
            Assert.IsTrue(CombatPhysics.HasClearShot(defender, archer), "수비병은 자기 난간을 넘어 쏜다.");

            // 성가퀴의 솟은 부분 바로 뒤에서는 온몸이 가려진다.
            battlements.SetStyle(BattlementStyle.Crenellated);
            Physics.SyncTransforms();
            Collider merlon = Battlement(wall).GetComponentsInChildren<Collider>().First(part => part.name == "Merlon");
            Vector3 behind = merlon.bounds.center - world.transform.forward * 0.8f;
            behind.y = defender.transform.position.y;
            defender.transform.position = behind;
            Vector3 front = merlon.bounds.center + world.transform.forward * 12f;
            front.y = archer.transform.position.y;
            archer.transform.position = front;
            Physics.SyncTransforms();
            Assert.IsFalse(CombatPhysics.HasClearShot(archer, defender), "솟은 부분 뒤의 수비병은 맞지 않는다.");
            Assert.IsFalse(CombatPhysics.HasClearShotFrom(front, archer, defender), "정면 자리에서는 가려진다.");

            // 막힘 방지: 성벽을 따라 옆으로 비켜선 자리(기본 3m 간격)에서는 옆 빈틈 너머로 보인다.
            var settings = battle.Settings.Siege;
            Vector3 along = world.transform.right;
            bool found = false;
            for (int step = 1; step <= settings.ArcherSideStepCount && !found; step++)
                foreach (int side in new[] { -1, 1 })
                    found |= CombatPhysics.HasClearShotFrom(front + along * (side * step * settings.ArcherSideStep), archer, defender);
            Assert.IsTrue(found, "옆으로 비켜선 후보 자리 중 하나에서는 쏠 수 있다.");

            Object.Destroy(defender.gameObject);
            Object.Destroy(archer.gameObject);
        }

        [UnityTest]
        public IEnumerator CannonCell_BecomesAGunPort_WithOnlyTheSill()
        {
            yield return null;
            battlements.SetStyle(BattlementStyle.Crenellated);
            PlacedStructure support = NorthWallWithoutLadder();
            var economy = Object.FindAnyObjectByType<GameBuildEconomy>();
            SiegeWeaponDefinition cannon = economy.Table.Weapons.Select(entry => entry.weapon)
                .First(definition => definition != null && definition.Id == "cannon");
            Assert.AreEqual(MountIssue.None, world.CheckMount(cannon, support, "top"));
            Assert.AreEqual(2, Battlement(support).GetComponentsInChildren<Collider>().Count(part => part.name == "Merlon"));

            Assert.IsTrue(world.TryMount(cannon, support, "top", out PlacedSiegeWeapon weapon));
            Assert.IsTrue(battlements.IsGunPortAt(support.Placement.Cell));
            Collider[] parts = Battlement(support).GetComponentsInChildren<Collider>();
            Assert.AreEqual(0, parts.Count(part => part.name == "Merlon"), "대포 칸은 솟은 부분이 없다.");
            Assert.AreEqual(1, parts.Count(part => part.name == "Parapet"), "받침(포구 아래)은 남는다.");
            Vector2Int neighbour = support.Placement.Cell + Vector2Int.right;
            PlacedStructure next = world.Structures.First(p => world.Layout.Top(neighbour) == p.Placement);
            if (!battlements.IsGunPortAt(neighbour))
                Assert.AreEqual(2, Battlement(next).GetComponentsInChildren<Collider>().Count(part => part.name == "Merlon"),
                    "옆 칸은 그대로 성가퀴다.");

            Assert.IsTrue(world.RemoveWeapon(weapon));
            Assert.IsFalse(battlements.IsGunPortAt(support.Placement.Cell));
            Assert.AreEqual(2, Battlement(support).GetComponentsInChildren<Collider>().Count(part => part.name == "Merlon"),
                "대포를 치우면 성가퀴가 돌아온다.");
        }

        [UnityTest]
        public IEnumerator DefenseUpgrade_SwitchesEveryWallToCrenellations()
        {
            var link = Object.FindAnyObjectByType<GameBattlements>();
            Assert.IsNotNull(link, "Game 씬에 방어 시설 레벨 연결이 있어야 한다.");
            FacilityState defense = Object.FindAnyObjectByType<GameFacilities>().Facility(FacilityType.Defense);
            Assert.IsNotNull(defense);
            Assert.AreEqual(BattlementStyle.Parapet, defense.Current.battlementStyle);

            // 건설 모드 패널의 업그레이드 버튼: 비용 표시, 골드가 모자라면 실패, 넉넉하면 2레벨(최대)로 오른다.
            var state = battle.RunLink.State;
            yield return null;
            int gold = state.Wallet.Gold;
            if (gold >= defense.NextUpgradeCost.gold) Assert.IsTrue(state.Wallet.TrySpend(new Cost(gold)));
            Assert.IsFalse(link.TryUpgrade(), "골드가 모자라면 오르지 않는다.");
            state.Wallet.AddGold(1000);
            Assert.IsTrue(link.TryUpgrade());
            Assert.IsTrue(defense.IsMaxLevel, "방어 시설은 2레벨까지다.");
            Assert.IsFalse(link.TryUpgrade(), "최대 레벨에서는 더 오르지 않는다.");
            Assert.AreEqual(1000 - 60, state.Wallet.Gold, "업그레이드 비용 60골드를 낸다(임시 값).");
            yield return null;
            Assert.AreEqual(BattlementStyle.Crenellated, battlements.Style);
            Transform root = Battlement(NorthWallWithoutLadder());
            Assert.IsNotNull(root);
            Collider[] parts = root.GetComponentsInChildren<Collider>();
            Assert.AreEqual(1, parts.Count(part => part.name == "Parapet"), "빈틈 아래 받침");
            Assert.AreEqual(2, parts.Count(part => part.name == "Merlon"), "솟은 부분 두 개, 가운데와 양 끝은 빈틈");
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            Assert.IsFalse(world.RebuildPending, "모양이 바뀌면 길찾기 메시를 다시 만든다.");
        }
    }
}
