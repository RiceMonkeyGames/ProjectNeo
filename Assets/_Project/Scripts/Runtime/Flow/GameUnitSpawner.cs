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
    /// 그 뒤로는 기존 부대원처럼 지휘·전투·결과 집계에 들어간다. 병영 팝업(D-4)이 <see cref="RecruitAtBarracks"/>를 부른다.
    /// D-0b의 F9 확인용 키는 병영 UI가 생겨 지웠다(D-4). <see cref="DebugSoldier"/>는 테스트가 쓰는 기본 잡졸이다.
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

        /// <summary>
        /// 잡졸을 모집해 <paramref name="facility"/> 앞에 만든다. <paramref name="preferred"/>가 없으면 자리가 남은 첫 부대,
        /// 모두 꽉 찼으면 새 부대다(Q-36). 준비 단계가 아니거나 넣을 수 없으면 null. 비용은 받지 않는다(테스트·연출용).
        /// 병영 모집은 <see cref="RecruitAtBarracks"/>를 쓴다.
        /// </summary>
        public UnitCombatAI Recruit(SoldierDefinition definition, FacilityType facility, Squad<RunUnit> preferred = null)
            => Recruit(definition, facility, preferred, false, out _);

        /// <summary>
        /// 병영 규칙으로 잡졸을 모집한다(D-4): 지금 병영 레벨에서 모집할 수 있는 종류여야 하고 모집 비용을 런 지갑에서 낸다.
        /// 병영 앞에 나타나 부대로 걸어가며 훈련 보너스를 받는다. 넣은 부대 순번은 <paramref name="squad"/>. 못 하면 null.
        /// </summary>
        public UnitCombatAI RecruitAtBarracks(SoldierDefinition definition, Squad<RunUnit> preferred, out int squad)
            => Recruit(definition, FacilityType.TrainingGround, preferred, true, out squad);

        UnitCombatAI Recruit(SoldierDefinition definition, FacilityType facility, Squad<RunUnit> preferred, bool pay, out int squad)
        {
            squad = -1;
            if (definition == null || battle == null) return null;
            if (definition.UnitPrefab == null)
            {
                Debug.LogWarning("잡졸 정의에 씬 유닛 프리팹이 없다: " + definition.Id, definition);
                return null;
            }
            Vector3 origin = stations.TryGetValue(facility, out FacilityStation station) ? station.transform.position
                : battle.transform.position;
            UnitCombatAI unit = battle.RunLink.TryRecruit(definition, preferred, def => Create(def, origin),
                out squad, out Vector3 gather, out bool hasGather, pay);
            if (unit == null) return null;

            Vector3 spawnPoint = FrontOf(origin, hasGather ? gather : origin + (station != null ? station.transform.forward : Vector3.forward));
            Place(unit, spawnPoint, hasGather ? gather : spawnPoint);
            unit.name = definition.Id + "_Recruit" + (++spawned) + "_Squad" + (squad + 1);
            unit.gameObject.SetActive(true);
            battle.RunLink.ApplyTraining();
            battle.RunLink.ApplyPerks();
            var agent = unit.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(spawnPoint);
            if (hasGather) unit.SetDirective(UnitDirective.Move, SlotNear(gather));
            else unit.SetDirective(UnitDirective.Hold);
            return unit;
        }

        /// <summary>
        /// 주점에서 영웅을 고용해 주점 앞에 만든다(D-3). <paramref name="captainOf"/>가 0 이상이면 그 부대 대장으로 부대 쪽으로 걸어가고,
        /// -1이면 대기 영웅으로 주점 앞에 선다. 비용을 낸다. 못 하면 null이고 이유는 <paramref name="check"/>.
        /// </summary>
        public UnitCombatAI HireAtTavern(HeroDefinition hero, int captainOf, out HireCheck check)
        {
            check = HireCheck.NotOffered;
            if (hero == null || battle == null) return null;
            if (hero.UnitPrefab == null)
            {
                Debug.LogWarning("영웅 정의에 씬 몸체 프리팹이 없다: " + hero.Id, hero);
                return null;
            }
            Vector3 origin = stations.TryGetValue(FacilityType.Tavern, out FacilityStation station) ? station.transform.position
                : battle.transform.position;
            Vector3 toward = origin + (station != null ? station.transform.forward : Vector3.forward);
            Vector3 spawnPoint = FrontOf(origin, toward);
            check = battle.RunLink.TryHire(hero, captainOf, def => CreateHero(def, spawnPoint, toward),
                out UnitCombatAI unit, out Vector3 gather, out bool hasGather);
            if (unit == null) return null;
            unit.name = hero.Id + "_Hero" + (++spawned) + (captainOf >= 0 ? "_Captain" + (captainOf + 1) : "_Reserve");
            battle.RunLink.ApplyPerks();
            if (hasGather) unit.SetDirective(UnitDirective.Move, SlotNear(gather));
            else unit.SetDirective(UnitDirective.Hold);
            return unit;
        }

        /// <summary>
        /// 저장된 런의 잡졸을 씬에 다시 만든다(FLOW-02). 런·부대에 넣는 일은 부르는 쪽이 한다.
        /// <paramref name="gather"/>(부대가 모인 곳)가 있으면 그 주변에, 없으면 병영 앞에 서서 자리를 지킨다. 못 만들면 null.
        /// </summary>
        public UnitCombatAI SpawnRestoredSoldier(SoldierDefinition definition, Vector3? gather)
        {
            if (definition == null || definition.UnitPrefab == null) return null;
            Vector3 origin = RestoreOrigin(FacilityType.TrainingGround, out Vector3 toward);
            UnitCombatAI unit = Create(definition, origin);
            if (unit == null) return null;
            spawned++;
            Vector3 point = SlotNear(gather ?? FrontOf(origin, toward));
            Place(unit, point, point + (toward - origin));
            unit.name = definition.Id + "_Restored" + spawned;
            unit.gameObject.SetActive(true);
            var agent = unit.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(point);
            unit.SetDirective(UnitDirective.Hold);
            return unit;
        }

        /// <summary>
        /// 저장된 런의 고용 영웅을 씬에 다시 만든다(FLOW-02). 대장이면 부대가 모인 곳 주변에, 대기 영웅이면 주점 앞에 선다.
        /// 켜진 채로 돌려주므로 영웅 캐릭터가 이미 있다. 못 만들면 null.
        /// </summary>
        public UnitCombatAI SpawnRestoredHero(HeroDefinition hero, Vector3? gather)
        {
            if (hero == null || hero.UnitPrefab == null) return null;
            Vector3 origin = RestoreOrigin(FacilityType.Tavern, out Vector3 toward);
            spawned++;
            Vector3 point = SlotNear(gather ?? FrontOf(origin, toward));
            UnitCombatAI unit = CreateHero(hero, point, point + (toward - origin));
            if (unit == null) return null;
            unit.name = hero.Id + "_RestoredHero" + spawned;
            unit.SetDirective(UnitDirective.Hold);
            return unit;
        }

        Vector3 RestoreOrigin(FacilityType facility, out Vector3 toward)
        {
            bool found = stations.TryGetValue(facility, out FacilityStation station);
            Vector3 origin = found ? station.transform.position : transform.position;
            toward = origin + (found ? station.transform.forward : Vector3.forward);
            return origin;
        }

        // 영웅 몸체를 꺼진 채 만들어 영웅 정의·AI·낙하 설정을 넣고, 제자리에 놓은 뒤 켠다. 켜야 영웅 캐릭터가 생긴다.
        UnitCombatAI CreateHero(HeroDefinition hero, Vector3 at, Vector3 lookAt)
        {
            var holder = new GameObject("HeroHolder");
            holder.SetActive(false);
            GameObject instance = Instantiate(hero.UnitPrefab, at, Quaternion.identity, holder.transform);
            var unit = instance.GetComponent<UnitCombatAI>();
            var actor = instance.GetComponent<CharacterActor>();
            if (unit == null || actor == null || actor.Profile == null)
            {
                Debug.LogError("영웅 몸체 프리팹에 UnitCombatAI와 전투 프로필이 있는 CharacterActor가 있어야 한다: " + hero.Id, hero);
                Destroy(holder);
                return null;
            }
            actor.Configure(hero, actor.Profile, CombatTeam.Allied);
            if (aiSettings != null) unit.Configure(aiSettings);
            var fall = instance.GetComponent<UnitFallController>();
            if (fall == null) fall = instance.AddComponent<UnitFallController>();
            fall.Configure(world, fallSettings);
            instance.SetActive(false);
            instance.transform.SetParent(unitRoot, true);
            Destroy(holder);
            Place(unit, at, lookAt);
            unit.gameObject.SetActive(true);
            var agent = unit.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(at);
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
