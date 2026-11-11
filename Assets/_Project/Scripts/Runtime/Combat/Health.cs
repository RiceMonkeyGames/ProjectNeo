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
