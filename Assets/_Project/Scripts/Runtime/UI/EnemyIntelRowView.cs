using ProjectNeo.Stages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 적 정보 창(UI-12)의 한 줄: 초상화 칸, 종류 이름, "× 수". 초상화 그림은 아직 없어 테두리만 보인다(사용자 결정 2026-11-22).
    /// 값만 받아 그린다.
    /// </summary>
    public sealed class EnemyIntelRowView : MonoBehaviour
    {
        [SerializeField] Image portrait;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text countLabel;

        public string NameText => nameLabel != null ? nameLabel.text : string.Empty;
        public string CountText => countLabel != null ? countLabel.text : string.Empty;
        public Image Portrait => portrait;

        public void Configure(Image portraitImage, TMP_Text name, TMP_Text count)
        {
            portrait = portraitImage; nameLabel = name; countLabel = count;
        }

        public void Show(EnemyIntelRow row)
        {
            if (nameLabel != null) nameLabel.text = row.Label;
            if (countLabel != null) countLabel.text = Count(row.Remaining);
        }

        /// <summary>줄 오른쪽 수. 예: "× 6".</summary>
        public static string Count(int remaining) => $"× {Mathf.Max(0, remaining)}";
    }
}
