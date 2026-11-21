using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Meta
{
    /// <summary>한 종류의 해금 트랙(Q-76). 항목은 화면에 보일 순서대로 둔다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Unlock Track", fileName = "UnlockTrack")]
    public sealed class UnlockTrackDefinition : ScriptableObject
    {
        [SerializeField] UnlockTrackCategory category;
        [SerializeField] string displayName;
        [SerializeField] List<UnlockEntry> entries = new List<UnlockEntry>();
        [SerializeField, Min(0), Tooltip("UI-06: unlock points one random unlock on this track costs (user 2026-11-21: 1 on every track).")]
        int drawCost = 1;

        public UnlockTrackCategory Category => category;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? category.ToString() : displayName;
        public IReadOnlyList<UnlockEntry> Entries => entries;
        /// <summary>이 트랙에서 무작위 해금 한 번에 드는 해금 포인트(UI-06). 항목마다의 비용은 쓰지 않는다.</summary>
        public int DrawCost => Mathf.Max(0, drawCost);

        /// <summary>무작위 해금 비용을 바꾼 트랙을 돌려준다(같은 객체). 테스트와 설정 스크립트가 쓴다.</summary>
        public UnlockTrackDefinition WithDrawCost(int cost)
        {
            drawCost = Mathf.Max(0, cost);
            return this;
        }

        /// <summary>항목을 받은 그대로(선행 항목 포함) 담은 트랙을 만든다.</summary>
        public static UnlockTrackDefinition Create(UnlockTrackCategory category, string displayName, IEnumerable<UnlockEntry> entries)
        {
            var track = CreateInstance<UnlockTrackDefinition>();
            track.category = category;
            track.displayName = displayName ?? string.Empty;
            if (entries != null)
                foreach (UnlockEntry entry in entries)
                    if (entry != null) track.entries.Add(entry);
            return track;
        }

        /// <summary>
        /// 일자형 트랙을 만든다. 각 항목의 선행을 바로 앞 항목 하나로 바꾼다(첫 항목은 선행 없음).
        /// </summary>
        public static UnlockTrackDefinition CreateStraight(UnlockTrackCategory category, string displayName, params UnlockEntry[] entries)
        {
            var chained = new List<UnlockEntry>();
            string previous = null;
            if (entries != null)
            {
                foreach (UnlockEntry entry in entries)
                {
                    if (entry == null) continue;
                    chained.Add(entry.WithPrerequisites(previous == null ? null : new[] { previous }));
                    previous = entry.Id;
                }
            }
            return Create(category, displayName, chained);
        }
    }
}
