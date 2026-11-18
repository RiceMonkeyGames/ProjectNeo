using System.Collections;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Siege;
using ProjectNeo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>PREP-06 Game 씬 준비 시간: 시계 표시, T 누르고 있기로 기다리기, 시간 소진, 다음 준비의 새 예산.</summary>
    public class GamePrepTimePlayTests
    {
        static int cleanup;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 0f;
            GameSession.Reset();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GamePrepTimeCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        static void Kill(CharacterActor actor) => actor.ReceiveDamage(actor.Health.Current + 1f);
        static Rect Bounds(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [UnityTest]
        public IEnumerator 준비_시계가_위에_보이고_T를_1초_누르면_한_번_기다리며_다_쓰면_막히고_다음_준비에_다시_찬다()
        {
            using var inputScope = new FocusIndependentInputScope();
            var originalKeyboard = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
                var interaction = Object.FindAnyObjectByType<PreparationInteraction>();
                var battle = Object.FindAnyObjectByType<GameBattleController>();
                var hud = Object.FindAnyObjectByType<GameFlowHud>();
                var clockHud = Object.FindAnyObjectByType<PrepClockHud>();
                Assert.IsNotNull(clockHud, "Game 씬에 준비 시계 표시가 있어야 한다(ProjectNeo/Setup/Configure Preparation Time).");
                for (int i = 0; i < 600 && !battle.CanStart; i++) yield return null;
                Assert.IsTrue(battle.CanStart);
                Cursor.lockState = CursorLockMode.Locked;
                yield return null; yield return null;

                RunState state = battle.RunLink.State;
                Assert.IsNotNull(state.PrepTime, "RunStart.asset에 준비 시간 에셋이 연결되어 있어야 한다.");
                float budget = state.PrepTime.BudgetHours, wait = state.PrepTime.WaitHours;
                Assert.Greater(budget, 0f);
                Assert.AreEqual(budget, state.Clock.Remaining, 0.0001f, "첫 준비는 예산이 가득 차 있다.");
                Assert.IsTrue(clockHud.IsShowing);
                StringAssert.Contains($"{budget:0.#}h left", clockHud.Text);
                StringAssert.Contains($"Hold {interaction.WaitKey} - Wait", hud.InteractionHint);
                // 시설 창(가운데)이 열려도 위 가운데 시계 줄이 가려지지 않는다.
                Canvas.ForceUpdateCanvases();
                RectTransform clockRect = clockHud.Area, phaseBox = (RectTransform)hud.transform.Find("Panel");
                var phaseText = (RectTransform)phaseBox.Find("Label");
                Assert.AreEqual(hud.transform.childCount - 1, clockRect.GetSiblingIndex(), "시계 줄은 시설 창보다 위에 그린다.");
                Assert.IsFalse(Bounds(clockRect).Overlaps(Bounds(phaseText)), "시계 줄은 단계 글자를 가리지 않는다(상자 위 여백에만 붙는다).");
                Assert.AreEqual(-16f, phaseBox.anchoredPosition.y, 0.01f, "단계 상자는 원래 자리에 있다.");
                foreach (string popup in new[] { "FacilityPanel", "EquipmentPanel" })
                {
                    Rect area = Bounds((RectTransform)hud.transform.Find(popup));
                    Assert.IsFalse(Bounds(clockRect).Overlaps(area), $"{popup}: clock={Bounds(clockRect)} popup={area}");
                }
                // 거의 화면 전체인 부대 편성 창이 열려 있는 동안에는 시계를 숨긴다.
                GameObject organization = hud.transform.Find("OrganizationPanel").gameObject;
                organization.SetActive(true);
                yield return null;
                Assert.IsFalse(clockHud.IsShowing, "부대 편성 창 위에는 시계를 그리지 않는다.");
                organization.SetActive(false);
                yield return null;
                Assert.IsTrue(clockHud.IsShowing);

                // 1초 누르면 한 번만 기다린다. 계속 누르고 있어도 다시 떼었다 눌러야 한다.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T));
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.Greater(interaction.WaitProgress, 0f);
                Assert.AreEqual(budget, state.Clock.Remaining, 0.0001f, "다 누르기 전에는 시간이 가지 않는다.");
                yield return new WaitForSecondsRealtime(1.0f);
                Assert.AreEqual(budget - wait, state.Clock.Remaining, 0.0001f);
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.AreEqual(budget - wait, state.Clock.Remaining, 0.0001f, "누르고 있는 채로는 다시 기다리지 않는다.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                Assert.AreEqual(0f, interaction.WaitProgress);
                Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase, "기다리기는 전투를 시작하지 않는다.");

                // 시간을 다 쓰면 기다리기가 막히고 안내가 사라지며 시계에 표시된다. 전투 시작은 그대로 된다.
                while (interaction.TryWait()) { }
                Assert.IsTrue(state.Clock.IsExhausted);
                Assert.IsFalse(interaction.CanWait);
                yield return null;
                StringAssert.DoesNotContain("Wait", hud.InteractionHint);
                StringAssert.Contains("out of time", clockHud.Text);
                Assert.IsTrue(interaction.CanStartBattle, "시간을 다 써도 B로 전투를 시작할 수 있다.");

                Assert.IsTrue(battle.TryStartBattle());
                yield return null;
                Assert.IsFalse(clockHud.IsShowing, "전투 중에는 준비 시계를 보이지 않는다.");
                Assert.IsFalse(state.TryWait());
                foreach (SiegeAttacker enemy in battle.Enemies) Kill(enemy.Actor);
                for (int i = 0; i < 10 && GameSession.Flow.Phase == GamePhase.Battle; i++) yield return null;
                Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
                Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
                yield return null; yield return null;

                Assert.AreEqual(budget, state.Clock.Remaining, 0.0001f, "다음 준비는 예산을 새로 채운다.");
                Assert.AreEqual(1, state.ClockPreparation);
                Assert.IsTrue(clockHud.IsShowing);
                StringAssert.DoesNotContain("out of time", clockHud.Text);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            }
        }
    }
}
