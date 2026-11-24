using System.Collections;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Stats;
using ProjectNeo.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    public sealed class PlayerHealthHudPlayTests
    {
        PlayerHealthHud hud;
        CharacterActor player;
        GameBattleController battle;
        float capture;
        static int cleanup;
        ItemDefinition potion;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            yield return null;
            hud = Object.FindAnyObjectByType<PlayerHealthHud>();
            player = Object.FindAnyObjectByType<PlayerInputController>().GetComponent<CharacterActor>();
            battle = Object.FindAnyObjectByType<GameBattleController>();
            Assert.IsNotNull(hud, "The saved Game scene must contain the HP HUD.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("HealthHudCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty)
                    yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            if (potion != null) Object.Destroy(potion);
            GameSession.Reset();
            Time.captureDeltaTime = capture;
        }

        void AssertHealth()
        {
            Assert.AreEqual($"HP {Mathf.CeilToInt(player.Health.Current)} / {Mathf.CeilToInt(player.Health.Maximum)}", hud.ValueText);
            Assert.AreEqual(player.Health.Current / player.Health.Maximum, hud.FillRatio, 0.001f);
            // UI-12 초상화 모양: 큰 숫자와 칸 체력바도 같은 값을 따른다(Game 씬에 Sidebar Layout을 적용했을 때).
            if (hud.NumberText.Length == 0) return;
            Assert.AreEqual(PrepHudText.HealthNumber(Mathf.CeilToInt(player.Health.Current), Mathf.CeilToInt(player.Health.Maximum)), hud.NumberText);
            int count = hud.SegmentCount;
            Assert.Greater(count, 0);
            float filled = 0f;
            for (int i = 0; i < count; i++) filled += hud.SegmentFill(i);
            Assert.AreEqual(player.Health.Current / player.Health.Maximum, filled / count, 0.001f, "칸을 모두 더한 채움이 체력 비율과 같다.");
        }

        IEnumerator StartBattle()
        {
            var world = Object.FindAnyObjectByType<ProjectNeo.Walls.ConstructionWorld>();
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
        }

        [UnityTest]
        public IEnumerator Preparation_DamageAndMaximumChanges_RefreshImmediately_AndPanelRestores()
        {
            Assert.IsTrue(hud.IsShowing);
            AssertHealth();
            player.ReceiveDamage(25f);
            AssertHealth();
            var source = new object();
            player.Stats.AddModifier(StatModifier.Flat(StatType.MaxHealth, 50f), source);
            AssertHealth();
            player.Stats.RemoveModifiersFrom(source);
            AssertHealth();
            var view = Object.FindAnyObjectByType<GamePhaseView>();
            Assert.IsTrue(view.EnterPanel());
            yield return null;
            // 시설·장비 창이 열려 있어도 체력 HUD는 보인다(UI-07, 사용자 결정 2026-11-23).
            Assert.IsTrue(hud.IsShowing, "창이 열려도 체력 HUD는 그대로 보인다.");
            AssertHealth();
            view.ExitPanel();
            yield return null;
            Assert.IsTrue(hud.IsShowing);
            AssertHealth();
        }

        [UnityTest]
        public IEnumerator Battle_PotionAndDeath_UpdateValues_AndResultsHideHud()
        {
            potion = ItemDefinition.Create("hp_hud_potion", ItemKind.Consumable, new Cost(10));
            GameSession.Flow.Run.State.Inventory.Add(potion);
            yield return StartBattle();
            Assert.IsTrue(hud.IsShowing);
            player.ReceiveDamage(player.Health.Maximum * 0.6f);
            AssertHealth();
            Assert.AreEqual(ConsumableUseCheck.Ok, battle.Consumables.TryUse());
            AssertHealth();
            player.ReceiveDamage(player.Health.Maximum + 1f);
            Assert.AreEqual(0f, hud.FillRatio);
            StringAssert.StartsWith("HP 0 /", hud.ValueText);
            yield return null;
            yield return null;
            Assert.IsFalse(hud.IsShowing);
        }

        [UnityTest]
        public IEnumerator NextPreparation_RestoresCurrentPlayerHealth_AndHudVisibility()
        {
            yield return StartBattle();
            player.ReceiveDamage(20f);
            AssertHealth();
            Assert.IsTrue(GameSession.Flow.FinishBattle(BattleOutcome.Victory));
            yield return null;
            Assert.IsFalse(hud.IsShowing);
            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return null;
            yield return null;
            Assert.IsTrue(hud.IsShowing);
            Assert.AreEqual(player.Health.Maximum, player.Health.Current, 0.001f);
            AssertHealth();
        }
    }
}
