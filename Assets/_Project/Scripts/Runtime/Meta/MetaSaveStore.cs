using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProjectNeo.Meta
{
    /// <summary>
    /// 메타 진행을 슬롯별 파일로 저장하고 읽는다(META-01). 폴더 하나만 알고 씬이나 흐름은 모른다.
    /// 저장할 때마다 직전 파일을 백업으로 남기고, 본 파일이 깨지면 백업을 읽는다.
    /// 둘 다 읽을 수 없으면 깨진 파일을 따로 보관하고 새 진행을 돌려준다. 런 중간 상태는 저장하지 않는다.
    /// </summary>
    public sealed class MetaSaveStore
    {
        public const int SlotCount = 3;
        /// <summary>2: 해금 트랙의 연 항목 id 추가(META-05). 1짜리 파일도 그대로 읽는다.</summary>
        public const int CurrentVersion = 2;

        const string LastSlotFile = "last_slot.txt";

        public string Directory { get; }
        /// <summary>마지막 저장·읽기에서 생긴 파일 오류. 없으면 빈 문자열.</summary>
        public string LastError { get; private set; } = string.Empty;

        public MetaSaveStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Save directory is required.", nameof(directory));
            Directory = directory;
        }

        public static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

        public string MainPath(int slot) => Path.Combine(Directory, $"meta_slot{Checked(slot) + 1}.json");
        public string BackupPath(int slot) => MainPath(slot) + ".bak";
        string TempPath(int slot) => MainPath(slot) + ".tmp";

        /// <summary>파일을 건드리지 않고 슬롯에 무엇이 들어 있는지 본다. 슬롯 선택 화면이 쓴다.</summary>
        public MetaLoadResult Inspect(int slot)
        {
            string main = MainPath(slot), backup = BackupPath(slot);
            MetaProgress progress = Read(main);
            if (progress != null) return new MetaLoadResult(progress, MetaLoadStatus.Loaded);
            progress = Read(backup);
            if (progress != null) return new MetaLoadResult(progress, MetaLoadStatus.RestoredFromBackup);
            bool anyFile = File.Exists(main) || File.Exists(backup);
            return new MetaLoadResult(new MetaProgress(), anyFile ? MetaLoadStatus.Unreadable : MetaLoadStatus.Empty);
        }

        /// <summary>
        /// 슬롯을 읽는다. 본 파일이 깨졌으면 따로 보관하고 백업으로 본 파일을 되살린다.
        /// 백업도 못 읽으면 그것도 보관하고 새 진행을 돌려준다.
        /// </summary>
        public MetaLoadResult Load(int slot)
        {
            LastError = string.Empty;
            MetaLoadResult result = Inspect(slot);
            try
            {
                string main = MainPath(slot), backup = BackupPath(slot);
                if (result.Status == MetaLoadStatus.RestoredFromBackup)
                {
                    Quarantine(main);
                    File.Copy(backup, main, true);
                }
                else if (result.Status == MetaLoadStatus.Unreadable)
                {
                    Quarantine(main);
                    Quarantine(backup);
                }
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                LastError = exception.Message;
            }
            return result;
        }

        /// <summary>슬롯에 저장한다. 임시 파일에 다 쓴 뒤 바꿔치기해서 쓰다 끊겨도 기존 파일이 남는다.</summary>
        public bool Save(int slot, MetaProgress progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            string main = MainPath(slot), backup = BackupPath(slot), temp = TempPath(slot);
            LastError = string.Empty;
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.WriteAllText(temp, JsonUtility.ToJson(new MetaSaveFile { version = CurrentVersion, progress = progress }, true));
                if (File.Exists(main))
                {
                    // 읽을 수 있는 파일만 백업으로 남긴다. 깨진 본 파일이 멀쩡한 백업을 덮으면 안 된다.
                    if (Read(main) != null) File.Copy(main, backup, true);
                    File.Delete(main);
                }
                File.Move(temp, main);
                return true;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                LastError = exception.Message;
                return false;
            }
        }

        /// <summary>슬롯의 본 파일과 백업을 지운다. 따로 보관한 깨진 파일은 남긴다.</summary>
        public bool Delete(int slot)
        {
            string main = MainPath(slot), backup = BackupPath(slot), temp = TempPath(slot);
            LastError = string.Empty;
            try
            {
                File.Delete(main);
                File.Delete(backup);
                File.Delete(temp);
                return true;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                LastError = exception.Message;
                return false;
            }
        }

        /// <summary>마지막으로 고른 슬롯. 기록이 없거나 잘못됐으면 0.</summary>
        public int GetLastSlot()
        {
            try
            {
                string path = Path.Combine(Directory, LastSlotFile);
                if (File.Exists(path) && int.TryParse(File.ReadAllText(path).Trim(), out int slot) && IsValidSlot(slot)) return slot;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                LastError = exception.Message;
            }
            return 0;
        }

        public void SetLastSlot(int slot)
        {
            Checked(slot);
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.WriteAllText(Path.Combine(Directory, LastSlotFile), slot.ToString());
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                LastError = exception.Message;
            }
        }

        static int Checked(int slot)
        {
            if (!IsValidSlot(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
            return slot;
        }

        /// <summary>파일을 읽어 쓸 수 있는 진행이면 돌려준다. 없거나 깨졌거나 더 새 버전이면 null.</summary>
        static MetaProgress Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                MetaSaveFile file = JsonUtility.FromJson<MetaSaveFile>(File.ReadAllText(path));
                if (file == null || file.version < 1 || file.version > CurrentVersion) return null;
                MetaProgress progress = file.progress;
                if (progress == null || progress.commanderLevel < 1) return null;
                if (progress.commanderXp < 0 || progress.perkPoints < 0 || progress.unlockPoints < 0) return null;
                progress.unlockedIds ??= new List<string>();
                progress.openedEntryIds ??= new List<string>();
                return progress;
            }
            catch (Exception exception) when (exception is ArgumentException || IsFileError(exception))
            {
                return null;
            }
        }

        /// <summary>읽을 수 없는 파일을 지우지 않고 다른 이름으로 옮겨 둔다.</summary>
        static void Quarantine(string path)
        {
            if (!File.Exists(path)) return;
            string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string target = $"{path}.corrupt-{stamp}";
            for (int i = 1; File.Exists(target); i++) target = $"{path}.corrupt-{stamp}-{i}";
            File.Move(path, target);
        }

        static bool IsFileError(Exception exception) =>
            exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException;
    }
}
