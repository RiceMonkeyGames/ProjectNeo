using System.Collections;
using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Core;
using ProjectNeo.Morale;
using ProjectNeo.Run;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using ProjectNeo.Stages;
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
        // 웨이브(B-7.1). 적은 준비 단계에 모두 꺼진 채로 만들어 두고, 시간표가 내보낼 때 웨이브 단위로 켠다.
        readonly List<List<SiegeAttacker>> waveUnits = new List<List<SiegeAttacker>>();
        readonly HashSet<SiegeAttacker> unreleased = new HashSet<SiegeAttacker>();
        readonly List<int> dueWaves = new List<int>();
        // 적 정보(C-5). 무리 번호는 편성의 웨이브 순서, 그 안의 무리 순서다.
        readonly List<List<SiegeAttacker>> groupUnits = new List<List<SiegeAttacker>>();
        readonly List<int> waveFirstGroup = new List<int>();
        EnemyIntel intel = new EnemyIntel(null);
        EncounterPlan plan;
        WaveSchedule schedule;
        BattleEnemySpawner spawner;
        GameFlow flow;
        GameRunLink runLink;
        Transform enemyRoot;
        bool battleRunning;
        /// <summary>이번 전투의 적 편성. 스테이지 에셋 또는 단일 적 목록에서 만든다.</summary>
        public EncounterPlan Plan => plan;
        /// <summary>
        /// 이번 전투의 적 목록(C-5): 종류별 남은 수, 숨긴 무리는 "? × 수". 준비 단계에는 전체 수, 전투 중에는 매 프레임
        /// 남은 수(나오지 않았거나 전장에 살아 있는 적. 죽거나 떠난 적은 빠진다)로 바뀌고, 웨이브가 나오면 그 무리가 공개된다.
        /// </summary>
        public EnemyIntel Intel => intel;
        public int WaveCount => schedule != null ? schedule.WaveCount : 0;
        public int WavesReleased => schedule != null ? schedule.ReleasedWaves : 0;
        /// <summary>다음 웨이브까지 남은 초. 남은 웨이브가 없으면 -1.</summary>
        public float SecondsUntilNextWave => schedule != null ? schedule.SecondsUntilNext : -1f;
        /// <summary>아직 나오지 않은 적 수. 승리 판정에서 나올 예정인 적으로 센다.</summary>
        public int EnemiesPending
        {
            get
            {
                int count = 0;
                foreach (SiegeAttacker enemy in unreleased)
                    if (enemy != null && (enemy.Actor == null || enemy.Actor.Health == null || !enemy.Actor.Health.IsDead)) count++;
                return count;
            }
        }
        /// <summary>웨이브가 전장에 나올 때마다 그 번호(0부터)로 알린다.</summary>
        public event System.Action<int> WaveReleased;
        public GameBattleSettings Settings => settings;
        public IReadOnlyList<SiegeAttacker> Enemies => enemies;
        public IReadOnlyList<SiegeLadder> Ladders => ladders;
        /// <summary>런 데이터 연결(C-1~2). 씬 유닛의 런 번호와 이번 전투 입력을 읽을 때 쓴다.</summary>
        public GameRunLink RunLink => runLink ??= CreateRunLink();

        // 런 저장·이어 하기(FLOW-02)에 필요한 것을 붙여 만든다. ID 목록이 없으면 저장도 이어 하기도 하지 않는다.
        GameRunLink CreateRunLink()
        {
            var link = new GameRunLink(flow, commander, player, world);
            RunContentCatalog content = settings != null ? settings.RunContent : null;
            if (content == null) return link;
            link.Content = content;
            var facilities = GetComponent<GameFacilities>();
            link.Restorer = new GameRunRestorer(commander, player, world, GetComponent<GameUnitSpawner>(), content,
                facilities != null ? facilities.StartConfig : null);
            return link;
        }

        /// <summary>
        /// 정해 둔 저장 계기면 런을 지금 슬롯에 저장한다(FLOW-02). 저장 시점은 <see cref="GameSession.RunSavePolicy"/>에서 바꾼다.
        /// 슬롯 없이 Game 씬을 바로 실행했거나 런이 연결되지 않았으면 아무것도 하지 않는다.
        /// </summary>
        public bool SaveRun(RunSaveTrigger trigger)
        {
            if (GameSession.ActiveSlot < 0 || !GameSession.RunSavePolicy.Allows(trigger)) return false;
            RunSnapshot snapshot = RunLink.CaptureSnapshot(trigger == RunSaveTrigger.RewardGranted ? 1 : 0);
            return snapshot != null && GameSession.SaveRun(snapshot);
        }

        void SaveRunOnQuit()
        {
            if (flow != null && flow.Phase == GamePhase.Preparation) SaveRun(RunSaveTrigger.QuitDuringPreparation);
        }
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
        /// <summary>남은 적 전체: 전장에 있는 적과 아직 나오지 않은 적.</summary>
        public int EnemiesAlive => CountEnemies(true);
        /// <summary>이미 나와서 전장에 살아 있는 적.</summary>
        public int EnemiesOnField => CountEnemies(false);
        int CountEnemies(bool includePending)
        {
            int count = 0;
            foreach (SiegeAttacker enemy in enemies)
                if (enemy != null && !retreated.Contains(enemy) && (includePending || !unreleased.Contains(enemy)))
                {
                    var actor = enemy.GetComponent<CharacterActor>();
                    if (actor.Health == null || !actor.Health.IsDead) count++;
                }
            return count;
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
            // 퍽 창(META-09). Game.unity를 고치지 않고 실행 중에 붙인다. 퍽 트리는 새 런 설정(RunStart)에 있다.
            var startFacilities = GetComponent<GameFacilities>();
            ProjectNeo.Meta.PerkTreeCatalog perkTrees = startFacilities != null && startFacilities.StartConfig != null ? startFacilities.StartConfig.PerkTrees : null;
            if (perkTrees != null)
            {
                PerkWindow = gameObject.AddComponent<GamePerkWindow>();
                PerkWindow.Configure(FindAnyObjectByType<GamePhaseView>(), perkTrees);
            }
        }
        /// <summary>준비 단계 성문 열기(PREP-05).</summary>
        public GamePrepGate PrepGate { get; private set; }
        /// <summary>전투 중 소모품 사용(PREP-04).</summary>
        public GameConsumables Consumables { get; private set; }
        /// <summary>퍽 창(META-09). 퍽 트리가 없으면 null.</summary>
        public GamePerkWindow PerkWindow { get; private set; }
        void OnEnable() { flow.PhaseChanged += OnPhaseChanged; GameSession.Quitting += SaveRunOnQuit; }
        void OnDisable() { flow.PhaseChanged -= OnPhaseChanged; GameSession.Quitting -= SaveRunOnQuit; }
        void Start() => OnPhaseChanged(flow.Phase);

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Preparation)
            {
                battleRunning = false;
                ClearEnemies();
                RunLink.OnPreparation();
                // 시작 재화는 첫 저장 전에 정해져야 한다. 되살린 런에는 다시 주지 않는다(FLOW-02).
                var build = GetComponent<GameBuildEconomy>();
                if (build != null && RunLink.State != null) build.OnRunReady(RunLink.WasRestored);
                // 열린 해금 트랙의 시작 보너스도 첫 저장 전에 준다. 되살린 런에는 다시 주지 않는다(META-07).
                var facilities = GetComponent<GameFacilities>();
                if (facilities != null && RunLink.State != null) facilities.OnRunReady(RunLink.WasRestored);
                // 퍽 특수 효과(전투 시작·부대원 사망)는 이 런의 퍽을 쓴다(META-02).
                var squadMorale = GetComponent<ProjectNeo.Morale.GameSquadMorale>();
                if (squadMorale != null && RunLink.State != null) squadMorale.UsePerks(RunLink.State.Perks);
                // 준비 시계는 저장 전에 이번 준비 예산으로 채운다. 되살린 런은 저장된 시계를 그대로 쓴다(PREP-06).
                if (build != null && build.StartConfig != null && RunLink.State != null)
                {
                    RunLink.State.SetupPrepTime(build.StartConfig.PrepTime);
                    RunLink.State.BeginPreparationClockIfNeeded();
                }
                // 되살린 런의 작업 주문을 다시 넣고 미완성 무기 표시를 맞춘다(SIEGE-08). 되살린 주문은 한 번만 쓴다.
                if (build != null && build.Work != null)
                {
                    build.Work.OnPreparation(RunLink.Restorer != null ? RunLink.Restorer.RestoredWork : null);
                    RunLink.Restorer?.RestoredWork.Clear();
                }
                foreach (SiegeLadder ladder in ladders) { ladder.ResetBattle(); ladder.enabled = false; }
                SetCannonsArmed(false);
                StartCoroutine(RestoreWhenReady());
                SpawnEnemies();
                SaveRun(RunSaveTrigger.PreparationStart);
            }
            else if (phase == GamePhase.Battle)
            {
                battleRunning = true;
                BattleNumber++;
                // 전투 입력을 만들기 전에 저장한다. 전투 중에 끄면 이 준비 상태로 돌아온다.
                SaveRun(RunSaveTrigger.BattleStart);
                // 남은 작업 주문을 정리한다: 0%는 없애고 환불, 나머지는 진행률만큼 체력(SIEGE-08). 성 기록 전에 한다.
                GetComponent<GameBuildEconomy>()?.Work?.FinishForBattle();
                RunLink.OnBattleStart();
                foreach (SiegeLadder ladder in ladders) ladder.enabled = true;
                schedule?.Reset();
                ReleaseDueWaves(0f);
                SetCannonsArmed(true);
            }
            else if (phase == GamePhase.Reward || phase == GamePhase.RunResult)
            {
                battleRunning = false;
                FreezeBattle();
                // 패배로 런이 끝나면 이어 할 런이 없다.
                if (phase == GamePhase.RunResult && GameSession.RunSavePolicy.deleteOnDefeat) GameSession.DeleteRunSave();
            }
        }

        public bool TryStartBattle() => CanStart && flow.StartBattle();

        void LateUpdate()
        {
            if (!battleRunning || flow.Phase != GamePhase.Battle) return;
            // 전투가 실제로 진행된 시간만큼만 시간표를 넘긴다(멈춘 동안은 0).
            ReleaseDueWaves(Time.deltaTime);
            RefreshIntel();
            // 이 프레임의 공격·투사체 처리가 끝난 뒤 동시 성립은 패배를 우선한다.
            SiegeOutcome outcome = Evaluate();
            if (outcome == SiegeOutcome.Ongoing)
            {
                if (!IsRetreating && ShouldRetreat()) BeginRetreat();
                if (retreatTargets.Count > 0) UpdateRetreat();
                outcome = Evaluate();
            }
            Resolve(outcome);
        }
        // 아직 나오지 않은 적이 있으면 전장이 비어도 승리가 아니다(Q-51).
        SiegeOutcome Evaluate() => SiegeOutcomeRules.Evaluate(player.Health == null || player.Health.IsDead,
            GateDestroyed, AlliesAlive, EnemiesOnField, EnemiesPending);

        /// <summary>시간표를 넘기고, 나올 차례가 된 웨이브의 적을 켠다. 각 웨이브는 전투마다 한 번만 나온다.</summary>
        void ReleaseDueWaves(float deltaSeconds)
        {
            if (schedule == null || schedule.AllReleased) return;
            dueWaves.Clear();
            if (schedule.Advance(deltaSeconds, EnemiesOnField, dueWaves) == 0) return;
            foreach (int index in dueWaves)
            {
                foreach (SiegeAttacker enemy in waveUnits[index])
                {
                    if (enemy == null) continue;
                    unreleased.Remove(enemy);
                    enemy.gameObject.SetActive(true);
                }
                // 전장에 나온 무리는 숨김이 풀린다(Q-53).
                int last = index + 1 < waveFirstGroup.Count ? waveFirstGroup[index + 1] : groupUnits.Count;
                for (int group = waveFirstGroup[index]; group < last; group++) intel.Reveal(group);
                WaveReleased?.Invoke(index);
            }
            // 새 적이 합류했으므로 후퇴 여부를 다시 판단한다. 이미 떠나던 적은 그대로 떠난다.
            IsRetreating = false;
        }

        // 후퇴 판단 대상: 전장에 나와 살아 있고, 떠났거나 떠나는 중이 아닌 적.
        bool CanJudgeRetreat(SiegeAttacker enemy) => enemy != null && !unreleased.Contains(enemy)
            && !retreated.Contains(enemy) && !retreatTargets.ContainsKey(enemy)
            && enemy.Actor != null && !enemy.Actor.Health.IsDead;

        bool ShouldRetreat()
        {
            // 나올 웨이브가 남아 있으면 설정으로 허용한 경우에만 후퇴를 판단한다(Q-52).
            if (schedule != null && !WaveSchedule.RetreatAllowed(schedule.PendingWaves, settings.RetreatWhileWavesPending))
                return false;
            int alive = 0, siegeUnits = 0;
            foreach (SiegeAttacker enemy in enemies)
            {
                if (!CanJudgeRetreat(enemy)) continue;
                alive++;
                if (enemy.Role != SiegeAttackerRole.Archer) siegeUnits++;
            }
            if (!SiegeOutcomeRules.ShouldRetreat(alive, siegeUnits, false,
                !world.IsRebuilding && !world.RebuildPending)) return false;
            // 오르기·떨어지기 중이거나 자기 방식(걷기·날기)으로 아군에 닿는 적이 있으면 아직 후퇴하지 않는다.
            foreach (SiegeAttacker enemy in enemies)
                if (CanJudgeRetreat(enemy) && (enemy.IsMidAction || enemy.CanReachAnyEnemy())) return false;
            return true;
        }

        void BeginRetreat()
        {
            IsRetreating = true;
            foreach (SiegeAttacker enemy in enemies)
            {
                if (!CanJudgeRetreat(enemy)) continue;
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
            plan = BuildPlan();
            schedule = new WaveSchedule(plan.Timings());
            spawner ??= new BattleEnemySpawner(world, settings, fallSettings, ladders, enemyRoot, this);
            spawner.ResetNumbering();
            intel = EnemyIntel.FromPlan(plan);
            foreach (EncounterWave wave in plan.Waves)
            {
                var units = new List<SiegeAttacker>();
                int first = groupUnits.Count;
                waveFirstGroup.Add(first);
                for (int i = 0; i < wave.Groups.Count; i++) groupUnits.Add(new List<SiegeAttacker>());
                foreach (BattleEnemySpawner.Spawned spawned in spawner.SpawnWave(wave))
                {
                    groupUnits[first + spawned.GroupIndex].Add(spawned.Attacker);
                    units.Add(spawned.Attacker);
                    enemies.Add(spawned.Attacker);
                    unreleased.Add(spawned.Attacker);
                    enemyIds[spawned.Attacker] = spawned.EnemyId;
                }
                waveUnits.Add(units);
            }
            RefreshIntel();
        }

        // 무리마다 남은 적(죽지 않았고 떠나지 않은 적)을 센다. 만들지 못한 적은 처음부터 남은 수에 들지 않는다.
        void RefreshIntel()
        {
            for (int group = 0; group < groupUnits.Count && group < intel.GroupCount; group++)
            {
                int alive = 0;
                foreach (SiegeAttacker enemy in groupUnits[group])
                    if (enemy != null && !retreated.Contains(enemy)
                        && (enemy.Actor == null || enemy.Actor.Health == null || !enemy.Actor.Health.IsDead)) alive++;
                intel.SetRemaining(group, alive);
            }
        }

        /// <summary>
        /// 이번 스테이지의 편성을 고른다. 스테이지 목록이 있으면 스테이지 번호에 맞는 웨이브를,
        /// 없으면 전투 설정의 단일 적 목록을 전투 시작과 함께 나오는 웨이브 하나로 쓴다.
        /// </summary>
        EncounterPlan BuildPlan()
        {
            int stageNumber = flow != null && flow.Run != null ? flow.Run.StageNumber : 1;
            EncounterPlan custom = EncounterSourceOverride?.Invoke(settings, stageNumber);
            return custom ?? DefaultEncounter(settings, stageNumber);
        }

        /// <summary>
        /// 편성을 고르는 방법을 통째로 바꾼다(디버그 편성·테스트용). 전투 설정과 스테이지 번호를 받아 편성을 돌려주며,
        /// null을 돌려주거나 비워 두면 기본 방법(DefaultEncounter)을 쓴다. 다음 준비 단계부터 적용되고,
        /// 지금 준비 단계에 바로 적용하려면 RebuildEncounter를 부른다.
        /// </summary>
        public static System.Func<GameBattleSettings, int, EncounterPlan> EncounterSourceOverride { get; set; }

        /// <summary>기본 편성: 스테이지 목록이 있으면 그 스테이지의 웨이브, 없으면 단일 적 목록.</summary>
        public static EncounterPlan DefaultEncounter(GameBattleSettings config, int stageNumber)
        {
            StageDefinition stage = config.Stages != null ? config.Stages.StageFor(stageNumber) : null;
            return stage != null ? EncounterPlan.FromStage(stage, config.SpawnCell) : SingleWaveEncounter(config);
        }

        /// <summary>전투 설정의 단일 적 목록을 전투 시작과 함께 나오는 웨이브 하나로 만든 편성.</summary>
        public static EncounterPlan SingleWaveEncounter(GameBattleSettings config, int stageNumber = 1) =>
            EncounterPlan.FromGroups(config.Enemies, config.SpawnCell);

        /// <summary>준비 단계에서 적을 지우고 지금 편성으로 다시 만든다. 준비 단계가 아니면 아무것도 하지 않는다.</summary>
        public bool RebuildEncounter()
        {
            if (flow == null || flow.Phase != GamePhase.Preparation) return false;
            ClearEnemies();
            SpawnEnemies();
            return true;
        }

        void ClearEnemies()
        {
            foreach (SiegeAttacker enemy in enemies)
                if (enemy != null) { enemy.gameObject.SetActive(false); Destroy(enemy.gameObject); }
            enemies.Clear();
            enemyIds.Clear();
            waveUnits.Clear(); unreleased.Clear();
            groupUnits.Clear(); waveFirstGroup.Clear();
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
                        // 다 짓지 못한 무기는 쏘지 않는다(SIEGE-08).
                        cannon.SetArmed(armed && !weapon.IsUnfinished);
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
            foreach (SiegeAttacker enemy in retreated)
            {
                if (enemy == null || !enemyIds.TryGetValue(enemy, out string id)) continue;
                retreats.TryGetValue(id, out int count);
                retreats[id] = count + 1;
            }
            return retreats;
        }
    }
}
