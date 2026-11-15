using System.Collections;
using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Core;
using ProjectNeo.Morale;
using ProjectNeo.Run;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Flow
{
    /// <summary>GameFlow의 전투 단계에 적 생성·공성전·결과를 연결한다. 아군·성·씬의 수명은 유지한다.</summary>
    [DefaultExecutionOrder(-40)]
    public sealed class GameBattleController : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField] GameBattleSettings settings;
        [SerializeField] SquadCommandCenter commander;
        [SerializeField] CharacterActor player;
        [SerializeField] FallSettings fallSettings;
        [SerializeField] SiegeLadder[] ladders = new SiegeLadder[0];
        readonly List<SiegeAttacker> enemies = new List<SiegeAttacker>();
        readonly Dictionary<Behaviour, bool> frozen = new Dictionary<Behaviour, bool>();
        readonly List<Behaviour> freezeOrder = new List<Behaviour>();
        readonly HashSet<SiegeAttacker> retreated = new HashSet<SiegeAttacker>();
        readonly Dictionary<SiegeAttacker, Vector3> retreatTargets = new Dictionary<SiegeAttacker, Vector3>();
        // 처치 수를 적 종류별로 세기 위한 적 ID. 지금은 적 프리팹 이름을 쓴다.
        readonly Dictionary<SiegeAttacker, string> enemyIds = new Dictionary<SiegeAttacker, string>();
        GameFlow flow;
        GameRunLink runLink;
        Transform enemyRoot;
        bool battleRunning;
        public GameBattleSettings Settings => settings;
        public IReadOnlyList<SiegeAttacker> Enemies => enemies;
        public IReadOnlyList<SiegeLadder> Ladders => ladders;
        /// <summary>런 데이터 연결(C-1~2). 씬 유닛의 런 번호와 이번 전투 입력을 읽을 때 쓴다.</summary>
        public GameRunLink RunLink => runLink ??= new GameRunLink(flow, commander, player, world);
        /// <summary>아군 부대 사기 연결(B-8.2). 사기 설정이 없으면 null.</summary>
        public GameSquadMorale Morale => morale;
        GameSquadMorale morale;
        public int BattleNumber { get; private set; }
        public bool IsRunning => battleRunning;
        public bool IsRetreating { get; private set; }
        public int RetreatedCount => retreated.Count;
        public bool CanStart => flow != null && flow.Phase == GamePhase.Preparation
            && !world.IsRebuilding && !world.RebuildPending && frozen.Count == 0
            && player.Health != null && !player.Health.IsDead;
        public bool GateDestroyed
        {
            get
            {
                foreach (PlacedStructure piece in world.Structures)
                    if (piece != null && piece.Placement.Definition.Kind == BuildPieceKind.Gate
                        && piece.Health != null && !piece.Health.IsDead) return false;
                return true;
            }
        }
        public int AlliesAlive
        {
            get { int count = 0; for (int i = 0; i < commander.Squads.Count; i++) count += commander.AliveCount(i); return count; }
        }
        public int EnemiesAlive
        {
            get
            {
                int count = 0;
                foreach (SiegeAttacker enemy in enemies)
                    if (enemy != null && !retreated.Contains(enemy))
                    {
                        var actor = enemy.GetComponent<CharacterActor>();
                        if (actor.Health == null || !actor.Health.IsDead) count++;
                    }
                return count;
            }
        }

        public void Configure(ConstructionWorld castle, GameBattleSettings config, SquadCommandCenter center,
            CharacterActor playerActor, FallSettings falling, SiegeLadder[] ladderSpots)
        { world = castle; settings = config; commander = center; player = playerActor; fallSettings = falling; ladders = ladderSpots; }

        void Awake()
        {
            flow = GameSession.Flow;
            enemyRoot = new GameObject("BattleEnemies").transform;
            enemyRoot.SetParent(transform, false);
            // 아군 부대 사기(B-8.2). 사기 설정이 있으면 씬 파일을 바꾸지 않고 실행 중에 붙인다. 단계 변화는 스스로 받는다.
            if (settings != null && settings.Morale != null && commander != null)
            {
                morale = gameObject.AddComponent<GameSquadMorale>();
                morale.Configure(flow, commander, world, settings.Morale, settings.Outward, () => RunLink.State,
                    new SeededRandom(System.Environment.TickCount));
            }
            // 준비 단계 성문 열기(PREP-05). 씬 파일을 바꾸지 않고 실행 중에 붙인다.
            if (world != null && settings != null)
            {
                PrepGate = gameObject.AddComponent<GamePrepGate>();
                PrepGate.Configure(world, FindAnyObjectByType<GamePhaseView>(), settings.Outward);
            }
            // 전투 중 소모품 사용(PREP-04). 씬 파일을 바꾸지 않고 실행 중에 붙인다.
            if (settings != null && player != null)
            {
                Consumables = gameObject.AddComponent<GameConsumables>();
                Consumables.Configure(flow, settings, player, () => RunLink.State);
            }
        }
        /// <summary>준비 단계 성문 열기(PREP-05).</summary>
        public GamePrepGate PrepGate { get; private set; }
        /// <summary>전투 중 소모품 사용(PREP-04).</summary>
        public GameConsumables Consumables { get; private set; }
        void OnEnable() => flow.PhaseChanged += OnPhaseChanged;
        void OnDisable() => flow.PhaseChanged -= OnPhaseChanged;
        void Start() => OnPhaseChanged(flow.Phase);

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Preparation)
            {
                battleRunning = false;
                ClearEnemies();
                RunLink.OnPreparation();
                foreach (SiegeLadder ladder in ladders) { ladder.ResetBattle(); ladder.enabled = false; }
                SetCannonsArmed(false);
                StartCoroutine(RestoreWhenReady());
                SpawnEnemies();
            }
            else if (phase == GamePhase.Battle)
            {
                battleRunning = true;
                BattleNumber++;
                RunLink.OnBattleStart();
                foreach (SiegeLadder ladder in ladders) ladder.enabled = true;
                foreach (SiegeAttacker enemy in enemies) enemy.gameObject.SetActive(true);
                SetCannonsArmed(true);
            }
            else if (phase == GamePhase.Reward || phase == GamePhase.RunResult)
            {
                battleRunning = false;
                FreezeBattle();
            }
        }

        public bool TryStartBattle() => CanStart && flow.StartBattle();

        void LateUpdate()
        {
            if (!battleRunning || flow.Phase != GamePhase.Battle) return;
            // 이 프레임의 공격·투사체 처리가 끝난 뒤 동시 성립은 패배를 우선한다.
            SiegeOutcome outcome = Evaluate();
            if (outcome == SiegeOutcome.Ongoing)
            {
                if (!IsRetreating && ShouldRetreat()) BeginRetreat();
                if (IsRetreating) UpdateRetreat();
                outcome = Evaluate();
            }
            Resolve(outcome);
        }
        SiegeOutcome Evaluate() => SiegeOutcomeRules.Evaluate(player.Health == null || player.Health.IsDead,
            GateDestroyed, AlliesAlive, EnemiesAlive, 0);

        bool ShouldRetreat()
        {
            int siegeUnits = 0;
            foreach (SiegeAttacker enemy in enemies)
                if (enemy != null && !retreated.Contains(enemy) && enemy.Actor != null && !enemy.Actor.Health.IsDead
                    && enemy.Role != SiegeAttackerRole.Archer) siegeUnits++;
            if (!SiegeOutcomeRules.ShouldRetreat(EnemiesAlive, siegeUnits, false,
                !world.IsRebuilding && !world.RebuildPending)) return false;
            // 오르기·떨어지기 중이거나 자기 방식(걷기·날기)으로 아군에 닿는 적이 있으면 아직 후퇴하지 않는다.
            foreach (SiegeAttacker enemy in enemies)
            {
                if (enemy == null || retreated.Contains(enemy) || enemy.Actor == null || enemy.Actor.Health.IsDead) continue;
                if (enemy.IsMidAction || enemy.CanReachAnyEnemy()) return false;
            }
            return true;
        }

        void BeginRetreat()
        {
            IsRetreating = true;
            foreach (SiegeAttacker enemy in enemies)
            {
                if (enemy == null || enemy.Actor.Health.IsDead) continue;
                Vector3 local = world.LocalPoint(enemy.transform.position);
                if (settings.Outward.x != 0) local.x = settings.Outward.x > 0
                    ? world.Grid.Width * world.Grid.CellSize - settings.RetreatExitMargin : settings.RetreatExitMargin;
                if (settings.Outward.y != 0) local.z = settings.Outward.y > 0
                    ? world.Grid.Depth * world.Grid.CellSize - settings.RetreatExitMargin : settings.RetreatExitMargin;
                local.y = 0f;
                Vector3 target = world.transform.TransformPoint(local);
                // 걷는 적은 NavMesh 출구로 걸어가고, 나는 적은 그 위로 날아간다.
                retreatTargets[enemy] = enemy.BeginExit(target, settings.RetreatExitMargin);
            }
        }
        void UpdateRetreat()
        {
            float distance = settings.RetreatArrivalDistance;
            foreach (var pair in retreatTargets)
            {
                SiegeAttacker enemy = pair.Key;
                if (enemy == null || enemy.Actor.Health.IsDead || retreated.Contains(enemy)) continue;
                if ((enemy.transform.position - pair.Value).sqrMagnitude > distance * distance) continue;
                retreated.Add(enemy);
                enemy.gameObject.SetActive(false);
            }
        }

        void SpawnEnemies()
        {
            Vector3 outward = world.transform.TransformDirection(new Vector3(settings.Outward.x, 0f, settings.Outward.y)).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, outward);
            Vector3 origin = world.transform.TransformPoint(new Vector3(settings.SpawnCell.x * world.Grid.CellSize,
                0f, settings.SpawnCell.y * world.Grid.CellSize));
            int row = 0;
            foreach (SiegeUnitGroup group in settings.Enemies)
            {
                if (group.unitPrefab == null || group.count <= 0) continue;
                if (group.unitPrefab.activeSelf)
                { Debug.LogError("Battle enemy prefab must be inactive: " + group.unitPrefab.name, this); continue; }
                for (int i = 0; i < group.count; i++)
                {
                    int inRow = Mathf.Min(settings.Columns, group.count - i / settings.Columns * settings.Columns);
                    Vector3 position = origin + right * ((i % settings.Columns - (inRow - 1) * 0.5f) * settings.Spacing)
                        + outward * ((row + i / settings.Columns) * settings.RowSpacing);
                    GameObject unit = Instantiate(group.unitPrefab, enemyRoot);
                    unit.name = group.unitPrefab.name + "_" + enemies.Count;
                    unit.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-outward));
                    var brain = unit.GetComponent<SiegeAttacker>();
                    if (brain == null) { Debug.LogError("Battle enemy needs a SiegeAttacker", unit); Destroy(unit); continue; }
                    // 나는 적은 발판이 무너져도 떨어지지 않으므로 낙하 부품을 붙이지 않는다.
                    if (brain.UsesFallController)
                    {
                        var fall = unit.GetComponent<UnitFallController>();
                        if (fall == null) fall = unit.AddComponent<UnitFallController>();
                        fall.Configure(world, fallSettings);
                    }
                    brain.Bind(world, ladders, settings.Outward, settings.Siege);
                    enemies.Add(brain);
                    enemyIds[brain] = group.unitPrefab.name;
                }
                row += (group.count + settings.Columns - 1) / settings.Columns;
            }
        }

        void ClearEnemies()
        {
            foreach (SiegeAttacker enemy in enemies)
                if (enemy != null) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
            enemies.Clear();
            enemyIds.Clear();
            retreated.Clear(); retreatTargets.Clear(); IsRetreating = false;
        }

        void Freeze(Behaviour component)
        {
            if (component == null || frozen.ContainsKey(component)) return;
            frozen.Add(component, component.enabled); freezeOrder.Add(component); component.enabled = false;
        }
        void FreezeActor(CharacterActor actor, bool restore)
        {
            if (actor == null) return;
            var agent = actor.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
            if (restore)
            {
                Freeze(actor); Freeze(actor.GetComponent<UnitCombatAI>()); Freeze(agent); Freeze(actor.GetComponent<UnitFallController>());
            }
            else
            {
                actor.enabled = false;
                var ai = actor.GetComponent<UnitCombatAI>(); if (ai != null) ai.enabled = false;
                if (agent != null) agent.enabled = false;
                var falling = actor.GetComponent<UnitFallController>(); if (falling != null) falling.enabled = false;
            }
        }
        void FreezeBattle()
        {
            foreach (SiegeAttacker enemy in enemies)
                if (enemy != null) { enemy.enabled = false; FreezeActor(enemy.GetComponent<CharacterActor>(), false); }
            FreezeActor(player, true);
            foreach (var squad in commander.Squads)
                foreach (UnitCombatAI member in squad.Members) if (member != null) FreezeActor(member.Actor, true);
            foreach (SiegeLadder ladder in ladders) ladder.enabled = false;
            foreach (CombatProjectile shot in Object.FindObjectsByType<CombatProjectile>())
                if (shot.gameObject.scene == gameObject.scene) { shot.enabled = false; Destroy(shot.gameObject); }
            SetCannonsArmed(false);
            // 결과가 난 뒤 날아가던 포탄이 피해를 주지 않게 터뜨리지 않고 없앤다.
            for (int i = CannonShell.Active.Count - 1; i >= 0; i--)
            {
                CannonShell shell = CannonShell.Active[i];
                if (shell != null && shell.gameObject.scene == gameObject.scene) { shell.enabled = false; Destroy(shell.gameObject); }
            }
        }

        /// <summary>성 위 대포를 전투 중에만 자동 사격하게 한다(SIEGE-02).</summary>
        void SetCannonsArmed(bool armed)
        {
            if (world == null) return;
            foreach (PlacedSiegeWeapon weapon in world.Weapons)
                if (weapon != null)
                    foreach (CannonModule cannon in weapon.GetComponentsInChildren<CannonModule>())
                        cannon.SetArmed(armed);
        }
        IEnumerator RestoreWhenReady()
        {
            while (world.IsRebuilding || world.RebuildPending) yield return null;
            if (flow.Phase != GamePhase.Preparation) yield break;
            foreach (Behaviour component in freezeOrder)
            {
                if (component == null) continue;
                var actor = component.GetComponent<CharacterActor>();
                if (actor != null && actor.Health != null && !actor.Health.IsDead) component.enabled = frozen[component];
            }
            frozen.Clear(); freezeOrder.Clear();
        }

        void Resolve(SiegeOutcome outcome)
        {
            if (!battleRunning || outcome == SiegeOutcome.Ongoing) return;
            battleRunning = false;
            BattleOutcome result = outcome == SiegeOutcome.Victory ? BattleOutcome.Victory : BattleOutcome.Defeat;
            // 런 데이터에 결과를 반영하고 단계를 넘긴다. 연결이 없으면 기존처럼 승패만 넘긴다.
            // 사기로 도망친 아군은 Fled로 적는다(B-8.2).
            if (!RunLink.TryFinish(result, EndReason(result), CountKills(),
                morale != null ? morale.HasFled : (System.Func<CharacterActor, bool>)null, CountRetreats()))
                flow.FinishBattle(result);
        }

        BattleEndReason EndReason(BattleOutcome outcome)
        {
            if (outcome == BattleOutcome.Victory)
                return retreated.Count > 0 ? BattleEndReason.EnemiesRetreated : BattleEndReason.EnemiesDefeated;
            return player.Health == null || player.Health.IsDead ? BattleEndReason.PlayerDied : BattleEndReason.GateAndAlliesLost;
        }

        Dictionary<string, int> CountKills()
        {
            var kills = new Dictionary<string, int>();
            foreach (SiegeAttacker enemy in enemies)
            {
                if (enemy == null || enemy.Actor == null || enemy.Actor.Health == null || !enemy.Actor.Health.IsDead) continue;
                if (!enemyIds.TryGetValue(enemy, out string id)) continue;
                kills.TryGetValue(id, out int count);
                kills[id] = count + 1;
            }
            return kills;
        }

        // 퇴각(Q-30)으로 빠진 적 종류별 수. 처치 수와 따로 결과에 적는다(Q-44).
        Dictionary<string, int> CountRetreats()
        {
            var retreats = new Dictionary<string, int>();
            foreach (SiegeAttackerBrain enemy in retreated)
            {
                if (enemy == null || !enemyIds.TryGetValue(enemy, out string id)) continue;
                retreats.TryGetValue(id, out int count);
                retreats[id] = count + 1;
            }
            return retreats;
        }
    }
}
