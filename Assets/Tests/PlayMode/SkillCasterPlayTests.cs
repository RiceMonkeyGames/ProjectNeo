using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Skills;
using ProjectNeo.Stats;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// COMBAT-06 2단계: 전투 중 스킬 시전(<see cref="SkillCaster"/>)의 대상 고르기, 효과 적용, 쿨다운, 시전 신호.
    /// 길찾기 없이 서 있는 유닛만 쓴다.
    /// </summary>
    public class SkillCasterPlayTests
    {
        readonly List<Object> assets = new List<Object>();
        readonly Vector3 origin = new Vector3(2000f, 0f, 2000f);
        Scene scene;
        Scene originalScene;
        float previousCaptureDelta;

        sealed class FakeContext : ISkillContext
        {
            public readonly Dictionary<CharacterActor, int> Squads = new Dictionary<CharacterActor, int>();
            public readonly List<(int squad, float amount)> MoraleCalls = new List<(int, float)>();

            public int SquadOf(CharacterActor actor) => actor != null && Squads.TryGetValue(actor, out int squad) ? squad : -1;

            public void CollectSquad(int squad, List<CharacterActor> into)
            {
                foreach (KeyValuePair<CharacterActor, int> pair in Squads)
                    if (pair.Value == squad && !into.Contains(pair.Key)) into.Add(pair.Key);
            }

            public float AddMorale(int squad, float amount)
            {
                MoraleCalls.Add((squad, amount));
                return amount;
            }
        }

        sealed class FakeAction : IInterruptibleAction
        {
            public ActionPoise InterruptPoise { get; set; }
            public int Interrupted;
            public void InterruptAction() => Interrupted++;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("SkillCasterTest");
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

        CharacterActor Unit(string name, CombatTeam team, Vector3 position, float attack = 20f, float health = 100f,
            float defense = 0f)
        {
            var definition = Track(SoldierDefinition.Create(name, new Cost(0),
                new StatValue(StatType.MaxHealth, health), new StatValue(StatType.Attack, attack),
                new StatValue(StatType.Defense, defense), new StatValue(StatType.AttackSpeed, 1f),
                new StatValue(StatType.AttackRange, 2f), new StatValue(StatType.MoveSpeed, 4f)));
            var profile = Track(CombatProfile.Create(0.1f, 70f, 0.4f, 2f, minimumDamage: 1f));
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.position = origin + position;
            var body = root.AddComponent<CapsuleCollider>();
            body.radius = profile.Radius;
            body.height = profile.Height;
            body.center = Vector3.up;
            var actor = root.AddComponent<CharacterActor>();
            actor.Configure(definition, profile, team);
            root.SetActive(true);
            return actor;
        }

        SkillCaster Caster(CharacterActor actor, SkillDefinition skill, ISkillContext context = null)
        {
            SkillCaster caster = SkillCaster.On(actor);
            caster.Setup(new[] { skill });
            caster.UseContext(context);
            return caster;
        }

        [UnityTest]
        public IEnumerator SelfArea_DamagesEnemiesInRadiusOnly_AndStartsCooldown()
        {
            SkillDefinition whirl = Track(SkillDefinition.Create("whirl", 5f, SkillTargetMode.Self, 0f, 3f,
                SkillEffect.Damage(10f, 1f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero, attack: 20f);
            CharacterActor ally = Unit("Ally", CombatTeam.Allied, new Vector3(1f, 0f, 0f));
            CharacterActor near = Unit("Near", CombatTeam.Hostile, new Vector3(2f, 0f, 0f), defense: 5f);
            CharacterActor far = Unit("Far", CombatTeam.Hostile, new Vector3(8f, 0f, 0f));
            yield return null;
            SkillCaster caster = Caster(hero, whirl);
            var casts = new List<SkillCastInfo>();
            caster.Cast += casts.Add;

            Assert.AreEqual(SkillCastResult.Cast, caster.TryCast(0, SkillAim.At(hero.transform.position)));
            Assert.AreEqual(100f - (10f + 20f - 5f), near.Health.Current, 1e-3f, "amount + attack x scale - defense");
            Assert.AreEqual(100f, far.Health.Current, "outside radius");
            Assert.AreEqual(100f, ally.Health.Current, "allies are not hit");
            Assert.AreEqual(100f, hero.Health.Current);
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(1, casts[0].AffectedCount);
            Assert.AreSame(whirl, casts[0].Skill);

            Assert.AreEqual(SkillCastResult.NotReady, caster.TryCast(0, SkillAim.At(hero.transform.position)));
            Assert.AreEqual(1, casts.Count);
            Assert.AreEqual(SkillCastResult.NoSkill, caster.TryCast(1, default));
        }

        [UnityTest]
        public IEnumerator Cooldown_RecoversOverTime_AndResetMakesReady()
        {
            SkillDefinition quick = Track(SkillDefinition.Create("quick", 0.5f, SkillTargetMode.Self, 0f, 0f,
                SkillEffect.Heal(1f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            yield return null;
            SkillCaster caster = Caster(hero, quick);
            Assert.AreEqual(SkillCastResult.Cast, caster.TryCast(0, default));
            Assert.IsFalse(caster.Slots.IsReady(0));
            for (int i = 0; i < 40; i++) yield return null;
            Assert.IsTrue(caster.Slots.IsReady(0), "0.5 s cooldown over ~0.66 s of frames");

            caster.TryCast(0, default);
            caster.ResetCooldowns();
            Assert.IsTrue(caster.Slots.IsReady(0));
        }

        [UnityTest]
        public IEnumerator AimPoint_HealsAlliesNearClampedPoint()
        {
            SkillDefinition heal = Track(SkillDefinition.Create("heal", 5f, SkillTargetMode.AimPoint, 6f, 2f,
                SkillEffect.Heal(30f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            CharacterActor ally = Unit("Ally", CombatTeam.Allied, new Vector3(6f, 0f, 0f));
            CharacterActor enemy = Unit("Enemy", CombatTeam.Hostile, new Vector3(6.5f, 0f, 0f));
            CharacterActor farAlly = Unit("FarAlly", CombatTeam.Allied, new Vector3(12f, 0f, 0f));
            yield return null;
            ally.Health.TakeDamage(50f);
            enemy.Health.TakeDamage(50f);
            farAlly.Health.TakeDamage(50f);
            SkillCaster caster = Caster(hero, heal);

            Assert.AreEqual(SkillCastResult.Cast, caster.TryCast(0, SkillAim.At(origin + new Vector3(12f, 0f, 0f))));
            Assert.AreEqual(80f, ally.Health.Current, 1e-3f, "aim pulled back to 6 m range");
            Assert.AreEqual(50f, enemy.Health.Current, "enemies are not healed");
            Assert.AreEqual(50f, farAlly.Health.Current, "clamped point is 6 m away from the far ally");
        }

        [UnityTest]
        public IEnumerator TargetUnit_ChecksSideAndRange_WithoutSpendingCooldown_ThenDebuffs()
        {
            SkillDefinition hamstring = Track(SkillDefinition.Create("hamstring", 5f, SkillTargetMode.TargetUnit, 3f, 0f,
                SkillEffect.Debuff(StatModifier.Percent(StatType.MoveSpeed, -0.5f), 0.5f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            CharacterActor ally = Unit("Ally", CombatTeam.Allied, new Vector3(1f, 0f, 0f));
            CharacterActor near = Unit("Near", CombatTeam.Hostile, new Vector3(2f, 0f, 0f));
            CharacterActor far = Unit("Far", CombatTeam.Hostile, new Vector3(10f, 0f, 0f));
            yield return null;
            SkillCaster caster = Caster(hero, hamstring);

            Assert.AreEqual(SkillCastResult.NoTarget, caster.TryCast(0, SkillAim.At(near.transform.position)));
            Assert.AreEqual(SkillCastResult.NoTarget, caster.TryCast(0, SkillAim.AtUnit(ally)));
            Assert.AreEqual(SkillCastResult.OutOfRange, caster.TryCast(0, SkillAim.AtUnit(far)));
            Assert.IsTrue(caster.Slots.IsReady(0), "failed casts keep the cooldown");

            Assert.AreEqual(SkillCastResult.Cast, caster.TryCast(0, SkillAim.AtUnit(near)));
            Assert.AreEqual(2f, near.Stats.GetValue(StatType.MoveSpeed), 1e-3f);
            Assert.AreEqual(4f, far.Stats.GetValue(StatType.MoveSpeed), 1e-3f);
            for (int i = 0; i < 40; i++) yield return null;
            Assert.AreEqual(4f, near.Stats.GetValue(StatType.MoveSpeed), 1e-3f, "debuff expired");
        }

        [UnityTest]
        public IEnumerator OwnSquad_BuffsSquadMembers_RecastRefreshesWithoutStacking()
        {
            SkillDefinition warCry = Track(SkillDefinition.Create("war_cry", 0f, SkillTargetMode.OwnSquad, 0f, 0f,
                SkillEffect.Buff(StatModifier.Percent(StatType.Attack, 0.5f), 1f)));
            CharacterActor captain = Unit("Captain", CombatTeam.Allied, Vector3.zero, attack: 20f);
            CharacterActor member = Unit("Member", CombatTeam.Allied, new Vector3(20f, 0f, 0f), attack: 10f);
            CharacterActor other = Unit("Other", CombatTeam.Allied, new Vector3(1f, 0f, 0f), attack: 10f);
            yield return null;
            var context = new FakeContext();
            context.Squads[captain] = 0;
            context.Squads[member] = 0;
            context.Squads[other] = 1;
            SkillCaster caster = Caster(captain, warCry, context);

            Assert.AreEqual(SkillCastResult.Cast, caster.TryCast(0, default));
            Assert.AreEqual(30f, captain.Stats.GetValue(StatType.Attack), 1e-3f);
            Assert.AreEqual(15f, member.Stats.GetValue(StatType.Attack), 1e-3f, "squad buff ignores distance");
            Assert.AreEqual(10f, other.Stats.GetValue(StatType.Attack), 1e-3f, "other squad untouched");

            Assert.AreEqual(SkillCastResult.Cast, caster.TryCast(0, default));
            Assert.AreEqual(30f, captain.Stats.GetValue(StatType.Attack), 1e-3f, "recast does not stack");
            Assert.AreEqual(1, captain.GetComponent<SkillStatusEffects>().ActiveCount);
            for (int i = 0; i < 70; i++) yield return null;
            Assert.AreEqual(20f, captain.Stats.GetValue(StatType.Attack), 1e-3f, "buff expired");
            Assert.AreEqual(10f, member.Stats.GetValue(StatType.Attack), 1e-3f);
        }

        [UnityTest]
        public IEnumerator OwnSquad_WithoutSquad_UsesAlliesInRadius_AndRaisesMoraleOncePerSquad()
        {
            SkillDefinition rally = Track(SkillDefinition.Create("rally", 0f, SkillTargetMode.OwnSquad, 0f, 5f,
                SkillEffect.MoraleGain(15f)));
            CharacterActor player = Unit("Player", CombatTeam.Allied, Vector3.zero);
            CharacterActor a1 = Unit("A1", CombatTeam.Allied, new Vector3(1f, 0f, 0f));
            CharacterActor a2 = Unit("A2", CombatTeam.Allied, new Vector3(2f, 0f, 0f));
            CharacterActor b1 = Unit("B1", CombatTeam.Allied, new Vector3(0f, 0f, 2f));
            CharacterActor farC = Unit("FarC", CombatTeam.Allied, new Vector3(20f, 0f, 0f));
            yield return null;
            var context = new FakeContext();
            context.Squads[a1] = 0;
            context.Squads[a2] = 0;
            context.Squads[b1] = 1;
            context.Squads[farC] = 2;
            SkillCaster caster = Caster(player, rally, context);

            Assert.AreEqual(SkillCastResult.Cast, caster.TryCast(0, default));
            CollectionAssert.AreEquivalent(new[] { (0, 15f), (1, 15f) }, context.MoraleCalls);
        }

        [UnityTest]
        public IEnumerator Stagger_InterruptsOnlyWeakPoise()
        {
            SkillDefinition slam = Track(SkillDefinition.Create("slam", 0f, SkillTargetMode.TargetUnit, 3f, 0f,
                SkillEffect.Stagger(40f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            CharacterActor enemy = Unit("Enemy", CombatTeam.Hostile, new Vector3(2f, 0f, 0f));
            yield return null;
            var weak = new FakeAction { InterruptPoise = ActionPoise.Interruptible(30f) };
            var strong = new FakeAction { InterruptPoise = ActionPoise.Interruptible(50f) };
            enemy.RegisterInterruptible(weak);
            enemy.RegisterInterruptible(strong);
            SkillCaster caster = Caster(hero, slam);

            Assert.AreEqual(SkillCastResult.Cast, caster.TryCast(0, SkillAim.AtUnit(enemy)));
            Assert.AreEqual(1, weak.Interrupted);
            Assert.AreEqual(0, strong.Interrupted);
            Assert.AreEqual(100f, enemy.Health.Current, "stagger alone deals no damage");
        }

        [UnityTest]
        public IEnumerator DeadCaster_CannotAct_AndDeathClearsBuffs()
        {
            SkillDefinition guard = Track(SkillDefinition.Create("guard", 0f, SkillTargetMode.Self, 0f, 0f,
                SkillEffect.Buff(StatModifier.Flat(StatType.Defense, 10f), 30f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            yield return null;
            SkillCaster caster = Caster(hero, guard);
            caster.TryCast(0, default);
            Assert.AreEqual(10f, hero.Stats.GetValue(StatType.Defense), 1e-3f);

            hero.ReceiveDamage(1000f);
            yield return null;
            Assert.AreEqual(0f, hero.Stats.GetValue(StatType.Defense), 1e-3f, "buff removed on death");
            caster.ResetCooldowns();
            Assert.AreEqual(SkillCastResult.CannotAct, caster.TryCast(0, default));
        }

        [UnityTest]
        public IEnumerator AimFinder_PicksUnitOnRayOrNearIt_AndGroundPointOtherwise()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = origin + new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            CharacterActor enemy = Unit("Enemy", CombatTeam.Hostile, new Vector3(0f, 0f, 10f));
            yield return new WaitForFixedUpdate();

            Vector3 eye = origin + new Vector3(0f, 1.6f, 0f);
            SkillAim direct = SkillAimFinder.FromRay(new Ray(eye, Vector3.forward), 50f, 0f, hero);
            Assert.AreSame(enemy, direct.Unit, "ray hits the enemy body");

            Ray nearMiss = new Ray(eye + Vector3.right * 0.9f, Vector3.forward);
            Assert.IsNull(SkillAimFinder.FromRay(nearMiss, 50f, 0f, hero).Unit, "no aim radius -> misses");
            Assert.AreSame(enemy, SkillAimFinder.FromRay(nearMiss, 50f, 0.75f, hero).Unit, "aim radius catches the near miss");

            Vector3 down = (origin + new Vector3(-5f, 0f, 5f) - eye).normalized;
            SkillAim floor = SkillAimFinder.FromRay(new Ray(eye, down), 50f, 0.75f, hero);
            Assert.IsNull(floor.Unit);
            Assert.AreEqual(0f, floor.Point.y - origin.y, 0.05f, "point is on the ground");
            Assert.AreEqual(-5f, floor.Point.x - origin.x, 0.1f);

            SkillAim own = SkillAimFinder.FromRay(new Ray(origin + new Vector3(0f, 1f, -3f), Vector3.forward), 2.5f, 0f, hero);
            Assert.IsNull(own.Unit, "the caster is never its own target");
        }

        [UnityTest]
        public IEnumerator PlaceholderView_ShowsDiscBriefly()
        {
            SkillDefinition whirl = Track(SkillDefinition.Create("whirl", 0f, SkillTargetMode.Self, 0f, 3f,
                SkillEffect.Damage(1f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            yield return null;
            SkillCaster caster = Caster(hero, whirl);
            PlaceholderSkillView view = hero.gameObject.AddComponent<PlaceholderSkillView>();
            caster.TryCast(0, default);
            Assert.AreEqual(1, view.ActiveDiscs);
            GameObject disc = GameObject.Find("SkillCast_whirl");
            Assert.IsNotNull(disc);
            Assert.IsNull(disc.GetComponent<Collider>(), "visual only, no collider");
            for (int i = 0; i < 50; i++) yield return null;
            Assert.AreEqual(0, view.ActiveDiscs);
        }
    }
}
