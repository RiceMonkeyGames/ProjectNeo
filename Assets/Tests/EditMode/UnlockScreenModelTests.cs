using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Core;
using ProjectNeo.Meta;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// 런 사이 해금 화면 규칙(META-06, UI-06): 트랙을 골라 한 번 누르면 트랙 비용으로 후보 하나가 무작위로 열린다.
    /// 후보는 선행이 모두 열린 미해금 항목뿐이다. 거절 안내와 글자도 본다.
    /// </summary>
    public class UnlockScreenModelTests
    {
        sealed class FixedRandom : IRandomSource
        {
            readonly int value;
            public FixedRandom(int value) { this.value = value; }
            public int NextInt(int maxExclusive) => Mathf.Clamp(value, 0, maxExclusive - 1);
            public float NextFloat() => 0f;
        }

        readonly List<Object> created = new List<Object>();
        UnlockCatalog catalog;
        MetaProgress progress;
        UnlockScreenModel model;

        [SetUp]
        public void SetUp()
        {
            // 영웅: 일자형(기사 -> 성기사). 보너스: 선행 없는 항목 3개(진짜 무작위 후보).
            UnlockTrackDefinition heroes = Track(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.Heroes, "Heroes",
                UnlockEntry.Create("hero_swordsman", 0, UnlockPayload.Hero("swordsman"), openedByDefault: true, displayName: "Swordsman"),
                UnlockEntry.Create("hero_knight", 2, UnlockPayload.Hero("knight"), displayName: "Knight"),
                UnlockEntry.Create("hero_paladin", 3, UnlockPayload.Hero("paladin"), displayName: "Paladin")));
            UnlockTrackDefinition bonuses = Track(UnlockTrackDefinition.Create(UnlockTrackCategory.StartingBonuses, "Bonuses", new[]
            {
                UnlockEntry.Create("bonus_a", 5, UnlockPayload.StartingGold(10), displayName: "Gold A"),
                UnlockEntry.Create("bonus_b", 5, UnlockPayload.StartingGold(20), displayName: "Gold B"),
                UnlockEntry.Create("bonus_c", 5, UnlockPayload.StartingGold(30), displayName: "Gold C"),
            }).WithDrawCost(2));
            catalog = Track(UnlockCatalog.Create(heroes, bonuses));
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
        public void OneClick_OpensTheOnlyCandidate_ForTheTrackCost()
        {
            Assert.AreEqual(0, model.SelectedTrackIndex, "처음에는 첫 트랙을 보여 준다");
            Assert.AreEqual(UnlockEntryState.Candidate, model.EntryState(Entry("hero_knight")));
            Assert.AreEqual(UnlockEntryState.Locked, model.EntryState(Entry("hero_paladin")), "선행이 안 열린 항목은 나오지 않는다");
            Assert.AreEqual("Knight 먼저", model.EntryStateText(Entry("hero_paladin")));

            Assert.AreEqual(UnlockClickResult.Opened, model.Draw(new FixedRandom(0)));

            Assert.AreEqual(3, progress.unlockPoints, "트랙 비용(기본 1)만 낸다. 항목 비용은 쓰지 않는다");
            CollectionAssert.Contains(progress.openedEntryIds, "hero_knight");
            Assert.AreEqual("hero_knight", model.LastOpenedId);
            Assert.AreEqual("Knight 해금!", model.Notice);
            Assert.AreEqual("해금됨", model.EntryStateText(Entry("hero_knight")));
            Assert.AreEqual(UnlockEntryState.Candidate, model.EntryState(Entry("hero_paladin")), "선행이 열리면 다음 후보가 된다");
            Assert.AreEqual("보유 해금 포인트  3", model.PointsText);
            StringAssert.StartsWith("Heroes", model.TrackTitleText);
            StringAssert.Contains("2/3 해금", model.TrackTitleText);
        }

        [Test]
        public void Draw_PicksAmongAllCandidates_WithTheRandomSource()
        {
            model.SelectTrack(1);
            progress.unlockPoints = 6;
            Assert.AreEqual(3, UnlockTracks.DrawCandidates(progress, catalog, model.SelectedTrack).Count);
            StringAssert.Contains("2 포인트", model.DrawButtonText);

            Assert.AreEqual(UnlockClickResult.Opened, model.Draw(new FixedRandom(2)));
            Assert.AreEqual("bonus_c", model.LastOpenedId, "난수 2는 세 번째 후보");
            Assert.AreEqual(4, progress.unlockPoints, "트랙 비용 2");

            Assert.AreEqual(UnlockClickResult.Opened, model.Draw(new FixedRandom(0)));
            Assert.AreEqual("bonus_a", model.LastOpenedId, "이미 열린 항목은 후보에서 빠진다");
            Assert.AreEqual(UnlockClickResult.Opened, model.Draw(new FixedRandom(0)));
            Assert.AreEqual("bonus_b", model.LastOpenedId);
            Assert.AreEqual(0, progress.unlockPoints);
        }

        [Test]
        public void NothingLeft_IsRefusedAndSpendsNothing()
        {
            progress.unlockPoints = 10;
            model.Draw(new FixedRandom(0));
            model.Draw(new FixedRandom(0));
            Assert.AreEqual("모두 해금함", model.DrawButtonText);
            Assert.IsFalse(model.CanDraw);

            Assert.AreEqual(UnlockClickResult.Refused, model.Draw(new FixedRandom(0)));

            Assert.AreEqual("Heroes에서 더 해금할 것이 없습니다.", model.Notice);
            Assert.AreEqual(8, progress.unlockPoints);
            Assert.IsEmpty(model.LastOpenedId);
        }

        [Test]
        public void NotEnoughPoints_IsRefusedAndSpendsNothing()
        {
            model.SelectTrack(1);
            progress.unlockPoints = 1;
            Assert.IsFalse(model.CanDraw);

            Assert.AreEqual(UnlockClickResult.Refused, model.Draw(new FixedRandom(0)));

            Assert.AreEqual("해금 포인트가 부족합니다: 2 필요, 1 보유.", model.Notice);
            Assert.AreEqual(1, progress.unlockPoints);
            Assert.IsEmpty(progress.openedEntryIds);
        }

        [Test]
        public void StraightTrack_KeepsTheEntryThumbnail()
        {
            var texture = Track(new Texture2D(4, 4));
            var sprite = Track(Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f));
            UnlockTrackDefinition track = Track(UnlockTrackDefinition.CreateStraight(UnlockTrackCategory.Heroes, "Icons",
                UnlockEntry.Create("a", 0, UnlockPayload.Hero("a")).WithIcon(sprite),
                UnlockEntry.Create("b", 0, UnlockPayload.Hero("b"))));

            Assert.AreSame(sprite, track.Entries[0].Icon, "선행을 이어 붙인 복사본도 썸네일을 지닌다");
            Assert.IsNull(track.Entries[1].Icon, "썸네일이 없으면 null(화면은 빈 칸)");
        }

        [Test]
        public void SelectTrack_ClearsTheNotice_AndIgnoresBadIndexes()
        {
            model.Draw(new FixedRandom(0));
            model.SelectTrack(1);
            Assert.AreEqual(1, model.SelectedTrackIndex);
            Assert.IsEmpty(model.Notice);
            Assert.IsEmpty(model.LastOpenedId);

            model.SelectTrack(5);
            model.SelectTrack(-1);
            Assert.AreEqual(1, model.SelectedTrackIndex, "없는 번호는 무시한다");
            StringAssert.Contains("0/3 해금", model.TrackText(catalog.Tracks[1]));
        }
    }
}
