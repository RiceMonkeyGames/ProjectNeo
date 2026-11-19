using System.Collections.Generic;

namespace ProjectNeo.Meta
{
    /// <summary>열린 시작 보너스 항목을 모두 더한 값(META-07). 새 런을 시작할 때 한 번 준다.</summary>
    public sealed class UnlockStartingBonus
    {
        readonly Dictionary<string, int> items = new Dictionary<string, int>();

        public int Gold { get; private set; }
        public int Grunts { get; private set; }
        /// <summary>아이템 id별 개수.</summary>
        public IReadOnlyDictionary<string, int> Items => items;
        public bool IsEmpty => Gold == 0 && Grunts == 0 && items.Count == 0;

        internal void Add(UnlockPayload payload)
        {
            switch (payload.Kind)
            {
                case UnlockPayloadKind.StartingGold: Gold += payload.Amount; break;
                case UnlockPayloadKind.StartingGrunts: Grunts += payload.Amount; break;
                case UnlockPayloadKind.StartingItem:
                    if (string.IsNullOrEmpty(payload.TargetId) || payload.Amount <= 0) break;
                    items.TryGetValue(payload.TargetId, out int count);
                    items[payload.TargetId] = count + payload.Amount;
                    break;
            }
        }
    }
}
