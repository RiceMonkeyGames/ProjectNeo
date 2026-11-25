using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 시설 앞에 선 NPC(SIEGE-36). F 상호작용은 건물이 아니라 이 NPC의 충돌체를 바라볼 때 그 시설을 연다.
    /// 시설 쪽 <see cref="PreparationStation.Npc"/>가 이 NPC를 가리키면 건물 충돌체로는 열리지 않는다.
    /// </summary>
    public sealed class StationNpc : MonoBehaviour
    {
        [SerializeField] PreparationStation station;
        [SerializeField] string displayName = "주민";

        public PreparationStation Station => station;
        /// <summary>안내 줄에 쓰는 NPC 이름(예: 바텐더).</summary>
        public string DisplayName => displayName;

        public void Configure(PreparationStation target, string npcName)
        {
            station = target;
            displayName = npcName;
            if (target != null) target.SetNpc(this);
        }
    }
}
