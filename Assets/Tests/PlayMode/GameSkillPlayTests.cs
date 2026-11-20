using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
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
                StringAssert.Contains("분대를 선택", skills.Message);

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
    }
}
