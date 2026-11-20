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
        // 시험 화면에서 고를 수성 무기 목록. 무기 종류가 늘면 에셋과 버튼만 추가한다.
        [SerializeField] SiegeWeaponDefinition[] weapons = new SiegeWeaponDefinition[0];
        public System.Collections.Generic.IReadOnlyList<SiegeWeaponDefinition> Weapons => weapons;

        public void ConfigureWeapons(SiegeWeaponDefinition[] weaponChoices)
        {
            weapons = weaponChoices;
        }
        public void Configure(PlacementController controller, ConstructionSandbox demo, BuildPieceDefinition wallPiece,
            BuildPieceDefinition stairPiece, TMP_Text selectionLabel, TMP_Text statusLabel, TMP_Text healthLabel)
        {
            placement = controller; sandbox = demo; wall = wallPiece; stairs = stairPiece;
            selection = selectionLabel; status = statusLabel; unitHealth = healthLabel;
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
                    case MountIssue.None: issue = "배치 가능"; break;
                    case MountIssue.NoSupport: issue = "성벽 위를 가리키세요"; break;
                    case MountIssue.NoMountPoint: issue = "이 조각에는 설치 자리가 없습니다"; break;
                    case MountIssue.Occupied: issue = "이미 무기가 설치되어 있습니다"; break;
                    case MountIssue.UnfinishedSupport: issue = "이 조각은 아직 짓는 중입니다"; break;
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
                    case PlacementIssue.None: issue = "배치 가능"; break;
                    case PlacementIssue.OutsideGrid: issue = "격자 밖입니다"; break;
                    case PlacementIssue.Occupied: issue = "다른 조각과 겹칩니다"; break;
                    case PlacementIssue.ReservedGate: issue = "성문 자리입니다"; break;
                    case PlacementIssue.UnitInTheWay: issue = "유닛과 겹칩니다"; break;
                    case PlacementIssue.UnevenSupport: issue = "받침 높이가 다릅니다"; break;
                    case PlacementIssue.UnfinishedSupport: issue = "아래 조각이 아직 짓는 중입니다"; break;
                    default: issue = "위치를 고르세요"; break;
                }
                string floor = placement.PreviewLevel > 0 ? " · " + (placement.PreviewLevel + 1) + "층" : "";
                selection.text = placement.Selected.DisplayName + " · " + placement.Rotation * 90 + "°" + floor + "\n" + issue;
                if (placement.IsPreviewVisible && placement.StairNeedsConnection) selection.text += "\n계단이 아직 성벽과 이어지지 않았습니다";
            }
            status.text = sandbox.Message;
            if (sandbox.Unit.Health != null)
                unitHealth.text = "시험 유닛 HP  " + sandbox.Unit.Health.Current.ToString("0") + " / " + sandbox.Unit.Health.Maximum.ToString("0");
        }

    }
}
