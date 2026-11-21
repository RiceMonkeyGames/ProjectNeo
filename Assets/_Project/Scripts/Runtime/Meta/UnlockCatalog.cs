using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Meta
{
    /// <summary>모든 해금 트랙의 목록. 항목 id는 트랙을 통틀어 하나뿐이어야 한다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Unlock Catalog", fileName = "UnlockCatalog")]
    public sealed class UnlockCatalog : ScriptableObject
    {
        [SerializeField] List<UnlockTrackDefinition> tracks = new List<UnlockTrackDefinition>();
        [Tooltip("업데이트에서 바뀐 콘텐츠 ID 표(META-10). 연 해금 항목 id를 새 id로 바꾼다. 런 콘텐츠 목록과 같은 에셋을 쓴다.")]
        [SerializeField] ProjectNeo.Core.ContentRenameTable renames;

        public IReadOnlyList<UnlockTrackDefinition> Tracks => tracks;
        /// <summary>콘텐츠 ID 이름 바꾸기 표(META-10). 없으면 이름을 바꾸지 않는다.</summary>
        public ProjectNeo.Core.ContentRenameTable Renames => renames;
        public void SetRenames(ProjectNeo.Core.ContentRenameTable table) => renames = table;

        public static UnlockCatalog Create(params UnlockTrackDefinition[] tracks)
        {
            var catalog = CreateInstance<UnlockCatalog>();
            if (tracks != null)
                foreach (UnlockTrackDefinition track in tracks)
                    if (track != null) catalog.tracks.Add(track);
            return catalog;
        }

        public UnlockTrackDefinition FindTrack(UnlockTrackCategory category)
        {
            foreach (UnlockTrackDefinition track in tracks)
                if (track != null && track.Category == category) return track;
            return null;
        }

        /// <summary>id로 항목을 찾는다. 없으면 null.</summary>
        public UnlockEntry FindEntry(string entryId)
        {
            if (string.IsNullOrEmpty(entryId)) return null;
            foreach (UnlockEntry entry in AllEntries())
                if (entry.Id == entryId) return entry;
            return null;
        }

        public IEnumerable<UnlockEntry> AllEntries()
        {
            foreach (UnlockTrackDefinition track in tracks)
            {
                if (track == null) continue;
                foreach (UnlockEntry entry in track.Entries)
                    if (entry != null) yield return entry;
            }
        }

        /// <summary>
        /// 데이터 실수를 찾는다: 빈 id, 겹치는 id, 같은 종류의 트랙 두 개, 없는 선행 항목, 자기 자신을 선행으로 둔 항목.
        /// 문제가 없으면 빈 목록.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var ids = new HashSet<string>();
            var categories = new HashSet<UnlockTrackCategory>();
            foreach (UnlockTrackDefinition track in tracks)
            {
                if (track == null) { problems.Add("Empty track slot."); continue; }
                if (!categories.Add(track.Category)) problems.Add($"Duplicate track category '{track.Category}'.");
            }
            foreach (UnlockEntry entry in AllEntries())
            {
                if (string.IsNullOrEmpty(entry.Id)) problems.Add("Entry without id.");
                else if (!ids.Add(entry.Id)) problems.Add($"Duplicate entry id '{entry.Id}'.");
            }
            foreach (UnlockEntry entry in AllEntries())
            {
                foreach (string prerequisite in entry.PrerequisiteIds)
                {
                    if (prerequisite == entry.Id) problems.Add($"Entry '{entry.Id}' requires itself.");
                    else if (!ids.Contains(prerequisite)) problems.Add($"Entry '{entry.Id}' requires unknown entry '{prerequisite}'.");
                }
            }
            return problems;
        }
    }
}
