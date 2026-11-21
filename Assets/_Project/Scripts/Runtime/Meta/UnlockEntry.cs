using System;
using System.Collections.Generic;
using ProjectNeo.Core;
using UnityEngine;

namespace ProjectNeo.Meta
{
    /// <summary>
    /// 해금 트랙의 항목 하나(Q-76). 자기 id, 비용, 선행 항목 id 목록, 여는 것(payload)을 가진다.
    /// 일자형 트랙은 바로 앞 항목 하나를 선행으로 두고, 갈래 트리는 선행을 여러 개 둘 수 있다.
    /// </summary>
    [Serializable]
    public sealed class UnlockEntry
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField, Min(0)] int cost;
        [SerializeField] List<string> prerequisiteIds = new List<string>();
        [Tooltip("true면 사지 않아도 처음부터 열려 있다. 예전 '처음부터 해금' 플래그를 옮긴 것.")]
        [SerializeField] bool openedByDefault;
        [SerializeField] UnlockPayload payload;
        [SerializeField, Tooltip("UI-06: thumbnail on the unlock screen card. Empty shows the blank thumbnail frame.")]
        Sprite icon;

        public string Id => id ?? string.Empty;
        /// <summary>해금 화면 카드의 썸네일(UI-06). 없으면 null(빈 칸만 보인다).</summary>
        public Sprite Icon => icon;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? Id : displayName;
        public int Cost => cost;
        public IReadOnlyList<string> PrerequisiteIds => prerequisiteIds ??= new List<string>();
        public bool OpenedByDefault => openedByDefault;
        public UnlockPayload Payload => payload;

        public static UnlockEntry Create(
            string id,
            int cost,
            UnlockPayload payload,
            bool openedByDefault = false,
            IEnumerable<string> prerequisiteIds = null,
            string displayName = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Unlock entry id is required.", nameof(id));
            var entry = new UnlockEntry
            {
                id = id,
                displayName = displayName ?? string.Empty,
                cost = Mathf.Max(cost, 0),
                openedByDefault = openedByDefault,
                payload = payload,
            };
            if (prerequisiteIds != null)
                foreach (string prerequisite in prerequisiteIds)
                    if (!string.IsNullOrEmpty(prerequisite)) entry.prerequisiteIds.Add(prerequisite);
            return entry;
        }

        /// <summary>
        /// 기존 영웅·아이템 하나를 여는 항목을 만든다. 그 데이터의 '처음부터 해금' 플래그가 켜져 있으면
        /// 항목도 처음부터 열린다(Q-76).
        /// </summary>
        public static UnlockEntry ForContent(
            string id,
            int cost,
            UnlockPayloadKind kind,
            IUnlockable target,
            IEnumerable<string> prerequisiteIds = null,
            string displayName = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var payload = new UnlockPayload(kind, target.Id, 1);
            if (!payload.IsContent) throw new ArgumentException("Content entries need a hero, recipe or shop item payload.", nameof(kind));
            return Create(id, cost, payload, target.UnlockedByDefault, prerequisiteIds, displayName);
        }

        /// <summary>선행 항목만 바꾼 복사본.</summary>
        public UnlockEntry WithPrerequisites(IEnumerable<string> prerequisites)
        {
            UnlockEntry copy = Create(Id, cost, payload, openedByDefault, prerequisites, displayName);
            copy.icon = icon;
            return copy;
        }

        /// <summary>썸네일을 바꾼 항목을 돌려준다(같은 객체). 설정 스크립트와 테스트가 쓴다.</summary>
        public UnlockEntry WithIcon(Sprite thumbnail)
        {
            icon = thumbnail;
            return this;
        }
    }
}
