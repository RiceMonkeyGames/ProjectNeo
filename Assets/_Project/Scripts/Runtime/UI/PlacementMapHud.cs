using System.Collections.Generic;
using ProjectNeo.Flow;
using ProjectNeo.Squads;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 부대 배치 화면(P, PREP-05)의 지도 표식(UI-13). 위에서 보는 화면 위에 부대 번호 표식, 기억해 둔 자리(점선 고리와 이어진 선),
    /// 성 안 구역 테두리, 전열 긋기 선을 화면 좌표로 그린다. 표식은 클릭을 막지 않으며(레이캐스트 끔) 배치 규칙에는 관여하지 않는다.
    /// 씬 파일을 바꾸지 않고 GamePhaseView가 실행 중에 붙인다. 모양은 HudStyle의 창 색을 따른다.
    /// </summary>
    public sealed class PlacementMapHud : MonoBehaviour
    {
        const float MarkerSize = 30f;
        const float MarkerLift = 26f;
        const float LineThickness = 2f;
        const float DragThickness = 4f;
        const float ZoneRefreshSeconds = 0.5f;
        /// <summary>기억한 자리가 지금 자리와 이만큼(미터) 넘게 다를 때만 점선 고리를 그린다.</summary>
        const float GhostMinDistance = 1.5f;
        const int TextureSize = 64;

        readonly struct Marker
        {
            public readonly RectTransform Root;
            public readonly Image Ring;
            public readonly Image Fill;
            public readonly TMP_Text Number;
            public readonly RectTransform Ghost;
            public readonly RectTransform Link;

            public Marker(RectTransform root, Image ring, Image fill, TMP_Text number, RectTransform ghost, RectTransform link)
            {
                Root = root; Ring = ring; Fill = fill; Number = number; Ghost = ghost; Link = link;
            }
        }

        GamePrepPlacement prep;
        HudStyle style;
        Canvas canvas;
        RectTransform canvasRoot;
        Sprite ringSprite, dashedSprite, discSprite;
        readonly List<Marker> markers = new List<Marker>();
        readonly List<RectTransform> zoneLines = new List<RectTransform>();
        RectTransform dragLine;
        readonly Vector3[] zoneCorners = new Vector3[4];
        bool zoneValid, wasShowing;
        float nextZoneRefresh;

        /// <summary>표시 중인가(배치 화면이 열려 있다).</summary>
        public bool IsShowing => canvas != null && canvas.enabled;
        /// <summary>지금 그리고 있는 부대 표식 수.</summary>
        public int ShownMarkerCount { get; private set; }
        /// <summary>지금 그리고 있는 기억한 자리 고리 수.</summary>
        public int ShownGhostCount { get; private set; }
        public bool ZoneShown { get; private set; }
        public bool DragLineShown { get; private set; }

        public void Configure(GamePrepPlacement placement, HudStyle hudStyle)
        {
            prep = placement; style = hudStyle;
        }

        void Awake()
        {
            var host = new GameObject("PlacementMapCanvas", typeof(RectTransform));
            host.transform.SetParent(transform, false);
            canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 키 가이드·부대 카드보다 아래에 그린다.
            canvas.sortingOrder = -5;
            canvasRoot = (RectTransform)host.transform;
            canvas.enabled = false;
            ringSprite = MakeSprite(false, true);
            dashedSprite = MakeSprite(true, true);
            discSprite = MakeSprite(false, false);
        }

        void OnDestroy()
        {
            DestroyTexture(ringSprite); DestroyTexture(dashedSprite); DestroyTexture(discSprite);
        }

        static void DestroyTexture(Sprite sprite)
        {
            if (sprite == null) return;
            Texture2D texture = sprite.texture;
            Destroy(sprite);
            if (texture != null) Destroy(texture);
        }

        void LateUpdate()
        {
            bool show = prep != null && prep.PlacementMode && prep.Center != null && prep.TopDownCamera != null;
            if (canvas.enabled != show) canvas.enabled = show;
            if (!show) { wasShowing = false; ShownMarkerCount = ShownGhostCount = 0; ZoneShown = DragLineShown = false; return; }
            if (!wasShowing) { wasShowing = true; nextZoneRefresh = 0f; }
            if (style == null)
            {
                // 부대 카드 HUD가 쓰는 공용 모양을 한 번 찾아 쓴다. 없으면 코드 기본 색을 쓴다.
                PrepSquadCardsHud cards = FindAnyObjectByType<PrepSquadCardsHud>();
                style = cards != null && cards.Style != null ? cards.Style : HudStyle.Create();
            }
            if (Time.unscaledTime >= nextZoneRefresh)
            {
                nextZoneRefresh = Time.unscaledTime + ZoneRefreshSeconds;
                zoneValid = prep.TryGetInteriorCorners(zoneCorners);
            }
            float scale = Mathf.Max(0.5f, Screen.height / 1080f);
            Camera camera = prep.TopDownCamera;
            DrawZone(camera, scale);
            DrawSquads(camera, scale);
            DrawDragLine(camera, scale);
        }

        Color Accent => style != null ? style.WindowAccent : new Color32(0xE3, 0xBB, 0x71, 0xFF);
        Color Plate => style != null ? style.WindowPlate : new Color32(0x10, 0x16, 0x1A, 0xF2);

        void DrawZone(Camera camera, float scale)
        {
            ZoneShown = zoneValid;
            EnsureLines(zoneLines, 4, "ZoneLine");
            for (int i = 0; i < zoneLines.Count; i++)
            {
                Vector2 a = default, b = default;
                bool used = zoneValid && TryScreen(camera, zoneCorners[i], out a) && TryScreen(camera, zoneCorners[(i + 1) % 4], out b);
                if (used) SetLine(zoneLines[i], a, b, LineThickness * scale, WithAlpha(Accent, 0.75f));
                if (zoneLines[i].gameObject.activeSelf != used) zoneLines[i].gameObject.SetActive(used);
            }
        }

        void DrawSquads(Camera camera, float scale)
        {
            SquadCommandCenter center = prep.Center;
            SquadCommander commander = center.Commander;
            int count = commander != null ? commander.SquadCount : 0;
            while (markers.Count < count) markers.Add(CreateMarker(markers.Count));
            int shown = 0, ghosts = 0;
            for (int i = 0; i < markers.Count; i++)
            {
                Marker marker = markers[i];
                Vector3 position = default;
                Vector2 point = default;
                bool onScreen = i < count && center.TryGetGatherPoint(i, out position) && TryScreen(camera, position, out point);
                SetActive(marker.Root, onScreen);
                if (!onScreen) { SetActive(marker.Ghost, false); SetActive(marker.Link, false); continue; }
                shown++;
                bool selected = commander.IsSelected(i);
                Vector2 markerPoint = point + new Vector2(0f, MarkerLift * scale);
                marker.Root.anchoredPosition = markerPoint;
                marker.Root.sizeDelta = Vector2.one * MarkerSize * scale;
                marker.Fill.color = selected ? Accent : Plate;
                marker.Ring.color = Accent;
                marker.Number.text = (i + 1).ToString();
                marker.Number.fontSize = 16f * scale;
                marker.Number.color = selected ? Plate : Accent;

                Vector2 ghostPoint = default;
                bool ghost = prep.Memory.TryGet(i, out SquadPlacementMemory.Entry entry)
                    && (entry.Position - position).sqrMagnitude > GhostMinDistance * GhostMinDistance
                    && TryScreen(camera, entry.Position, out ghostPoint);
                if (ghost)
                {
                    ghostPoint += new Vector2(0f, MarkerLift * scale);
                    marker.Ghost.anchoredPosition = ghostPoint;
                    marker.Ghost.sizeDelta = Vector2.one * MarkerSize * scale;
                    marker.Ghost.GetComponent<Image>().color = WithAlpha(Accent, 0.8f);
                    SetLine(marker.Link, ghostPoint, markerPoint, LineThickness * scale, WithAlpha(Accent, 0.45f));
                    ghosts++;
                }
                SetActive(marker.Ghost, ghost);
                SetActive(marker.Link, ghost);
            }
            ShownMarkerCount = shown; ShownGhostCount = ghosts;
        }

        void DrawDragLine(Camera camera, float scale)
        {
            if (dragLine == null) dragLine = CreateLine("DragLine");
            Vector2 from = default, to = default;
            bool used = prep.TryGetDragLine(out Vector3 start, out Vector3 end)
                && TryScreen(camera, start, out from) && TryScreen(camera, end, out to);
            if (used) SetLine(dragLine, from, to, DragThickness * scale, Accent);
            SetActive(dragLine, used);
            DragLineShown = used;
        }

        // ── 만들기 ─────────────────────────────────────────────

        Marker CreateMarker(int index)
        {
            var root = NewRect("Squad" + (index + 1), canvasRoot);
            Image fill = AddImage(root.gameObject, discSprite);
            var ringObject = NewRect("Ring", root);
            Stretch(ringObject);
            Image ring = AddImage(ringObject.gameObject, ringSprite);
            var textObject = NewRect("Number", root);
            Stretch(textObject);
            var number = textObject.gameObject.AddComponent<TextMeshProUGUI>();
            number.alignment = TextAlignmentOptions.Center;
            number.raycastTarget = false;
            number.textWrappingMode = TextWrappingModes.NoWrap;
            if (TMP_Settings.defaultFontAsset != null) number.font = TMP_Settings.defaultFontAsset;

            var ghost = NewRect("Ghost" + (index + 1), canvasRoot);
            AddImage(ghost.gameObject, dashedSprite);
            RectTransform link = CreateLine("Link" + (index + 1));
            return new Marker(root, ring, fill, number, ghost, link);
        }

        void EnsureLines(List<RectTransform> lines, int count, string name)
        {
            while (lines.Count < count) lines.Add(CreateLine(name + (lines.Count + 1)));
        }

        RectTransform CreateLine(string name)
        {
            RectTransform line = NewRect(name, canvasRoot);
            line.pivot = new Vector2(0f, 0.5f);
            AddImage(line.gameObject, null);
            return line;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static Image AddImage(GameObject target, Sprite sprite)
        {
            var image = target.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        // ── 그리기 도우미 ──────────────────────────────────────

        static bool TryScreen(Camera camera, Vector3 world, out Vector2 screen)
        {
            Vector3 point = camera.WorldToScreenPoint(world);
            screen = point;
            return point.z > 0f;
        }

        static void SetLine(RectTransform line, Vector2 from, Vector2 to, float thickness, Color color)
        {
            Vector2 along = to - from;
            line.anchoredPosition = from;
            line.sizeDelta = new Vector2(along.magnitude, thickness);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            line.GetComponent<Image>().color = color;
        }

        static void SetActive(RectTransform rect, bool active)
        {
            if (rect != null && rect.gameObject.activeSelf != active) rect.gameObject.SetActive(active);
        }

        static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        /// <summary>고리나 채운 원 스프라이트를 만든다. 점선이면 고리를 여덟 토막으로 끊는다.</summary>
        static Sprite MakeSprite(bool dashed, bool ring)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            float radius = TextureSize * 0.5f - 1f;
            float inner = radius - 4f;
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float dx = x + 0.5f - TextureSize * 0.5f, dy = y + 0.5f - TextureSize * 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01(radius - distance + 0.5f);
                    float alpha = ring ? Mathf.Min(edge, Mathf.Clamp01(distance - inner + 0.5f)) : edge;
                    if (dashed)
                    {
                        float angle = (Mathf.Atan2(dy, dx) + Mathf.PI) / (2f * Mathf.PI) * 16f;
                        if (Mathf.FloorToInt(angle) % 2 == 1) alpha = 0f;
                    }
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
