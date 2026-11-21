using System;
using System.Collections.Generic;
using ProjectNeo.Economy;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 저장할 때의 값(골드 + 재료, META-10). 업데이트에서 그 콘텐츠가 없어지면 정의를 읽을 수 없으므로,
    /// 저장 파일에 적어 둔 이 값을 돌려준다(사용자 결정 2026-11-21). META-10 전 저장에는 없어 0으로 읽힌다.
    /// </summary>
    [Serializable]
    public sealed class ValueRecord
    {
        public int gold;
        public List<MaterialRecord> materials = new List<MaterialRecord>();

        public bool IsEmpty
        {
            get
            {
                if (gold > 0) return false;
                if (materials == null) return true;
                foreach (MaterialRecord material in materials)
                    if (material.amount > 0) return false;
                return true;
            }
        }

        public static ValueRecord Of(Cost cost)
        {
            var value = new ValueRecord { gold = Math.Max(0, cost.gold) };
            if (cost.materials == null) return value;
            foreach (MaterialAmount entry in cost.materials)
                if (entry.material != null && entry.amount > 0)
                    value.materials.Add(new MaterialRecord { id = entry.material.Id, amount = entry.amount });
            return value;
        }

        public static ValueRecord OfGold(int gold) => new ValueRecord { gold = Math.Max(0, gold) };
    }
}
