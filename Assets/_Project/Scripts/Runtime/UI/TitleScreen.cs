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
        [SerializeField] SceneCatalog catalog;
        [SerializeField, Tooltip("Detail text of each slot card (the slot name is a static label on the card).")]
        TMP_Text[] slotLabels = new TMP_Text[0];
        [SerializeField, Tooltip("UI-10: highlight frame of each slot card, shown on the selected slot.")]
        GameObject[] slotHighlights = new GameObject[0];
        [SerializeField] TMP_Text deleteLabel;
        [SerializeField] TMP_Text statusLabel;

        [SerializeField] UnityEngine.UI.Button continueButton;
        [SerializeField] TMP_Text continueLabel;
        [SerializeField] UnlockScreen unlockScreen;
        [SerializeField] AscensionPicker ascensionPicker;
        [SerializeField, Tooltip("UI-06: unlock points of the selected slot on the expedition screen's unlock card.")]
        TMP_Text expeditionPointsLabel;
        [Header("Screens (UI-05)")]
        [SerializeField, Tooltip("Main menu shown first: logo, start, quit.")] GameObject mainMenu;
        [SerializeField, Tooltip("Slot window opened from the main menu: slots, continue, new game, unlocks, delete, back.")]
        GameObject slotMenu;

        int selectedSlot;
        string notice = string.Empty;

        /// <summary>고른 슬롯에 이어 할 런 저장이 있는지(FLOW-02).</summary>
        public bool CanContinue => GameSession.HasRunSave(selectedSlot);
        /// <summary>이어 하기 버튼에 지금 보이는 글. 버튼이 없으면 빈 문자열.</summary>
        public string ContinueText => continueLabel != null ? continueLabel.text : string.Empty;

        public void ConfigureContinue(UnityEngine.UI.Button button, TMP_Text label)
        {
            continueButton = button;
            continueLabel = label;
        }

        public void ConfigureUnlocks(UnlockScreen screen) => unlockScreen = screen;
        public UnlockScreen Unlocks => unlockScreen;

        /// <summary>고른 슬롯의 런 사이 해금 창을 연다(META-06).</summary>
        public void OpenUnlocks()
        {
            if (SceneSwitcher.IsLoading || unlockScreen == null) return;
            notice = unlockScreen.Open(selectedSlot) ? string.Empty : "해금을 사용할 수 없습니다.";
            Refresh();
        }

        public int SelectedSlot => selectedSlot;
        /// <summary>초기화 버튼에 보이는 글(UI-10). 버튼이 없으면 빈 문자열.</summary>
        public string DeleteText => deleteLabel != null ? deleteLabel.text : string.Empty;

        /// <summary>슬롯 버튼에 지금 보이는 글. 버튼이 없으면 빈 문자열.</summary>
        public string SlotText(int slot) =>
            slot >= 0 && slot < slotLabels.Length && slotLabels[slot] != null ? slotLabels[slot].text : string.Empty;

        public void Configure(SceneCatalog sceneCatalog) => catalog = sceneCatalog;

        public void ConfigureMenus(GameObject main, GameObject slots)
        {
            mainMenu = main;
            slotMenu = slots;
        }

        /// <summary>슬롯 창이 열려 있는지. 메뉴가 연결되지 않은 옛 씬에서는 항상 true(한 화면에 다 있음).</summary>
        public bool IsSlotMenuOpen => slotMenu == null || slotMenu.activeSelf;

        /// <summary>메인 화면에서 "게임 시작"을 누르면 슬롯 창으로 넘어간다(UI-05).</summary>
        public void OpenSlotMenu()
        {
            if (SceneSwitcher.IsLoading) return;
            ShowSlotMenu(true);
            notice = string.Empty;
            Refresh();
        }

        /// <summary>슬롯 창에서 "뒤로"를 누르면 메인 화면으로 돌아간다. 열린 승천·해금 창도 닫는다.</summary>
        public void BackToMain()
        {
            if (ascensionPicker != null) ascensionPicker.Close();
            if (unlockScreen != null && unlockScreen.IsOpen) unlockScreen.Close();
            notice = string.Empty;
            ShowSlotMenu(false);
            Refresh();
        }

        void ShowSlotMenu(bool open)
        {
            if (mainMenu != null) mainMenu.SetActive(!open);
            if (slotMenu != null) slotMenu.SetActive(open);
        }

        public void ConfigureSlots(TMP_Text[] slots, TMP_Text delete, TMP_Text status)
        {
            slotLabels = slots;
            deleteLabel = delete;
            statusLabel = status;
        }

        public void ConfigureSlotHighlights(GameObject[] highlights) => slotHighlights = highlights;

        /// <summary>이 슬롯 카드의 선택 테두리가 켜져 있는지(UI-10). 테두리가 없으면 false.</summary>
        public bool IsSlotHighlighted(int slot) =>
            slot >= 0 && slot < slotHighlights.Length && slotHighlights[slot] != null && slotHighlights[slot].activeSelf;

        void Start()
        {
            // 전투 중 잠근 커서가 남아 있지 않게 한다.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            selectedSlot = GameSession.SaveStore.GetLastSlot();
            if (unlockScreen != null) unlockScreen.Closed += Refresh;
            // 런이 끝나고 돌아왔으면 같은 슬롯의 출정 준비 화면을 바로 연다. 처음 켰을 때는 메인 화면부터.
            bool returnedFromRun = GameSession.Flow.TakeReturnedFromRunEnd();
            if (returnedFromRun && MetaSaveStore.IsValidSlot(GameSession.ActiveSlot)) selectedSlot = GameSession.ActiveSlot;
            ShowSlotMenu(returnedFromRun);
            Refresh();
            if (returnedFromRun) OpenExpedition();
        }

        void OnDestroy()
        {
            if (unlockScreen != null) unlockScreen.Closed -= Refresh;
        }

        public void SelectSlot(int slot)
        {
            if (!MetaSaveStore.IsValidSlot(slot)) return;
            selectedSlot = slot;
            if (ascensionPicker != null) ascensionPicker.Close();
            notice = string.Empty;
            Refresh();
        }

        /// <summary>고른 슬롯의 저장된 런을 이어 한다(FLOW-02). 런 저장이 없으면 아무 일도 없다.</summary>
        public void ContinueGame()
        {
            if (SceneSwitcher.IsLoading) return;
            if (!GameSession.ContinueRun(selectedSlot))
            {
                notice = "이어 할 런이 없습니다.";
                Refresh();
                return;
            }
            SceneSwitcher.Load(catalog.GameScene);
        }

        /// <summary>고른 슬롯을 바로 초기화(삭제)한다. 확인을 한 번 더 묻지 않는다(UI-10, 사용자 결정).</summary>
        public void DeleteSelectedSlot()
        {
            MetaSaveStore store = GameSession.SaveStore;
            bool hasRunFile = GameSession.RunSaves.Inspect(selectedSlot).Status != ProjectNeo.Run.RunSaveStatus.Empty;
            if (store.Inspect(selectedSlot).Status == MetaLoadStatus.Empty && !hasRunFile)
            {
                notice = "빈 슬롯입니다.";
            }
            else
            {
                // 슬롯을 지우면 그 슬롯에서 진행 중이던 런 저장도 함께 지운다.
                bool runDeleted = GameSession.RunSaves.Delete(selectedSlot);
                notice = store.Delete(selectedSlot) && runDeleted
                    ? $"슬롯 {selectedSlot + 1}을(를) 초기화했습니다."
                    : "슬롯을 초기화하지 못했습니다.";
            }
            Refresh();
        }

        public void StartGame()
        {
            if (SceneSwitcher.IsLoading) return;
            // 한 번 누르면 바로 출정 준비로 간다(UI-10, 사용자 결정). 저장된 런은 출정할 때(BeginRun) 버린다.
            if (GameSession.Flow.Phase != GamePhase.Title) return;
            if (OpenExpedition()) return;
            BeginRun(0);
        }

        /// <summary>
        /// 고른 슬롯의 출정 준비 화면을 연다(UI-05): 런 사이 해금, 승천 단계 선택, 출정. 열린 단계가 없으면 기본 게임만 고를 수 있다.
        /// 창이 없으면 false(옛 씬에서는 바로 시작).
        /// </summary>
        public bool OpenExpedition()
        {
            if (SceneSwitcher.IsLoading || ascensionPicker == null || ascensionPicker.Table == null) return false;
            int highest = AscensionProgress.HighestUnlocked(GameSession.SaveStore.Inspect(selectedSlot).Progress,
                ascensionPicker.Table.MaxLevel);
            if (!ascensionPicker.Open(highest, BeginRun)) return false;
            // 팝업이 아니라 다음 단계처럼 보이게 메인·슬롯 화면을 숨긴다.
            if (mainMenu != null) mainMenu.SetActive(false);
            if (slotMenu != null) slotMenu.SetActive(false);
            notice = string.Empty;
            Refresh();
            return true;
        }

        public bool IsExpeditionOpen => ascensionPicker != null && ascensionPicker.IsOpen;

        /// <summary>출정 준비 화면의 "뒤로": 화면을 닫고 슬롯 창으로 돌아간다. 열린 해금 창도 닫는다.</summary>
        public void CloseExpedition()
        {
            if (unlockScreen != null && unlockScreen.IsOpen) unlockScreen.Close();
            if (ascensionPicker != null) ascensionPicker.Close();
            notice = string.Empty;
            ShowSlotMenu(true);
            Refresh();
        }

        /// <summary>고른 슬롯에서 이 승천 단계로 새 런을 시작해 게임 씬으로 간다. 슬롯의 저장된 런은 버린다.</summary>
        public void BeginRun(int ascensionLevel)
        {
            if (SceneSwitcher.IsLoading || GameSession.Flow.Phase != GamePhase.Title) return;
            GameSession.RunSaves.Delete(selectedSlot);
            if (!GameSession.Flow.StartRun(null, ascensionLevel)) return;
            GameSession.SelectSlot(selectedSlot);
            SceneSwitcher.Load(catalog.GameScene);
        }

        public void ConfigureAscension(AscensionPicker picker) => ascensionPicker = picker;

        public void ConfigureExpeditionPoints(TMP_Text points) => expeditionPointsLabel = points;
        /// <summary>출정 준비 화면 해금 카드의 보유 포인트 글(UI-06). 글이 없으면 빈 문자열.</summary>
        public string ExpeditionPointsText => expeditionPointsLabel != null ? expeditionPointsLabel.text : string.Empty;
        public AscensionPicker Ascension => ascensionPicker;

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Refresh()
        {
            MetaSaveStore store = GameSession.SaveStore;
            for (int i = 0; i < slotLabels.Length; i++)
                if (slotLabels[i] != null) slotLabels[i].text = Describe(store.Inspect(i)) + DescribeRun(i);
            // 고른 슬롯은 카드 테두리로 보여 준다(UI-10). 테두리가 없는 옛 씬에서는 글 앞에 "> "를 붙인다.
            for (int i = 0; i < slotLabels.Length; i++)
            {
                bool selected = i == selectedSlot;
                if (i < slotHighlights.Length && slotHighlights[i] != null) slotHighlights[i].SetActive(selected);
                else if (slotLabels[i] != null) slotLabels[i].text = (selected ? "> " : "  ") + slotLabels[i].text;
            }
            // 진행 중인 스테이지는 슬롯 카드에 나오므로 이어 하기 버튼은 짧게 둔다(UI-10).
            if (continueButton != null) continueButton.interactable = GameSession.RunSaves.Inspect(selectedSlot).HasRun;
            if (statusLabel != null) statusLabel.text = notice;
            // 해금 창에서 포인트를 쓰고 닫으면(Closed) 여기서 다시 채운다.
            if (expeditionPointsLabel != null)
                expeditionPointsLabel.text = $"보유 해금 포인트  <color=#E3BB71>{store.Inspect(selectedSlot).Progress.unlockPoints}</color>";
        }

        // 슬롯 카드 아래에 붙이는 진행 중인 런 줄. 런 저장이 없으면 빈 문자열.
        static string DescribeRun(int slot)
        {
            ProjectNeo.Run.RunLoadResult run = GameSession.RunSaves.Inspect(slot);
            if (!run.HasRun) return string.Empty;
            string ascension = run.Snapshot.ascensionLevel > 0 ? $" (승천 {run.Snapshot.ascensionLevel})" : string.Empty;
            return $"\n\n스테이지 {run.Snapshot.stageNumber} 진행 중{ascension}";
        }

        // 슬롯 카드의 본문(UI-10): 슬롯 이름은 카드의 고정 글이라 여기에는 없고, 한 줄에 하나씩 적는다.
        static string Describe(MetaLoadResult result)
        {
            MetaProgress progress = result.Progress;
            string summary = $"지휘관 Lv {progress.commanderLevel}\n해금 포인트 {progress.unlockPoints}";
            if (progress.ascensionUnlocked > 0) summary += $" · 승천 {progress.ascensionUnlocked}";
            if (progress.bestRunScore > 0) summary += $"\n최고 {ProjectNeo.Run.RunScore.Number(progress.bestRunScore)}";
            switch (result.Status)
            {
                case MetaLoadStatus.Loaded: return summary;
                case MetaLoadStatus.RestoredFromBackup: return summary + "\n(백업에서 복구)";
                case MetaLoadStatus.Unreadable: return "손상됨 - 새로 시작";
                default: return "비어 있음";
            }
        }
    }
}
