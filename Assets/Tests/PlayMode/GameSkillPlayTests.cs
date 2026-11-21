using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Skills;
using ProjectNeo.Squads;
using ProjectNeo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// COMBAT-06 3단계: Game 씬에서 Z/X/C로 플레이어 스킬, Alt+키로 선택 분대 대장 스킬을 쓰고, 스킬 HUD가 전투 중에만 보인다.
    /// </summary>
    public class GameSkillPlayTests
    {
        static int cleanup;
        readonly List<Object> assets = new List<Object>();
        GameBattleController battle;
        GameSkills skills;
        CharacterActor player;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
            skills = battle.Skills;
            player = Object.FindAnyObjectByType<PlayerInputController>().GetComponent<CharacterActor>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameSkillCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            foreach (Object asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        T Track<T>(T value) where T : Object { assets.Add(value); return value; }

        IEnumerator StartBattle()
        {
            var world = Object.FindAnyObjectByType<ProjectNeo.Walls.ConstructionWorld>();
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
        }

        static IEnumerator Press(Keyboard keyboard, params Key[] keys)
        {
            Cursor.lockState = CursorLockMode.Locked;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator 기본_키는_ZXC와_Alt이고_전투_중에만_HUD가_보인다()
        {
            Assert.IsNotNull(skills, "Game 전투 오브젝트에 스킬 부품이 붙어야 한다.");
            Assert.IsNotNull(battle.SkillHud);
            Assert.AreEqual(3, skills.SlotCount);
            Assert.AreEqual("Z", skills.KeyOf(0));
            Assert.AreEqual("X", skills.KeyOf(1));
            Assert.AreEqual("C", skills.KeyOf(2));
            Assert.AreEqual("<Keyboard>/alt", battle.Settings.CaptainCastModifier.bindings[0].path,
                "기존 전투 설정 에셋도 기본 보조 키 Alt를 쓴다.");

            SkillDefinition whirl = Track(SkillDefinition.Create("test_whirl", 5f, SkillTargetMode.Self, 0f, 3f,
                SkillEffect.Damage(5f)));
            skills.PlayerSkills = () => new[] { whirl };
            yield return null;
            Assert.IsFalse(battle.SkillHud.Visible, "준비 중에는 HUD를 숨긴다.");

            yield return StartBattle();
            yield return null;
            Assert.IsNotNull(skills.PlayerCaster);
            Assert.AreEqual(1, skills.PlayerCaster.Slots.Count);
            Assert.IsTrue(skills.PlayerCaster.Slots.IsReady(0), "전투 시작 때 모든 칸이 준비 상태다.");
            Assert.IsTrue(battle.SkillHud.Visible);
            Assert.AreEqual("Z", battle.SkillHud.KeyText(0));
            Assert.AreEqual("test_whirl", battle.SkillHud.NameText(0));
            Assert.AreEqual("-", battle.SkillHud.NameText(1), "빈 칸");
            Assert.IsNotNull(player.GetComponent<PlaceholderSkillView>());
        }

        [UnityTest]
        public IEnumerator Z를_누르면_플레이어가_쓰고_빈_칸_키는_이유를_보여준다()
        {
            SkillDefinition whirl = Track(SkillDefinition.Create("test_whirl", 5f, SkillTargetMode.Self, 0f, 3f,
                SkillEffect.Damage(5f)));
            skills.PlayerSkills = () => new[] { whirl };
            yield return StartBattle();
            var casts = new List<SkillCastInfo>();
            skills.PlayerCaster.Cast += casts.Add;

            using var inputScope = new FocusIndependentInputScope();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return Press(keyboard, Key.Z);
                Assert.AreEqual(1, casts.Count, "Z로 첫 칸 스킬을 쓴다.");
                Assert.IsFalse(skills.PlayerCaster.Slots.IsReady(0));
                yield return null;
                Assert.Greater(battle.SkillHud.CoverRatio(0), 0.5f, "쿨다운 덮개");
                Assert.AreEqual("5", battle.SkillHud.TimerText(0));

                yield return Press(keyboard, Key.Z);
                Assert.AreEqual(1, casts.Count, "쿨다운 중에는 다시 쓰지 않는다.");
                StringAssert.Contains("준비되지", skills.Message);

                yield return Press(keyboard, Key.X);
                Assert.AreEqual(SkillCastResult.NoSkill, skills.LastResult);
                StringAssert.Contains("스킬이 없습니다", skills.Message);
                yield return null;
                Assert.AreEqual(skills.Message, battle.SkillHud.MessageText);
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator Alt와_함께_누르면_선택한_분대의_대장이_쓰고_플레이어는_쓰지_않는다()
        {
            SkillDefinition playerSkill = Track(SkillDefinition.Create("test_player", 5f, SkillTargetMode.Self, 0f, 0f,
                SkillEffect.Heal(1f)));
            SkillDefinition captainSkill = Track(SkillDefinition.Create("test_captain", 5f, SkillTargetMode.Self, 0f, 0f,
                SkillEffect.Heal(1f)));
            skills.PlayerSkills = () => new[] { playerSkill };
            yield return StartBattle();

            SquadCommandCenter center = Object.FindAnyObjectByType<SquadCommandCenter>();
            Assert.Greater(center.Squads.Count, 0, "Game 씬은 시작 분대가 있다.");
            Squad<UnitCombatAI> squad = center.Squads[0];
            if (squad.Leader == null) Assert.IsTrue(center.SetLeader(0, squad.Members[0]));
            CharacterActor captain = squad.Leader.GetComponent<CharacterActor>();
            SkillCaster captainCaster = SkillCaster.On(captain);
            captainCaster.Setup(new[] { captainSkill });
            int captainCasts = 0, playerCasts = 0;
            captainCaster.Cast += _ => captainCasts++;
            skills.PlayerCaster.Cast += _ => playerCasts++;

            using var inputScope = new FocusIndependentInputScope();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                center.Commander.ClearSelection();
                for (int i = 0; i < center.Squads.Count; i++) Assert.IsFalse(center.Commander.IsSelected(i));
                yield return Press(keyboard, Key.LeftAlt, Key.Z);
                Assert.AreEqual(0, captainCasts);
                Assert.AreEqual(0, playerCasts, "Alt를 누르면 플레이어 스킬은 쓰지 않는다.");
                StringAssert.Contains("부대를 선택", skills.Message);

                Assert.IsTrue(center.Commander.Select(0));
                yield return Press(keyboard, Key.LeftAlt, Key.Z);
                Assert.AreEqual(1, captainCasts, "선택 분대 대장이 첫 칸을 쓴다.");
                Assert.AreEqual(0, playerCasts);

                yield return Press(keyboard, Key.Z);
                Assert.AreEqual(1, playerCasts, "Alt 없이 누르면 플레이어가 쓴다.");
                Assert.AreEqual(1, captainCasts);
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator 스킬_퍽을_찍으면_플레이어_칸에_들어가고_고용_영웅은_고정_스킬과_자동_시전을_받는다()
        {
            RunState state = GameSession.Flow.Run.State;
            Assert.IsNotNull(state.Perks, "준비 단계에서 퍽 묶음이 만들어진다.");
            PerkTreeCatalog trees = Object.FindAnyObjectByType<GameFacilities>().StartConfig.PerkTrees;
            PerkDefinition whirlPerk = trees.AllPerks().Single(perk => perk.Id == "perk_combat_skill_whirlwind");
            Assert.AreEqual("skill_whirlwind", whirlPerk.GrantedSkill.Id);
            state.Perks.BonusPoints = 5;
            Assert.IsTrue(state.TryAllocatePerk(whirlPerk));
            CollectionAssert.AreEqual(new[] { whirlPerk.GrantedSkill }, state.LearnedPlayerSkills());

            // 주점 1레벨 영웅(검사·궁수) 중 하나를 1분대 대장으로 고용한다. 1등급이라 고정 스킬 첫 칸만 열린다.
            var facilities = Object.FindAnyObjectByType<GameFacilities>();
            state.Wallet.AddGold(200);
            Assert.IsTrue(Object.FindObjectsByType<FacilityStation>().Single(s => s.Type == ProjectNeo.Facilities.FacilityType.Tavern).Interact());
            HeroDefinition offered = state.TavernOffers[0];
            Assert.IsTrue(facilities.ChooseCaptainSquad(0));
            Assert.IsTrue(facilities.TryHire(0));
            facilities.Close();
            yield return null;

            yield return StartBattle();
            Assert.AreEqual(1, skills.PlayerCaster.Slots.Count);
            Assert.AreEqual("skill_whirlwind", skills.PlayerCaster.Slots.Get(0).Id, "찍은 스킬 퍽의 스킬이 첫 칸에 들어간다.");
            Assert.AreEqual("회오리 베기", battle.SkillHud.NameText(0));

            SquadCommandCenter center = Object.FindAnyObjectByType<SquadCommandCenter>();
            SkillCaster captainCaster = center.Squads[0].Leader.GetComponent<SkillCaster>();
            Assert.IsNotNull(captainCaster, "고용 영웅에게도 스킬 칸이 붙는다.");
            Assert.AreEqual(1, captainCaster.Slots.Count, "1등급 영웅은 1칸");
            Assert.AreSame(offered.Skills[0], captainCaster.Slots.Get(0));
            HeroSkillAI brain = captainCaster.GetComponent<HeroSkillAI>();
            Assert.IsNotNull(brain);
            Assert.IsTrue(brain.Auto, "기본은 자동 시전");
            Assert.IsNull(player.GetComponent<HeroSkillAI>(), "플레이어는 자동 시전하지 않는다.");
        }

        [UnityTest]
        public IEnumerator 장비_창_스킬_보기에서_스킬을_바꿔_끼우고_영웅_자동_사용을_끈다()
        {
            RunState state = GameSession.Flow.Run.State;
            PerkTreeCatalog trees = Object.FindAnyObjectByType<GameFacilities>().StartConfig.PerkTrees;
            PerkDefinition Perk(string id) => trees.AllPerks().Single(perk => perk.Id == id);
            state.Perks.BonusPoints = 10;
            // 회오리(선행 없음) -> 벼린 칼날 -> 방패 강타 -> 강인함 -> 전투 함성: 배운 스킬 3개.
            foreach (string id in new[] { "perk_combat_skill_whirlwind", "perk_combat_sharpened_blade", "perk_combat_skill_shield_bash",
                "perk_combat_toughness", "perk_combat_skill_war_cry" })
                Assert.IsTrue(state.TryAllocatePerk(Perk(id)), id);
            Assert.AreEqual(3, state.LearnedPlayerSkills().Count);

            var facilities = Object.FindAnyObjectByType<GameFacilities>();
            state.Wallet.AddGold(200);
            Assert.IsTrue(Object.FindObjectsByType<FacilityStation>().Single(s => s.Type == ProjectNeo.Facilities.FacilityType.Tavern).Interact());
            Assert.IsTrue(facilities.ChooseCaptainSquad(0));
            Assert.IsTrue(facilities.TryHire(0));
            facilities.Close();
            yield return null;

            var equipment = Object.FindAnyObjectByType<GameEquipment>();
            Assert.IsTrue(equipment.Open());
            Assert.IsFalse(equipment.SkillView);
            Assert.IsTrue(equipment.ToggleView());
            Assert.IsTrue(equipment.SkillView);
            StringAssert.Contains("스킬 칸 3 / 3", equipment.StatusText);
            Assert.IsTrue(equipment.EquippedRowTexts.Any(text => text.StartsWith("Z: 회오리 베기")), string.Join(" | ", equipment.EquippedRowTexts));
            Assert.IsFalse(equipment.TryEquipSkill(0), "남은 배운 스킬이 없다.");

            Assert.IsTrue(equipment.TryUnequipSkill(0));
            StringAssert.Contains("스킬 칸 2 / 3", equipment.StatusText);
            Assert.IsTrue(equipment.InventoryRowTexts.Any(text => text.StartsWith("회오리 베기")), "뺀 스킬이 배운 스킬 목록에 나온다.");
            Assert.IsTrue(equipment.TryEquipSkill(0));
            Assert.AreEqual("skill_whirlwind", state.PlayerSkillLoadout.EquippedId(0), "첫 빈 칸에 다시 들어간다.");

            Assert.IsTrue(equipment.NextHero());
            RunHero hero = equipment.SelectedHero;
            Assert.IsFalse(hero.IsPlayer);
            Assert.IsTrue(equipment.EquippedRowTexts.Any(text => text.StartsWith("Alt+Z: ")), string.Join(" | ", equipment.EquippedRowTexts));
            Assert.IsTrue(equipment.EquippedRowTexts.Any(text => text.StartsWith("잠김: ")), "1등급 영웅은 둘째 칸부터 잠겨 있다.");
            Assert.IsTrue(equipment.ToggleHeroAuto());
            Assert.IsFalse(hero.SkillAuto);
            StringAssert.Contains("자동 사용 꺼짐", equipment.StatusText);
            Assert.IsTrue(equipment.ToggleView());
            Assert.IsFalse(equipment.ToggleHeroAuto(), "장비 보기에서는 바꾸지 않는다.");
            equipment.Close();
            yield return null;

            yield return StartBattle();
            CollectionAssert.AreEqual(new[] { "skill_whirlwind", "skill_shield_bash", "skill_war_cry" },
                Enumerable.Range(0, skills.PlayerCaster.Slots.Count).Select(i => skills.PlayerCaster.Slots.Get(i).Id).ToArray());
            SquadCommandCenter center = Object.FindAnyObjectByType<SquadCommandCenter>();
            HeroSkillAI brain = center.Squads[0].Leader.GetComponent<HeroSkillAI>();
            Assert.IsNotNull(brain);
            Assert.IsFalse(brain.Auto, "장비 창에서 끈 자동 사용이 전투에 반영된다.");
        }
    }
}
