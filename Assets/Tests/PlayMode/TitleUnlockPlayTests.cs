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
    /// <summary>META-06: 저장된 Title 씬의 해금 창에서 항목을 사면 바로 슬롯에 저장된다. 실제 저장 폴더는 쓰지 않는다.</summary>
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

        [UnityTest]
        public IEnumerator BuyingAnEntry_SavesTheSlotRightAway()
        {
            store.Save(1, new MetaProgress { unlockPoints = 4 });
            store.SetLastSlot(1);
            yield return OpenTitle();
            var title = Object.FindAnyObjectByType<TitleScreen>();
            UnlockScreen unlocks = title.Unlocks;
            Assert.IsNotNull(unlocks, "Title 씬에 해금 창이 연결돼 있어야 한다.");
            Assert.IsFalse(unlocks.IsOpen);

            title.OpenUnlocks();

            Assert.IsTrue(unlocks.IsOpen);
            Assert.AreEqual(1, GameSession.ActiveSlot);
            Assert.AreEqual("해금 포인트: 4", unlocks.PointsText);
            Assert.AreEqual(19, unlocks.RowCount, "트랙 4개의 항목이 모두 보인다");
            Assert.AreEqual("성기사 - 3 (필요: 기사)", unlocks.RowText("hero_paladin"));

            Assert.AreEqual(UnlockClickResult.Armed, unlocks.Click("hero_knight"));
            Assert.AreEqual(4, store.Load(1).Progress.unlockPoints, "확인 전에는 저장이 그대로다");
            Assert.AreEqual(UnlockClickResult.Opened, unlocks.Click("hero_knight"));

            MetaProgress saved = store.Load(1).Progress;
            Assert.AreEqual(2, saved.unlockPoints);
            CollectionAssert.Contains(saved.openedEntryIds, "hero_knight");
            Assert.AreEqual("기사 - 해금됨", unlocks.RowText("hero_knight"));
            Assert.AreEqual("해금 포인트: 2", unlocks.PointsText);

            unlocks.Close();
            Assert.IsFalse(unlocks.IsOpen);
            StringAssert.Contains("해금 포인트 2", title.SlotText(1), "닫으면 슬롯 글이 새 포인트를 보여 준다");
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
            Assert.AreEqual("해금 포인트: 0", title.Unlocks.PointsText);
            Assert.AreEqual(UnlockClickResult.Refused, title.Unlocks.Click("bonus_gold_50"));
            StringAssert.StartsWith("해금 포인트가 부족", title.Unlocks.StatusText);
        }
    }
}
