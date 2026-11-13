using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Siege
{
    public enum SiegePhase { Preparation = 0, Battle = 1, Finished = 2 }

    /// <summary>
    /// 수성전 프로토타입 한 판의 진행. 전투 준비(건설·배치) → 전투(건설 불가, 적 침공) → 결과.
    /// 아군 부대와 적 무리는 씬에 미리 두지 않고, 시작할 때 SiegeSettings의 편성대로 만든다.
    /// 준비 중에는 Tab으로 건설 시점과 1인칭을 오가고, 전투가 시작되면 1인칭으로 고정한다.
    /// 정식 런 진행·보상과는 아직 연결하지 않았다.
    /// </summary>
    [DefaultExecutionOrder(-75)]
    public sealed class SiegeDirector : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField] SiegeSettings settings;
        [SerializeField] SiegePrepController prep;
        [SerializeField] SquadCommandCenter center;
        [SerializeField] Camera constructionCamera;
        [SerializeField] FirstPersonCamera commandCamera;
        [SerializeField] PlacementController placement;
        [SerializeField] PlayerInputController playerInput;
        [SerializeField] SquadCommandInput commandInput;
        [SerializeField] GameObject constructionHud, commandHud;
        [SerializeField] CharacterActor player;
        [Header("유닛 생성")]
        [SerializeField] UnitAISettings allyAI;
        [SerializeField] FallSettings fall;
        [SerializeField] SiegeLadder[] ladders = new SiegeLadder[0];
        // 성 바깥을 향하는 격자 방향.
        [SerializeField] Vector2Int outward = new Vector2Int(0, 1);
        // 부대마다 처음 서는 자리. 부대 수가 이보다 많으면 마지막 자리 뒤로 줄을 세운다.
        [SerializeField] Vector3[] allySpawnPoints = new Vector3[0];
        [SerializeField, Min(1)] int allyColumns = 5;
        [SerializeField, Min(0.1f)] float allySpacing = 1.1f;
        [SerializeField, Min(1)] int allyExtraColumns = 4;
        [SerializeField, Min(0.1f)] float allyExtraSpacing = 8f;
        [SerializeField, Min(0.1f)] float allyExtraRowSpacing = 4.5f;
        [SerializeField, Min(0f)] float enemyEdgeMargin = 1.5f;
        // 적 첫 줄의 가운데. 무리마다, 그리고 한 줄이 차면 성 바깥쪽으로 다음 줄을 세운다.
        [SerializeField] Vector3 enemySpawnPoint;
        [SerializeField, Min(1)] int enemyColumns = 10;
        [SerializeField, Min(0.1f)] float enemySpacing = 2f;
        [SerializeField, Min(0.1f)] float enemyRowSpacing = 2.5f;
        readonly List<SiegeAttacker> enemies = new List<SiegeAttacker>();
        InputAction toggle, restart;
        public SiegePhase Phase { get; private set; } = SiegePhase.Preparation;
        public SiegeOutcome Outcome { get; private set; } = SiegeOutcome.Ongoing;
        public bool IsConstructionView { get; private set; } = true;
        public IReadOnlyList<SiegeAttacker> Enemies => enemies;
        public SiegeSettings Settings => settings;

        public void Configure(ConstructionWorld owner, SiegeSettings siegeSettings, SiegePrepController prepController,
            SquadCommandCenter commandCenter, Camera buildCamera, FirstPersonCamera firstPersonCamera,
            PlacementController placementController, PlayerInputController playerController, SquadCommandInput squadInput,
            GameObject buildHud, GameObject squadHud, CharacterActor playerActor)
        {
            world = owner; settings = siegeSettings; prep = prepController; center = commandCenter;
            constructionCamera = buildCamera; commandCamera = firstPersonCamera; placement = placementController;
            playerInput = playerController; commandInput = squadInput; constructionHud = buildHud; commandHud = squadHud;
            player = playerActor;
        }

        public void ConfigureSpawns(UnitAISettings allyAISettings, FallSettings fallSettings, SiegeLadder[] ladderSpots,
            Vector2Int outwardDirection, Vector3[] allyPoints, Vector3 enemyPoint)
        {
            allyAI = allyAISettings; fall = fallSettings; ladders = ladderSpots; outward = outwardDirection;
            allySpawnPoints = allyPoints; enemySpawnPoint = enemyPoint;
        }

        void Awake()
        {
            // ConstructionWorld(-200)가 길을 만든 뒤, 지휘 센터(-50)가 편성을 읽기 전에 유닛을 만든다.
            center.ConfigureRosters(SpawnAllies());
            player.gameObject.SetActive(true);
            SpawnEnemies();
            toggle = settings.SwitchView.Clone();
            restart = settings.Restart.Clone();
            ShowView(true);
        }

        void OnEnable() { toggle.Enable(); restart.Enable(); }
        void OnDisable() { if (toggle != null) toggle.Disable(); if (restart != null) restart.Disable(); }
        void OnDestroy() { if (toggle != null) toggle.Dispose(); if (restart != null) restart.Dispose(); }

        Vector3 OutwardWorld => world.transform.TransformDirection(new Vector3(outward.x, 0f, outward.y)).normalized;

        // 프리팹은 꺼진 채로 저장돼 있어야 한다. 켜진 프리팹은 연결하기 전에 동작을 시작하므로 쓰지 않는다.
        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, string unitName)
        {
            if (prefab == null) return null;
            if (prefab.activeSelf)
            {
                Debug.LogError("Siege unit prefabs must be saved inactive: " + prefab.name, this);
                return null;
            }
            GameObject unit = Instantiate(prefab, position, rotation);
            SceneManager.MoveGameObjectToScene(unit, gameObject.scene);
            unit.name = unitName;
            // 나는 적은 발판이 무너져도 떨어지지 않으므로 낙하 부품을 붙이지 않는다.
            var attacker = unit.GetComponent<SiegeAttacker>();
            if (attacker != null && !attacker.UsesFallController) return unit;
            var falling = unit.GetComponent<UnitFallController>();
            if (falling == null) falling = unit.AddComponent<UnitFallController>();
            falling.Configure(world, fall);
            return unit;
        }

        SquadRoster[] SpawnAllies()
        {
            var rosters = new List<SquadRoster>();
            Quaternion facing = Quaternion.LookRotation(OutwardWorld);
            Vector3 right = Vector3.Cross(Vector3.up, OutwardWorld);
            foreach (SiegeUnitGroup group in settings.AllySquads)
            {
                if (rosters.Count >= center.Config.MaxSquads) break;
                if (group.unitPrefab == null || group.count <= 0) continue;
                int squad = rosters.Count;
                Vector3 origin = AllyOrigin(squad, right);
                int count = Mathf.Min(group.count, center.Config.MaxMembersPerSquad);
                var members = new List<UnitCombatAI>();
                for (int i = 0; i < count; i++)
                {
                    int inRow = Mathf.Min(allyColumns, count - i / allyColumns * allyColumns);
                    Vector3 offset = right * ((i % allyColumns - (inRow - 1) * 0.5f) * allySpacing)
                        - OutwardWorld * (i / allyColumns * allySpacing);
                    GameObject unit = Spawn(group.unitPrefab, origin + offset, facing, "Squad" + (squad + 1) + "_" + (i + 1));
                    var ai = unit != null ? unit.GetComponent<UnitCombatAI>() : null;
                    if (ai == null) continue;
                    if (allyAI != null) ai.Configure(allyAI);
                    unit.SetActive(true);
                    members.Add(ai);
                }
                rosters.Add(new SquadRoster { members = members.ToArray() });
            }
            return rosters.ToArray();
        }

        // 정해 둔 자리보다 부대가 많으면 마지막 자리의 양옆으로, 그래도 넘치면 그 뒤 줄로 세운다.
        Vector3 AllyOrigin(int squad, Vector3 right)
        {
            if (allySpawnPoints.Length == 0) return transform.position;
            if (squad < allySpawnPoints.Length) return allySpawnPoints[squad];
            int extra = squad - allySpawnPoints.Length;
            int column = extra % allyExtraColumns;
            float side = (column / 2 + 1) * allyExtraSpacing * (column % 2 == 0 ? -1f : 1f);
            return allySpawnPoints[allySpawnPoints.Length - 1] + right * side
                - OutwardWorld * (extra / allyExtraColumns * allyExtraRowSpacing);
        }

        // 적 첫 줄에서 판 바깥 끝까지 남은 거리. 줄이 많으면 이 안에 들어오도록 줄 간격을 좁힌다.
        float EnemyDepth()
        {
            Vector3 local = world.LocalPoint(enemySpawnPoint);
            float size = world.Grid.CellSize;
            float edge = outward.x > 0 ? world.Grid.Width * size - local.x : outward.x < 0 ? local.x
                : outward.y > 0 ? world.Grid.Depth * size - local.z : local.z;
            return Mathf.Max(0f, edge - enemyEdgeMargin);
        }

        // 적은 꺼 둔 채로 만들어 두고 전투가 시작될 때 켠다.
        void SpawnEnemies()
        {
            Quaternion facing = Quaternion.LookRotation(-OutwardWorld);
            Vector3 right = Vector3.Cross(Vector3.up, OutwardWorld);
            int rows = 0;
            foreach (SiegeUnitGroup group in settings.EnemyGroups)
                if (group.unitPrefab != null && group.count > 0) rows += (group.count + enemyColumns - 1) / enemyColumns;
            float enemyRowSpacing = rows > 1 ? Mathf.Min(this.enemyRowSpacing, EnemyDepth() / (rows - 1)) : this.enemyRowSpacing;
            int row = 0;
            foreach (SiegeUnitGroup group in settings.EnemyGroups)
            {
                if (group.unitPrefab == null || group.count <= 0) continue;
                for (int i = 0; i < group.count; i++)
                {
                    int inRow = Mathf.Min(enemyColumns, group.count - i / enemyColumns * enemyColumns);
                    Vector3 position = enemySpawnPoint
                        + right * ((i % enemyColumns - (inRow - 1) * 0.5f) * enemySpacing)
                        + OutwardWorld * ((row + i / enemyColumns) * enemyRowSpacing);
                    GameObject unit = Spawn(group.unitPrefab, position, facing, group.unitPrefab.name + "_" + (enemies.Count + 1));
                    var brain = unit != null ? unit.GetComponent<SiegeAttacker>() : null;
                    if (brain == null)
                    {
                        Debug.LogError("Siege enemy prefab needs a SiegeAttacker: " + group.unitPrefab.name, this);
                        if (unit != null) Destroy(unit);
                        break;
                    }
                    brain.Bind(world, ladders, outward);
                    enemies.Add(brain);
                }
                row += (group.count + enemyColumns - 1) / enemyColumns;
            }
        }

        void Update()
        {
            if (Phase == SiegePhase.Preparation)
            {
                if (toggle.WasPressedThisFrame()) SetConstructionView(!IsConstructionView);
                return;
            }
            if (Phase == SiegePhase.Battle)
            {
                if (!RetreatOrdered && !world.IsRebuilding && !world.RebuildPending && ShouldOrderRetreat())
                {
                    RetreatOrdered = true;
                    foreach (SiegeAttacker enemy in enemies) if (IsInBattle(enemy)) enemy.Retreat();
                }
                Outcome = SiegeOutcomeRules.Evaluate(player.Health == null || player.Health.IsDead,
                    GateDestroyed, AlliesAlive, EnemiesAlive, 0);
                if (Outcome != SiegeOutcome.Ongoing) Phase = SiegePhase.Finished;
                return;
            }
            if (restart.WasPressedThisFrame()) SceneManager.LoadScene(gameObject.scene.name);
        }

        public bool GateDestroyed
        {
            get
            {
                foreach (PlacedStructure structure in world.Structures)
                    if (structure != null && structure.Placement.Definition.Kind == BuildPieceKind.Gate) return false;
                return true;
            }
        }

        /// <summary>플레이어를 뺀 부대원 중 살아 있는 수.</summary>
        public int AlliesAlive
        {
            get
            {
                int count = 0;
                for (int i = 0; i < center.Squads.Count; i++) count += center.AliveCount(i);
                return count;
            }
        }

        public int EnemiesAlive => CountEnemies(null);

        /// <summary>남은 적에게 후퇴를 지시했는지(Q-30).</summary>
        public bool RetreatOrdered { get; private set; }

        /// <summary>후퇴해 전장을 떠난 적 수.</summary>
        public int EnemiesFled
        {
            get
            {
                int count = 0;
                foreach (SiegeAttacker enemy in enemies) if (enemy != null && enemy.Fled) count++;
                return count;
            }
        }

        static bool IsInBattle(SiegeAttacker enemy) => enemy != null && !enemy.Fled
            && enemy.gameObject.activeSelf && enemy.Actor.Health != null && !enemy.Actor.Health.IsDead;

        bool ShouldOrderRetreat()
        {
            int alive = 0, breachers = 0, assaulting = 0, undecided = 0;
            foreach (SiegeAttacker enemy in enemies)
            {
                if (!IsInBattle(enemy)) continue;
                alive++;
                if (enemy.Role != SiegeAttackerRole.Archer) breachers++;
                if (enemy.IsAssaulting) assaulting++;
                if (!enemy.HasDecided) undecided++;
            }
            return SiegeOutcomeRules.ShouldRetreat(alive, breachers, assaulting, undecided);
        }

        public int CountEnemies(SiegeAttackerRole? role)
        {
            int count = 0;
            foreach (SiegeAttacker enemy in enemies)
            {
                if (enemy == null || enemy.Fled || (role.HasValue && enemy.Role != role.Value)) continue;
                // 아직 나오지 않은 적은 체력이 만들어지기 전이므로 살아 있는 것으로 센다.
                var actor = enemy.GetComponent<CharacterActor>();
                if (actor.Health == null || !actor.Health.IsDead) count++;
            }
            return count;
        }

        /// <summary>전투를 시작한다. 건설과 수리를 막고 적을 내보낸다.</summary>
        public bool StartBattle()
        {
            if (Phase != SiegePhase.Preparation) return false;
            prep.Lock();
            Phase = SiegePhase.Battle;
            ShowView(false);
            foreach (SiegeAttacker enemy in enemies) if (enemy != null) enemy.gameObject.SetActive(true);
            return true;
        }

        /// <summary>준비 단계에서만 건설 시점으로 돌아갈 수 있다.</summary>
        public void SetConstructionView(bool build)
        {
            if (build && Phase != SiegePhase.Preparation) return;
            ShowView(build);
        }

        void ShowView(bool build)
        {
            placement.enabled = false; prep.enabled = false; playerInput.enabled = false; commandInput.enabled = false;
            constructionCamera.gameObject.SetActive(build);
            commandCamera.gameObject.SetActive(!build);
            constructionHud.SetActive(build); commandHud.SetActive(!build);
            IsConstructionView = build;
            placement.enabled = build;
            prep.enabled = build;
            playerInput.enabled = !build;
            commandInput.enabled = !build;
            Cursor.lockState = build ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = build;
        }
    }
}
