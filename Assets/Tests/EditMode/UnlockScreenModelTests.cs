using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Meta;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>런 사이 해금 화면 규칙(META-06): 두 번 눌러 사기, 거절 안내, 항목 글자.</summary>
    public class UnlockScreenModelTests
    {
        readonly List<Object> created = new List<Object>();
        UnlockCatalog catalog;
        MetaProgress progress;
        UnlockScreenModel model;

        [SetUp]
        public void SetUp()
        {
            UnlockTrackDefinition heroes = Track(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.Heroes, "Heroes",
                UnlockEntry.Create("hero_swordsman", 0, UnlockPayload.Hero("swordsman"), openedByDefault: true, displayName: "Swordsman"),
                UnlockEntry.Create("hero_knight", 2, UnlockPayload.Hero("knight"), displayName: "Knight"),
                UnlockEntry.Create("hero_paladin", 3, UnlockPayload.Hero("paladin"), displayName: "Paladin")));
            catalog = Track(UnlockCatalog.Create(heroes));
            progress = new MetaProgress { unlockPoints = 4 };
            model = new UnlockScreenModel(progress, catalog);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        UnlockEntry Entry(string id) => catalog.FindEntry(id);

        [Test]
        public void FirstClick_Arms_SecondClick_Buys()
        {
            Assert.AreEqual(UnlockClickResult.Armed, model.Click("hero_knight"));
            Assert.AreEqual(4, progress.unlockPoints, "첫 클릭은 아무것도 사지 않는다");
            Assert.AreEqual("한 번 더 클릭: Knight (2)", model.EntryText(Entry("hero_knight")));

            Assert.AreEqual(UnlockClickResult.Opened, model.Click("hero_knight"));

            Assert.AreEqual(2, progress.unlockPoints);
            Assert.AreEqual("Knight - 해금됨", model.EntryText(Entry("hero_knight")));
            Assert.AreEqual("Knight 해금.", model.Notice);
            Assert.AreEqual("해금 포인트: 2", model.PointsText);
            Assert.IsEmpty(model.ArmedEntryId);
        }

        [Test]
        public void ClickingAnotherEntry_MovesTheConfirmation()
        {
            progress.unlockPoints = 10;
            model.Click("hero_knight");
            UnlockTracks.TryOpen(progress, catalog, "hero_knight");
            progress.unlockPoints = 10;

            Assert.AreEqual(UnlockClickResult.Armed, model.Click("hero_paladin"), "다른 항목을 누르면 그 항목이 확인을 기다린다");
            Assert.AreEqual("hero_paladin", model.ArmedEntryId);
        }

        [Test]
        public void MissingPrerequisite_IsRefusedWithItsName()
        {
            Assert.AreEqual("Paladin - 3 (필요: Knight)", model.EntryText(Entry("hero_paladin")));

            Assert.AreEqual(UnlockClickResult.Refused, model.Click("hero_paladin"));

            Assert.AreEqual("먼저 Knight을(를) 해금하세요.", model.Notice);
            Assert.AreEqual(4, progress.unlockPoints);
            Assert.IsFalse(model.CanOpen(Entry("hero_paladin")));
        }

        [Test]
        public void NotEnoughPoints_IsRefusedAndDisarms()
        {
            model.Click("hero_knight");
            progress.unlockPoints = 1;

            Assert.AreEqual(UnlockClickResult.Refused, model.Click("hero_knight"));

            Assert.AreEqual("해금 포인트가 부족합니다: 2 필요, 1 보유.", model.Notice);
            Assert.IsEmpty(model.ArmedEntryId);
            Assert.AreEqual(1, progress.unlockPoints);
        }

        [Test]
        public void OpenedEntry_IsRefusedAsAlreadyUnlocked()
        {
            Assert.AreEqual("Swordsman - 해금됨", model.EntryText(Entry("hero_swordsman")));
            Assert.AreEqual(UnlockClickResult.Refused, model.Click("hero_swordsman"));
            Assert.AreEqual("Swordsman은(는) 이미 해금했습니다.", model.Notice);
        }

        [Test]
        public void Disarm_ClearsTheConfirmation()
        {
            model.Click("hero_knight");
            model.Disarm();

            Assert.AreEqual(UnlockClickResult.Armed, model.Click("hero_knight"), "다시 열면 처음부터 두 번 눌러야 한다");
            Assert.AreEqual(4, progress.unlockPoints);
        }
    }
}
