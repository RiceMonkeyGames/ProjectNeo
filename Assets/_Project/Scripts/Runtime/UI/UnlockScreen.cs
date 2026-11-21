using System;
using System.Collections.Generic;
using ProjectNeo.Core;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 타이틀의 런 사이 해금 화면(META-06, UI-06 시안 B). 출정 준비 화면 위를 통째로 덮는 별도 패널이다.
    /// 왼쪽에서 트랙을 고르면 오른쪽에 그 트랙 항목 카드가 나오고, "무작위 해금"을 누르면 바로 하나가 열려 슬롯에 저장된다.
    /// 규칙과 글자는 <see cref="UnlockScreenModel"/>이 맡는다.
    /// </summary>
    public sealed class UnlockScreen : MonoBehaviour
    {
        static readonly Color OpenedFill = new Color32(0x1F, 0x3B, 0x2B, 0xFF);
        static readonly Color CandidateFill = new Color32(0x1F, 0x3A, 0x52, 0xFF);
        static readonly Color LockedFill = new Color32(0x10, 0x18, 0x20, 0xFF);
        static readonly Color OpenedText = new Color32(0x8F, 0xC3, 0x9A, 0xFF);
        static readonly Color CandidateText = new Color32(0xE3, 0xBB, 0x71, 0xFF);
        static readonly Color LockedText = new Color32(0x7D, 0x8A, 0x94, 0xFF);
        static readonly Color NameText = new Color32(0xF2, 0xEA, 0xD8, 0xFF);

        [SerializeField] UnlockCatalog catalog;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text pointsLabel;
        [SerializeField] TMP_Text statusLabel;
        [Header("Tracks (left)")]
        [SerializeField, Tooltip("One button per catalog track, in catalog order.")] Button[] trackButtons = new Button[0];
        [SerializeField] TMP_Text[] trackLabels = new TMP_Text[0];
        [SerializeField, Tooltip("Gold frame shown on the selected track button.")] GameObject[] trackHighlights = new GameObject[0];
        [Header("Entries (right)")]
        [SerializeField] TMP_Text trackTitleLabel;
        [SerializeField] RectTransform entryGrid;
        [SerializeField, Tooltip("Inactive card: Visual/Fill (Image), Visual/Highlight, Visual/Thumbnail/Icon (Image), Label_Name, Label_State.")]
        GameObject entryTemplate;
        [SerializeField] Button drawButton;
        [SerializeField] TMP_Text drawLabel;

        readonly List<Card> cards = new List<Card>();
        UnlockScreenModel model;
        IRandomSource random = new SeededRandom(Environment.TickCount);

        struct Card
        {
            public GameObject root;
            public Image fill;
            public GameObject highlight;
            public Image icon;
            public TMP_Text name;
            public TMP_Text state;
            public UnlockEntry entry;
        }

        /// <summary>화면을 닫으면 알린다(타이틀이 슬롯·출정 글을 새로 쓴다).</summary>
        public event Action Closed;

        public bool IsOpen => panel != null && panel.activeSelf;
        public UnlockScreenModel Model => model;
        public string PointsText => pointsLabel != null ? pointsLabel.text : string.Empty;
        public string StatusText => statusLabel != null ? statusLabel.text : string.Empty;
        public string TrackTitleText => trackTitleLabel != null ? trackTitleLabel.text : string.Empty;
        public string DrawText => drawLabel != null ? drawLabel.text : string.Empty;
        /// <summary>오른쪽에 보이는 항목 카드 수(고른 트랙의 항목 수).</summary>
        public int RowCount => cards.Count;
        public bool IsTrackHighlighted(int index) =>
            index >= 0 && index < trackHighlights.Length && trackHighlights[index] != null && trackHighlights[index].activeSelf;

        public void Configure(UnlockCatalog unlockCatalog, GameObject root, TMP_Text points, TMP_Text status)
        {
            catalog = unlockCatalog; panel = root; pointsLabel = points; statusLabel = status;
        }

        public void ConfigureTracks(Button[] buttons, TMP_Text[] labels, GameObject[] highlights)
        {
            trackButtons = buttons; trackLabels = labels; trackHighlights = highlights;
        }

        public void ConfigureEntries(TMP_Text title, RectTransform grid, GameObject template, Button draw, TMP_Text drawCaption)
        {
            trackTitleLabel = title; entryGrid = grid; entryTemplate = template; drawButton = draw; drawLabel = drawCaption;
        }

        /// <summary>무작위 해금에 쓸 난수(테스트가 바꾼다).</summary>
        public void UseRandom(IRandomSource source) => random = source ?? throw new ArgumentNullException(nameof(source));

        void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (entryTemplate != null) entryTemplate.SetActive(false);
        }

        /// <summary>슬롯의 메타 진행을 불러와 화면을 연다. 첫 트랙을 고른 채로 연다. 해금 트랙이 없으면 false.</summary>
        public bool Open(int slot)
        {
            if (catalog == null || panel == null || !MetaSaveStore.IsValidSlot(slot)) return false;
            if (GameSession.ActiveSlot != slot) GameSession.SelectSlot(slot);
            // 업데이트로 없어진 항목은 포인트를 돌려준 뒤 보여 준다(META-10).
            if (GameFacilities.MigrateUnlocks(catalog) > 0) GameSession.SaveMeta();
            model = new UnlockScreenModel(GameSession.Meta, catalog);
            panel.SetActive(true);
            BuildCards();
            Refresh();
            return true;
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            Closed?.Invoke();
        }

        /// <summary>왼쪽 트랙 버튼이 부른다.</summary>
        public void SelectTrack(int index)
        {
            if (model == null) return;
            int before = model.SelectedTrackIndex;
            model.SelectTrack(index);
            if (model.SelectedTrackIndex != before) BuildCards();
            Refresh();
        }

        /// <summary>"무작위 해금" 버튼이 부르고 테스트도 쓴다. 열리면 바로 슬롯을 저장한다.</summary>
        public UnlockClickResult Draw()
        {
            if (model == null) return UnlockClickResult.Refused;
            UnlockClickResult result = model.Draw(random);
            if (result == UnlockClickResult.Opened && !GameSession.SaveMeta())
                model.AppendNotice("슬롯을 저장하지 못했습니다.");
            Refresh();
            return result;
        }

        /// <summary>버튼 이벤트용(반환값 없는 <see cref="Draw"/>).</summary>
        public void DrawFromButton() => Draw();

        /// <summary>항목 카드 글(이름 - 상태). 지금 보이는 트랙에 없는 항목이면 빈 문자열.</summary>
        public string RowText(string entryId)
        {
            foreach (Card card in cards) if (card.entry.Id == entryId) return card.name.text + " - " + card.state.text;
            return string.Empty;
        }

        /// <summary>이 항목 카드에 썸네일 칸이 있는지, 있으면 그림이 보이는지(UI-06). 카드가 없으면 둘 다 false.</summary>
        public bool RowThumbnail(string entryId, out bool showsIcon)
        {
            foreach (Card card in cards)
                if (card.entry.Id == entryId)
                {
                    showsIcon = card.icon != null && card.icon.enabled && card.icon.sprite != null;
                    return card.icon != null;
                }
            showsIcon = false;
            return false;
        }

        /// <summary>이 항목 카드에 새로 열림 강조가 켜져 있는지.</summary>
        public bool IsRowHighlighted(string entryId)
        {
            foreach (Card card in cards) if (card.entry.Id == entryId) return card.highlight != null && card.highlight.activeSelf;
            return false;
        }

        void BuildCards()
        {
            // Editor captures drive the screen outside Play Mode, where Destroy is not allowed.
            foreach (Card card in cards)
                if (card.root != null)
                {
                    if (Application.isPlaying) Destroy(card.root);
                    else DestroyImmediate(card.root);
                }
            cards.Clear();
            UnlockTrackDefinition track = model?.SelectedTrack;
            if (track == null || entryTemplate == null || entryGrid == null) return;
            foreach (UnlockEntry entry in track.Entries)
            {
                if (entry == null) continue;
                GameObject root = Instantiate(entryTemplate, entryGrid);
                root.name = "Card_" + entry.Id;
                root.SetActive(true);
                Transform fill = root.transform.Find("Visual/Fill");
                Transform highlight = root.transform.Find("Visual/Highlight");
                Transform icon = root.transform.Find("Visual/Thumbnail/Icon");
                cards.Add(new Card
                {
                    root = root,
                    fill = fill != null ? fill.GetComponent<Image>() : null,
                    highlight = highlight != null ? highlight.gameObject : null,
                    icon = icon != null ? icon.GetComponent<Image>() : null,
                    name = root.transform.Find("Label_Name").GetComponent<TMP_Text>(),
                    state = root.transform.Find("Label_State").GetComponent<TMP_Text>(),
                    entry = entry,
                });
            }
        }

        void Refresh()
        {
            if (model == null) return;
            if (pointsLabel != null) pointsLabel.text = model.PointsText;
            if (statusLabel != null) statusLabel.text = model.Notice;
            if (trackTitleLabel != null) trackTitleLabel.text = model.TrackTitleText;
            if (drawLabel != null) drawLabel.text = model.DrawButtonText;
            if (drawButton != null) drawButton.interactable = model.CanDraw;

            IReadOnlyList<UnlockTrackDefinition> tracks = catalog.Tracks;
            for (int i = 0; i < trackButtons.Length; i++)
            {
                bool exists = i < tracks.Count && tracks[i] != null;
                if (trackButtons[i] != null) trackButtons[i].gameObject.SetActive(exists);
                if (exists && i < trackLabels.Length && trackLabels[i] != null) trackLabels[i].text = model.TrackText(tracks[i]);
                if (i < trackHighlights.Length && trackHighlights[i] != null) trackHighlights[i].SetActive(i == model.SelectedTrackIndex);
            }

            foreach (Card card in cards)
            {
                UnlockEntryState state = model.EntryState(card.entry);
                card.name.text = card.entry.DisplayName;
                card.name.color = state == UnlockEntryState.Locked ? LockedText : NameText;
                card.state.text = model.EntryStateText(card.entry);
                card.state.color = state == UnlockEntryState.Opened ? OpenedText
                    : state == UnlockEntryState.Candidate ? CandidateText : LockedText;
                if (card.fill != null)
                    card.fill.color = state == UnlockEntryState.Opened ? OpenedFill
                        : state == UnlockEntryState.Candidate ? CandidateFill : LockedFill;
                if (card.highlight != null) card.highlight.SetActive(card.entry.Id == model.LastOpenedId);
                if (card.icon != null)
                {
                    // 썸네일이 없는 항목은 빈 칸(테두리)만 보인다. 아직 못 여는 항목은 어둡게.
                    card.icon.sprite = card.entry.Icon;
                    card.icon.enabled = card.entry.Icon != null;
                    card.icon.color = state == UnlockEntryState.Locked ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
                }
            }
        }
    }
}
