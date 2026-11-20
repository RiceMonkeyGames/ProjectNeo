using System.IO;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 씬이 바뀌어도 유지되는 게임 흐름의 보관처. 어느 씬에서 Play를 눌러도 같은 방법으로 흐름에 접근한다.
    /// 실행을 시작할 때마다 새로 만든다.
    /// </summary>
    public static class GameSession
    {
        const int NoSlot = -1;

        static GameFlow flow;
        static MetaProgress meta;
        static MetaSaveStore store;
        static int activeSlot = NoSlot;

        public static GameFlow Flow => flow ??= new GameFlow();

        /// <summary>
        /// 런이 끝나도 남는 메타 진행(지휘관 경험치·해금 포인트 등). 타이틀에서 슬롯을 고르면 그 파일의 내용이 되고(META-01),
        /// 슬롯 없이 Game 씬을 바로 실행하면 실행하는 동안만 남는다.
        /// </summary>
        public static MetaProgress Meta => meta ??= new MetaProgress();

        /// <summary>메타 저장 파일을 다루는 곳. 기본 위치는 persistentDataPath/Saves.</summary>
        public static MetaSaveStore SaveStore => store ??= new MetaSaveStore(Path.Combine(Application.persistentDataPath, "Saves"));

        /// <summary>지금 쓰는 저장 슬롯(0부터). 고르지 않았으면 -1이고 이때는 파일에 저장하지 않는다.</summary>
        public static int ActiveSlot => activeSlot;

        /// <summary>저장 위치를 바꾼다. 테스트가 실제 저장 폴더를 건드리지 않게 할 때 쓴다. 런 저장도 같은 폴더를 따라간다.</summary>
        public static void UseSaveStore(MetaSaveStore saveStore) { store = saveStore; runStore = null; }

        static RunSaveStore runStore;
        static RunSnapshot pendingRun;

        /// <summary>진행 중인 런의 저장 파일을 다루는 곳(FLOW-02). 메타 저장과 같은 폴더·같은 슬롯 번호를 쓴다.</summary>
        public static RunSaveStore RunSaves => runStore ??= new RunSaveStore(SaveStore.Directory);

        /// <summary>런을 언제 저장하고 지울지. 값을 바꾸면 그 뒤의 저장부터 따른다.</summary>
        public static RunSavePolicy RunSavePolicy { get; set; } = new RunSavePolicy();

        /// <summary>게임을 끄기 직전에 알린다. 게임 씬이 준비 단계의 런을 저장할 때 쓴다.</summary>
        public static event System.Action Quitting;

        /// <summary>이 슬롯에 이어 할 수 있는 런 저장이 있는지. 파일을 바꾸지 않는다.</summary>
        public static bool HasRunSave(int slot) => MetaSaveStore.IsValidSlot(slot) && RunSaves.Inspect(slot).HasRun;

        /// <summary>
        /// 슬롯의 저장된 런을 이어 한다: 메타 진행을 불러오고, 런 기록을 게임 씬에 넘길 준비를 하고, 그 스테이지의 준비 단계로 간다.
        /// 런 저장이 없거나 읽을 수 없거나 타이틀이 아니면 false이고 아무것도 바뀌지 않는다. 게임 씬은 부르는 쪽이 연다.
        /// </summary>
        public static bool ContinueRun(int slot)
        {
            if (!MetaSaveStore.IsValidSlot(slot) || Flow.Phase != GamePhase.Title) return false;
            RunLoadResult result = RunSaves.Load(slot);
            if (!result.HasRun) return false;
            if (result.Status == RunSaveStatus.RestoredFromBackup)
                Debug.LogWarning($"Run save slot {slot + 1} was unreadable; restored from backup.");
            SelectSlot(slot);
            pendingRun = result.Snapshot;
            return Flow.ResumeRun(result.Snapshot.stageNumber, result.Snapshot.ascensionLevel);
        }

        /// <summary>이어 하기로 넘겨받은 런 기록을 한 번 꺼낸다. 없으면 null. 게임 씬이 첫 준비 때 부른다.</summary>
        public static RunSnapshot TakePendingRun()
        {
            RunSnapshot snapshot = pendingRun;
            pendingRun = null;
            return snapshot;
        }

        /// <summary>런 기록을 지금 슬롯에 저장한다. 슬롯이 없으면(Game 씬을 바로 실행) 아무것도 하지 않는다.</summary>
        public static bool SaveRun(RunSnapshot snapshot)
        {
            if (activeSlot == NoSlot || snapshot == null) return false;
            if (RunSaves.Save(activeSlot, snapshot)) return true;
            Debug.LogWarning($"Run save slot {activeSlot + 1} could not be written: {RunSaves.LastError}");
            return false;
        }

        /// <summary>지금 슬롯의 런 저장을 지운다(패배로 런이 끝났을 때). 슬롯이 없으면 아무것도 하지 않는다.</summary>
        public static bool DeleteRunSave() => activeSlot != NoSlot && RunSaves.Delete(activeSlot);

        /// <summary>
        /// 지금 슬롯의 런 저장이 되살릴 수 없는 내용이면 따로 치운다. 다음부터 그 슬롯은 새 게임만 할 수 있다.
        /// </summary>
        public static bool DiscardRunSave() => activeSlot != NoSlot && RunSaves.Discard(activeSlot);

        /// <summary>슬롯을 골라 그 파일의 메타 진행을 불러온다. 이후 <see cref="SaveMeta"/>가 이 슬롯에 쓴다.</summary>
        public static MetaLoadStatus SelectSlot(int slot)
        {
            MetaLoadResult result = SaveStore.Load(slot);
            meta = result.Progress;
            activeSlot = slot;
            SaveStore.SetLastSlot(slot);
            if (result.Status == MetaLoadStatus.RestoredFromBackup)
                Debug.LogWarning($"Meta save slot {slot + 1} was unreadable; restored from backup.");
            else if (result.Status == MetaLoadStatus.Unreadable)
                Debug.LogWarning($"Meta save slot {slot + 1} and its backup were unreadable; starting fresh. Damaged files were kept in {SaveStore.Directory}.");
            return result.Status;
        }

        /// <summary>메타 진행을 지금 슬롯에 저장한다. 메타 값을 바꾼 직후에 부른다. 슬롯이 없으면 아무것도 하지 않는다.</summary>
        public static bool SaveMeta()
        {
            if (activeSlot == NoSlot || meta == null) return false;
            if (SaveStore.Save(activeSlot, meta)) return true;
            Debug.LogWarning($"Meta save slot {activeSlot + 1} could not be written: {SaveStore.LastError}");
            return false;
        }

        /// <summary>흐름을 처음 상태로 되돌리고 슬롯 선택을 푼다. 테스트에서도 쓴다. 저장 위치는 그대로 둔다.</summary>
        public static void Reset() { flow = null; meta = null; activeSlot = NoSlot; pendingRun = null; }

        /// <summary>실행 시작 때 자동으로 불린다. 저장 위치도 기본으로 되돌리고 종료 시 저장을 건다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            Reset();
            store = null;
            runStore = null;
            RunSavePolicy = new RunSavePolicy();
            Quitting = null;
            Application.quitting -= SaveOnQuit;
            Application.quitting += SaveOnQuit;
        }

        static void SaveOnQuit()
        {
            Quitting?.Invoke();
            SaveMeta();
        }
    }
}
