using System;
using ProjectNeo.Combat;
using ProjectNeo.Stats;
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
        /// <summary>무기 자체 체력. 설치할 때 정의의 최대 체력으로 채운다.</summary>
        public Health Health { get; private set; }
        public bool IsRemoved { get; private set; }
        /// <summary>
        /// 다 짓지 못한 무기(SIEGE-08). 전투에서 쏘지 않는다(무장하지 않는다). 전투를 넘겨도 남고, 이후 준비에서 체력을 끝까지
        /// 수리하면 완성된다.
        /// </summary>
        public bool IsUnfinished { get; private set; }
        public void SetUnfinished(bool value) => IsUnfinished = value;
        public event Action<PlacedSiegeWeapon, SiegeWeaponRemoval> Removed;
        /// <summary>체력이 0이 된 순간 한 번 알린다. 실제 제거는 ConstructionWorld가 한다.</summary>
        public event Action<PlacedSiegeWeapon> HealthDepleted;

        public void Initialize(MountedWeapon mount, PlacedStructure support)
        {
            Mount = mount;
            Support = support;
            var stats = new StatSheet();
            stats.SetBase(StatType.MaxHealth, mount.Definition.MaxHealth);
            Health = new Health(stats);
            Health.Died += OnDied;
            modules = GetComponentsInChildren<ISiegeWeaponModule>(true);
            foreach (ISiegeWeaponModule module in modules) module.OnInstalled(this);
        }

        /// <summary>제거된 무기는 피해를 받지 않는다. 실제로 깎인 양을 돌려준다.</summary>
        public float ReceiveDamage(float amount) => IsRemoved || Health == null ? 0f : Health.TakeDamage(amount);

        public void NotifyRemoved(SiegeWeaponRemoval reason)
        {
            if (IsRemoved) return;
            IsRemoved = true;
            foreach (ISiegeWeaponModule module in modules) module.OnRemoved(this, reason);
            Removed?.Invoke(this, reason);
        }

        void OnDied()
        {
            if (!IsRemoved) HealthDepleted?.Invoke(this);
        }

        void OnDestroy()
        {
            if (Health == null) return;
            Health.Died -= OnDied;
            Health.Dispose();
        }
    }
}
