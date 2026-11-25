using System;
using System.IO;
using UnityEngine;

namespace ProjectNeo.Run
{
    /// <summary>런 저장 파일에 무엇이 들어 있었는지. 값은 메타 저장(META-01)과 같은 뜻이다.</summary>
    public enum RunSaveStatus
    {
        Empty = 0,
        Loaded = 1,
        RestoredFromBackup = 2,
        Unreadable = 3,
    }

    public readonly struct RunLoadResult
    {
        /// <summary>읽은 런 기록. 없거나 읽을 수 없으면 null.</summary>
        public RunSnapshot Snapshot { get; }
        public RunSaveStatus Status { get; }
        public bool HasRun => Snapshot != null;
        public RunLoadResult(RunSnapshot snapshot, RunSaveStatus status) { Snapshot = snapshot; Status = status; }
    }

    /// <summary>파일에 쓰는 겉봉. 버전으로 나중에 형식이 바뀐 파일을 가려낸다.</summary>
    [Serializable]
    public sealed class RunSaveFile
    {
        public int version;
        public RunSnapshot snapshot;
    }

    /// <summary>
    /// 진행 중인 런을 슬롯별 파일로 저장하고 읽는다(FLOW-02). 메타 저장과 같은 폴더·같은 슬롯 번호를 쓰고 규칙도 같다:
    /// 저장할 때마다 직전 파일을 백업으로 남기고, 본 파일이 깨지면 백업을 읽고, 둘 다 못 읽으면 깨진 파일을 따로 보관한다.
    /// 폴더 하나만 알고 씬이나 흐름은 모른다. ID를 에셋으로 바꾸는 일은 하지 않는다.
    /// </summary>
    public sealed class RunSaveStore
    {
        /// <summary>
        /// 2: Game 격자가 7.5m 칸으로 바뀌었다(SIEGE-18). 성 조각 칸 좌표가 3m 칸 기준인 1판은 읽지 않는다.
        /// 3: 기본 성이 26칸 고리로 넓어졌다(SIEGE-36). 13칸 성 좌표인 2판은 읽지 않는다(사용자 결정 2026-11-25: 옮기지 않고 새로 시작).
        /// </summary>
        public const int CurrentVersion = 3;
        /// <summary>읽을 수 있는 가장 오래된 버전.</summary>
        public const int MinimumVersion = 3;

        public string Directory { get; }
        /// <summary>마지막 저장·읽기에서 생긴 파일 오류. 없으면 빈 문자열.</summary>
        public string LastError { get; private set; } = string.Empty;

        public RunSaveStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Save directory is required.", nameof(directory));
            Directory = directory;
        }

        public string MainPath(int slot)
        {
            if (slot < 0) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(Directory, $"run_slot{slot + 1}.json");
        }
        public string BackupPath(int slot) => MainPath(slot) + ".bak";
        string TempPath(int slot) => MainPath(slot) + ".tmp";

        /// <summary>파일을 건드리지 않고 슬롯에 저장된 런을 본다. 타이틀 화면이 쓴다.</summary>
        public RunLoadResult Inspect(int slot)
        {
            string main = MainPath(slot), backup = BackupPath(slot);
            RunSnapshot snapshot = Read(main);
            if (snapshot != null) return new RunLoadResult(snapshot, RunSaveStatus.Loaded);
            snapshot = Read(backup);
            if (snapshot != null) return new RunLoadResult(snapshot, RunSaveStatus.RestoredFromBackup);
            bool anyFile = File.Exists(main) || File.Exists(backup);
            return new RunLoadResult(null, anyFile ? RunSaveStatus.Unreadable : RunSaveStatus.Empty);
        }

        /// <summary>
        /// 슬롯의 런을 읽는다. 본 파일이 깨졌으면 따로 보관하고 백업으로 본 파일을 되살린다. 백업도 못 읽으면 그것도 보관한다.
        /// </summary>
        public RunLoadResult Load(int slot)
        {
            LastError = string.Empty;
            RunLoadResult result = Inspect(slot);
            try
            {
                string main = MainPath(slot), backup = BackupPath(slot);
                if (result.Status == RunSaveStatus.RestoredFromBackup)
                {
                    Quarantine(main);
                    File.Copy(backup, main, true);
                }
                else if (result.Status == RunSaveStatus.Unreadable)
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
        public bool Save(int slot, RunSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string main = MainPath(slot), backup = BackupPath(slot), temp = TempPath(slot);
            LastError = string.Empty;
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.WriteAllText(temp, JsonUtility.ToJson(new RunSaveFile { version = CurrentVersion, snapshot = snapshot }, true));
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

        /// <summary>슬롯의 런 저장(본 파일과 백업)을 지운다. 따로 보관한 깨진 파일은 남긴다.</summary>
        public bool Delete(int slot)
        {
            LastError = string.Empty;
            try
            {
                File.Delete(MainPath(slot));
                File.Delete(BackupPath(slot));
                File.Delete(TempPath(slot));
                return true;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                LastError = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 읽히기는 하지만 되살릴 수 없는 런 저장(없어진 아이템 ID 등)을 지우지 않고 다른 이름으로 치운다.
        /// 이후 그 슬롯은 런 저장이 없는 것으로 보인다.
        /// </summary>
        public bool Discard(int slot)
        {
            LastError = string.Empty;
            try
            {
                Quarantine(MainPath(slot));
                Quarantine(BackupPath(slot));
                return true;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                LastError = exception.Message;
                return false;
            }
        }

        /// <summary>파일을 읽어 쓸 수 있는 런 기록이면 돌려준다. 없거나 깨졌거나 더 새 버전이면 null.</summary>
        static RunSnapshot Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                RunSaveFile file = JsonUtility.FromJson<RunSaveFile>(File.ReadAllText(path));
                if (file == null || file.version < MinimumVersion || file.version > CurrentVersion) return null;
                RunSnapshot snapshot = file.snapshot;
                // JsonUtility는 빈 내용에서도 빈 기록을 만들어 주므로 꼭 있어야 하는 값으로 가려낸다.
                if (snapshot == null || snapshot.stageNumber < 1 || snapshot.nextUnitId < 1) return null;
                if (snapshot.heroes == null || snapshot.heroes.Count == 0) return null;
                return snapshot;
            }
            catch (Exception exception) when (exception is ArgumentException || IsFileError(exception))
            {
                return null;
            }
        }

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
