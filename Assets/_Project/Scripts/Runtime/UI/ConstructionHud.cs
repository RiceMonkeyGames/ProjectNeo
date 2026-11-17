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
                    case MountIssue.None: issue = "Can place"; break;
                    case MountIssue.NoSupport: issue = "Point at a wall top"; break;
                    case MountIssue.NoMountPoint: issue = "This piece has no mount point"; break;
                    case MountIssue.Occupied: issue = "A weapon already occupies this mount"; break;
                    default: issue = "Choose a location"; break;
                }
                selection.text = placement.SelectedWeapon.DisplayName + "\n" + issue;
            }
            else if (placement.Selected == null) selection.text = "Choose a piece to build";
            else
            {
                string issue;
                switch (placement.Issue)
                {
                    case PlacementIssue.None: issue = "Can place"; break;
                    case PlacementIssue.OutsideGrid: issue = "Outside the grid"; break;
                    case PlacementIssue.Occupied: issue = "Overlaps another piece"; break;
                    case PlacementIssue.ReservedGate: issue = "Reserved gate location"; break;
                    case PlacementIssue.UnitInTheWay: issue = "Overlaps a unit"; break;
                    case PlacementIssue.UnevenSupport: issue = "Support heights differ"; break;
                    default: issue = "Choose a location"; break;
                }
                string floor = placement.PreviewLevel > 0 ? " · " + (placement.PreviewLevel + 1) + " floor" : "";
                selection.text = placement.Selected.DisplayName + " · " + placement.Rotation * 90 + "°" + floor + "\n" + issue;
                if (placement.IsPreviewVisible && placement.StairNeedsConnection) selection.text += "\nStairs are not connected to a wall yet";
            }
            status.text = sandbox.Message;
            if (sandbox.Unit.Health != null)
                unitHealth.text = "Test unit HP  " + sandbox.Unit.Health.Current.ToString("0") + " / " + sandbox.Unit.Health.Maximum.ToString("0");
        }

    }
}
