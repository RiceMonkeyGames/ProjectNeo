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
        // 수성 무기를 놓는 보루(SIEGE-11). 비어 있으면 버튼이 아무것도 고르지 않는다.
        [SerializeField] BuildPieceDefinition bastion;
        // 보루보다 비싸고 강한 2×2 망루(SIEGE-14, Game 통합 SIEGE-24). 비어 있으면 버튼이 아무것도 고르지 않는다.
        [SerializeField] BuildPieceDefinition tower;
        [SerializeField] TMP_Text selection;
        public const string PickPieceHint = "지을 조각을 고르세요";
        /// <summary>고른 조각이 없을 때 상태 띠 글. 작업 탭(PREP-11)은 주문 고르기 안내로 바꾼다.</summary>
        public string IdleHint { get; set; } = PickPieceHint;
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

        // 건설 막대(UI-13)가 카드로 보여 줄 조각들. 비어 있는 칸은 카드를 만들지 않는다.
        public BuildPieceDefinition Wall => wall;
        public BuildPieceDefinition Stairs => stairs;
        public BuildPieceDefinition Tower => tower;
        public PlacementController Placement => placement;
        public void Select(BuildPieceDefinition piece) { if (piece != null) placement.Select(piece); }
        public void SelectWeapon(SiegeWeaponDefinition weapon) { if (weapon != null) placement.SelectWeapon(weapon); }

        public void SelectWall() => placement.Select(wall);
        public void SelectStairs() => placement.Select(stairs);
        public void SelectBastion() { if (bastion != null) placement.Select(bastion); }
        public void ConfigureBastion(BuildPieceDefinition bastionPiece) => bastion = bastionPiece;
        public BuildPieceDefinition Bastion => bastion;
        public void SelectTower() { if (tower != null) placement.Select(tower); }
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
                    case MountIssue.NoSupport: issue = "보루나 망루 위를 가리키세요"; break;
                    case MountIssue.NoMountPoint: issue = "무기는 보루·망루 위에만 놓습니다"; break;
                    case MountIssue.Occupied: issue = "이미 무기가 설치되어 있습니다"; break;
                    case MountIssue.UnfinishedSupport: issue = "이 조각은 아직 짓는 중입니다"; break;
                    default: issue = "위치를 고르세요"; break;
                }
                selection.text = placement.SelectedWeapon.DisplayName + "\n" + issue;
            }
            else if (placement.Selected == null) selection.text = IdleHint;
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
                    case PlacementIssue.BastionNeedsWall: issue = "보루는 성 바깥에서 성벽에 붙여 짓습니다"; break;
                    case PlacementIssue.LadderLane: issue = "사다리가 서는 자리입니다"; break;
                    case PlacementIssue.BastionStack: issue = "보루 위에는 보루만 쌓습니다"; break;
                    case PlacementIssue.Valley: issue = "양옆보다 낮은 골짜기(2층-1층-2층)가 됩니다"; break;
                    case PlacementIssue.TowerNeedsWall: issue = "망루는 성벽 줄 두 칸을 바꿔 바깥쪽으로 내밀어 짓습니다"; break;
                    case PlacementIssue.TowerStack: issue = "망루 위에는 아무것도 쌓을 수 없습니다"; break;
                    case PlacementIssue.StairsGroundOnly: issue = "계단은 땅 위에만 놓습니다. 성벽 위 계단은 자동으로 생깁니다"; break;
                    case PlacementIssue.StoreyCap: issue = "이 조각은 더 높이 쌓을 수 없습니다"; break;
                    default: issue = "위치를 고르세요"; break;
                }
                string floor = placement.PreviewLevel > 0 ? " · " + (placement.PreviewLevel + 1) + "층" : "";
                // 한 번에 여러 층을 쌓으면(새 망루 2층, SIEGE-24) 층 수를 알린다. 비용은 층마다 낸다.
                if (placement.PreviewCount > 1) floor += " · " + placement.PreviewCount + "층 한 번에";
                selection.text = placement.Selected.DisplayName + " · " + placement.Rotation * 90 + "°" + floor + "\n" + issue;
                if (placement.IsPreviewVisible && placement.StairNeedsConnection) selection.text += "\n계단이 아직 성벽과 이어지지 않았습니다";
                if (placement.IsPreviewVisible && placement.BastionNeedsConnection)
                    selection.text += "\n보루가 아직 성벽 통로와 이어지지 않았습니다. 위에 보루를 더 쌓으세요";
            }
            status.text = sandbox.Message;
            if (sandbox.Unit.Health != null)
                unitHealth.text = "시험 유닛 HP  " + sandbox.Unit.Health.Current.ToString("0") + " / " + sandbox.Unit.Health.Maximum.ToString("0");
        }

    }
}
