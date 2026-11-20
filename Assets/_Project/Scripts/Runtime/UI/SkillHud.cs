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
        static readonly Color SlotColor = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color ReadyColor = new Color(0.95f, 0.85f, 0.4f, 1f);
        static readonly Color EmptyColor = new Color(1f, 1f, 1f, 0.35f);
        static readonly Color CoverColor = new Color(0f, 0f, 0f, 0.65f);

        GameSkills skills;
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
        }

        // 화면 아래 가운데: 칸 3개를 가로로, 그 위에 안내 한 줄.
        void Build()
        {
            var root = new GameObject("SkillCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            int count = Mathf.Max(1, skills != null ? skills.SlotCount : SkillSlotRules.PlayerSlots);
            float width = count * SlotWidth + (count - 1) * Gap;
            panel = new GameObject("SkillBar", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 84f);
            rect.sizeDelta = new Vector2(width, SlotHeight);

            slots = new Slot[count];
            for (int i = 0; i < count; i++) slots[i] = BuildSlot(i, panel.transform, -width * 0.5f + i * (SlotWidth + Gap));

            message = Label("Message", panel.transform, 15f, FontStyles.Normal, TextAlignmentOptions.Center);
            var messageRect = (RectTransform)message.transform;
            messageRect.anchorMin = new Vector2(0f, 1f); messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.pivot = new Vector2(0.5f, 0f);
            messageRect.offsetMin = new Vector2(-120f, 4f); messageRect.offsetMax = new Vector2(120f, 28f);
            panel.SetActive(false);
        }

        static Slot BuildSlot(int index, Transform parent, float left)
        {
            GameObject frame = Box("Slot" + (index + 1), parent, SlotColor);
            var rect = (RectTransform)frame.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(SlotWidth, SlotHeight);

            GameObject cover = Box("Cooldown", frame.transform, CoverColor);
            var coverRect = (RectTransform)cover.transform;
            coverRect.anchorMin = Vector2.zero; coverRect.anchorMax = new Vector2(1f, 0f);
            coverRect.offsetMin = coverRect.offsetMax = Vector2.zero;

            var slot = new Slot { Frame = frame.GetComponent<Image>(), Cover = coverRect };
            slot.Key = Label("Key", frame.transform, 14f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Inset((RectTransform)slot.Key.transform, 6f, 4f);
            slot.Name = Label("Name", frame.transform, 14f, FontStyles.Bold, TextAlignmentOptions.Bottom);
            Inset((RectTransform)slot.Name.transform, 4f, 6f);
            slot.Timer = Label("Timer", frame.transform, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
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

        static TMP_Text Label(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = string.Empty;
            label.fontSize = size; label.fontStyle = style; label.color = Color.white;
            label.alignment = alignment; label.raycastTarget = false;
            return label;
        }
    }
}
