using ProjectNeo.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Flow
{
    /// <summary>준비 중 화면 가운데로 가까운 건설소를 바라보고 F로 열며 Esc로 돌아간다.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PreparationInteraction : MonoBehaviour
    {
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] Camera playerCamera;
        [SerializeField] CharacterActor player;
        [SerializeField] PreparationInteractionSettings settings;
        [SerializeField] GameBattleController battle;
        InputAction interact, close, equipment, startBattle;
        float battleStartElapsed;
        bool holdingBattleStart;
        public float BattleStartProgress => Mathf.Clamp01(battleStartElapsed / settings.BattleStartHoldSeconds);
        public string BattleStartKey => startBattle != null ? startBattle.GetBindingDisplayString() : string.Empty;
        public bool CanStartBattle => battle != null && battle.CanStart && phaseView.IsExplorationView
            && Cursor.lockState == CursorLockMode.Locked;
        public PreparationStation Target { get; private set; }
        public string InteractKey => interact.GetBindingDisplayString();
        public string CloseKey => close.GetBindingDisplayString();
        public string EquipmentKey => equipment != null ? equipment.GetBindingDisplayString() : string.Empty;
        /// <summary>1인칭 탐색 중 장비 키를 눌렀다(D-7). 장비 화면이 듣는다.</summary>
        public event System.Action EquipmentRequested;

        public void Configure(GamePhaseView view, Camera camera, CharacterActor actor, PreparationInteractionSettings config)
        { phaseView = view; playerCamera = camera; player = actor; settings = config; }
        public void ConfigureBattle(GameBattleController controller) => battle = controller;

        void Awake()
        {
            interact = settings.Interact.Clone();
            close = settings.Close.Clone();
            equipment = settings.Equipment?.Clone();
            startBattle = settings.StartBattle.Clone();
        }
        void OnEnable() { interact.Enable(); close.Enable(); equipment?.Enable(); startBattle.Enable(); }
        void OnDisable() { interact?.Disable(); close?.Disable(); equipment?.Disable(); startBattle?.Disable(); Target = null; CancelBattleStart(); }
        void OnDestroy() { interact?.Dispose(); close?.Dispose(); equipment?.Dispose(); startBattle?.Dispose(); }

        void CancelBattleStart() { holdingBattleStart = false; battleStartElapsed = 0f; }

        void UpdateBattleStart()
        {
            if (!CanStartBattle || !startBattle.IsPressed()) { CancelBattleStart(); return; }
            // A key held through a modal, focus loss or phase transition must be released and pressed again.
            if (startBattle.WasPressedThisFrame()) holdingBattleStart = true;
            if (!holdingBattleStart) return;
            battleStartElapsed += Time.unscaledDeltaTime;
            if (battleStartElapsed < settings.BattleStartHoldSeconds) return;
            CancelBattleStart();
            battle.TryStartBattle();
        }

        void OnApplicationFocus(bool focused) { if (!focused) CancelBattleStart(); }

        public void RefreshTarget()
        {
            Target = null;
            if (!phaseView.IsExplorationView || Cursor.lockState != CursorLockMode.Locked
                || player.Health == null || player.Health.IsDead) return;
            if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward,
                out RaycastHit hit, settings.Distance, ~0, QueryTriggerInteraction.Ignore))
            {
                var station = hit.collider.GetComponentInParent<PreparationStation>();
                if (station != null && !(station is BattleStartStation) && station.PhaseView == phaseView) Target = station;
            }
        }

        public bool TryInteract()
        {
            RefreshTarget();
            bool opened = Target != null && Target.Interact();
            if (opened) Target = null;
            return opened;
        }

        void Update()
        {
            if (phaseView.IsBuildView)
            {
                CancelBattleStart();
                Target = null;
                if (close.WasPressedThisFrame()) phaseView.ExitConstruction();
                return;
            }
            if (phaseView.IsPanelView)
            {
                CancelBattleStart();
                Target = null;
                if (close.WasPressedThisFrame()) phaseView.ExitPanel();
                return;
            }
            RefreshTarget();
            if (interact.WasPressedThisFrame()) { CancelBattleStart(); TryInteract(); }
            else if (phaseView.IsExplorationView && equipment != null && equipment.WasPressedThisFrame())
            { CancelBattleStart(); EquipmentRequested?.Invoke(); }
            else UpdateBattleStart();
        }
    }
}
