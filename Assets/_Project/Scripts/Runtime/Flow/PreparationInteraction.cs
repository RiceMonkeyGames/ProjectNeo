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
        InputAction interact, close;
        public PreparationStation Target { get; private set; }
        public string InteractKey => interact.GetBindingDisplayString();
        public string CloseKey => close.GetBindingDisplayString();

        public void Configure(GamePhaseView view, Camera camera, CharacterActor actor, PreparationInteractionSettings config)
        { phaseView = view; playerCamera = camera; player = actor; settings = config; }

        void Awake()
        {
            interact = settings.Interact.Clone();
            close = settings.Close.Clone();
        }
        void OnEnable() { interact.Enable(); close.Enable(); }
        void OnDisable() { interact?.Disable(); close?.Disable(); Target = null; }
        void OnDestroy() { interact?.Dispose(); close?.Dispose(); }

        public void RefreshTarget()
        {
            Target = null;
            if (!phaseView.IsExplorationView || Cursor.lockState != CursorLockMode.Locked
                || player.Health == null || player.Health.IsDead) return;
            if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward,
                out RaycastHit hit, settings.Distance, ~0, QueryTriggerInteraction.Ignore))
            {
                var station = hit.collider.GetComponentInParent<PreparationStation>();
                if (station != null && station.PhaseView == phaseView) Target = station;
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
                Target = null;
                if (close.WasPressedThisFrame()) phaseView.ExitConstruction();
                return;
            }
            if (phaseView.IsPanelView)
            {
                Target = null;
                if (close.WasPressedThisFrame()) phaseView.ExitPanel();
                return;
            }
            RefreshTarget();
            if (interact.WasPressedThisFrame()) TryInteract();
        }
    }
}
