using ProjectNeo.Walls;
using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    public sealed class ConstructionHud : MonoBehaviour
    {
        [SerializeField] PlacementController placement;
        [SerializeField] ConstructionSandbox sandbox;
        [SerializeField] BuildPieceDefinition wall;
        [SerializeField] BuildPieceDefinition stairs;
        [SerializeField] TMP_Text selection;
        [SerializeField] TMP_Text status;
        [SerializeField] TMP_Text unitHealth;
        [SerializeField] TMP_Text[] captionLabels;
        [SerializeField] string[] captions;
        TMP_FontAsset runtimeFont;
        public void Configure(PlacementController controller, ConstructionSandbox demo, BuildPieceDefinition wallPiece,
            BuildPieceDefinition stairPiece, TMP_Text selectionLabel, TMP_Text statusLabel, TMP_Text healthLabel,
            TMP_Text[] translatedLabels, string[] translatedCaptions)
        {
            placement = controller; sandbox = demo; wall = wallPiece; stairs = stairPiece;
            selection = selectionLabel; status = statusLabel; unitHealth = healthLabel;
            captionLabels = translatedLabels; captions = translatedCaptions;
        }

        void Awake()
        {
            // 시스템 글꼴을 런타임에 사용한다. 운영체제 글꼴 파일을 프로젝트에 복사하지 않는다.
            runtimeFont = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular");
            if (runtimeFont != null)
            {
                foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) text.font = runtimeFont;
                for (int i = 0; i < captionLabels.Length; i++) captionLabels[i].text = captions[i];
            }
        }
        public void SelectWall() => placement.Select(wall);
        public void SelectStairs() => placement.Select(stairs);
        public void Cancel() => placement.Cancel();
        public void Rotate() => placement.Rotate();
        public void WalkToWall() => sandbox.WalkToWall();
        public void CollapseUnderUnit() => sandbox.CollapseUnderUnit();

        void Update()
        {
            if (placement.Selected == null) selection.text = "지을 조각을 고르세요";
            else
            {
                string issue;
                switch (placement.Issue)
                {
                    case PlacementIssue.None: issue = "설치 가능"; break;
                    case PlacementIssue.OutsideGrid: issue = "격자 밖입니다"; break;
                    case PlacementIssue.Occupied: issue = "다른 조각과 겹칩니다"; break;
                    case PlacementIssue.ReservedGate: issue = "고정 성문 자리입니다"; break;
                    case PlacementIssue.UnitInTheWay: issue = "유닛과 겹칩니다"; break;
                    default: issue = "위치를 고르세요"; break;
                }
                selection.text = placement.Selected.DisplayName + " · " + placement.Rotation * 90 + "°\n" + issue;
                if (placement.IsPreviewVisible && placement.StairNeedsConnection) selection.text += "\n계단이 아직 성벽과 연결되지 않았습니다";
            }
            status.text = sandbox.Message;
            if (sandbox.Unit.Health != null)
                unitHealth.text = "시험 유닛 체력  " + sandbox.Unit.Health.Current.ToString("0") + " / " + sandbox.Unit.Health.Maximum.ToString("0");
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
