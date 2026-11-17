using ProjectNeo.Siege;
using ProjectNeo.Walls;
using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>수성전 프로토타입의 준비 단계 건설 화면. 시험용이며 정식 준비 화면은 아니다.</summary>
    public sealed class SiegeHud : MonoBehaviour
    {
        [SerializeField] SiegeDirector director;
        [SerializeField] SiegePrepController prep;
        [SerializeField] PlacementController placement;
        [SerializeField] SiegeSettings settings;
        [SerializeField] BuildPieceDefinition wall;
        [SerializeField] BuildPieceDefinition stairs;
        [SerializeField] SiegeWeaponDefinition[] weapons = new SiegeWeaponDefinition[0];
        [SerializeField] TMP_Text budget;
        [SerializeField] TMP_Text selection;
        [SerializeField] TMP_Text status;
        [SerializeField] TMP_Text enemies;

        public void Configure(SiegeDirector siegeDirector, SiegePrepController prepController,
            PlacementController placementController, SiegeSettings siegeSettings, BuildPieceDefinition wallPiece,
            BuildPieceDefinition stairPiece, SiegeWeaponDefinition[] weaponChoices, TMP_Text budgetLabel,
            TMP_Text selectionLabel, TMP_Text statusLabel, TMP_Text enemyLabel)
        {
            director = siegeDirector; prep = prepController; placement = placementController; settings = siegeSettings;
            wall = wallPiece; stairs = stairPiece; weapons = weaponChoices; budget = budgetLabel;
            selection = selectionLabel; status = statusLabel; enemies = enemyLabel;
        }


        // 부대 배치 버튼. 실제 편성된 부대 수만큼만 보여준다.
        [SerializeField] ProjectNeo.Squads.SquadCommandCenter center;
        [SerializeField] GameObject[] squadButtons = new GameObject[0];
        [SerializeField] TMP_Text[] squadLabels = new TMP_Text[0];

        public void ConfigureSquads(ProjectNeo.Squads.SquadCommandCenter commandCenter, GameObject[] buttons, TMP_Text[] buttonLabels)
        {
            center = commandCenter; squadButtons = buttons; squadLabels = buttonLabels;
        }

        // 화면 높이(기준 720) 안에 패널을 넣을 때 남길 여백.
        [SerializeField, Min(0f)] float panelMargin = 20f;

        // 부대 버튼을 실제 편성된 부대 수에 맞춘다. 미리 만든 버튼보다 부대가 많으면 첫 버튼을 복제해 늘리고,
        // 늘어난 만큼 패널을 키운다. 화면보다 길어지면 패널 전체를 줄여서 넣는다.
        void Start()
        {
            if (center == null || squadButtons.Length == 0) return;
            GameObject template = squadButtons[0];
            var panel = (RectTransform)template.transform.parent;
            int baked = squadButtons.Length, count = center.Squads.Count;
            var buttons = new System.Collections.Generic.List<GameObject>(squadButtons);
            for (int i = baked; i < count; i++)
            {
                GameObject extra = Instantiate(template, panel);
                extra.name = "Button_Squad" + (i + 1);
                extra.transform.SetSiblingIndex(buttons[i - 1].transform.GetSiblingIndex() + 1);
                // 복제한 버튼에는 원래 버튼의 부대 번호 연결이 딸려 오므로 비우고 새로 건다.
                var button = extra.GetComponent<UnityEngine.UI.Button>();
                button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                int squad = i;
                button.onClick.AddListener(() => SelectSquad(squad));
                buttons.Add(extra);
            }
            for (int i = 0; i < buttons.Count; i++)
            {
                bool exists = i < count;
                buttons[i].SetActive(exists);
                if (exists) buttons[i].GetComponentInChildren<TMP_Text>(true).text =
                    SquadCommandHud.SquadName(center, i) + "  (" + center.Squads[i].Members.Count + " units)";
            }
            var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            float step = template.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight + (layout != null ? layout.spacing : 0f);
            panel.sizeDelta += new Vector2(0f, (count - baked) * step);
            fittedPanel = panel;
            FitPanel();
        }

        RectTransform fittedPanel;
        float fittedHeight = -1f;

        // 패널이 화면보다 길면 전체를 줄여서 넣는다. 창 크기나 화면 비율이 바뀌면 다시 맞춘다.
        void FitPanel()
        {
            if (fittedPanel == null) return;
            float available = ((RectTransform)transform).rect.height;
            if (available <= 0f || Mathf.Approximately(available, fittedHeight)) return;
            fittedHeight = available;
            fittedPanel.localScale = Vector3.one * Mathf.Min(1f, (available - panelMargin) / fittedPanel.sizeDelta.y);
        }

        public void SelectWall() { prep.CancelTool(); placement.Select(wall); }
        public void SelectStairs() { prep.CancelTool(); placement.Select(stairs); }
        public void SelectWeapon(int index) { prep.CancelTool(); placement.SelectWeapon(weapons[index]); }
        public void Rotate() => placement.Rotate();
        public void Cancel() { placement.Cancel(); prep.CancelTool(); }
        public void Dismantle() => prep.BeginDismantle();
        public void SelectSquad(int index) => prep.SelectSquad(index);
        public void StartBattle() => director.StartBattle();

        void Update()
        {
            FitPanel();
            budget.text ="Budget  " + prep.Budget.Remaining + " / " + prep.Budget.Total;
            enemies.text = "Incoming enemies\nLadders " + director.CountEnemies(SiegeAttackerRole.Ladder)
                + " - Gate breakers " + director.CountEnemies(SiegeAttackerRole.GateBreaker)
                + " - Archers " + director.CountEnemies(SiegeAttackerRole.Archer);
            status.text = prep.Message;
            if (prep.SelectedSquad >= 0) selection.text = (prep.SelectedSquad + 1) + ": Place squad\nClick ground or a wall top";
            else if (prep.Dismantling) selection.text = "Dismantle\nClick a piece built this preparation";
            else if (placement.SelectedWeapon != null)
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
                selection.text = placement.SelectedWeapon.DisplayName + " - Cost "
                    + settings.CostOf(placement.SelectedWeapon) + "\n" + issue;
            }
            else if (placement.Selected == null) selection.text = "Choose a piece or squad to place";
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
                    default: issue = "Choose a location"; break;
                }
                selection.text = placement.Selected.DisplayName + " - Cost " + settings.CostOf(placement.Selected)
                    + " · " + placement.Rotation * 90 + "°\n" + issue;
                if (placement.IsPreviewVisible && placement.StairNeedsConnection)
                    selection.text += "\nStairs are not connected to a wall yet";
            }
        }

    }
}
