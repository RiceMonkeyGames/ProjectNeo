using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 한 편의 사격만 막는 엄폐 충돌체(SIEGE-10 성가퀴). 지정한 편의 화살·사선 검사·포탄은 통과하고, 다른 편은 막힌다.
    /// 길찾기와 이동은 보통 충돌체처럼 막는다.
    /// </summary>
    public sealed class ShotCover : MonoBehaviour
    {
        [SerializeField] CombatTeam passableFor = CombatTeam.Allied;
        public CombatTeam PassableFor => passableFor;

        public void Configure(CombatTeam team) => passableFor = team;

        /// <summary>이 충돌체가 그 편의 사격을 막지 않는 엄폐물인지.</summary>
        public static bool Passes(Collider collider, CombatTeam shooter)
        {
            if (collider == null) return false;
            var cover = collider.GetComponent<ShotCover>();
            return cover != null && cover.passableFor == shooter;
        }
    }
}
