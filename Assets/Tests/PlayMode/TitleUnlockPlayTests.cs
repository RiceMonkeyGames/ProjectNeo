using System.Collections;
using NUnit.Framework;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using ProjectNeo.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// META-06, UI-06: 저장된 Title 씬의 해금 화면에서 무작위 해금을 하면 바로 슬롯에 저장된다. 실제 저장 폴더는 쓰지 않는다.
    /// </summary>
    public class TitleUnlockPlayTests
    {
        static int cleanup;
        string saveDirectory;
        MetaSaveStore store;

        [SetUp]
        public void SetUp()
        {
            GameSession.Reset();
            saveDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ProjectNeoTitleUnlockTests_" + System.Guid.NewGuid().ToString("N"));
            store = new MetaSaveStore(saveDirectory);
            GameSession.UseSaveStore(store);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("TitleUnlockCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset();
            GameSession.UseSaveStore(null);
            if (System.IO.Directory.Exists(saveDirectory)) System.IO.Directory.Delete(saveDirectory, true);
        }

        IEnumerator OpenTitle()
        {
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SlotText_ShowsTheSavedBestRunScore()
        {
            // META-04 수동 확인 대체: 최고 점수가 있는 슬롯만 "최고 N"을 보여 준다.
            store.Save(0, new MetaProgress { bestRunScore = 12345 });
            store.Save(1, new MetaProgress());
            yield return OpenTitle();
            var title = Object.FindAnyObjectByType<TitleScreen>();

            StringAssert.Contains("최고 12,345", title.SlotText(0));
            StringAssert.DoesNotContain("최고", title.SlotText(1), "최고 점수가 0이면 표시하지 않는다");
        }

        sealed class FirstRandom : ProjectNeo.Core.IRandomSource
        {
            public int NextInt(int maxExclusive) => 0;
            public float NextFloat() => 0f;
        }

        [UnityTest]
        public IEnumerator RandomUnlock_OpensACandidate_AndSavesTheSlotRightAway()
        {
            store.Save(1, new MetaProgress { unlockPoints = 4 });
            store.SetLastSlot(1);
            yield return OpenTitle();
            var title = Object.FindAnyObjectByType<TitleScreen>();
            UnlockScreen unlocks = title.Unlocks;
            Assert.IsNotNull(unlocks, "Title 씬에 해금 화면이 연결돼 있어야 한다.");
            Assert.IsFalse(unlocks.IsOpen);
            unlocks.UseRandom(new FirstRandom());

            title.OpenUnlocks();

            Assert.IsTrue(unlocks.IsOpen);
            Assert.AreEqual(1, GameSession.ActiveSlot);
            Assert.AreEqual("보유 해금 포인트  4", unlocks.PointsText);
            Assert.IsTrue(unlocks.IsTrackHighlighted(0), "처음에는 첫 트랙(영웅)을 고른 채로 연다");
            StringAssert.StartsWith("영웅", unlocks.TrackTitleText);
            Assert.AreEqual(4, unlocks.RowCount, "고른 트랙의 항목만 카드로 보인다");
            Assert.AreEqual("기사 - 다음 후보", unlocks.RowText("hero_knight"));
            Assert.AreEqual("성기사 - 기사 먼저", unlocks.RowText("hero_paladin"));
            Assert.IsTrue(unlocks.RowThumbnail("hero_knight", out bool showsIcon), "카드마다 썸네일 칸이 있다(UI-06).");
            Assert.IsFalse(showsIcon, "아직 썸네일 그림이 없는 항목은 빈 칸만 보인다.");

            Assert.AreEqual(UnlockClickResult.Opened, unlocks.Draw(), "한 번 누르면 바로 연다(확인 없음)");

            MetaProgress saved = store.Load(1).Progress;
            Assert.AreEqual(3, saved.unlockPoints, "트랙 비용 1");
            CollectionAssert.Contains(saved.openedEntryIds, "hero_knight");
            Assert.AreEqual("기사 - 해금됨", unlocks.RowText("hero_knight"));
            Assert.IsTrue(unlocks.IsRowHighlighted("hero_knight"), "새로 열린 카드는 금색 테두리로 보인다");
            Assert.AreEqual("기사 해금!", unlocks.StatusText);
            Assert.AreEqual("보유 해금 포인트  3", unlocks.PointsText);

            unlocks.SelectTrack(3);
            Assert.IsTrue(unlocks.IsTrackHighlighted(3));
            StringAssert.StartsWith("시작 보너스", unlocks.TrackTitleText);
            Assert.AreEqual(4, unlocks.RowCount);
            Assert.IsEmpty(unlocks.StatusText, "트랙을 바꾸면 안내를 지운다");

            unlocks.Close();
            Assert.IsFalse(unlocks.IsOpen);
            StringAssert.Contains("해금 포인트 3", title.SlotText(1), "닫으면 슬롯 글이 새 포인트를 보여 준다");
        }

        [UnityTest]
        public IEnumerator UnlocksButton_OpensTheWindowOverTheTitle()
        {
            yield return OpenTitle();
            var title = Object.FindAnyObjectByType<TitleScreen>();
            Assert.IsNull(GameObject.Find("Button_Unlocks"), "해금은 메인 화면에 없고 슬롯 창에서만 보인다(UI-05).");
            title.OpenSlotMenu();
            Assert.IsNull(GameObject.Find("Button_Unlocks"), "슬롯 창에도 없고 출정 준비 화면에만 있다.");
            Assert.IsTrue(title.OpenExpedition());
            UnityEngine.UI.Button button = GameObject.Find("Button_Unlocks").GetComponent<UnityEngine.UI.Button>();

            button.onClick.Invoke();

            Assert.IsTrue(title.Unlocks.IsOpen);
            Assert.Greater(title.transform.Find("UnlockOverlay").GetSiblingIndex(), title.transform.Find("AscensionOverlay").GetSiblingIndex(),
                "해금 화면은 출정 준비 화면 위를 덮는 별도 패널이다(UI-06).");
            Assert.AreEqual("보유 해금 포인트  0", title.Unlocks.PointsText);
            Assert.AreEqual(UnlockClickResult.Refused, title.Unlocks.Draw());
            StringAssert.StartsWith("해금 포인트가 부족", title.Unlocks.StatusText);
            title.transform.Find("UnlockOverlay/Button_Back").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.IsFalse(title.Unlocks.IsOpen);
            Assert.IsTrue(title.IsExpeditionOpen, "뒤로 가면 출정 준비 화면으로 돌아간다.");
        }
    }
}
