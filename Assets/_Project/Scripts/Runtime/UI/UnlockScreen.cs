using System;
using System.Collections.Generic;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 타이틀의 런 사이 해금 창(META-06). 고른 슬롯의 해금 포인트와 트랙 4개를 나란히 보여 주고,
    /// 항목을 두 번 누르면 사서 바로 저장한다. 규칙과 글자는 <see cref="UnlockScreenModel"/>이 맡는다.
    /// </summary>
    public sealed class UnlockScreen : MonoBehaviour
    {
        static readonly Color OpenedColor = new Color(0.16f, 0.3f, 0.2f);
        static readonly Color AvailableColor = new Color(0.15f, 0.24f, 0.29f);
        static readonly Color ArmedColor = new Color(0.45f, 0.36f, 0.12f);
        static readonly Color LockedColor = new Color(0.12f, 0.13f, 0.15f);

        [SerializeField] UnlockCatalog catalog;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text pointsLabel;
        [SerializeField] TMP_Text statusLabel;
        [Tooltip("트랙마다 하나. 각 열의 첫 자식은 트랙 이름 글이다.")]
        [SerializeField] RectTransform[] columns = new RectTransform[0];
        [SerializeField] Button rowTemplate;

        readonly List<Row> rows = new List<Row>();
        UnlockScreenModel model;

        struct Row
        {
            public Button button;
            public TMP_Text label;
            public UnlockEntry entry;
        }

        /// <summary>창을 닫으면 알린다(타이틀이 슬롯 글을 새로 쓴다).</summary>
        public event Action Closed;

        public bool IsOpen => panel != null && panel.activeSelf;
        public UnlockScreenModel Model => model;
        public string PointsText => pointsLabel != null ? pointsLabel.text : string.Empty;
        public string StatusText => statusLabel != null ? statusLabel.text : string.Empty;
        public int RowCount => rows.Count;

        public void Configure(UnlockCatalog unlockCatalog, GameObject root, TMP_Text points, TMP_Text status,
            RectTransform[] trackColumns, Button template)
        {
            catalog = unlockCatalog; panel = root; pointsLabel = points; statusLabel = status;
            columns = trackColumns; rowTemplate = template;
        }

        void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        /// <summary>슬롯의 메타 진행을 불러와 창을 연다. 해금 트랙이 없으면 false.</summary>
        public bool Open(int slot)
        {
            if (catalog == null || panel == null || !MetaSaveStore.IsValidSlot(slot)) return false;
            if (GameSession.ActiveSlot != slot) GameSession.SelectSlot(slot);
            model = new UnlockScreenModel(GameSession.Meta, catalog);
            BuildRows();
            panel.SetActive(true);
            Refresh();
            return true;
        }

        public void Close()
        {
            model?.Disarm();
            if (panel != null) panel.SetActive(false);
            Closed?.Invoke();
        }

        /// <summary>항목을 누른다. 버튼이 부르고 테스트도 쓴다.</summary>
        public UnlockClickResult Click(string entryId)
        {
            if (model == null) return UnlockClickResult.Refused;
            UnlockClickResult result = model.Click(entryId);
            if (result == UnlockClickResult.Opened && !GameSession.SaveMeta())
                model.AppendNotice("슬롯을 저장하지 못했습니다.");
            Refresh();
            return result;
        }

        /// <summary>항목 버튼 글자. 없는 항목이면 빈 문자열.</summary>
        public string RowText(string entryId)
        {
            foreach (Row row in rows) if (row.entry.Id == entryId) return row.label.text;
            return string.Empty;
        }

        void BuildRows()
        {
            foreach (Row row in rows) if (row.button != null) Destroy(row.button.gameObject);
            rows.Clear();
            if (rowTemplate == null) return;
            IReadOnlyList<UnlockTrackDefinition> tracks = catalog.Tracks;
            for (int i = 0; i < tracks.Count && i < columns.Length; i++)
            {
                if (tracks[i] == null || columns[i] == null) continue;
                TMP_Text title = columns[i].GetComponentInChildren<TMP_Text>(true);
                if (title != null) title.text = tracks[i].DisplayName;
                foreach (UnlockEntry entry in tracks[i].Entries)
                {
                    Button button = Instantiate(rowTemplate, columns[i]);
                    button.gameObject.name = "Row_" + entry.Id;
                    button.gameObject.SetActive(true);
                    string id = entry.Id;
                    button.onClick.AddListener(() => Click(id));
                    rows.Add(new Row { button = button, label = button.GetComponentInChildren<TMP_Text>(true), entry = entry });
                }
            }
        }

        void Refresh()
        {
            if (model == null) return;
            if (pointsLabel != null) pointsLabel.text = model.PointsText;
            if (statusLabel != null) statusLabel.text = model.Notice;
            foreach (Row row in rows)
            {
                if (row.label != null) row.label.text = model.EntryText(row.entry);
                var image = row.button.targetGraphic as Image;
                if (image == null) continue;
                image.color = UnlockTracks.IsOpened(model.Progress, row.entry) ? OpenedColor
                    : row.entry.Id == model.ArmedEntryId ? ArmedColor
                    : model.CanOpen(row.entry) ? AvailableColor : LockedColor;
            }
        }
    }
}
