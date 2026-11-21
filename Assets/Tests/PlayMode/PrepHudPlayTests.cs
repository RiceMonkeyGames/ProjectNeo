using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>UI-06 준비 HUD: 위 자원 바, 왼쪽 분대 카드, 짧은 안내 줄과 H 전체 키 목록. 겹침 없이 배치된다.</summary>
    public class PrepHudPlayTests
    {
        static int cleanup;
        GameBattleController battle;
        ConstructionWorld world;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
            world = Object.FindAnyObjectByType<ConstructionWorld>();
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending || !battle.CanStart); i++) yield return null;
            Assert.IsTrue(battle.CanStart);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("PrepHudCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        static Rect ScreenRect(Transform target)
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [UnityTest]
        public IEnumerator 준비_중_위_자원_바에_단계와_골드가_보이고_분대마다_카드가_생기며_시험용_목록은_숨는다()
        {
            yield return null;
            var hud = Object.FindAnyObjectByType<GameFlowHud>();
            var bar = Object.FindAnyObjectByType<PrepTopBarHud>();
            var cards = Object.FindAnyObjectByType<PrepSquadCardsHud>();
            var center = Object.FindAnyObjectByType<SquadCommandCenter>();
            var list = Object.FindAnyObjectByType<SquadCommandHud>(FindObjectsInactive.Include);
            Assert.IsNotNull(bar, "ProjectNeo/Setup/Preparation HUD (UI-06)을 실행해야 한다.");
            Assert.IsNotNull(cards);
            RunState state = battle.RunLink.State;

            Assert.IsTrue(bar.IsShowing);
            Assert.IsFalse(hud.transform.Find("Panel").gameObject.activeSelf, "준비 중에는 단계 상자 대신 자원 바가 보인다.");
            StringAssert.StartsWith("준비 · 스테이지 1", bar.StageText);
            StringAssert.StartsWith($"골드 {state.Wallet.Gold}", bar.ResourcesText);

            Assert.IsTrue(cards.IsShowing);
            Assert.IsFalse(list.ListVisible, "준비 중에는 분대 카드가 시험용 분대 목록을 대신한다.");
            Assert.AreEqual(center.Commander.SquadCount, cards.Cards.Count(card => card.gameObject.activeSelf));
            PrepSquadCard first = cards.Cards[0];
            Assert.AreEqual(SquadCommandHud.SquadName(center, 0), first.TitleText);
            Assert.AreEqual(PrepHudText.Headcount(center.AliveCount(0), center.Squads[0].Capacity), first.HeadcountText);
            StringAssert.StartsWith("사기 ", first.MoraleText);
            Assert.Greater(first.MoraleRatio, 0f);

            // 자원 바·분대 카드·적 정보 창이 서로 겹치지 않는다.
            Object.FindAnyObjectByType<EnemyIntelHud>().TogglePreview();
            yield return null;
            Rect[] areas = { ScreenRect(bar.Area), ScreenRect(cards.Area), ScreenRect(hud.transform.Find("EnemyIntel")) };
            for (int a = 0; a < areas.Length; a++)
                for (int b = a + 1; b < areas.Length; b++)
                    Assert.IsFalse(areas[a].Overlaps(areas[b]), $"{a} / {b}");

            // 전투가 시작되면 자원 바와 카드는 숨고 단계 상자와 시험용 목록이 돌아온다.
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Assert.IsFalse(bar.IsShowing || cards.IsShowing);
            Assert.IsTrue(hud.transform.Find("Panel").gameObject.activeSelf);
            Assert.IsTrue(list.ListVisible);
        }

        [UnityTest]
        public IEnumerator 아래_안내_줄은_짧고_H를_누르면_전체_키_목록이_열리고_닫힌다()
        {
            using var inputScope = new FocusIndependentInputScope();
            var originalKeyboard = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var hud = Object.FindAnyObjectByType<GameFlowHud>();
                var interaction = Object.FindAnyObjectByType<PreparationInteraction>();
                var keys = Object.FindAnyObjectByType<KeyListHud>();
                Assert.IsNotNull(keys);
                Cursor.lockState = CursorLockMode.Locked;
                yield return null; yield return null;

                StringAssert.DoesNotContain("WASD", hud.InteractionHint, "키 목록은 아래 줄에서 빠졌다.");
                StringAssert.Contains($"{interaction.BattleStartKey} 길게 누르기 - 전투 시작", hud.InteractionHint);
                Assert.IsTrue(keys.HintShowing);
                Assert.AreEqual($"{interaction.KeyListKey} 키 안내", keys.HintText);
                Assert.IsFalse(keys.IsShowing);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.IsTrue(keys.IsShowing, "H를 누르면 전체 키 목록이 열린다.");
                foreach (string line in new[] { "WASD - 이동", $"{interaction.InteractKey} - 상호작용", $"{interaction.EquipmentKey} - 장비",
                    $"{interaction.EnemyPreviewKey} - 적 정보", "부대 선택", $"{interaction.KeyListKey} - 키 안내 닫기" })
                    StringAssert.Contains(line, keys.Body);
                Assert.IsTrue(interaction.CanStartBattle, "목록은 화면을 막지 않는다.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.IsFalse(keys.IsShowing);

                // 다른 화면(건설)에서는 목록과 안내 줄이 보이지 않는다.
                keys.Toggle();
                Assert.IsTrue(Object.FindAnyObjectByType<GamePhaseView>().EnterConstruction());
                yield return null; yield return null;
                Assert.IsFalse(keys.IsShowing || keys.HintShowing);
                Assert.AreEqual(string.Empty, hud.ContextPrompt);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            }
        }
    }
}
