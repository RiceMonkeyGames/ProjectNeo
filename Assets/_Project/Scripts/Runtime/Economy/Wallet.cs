using System;
using System.Collections.Generic;

namespace ProjectNeo.Economy
{
    /// <summary>플레이어가 가진 골드와 몬스터 재료.</summary>
    public sealed class Wallet
    {
        readonly Dictionary<MaterialDefinition, int> materials = new Dictionary<MaterialDefinition, int>();

        public int Gold { get; private set; }

        public event Action Changed;

        /// <summary>가진 재료와 수. 0개인 재료도 한 번 가졌으면 들어 있다.</summary>
        public IEnumerable<KeyValuePair<MaterialDefinition, int>> Materials => materials;

        public int GetMaterial(MaterialDefinition material)
        {
            return material != null && materials.TryGetValue(material, out int amount) ? amount : 0;
        }

        public void AddGold(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Gold += amount;
            Changed?.Invoke();
        }

        public void AddMaterial(MaterialDefinition material, int amount)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            materials[material] = GetMaterial(material) + amount;
            Changed?.Invoke();
        }

        public bool CanAfford(Cost cost)
        {
            if (Gold < cost.gold) return false;
            foreach (KeyValuePair<MaterialDefinition, int> required in SumMaterials(cost))
                if (GetMaterial(required.Key) < required.Value) return false;
            return true;
        }

        /// <summary>비용을 낼 수 있으면 전부 차감하고 true, 모자라면 아무것도 차감하지 않고 false.</summary>
        public bool TrySpend(Cost cost)
        {
            if (!CanAfford(cost)) return false;
            Gold -= Math.Max(cost.gold, 0);
            foreach (KeyValuePair<MaterialDefinition, int> required in SumMaterials(cost))
                materials[required.Key] = GetMaterial(required.Key) - required.Value;
            Changed?.Invoke();
            return true;
        }

        // 비용에 같은 재료가 여러 줄로 들어 있어도 합쳐서 계산한다.
        static Dictionary<MaterialDefinition, int> SumMaterials(Cost cost)
        {
            var totals = new Dictionary<MaterialDefinition, int>();
            if (cost.materials == null) return totals;
            foreach (MaterialAmount entry in cost.materials)
            {
                if (entry.material == null || entry.amount <= 0) continue;
                totals.TryGetValue(entry.material, out int current);
                totals[entry.material] = current + entry.amount;
            }
            return totals;
        }
    }
}
