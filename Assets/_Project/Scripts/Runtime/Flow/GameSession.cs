using System.IO;
using ProjectNeo.Meta;
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

        /// <summary>저장 위치를 바꾼다. 테스트가 실제 저장 폴더를 건드리지 않게 할 때 쓴다.</summary>
        public static void UseSaveStore(MetaSaveStore saveStore) => store = saveStore;

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
        public static void Reset() { flow = null; meta = null; activeSlot = NoSlot; }

        /// <summary>실행 시작 때 자동으로 불린다. 저장 위치도 기본으로 되돌리고 종료 시 저장을 건다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            Reset();
            store = null;
            Application.quitting -= SaveOnQuit;
            Application.quitting += SaveOnQuit;
        }

        static void SaveOnQuit() => SaveMeta();
    }
}
