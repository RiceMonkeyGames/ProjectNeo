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
            Assert.AreEqual(PrepHudText.Gold(state.Wallet.Gold), bar.GoldText, "골드는 동전 아이콘 옆 숫자로 따로 보인다.");
            Assert.AreEqual("스테이지 1", bar.StageTitleText);

            Assert.IsTrue(cards.IsShowing);
            Assert.IsFalse(list.ListVisible, "준비 중에는 분대 카드가 시험용 분대 목록을 대신한다.");
            Assert.AreEqual(center.Commander.SquadCount, cards.Cards.Count(card => card.gameObject.activeSelf));
            PrepSquadCard first = cards.Cards[0];
            Assert.AreEqual(SquadCommandHud.SquadName(center, 0), first.TitleText);
            Assert.AreEqual(PrepHudText.Headcount(center.AliveCount(0), center.Squads[0].Capacity), first.HeadcountText);
            StringAssert.StartsWith("사기 ", first.MoraleText);
            Assert.Greater(first.MoraleRatio, 0f);
            // UI-12 A안 카드: 번호 배지, 명령 배지, 정원만큼 인원 칸(살아 있는 수만큼 채움).
            Assert.AreEqual("1", first.NumberText);
            Assert.AreEqual(SquadCommandHud.OrderName(center.Commander.OrderOf(0).Kind), first.OrderText);
            Assert.AreEqual(center.Squads[0].Capacity, first.SegmentCount);
            Assert.AreEqual(center.AliveCount(0), first.FilledSegments);

            // 위 판(사이드바 머리판, 없으면 자원 바 전체)·골드 칩·분대 카드·적 정보 창이 서로 겹치지 않는다.
            // UI-12 사이드바에서는 자원 바가 보이지 않는 전체 폭 띠라서, 적 정보 창이 골드 칩 바로 아래(띠 안)에 온다.
            Object.FindAnyObjectByType<EnemyIntelHud>().TogglePreview();
            yield return null;
            Transform header = bar.Area.Find("Visual/Sidebar_Header");
            Transform chip = bar.Area.Find("ResourceChip");
            Rect[] areas = header != null && chip != null
                ? new[] { ScreenRect(header), ScreenRect(chip), ScreenRect(cards.Area), ScreenRect(hud.transform.Find("EnemyIntel")) }
                : new[] { ScreenRect(bar.Area), ScreenRect(cards.Area), ScreenRect(hud.transform.Find("EnemyIntel")) };
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
        public IEnumerator 아래_안내_줄은_짧고_키_가이드는_상황별로_바뀌며_H로_접고_편다()
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
                // UI-12 조준점 링: 탐색 중에는 아래 판 대신, B를 누르는 동안만 조준점 둘레 링과 글이 보인다.
                Assert.IsFalse(hud.InteractionPlateShowing, "탐색 중에는 아래 전투 시작 판을 숨긴다.");
                Assert.IsFalse(hud.HoldRingShowing);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.B));
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.IsTrue(hud.HoldRingShowing, "B를 누르는 동안 링이 보인다.");
                Assert.Greater(hud.HoldRingRatio, 0f);
                StringAssert.StartsWith("전투 시작 ", hud.HoldRingText);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                Assert.IsFalse(hud.HoldRingShowing, "떼면 링이 사라진다.");
                Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);

                // UI-12: 화면 키 가이드는 처음부터 펼쳐져 있고, 예전 "H 키 안내" 줄은 숨는다.
                Assert.IsTrue(keys.IsShowing && keys.ListOpen, "키 가이드는 처음부터 보인다.");
                Assert.IsFalse(keys.HintShowing);
                foreach (string line in new[] { $"{interaction.EquipmentKey} - 장비", $"{interaction.EnemyPreviewKey} - 적 정보",
                    $"{interaction.BattleStartKey} - 길게 누르기 - 전투 시작", $"{interaction.KeyListKey} - 가이드 숨기기" })
                    StringAssert.Contains(line, keys.Body);

                // 가이드는 상황과 상관없이 고정이다: 부대를 골라도 같은 줄이 같은 순서로 남는다.
                var center = Object.FindAnyObjectByType<SquadCommandCenter>();
                center.Commander.ClearSelection();
                yield return null;
                string unselected = keys.Body;
                foreach (string line in new[] { $"{interaction.InteractKey} - 상호작용", "- 부대 선택/해제",
                    $"{center.Settings.SelectAll.GetBindingDisplayString()} - 전체 선택/해제",
                    $"{center.Settings.Move.GetBindingDisplayString()} - 부대 이동", "- 부대 페이지" })
                    StringAssert.Contains(line, unselected);
                StringAssert.DoesNotContain("·", unselected, "전체 선택 키는 부대 선택 범위와 따로 한 줄이다.");
                Assert.IsTrue(center.Commander.Select(0));
                yield return null;
                Assert.AreEqual(unselected, keys.Body, "부대를 골라도 가이드 줄은 바뀌지 않는다.");

                // 한 부대만 골라 둔 채 같은 선택 키를 다시 누르면, 모두 골라 둔 채 전체 선택 키를 누르면 선택이 풀린다(UI-12).
                var squadInput = Object.FindAnyObjectByType<SquadCommandInput>();
                Assert.IsFalse(squadInput.SelectSquad(0));
                Assert.AreEqual(0, center.Commander.SelectedCount);
                Assert.IsTrue(squadInput.SelectSquad(0), "풀린 뒤 다시 누르면 다시 고른다.");
                squadInput.SelectAll();
                Assert.AreEqual(center.Commander.SquadCount, center.Commander.SelectedCount);
                Assert.IsTrue(squadInput.SelectSquad(0), "여러 부대를 골라 두었으면 그 부대 하나만 고른다.");
                Assert.AreEqual(1, center.Commander.SelectedCount);
                squadInput.SelectAll();
                squadInput.SelectAll();
                Assert.AreEqual(0, center.Commander.SelectedCount, "모두 골라 둔 채 전체 선택 키를 다시 누르면 풀린다.");

                // H로 접으면 펼치는 H 한 줄만 남고, 다시 누르면 펼친다.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                Assert.IsFalse(keys.ListOpen);
                Assert.AreEqual($"{interaction.KeyListKey} - 키 가이드 보기", keys.Body);
                Assert.IsTrue(keys.Rows.First(row => row.gameObject.activeSelf).RightAligned, "접힌 H 줄은 오른쪽 아래 모서리에 붙는다.");
                Assert.IsTrue(interaction.CanStartBattle, "가이드는 화면을 막지 않는다.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                Assert.IsTrue(keys.ListOpen);
                StringAssert.Contains($"{interaction.EquipmentKey} - 장비", keys.Body);
                Assert.IsFalse(keys.Rows.Any(row => row.gameObject.activeSelf && row.RightAligned), "펼치면 줄은 다시 왼쪽 정렬이다.");

                // 다른 화면(건설)에서는 가이드와 안내 줄이 보이지 않는다.
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
