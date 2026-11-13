using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Facilities;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 준비 중에 런에 들어온 잡졸을 Game 씬에 만든다(D-0b, Q-36). 시설 앞에 나타나 소속 부대 쪽으로 걸어가고,
    /// 그 뒤로는 기존 부대원처럼 지휘·전투·결과 집계에 들어간다. 병영(D-4)이 <see cref="Recruit"/>를 부른다.
    /// 에디터에서는 확인용 키(기본 F9)로 병영 앞에 잡졸을 하나 모집한다. 병영 UI가 생기면 이 키는 지운다.
    /// </summary>
    public sealed class GameUnitSpawner : MonoBehaviour
    {
        [SerializeField] GameBattleController battle;
        [SerializeField] SquadCommandCenter squads;
        [SerializeField] ConstructionWorld world;
        [SerializeField] UnitAISettings aiSettings;
        [SerializeField] FallSettings fallSettings;
        [Tooltip("시설 건물 중심에서 유닛이 나타나는 곳까지의 거리(m).")]
        [SerializeField, Min(0f)] float facilityFrontDistance = 6f;
        [Tooltip("나타날 곳을 걸을 수 있는 지점으로 붙일 때 찾는 반경(m).")]
        [SerializeField, Min(0.1f)] float navigationSnapDistance = 4f;
        [Tooltip("부대가 모인 곳 주변에서 새 부대원이 설 자리를 흩뜨리는 반경(m).")]
        [SerializeField, Min(0f)] float gatherScatter = 1.5f;
        [SerializeField] SoldierDefinition debugSoldier;
        readonly Dictionary<FacilityType, FacilityStation> stations = new Dictionary<FacilityType, FacilityStation>();
        Transform unitRoot;
        int spawned;

        public SoldierDefinition DebugSoldier => debugSoldier;

        public void Configure(GameBattleController battleController, SquadCommandCenter commandCenter, ConstructionWorld castle,
            UnitAISettings ai, FallSettings falling, SoldierDefinition debugRecruit)
        {
            battle = battleController; squads = commandCenter; world = castle;
            aiSettings = ai; fallSettings = falling; debugSoldier = debugRecruit;
        }

        void Awake()
        {
            unitRoot = new GameObject("RecruitedUnits").transform;
            unitRoot.SetParent(transform, false);
            foreach (FacilityStation station in FindObjectsByType<FacilityStation>(FindObjectsInactive.Include))
                if (station.gameObject.scene == gameObject.scene && !stations.ContainsKey(station.Type))
                    stations.Add(station.Type, station);
        }

#if UNITY_EDITOR
        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (debugSoldier == null || keyboard == null || !keyboard.f9Key.wasPressedThisFrame) return;
            UnitCombatAI unit = Recruit(debugSoldier, FacilityType.TrainingGround);
            Debug.Log(unit != null ? "[D-0b 확인용] 병영 앞에 잡졸을 모집했다: " + unit.name
                : "[D-0b 확인용] 모집하지 못했다(준비 단계가 아니거나 부대 수·인원 상한).");
        }
#endif

        /// <summary>
        /// 잡졸을 모집해 <paramref name="facility"/> 앞에 만든다. <paramref name="preferred"/>가 없으면 자리가 남은 첫 부대,
        /// 모두 꽉 찼으면 새 부대다(Q-36). 준비 단계가 아니거나 넣을 수 없으면 null. 비용은 받지 않는다.
        /// </summary>
        public UnitCombatAI Recruit(SoldierDefinition definition, FacilityType facility, Squad<RunUnit> preferred = null)
        {
            if (definition == null || battle == null) return null;
            if (definition.UnitPrefab == null)
            {
                Debug.LogWarning("잡졸 정의에 씬 유닛 프리팹이 없다: " + definition.Id, definition);
                return null;
            }
            Vector3 origin = stations.TryGetValue(facility, out FacilityStation station) ? station.transform.position
                : battle.transform.position;
            UnitCombatAI unit = battle.RunLink.TryRecruit(definition, preferred, def => Create(def, origin),
                out int squad, out Vector3 gather, out bool hasGather);
            if (unit == null) return null;

            Vector3 spawnPoint = FrontOf(origin, hasGather ? gather : origin + (station != null ? station.transform.forward : Vector3.forward));
            Place(unit, spawnPoint, hasGather ? gather : spawnPoint);
            unit.name = definition.Id + "_Recruit" + (++spawned) + "_Squad" + (squad + 1);
            unit.gameObject.SetActive(true);
            var agent = unit.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(spawnPoint);
            if (hasGather) unit.SetDirective(UnitDirective.Move, SlotNear(gather));
            else unit.SetDirective(UnitDirective.Hold);
            return unit;
        }

        // 비활성 상태로 만들어 AI·낙하 설정을 넣은 뒤 켠다. 프리팹 에셋은 바꾸지 않는다.
        UnitCombatAI Create(SoldierDefinition definition, Vector3 at)
        {
            var holder = new GameObject("RecruitHolder");
            holder.SetActive(false);
            GameObject instance = Instantiate(definition.UnitPrefab, at, Quaternion.identity, holder.transform);
            var unit = instance.GetComponent<UnitCombatAI>();
            var actor = instance.GetComponent<CharacterActor>();
            if (unit == null || actor == null || actor.SoldierDefinition != definition)
            {
                Debug.LogError("잡졸 프리팹에 UnitCombatAI와 같은 정의의 CharacterActor가 있어야 한다: " + definition.Id, definition);
                Destroy(holder);
                return null;
            }
            actor.SetTeam(CombatTeam.Allied);
            if (aiSettings != null) unit.Configure(aiSettings);
            var fall = instance.GetComponent<UnitFallController>();
            if (fall == null) fall = instance.AddComponent<UnitFallController>();
            fall.Configure(world, fallSettings);
            instance.SetActive(false);
            instance.transform.SetParent(unitRoot, true);
            Destroy(holder);
            return unit;
        }

        Vector3 FrontOf(Vector3 building, Vector3 toward)
        {
            Vector3 direction = toward - building;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            Vector3 candidate = building + direction.normalized * facilityFrontDistance;
            return NavMesh.SamplePosition(candidate, out NavMeshHit hit, navigationSnapDistance, NavMesh.AllAreas)
                ? hit.position : candidate;
        }

        static void Place(UnitCombatAI unit, Vector3 position, Vector3 lookAt)
        {
            Vector3 facing = lookAt - position;
            facing.y = 0f;
            unit.transform.SetPositionAndRotation(position,
                facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity);
        }

        // 모집 순번으로 정한 해바라기 배치라 같은 자리에 겹치지 않고, 무작위를 쓰지 않는다.
        Vector3 SlotNear(Vector3 gather)
        {
            float angle = spawned * 137.5f * Mathf.Deg2Rad;
            float radius = gatherScatter * Mathf.Sqrt((spawned % 8 + 1) / 8f);
            Vector3 candidate = gather + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            return NavMesh.SamplePosition(candidate, out NavMeshHit hit, navigationSnapDistance, NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - gather.y) <= 1f ? hit.position : gather;
        }
    }
}
