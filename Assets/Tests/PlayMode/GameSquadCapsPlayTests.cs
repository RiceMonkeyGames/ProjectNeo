using System.Collections;
using NUnit.Framework;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// PREP-09(Q-57): Game 씬은 10부대 × 잡졸 20명(대장은 따로)과 부대 선택 키 F1~F10을 쓴다.
    /// 잡졸 20명이 찬 부대도 편성 창 카드에 모든 줄이 들어가고, 카드 영역을 세로로 스크롤하면 마지막 줄이 보인다.
    /// </summary>
    public class GameSquadCapsPlayTests
    {
        static int cleanup;
        GameFacilities facilities;
        GameSquadOrganization organization;
        SquadCommandCenter center;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            facilities = Object.FindAnyObjectByType<GameFacilities>();
            organization = Object.FindAnyObjectByType<GameSquadOrganization>();
            center = Object.FindAnyObjectByType<SquadCommandCenter>();
            var world = Object.FindAnyObjectByType<ConstructionWorld>();
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (facilities != null && facilities.IsOpen) facilities.Close();
            if (organization != null && organization.IsOpen) organization.Close();
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameSquadCapsCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        [UnityTest]
        public IEnumerator Game_씬은_10부대_20명_상한과_F1부터_F10까지의_선택_키를_쓴다()
        {
            Assert.AreEqual(10, center.Config.MaxSquads);
            Assert.AreEqual(20, center.Config.MaxMembersPerSquad, "대장은 인원 상한에 들지 않는다.");
            Assert.AreEqual(10, center.Settings.SelectSquad.Length);
            for (int i = 0; i < 10; i++)
                Assert.AreEqual("<Keyboard>/f" + (i + 1), center.Settings.SelectSquad[i].bindings[0].path);
            StringAssert.StartsWith("F1~F10", SquadCommandHud.SelectKeys(center.Settings));
            RunState state = GameSession.Flow.Run.State;
            Assert.AreEqual(20, state.Army.Squads[0].Capacity, "런 부대도 씬과 같은 상한을 쓴다.");
            yield break;
        }

        [UnityTest]
        public IEnumerator 잡졸_20명이_찬_부대도_편성_창_카드에_모든_줄이_보인다()
        {
            RunState state = GameSession.Flow.Run.State;
            state.Wallet.AddGold(2000);
            Assert.IsTrue(facilities.Open(FacilityType.TrainingGround));
            yield return null;
            Assert.IsTrue(facilities.ChooseSquad(0));
            for (int guard = 0; guard < 30 && !state.Army.Squads[0].IsFull; guard++)
                Assert.IsTrue(facilities.TryRecruit(0), facilities.Message);
            Assert.AreEqual(20, state.Army.Squads[0].Members.Count);
            Assert.AreEqual(20, center.Squads[0].Members.Count, "씬 부대도 20명이다.");
            facilities.Close();
            yield return null;

            Assert.IsTrue(organization.Open());
            yield return null;
            Canvas.ForceUpdateCanvases();
            RectTransform card = organization.CardAt(0);
            Assert.IsNotNull(card);
            var list = (RectTransform)card.Find("List");
            Assert.AreEqual(21, list.childCount, "대장 줄 + 잡졸 20줄");
            RectTransform last = (RectTransform)list.GetChild(list.childCount - 1);
            Assert.GreaterOrEqual(Bottom(last), Bottom(card) - 0.5f, "마지막 줄이 카드 안에 있다.");

            var scroll = card.GetComponentInParent<ScrollRect>();
            Assert.IsTrue(scroll.vertical, "카드 영역은 세로로도 스크롤한다.");
            Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height, "21줄 카드는 보이는 영역보다 길다.");
            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            Assert.GreaterOrEqual(Bottom(last), Bottom(scroll.viewport) - 0.5f, "맨 아래로 스크롤하면 마지막 줄이 보인다.");
            Assert.LessOrEqual(Top(last), Top(scroll.viewport) + 0.5f);
        }

        static float Bottom(RectTransform rect) { var corners = new Vector3[4]; rect.GetWorldCorners(corners); return corners[0].y; }
        static float Top(RectTransform rect) { var corners = new Vector3[4]; rect.GetWorldCorners(corners); return corners[1].y; }
    }
}
