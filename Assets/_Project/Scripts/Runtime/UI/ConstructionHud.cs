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
        // 시험 화면에서 고를 수성 무기 목록. 무기 종류가 늘면 에셋과 버튼만 추가한다.
        [SerializeField] SiegeWeaponDefinition[] weapons = new SiegeWeaponDefinition[0];
        TMP_FontAsset runtimeFont;
        public System.Collections.Generic.IReadOnlyList<SiegeWeaponDefinition> Weapons => weapons;

        public void ConfigureWeapons(SiegeWeaponDefinition[] weaponChoices, TMP_Text[] addedLabels, string[] addedCaptions)
        {
            weapons = weaponChoices;
            var labels = new System.Collections.Generic.List<TMP_Text>(captionLabels); labels.AddRange(addedLabels);
            var texts = new System.Collections.Generic.List<string>(captions); texts.AddRange(addedCaptions);
            captionLabels = labels.ToArray(); captions = texts.ToArray();
        }
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
        public void SelectWeapon(int index) => placement.SelectWeapon(weapons[index]);
        public void CollapseUnderWeapon() => sandbox.CollapseUnderWeapon();

        void Update()
        {
            if (placement.SelectedWeapon != null)
            {
                string issue;
                switch (placement.MountIssue)
                {
                    case MountIssue.None: issue = "설치 가능"; break;
                    case MountIssue.NoSupport: issue = "성벽 위를 가리키세요"; break;
                    case MountIssue.NoMountPoint: issue = "이 조각에는 설치할 자리가 없습니다"; break;
                    case MountIssue.Occupied: issue = "이미 무기가 설치된 자리입니다"; break;
                    default: issue = "위치를 고르세요"; break;
                }
                selection.text = placement.SelectedWeapon.DisplayName + "\n" + issue;
            }
            else if (placement.Selected == null) selection.text = "지을 조각을 고르세요";
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
