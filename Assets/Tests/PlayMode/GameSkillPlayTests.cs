using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
        public IEnumerator 플레이어는_고정_스킬_3개를_모두_쓰고_고용_영웅은_고정_스킬과_자동_시전을_받는다()
        {
            RunState state = GameSession.Flow.Run.State;
            Assert.AreEqual(3, state.Player.Grade, "플레이어는 처음부터 최고 등급이다(UI-07).");

            // 주점 1레벨 영웅(검사·궁수) 중 하나를 1분대 대장으로 고용한다. 1등급이라 고정 스킬 첫 칸만 열린다.
            var facilities = Object.FindAnyObjectByType<GameFacilities>();
            state.Wallet.AddGold(200);
            Assert.IsTrue(Object.FindObjectsByType<FacilityStation>().Single(s => s.Type == ProjectNeo.Facilities.FacilityType.Tavern).Interact());
            HeroDefinition offered = state.TavernOffers[0];
            Assert.IsTrue(facilities.TryHire(0));
            facilities.Close();
            TavernCaptainHelper.MakeLastReserveCaptain(0); // UI-13: 대장은 편성 창에서 정한다.
            yield return null;

            yield return StartBattle();
            Assert.AreEqual(3, skills.PlayerCaster.Slots.Count, "플레이어 영웅 데이터의 고정 스킬 3개가 모두 열린다.");
            for (int i = 0; i < 3; i++) Assert.AreSame(state.Player.Definition.Skills[i], skills.PlayerCaster.Slots.Get(i));
            Assert.AreEqual(state.Player.Definition.Skills[0].DisplayName, battle.SkillHud.NameText(0));

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
        public IEnumerator 장비_창_스킬_보기는_고정_스킬을_보여주고_영웅_자동_사용을_끈다()
        {
            RunState state = GameSession.Flow.Run.State;

            var facilities = Object.FindAnyObjectByType<GameFacilities>();
            state.Wallet.AddGold(200);
            Assert.IsTrue(Object.FindObjectsByType<FacilityStation>().Single(s => s.Type == ProjectNeo.Facilities.FacilityType.Tavern).Interact());
            Assert.IsTrue(facilities.TryHire(0));
            facilities.Close();
            TavernCaptainHelper.MakeLastReserveCaptain(0); // UI-13: 대장은 편성 창에서 정한다.
            yield return null;

            var equipment = Object.FindAnyObjectByType<GameEquipment>();
            Assert.IsTrue(equipment.Open());
            // UI-13: 장비와 스킬을 탭 없이 한 창에 나란히 보인다.
            Assert.IsTrue(equipment.SideBySide, "Game 씬 장비 창은 장비와 스킬을 나란히 보여야 한다.");
            Assert.IsTrue(equipment.SkillsShown);
            Assert.IsFalse(equipment.ToggleView(), "나란히 보기에는 탭이 없다.");
            // UI-07 삼각형 스킬 칸: 칸 3개, 고른 칸 설명, 영웅만 자동 사용 버튼.
            Assert.IsTrue(equipment.SkillTriangle, "Game 씬 장비 창에 스킬 탭 화면이 연결돼 있어야 한다.");
            string first = state.Player.Definition.Skills[0].DisplayName;
            Assert.AreEqual(first, equipment.SkillCircles[0].SkillName);
            Assert.IsTrue(equipment.SkillCircles.All(circle => !circle.Locked), "플레이어 스킬은 모두 열려 있다.");
            Assert.IsTrue(equipment.SkillCircles[0].Selected, "처음에는 1번 칸을 고른다.");
            StringAssert.Contains(first, equipment.SkillDetailText);
            StringAssert.Contains("Z", equipment.SkillDetailText, "플레이어는 스킬 키만 보인다.");
            Assert.IsFalse(equipment.AutoPlateShown, "플레이어는 자동 사용 판이 없다.");
            Assert.IsFalse(equipment.ToggleHeroAuto(), "플레이어는 자동 사용을 바꿀 수 없다.");
            equipment.SkillCircles[2].Click();
            Assert.AreEqual(2, equipment.SelectedSkillSlot, "칸을 누르면 그 칸을 고른다.");
            StringAssert.Contains(state.Player.Definition.Skills[2].DisplayName, equipment.SkillDetailText);

            Assert.IsTrue(equipment.NextHero());
            RunHero hero = equipment.SelectedHero;
            Assert.IsFalse(hero.IsPlayer);
            Assert.AreEqual(0, equipment.SelectedSkillSlot, "영웅을 바꾸면 1번 칸부터 본다.");
            StringAssert.Contains("Alt + Z", equipment.SkillDetailText);
            Assert.IsFalse(equipment.SkillCircles[0].Locked);
            Assert.IsTrue(equipment.SkillCircles[1].Locked && equipment.SkillCircles[2].Locked, "1등급 영웅은 둘째 칸부터 잠겨 있다.");
            Assert.IsTrue(equipment.SelectSkill(1));
            StringAssert.Contains("2등급에 열림", equipment.SkillDetailText);
            Assert.IsTrue(equipment.AutoPlateShown);
            Assert.IsTrue(equipment.AutoPlateShowsOn, "기본은 켜짐(초록 판).");
            Assert.IsTrue(equipment.ToggleHeroAuto(), "판을 누르면 바뀐다.");
            Assert.IsFalse(hero.SkillAuto);
            Assert.IsFalse(equipment.AutoPlateShowsOn, "꺼지면 회색 판.");
            equipment.Close();
            yield return null;

            yield return StartBattle();
            CollectionAssert.AreEqual(state.Player.Definition.Skills.Select(skill => skill.Id).ToArray(),
                Enumerable.Range(0, skills.PlayerCaster.Slots.Count).Select(i => skills.PlayerCaster.Slots.Get(i).Id).ToArray(),
                "플레이어는 자기 고정 스킬을 칸 순서대로 쓴다.");
            SquadCommandCenter center = Object.FindAnyObjectByType<SquadCommandCenter>();
            HeroSkillAI brain = center.Squads[0].Leader.GetComponent<HeroSkillAI>();
            Assert.IsNotNull(brain);
            Assert.IsFalse(brain.Auto, "장비 창에서 끈 자동 사용이 전투에 반영된다.");
        }
    }
}
