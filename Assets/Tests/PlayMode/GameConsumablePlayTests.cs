using System.Collections;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>PREP-04: 전투 중 Q로 런 보관함의 회복약을 써서 플레이어 체력을 회복한다.</summary>
    public class GameConsumablePlayTests
    {
        static int cleanup;
        GameBattleController battle;
        GameConsumables consumables;
        CharacterActor player;
        RunState state;
        ItemDefinition potion;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
            consumables = battle.Consumables;
            player = Object.FindAnyObjectByType<PlayerInputController>().GetComponent<CharacterActor>();
            state = GameSession.Flow.Run.State;
            potion = ItemDefinition.Create("test_potion", ItemKind.Consumable, new Cost(10));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameConsumableCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            Object.Destroy(potion);
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        IEnumerator StartBattle()
        {
            var world = Object.FindAnyObjectByType<ProjectNeo.Walls.ConstructionWorld>();
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
        }

        [UnityTest]
        public IEnumerator 전투_중에만_회복약을_쓰고_체력_비율만큼_회복하며_보관함에서_하나_줄어든다()
        {
            Assert.IsNotNull(consumables, "Game 전투 오브젝트에 소모품 사용 부품이 붙어야 한다.");
            Assert.AreEqual("<Keyboard>/q", battle.Settings.UseConsumable.bindings[0].path, "기존 전투 설정 에셋도 기본 키 Q를 쓴다.");
            Assert.AreEqual(10f, battle.Settings.ConsumableCooldown);
            state.Inventory.Add(potion);
            state.Inventory.Add(potion);
            Assert.AreEqual(2, consumables.Count);
            player.ReceiveDamage(player.Health.Maximum * 0.6f);
            float hurt = player.Health.Current;
            Assert.AreEqual(ConsumableUseCheck.NotInBattle, consumables.TryUse(), "준비 중에는 쓰지 않는다.");
            Assert.AreEqual(2, consumables.Count);
            Assert.AreEqual(hurt, player.Health.Current, 0.001f);

            yield return StartBattle();
            // 전투 시작 때 체력이 달라질 수 있어 다시 다치게 한 뒤 잰다.
            if (player.Health.Current >= player.Health.Maximum) player.ReceiveDamage(player.Health.Maximum * 0.6f);
            hurt = player.Health.Current;
            int used = 0; float healed = 0f;
            consumables.Used += (_, amount) => { used++; healed = amount; };
            Assert.AreEqual(ConsumableUseCheck.Ok, consumables.TryUse());
            float expected = player.Health.Maximum * potion.UseHealRatio;
            Assert.AreEqual(hurt + expected, player.Health.Current, 0.01f, "최대 체력의 35%를 바로 회복한다.");
            Assert.AreEqual(expected, healed, 0.01f);
            Assert.AreEqual(1, used);
            Assert.AreEqual(1, consumables.Count, "쓴 회복약은 런 보관함에서 사라진다.");
            Assert.AreEqual(1, state.Inventory.CountOf(potion));
            Assert.AreEqual(battle.Settings.ConsumableCooldown, consumables.CooldownRemaining, 0.001f);

            Assert.AreEqual(ConsumableUseCheck.CoolingDown, consumables.TryUse(), "재사용 대기 중에는 쓰지 못한다.");
            Assert.AreEqual(1, consumables.Count);
            StringAssert.Contains("Not ready", consumables.Message);
        }

        [UnityTest]
        public IEnumerator 체력이_가득하거나_회복약이_없으면_쓰지_않고_죽은_뒤에도_쓰지_않는다()
        {
            yield return StartBattle();
            Assert.AreEqual(ConsumableUseCheck.NoItem, consumables.TryUse());
            state.Inventory.Add(potion);
            player.Health.RestoreFull();
            Assert.AreEqual(ConsumableUseCheck.FullHealth, consumables.TryUse(), "가득 찬 체력에서는 낭비하지 않는다.");
            Assert.AreEqual(1, consumables.Count);

            player.ReceiveDamage(player.Health.Maximum + 1f);
            Assert.IsTrue(player.Health.IsDead);
            Assert.AreEqual(ConsumableUseCheck.Dead, consumables.TryUse());
            Assert.AreEqual(1, consumables.Count);
        }

        [UnityTest]
        public IEnumerator Q를_누르면_회복약을_쓰고_전투_화면에_개수와_대기_시간을_보여준다()
        {
            state.Inventory.Add(potion);
            state.Inventory.Add(potion);
            yield return StartBattle();
            player.ReceiveDamage(player.Health.Maximum * 0.6f);
            float hurt = player.Health.Current;
            var hud = Object.FindAnyObjectByType<GameFlowHud>();
            yield return null;
            StringAssert.Contains("×2", consumables.Hint());
            StringAssert.Contains(consumables.Hint(), hud.StatusText, "전투 상태 칸에 소모품 안내가 보인다.");

            using var inputScope = new FocusIndependentInputScope();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                yield return null; yield return null;
                Assert.Greater(player.Health.Current, hurt, "Q로 회복약을 쓴다.");
                Assert.AreEqual(1, consumables.Count);
                StringAssert.Contains("×1", consumables.Hint());
                StringAssert.Contains("ready in", consumables.Hint());
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
            }
            finally { InputSystem.RemoveDevice(keyboard); }

            // 대기 시간이 지나면 다시 쓸 수 있다.
            float wait = battle.Settings.ConsumableCooldown;
            for (int i = 0; i < Mathf.CeilToInt(wait * 30f) + 5 && consumables.CooldownRemaining > 0f; i++) yield return null;
            Assert.AreEqual(0f, consumables.CooldownRemaining);
            if (player.Health.Current >= player.Health.Maximum) player.ReceiveDamage(10f);
            Assert.AreEqual(ConsumableUseCheck.Ok, consumables.TryUse());
            Assert.AreEqual(0, consumables.Count);
            Assert.AreEqual(string.Empty, consumables.Hint(), "회복약이 없으면 안내를 숨긴다.");
        }
    }
}
