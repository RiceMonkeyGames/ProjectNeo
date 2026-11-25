using System.Collections.Generic;
using ProjectNeo.Core;
using ProjectNeo.Flow;
using UnityEngine;

namespace ProjectNeo.Town
{
    /// <summary>
    /// 성 안 주민을 단계에 맞춰 움직인다(SIEGE-36, 사용자 결정 2026-11-25).
    /// 준비: 시설 NPC는 입구 옆에 서고 시민은 산책 지점 사이를 걷는다. 전투가 시작되면 모두 가장 가까운 건물 입구로 달려가 숨고,
    /// 다음 준비 단계에 제자리로 돌아온다. 주민은 전투 대상이 아니다(CharacterActor가 없다).
    /// </summary>
    public sealed class TownDirector : MonoBehaviour
    {
        [SerializeField] TownSettings settings;
        [SerializeField] TownResident[] residents = new TownResident[0];
        [Tooltip("시민 산책 지점(월드 좌표). 길·광장 위다.")]
        [SerializeField] Vector3[] wanderPoints = new Vector3[0];
        [Tooltip("전투 때 달아날 건물 입구(월드 좌표).")]
        [SerializeField] Vector3[] shelters = new Vector3[0];
        [SerializeField] int seed = 3610;

        IRandomSource random;
        GameFlow flow;
        bool preparing;

        public IReadOnlyList<TownResident> Residents => residents;
        public IReadOnlyList<Vector3> Shelters => shelters;
        public IReadOnlyList<Vector3> WanderPoints => wanderPoints;
        public TownSettings Settings => settings;

        public void Configure(TownSettings config, TownResident[] people, Vector3[] wander, Vector3[] shelterPoints, int randomSeed)
        {
            settings = config; residents = people; wanderPoints = wander; shelters = shelterPoints; seed = randomSeed;
        }

        void Awake() => random = new SeededRandom(seed);

        void OnEnable()
        {
            flow = GameSession.Flow;
            flow.PhaseChanged += Apply;
        }

        void OnDisable()
        {
            if (flow != null) flow.PhaseChanged -= Apply;
        }

        void Start() => Apply(flow.Phase);

        void Apply(GamePhase phase)
        {
            preparing = phase == GamePhase.Preparation;
            if (preparing) ShowAll();
            else if (phase == GamePhase.Battle) FleeAll();
            else HideAll();
        }

        void ShowAll()
        {
            foreach (TownResident resident in residents)
                if (resident != null) resident.ShowAtHome(Idle());
        }

        void HideAll()
        {
            foreach (TownResident resident in residents)
                if (resident != null) resident.Hide();
        }

        /// <summary>모두 가장 가까운 건물 입구로 달려간다. 갈 수 없으면 바로 숨는다.</summary>
        public void FleeAll()
        {
            foreach (TownResident resident in residents)
            {
                if (resident == null || !resident.gameObject.activeSelf) continue;
                int shelter = TownRules.NearestShelter(resident.transform.position, shelters);
                if (shelter < 0 || !resident.MoveTo(shelters[shelter], settings.RunSpeed, TownResidentState.Fleeing))
                {
                    resident.Hide();
                    continue;
                }
                resident.Timer = settings.HideTimeout;
            }
        }

        float Idle() => TownRules.IdleSeconds(settings.IdleMin, settings.IdleMax, random);

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (TownResident resident in residents)
            {
                if (resident == null || !resident.gameObject.activeSelf) continue;
                switch (resident.State)
                {
                    case TownResidentState.Fleeing:
                        resident.Timer -= dt;
                        if (resident.HasArrived(settings.ArriveDistance) || resident.Timer <= 0f) resident.Hide();
                        break;
                    case TownResidentState.Standing:
                    case TownResidentState.Idle:
                        // 처음 놓일 때 NavMesh가 아직 없었으면 다시 놓는다.
                        if (resident.Agent != null && !resident.Agent.enabled) { if (preparing) resident.ShowAtHome(Idle()); break; }
                        if (resident.State == TownResidentState.Standing || !preparing) break;
                        resident.Timer -= dt;
                        if (resident.Timer > 0f) break;
                        int next = TownRules.NextWaypoint(resident.Waypoint, wanderPoints.Length, random);
                        if (next < 0 || !resident.MoveTo(wanderPoints[next], settings.WalkSpeed, TownResidentState.Walking))
                        { resident.Timer = Idle(); break; }
                        resident.Waypoint = next;
                        break;
                    case TownResidentState.Walking:
                        if (resident.HasArrived(settings.ArriveDistance)) resident.Rest(Idle());
                        break;
                }
            }
        }
    }
}
