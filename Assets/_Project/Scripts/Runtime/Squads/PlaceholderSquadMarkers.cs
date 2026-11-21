using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Squads
{
    /// <summary>
    /// 교체용 임시 지휘 표시. 선택한 부대 유닛의 발밑 원판과 지시한 목적지 원판을 보여준다.
    /// 선택·지시 상태를 읽기만 하며 규칙에 영향을 주지 않는다. 외곽선 등 최종 표현은 아트 단계에서 이 부품을 바꾼다.
    /// </summary>
    public sealed class PlaceholderSquadMarkers : MonoBehaviour
    {
        [SerializeField] SquadCommandCenter center;
        [SerializeField] Material selectionMaterial;
        [SerializeField] Material destinationMaterial;
        [SerializeField, Min(0.1f)] float memberDiameter = 1.3f;
        [SerializeField, Min(0.1f)] float destinationDiameter = 2.4f;
        [SerializeField, Min(0f)] float lift = 0.06f;
        [SerializeField, Min(0f)] float slotDiameter = 0.6f;
        readonly Dictionary<UnitCombatAI, GameObject> memberDiscs = new Dictionary<UnitCombatAI, GameObject>();
        readonly List<GameObject> destinationDiscs = new List<GameObject>();
        // 전열을 끄는 동안 병사마다 설 자리(COMBAT-08). 쓰는 만큼만 만들어 다시 쓴다.
        readonly List<GameObject> slotDiscs = new List<GameObject>();

        /// <summary>지금 보이는 자리 미리보기 원판 수.</summary>
        public int VisibleSlotCount
        {
            get { int count = 0; foreach (GameObject disc in slotDiscs) if (disc.activeSelf) count++; return count; }
        }

        public void Configure(SquadCommandCenter commandCenter, Material selection, Material destination)
        {
            center = commandCenter; selectionMaterial = selection; destinationMaterial = destination;
        }

        void Start() => EnsureDiscs();

        // 준비 중에 모집한 유닛·새 부대(D-0b)에도 표시를 붙인다.
        void EnsureDiscs()
        {
            for (int squad = 0; squad < center.Squads.Count; squad++)
            {
                foreach (UnitCombatAI member in center.Squads[squad].Members)
                    if (member != null && !memberDiscs.ContainsKey(member))
                        memberDiscs[member] = Disc("Selected_" + member.name, memberDiameter, selectionMaterial);
                if (squad >= destinationDiscs.Count)
                    destinationDiscs.Add(Disc("Destination_" + (squad + 1), destinationDiameter, destinationMaterial));
            }
        }

        GameObject Disc(string name, float diameter, Material material)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(transform, false);
            disc.transform.localScale = new Vector3(diameter, 0.01f, diameter);
            var renderer = disc.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            disc.SetActive(false);
            return disc;
        }

        public bool IsMarked(UnitCombatAI member) =>
            memberDiscs.TryGetValue(member, out GameObject disc) && disc.activeSelf;

        public bool TryGetDestinationMarker(int squad, out Vector3 position)
        {
            position = default;
            if (squad < 0 || squad >= destinationDiscs.Count || !destinationDiscs[squad].activeSelf) return false;
            position = destinationDiscs[squad].transform.position;
            return true;
        }

        void LateUpdate()
        {
            EnsureDiscs();
            // 편성(D-8)이나 전투 뒤 정리(C-3)로 부대가 줄면 남는 목적지 원판은 숨긴다. 부대 번호는 지금 부대 수까지만 읽는다.
            for (int squad = center.Squads.Count; squad < destinationDiscs.Count; squad++) destinationDiscs[squad].SetActive(false);
            for (int squad = 0; squad < center.Squads.Count && squad < destinationDiscs.Count; squad++)
            {
                bool selected = center.Commander.IsSelected(squad);
                foreach (UnitCombatAI member in center.Squads[squad].Members)
                {
                    if (member == null || !memberDiscs.TryGetValue(member, out GameObject disc)) continue;
                    bool alive = member != null && member.isActiveAndEnabled
                        && member.Actor.Health != null && !member.Actor.Health.IsDead;
                    disc.SetActive(selected && alive);
                    if (disc.activeSelf) disc.transform.position = member.transform.position + Vector3.up * lift;
                }
                // 가고 있거나 지키라고 지정한 지점만 보여준다. 도착해 중지가 된 이동은 표시를 지운다.
                SquadOrder order = center.Commander.OrderOf(squad);
                bool show = order.HasDestination && order.Kind != SquadOrderKind.Hold && center.AliveCount(squad) > 0;
                destinationDiscs[squad].SetActive(show);
                if (show) destinationDiscs[squad].transform.position = order.Destination + Vector3.up * lift;
            }
            ShowSlots(center.Preview);
        }

        void ShowSlots(FormationPreview preview)
        {
            int count = preview.Active ? preview.Slots.Count : 0;
            while (slotDiscs.Count < count) slotDiscs.Add(Disc("FormationSlot_" + (slotDiscs.Count + 1), slotDiameter, destinationMaterial));
            for (int i = 0; i < slotDiscs.Count; i++)
            {
                bool show = i < count;
                slotDiscs[i].SetActive(show);
                if (show) slotDiscs[i].transform.position = preview.Slots[i] + Vector3.up * lift;
            }
        }
    }
}
