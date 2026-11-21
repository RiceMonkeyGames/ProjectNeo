using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Stats;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>맞은 자리에 박혀 남는 화살(ART-05).</summary>
    public class StuckArrowPlayTests
    {
        const float Speed = 300f;
        readonly List<Object> assets = new List<Object>();
        readonly Vector3 origin = new Vector3(-1000f, 0f, 1000f);
        Scene scene;
        Scene originalScene;
        float previousCaptureDelta;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("StuckArrowTest");
            SceneManager.SetActiveScene(scene);
            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = previousCaptureDelta;
            SceneManager.SetActiveScene(originalScene);
            yield return SceneManager.UnloadSceneAsync(scene);
            foreach (Object asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
            yield return null;
        }

        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        GameObject Template(StuckArrowSettings settings)
        {
            var template = new GameObject("ArrowTemplate");
            template.AddComponent<CombatProjectile>();
            template.AddComponent<StuckArrow>().Configure(Track(settings));
            return template;
        }

        GameObject Box(string name, Vector3 position, Vector3 scale)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = origin + position;
            box.transform.localScale = scale;
            return box;
        }

        CharacterActor Unit(string name, CombatTeam team, Vector3 position, GameObject projectile, float health = 100f)
        {
            var definition = Track(SoldierDefinition.Create(name, new Cost(0),
                new StatValue(StatType.MaxHealth, health), new StatValue(StatType.Attack, 30f),
                new StatValue(StatType.Defense, 0f), new StatValue(StatType.AttackSpeed, 2f),
                new StatValue(StatType.AttackRange, 30f), new StatValue(StatType.MoveSpeed, 3.5f)));
            var profile = Track(CombatProfile.Create(0.05f, 70f, 0.4f, 2f, AttackKind.Projectile, projectile, Speed));
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.position = origin + position;
            var body = root.AddComponent<CapsuleCollider>();
            body.radius = profile.Radius;
            body.height = profile.Height;
            body.center = Vector3.up;
            root.AddComponent<CharacterActor>().Configure(definition, profile, team);
            root.SetActive(true);
            Physics.SyncTransforms();
            return root.GetComponent<CharacterActor>();
        }

        static StuckArrow Shoot(GameObject template, CharacterActor shooter, Vector3 direction, float attack = 30f)
        {
            var shot = Object.Instantiate(template, shooter.AimPoint, Quaternion.LookRotation(direction));
            shot.GetComponent<CombatProjectile>().Launch(shooter, attack, direction.normalized * Speed);
            return shot.GetComponent<StuckArrow>();
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator HitWall_StaysAtImpact_ThenDisappearsAfterLifetime()
        {
            GameObject template = Template(StuckArrowSettings.Create(lifetime: 0.5f));
            var archer = Unit("Archer", CombatTeam.Allied, Vector3.zero, template);
            GameObject wall = Box("Wall", new Vector3(0f, 1f, 6f), new Vector3(4f, 4f, 1f));
            Physics.SyncTransforms();
            StuckArrow arrow = Shoot(template, archer, Vector3.forward);
            yield return Frames(5);
            Assert.IsTrue(arrow != null, "벽에 맞은 화살은 사라지지 않아야 한다.");
            Assert.IsTrue(arrow.IsStuck);
            Assert.AreEqual(wall.transform, arrow.Anchor);
            float wallFace = origin.z + 5.5f;
            Assert.That(arrow.transform.position.z, Is.InRange(wallFace, wallFace + 0.3f), "화살촉이 벽면 안쪽에 조금 박혀야 한다.");
            Vector3 stuckAt = arrow.transform.position;
            yield return Frames(10);
            Assert.AreEqual(stuckAt, arrow.transform.position, "박힌 화살은 움직이지 않는다.");
            yield return Frames(30);
            Assert.IsTrue(arrow == null, "수명이 지나면 사라져야 한다.");
        }

        [UnityTest]
        public IEnumerator HitCharacter_DamagesAndFollowsBody_AndStaysOnCorpse()
        {
            GameObject template = Template(StuckArrowSettings.Create());
            var archer = Unit("Archer", CombatTeam.Allied, Vector3.zero, template);
            var enemy = Unit("Enemy", CombatTeam.Hostile, new Vector3(0f, 0f, 8f), template, health: 50f);
            StuckArrow arrow = Shoot(template, archer, Vector3.forward);
            yield return Frames(5);
            Assert.Less(enemy.Health.Current, 50f, "박혀도 피해는 그대로 준다.");
            Assert.IsTrue(arrow != null && arrow.IsStuck);
            Vector3 offset = arrow.transform.position - enemy.transform.position;
            enemy.transform.position += new Vector3(2f, 0f, 1f);
            yield return Frames(2);
            Assert.That(Vector3.Distance(enemy.transform.position + offset, arrow.transform.position), Is.LessThan(0.001f),
                "캐릭터에 박힌 화살은 몸을 따라 움직인다.");
            enemy.ReceiveDamage(1000f);
            yield return Frames(5);
            Assert.IsTrue(enemy.Health.IsDead);
            Assert.IsTrue(arrow != null, "죽은 뒤에도 시체에 남는다.");
            Object.Destroy(enemy.gameObject);
            yield return Frames(2);
            Assert.IsTrue(arrow == null, "시체가 사라지면 함께 사라진다.");
        }

        [UnityTest]
        public IEnumerator OverCap_RemovesOldestStuckArrow()
        {
            GameObject template = Template(StuckArrowSettings.Create(maxCount: 2));
            var archer = Unit("Archer", CombatTeam.Allied, Vector3.zero, template);
            Box("Wall", new Vector3(0f, 1f, 6f), new Vector3(8f, 4f, 1f));
            Physics.SyncTransforms();
            var arrows = new List<StuckArrow>();
            for (int i = 0; i < 3; i++)
            {
                arrows.Add(Shoot(template, archer, new Vector3((i - 1) * 0.2f, 0f, 1f)));
                yield return Frames(4);
            }
            Assert.IsTrue(arrows[0] == null, "상한을 넘으면 가장 먼저 박힌 화살부터 사라진다.");
            Assert.IsTrue(arrows[1] != null && arrows[2] != null);
        }

        [UnityTest]
        public IEnumerator HitFirstPersonPlayer_DoesNotStick()
        {
            GameObject template = Template(StuckArrowSettings.Create());
            var archer = Unit("Archer", CombatTeam.Hostile, Vector3.zero, template);
            var player = Unit("Player", CombatTeam.Allied, new Vector3(0f, 0f, 8f), template);
            new GameObject("Camera").AddComponent<Camera>().transform.SetParent(player.transform, false);
            StuckArrow arrow = Shoot(template, archer, Vector3.forward);
            yield return Frames(5);
            Assert.Less(player.Health.Current, 100f);
            Assert.IsTrue(arrow == null, "1인칭 플레이어 몸에는 박히지 않는다.");
        }
    }
}
