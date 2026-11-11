using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 임시 화면에서 한글을 보여주기 위해 운영체제 글꼴을 실행 중에 만들어 자식 글자에 적용한다.
    /// 글꼴 파일을 프로젝트에 복사하지 않는다. 최종 글꼴은 M5에서 정한다.
    /// </summary>
    public sealed class RuntimeSystemFont : MonoBehaviour
    {
        [SerializeField] TMP_Text[] captionLabels = new TMP_Text[0];
        [SerializeField] string[] captions = new string[0];
        TMP_FontAsset runtimeFont;

        /// <summary>글꼴을 만들 수 있을 때만 바꿔 넣을 한글 문구. 실패하면 씬에 저장된 영어 문구가 남는다.</summary>
        public void Configure(TMP_Text[] labels, string[] translatedCaptions)
        {
            captionLabels = labels;
            captions = translatedCaptions;
        }

        public bool Available => runtimeFont != null;

        void Awake()
        {
            runtimeFont = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular");
            if (runtimeFont == null) return;
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) text.font = runtimeFont;
            for (int i = 0; i < captionLabels.Length; i++) captionLabels[i].text = captions[i];
        }

        void OnDestroy()
        {
            if (runtimeFont == null) return;
            foreach (Texture2D texture in runtimeFont.atlasTextures) if (texture != null) Destroy(texture);
            if (runtimeFont.material != null) Destroy(runtimeFont.material);
            Destroy(runtimeFont);
        }
    }
}
