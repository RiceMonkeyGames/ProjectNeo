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
    /// D-8 부대 편성 화면(O 키)을 Game 씬에 붙인다. 시설·장비 팝업과 같은 모양의 OrganizationPanel을 만들고
    /// 전투 오브젝트에 <see cref="GameSquadOrganization"/>을 붙여 연결한다. 다시 실행하면 패널을 새로 만든다.
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
                out RectTransform source, out RectTransform target, out Button row);
            organization.Configure(view, interaction, panel, status, body, source, target, row);
            EditorUtility.SetDirty(organization);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static void BuildPanel(GameFlowHud hud, GameSquadOrganization organization, out GameObject panel, out TMP_Text status,
            out TMP_Text body, out RectTransform source, out RectTransform target, out Button row)
        {
            // 한글 글꼴(RuntimeSystemFont)이 실행 중에 바꿔 주는 범위 안에 둔다(장비 팝업과 같다).
            var font = (RuntimeSystemFont)new SerializedObject(hud).FindProperty("font").objectReferenceValue;
            Transform parent = font != null && !hud.transform.IsChildOf(font.transform) ? font.transform : hud.transform;
            Transform existing = parent.Find("OrganizationPanel");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            panel = new GameObject("OrganizationPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(860f, 600f);
            panel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.1f, 0.94f);
            TMP_Text title = GameFacilitySetup.Text(panel.transform, "Title", "부대 편성", 32f, new Vector2(24f, -20f), new Vector2(-24f, -66f));
            title.fontStyle = FontStyles.Bold;
            status = GameFacilitySetup.Text(panel.transform, "Status", "Squads", 18f, new Vector2(24f, -70f), new Vector2(-24f, -122f));
            status.color = new Color(1f, 0.9f, 0.55f);
            body = GameFacilitySetup.Text(panel.transform, "Body", "", 17f, new Vector2(24f, -126f), new Vector2(-24f, -180f));

            var lists = new GameObject("Lists", typeof(RectTransform));
            lists.transform.SetParent(panel.transform, false);
            var listsRect = lists.GetComponent<RectTransform>();
            listsRect.anchorMin = Vector2.zero; listsRect.anchorMax = Vector2.one;
            listsRect.offsetMin = new Vector2(24f, 84f); listsRect.offsetMax = new Vector2(-24f, -188f);
            source = GameFacilitySetup.Column(lists.transform, "Source", "보내는 부대 잡졸 (클릭해서 옮기기)", 0f, 0.5f);
            target = GameFacilitySetup.Column(lists.transform, "Target", "받는 부대 · 대장 (클릭해서 선택)", 0.5f, 1f);

            row = GameFacilitySetup.ButtonAt(panel.transform, "RowTemplate", "Row", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(380f, 30f), null, out TMP_Text rowLabel);
            rowLabel.fontSize = 15f; rowLabel.alignment = TextAlignmentOptions.MidlineLeft;
            rowLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            row.gameObject.SetActive(false);

            GameFacilitySetup.ButtonAt(panel.transform, "Button_Close", "닫기 (Esc)", new Vector2(0.5f, 0f), new Vector2(0f, 20f),
                new Vector2(200f, 48f), organization.Close, out _);
            panel.SetActive(false);
        }
    }
}
