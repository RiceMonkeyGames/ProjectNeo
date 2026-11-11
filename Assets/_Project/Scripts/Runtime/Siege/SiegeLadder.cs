using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 맵에 고정된 사다리 자리 하나. 그 칸에 성벽이 있을 때만 쓸 수 있다.
    /// 적이 밑에서 세우고 한 명씩 오르며, 성벽 위의 아군이 밀어 쓰러뜨린다. 외형은 Visual 자식이 맡는다.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class SiegeLadder : MonoBehaviour
    {
        [SerializeField] ConstructionWorld world;
        [SerializeField] SiegeSettings settings;
        [SerializeField] Vector2Int cell;
        // 성 바깥을 향하는 격자 방향.
        [SerializeField] Vector2Int outward = new Vector2Int(0, 1);
        readonly HashSet<SiegeAttackerBrain> crowd = new HashSet<SiegeAttackerBrain>();
        SiegeAttackerBrain raiser;
        SiegeAttackerBrain climber;
        InputAction push;
        float wallHeight = 3f;
        int supportId;
        public SiegeLadderState State { get; private set; }
        public Vector2Int Cell => cell;
        public int Crowd => crowd.Count;
        /// <summary>플레이어가 밀 수 있는 거리 안에 있고 사다리가 세워져 있는지. 안내 표시에 쓴다.</summary>
        public bool PlayerCanPush { get; private set; }

        public void Configure(ConstructionWorld owner, SiegeSettings siegeSettings, Vector2Int ladderCell, Vector2Int outwardDirection)
        {
            world = owner; settings = siegeSettings; cell = ladderCell; outward = outwardDirection;
        }

        void Awake()
        {
            State = new SiegeLadderState(settings.LadderRaiseSeconds, settings.LadderPushSeconds);
            State.Toppled += OnToppled;
            push = settings.PushLadder.Clone();
        }

        void OnEnable() => push.Enable();
        void OnDisable() => push.Disable();
        void OnDestroy()
        {
            if (State != null) State.Toppled -= OnToppled;
            if (push != null) push.Dispose();
        }

        public bool HasWall
        {
            get
            {
                BuildPlacement placement = world.Layout != null ? world.Layout.Top(cell) : null;
                if (placement == null || placement.Definition.Kind != BuildPieceKind.Wall) return false;
                float height = placement.Elevation + placement.Definition.Height;
                if (supportId != 0 && (supportId != placement.Id || !Mathf.Approximately(wallHeight, height)))
                    State?.Topple();
                supportId = placement.Id;
                wallHeight = height;
                return true;
            }
        }

        public float WallHeight => wallHeight;
        public void ResetBattle()
        {
            State.Topple();
            crowd.Clear(); raiser = null; climber = null; supportId = 0; PlayerCanPush = false;
        }
        public Vector3 OutwardWorld => world.transform.TransformDirection(new Vector3(outward.x, 0f, outward.y)).normalized;

        Vector3 Point(float fromCenter, float height)
        {
            float size = world.Grid.CellSize;
            var center = new Vector3((cell.x + 0.5f) * size, height, (cell.y + 0.5f) * size);
            return world.transform.TransformPoint(center + new Vector3(outward.x, 0f, outward.y) * fromCenter);
        }

        /// <summary>적이 사다리를 세우고 오르기 시작하는 땅 위의 자리.</summary>
        public Vector3 BasePoint => Point(world.Grid.CellSize * 0.5f + settings.LadderBaseOffset, 0f);
        /// <summary>사다리 위쪽 끝. 성벽 바깥면 바로 앞이다.</summary>
        public Vector3 EdgePoint => Point(world.Grid.CellSize * 0.5f + 0.35f, wallHeight);
        /// <summary>다 오른 유닛이 올라서는 성벽 위의 자리.</summary>
        public Vector3 TopPoint => Point(world.Grid.CellSize * 0.5f - settings.LadderTopInset, wallHeight);

        public void Join(SiegeAttackerBrain unit) => crowd.Add(unit);

        public void Leave(SiegeAttackerBrain unit)
        {
            crowd.Remove(unit);
            if (raiser == unit) raiser = null;
            if (climber == unit) { climber = null; State.EndClimb(); }
        }

        public bool IsRaiser(SiegeAttackerBrain unit) => raiser == unit && unit != null;

        /// <summary>바닥에 놓인 사다리를 세우기 시작한다. 이미 다른 유닛이 세우고 있으면 실패한다.</summary>
        public bool TryRaise(SiegeAttackerBrain unit)
        {
            if (!HasWall || State.Phase == LadderPhase.Up) return false;
            if (raiser != null && raiser != unit && IsNearBase(raiser)) return false;
            raiser = unit;
            return true;
        }

        public bool TryBeginClimb(SiegeAttackerBrain unit)
        {
            if (!HasWall || !State.TryBeginClimb()) return false;
            climber = unit;
            return true;
        }

        bool IsNearBase(SiegeAttackerBrain unit)
        {
            if (unit == null || !unit.isActiveAndEnabled || unit.Actor.Health == null || unit.Actor.Health.IsDead) return false;
            Vector3 offset = unit.transform.position - BasePoint;
            offset.y = 0f;
            return offset.sqrMagnitude <= settings.LadderUseRadius * settings.LadderUseRadius;
        }

        void Update()
        {
            PlayerCanPush = false;
            if (!HasWall)
            {
                // 받치던 성벽이 사라지면 사다리도 쓰러지고 오르던 유닛은 떨어진다.
                State.Topple();
                raiser = null;
                return;
            }
            if (raiser != null && !IsNearBase(raiser)) raiser = null;
            bool pusher = false, blocked = false;
            if (State.Phase == LadderPhase.Up) FindPushers(out pusher, out blocked);
            State.Tick(Time.deltaTime, raiser != null, pusher, blocked);
            if (State.Phase == LadderPhase.Up) raiser = null;
        }

        void FindPushers(out bool pusher, out bool blocked)
        {
            pusher = false; blocked = false;
            Vector3 top = TopPoint;
            foreach (CharacterActor actor in CharacterActor.ActiveActors)
            {
                if (actor.gameObject.scene != gameObject.scene) continue;
                if (climber != null && actor == climber.Actor) continue;
                Vector3 offset = actor.transform.position - top;
                if (Mathf.Abs(offset.y) > settings.LadderHeightTolerance) continue;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (actor.Team == CombatTeam.Hostile)
                {
                    // 올라온 적이 있으면 아군은 그 적과 싸우느라 밀지 못한다.
                    if (distance <= settings.LadderBlockRadius) blocked = true;
                    continue;
                }
                if (distance > settings.LadderPushRadius) continue;
                if (actor.GetComponent<PlayerInputController>() != null)
                {
                    PlayerCanPush = true;
                    if (push.IsPressed()) pusher = true;
                }
                else
                {
                    var ai = actor.GetComponent<UnitCombatAI>();
                    if (ai != null && ai.Target == null && !actor.IsAttacking) pusher = true;
                }
            }
            if (blocked) PlayerCanPush = false;
        }

        void OnToppled(bool hadClimber)
        {
            SiegeAttackerBrain fallen = climber;
            climber = null;
            raiser = null;
            if (hadClimber && fallen != null) fallen.OnLadderToppled();
        }
    }
}
