using ProjectNeo.Combat;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 포탄이 지나가는 충돌체 판정(SIEGE-33). 포탄 비행과 쏘기 전 포물선 검사가 같은 기준을 쓴다.
    /// 유닛 몸통 판정은 쓰는 쪽이 따로 한다(포탄은 적에 맞으면 터지고, 쏘기 전 검사는 유닛을 무시한다).
    /// </summary>
    public static class CannonShotFilter
    {
        /// <summary>대포를 받치는 성벽 조각 충돌체. 성벽 위에서 바로 아래로 쏠 때 발밑 모서리에 터지지 않게 통과한다.</summary>
        public static Collider Footing(PlacedSiegeWeapon weapon) =>
            weapon != null && weapon.Support != null ? weapon.Support.GetComponentInChildren<Collider>() : null;

        /// <summary>다른 장면, 발밑, 그 편이 쏠 수 있는 엄폐물, 쏜 대포 자신의 충돌체는 지나간다.</summary>
        public static bool PassesStructure(Collider collider, Scene scene, CombatTeam team, Collider footing,
            PlacedSiegeWeapon source)
        {
            if (collider == null) return true;
            if (collider.gameObject.scene != scene || collider == footing) return true;
            if (ShotCover.Passes(collider, team)) return true;
            return source != null && collider.transform.IsChildOf(source.transform);
        }
    }
}
