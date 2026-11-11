using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>테스트용 무기 동작 부품. 공통 몸통이 설치·제거를 한 번씩 알려주는지 기록한다.</summary>
    public sealed class RecordingSiegeWeaponModule : MonoBehaviour, ISiegeWeaponModule
    {
        public int Installed { get; private set; }
        public int Removed { get; private set; }
        public SiegeWeaponRemoval LastReason { get; private set; }
        public void OnInstalled(PlacedSiegeWeapon weapon) => Installed++;
        public void OnRemoved(PlacedSiegeWeapon weapon, SiegeWeaponRemoval reason) { Removed++; LastReason = reason; }
    }
}
