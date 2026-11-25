using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using ProjectNeo.Walls;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 방어 시설 레벨의 성가퀴 모양을 성 전체에 적용하고(SIEGE-10), 건설 모드 패널의 성가퀴 업그레이드 버튼을 다룬다.
    /// 레벨은 업그레이드·런 되살리기·새 런으로 바뀌므로 매 프레임 지금 런의 값을 맞춘다(같으면 아무 일도 하지 않는다).
    /// 런에 방어 시설이 아직 없으면 시작 모양을 쓴다. 업그레이드는 시설 업그레이드와 같은 무료 행동이다(준비 시계를 쓰지 않는다).
    /// 준비 단계에 들어설 때 성 바깥 판정을 다시 한다(전투 중에는 성가퀴 면을 고정한다).
    /// </summary>
    public sealed class GameBattlements : MonoBehaviour
    {
        [SerializeField] GameBattleController battle;
        [SerializeField] CastleBattlements battlements;
        [SerializeField] BattlementStyle startStyle = BattlementStyle.Parapet;
        [SerializeField] GameFacilities facilities;
        [SerializeField] Button upgradeButton;
        [SerializeField] TMP_Text upgradeLabel;

        public CastleBattlements Battlements => battlements;
        public string UpgradeText => upgradeLabel != null ? upgradeLabel.text : string.Empty;
        public bool UpgradeInteractable => upgradeButton != null && upgradeButton.interactable;

        public void Configure(GameBattleController controller, CastleBattlements castle, BattlementStyle initial)
        { battle = controller; battlements = castle; startStyle = initial; }

        public void ConfigureUpgrade(GameFacilities facilityController, Button button, TMP_Text label)
        { facilities = facilityController; upgradeButton = button; upgradeLabel = label; }

        RunState State => battle != null ? battle.RunLink.State : null;

        /// <summary>지금 런에서 적용할 모양. 런이 없으면 null.</summary>
        public BattlementStyle? Wanted
        {
            get
            {
                RunState state = State;
                if (state == null) return null;
                FacilityState defense = state.Facility(FacilityType.Defense);
                return defense != null ? defense.Current.battlementStyle : startStyle;
            }
        }

        // 시설 목록을 런에 채운 뒤의 방어 시설. 시설 창을 한 번도 열지 않은 런에서도 업그레이드할 수 있게 한다.
        FacilityState Defense => facilities != null ? facilities.Facility(FacilityType.Defense) : State?.Facility(FacilityType.Defense);

        static bool InPreparation => GameSession.Flow != null && GameSession.Flow.Phase == GamePhase.Preparation;

        /// <summary>방어 시설을 한 레벨 올린다. 준비 단계가 아니거나 최대 레벨이거나 골드가 모자라면 false.</summary>
        public bool TryUpgrade()
        {
            RunState state = State;
            if (state == null || !InPreparation) return false;
            FacilityState defense = Defense;
            if (defense == null || defense.IsMaxLevel) return false;
            bool upgraded = state.TryUpgradeFacility(FacilityType.Defense);
            if (upgraded) Apply();
            RefreshButton();
            return upgraded;
        }

        public void TryUpgradeFromButton() => TryUpgrade();

        GamePhase? lastPhase;

        void Update()
        {
            // 준비 단계에 들어서면 전투 중 무너진 자리까지 반영해 성 바깥을 다시 판정한다(BUG-battlement-inner-faces).
            GamePhase? phase = GameSession.Flow != null ? GameSession.Flow.Phase : (GamePhase?)null;
            if (phase == GamePhase.Preparation && lastPhase.HasValue && lastPhase != GamePhase.Preparation && battlements != null)
                battlements.RecomputeOutside();
            lastPhase = phase;
            Apply();
            RefreshButton();
        }

        void Apply()
        {
            if (battlements == null) return;
            BattlementStyle? wanted = Wanted;
            if (wanted.HasValue) battlements.SetStyle(wanted.Value);
        }

        void RefreshButton()
        {
            if (upgradeButton == null || !upgradeButton.isActiveAndEnabled) return;
            RunState state = State;
            FacilityState defense = state != null ? Defense : null;
            string text;
            bool interactable = false;
            // 건설 막대 오른쪽 도구 칸에 들어가도록 짧게(UI-13).
            if (defense == null) text = "흉벽";
            else if (defense.IsMaxLevel) text = $"흉벽 Lv{defense.Level + 1} (최대)";
            else
            {
                Cost cost = defense.NextUpgradeCost;
                // 시설 강화라 준비 시간이 들고 장인 1명이 일한다(PREP-11). 도구 칸이 좁아 시간만 붙인다.
                text = $"흉벽 강화 · {cost.gold}골드" + GameFacilities.WorkTime(state, state.FacilityUpgradeHours, 0);
                interactable = InPreparation && state.CheckUpgradeFacility(FacilityType.Defense) == FacilityUpgradeCheck.Ok;
            }
            if (upgradeLabel != null && upgradeLabel.text != text) upgradeLabel.text = text;
            if (upgradeButton.interactable != interactable) upgradeButton.interactable = interactable;
        }
    }
}
