using System;
using System.Collections.Generic;

namespace ProjectNeo.Meta
{
    /// <summary>
    /// 런 사이 해금 화면의 규칙과 글자(META-06). 화면 오브젝트를 모르는 순수 C#이다.
    /// 항목을 처음 누르면 확인을 기다리고, 같은 항목을 다시 누르면 산다(사용자 결정 2026-11-19). 환불은 없다.
    /// </summary>
    public sealed class UnlockScreenModel
    {
        public MetaProgress Progress { get; }
        public UnlockCatalog Catalog { get; }
        /// <summary>확인을 기다리는 항목 id. 없으면 빈 문자열.</summary>
        public string ArmedEntryId { get; private set; } = string.Empty;
        /// <summary>마지막으로 누른 결과 안내. 없으면 빈 문자열.</summary>
        public string Notice { get; private set; } = string.Empty;

        public UnlockScreenModel(MetaProgress progress, UnlockCatalog catalog)
        {
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public string PointsText => $"Unlock points: {Progress.unlockPoints}";

        public UnlockClickResult Click(string entryId)
        {
            UnlockEntry entry = Catalog.FindEntry(entryId);
            UnlockOpenCheck check = UnlockTracks.Check(Progress, Catalog, entryId);
            if (check != UnlockOpenCheck.Ok)
            {
                ArmedEntryId = string.Empty;
                Notice = Reason(check, entry);
                return UnlockClickResult.Refused;
            }
            if (ArmedEntryId != entry.Id)
            {
                ArmedEntryId = entry.Id;
                Notice = string.Empty;
                return UnlockClickResult.Armed;
            }
            ArmedEntryId = string.Empty;
            UnlockTracks.TryOpen(Progress, Catalog, entry.Id);
            Notice = $"Unlocked {entry.DisplayName}.";
            return UnlockClickResult.Opened;
        }

        /// <summary>확인 대기를 푼다(창을 닫거나 다른 곳을 눌렀을 때).</summary>
        public void Disarm() => ArmedEntryId = string.Empty;

        /// <summary>저장 실패처럼 화면 밖에서 생긴 안내를 덧붙인다.</summary>
        public void AppendNotice(string text) => Notice = string.IsNullOrEmpty(Notice) ? text : Notice + " " + text;

        /// <summary>항목 버튼 글자.</summary>
        public string EntryText(UnlockEntry entry)
        {
            if (entry == null) return string.Empty;
            if (UnlockTracks.IsOpened(Progress, entry)) return $"{entry.DisplayName} - Opened";
            if (entry.Id == ArmedEntryId) return $"Click again: {entry.DisplayName} ({entry.Cost})";
            List<string> missing = MissingPrerequisites(entry);
            return missing.Count > 0
                ? $"{entry.DisplayName} - {entry.Cost} (needs {string.Join(", ", missing)})"
                : $"{entry.DisplayName} - {entry.Cost}";
        }

        /// <summary>항목을 지금 살 수 있는지(버튼 강조용). 이미 열렸으면 false.</summary>
        public bool CanOpen(UnlockEntry entry) => entry != null && UnlockTracks.Check(Progress, Catalog, entry.Id) == UnlockOpenCheck.Ok;

        List<string> MissingPrerequisites(UnlockEntry entry)
        {
            var missing = new List<string>();
            foreach (string id in entry.PrerequisiteIds)
            {
                UnlockEntry prerequisite = Catalog.FindEntry(id);
                if (!UnlockTracks.IsOpened(Progress, prerequisite)) missing.Add(prerequisite != null ? prerequisite.DisplayName : id);
            }
            return missing;
        }

        string Reason(UnlockOpenCheck check, UnlockEntry entry) => check switch
        {
            UnlockOpenCheck.AlreadyOpened => $"{entry.DisplayName} is already unlocked.",
            UnlockOpenCheck.MissingPrerequisite => $"Unlock {string.Join(", ", MissingPrerequisites(entry))} first.",
            UnlockOpenCheck.NotEnoughPoints => $"Not enough unlock points: {entry.Cost} needed, {Progress.unlockPoints} held.",
            _ => "This entry does not exist.",
        };
    }
}
