using System;
using ProjectNeo.Stats;

namespace ProjectNeo.Combat
{
    /// <summary>체력 규칙. 화면이나 조종 주체를 알지 않는다.</summary>
    public sealed class Health : IDisposable
    {
        readonly StatSheet stats;
        float current;
        bool dead;
        public float Maximum => stats.GetValue(StatType.MaxHealth);
        public float Current => current;
        public bool IsDead => dead;
        public event Action Changed;
        public event Action Died;

        public Health(StatSheet stats)
        {
            this.stats = stats ?? throw new ArgumentNullException(nameof(stats));
            current = Maximum;
            dead = current <= 0f;
            stats.Changed += OnStatChanged;
        }

        public float TakeDamage(float amount)
        {
            if (dead || float.IsNaN(amount) || amount <= 0f) return 0f;
            float applied = Math.Min(current, amount);
            current -= applied;
            Notify();
            return applied;
        }

        /// <summary>
        /// 살아 있으면 최대 체력까지 회복하고 회복량을 돌려준다. 죽은 유닛은 되살리지 않는다.
        /// 다음 전투 전 생존 유닛의 전체 회복(2026-11-12 결정)에 쓴다.
        /// </summary>
        public float RestoreFull()
        {
            if (dead) return 0f;
            float restored = Maximum - current;
            if (restored <= 0f) return 0f;
            current = Maximum;
            Changed?.Invoke();
            return restored;
        }

        /// <summary>살아 있으면 정한 양만큼 회복하고 실제 회복량을 돌려준다. 최대 체력을 넘지 않으며 죽은 유닛은 되살리지 않는다.</summary>
        public float Heal(float amount)
        {
            if (dead || float.IsNaN(amount) || amount <= 0f) return 0f;
            float applied = Math.Min(Maximum - current, amount);
            if (applied <= 0f) return 0f;
            current += applied;
            Changed?.Invoke();
            return applied;
        }

        /// <summary>파괴된 구조물(성문 등)을 최대 체력으로 복구한다.</summary>
        public float ReviveFull()
        {
            float restored = Maximum - current;
            dead = Maximum <= 0f;
            current = Maximum;
            Changed?.Invoke();
            return Math.Max(0f, restored);
        }

        void OnStatChanged(StatType stat)
        {
            if (stat != StatType.MaxHealth) return;
            current = Math.Min(current, Maximum);
            Notify();
        }

        void Notify()
        {
            bool diedNow = !dead && current <= 0f;
            if (diedNow) dead = true;
            Changed?.Invoke();
            if (diedNow) Died?.Invoke();
        }

        public void Dispose() => stats.Changed -= OnStatChanged;
    }
}
