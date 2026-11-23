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
        InputAction interact, close, equipment, organization, startBattle, squadPlacement, wait, enemyPreview, keyList;
        public string EnemyPreviewKey => enemyPreview != null ? enemyPreview.GetBindingDisplayString() : string.Empty;
        /// <summary>1인칭 탐색 중 적 목록 키를 눌렀다(C-5). 적 목록 창이 듣고 열거나 닫는다.</summary>
        public event System.Action EnemyPreviewToggled;
        public string KeyListKey => keyList != null ? keyList.GetBindingDisplayString() : string.Empty;
        /// <summary>1인칭 탐색 중 전체 키 목록 키를 눌렀다(UI-06). 키 목록 창이 듣고 열거나 닫는다.</summary>
        public event System.Action KeyListToggled;
        float waitElapsed;
        bool holdingWait;
        /// <summary>기다리기 키를 누르고 있는 진행률(0~1, PREP-06).</summary>
        public float WaitProgress => Mathf.Clamp01(waitElapsed / settings.WaitHoldSeconds);
        public string WaitKey => wait != null ? wait.GetBindingDisplayString() : string.Empty;
        /// <summary>지금 1인칭 탐색에서 기다리기를 할 수 있는가. 준비 시간이 한 번 기다릴 만큼 남아 있어야 한다.</summary>
        public bool CanWait => CanStartBattle && battle.RunLink.State != null && battle.RunLink.State.CanWait;
        /// <summary>기다리기로 시계가 갔다(PREP-06).</summary>
        public event System.Action Waited;
        public string PlacementKey => squadPlacement != null ? squadPlacement.GetBindingDisplayString() : string.Empty;
        float battleStartElapsed;
        bool holdingBattleStart;
        public float BattleStartProgress => Mathf.Clamp01(battleStartElapsed / settings.BattleStartHoldSeconds);
        public string BattleStartKey => startBattle != null ? startBattle.GetBindingDisplayString() : string.Empty;
        public bool CanStartBattle => battle != null && battle.CanStart && phaseView.IsExplorationView
            && Cursor.lockState == CursorLockMode.Locked;
        float blockedStartRemaining;
        /// <summary>전투를 시작할 수 없을 때 시작 키를 눌러, 이유를 잠깐 보여 줄 때인가(UI-12).</summary>
        public bool ShowingBlockedStart => blockedStartRemaining > 0f;
        /// <summary>마지막으로 막힌 시작 키를 눌렀을 때 커서가 잠겨 있었는가. false면 화면을 클릭하라고 알린다.</summary>
        public bool BlockedStartCursorLocked { get; private set; }
        public PreparationStation Target { get; private set; }
        public string InteractKey => interact.GetBindingDisplayString();
        public string CloseKey => close.GetBindingDisplayString();
        public string EquipmentKey => equipment != null ? equipment.GetBindingDisplayString() : string.Empty;
        /// <summary>1인칭 탐색 중 장비 키를 눌렀다(D-7). 장비 화면이 듣는다.</summary>
        public event System.Action EquipmentRequested;
        /// <summary>창이 열린 동안 장비 키를 눌렀다(UI-07: 장비 창은 연 키로 다시 닫는다).</summary>
        public event System.Action EquipmentKeyInPanel;
        /// <summary>창이 열린 동안 편성 키를 눌렀다(UI-07: 편성 창은 연 키로 다시 닫는다).</summary>
        public event System.Action OrganizationKeyInPanel;
        public string OrganizationKey => organization != null ? organization.GetBindingDisplayString() : string.Empty;
        /// <summary>1인칭 탐색 중 편성 키를 눌렀다(D-8). 편성 화면이 듣는다.</summary>
        public event System.Action OrganizationRequested;

        public void Configure(GamePhaseView view, Camera camera, CharacterActor actor, PreparationInteractionSettings config)
        { phaseView = view; playerCamera = camera; player = actor; settings = config; }
        public void ConfigureBattle(GameBattleController controller) => battle = controller;

        void Awake()
        {
            interact = settings.Interact.Clone();
            close = settings.Close.Clone();
            equipment = settings.Equipment?.Clone();
            organization = settings.Organization?.Clone();
            startBattle = settings.StartBattle.Clone();
            // 예전 설정 에셋에 키가 비어 있어도 P로 연다.
            squadPlacement = settings.SquadPlacement != null && settings.SquadPlacement.bindings.Count > 0
                ? settings.SquadPlacement.Clone()
                : new InputAction("OpenSquadPlacement", InputActionType.Button, "<Keyboard>/p");
            // 예전 설정 에셋에 키가 비어 있어도 T로 기다린다.
            wait = settings.Wait != null && settings.Wait.bindings.Count > 0
                ? settings.Wait.Clone()
                : new InputAction("WaitPrepTime", InputActionType.Button, "<Keyboard>/t");
            // 예전 설정 에셋에 키가 비어 있어도 V로 적 목록을 연다.
            enemyPreview = settings.EnemyPreview != null && settings.EnemyPreview.bindings.Count > 0
                ? settings.EnemyPreview.Clone()
                : new InputAction("ToggleEnemyPreview", InputActionType.Button, "<Keyboard>/v");
            // 예전 설정 에셋에 키가 비어 있어도 H로 전체 키 목록을 연다.
            keyList = settings.KeyList != null && settings.KeyList.bindings.Count > 0
                ? settings.KeyList.Clone()
                : new InputAction("ToggleKeyList", InputActionType.Button, "<Keyboard>/h");
        }
        void OnEnable()
        {
            interact.Enable(); close.Enable(); equipment?.Enable(); organization?.Enable(); startBattle.Enable(); squadPlacement?.Enable();
            wait?.Enable(); enemyPreview?.Enable(); keyList?.Enable();
        }
        void OnDisable()
        {
            interact?.Disable(); close?.Disable(); equipment?.Disable(); organization?.Disable(); startBattle?.Disable();
            squadPlacement?.Disable(); wait?.Disable(); enemyPreview?.Disable(); keyList?.Disable();
            Target = null; CancelBattleStart(); CancelWait(); blockedStartRemaining = 0f;
        }
        void OnDestroy()
        {
            interact?.Dispose(); close?.Dispose(); equipment?.Dispose(); organization?.Dispose(); startBattle?.Dispose();
            squadPlacement?.Dispose(); wait?.Dispose(); enemyPreview?.Dispose(); keyList?.Dispose();
        }

        void CancelBattleStart() { holdingBattleStart = false; battleStartElapsed = 0f; }
        void CancelWait() { holdingWait = false; waitElapsed = 0f; }

        void UpdateWait()
        {
            if (wait == null || !CanWait || !wait.IsPressed()) { CancelWait(); return; }
            // 모달·포커스 잃음·한 번 기다린 뒤에도 누르고 있던 키는 떼었다 다시 눌러야 한다.
            if (wait.WasPressedThisFrame()) holdingWait = true;
            if (!holdingWait) return;
            waitElapsed += Time.unscaledDeltaTime;
            if (waitElapsed < settings.WaitHoldSeconds) return;
            CancelWait();
            if (battle.RunLink.State.TryWait()) Waited?.Invoke();
        }

        /// <summary>테스트·자동 조작용: 키 없이 기다리기 한 번. 할 수 없으면 false.</summary>
        public bool TryWait()
        {
            if (!CanWait || !battle.RunLink.State.TryWait()) return false;
            Waited?.Invoke();
            return true;
        }

        void UpdateBattleStart()
        {
            if (!CanStartBattle)
            {
                // UI-12: 막힌 동안 누르면 그때만 이유를 잠깐 띄운다(다시 누르면 시간을 처음부터 센다).
                if (phaseView.IsExplorationView && battle != null && startBattle.WasPressedThisFrame())
                {
                    BlockedStartCursorLocked = Cursor.lockState == CursorLockMode.Locked;
                    blockedStartRemaining = settings.BlockedStartNoticeSeconds;
                }
                CancelBattleStart(); return;
            }
            if (!startBattle.IsPressed()) { CancelBattleStart(); return; }
            // A key held through a modal, focus loss or phase transition must be released and pressed again.
            if (startBattle.WasPressedThisFrame()) holdingBattleStart = true;
            if (!holdingBattleStart) return;
            battleStartElapsed += Time.unscaledDeltaTime;
            if (battleStartElapsed < settings.BattleStartHoldSeconds) return;
            CancelBattleStart();
            battle.TryStartBattle();
        }

        void OnApplicationFocus(bool focused) { if (!focused) { CancelBattleStart(); CancelWait(); } }

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
            if (blockedStartRemaining > 0f) blockedStartRemaining -= Time.unscaledDeltaTime;
            if (phaseView.IsBuildView)
            {
                CancelBattleStart(); CancelWait();
                Target = null;
                if (close.WasPressedThisFrame()) phaseView.ExitConstruction();
                return;
            }
            if (phaseView.IsPanelView)
            {
                CancelBattleStart(); CancelWait();
                Target = null;
                if (close.WasPressedThisFrame()) phaseView.ExitPanel();
                else if (equipment != null && equipment.WasPressedThisFrame()) EquipmentKeyInPanel?.Invoke();
                else if (organization != null && organization.WasPressedThisFrame()) OrganizationKeyInPanel?.Invoke();
                return;
            }
            if (phaseView.IsPlacementView)
            {
                CancelBattleStart(); CancelWait();
                Target = null;
                if (close.WasPressedThisFrame() || squadPlacement.WasPressedThisFrame()) phaseView.ExitPlacement();
                return;
            }
            RefreshTarget();
            if (interact.WasPressedThisFrame()) { CancelBattleStart(); CancelWait(); TryInteract(); }
            else if (phaseView.IsExplorationView && squadPlacement.WasPressedThisFrame())
            { CancelBattleStart(); CancelWait(); phaseView.EnterPlacement(); }
            else if (phaseView.IsExplorationView && equipment != null && equipment.WasPressedThisFrame())
            { CancelBattleStart(); CancelWait(); EquipmentRequested?.Invoke(); }
            else if (phaseView.IsExplorationView && organization != null && organization.WasPressedThisFrame())
            { CancelBattleStart(); CancelWait(); OrganizationRequested?.Invoke(); }
            else
            {
                // 적 목록 창은 화면을 막지 않으므로 전투 시작·기다리기 누르기를 끊지 않는다.
                if (phaseView.IsExplorationView && enemyPreview != null && enemyPreview.WasPressedThisFrame())
                    EnemyPreviewToggled?.Invoke();
                if (phaseView.IsExplorationView && keyList != null && keyList.WasPressedThisFrame())
                    KeyListToggled?.Invoke();
                UpdateBattleStart(); UpdateWait();
            }
        }
    }
}
