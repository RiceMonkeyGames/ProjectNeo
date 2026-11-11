using System;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>설치된 수성 무기의 공통 몸통. 모든 무기 종류가 같은 컴포넌트를 쓴다.</summary>
    public sealed class PlacedSiegeWeapon : MonoBehaviour
    {
        ISiegeWeaponModule[] modules = Array.Empty<ISiegeWeaponModule>();
        public MountedWeapon Mount { get; private set; }
        public SiegeWeaponDefinition Definition => Mount?.Definition;
        public PlacedStructure Support { get; private set; }
        public bool IsRemoved { get; private set; }
        public event Action<PlacedSiegeWeapon, SiegeWeaponRemoval> Removed;

        public void Initialize(MountedWeapon mount, PlacedStructure support)
        {
            Mount = mount;
            Support = support;
            modules = GetComponentsInChildren<ISiegeWeaponModule>(true);
            foreach (ISiegeWeaponModule module in modules) module.OnInstalled(this);
        }

        public void NotifyRemoved(SiegeWeaponRemoval reason)
        {
            if (IsRemoved) return;
            IsRemoved = true;
            foreach (ISiegeWeaponModule module in modules) module.OnRemoved(this, reason);
            Removed?.Invoke(this, reason);
        }
    }
}
