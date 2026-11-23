using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 장비 창 스킬 탭(UI-07, 사용자 스케치 2026-11-23)의 둥근 스킬 칸 하나. 그림(없으면 이름 앞 글자), 왼쪽 위의
    /// 열리는 등급 배지, 잠김(흐리게), 고른 칸의 금테를 보여 주고 클릭을 알린다.
    /// </summary>
    public sealed class SkillCircleView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text letters;
        [SerializeField] TMP_Text grade;
        [SerializeField] CanvasGroup body;
        [SerializeField] GameObject selection;

        const float LockedAlpha = 0.55f;
        static readonly Color LockedTint = new Color(0.35f, 0.35f, 0.35f, 1f);

        public void Configure(Button clickTarget, Image iconImage, TMP_Text lettersLabel, TMP_Text gradeLabel,
            CanvasGroup content, GameObject selectedRing)
        {
            button = clickTarget; icon = iconImage; letters = lettersLabel; grade = gradeLabel;
            body = content; selection = selectedRing;
        }

        /// <summary>칸에 든 스킬 이름(테스트용). 비어 있으면 빈 문자열.</summary>
        public string SkillName { get; private set; } = string.Empty;
        public bool Locked { get; private set; }
        public bool Selected => selection != null && selection.activeSelf;

        /// <summary>스킬을 보여 준다. 잠긴 칸도 누를 수 있다(무엇이 언제 열리는지 설명을 본다).</summary>
        public void Show(string name, Sprite sprite, int openGrade, bool locked, bool selected, Action click)
        {
            SkillName = name ?? string.Empty;
            Locked = locked;
            gameObject.SetActive(true);
            // 잠긴 칸은 그림·글자를 회색으로 눌러 열린 칸과 바로 구분된다.
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; icon.color = locked ? LockedTint : Color.white; }
            if (letters != null)
            {
                letters.text = sprite == null ? EquipmentSlotView.Abbreviation(SkillName) : string.Empty;
                letters.color = locked ? LockedTint : Color.white;
            }
            if (grade != null) grade.text = openGrade.ToString();
            if (body != null) body.alpha = locked ? LockedAlpha : 1f;
            if (selection != null) selection.SetActive(selected);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.interactable = click != null;
            if (click != null) button.onClick.AddListener(() => click());
        }

        /// <summary>스킬이 없는 칸은 감춘다.</summary>
        public void Hide()
        {
            SkillName = string.Empty;
            Locked = false;
            gameObject.SetActive(false);
        }

        /// <summary>코드에서 클릭한 것처럼 누른다(테스트용).</summary>
        public void Click()
        {
            if (button != null && button.interactable) button.onClick.Invoke();
        }
    }
}
