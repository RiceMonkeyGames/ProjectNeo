using ProjectNeo.Flow;
using ProjectNeo.Meta;
using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 타이틀 화면의 버튼 동작. 저장 슬롯을 고르고(META-01) 새 런을 시작해 게임 씬으로 가거나 게임을 끝낸다.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        const int NoSlot = -1;

        [SerializeField] SceneCatalog catalog;
        [SerializeField] TMP_Text[] slotLabels = new TMP_Text[0];
        [SerializeField] TMP_Text deleteLabel;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] RuntimeSystemFont font;

        [SerializeField] UnityEngine.UI.Button continueButton;
        [SerializeField] TMP_Text continueLabel;

        int selectedSlot;
        int deleteArmedSlot = NoSlot;
        int newGameArmedSlot = NoSlot;
        string notice = string.Empty;

        /// <summary>고른 슬롯에 이어 할 런 저장이 있는지(FLOW-02).</summary>
        public bool CanContinue => GameSession.HasRunSave(selectedSlot);
        /// <summary>새 게임을 한 번 눌러, 저장된 런을 버릴지 확인을 기다리는 중인지.</summary>
        public bool IsNewGameArmed => newGameArmedSlot != NoSlot;
        /// <summary>이어 하기 버튼에 지금 보이는 글. 버튼이 없으면 빈 문자열.</summary>
        public string ContinueText => continueLabel != null ? continueLabel.text : string.Empty;

        public void ConfigureContinue(UnityEngine.UI.Button button, TMP_Text label)
        {
            continueButton = button;
            continueLabel = label;
        }

        public int SelectedSlot => selectedSlot;
        /// <summary>삭제 버튼을 한 번 눌러 확인을 기다리는 중인지.</summary>
        public bool IsDeleteArmed => deleteArmedSlot != NoSlot;

        /// <summary>슬롯 버튼에 지금 보이는 글. 버튼이 없으면 빈 문자열.</summary>
        public string SlotText(int slot) =>
            slot >= 0 && slot < slotLabels.Length && slotLabels[slot] != null ? slotLabels[slot].text : string.Empty;

        public void Configure(SceneCatalog sceneCatalog) => catalog = sceneCatalog;

        public void ConfigureSlots(TMP_Text[] slots, TMP_Text delete, TMP_Text status, RuntimeSystemFont systemFont)
        {
            slotLabels = slots;
            deleteLabel = delete;
            statusLabel = status;
            font = systemFont;
        }

        void Start()
        {
            // 전투 중 잠근 커서가 남아 있지 않게 한다.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            selectedSlot = GameSession.SaveStore.GetLastSlot();
            Refresh();
        }

        public void SelectSlot(int slot)
        {
            if (!MetaSaveStore.IsValidSlot(slot)) return;
            selectedSlot = slot;
            deleteArmedSlot = NoSlot;
            newGameArmedSlot = NoSlot;
            notice = string.Empty;
            Refresh();
        }

        /// <summary>고른 슬롯의 저장된 런을 이어 한다(FLOW-02). 런 저장이 없으면 아무 일도 없다.</summary>
        public void ContinueGame()
        {
            if (SceneSwitcher.IsLoading) return;
            deleteArmedSlot = NoSlot;
            newGameArmedSlot = NoSlot;
            if (!GameSession.ContinueRun(selectedSlot))
            {
                notice = "There is no run to continue.";
                Refresh();
                return;
            }
            SceneSwitcher.Load(catalog.GameScene);
        }

        /// <summary>고른 슬롯을 지운다. 처음 누르면 확인을 기다리고, 같은 슬롯에서 다시 누르면 지운다.</summary>
        public void DeleteSelectedSlot()
        {
            bool korean = Korean;
            MetaSaveStore store = GameSession.SaveStore;
            newGameArmedSlot = NoSlot;
            bool hasRunFile = GameSession.RunSaves.Inspect(selectedSlot).Status != ProjectNeo.Run.RunSaveStatus.Empty;
            if (store.Inspect(selectedSlot).Status == MetaLoadStatus.Empty && !hasRunFile)
            {
                deleteArmedSlot = NoSlot;
                notice = korean ? "빈 슬롯입니다." : "This slot is empty.";
            }
            else if (deleteArmedSlot != selectedSlot)
            {
                deleteArmedSlot = selectedSlot;
                notice = string.Empty;
            }
            else
            {
                deleteArmedSlot = NoSlot;
                // 슬롯을 지우면 그 슬롯에서 진행 중이던 런 저장도 함께 지운다.
                bool runDeleted = GameSession.RunSaves.Delete(selectedSlot);
                notice = store.Delete(selectedSlot) && runDeleted
                    ? (korean ? $"슬롯 {selectedSlot + 1}을 지웠습니다." : $"Slot {selectedSlot + 1} deleted.")
                    : (korean ? "슬롯을 지우지 못했습니다." : "Could not delete the slot.");
            }
            Refresh();
        }

        public void StartGame()
        {
            if (SceneSwitcher.IsLoading) return;
            // 저장된 런이 있으면 한 번 더 눌러야 버리고 새로 시작한다(FLOW-02).
            if (GameSession.HasRunSave(selectedSlot) && newGameArmedSlot != selectedSlot)
            {
                newGameArmedSlot = selectedSlot;
                deleteArmedSlot = NoSlot;
                notice = "A run is in progress. Click New Game again to discard it.";
                Refresh();
                return;
            }
            if (GameSession.Flow.Phase != GamePhase.Title) return;
            GameSession.RunSaves.Delete(selectedSlot);
            if (!GameSession.Flow.StartRun()) return;
            GameSession.SelectSlot(selectedSlot);
            SceneSwitcher.Load(catalog.GameScene);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        bool Korean => font != null && font.Available;

        void Refresh()
        {
            bool korean = Korean;
            MetaSaveStore store = GameSession.SaveStore;
            for (int i = 0; i < slotLabels.Length; i++)
            {
                if (slotLabels[i] == null) continue;
                string mark = i == selectedSlot ? "> " : "  ";
                slotLabels[i].text = mark + Describe(i, store.Inspect(i), korean) + DescribeRun(i);
            }
            ProjectNeo.Run.RunLoadResult run = GameSession.RunSaves.Inspect(selectedSlot);
            if (continueButton != null) continueButton.interactable = run.HasRun;
            if (continueLabel != null)
                continueLabel.text = run.HasRun ? $"Continue (Stage {run.Snapshot.stageNumber})" : "Continue";
            if (deleteLabel != null)
                deleteLabel.text = deleteArmedSlot == selectedSlot
                    ? (korean ? $"한 번 더 누르면 슬롯 {selectedSlot + 1} 삭제" : $"Click again to delete slot {selectedSlot + 1}")
                    : (korean ? "고른 슬롯 삭제" : "Delete selected slot");
            if (statusLabel != null) statusLabel.text = notice;
        }

        // 슬롯 글 뒤에 붙이는 진행 중인 런 표시. 런 저장이 없으면 빈 문자열. 새 UI 글은 영어 하나로 쓴다(UI-02 결정).
        static string DescribeRun(int slot)
        {
            ProjectNeo.Run.RunLoadResult run = GameSession.RunSaves.Inspect(slot);
            return run.HasRun ? $" - Stage {run.Snapshot.stageNumber} in progress" : string.Empty;
        }

        static string Describe(int slot, MetaLoadResult result, bool korean)
        {
            string name = korean ? $"슬롯 {slot + 1}" : $"Slot {slot + 1}";
            MetaProgress progress = result.Progress;
            string summary = korean
                ? $"지휘관 Lv {progress.commanderLevel} · 해금 {progress.unlockedIds.Count}"
                : $"Commander Lv {progress.commanderLevel} - Unlocks {progress.unlockedIds.Count}";
            switch (result.Status)
            {
                case MetaLoadStatus.Loaded: return $"{name}: {summary}";
                case MetaLoadStatus.RestoredFromBackup:
                    return korean ? $"{name}: {summary} (백업에서 복구)" : $"{name}: {summary} (from backup)";
                case MetaLoadStatus.Unreadable:
                    return korean ? $"{name}: 손상됨 - 새로 시작" : $"{name}: Damaged - starts fresh";
                default: return korean ? $"{name}: 비어 있음" : $"{name}: Empty";
            }
        }
    }
}
