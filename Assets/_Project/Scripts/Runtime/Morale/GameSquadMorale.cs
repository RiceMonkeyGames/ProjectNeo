using System;
using System.Collections;
using System.Collections.Generic;
using ProjectNeo.Combat;
using ProjectNeo.Core;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Morale
{
    /// <summary>
    /// Game 씬의 아군 부대 사기를 실제 전투에 잇는다(B-8.2, Q-44). 적은 사기가 없다.
    /// 전투 시작 때 살아 있는 부대마다 <see cref="BattleSquadMorale"/>을 만들고, 부대원·부대장 사망, 메인 성문 파괴,
    /// 근처 성벽 붕괴, 근처 부대 도망을 알린다. 사기 0이 된 부대는 지시에서 떼어 퇴각 지점으로 달리게 하고,
    /// 도착하거나 제한 시간이 지나면 숨겨 전투에서 뺀다. 전투가 끝나면 전투 후 회복을 적용하고,
    /// 다음 준비 단계에 도망친 유닛을 퇴각 지점에서 다시 나타나게 해 부대로 돌려보낸다.
    /// 사기 값은 런 부대(<see cref="Squad{TUnit}.Morale"/>)에 둔다. 런이 없으면 씬 부대에 둔다.
    /// COMBAT-03: 공격력 보정, 공포 지시 거부, 패닉 대기, 전염 및 HUD 상태를 연결한다.
    /// COMBAT-07: 퍽·스킬·부대장이 사기에 닿는 창구. 전투 중에만 <see cref="IPerkEffectContext"/>로 동작하고,
    /// 전투 시작 때 <see cref="PerkTrigger.BattleStart"/> 퍽을 한 번 발동하며, 부대장 각성 확률 보너스를 넣는다.
    /// </summary>
    public sealed class GameSquadMorale : MonoBehaviour, IPerkEffectContext
    {
        // 다시 나타난 유닛이 길찾기에 올라설 때까지 기다리는 최대 프레임 수.
        const int ReturnWaitFrames = 300;

        GameFlow flow;
        SquadCommandCenter center;
        ConstructionWorld world;
        MoraleSettings settings;
        Vector2Int outward;
        Func<RunState> runState;
        IRandomSource random;
        PerkLoadout perks;
        Func<UnitCombatAI, float> captainAwakenBonus;

        readonly List<BattleSquadMorale> battles = new List<BattleSquadMorale>();
        readonly List<KeyValuePair<CharacterActor, Action>> deathHandlers = new List<KeyValuePair<CharacterActor, Action>>();
        readonly Dictionary<UnitCombatAI, float> routing = new Dictionary<UnitCombatAI, float>();
        readonly HashSet<UnitCombatAI> exited = new HashSet<UnitCombatAI>();
        readonly HashSet<CharacterActor> fled = new HashSet<CharacterActor>();
        readonly Dictionary<int, int> wallLossFrame = new Dictionary<int, int>();
        readonly HashSet<UnitCombatAI> panicking = new HashSet<UnitCombatAI>();
        readonly Dictionary<StatSheet, BattleSquadMorale> attackOwners = new Dictionary<StatSheet, BattleSquadMorale>();
        readonly Dictionary<BattleSquadMorale, float> attackMultipliers = new Dictionary<BattleSquadMorale, float>();
        readonly List<KeyValuePair<SquadMorale, Action>> moraleHandlers = new List<KeyValuePair<SquadMorale, Action>>();
        readonly Dictionary<int, string> notices = new Dictionary<int, string>();
        Transform panicPoint;
        bool battleActive;
        bool gateLost;

        /// <summary>도망치는 아군이 달려가는 곳. 전투 시작 때 성 격자에서 계산한다.</summary>
        public Vector3 RetreatPoint { get; private set; }
        public Vector3 PanicRetreatPoint => panicPoint != null ? panicPoint.position : RetreatPoint;
        public bool BattleActive => battleActive;
        public MoraleSettings Settings => settings;

        public void Configure(GameFlow gameFlow, SquadCommandCenter squads, ConstructionWorld castle, MoraleSettings morale,
            Vector2Int enemyOutward, Func<RunState> run, IRandomSource randomSource)
        {
            if (flow != null) flow.PhaseChanged -= OnPhaseChanged;
            flow = gameFlow; center = squads; world = castle; settings = morale; outward = enemyOutward;
            runState = run; random = randomSource ?? new SeededRandom(Environment.TickCount);
            if (flow != null) flow.PhaseChanged += OnPhaseChanged;
        }

        /// <summary>한계 판정에 쓸 랜덤을 바꾼다(런 시드·테스트). 다음 전투부터 적용된다.</summary>
        public void UseRandom(IRandomSource source)
        {
            if (source != null) random = source;
        }

        /// <summary>
        /// 이번 런에서 찍은 퍽(COMBAT-07). 다음 전투 시작부터 <see cref="PerkTrigger.BattleStart"/> 특수 효과가 발동한다.
        /// null이면 퍽을 쓰지 않는다. 런 퍽 연결은 META-02에서 한다.
        /// </summary>
        public void UsePerks(PerkLoadout loadout) => perks = loadout;

        /// <summary>
        /// 부대장 특성이 더하는 각성 확률(COMBAT-07). 전투 시작 때 살아 있는 부대장마다 한 번 부르고,
        /// 결과를 그 부대의 <see cref="BattleSquadMorale.AwakenChanceBonus"/>에 넣는다. null이면 0.
        /// </summary>
        public void UseCaptainAwakenBonus(Func<UnitCombatAI, float> bonus) => captainAwakenBonus = bonus;

        /// <summary>
        /// 전투 중인 모든 아군 부대의 사기를 올린다(퍽·스킬). 도망 중인 부대와 전투 밖에서는 아무 일도 없다.
        /// </summary>
        public void AddMoraleToAllySquads(float amount)
        {
            if (!battleActive) return;
            foreach (BattleSquadMorale battle in battles) battle?.ApplyGain(amount);
        }

        /// <summary>전투 중인 한 부대의 사기를 올린다(대상 지정 스킬). 실제로 오른 양을 돌려준다.</summary>
        public float AddMorale(int squad, float amount)
        {
            BattleSquadMorale battle = BattleOf(squad);
            return battle != null ? battle.ApplyGain(amount) : 0f;
        }

        void OnDestroy()
        {
            if (flow != null) flow.PhaseChanged -= OnPhaseChanged;
            EndBattle(false);
        }

        void OnEnable() { if (flow != null) flow.PhaseChanged += OnPhaseChanged; }
        void OnDisable()
        {
            if (flow != null) flow.PhaseChanged -= OnPhaseChanged;
            EndBattle(false);
        }

        /// <summary>부대의 사기. 런 부대에 없으면 시작값(150)으로 만든다. 부대 번호가 틀리면 null.</summary>
        public SquadMorale MoraleOf(int squad)
        {
            if (center == null || squad < 0 || squad >= center.Squads.Count) return null;
            RunState state = runState?.Invoke();
            Squad<UnitCombatAI> scene = center.Squads[squad];
            if (state != null && state.Army.Squads.Count == center.Squads.Count)
            {
                Squad<RunUnit> owner = state.Army.Squads[squad];
                if (owner.Morale == null) owner.Morale = scene.Morale ?? NewSquadMorale(state);
                scene.Morale = owner.Morale;
                return owner.Morale;
            }
            if (scene.Morale == null) scene.Morale = NewSquadMorale(state);
            return scene.Morale;
        }

        // 새 부대의 사기. 승천의 시작 사기 감소(FLOW-03)를 런에서 읽는다. 런이 없거나 단계 0이면 설정의 시작값이다.
        SquadMorale NewSquadMorale(RunState state)
        {
            SquadMorale morale = settings.CreateSquadMorale();
            if (state == null || state.Ascension.Level == 0) return morale;
            return new SquadMorale(morale.Max, state.Ascension.StartingMorale(morale.Value, morale.Max));
        }

        /// <summary>이번 전투의 부대 사기 규칙. 전투 중이 아니거나 살아 있는 부대원 없이 시작한 부대면 null.</summary>
        public BattleSquadMorale BattleOf(int squad) => battleActive && squad >= 0 && squad < battles.Count ? battles[squad] : null;

        public bool IsRouting(UnitCombatAI unit) => unit != null && routing.ContainsKey(unit);
        public bool IsPanicking(UnitCombatAI unit) => unit != null && panicking.Contains(unit);
        public string NoticeOf(int squad) => notices.TryGetValue(squad, out string notice) ? notice : "";
        public string LastNotice { get; private set; } = "";

        public string StateOf(int squad)
        {
            BattleSquadMorale battle = BattleOf(squad);
            if (battle == null) return "Normal";
            if (battle.IsRouted) return "Routed";
            if (battle.State == MoraleState.Collapsed) return "Collapsed: " + CollapseName(battle.Collapse);
            if (battle.State == MoraleState.Awakened) return "Awakened: " + AwakenName(battle.Awaken);
            return "Normal";
        }

        static string CollapseName(CollapseEffect effect) => effect == CollapseEffect.Fear ? "Fear"
            : effect == CollapseEffect.Panic ? "Panic" : "Contagion";
        static string AwakenName(AwakenEffect effect) => effect == AwakenEffect.Rally ? "Rally"
            : effect == AwakenEffect.LastStand ? "Last stand" : "Steadfast";

        void SetNotice(int squad, string notice)
        {
            notices[squad] = notice;
            LastNotice = "Squad " + (squad + 1) + ": " + notice;
        }

        void OnBreakingPoint(BattleSquadMorale battle)
        {
            int squad = battles.IndexOf(battle);
            if (squad >= 0) SetNotice(squad, "Limit check - " + StateOfBattle(battle));
        }

        static string StateOfBattle(BattleSquadMorale battle) => battle.State == MoraleState.Awakened
            ? "Awakened: " + AwakenName(battle.Awaken) : "Collapsed: " + CollapseName(battle.Collapse);

        bool AcceptOrder(int squad, SquadOrder order)
        {
            BattleSquadMorale battle = BattleOf(squad);
            if (battle == null) return true;
            if (battle.IsRouted || SquadIsPanicking(squad)) return false;
            if (!battle.ShouldIgnoreOrder()) return true;
            SetNotice(squad, "Order ignored due to fear");
            return false;
        }

        bool SquadIsPanicking(int squad)
        {
            if (panicking.Contains(center.Squads[squad].Leader)) return true;
            foreach (UnitCombatAI member in center.Squads[squad].Members)
                if (panicking.Contains(member)) return true;
            return false;
        }

        void OnPanic(BattleSquadMorale battle)
        {
            int squad = battles.IndexOf(battle);
            if (squad < 0 || battle.IsRouted || SquadIsPanicking(squad)) return;
            foreach (UnitCombatAI member in center.Squads[squad].Members) StartPanic(member);
            StartPanic(center.Squads[squad].Leader);
            SetNotice(squad, "Panic: waiting at retreat point until battle ends");
        }

        void StartPanic(UnitCombatAI unit)
        {
            if (!IsAlive(unit) || routing.ContainsKey(unit) || !panicking.Add(unit)) return;
            center.Detach(unit);
            unit.SetDirective(UnitDirective.Move, PanicRetreatPoint);
        }

        void TrackAttack(UnitCombatAI unit, BattleSquadMorale battle)
        {
            if (IsAlive(unit)) attackOwners[unit.Actor.Stats] = battle;
        }

        void RefreshAttack(BattleSquadMorale battle)
        {
            float multiplier = battle.AttackMultiplier;
            if (attackMultipliers.TryGetValue(battle, out float previous) && Mathf.Approximately(previous, multiplier)) return;
            attackMultipliers[battle] = multiplier;
            foreach (var pair in attackOwners)
            {
                if (pair.Value != battle) continue;
                pair.Key.RemoveModifiersFrom(battle);
                if (!Mathf.Approximately(multiplier, 1f))
                    pair.Key.AddModifier(StatModifier.Percent(StatType.Attack, multiplier - 1f), battle);
            }
        }
        public bool HasExited(UnitCombatAI unit) => unit != null && exited.Contains(unit);

        /// <summary>이번 전투에서 도망친(달리는 중이거나 빠진) 아군. 결과의 <see cref="UnitFate.Fled"/>에 쓴다. 죽음이 먼저다.</summary>
        public bool HasFled(CharacterActor actor) => actor != null && fled.Contains(actor);

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Battle) BeginBattle();
            else if (phase == GamePhase.Reward || phase == GamePhase.RunResult) EndBattle(true);
            else if (phase == GamePhase.Preparation) ReturnFled();
        }

        void BeginBattle()
        {
            if (center == null || settings == null) return;
            EndBattle(false);
            routing.Clear(); exited.Clear(); fled.Clear(); wallLossFrame.Clear();
            notices.Clear();
            LastNotice = "";
            gateLost = false;
            RetreatPoint = ComputeRetreatPoint();
            if (panicPoint == null)
            {
                panicPoint = new GameObject("PanicRetreatPoint").transform;
                panicPoint.SetParent(transform, false);
            }
            panicPoint.position = RetreatPoint;
            for (int i = 0; i < center.Squads.Count; i++)
            {
                Squad<UnitCombatAI> squad = center.Squads[i];
                int alive = 0;
                foreach (UnitCombatAI member in squad.Members) if (IsAlive(member)) alive++;
                if (alive == 0 && !IsAlive(squad.Leader)) { battles.Add(null); continue; }
                var battle = new BattleSquadMorale(MoraleOf(i), MoraleSide.Ally, Mathf.Max(1, alive), settings, random);
                if (captainAwakenBonus != null && IsAlive(squad.Leader))
                    battle.AwakenChanceBonus = captainAwakenBonus(squad.Leader);
                battle.Routed += OnRouted;
                battle.NearbyMoraleChange += OnNearbyMoraleChange;
                battle.PanicRetreat += OnPanic;
                battle.BreakingPointReached += OnBreakingPoint;
                battles.Add(battle);
                foreach (UnitCombatAI member in squad.Members) TrackAttack(member, battle);
                TrackAttack(squad.Leader, battle);
                Action changed = () => RefreshAttack(battle);
                battle.Morale.Changed += changed;
                moraleHandlers.Add(new KeyValuePair<SquadMorale, Action>(battle.Morale, changed));
                RefreshAttack(battle);
                if (battle.StartedShaken) SetNotice(i, "Low morale: weakened " + CollapseName(battle.Collapse));
                foreach (UnitCombatAI member in squad.Members)
                    if (IsAlive(member)) WatchDeath(member.Actor, battle.OnMemberDied);
                if (IsAlive(squad.Leader)) WatchDeath(squad.Leader.Actor, battle.OnCaptainDied);
            }
            if (world != null) world.PieceCollapsing += OnPieceCollapsing;
            center.Commander.OrderRequested += AcceptOrder;
            battleActive = true;
            // 모든 부대의 전투 사기와 공격력 보정이 준비된 뒤 한 번만 발동한다.
            perks?.RaiseTrigger(PerkTrigger.BattleStart, this);
        }

        void EndBattle(bool recover)
        {
            if (!battleActive) return;
            battleActive = false;
            if (center != null && center.Commander != null) center.Commander.OrderRequested -= AcceptOrder;
            foreach (var pair in moraleHandlers) pair.Key.Changed -= pair.Value;
            moraleHandlers.Clear();
            foreach (var pair in attackOwners) pair.Key.RemoveModifiersFrom(pair.Value);
            attackOwners.Clear(); attackMultipliers.Clear();
            foreach (UnitCombatAI unit in panicking)
                if (unit != null)
                {
                    center?.Reattach(unit);
                    if (IsAlive(unit)) unit.SetDirective(UnitDirective.Hold);
                }
            panicking.Clear();
            notices.Clear();
            LastNotice = "";
            if (world != null) world.PieceCollapsing -= OnPieceCollapsing;
            foreach (KeyValuePair<CharacterActor, Action> pair in deathHandlers)
                if (pair.Key != null) pair.Key.Died -= pair.Value;
            deathHandlers.Clear();
            foreach (BattleSquadMorale battle in battles)
            {
                if (battle == null) continue;
                battle.Routed -= OnRouted;
                battle.NearbyMoraleChange -= OnNearbyMoraleChange;
                battle.PanicRetreat -= OnPanic;
                battle.BreakingPointReached -= OnBreakingPoint;
                battle.Dispose();
            }
            battles.Clear();
            if (!recover || center == null) return;
            // Q-39: 모든 부대가 전투 후 max(현재 + 20, 80)이 된다. 도망친 부대(0)는 80에서 다음 전투를 시작한다.
            for (int i = 0; i < center.Squads.Count; i++) MoraleRules.RecoverAfterBattle(MoraleOf(i), settings);
        }

        void WatchDeath(CharacterActor actor, Action handler)
        {
            if (actor == null) return;
            actor.Died += handler;
            deathHandlers.Add(new KeyValuePair<CharacterActor, Action>(actor, handler));
        }

        void OnRouted(BattleSquadMorale battle)
        {
            int index = battles.IndexOf(battle);
            if (index < 0) return;
            Squad<UnitCombatAI> squad = center.Squads[index];
            bool hasCenter = TryGetCenter(index, out Vector3 point);
            foreach (UnitCombatAI member in squad.Members) StartRout(member);
            StartRout(squad.Leader);
            if (!hasCenter) return;
            for (int i = 0; i < battles.Count; i++)
                if (i != index && battles[i] != null && !battles[i].IsRouted && IsNear(i, point))
                    battles[i].OnNearbySquadRouted();
        }

        void StartRout(UnitCombatAI unit)
        {
            if (!IsAlive(unit) || routing.ContainsKey(unit)) return;
            panicking.Remove(unit);
            center.Detach(unit);
            unit.SetDirective(UnitDirective.Move, RetreatPoint);
            routing.Add(unit, Time.time);
            fled.Add(unit.Actor);
        }

        void OnNearbyMoraleChange(BattleSquadMorale source, float amount)
        {
            int index = battles.IndexOf(source);
            if (index < 0 || !TryGetCenter(index, out Vector3 point)) return;
            for (int i = 0; i < battles.Count; i++)
                if (i != index && battles[i] != null && !battles[i].IsRouted && IsNear(i, point))
                {
                    // 사기 상승(근처 부대 사기 상승)은 상승 규칙, 전염은 그대로 깎는다.
                    if (amount > 0f) battles[i].ApplyGain(amount);
                    else battles[i].Morale.Add(amount);
                }
        }

        void OnPieceCollapsing(PlacedStructure piece)
        {
            if (!battleActive || piece == null || piece.Placement == null) return;
            BuildPieceKind kind = piece.Placement.Definition.Kind;
            if (kind == BuildPieceKind.Gate)
            {
                if (gateLost) return;
                foreach (PlacedStructure other in world.Structures)
                    if (other != piece && other != null && other.Placement != null
                        && other.Placement.Definition.Kind == BuildPieceKind.Gate
                        && other.Health != null && !other.Health.IsDead) return;
                gateLost = true;
                foreach (BattleSquadMorale battle in battles) battle?.OnGateDestroyed();
                return;
            }
            if (kind != BuildPieceKind.Wall) return;
            // 한 번 무너질 때 위에 쌓인 조각이 함께 부서져도 부대마다 한 번만 깎는다.
            Vector3 position = piece.transform.position;
            for (int i = 0; i < battles.Count; i++)
            {
                if (battles[i] == null || battles[i].IsRouted) continue;
                if (wallLossFrame.TryGetValue(i, out int frame) && frame == Time.frameCount) continue;
                if (!IsNear(i, position)) continue;
                wallLossFrame[i] = Time.frameCount;
                battles[i].OnNearbyWallCollapsed();
            }
        }

        void Update()
        {
            if (!battleActive) return;
            // Tick may route other squads through Contagion. Do not mutate battles during this phase.
            foreach (BattleSquadMorale battle in battles)
            {
                if (battle == null) continue;
                battle.Tick(Time.deltaTime);
                RefreshAttack(battle);
            }
            foreach (UnitCombatAI unit in panicking)
            {
                if (!IsAlive(unit)) continue;
                Vector3 offset = unit.transform.position - PanicRetreatPoint;
                offset.y = 0f;
                if (unit.Directive == UnitDirective.Move
                    && offset.sqrMagnitude <= settings.RetreatArrivalDistance * settings.RetreatArrivalDistance)
                    unit.SetDirective(UnitDirective.Hold);
            }
            if (routing.Count == 0) return;
            float arrival = settings.RetreatArrivalDistance;
            var leaving = new List<UnitCombatAI>();
            var dead = new List<UnitCombatAI>();
            foreach (KeyValuePair<UnitCombatAI, float> pair in routing)
            {
                UnitCombatAI unit = pair.Key;
                if (unit == null || unit.Actor == null || unit.Actor.Health == null || unit.Actor.Health.IsDead)
                { dead.Add(unit); continue; }
                Vector3 offset = unit.transform.position - RetreatPoint;
                offset.y = 0f;
                if (offset.sqrMagnitude <= arrival * arrival || Time.time - pair.Value >= settings.RetreatTimeout)
                    leaving.Add(unit);
            }
            foreach (UnitCombatAI unit in dead) routing.Remove(unit);
            foreach (UnitCombatAI unit in leaving)
            {
                routing.Remove(unit);
                exited.Add(unit);
                // 숨기면 살아 있는 아군 수와 적의 목표에서 빠진다.
                unit.gameObject.SetActive(false);
            }
        }

        void ReturnFled()
        {
            if (center == null) return;
            var returning = new List<UnitCombatAI>();
            foreach (UnitCombatAI unit in routing.Keys) if (unit != null) returning.Add(unit);
            foreach (UnitCombatAI unit in exited) if (unit != null) returning.Add(unit);
            routing.Clear(); exited.Clear(); fled.Clear();
            var revived = new List<UnitCombatAI>();
            foreach (UnitCombatAI unit in returning)
            {
                CharacterActor actor = unit.GetComponent<CharacterActor>();
                if (actor == null || actor.Health == null || actor.Health.IsDead) { center.Reattach(unit); continue; }
                if (!unit.gameObject.activeSelf)
                {
                    unit.transform.position = RetreatPoint;
                    unit.gameObject.SetActive(true);
                }
                revived.Add(unit);
            }
            if (revived.Count > 0) StartCoroutine(WalkBack(revived));
        }

        // 준비 단계의 멈춤 해제(GameBattleController)가 끝나 유닛이 길찾기에 올라서면 부대가 모인 곳으로 보낸다.
        IEnumerator WalkBack(List<UnitCombatAI> units)
        {
            for (int frame = 0; frame < ReturnWaitFrames; frame++)
            {
                if (flow != null && flow.Phase != GamePhase.Preparation) yield break;
                bool ready = world == null || (!world.IsRebuilding && !world.RebuildPending);
                if (ready)
                    foreach (UnitCombatAI unit in units)
                    {
                        if (unit == null) continue;
                        var agent = unit.GetComponent<NavMeshAgent>();
                        if (!unit.isActiveAndEnabled || agent == null || !agent.enabled || !agent.isOnNavMesh) { ready = false; break; }
                    }
                if (ready) break;
                yield return null;
            }
            // 모이는 곳은 모두 떼어 둔 채 먼저 구해 돌아오는 유닛 자신의 위치가 섞이지 않게 한다.
            var destinations = new Dictionary<UnitCombatAI, Vector3>();
            foreach (UnitCombatAI unit in units)
            {
                if (unit == null) continue;
                int squad = SquadIndexOf(unit);
                if (squad >= 0 && center.TryGetGatherPoint(squad, out Vector3 point)) destinations[unit] = point;
            }
            foreach (UnitCombatAI unit in units)
            {
                if (unit == null) continue;
                center.Reattach(unit);
                if (!unit.isActiveAndEnabled) continue;
                if (destinations.TryGetValue(unit, out Vector3 destination)) unit.SetDirective(UnitDirective.Move, destination);
                else unit.SetDirective(UnitDirective.Hold);
            }
        }

        int SquadIndexOf(UnitCombatAI unit)
        {
            for (int i = 0; i < center.Squads.Count; i++)
            {
                Squad<UnitCombatAI> squad = center.Squads[i];
                if (squad.Leader == unit) return i;
                foreach (UnitCombatAI member in squad.Members) if (member == unit) return i;
            }
            return -1;
        }

        // 부대의 가운데: 살아서 아직 전투에 남은 부대원(달리는 중 포함)의 평균 위치.
        bool TryGetCenter(int squad, out Vector3 point)
        {
            point = default;
            int count = 0;
            foreach (UnitCombatAI member in center.Squads[squad].Members)
            {
                if (!IsAlive(member)) continue;
                point += member.transform.position;
                count++;
            }
            if (count == 0) return false;
            point /= count;
            return true;
        }

        // 근처: 살아서 전투에 남은 부대원 중 하나라도 반경 안에 있다.
        bool IsNear(int squad, Vector3 point)
        {
            float radius = settings.NearbyRadius;
            foreach (UnitCombatAI member in center.Squads[squad].Members)
                if (IsAlive(member) && (member.transform.position - point).sqrMagnitude <= radius * radius) return true;
            return false;
        }

        static bool IsAlive(UnitCombatAI unit) => unit != null && unit.isActiveAndEnabled && unit.Actor != null
            && unit.Actor.Health != null && !unit.Actor.Health.IsDead;

        // 적이 오는 쪽의 반대편 성 가장자리에서 안쪽으로 들어온 곳(Q-44). 성 조각들의 범위로 성 가장자리를 찾는다.
        Vector3 ComputeRetreatPoint()
        {
            if (world == null) return transform.position;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (PlacedStructure piece in world.Structures)
            {
                if (piece == null) continue;
                Vector3 local = world.LocalPoint(piece.transform.position);
                minX = Mathf.Min(minX, local.x); maxX = Mathf.Max(maxX, local.x);
                minZ = Mathf.Min(minZ, local.z); maxZ = Mathf.Max(maxZ, local.z);
            }
            if (minX > maxX)
            {
                minX = 0f; minZ = 0f;
                maxX = world.Grid.Width * world.Grid.CellSize; maxZ = world.Grid.Depth * world.Grid.CellSize;
            }
            var direction = new Vector2(outward.x, outward.y);
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
            direction.Normalize();
            var middle = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            float extent = Mathf.Abs(direction.x) * (maxX - minX) * 0.5f + Mathf.Abs(direction.y) * (maxZ - minZ) * 0.5f;
            Vector2 flat = middle - direction * Mathf.Max(0f, extent - settings.RetreatInset);
            Vector3 point = world.transform.TransformPoint(new Vector3(flat.x, 0f, flat.y));
            return NavMesh.SamplePosition(point, out NavMeshHit hit, settings.RetreatInset + 5f, NavMesh.AllAreas)
                ? hit.position : point;
        }
    }
}
