using System;
using ProjectNeo.Flow;
using ProjectNeo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// D-8 부대 편성 화면(O 키)을 Game 씬에 붙인다. UI-01(Q-49): 부대 카드를 가로로 늘어놓는 스크롤 영역, 카드 틀(머리 버튼 + 목록),
    /// 줄 틀을 가진 OrganizationPanel을 만들고 전투 오브젝트의 <see cref="GameSquadOrganization"/>에 연결한다. 다시 실행하면 패널을 새로 만든다.
    /// 상호작용 설정 에셋은 새 키 칸(O)을 저장하도록 더럽힌다.
    /// </summary>
    public static class SquadOrganizationSetup
    {
        [MenuItem("ProjectNeo/Setup/Connect Squad Organization To Game Scene")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            GameBattleController battle = GameFacilitySetup.Find<GameBattleController>(scene);
            GamePhaseView view = GameFacilitySetup.Find<GamePhaseView>(scene);
            GameFlowHud hud = GameFacilitySetup.Find<GameFlowHud>(scene);
            var interaction = GameFacilitySetup.Find<PreparationInteraction>(scene);
            var interactionSettings = (PreparationInteractionSettings)new SerializedObject(interaction)
                .FindProperty("settings").objectReferenceValue;
            if (interactionSettings != null) EditorUtility.SetDirty(interactionSettings);

            var organization = battle.GetComponent<GameSquadOrganization>();
            if (organization == null) organization = battle.gameObject.AddComponent<GameSquadOrganization>();
            BuildPanel(hud, organization, out GameObject panel, out TMP_Text status, out TMP_Text body,
                out RectTransform cards, out RectTransform card, out Button row);
            organization.Configure(view, interaction, panel, status, body, cards, card, row);
            EditorUtility.SetDirty(organization);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static void BuildPanel(GameFlowHud hud, GameSquadOrganization organization, out GameObject panel, out TMP_Text status,
            out TMP_Text body, out RectTransform cards, out RectTransform card, out Button row)
        {
            Transform parent = hud.transform;
            Transform existing = parent.Find("OrganizationPanel");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            panel = new GameObject("OrganizationPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1120f, 660f);
            panel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.1f, 0.94f);
            TMP_Text title = GameFacilitySetup.Text(panel.transform, "Title", "부대 편성", 32f, new Vector2(24f, -20f), new Vector2(-24f, -66f));
            title.fontStyle = FontStyles.Bold;
            status = GameFacilitySetup.Text(panel.transform, "Status", "Squads", 18f, new Vector2(24f, -70f), new Vector2(-24f, -100f));
            status.color = new Color(1f, 0.9f, 0.55f);
            body = GameFacilitySetup.Text(panel.transform, "Body", "", 16f, new Vector2(24f, -104f), new Vector2(-24f, -156f));

            // 카드는 가로로 늘어놓고 넘치면 가로 스크롤한다(마우스 휠도 가로로 움직인다).
            var scroll = new GameObject("Cards", typeof(RectTransform), typeof(ScrollRect));
            scroll.transform.SetParent(panel.transform, false);
            var scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero; scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(24f, 84f); scrollRect.offsetMax = new Vector2(-24f, -164f);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(scroll.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);
            var content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            cards = content.GetComponent<RectTransform>();
            cards.anchorMin = new Vector2(0f, 0f); cards.anchorMax = new Vector2(0f, 1f); cards.pivot = new Vector2(0f, 0.5f);
            cards.offsetMin = cards.offsetMax = Vector2.zero;
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f; layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scrollView = scroll.GetComponent<ScrollRect>();
            scrollView.viewport = viewportRect; scrollView.content = cards;
            scrollView.horizontal = true; scrollView.vertical = false;
            scrollView.movementType = ScrollRect.MovementType.Clamped; scrollView.scrollSensitivity = 30f;

            // 카드 틀: 머리 버튼(놓을 곳) + 세로 목록. 실행 중에 부대마다 복제한다.
            var cardObject = new GameObject("CardTemplate", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            cardObject.transform.SetParent(content.transform, false);
            card = cardObject.GetComponent<RectTransform>();
            cardObject.GetComponent<Image>().color = new Color(0.11f, 0.15f, 0.19f, 1f);
            var cardSize = cardObject.GetComponent<LayoutElement>();
            cardSize.preferredWidth = cardSize.minWidth = 210f;
            Button header = GameFacilitySetup.ButtonAt(cardObject.transform, "Header", "부대", new Vector2(0.5f, 1f), new Vector2(0f, -6f),
                new Vector2(198f, 52f), null, out TMP_Text headerLabel);
            headerLabel.fontSize = 15f;
            header.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            header.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -6f);
            var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
            list.transform.SetParent(cardObject.transform, false);
            var listRect = list.GetComponent<RectTransform>();
            listRect.anchorMin = Vector2.zero; listRect.anchorMax = Vector2.one;
            listRect.offsetMin = new Vector2(6f, 6f); listRect.offsetMax = new Vector2(-6f, -64f);
            var listLayout = list.GetComponent<VerticalLayoutGroup>();
            listLayout.spacing = 3f; listLayout.childControlHeight = true; listLayout.childControlWidth = true;
            listLayout.childForceExpandHeight = false; listLayout.childForceExpandWidth = true;
            cardObject.SetActive(false);

            row = GameFacilitySetup.ButtonAt(panel.transform, "RowTemplate", "항목", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(198f, 26f), null, out TMP_Text rowLabel);
            rowLabel.fontSize = 14f; rowLabel.alignment = TextAlignmentOptions.MidlineLeft;
            rowLabel.rectTransform.offsetMin = new Vector2(8f, 0f);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
            row.gameObject.SetActive(false);

            GameFacilitySetup.ButtonAt(panel.transform, "Button_Close", "닫기 (Esc)", new Vector2(0.5f, 0f), new Vector2(0f, 20f),
                new Vector2(200f, 48f), organization.Close, out _);
            panel.SetActive(false);
        }
    }
}
