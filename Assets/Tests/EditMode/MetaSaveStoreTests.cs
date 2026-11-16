using System;
using System.IO;
using NUnit.Framework;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>메타 진행 파일 저장(META-01). 임시 폴더만 쓰고 실제 저장 폴더는 건드리지 않는다.</summary>
    public class MetaSaveStoreTests
    {
        string directory;
        MetaSaveStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ProjectNeoMetaSaveTests_" + Guid.NewGuid().ToString("N"));
            store = new MetaSaveStore(directory);
            GameSession.Reset();
            GameSession.UseSaveStore(store);
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.Reset();
            GameSession.UseSaveStore(null);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        static MetaProgress Sample(int level = 4)
        {
            var progress = new MetaProgress { commanderLevel = level, commanderXp = 30, perkPoints = 3, unlockPoints = 20 };
            progress.unlockedIds.Add("archer");
            progress.guaranteedNextRunIds.Add("archer");
            return progress;
        }

        [Test]
        public void EmptySlot_LoadsFreshProgressWithoutCreatingFiles()
        {
            MetaLoadResult result = store.Load(0);

            Assert.AreEqual(MetaLoadStatus.Empty, result.Status);
            Assert.AreEqual(1, result.Progress.commanderLevel);
            Assert.IsFalse(File.Exists(store.MainPath(0)));
        }

        [Test]
        public void SaveThenLoad_RestoresEveryField()
        {
            Assert.IsTrue(store.Save(1, Sample()));

            MetaLoadResult result = new MetaSaveStore(directory).Load(1);

            Assert.AreEqual(MetaLoadStatus.Loaded, result.Status);
            Assert.AreEqual(4, result.Progress.commanderLevel);
            Assert.AreEqual(30, result.Progress.commanderXp);
            Assert.AreEqual(3, result.Progress.perkPoints);
            Assert.AreEqual(20, result.Progress.unlockPoints);
            CollectionAssert.AreEqual(new[] { "archer" }, result.Progress.unlockedIds);
            CollectionAssert.AreEqual(new[] { "archer" }, result.Progress.guaranteedNextRunIds);
            Assert.IsFalse(File.Exists(store.MainPath(1) + ".tmp"), "임시 파일이 남으면 안 된다.");
        }

        [Test]
        public void Slots_AreIndependent()
        {
            store.Save(0, Sample(2));
            store.Save(2, Sample(7));

            Assert.AreEqual(2, store.Load(0).Progress.commanderLevel);
            Assert.AreEqual(MetaLoadStatus.Empty, store.Load(1).Status);
            Assert.AreEqual(7, store.Load(2).Progress.commanderLevel);
        }

        [Test]
        public void SecondSave_KeepsPreviousFileAsBackup()
        {
            store.Save(0, Sample(2));
            store.Save(0, Sample(3));

            Assert.IsTrue(File.Exists(store.BackupPath(0)));
            File.Delete(store.MainPath(0));
            MetaLoadResult result = store.Load(0);
            Assert.AreEqual(MetaLoadStatus.RestoredFromBackup, result.Status);
            Assert.AreEqual(2, result.Progress.commanderLevel);
        }

        [Test]
        public void CorruptMain_RestoresBackupAndKeepsDamagedFile()
        {
            store.Save(0, Sample(2));
            store.Save(0, Sample(3));
            File.WriteAllText(store.MainPath(0), "{ \"version\": 1, \"progress\": { \"commanderLev");

            MetaLoadResult result = store.Load(0);

            Assert.AreEqual(MetaLoadStatus.RestoredFromBackup, result.Status);
            Assert.AreEqual(2, result.Progress.commanderLevel);
            Assert.AreEqual(1, Directory.GetFiles(directory, "*.corrupt-*").Length, "깨진 파일은 지우지 않고 보관한다.");
            Assert.AreEqual(MetaLoadStatus.Loaded, store.Load(0).Status, "본 파일이 백업으로 되살아나야 한다.");
        }

        [Test]
        public void CorruptMainAndBackup_StartsFreshAndKeepsBothFiles()
        {
            store.Save(0, Sample(2));
            store.Save(0, Sample(3));
            File.WriteAllText(store.MainPath(0), "not json");
            File.WriteAllText(store.BackupPath(0), string.Empty);

            MetaLoadResult result = store.Load(0);

            Assert.AreEqual(MetaLoadStatus.Unreadable, result.Status);
            Assert.AreEqual(1, result.Progress.commanderLevel);
            Assert.AreEqual(2, Directory.GetFiles(directory, "*.corrupt-*").Length);
            Assert.AreEqual(MetaLoadStatus.Empty, store.Load(0).Status);
        }

        [Test]
        public void CorruptMain_IsNotCopiedOverGoodBackupOnSave()
        {
            store.Save(0, Sample(2));
            store.Save(0, Sample(3));
            File.WriteAllText(store.MainPath(0), "not json");

            store.Save(0, Sample(5));

            File.Delete(store.MainPath(0));
            Assert.AreEqual(2, store.Load(0).Progress.commanderLevel);
        }

        [Test]
        public void NewerVersionOrInvalidValues_AreUnreadable()
        {
            Directory.CreateDirectory(directory);
            var newer = new MetaSaveFile { version = MetaSaveStore.CurrentVersion + 1, progress = Sample() };
            File.WriteAllText(store.MainPath(0), JsonUtility.ToJson(newer));
            var negative = new MetaSaveFile { version = MetaSaveStore.CurrentVersion, progress = Sample() };
            negative.progress.unlockPoints = -1;
            File.WriteAllText(store.MainPath(1), JsonUtility.ToJson(negative));
            File.WriteAllText(store.MainPath(2), "{}");

            for (int slot = 0; slot < MetaSaveStore.SlotCount; slot++)
                Assert.AreEqual(MetaLoadStatus.Unreadable, store.Inspect(slot).Status, "slot " + slot);
        }

        [Test]
        public void Inspect_DoesNotMoveOrRewriteFiles()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(store.MainPath(0), "not json");

            Assert.AreEqual(MetaLoadStatus.Unreadable, store.Inspect(0).Status);

            Assert.AreEqual("not json", File.ReadAllText(store.MainPath(0)));
            Assert.AreEqual(1, Directory.GetFiles(directory).Length);
        }

        [Test]
        public void Delete_RemovesMainAndBackupOnly()
        {
            store.Save(0, Sample(2));
            store.Save(0, Sample(3));
            store.Save(1, Sample(6));

            Assert.IsTrue(store.Delete(0));

            Assert.AreEqual(MetaLoadStatus.Empty, store.Inspect(0).Status);
            Assert.IsFalse(File.Exists(store.BackupPath(0)));
            Assert.AreEqual(6, store.Inspect(1).Progress.commanderLevel);
        }

        [Test]
        public void LastSlot_IsRememberedAndDefaultsToFirst()
        {
            Assert.AreEqual(0, store.GetLastSlot());
            store.SetLastSlot(2);
            Assert.AreEqual(2, new MetaSaveStore(directory).GetLastSlot());
            File.WriteAllText(Path.Combine(directory, "last_slot.txt"), "9");
            Assert.AreEqual(0, store.GetLastSlot());
        }

        [Test]
        public void InvalidSlot_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => store.Load(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(MetaSaveStore.SlotCount, Sample()));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.Delete(MetaSaveStore.SlotCount));
        }

        [Test]
        public void Session_WithoutSlot_NeverWrites()
        {
            GameSession.Meta.unlockPoints = 5;

            Assert.IsFalse(GameSession.SaveMeta());

            Assert.AreEqual(-1, GameSession.ActiveSlot);
            Assert.IsFalse(Directory.Exists(directory));
        }

        [Test]
        public void Session_SelectSlot_LoadsThenSavesToThatSlot()
        {
            store.Save(1, Sample(4));

            Assert.AreEqual(MetaLoadStatus.Loaded, GameSession.SelectSlot(1));
            Assert.AreEqual(4, GameSession.Meta.commanderLevel);
            GameSession.Meta.unlockPoints += 7;
            Assert.IsTrue(GameSession.SaveMeta());
            GameSession.Reset();

            Assert.AreEqual(1, GameSession.Meta.commanderLevel, "Reset 뒤에는 슬롯 없는 새 진행이어야 한다.");
            Assert.AreEqual(27, store.Load(1).Progress.unlockPoints);
            Assert.AreEqual(MetaLoadStatus.Empty, store.Inspect(0).Status);
            Assert.AreEqual(1, store.GetLastSlot());
        }

        [Test]
        public void Session_SelectDamagedSlot_WarnsAndStartsFresh()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(store.MainPath(0), "not json");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("starting fresh"));

            Assert.AreEqual(MetaLoadStatus.Unreadable, GameSession.SelectSlot(0));

            Assert.AreEqual(1, GameSession.Meta.commanderLevel);
            Assert.AreEqual(0, GameSession.ActiveSlot);
        }
    }
}
