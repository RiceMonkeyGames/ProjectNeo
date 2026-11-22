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
    /// COMBAT-06 4단계: 영웅 AI 자동 시전(<see cref="HeroSkillAI"/>). 범위 공격은 적 수, 대상 공격은 사거리,
    /// 회복은 다친 아군, 지원은 교전 거리로 판단하고, Auto·전투 여부로 켜고 끈다.
    /// </summary>
    public class HeroSkillAIPlayTests
    {
        readonly List<Object> assets = new List<Object>();
        readonly Vector3 origin = new Vector3(3000f, 0f, 3000f);
        Scene scene;
        Scene originalScene;
        float previousCaptureDelta;
        SkillAutoSettings auto;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("HeroSkillAITest");
            SceneManager.SetActiveScene(scene);
            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            auto = Track(SkillAutoSettings.Create(0.25f, 2, 0.6f, 10f));
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

        CharacterActor Unit(string name, CombatTeam team, Vector3 position, float health = 100f)
        {
            var definition = Track(SoldierDefinition.Create(name, new Cost(0),
                new StatValue(StatType.MaxHealth, health), new StatValue(StatType.Attack, 10f),
                new StatValue(StatType.Defense, 0f), new StatValue(StatType.AttackSpeed, 1f),
                new StatValue(StatType.AttackRange, 2f), new StatValue(StatType.MoveSpeed, 4f)));
            var profile = Track(CombatProfile.Create(0.1f, 70f, 0.4f, 2f));
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

        HeroSkillAI Brain(CharacterActor hero, CharacterActor target, params SkillDefinition[] skills)
        {
            SkillCaster caster = SkillCaster.On(hero);
            caster.Setup(skills);
            HeroSkillAI brain = HeroSkillAI.On(caster);
            brain.Configure(auto);
            brain.Auto = false; // 테스트는 Think()를 직접 부른다.
            brain.TargetSource = () => target;
            return brain;
        }

        [UnityTest]
        public IEnumerator SelfArea_WaitsForEnoughEnemies()
        {
            SkillDefinition whirl = Track(SkillDefinition.Create("whirl", 5f, SkillTargetMode.Self, 0f, 3f, SkillEffect.Damage(5f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            CharacterActor first = Unit("E1", CombatTeam.Hostile, new Vector3(2f, 0f, 0f));
            yield return null;
            HeroSkillAI brain = Brain(hero, first, whirl);

            Assert.AreEqual(-1, brain.Think(), "one enemy is not enough");
            Unit("E2", CombatTeam.Hostile, new Vector3(-2f, 0f, 0f));
            yield return null;
            Assert.AreEqual(0, brain.Think());
            Assert.Less(first.Health.Current, 100f);
            Assert.AreEqual(-1, brain.Think(), "on cooldown");
        }

        [UnityTest]
        public IEnumerator TargetUnit_UsesCurrentTargetOnlyInRange()
        {
            SkillDefinition slam = Track(SkillDefinition.Create("slam", 5f, SkillTargetMode.TargetUnit, 3f, 0f, SkillEffect.Damage(10f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            CharacterActor enemy = Unit("Enemy", CombatTeam.Hostile, new Vector3(8f, 0f, 0f));
            yield return null;
            HeroSkillAI brain = Brain(hero, enemy, slam);

            Assert.AreEqual(-1, brain.Think(), "out of range");
            enemy.transform.position = origin + new Vector3(2f, 0f, 0f);
            Assert.AreEqual(0, brain.Think());
            Assert.Less(enemy.Health.Current, 100f);

            brain.TargetSource = () => null;
            brain.GetComponent<SkillCaster>().ResetCooldowns();
            enemy.transform.position = origin + new Vector3(8f, 0f, 0f);
            Assert.AreEqual(-1, brain.Think(), "no target and no enemy within skill range, no cast");
        }

        [UnityTest]
        public IEnumerator TargetUnit_FindsNearestEnemyInSkillRange_WhenTargetMissingOrOutOfReach()
        {
            SkillDefinition slam = Track(SkillDefinition.Create("slam", 5f, SkillTargetMode.TargetUnit, 3f, 0f, SkillEffect.Damage(10f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            CharacterActor far = Unit("Far", CombatTeam.Hostile, new Vector3(9f, 0f, 0f));
            CharacterActor close = Unit("Close", CombatTeam.Hostile, new Vector3(0f, 0f, 2.5f));
            CharacterActor closer = Unit("Closer", CombatTeam.Hostile, new Vector3(-2f, 0f, 0f));
            yield return null;
            HeroSkillAI brain = Brain(hero, null, slam);

            Assert.AreEqual(0, brain.Think(), "no current target (e.g. melee hero on Hold): looks within skill range");
            Assert.Less(closer.Health.Current, 100f, "the nearest enemy in range");
            Assert.AreEqual(100f, close.Health.Current);

            brain.GetComponent<SkillCaster>().ResetCooldowns();
            closer.transform.position = origin + new Vector3(-20f, 0f, 0f);
            brain.TargetSource = () => far;
            Assert.AreEqual(0, brain.Think(), "current target out of skill range: falls back to an enemy in range");
            Assert.Less(close.Health.Current, 100f);
            Assert.AreEqual(100f, far.Health.Current);
        }

        [UnityTest]
        public IEnumerator AimPointArea_AimsAtBestClusterInsteadOfCurrentTarget()
        {
            SkillDefinition meteor = Track(SkillDefinition.Create("meteor", 5f, SkillTargetMode.AimPoint, 12f, 2f, SkillEffect.Damage(10f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            CharacterActor lone = Unit("Lone", CombatTeam.Hostile, new Vector3(3f, 0f, 0f));
            CharacterActor a = Unit("A", CombatTeam.Hostile, new Vector3(0f, 0f, 9f));
            CharacterActor b = Unit("B", CombatTeam.Hostile, new Vector3(1f, 0f, 9f));
            CharacterActor c = Unit("C", CombatTeam.Hostile, new Vector3(0f, 0f, 10f));
            yield return null;
            HeroSkillAI brain = Brain(hero, lone, meteor);

            Assert.AreEqual(0, brain.Think());
            Assert.AreEqual(100f, lone.Health.Current, 1e-3f, "the current target alone is not the best spot");
            Assert.Less(a.Health.Current, 100f);
            Assert.Less(b.Health.Current, 100f);
            Assert.Less(c.Health.Current, 100f);

            brain.GetComponent<SkillCaster>().ResetCooldowns();
            brain.TargetSource = () => null;
            a.transform.position = origin + new Vector3(0f, 0f, 30f);
            b.transform.position = origin + new Vector3(1f, 0f, 30f);
            c.transform.position = origin + new Vector3(0f, 0f, 31f);
            Assert.AreEqual(-1, brain.Think(), "clump moved out of skill range and the lone enemy is below the 2-enemy minimum");
        }

        sealed class MoraleContext : ISkillContext
        {
            public readonly Dictionary<CharacterActor, int> Squads = new Dictionary<CharacterActor, int>();
            public readonly Dictionary<int, float> Ratios = new Dictionary<int, float>();
            public int Raised;

            public int SquadOf(CharacterActor actor) => actor != null && Squads.TryGetValue(actor, out int squad) ? squad : -1;

            public void CollectSquad(int squad, List<CharacterActor> into)
            {
                foreach (KeyValuePair<CharacterActor, int> pair in Squads)
                    if (pair.Value == squad && !into.Contains(pair.Key)) into.Add(pair.Key);
            }

            public float AddMorale(int squad, float amount) { Raised++; return amount; }

            public bool TryGetMoraleRatio(int squad, out float ratio) => Ratios.TryGetValue(squad, out ratio);
        }

        [UnityTest]
        public IEnumerator MoraleOnly_WaitsUntilSquadMoraleIsAtOrBelowThreshold()
        {
            SkillDefinition rally = Track(SkillDefinition.Create("rally", 5f, SkillTargetMode.OwnSquad, 0f, 0f, SkillEffect.MoraleGain(15f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            CharacterActor member = Unit("Member", CombatTeam.Allied, new Vector3(1f, 0f, 0f));
            Unit("Enemy", CombatTeam.Hostile, new Vector3(5f, 0f, 0f));
            yield return null;
            HeroSkillAI brain = Brain(hero, null, rally);
            var context = new MoraleContext();
            context.Squads[hero] = 0;
            context.Squads[member] = 0;
            brain.GetComponent<SkillCaster>().UseContext(context);

            Assert.AreEqual(-1, brain.Think(), "squad not in battle (no morale ratio): nothing to raise");
            context.Ratios[0] = 0.9f;
            Assert.AreEqual(-1, brain.Think(), "90% is above the 75% default threshold");
            context.Ratios[0] = 0.75f;
            Assert.AreEqual(0, brain.Think(), "engaged with no current target (nearest enemy within 10 m) and morale at 75%");
            Assert.AreEqual(1, context.Raised);
        }

        [UnityTest]
        public IEnumerator AimPointHeal_PicksMostHurtAllyBelowThreshold()
        {
            SkillDefinition heal = Track(SkillDefinition.Create("heal", 5f, SkillTargetMode.AimPoint, 10f, 1f, SkillEffect.Heal(20f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            CharacterActor scratched = Unit("Scratched", CombatTeam.Allied, new Vector3(3f, 0f, 0f));
            CharacterActor wounded = Unit("Wounded", CombatTeam.Allied, new Vector3(-6f, 0f, 0f));
            yield return null;
            HeroSkillAI brain = Brain(hero, null, heal);
            scratched.Health.TakeDamage(20f);
            Assert.AreEqual(-1, brain.Think(), "80% is above the 60% threshold");

            wounded.Health.TakeDamage(70f);
            Assert.AreEqual(0, brain.Think());
            Assert.AreEqual(50f, wounded.Health.Current, 1e-3f, "aimed at the most hurt ally");
            Assert.AreEqual(80f, scratched.Health.Current, 1e-3f, "outside the 1 m heal radius");
        }

        [UnityTest]
        public IEnumerator Support_OnlyWhileEngaged_AndFirstFittingSlotWins()
        {
            SkillDefinition slam = Track(SkillDefinition.Create("slam", 5f, SkillTargetMode.TargetUnit, 2f, 0f, SkillEffect.Damage(10f)));
            SkillDefinition cry = Track(SkillDefinition.Create("cry", 5f, SkillTargetMode.Self, 0f, 0f,
                SkillEffect.Buff(StatModifier.Percent(StatType.Attack, 0.5f), 5f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            CharacterActor enemy = Unit("Enemy", CombatTeam.Hostile, new Vector3(20f, 0f, 0f));
            yield return null;
            HeroSkillAI brain = Brain(hero, enemy, slam, cry);

            Assert.AreEqual(-1, brain.Think(), "enemy 20 m away: not engaged");
            enemy.transform.position = origin + new Vector3(8f, 0f, 0f);
            Assert.AreEqual(1, brain.Think(), "slam out of range, so the buff in slot 2 is used");
            Assert.AreEqual(15f, hero.Stats.GetValue(StatType.Attack), 1e-3f);
            Assert.AreEqual(1, brain.AutoCasts);
        }

        [UnityTest]
        public IEnumerator Update_CastsOnlyWhenAutoOnAndGateOpen()
        {
            SkillDefinition cry = Track(SkillDefinition.Create("cry", 30f, SkillTargetMode.Self, 0f, 0f,
                SkillEffect.Buff(StatModifier.Flat(StatType.Defense, 5f), 5f)));
            CharacterActor hero = Unit("Hero", CombatTeam.Allied, Vector3.zero);
            CharacterActor enemy = Unit("Enemy", CombatTeam.Hostile, new Vector3(4f, 0f, 0f));
            yield return null;
            HeroSkillAI brain = Brain(hero, enemy, cry);
            bool battle = false;
            brain.Gate = () => battle;

            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(0, brain.AutoCasts, "Auto off");

            brain.Auto = true;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(0, brain.AutoCasts, "gate closed outside battle");

            battle = true;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(1, brain.AutoCasts, "casts within one think interval");
            Assert.AreEqual(5f, hero.Stats.GetValue(StatType.Defense), 1e-3f);
        }
    }
}
