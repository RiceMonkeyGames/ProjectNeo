using ProjectNeo.Flow;
using ProjectNeo.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 플레이어 스킬 칸 HUD(COMBAT-06). 전투 중 화면 아래 가운데에 칸마다 키·스킬 이름·남은 쿨다운을 보여 주고,
    /// 쿨다운 동안은 칸이 아래에서 위로 어둡게 덮인다. 그 위 한 줄에 시전하지 못한 이유를 잠깐 보여 준다.
    /// 씬 파일을 바꾸지 않게 실행 중에 붙이고 화면은 코드로 만든다(<see cref="BossHealthHud"/>와 같은 방식).
    /// </summary>
    public sealed class SkillHud : MonoBehaviour
    {
        const float SlotWidth = 116f;
        const float SlotHeight = 64f;
        const float Gap = 8f;
        // 칸 아래쪽 높이(UI-13): 플레이어 체력·키 가이드와 같은 화면 아래 가장자리 줄.
        const float BarBottom = 12f;
        // 쿨다운 중인 칸의 테두리는 조금 어둡게(UI-13).
        static readonly Color DimFrame = new Color(0.5f, 0.5f, 0.5f, 1f);
        static readonly Color SlotColor = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color ReadyColor = new Color(0.95f, 0.85f, 0.4f, 1f);
        static readonly Color EmptyColor = new Color(1f, 1f, 1f, 0.35f);
        static readonly Color CoverColor = new Color(0f, 0f, 0f, 0.65f);

        GameSkills skills;
        HudStyle style;
        GameObject panel;
        TMP_Text message;
        Slot[] slots = new Slot[0];

        sealed class Slot
        {
            public TMP_Text Key, Name, Timer;
            public RectTransform Cover;
            public Image Frame;
        }

        /// <summary>지금 HUD가 보이는지(테스트용).</summary>
        public bool Visible => panel != null && panel.activeSelf;
        public string MessageText => message != null ? message.text : string.Empty;
        public string NameText(int slot) => slot >= 0 && slot < slots.Length ? slots[slot].Name.text : string.Empty;
        public string KeyText(int slot) => slot >= 0 && slot < slots.Length ? slots[slot].Key.text : string.Empty;
        public string TimerText(int slot) => slot >= 0 && slot < slots.Length ? slots[slot].Timer.text : string.Empty;
        /// <summary>쿨다운 덮개 비율 0~1(테스트용).</summary>
        public float CoverRatio(int slot) => slot >= 0 && slot < slots.Length ? slots[slot].Cover.anchorMax.y : 0f;

        public void Configure(GameSkills gameSkills)
        {
            skills = gameSkills;
            if (panel == null) Build();
        }

        void LateUpdate()
        {
            SkillSlots playerSlots = skills != null && skills.PlayerCaster != null ? skills.PlayerCaster.Slots : null;
            bool show = skills != null && skills.BattleActive && playerSlots != null && playerSlots.Count > 0;
            if (panel.activeSelf != show) panel.SetActive(show);
            if (!show) return;
            for (int i = 0; i < slots.Length; i++) Refresh(slots[i], i, playerSlots);
            if (message.text != skills.Message) message.text = skills.Message;
        }

        void Refresh(Slot slot, int index, SkillSlots playerSlots)
        {
            SkillDefinition skill = playerSlots.Get(index);
            string key = skills.KeyOf(index);
            if (slot.Key.text != key) slot.Key.text = key;
            string name = skill != null ? skill.DisplayName : "-";
            if (slot.Name.text != name) slot.Name.text = name;
            float remaining = playerSlots.Remaining(index);
            string timer = remaining > 0f ? Mathf.CeilToInt(remaining).ToString() : string.Empty;
            if (slot.Timer.text != timer) slot.Timer.text = timer;
            slot.Cover.anchorMax = new Vector2(1f, playerSlots.RemainingFraction(index));
            Color color = skill == null ? EmptyColor : playerSlots.IsReady(index) ? ReadyColor : Color.white;
            if (slot.Name.color != color) slot.Name.color = color;
            if (slot.Frame != null)
            {
                Color frame = skill != null && playerSlots.IsReady(index) ? Color.white : DimFrame;
                if (slot.Frame.color != frame) slot.Frame.color = frame;
            }
        }

        // 화면 아래 가운데: 칸 3개를 가로로, 그 위에 안내 한 줄.
        // UI-13 전투 HUD A안: 준비 HUD 사이드바와 같은 어두운 판 + 금색 테두리 칸, 왼쪽 위 작은 키 칸. 아래 가운데 판이 전투 중에는
        // 없으므로 화면 아래쪽(분대 카드 열·키 가이드와 같은 가장자리 높이)에 붙인다. 모양 값이 없으면 예전 평평한 칸이다.
        void Build()
        {
            style = HudPlate.FindStyle(gameObject.scene);
            RectTransform root = HudPlate.OverlayCanvas(transform, "SkillCanvas", 25);

            int count = Mathf.Max(1, skills != null ? skills.SlotCount : SkillSlotRules.PlayerSlots);
            float width = count * SlotWidth + (count - 1) * Gap;
            panel = new GameObject("SkillBar", typeof(RectTransform));
            panel.transform.SetParent(root, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, BarBottom);
            rect.sizeDelta = new Vector2(width, SlotHeight);

            slots = new Slot[count];
            for (int i = 0; i < count; i++) slots[i] = BuildSlot(i, panel.transform, -width * 0.5f + i * (SlotWidth + Gap));

            message = Label("Message", panel.transform, 15f, FontStyles.Normal, TextAlignmentOptions.Center);
            var messageRect = (RectTransform)message.transform;
            messageRect.anchorMin = new Vector2(0f, 1f); messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.pivot = new Vector2(0.5f, 0f);
            messageRect.offsetMin = new Vector2(-120f, 6f); messageRect.offsetMax = new Vector2(120f, 30f);
            panel.SetActive(false);
        }

        Slot BuildSlot(int index, Transform parent, float left)
        {
            var frame = HudPlate.Node(parent, "Slot" + (index + 1));
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0f);
            frame.pivot = new Vector2(0f, 0f);
            frame.anchoredPosition = new Vector2(left, 0f);
            frame.sizeDelta = new Vector2(SlotWidth, SlotHeight);
            Image border = HudPlate.Plate(frame, style, style != null ? HudPlate.PlateColor : SlotColor);

            GameObject cover = Box("Cooldown", frame, CoverColor);
            var coverRect = (RectTransform)cover.transform;
            coverRect.anchorMin = Vector2.zero; coverRect.anchorMax = new Vector2(1f, 0f);
            coverRect.offsetMin = new Vector2(3f, 3f); coverRect.offsetMax = new Vector2(-3f, 0f);
            if (border != null) border.transform.SetAsLastSibling(); // 덮개가 테두리를 가리지 않게

            var slot = new Slot { Frame = border, Cover = coverRect };
            // 왼쪽 위 작은 키 칸: 어두운 바탕 위 크림색 키 이름.
            Image keyPlate = HudPlate.Box(frame, "KeyPlate", HudPlate.TrackColor);
            HudPlate.Place(keyPlate.rectTransform, new Vector2(0f, 1f), new Vector2(6f, -6f), new Vector2(22f, 20f), new Vector2(0f, 1f));
            slot.Key = Label("Key", keyPlate.transform, 13f, FontStyles.Normal, TextAlignmentOptions.Center);
            slot.Key.color = HudPlate.Cream;
            slot.Name = Label("Name", frame, 15f, FontStyles.Normal, TextAlignmentOptions.Bottom);
            Inset((RectTransform)slot.Name.transform, 6f, 7f);
            slot.Timer = Label("Timer", frame, 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            return slot;
        }

        static void Inset(RectTransform rect, float x, float y)
        {
            rect.offsetMin = new Vector2(x, y);
            rect.offsetMax = new Vector2(-x, -y);
        }

        static GameObject Box(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return go;
        }

        TMP_Text Label(string name, Transform parent, float size, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = string.Empty;
            if (style != null && style.WindowFont != null) label.font = style.WindowFont;
            label.fontSize = size; label.fontStyle = fontStyle; label.color = Color.white;
            label.alignment = alignment; label.raycastTarget = false;
            return label;
        }
    }
}
