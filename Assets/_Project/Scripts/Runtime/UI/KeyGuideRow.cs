using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>화면 키 가이드(UI-12)의 한 줄: 길게 누르기 표시, 키캡 글자, 하는 일. 값만 받아 그린다.</summary>
    public sealed class KeyGuideRow : MonoBehaviour
    {
        [SerializeField] TMP_Text hold;
        [SerializeField] TMP_Text key;
        [SerializeField] TMP_Text action;
        [Tooltip("두 번째 키캡 글자(UI-12). 키캡은 이 글자의 부모이고, 두 번째 키가 있는 줄에서만 켠다.")]
        [SerializeField] TMP_Text secondKey;

        public string KeyText => key != null ? key.text : string.Empty;
        public string SecondKeyText => secondKey != null && secondKey.transform.parent.gameObject.activeSelf ? secondKey.text : string.Empty;
        public string ActionText => action != null ? action.text : string.Empty;
        public bool HoldShowing => hold != null && hold.text.Length > 0;
        /// <summary>줄 내용을 오른쪽 끝에 붙였는가(접힌 가이드의 H 한 줄).</summary>
        public bool RightAligned { get; private set; }

        /// <summary>
        /// 줄 내용을 오른쪽 끝(true) 또는 왼쪽 끝(false)에 붙인다. 접힌 가이드의 H 한 줄은 화면 오른쪽 아래 모서리에 붙인다
        /// (UI-12 사용자 결정). 줄의 HorizontalLayoutGroup 정렬만 바꾼다.
        /// </summary>
        public void Align(bool right)
        {
            if (RightAligned == right && alignedOnce) return;
            alignedOnce = true;
            RightAligned = right;
            var line = GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (line != null) line.childAlignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        bool alignedOnce;

        public void Configure(TMP_Text holdLabel, TMP_Text keyLabel, TMP_Text actionLabel)
        {
            hold = holdLabel; key = keyLabel; action = actionLabel;
        }

        public void ConfigureSecondKey(TMP_Text keyLabel) => secondKey = keyLabel;

        public void Show(KeyHint hint)
        {
            if (secondKey != null)
            {
                GameObject cap = secondKey.transform.parent.gameObject;
                bool has = hint.SecondKey.Length > 0;
                if (cap.activeSelf != has) cap.SetActive(has);
                secondKey.text = hint.SecondKey;
            }
            if (hold != null) hold.text = hint.Hold ? "HOLD" : string.Empty;
            if (key != null) key.text = hint.Key;
            if (action != null) action.text = hint.Action;
        }
    }
}
