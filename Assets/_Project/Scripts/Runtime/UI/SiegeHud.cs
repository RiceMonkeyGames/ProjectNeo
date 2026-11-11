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
        [SerializeField] TMP_Text[] captionLabels = new TMP_Text[0];
        [SerializeField] string[] captions = new string[0];
        TMP_FontAsset runtimeFont;

        public void Configure(SiegeDirector siegeDirector, SiegePrepController prepController,
            PlacementController placementController, SiegeSettings siegeSettings, BuildPieceDefinition wallPiece,
            BuildPieceDefinition stairPiece, SiegeWeaponDefinition[] weaponChoices, TMP_Text budgetLabel,
            TMP_Text selectionLabel, TMP_Text statusLabel, TMP_Text enemyLabel, TMP_Text[] translatedLabels,
            string[] translatedCaptions)
        {
            director = siegeDirector; prep = prepController; placement = placementController; settings = siegeSettings;
            wall = wallPiece; stairs = stairPiece; weapons = weaponChoices; budget = budgetLabel;
            selection = selectionLabel; status = statusLabel; enemies = enemyLabel;
            captionLabels = translatedLabels; captions = translatedCaptions;
        }

        void Awake()
        {
            // 시스템 글꼴을 런타임에 사용한다. 운영체제 글꼴 파일을 프로젝트에 복사하지 않는다.
            runtimeFont = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular");
            if (runtimeFont == null) return;
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) text.font = runtimeFont;
            for (int i = 0; i < captionLabels.Length; i++) captionLabels[i].text = captions[i];
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
                    SquadCommandHud.SquadName(center, i) + "  (" + center.Squads[i].Members.Count + "명)";
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
            if (runtimeFont == null) return;
            budget.text ="코스트  " + prep.Budget.Remaining + " / " + prep.Budget.Total;
            enemies.text = "다가오는 적\n사다리 " + director.CountEnemies(SiegeAttackerRole.Ladder)
                + " · 성문 파괴 " + director.CountEnemies(SiegeAttackerRole.GateBreaker)
                + " · 궁수 " + director.CountEnemies(SiegeAttackerRole.Archer);
            status.text = prep.Message;
            if (prep.SelectedSquad >= 0) selection.text = (prep.SelectedSquad + 1) + "번 부대 배치\n땅이나 성벽 위를 클릭";
            else if (prep.Dismantling) selection.text = "철거\n이번에 지은 조각을 클릭";
            else if (placement.SelectedWeapon != null)
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
                selection.text = placement.SelectedWeapon.DisplayName + " · 코스트 "
                    + settings.CostOf(placement.SelectedWeapon) + "\n" + issue;
            }
            else if (placement.Selected == null) selection.text = "지을 조각이나 배치할 부대를 고르세요";
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
                selection.text = placement.Selected.DisplayName + " · 코스트 " + settings.CostOf(placement.Selected)
                    + " · " + placement.Rotation * 90 + "°\n" + issue;
                if (placement.IsPreviewVisible && placement.StairNeedsConnection)
                    selection.text += "\n계단이 아직 성벽과 연결되지 않았습니다";
            }
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
