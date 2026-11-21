using System;
using System.Collections.Generic;
using ProjectNeo.Core;

namespace ProjectNeo.Meta
{
    /// <summary>
    /// 런 사이 해금 화면의 규칙과 글자(META-06, UI-06). 화면 오브젝트를 모르는 순수 C#이다.
    /// 왼쪽에서 트랙을 고르고 "무작위 해금"을 한 번 누르면 트랙 비용을 내고 후보 하나가 바로 열린다(사용자 결정 2026-11-21).
    /// 확인을 한 번 더 묻지 않고, 환불은 없다.
    /// </summary>
    public sealed class UnlockScreenModel
    {
        public MetaProgress Progress { get; }
        public UnlockCatalog Catalog { get; }
        /// <summary>오른쪽에 보이는 트랙 번호. 트랙이 없으면 -1.</summary>
        public int SelectedTrackIndex { get; private set; }
        /// <summary>마지막 무작위 해금으로 열린 항목 id(카드 강조용). 없으면 빈 문자열.</summary>
        public string LastOpenedId { get; private set; } = string.Empty;
        /// <summary>마지막으로 누른 결과 안내. 없으면 빈 문자열.</summary>
        public string Notice { get; private set; } = string.Empty;

        public UnlockScreenModel(MetaProgress progress, UnlockCatalog catalog)
        {
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            SelectedTrackIndex = catalog.Tracks.Count > 0 ? 0 : -1;
        }

        public UnlockTrackDefinition SelectedTrack =>
            SelectedTrackIndex >= 0 && SelectedTrackIndex < Catalog.Tracks.Count ? Catalog.Tracks[SelectedTrackIndex] : null;

        public string PointsText => $"보유 해금 포인트  {Progress.unlockPoints}";

        /// <summary>트랙을 고른다. 없는 번호면 아무 일도 없다. 안내와 새 항목 강조는 지운다.</summary>
        public void SelectTrack(int index)
        {
            if (index < 0 || index >= Catalog.Tracks.Count || Catalog.Tracks[index] == null) return;
            SelectedTrackIndex = index;
            Notice = string.Empty;
            LastOpenedId = string.Empty;
        }

        /// <summary>고른 트랙에서 무작위로 하나를 연다. 열면 <see cref="UnlockClickResult.Opened"/>, 못 열면 Refused.</summary>
        public UnlockClickResult Draw(IRandomSource random)
        {
            UnlockTrackDefinition track = SelectedTrack;
            UnlockEntry opened = UnlockTracks.TryDraw(Progress, Catalog, track, random, out UnlockDrawCheck check);
            if (opened == null)
            {
                LastOpenedId = string.Empty;
                Notice = Reason(check, track);
                return UnlockClickResult.Refused;
            }
            LastOpenedId = opened.Id;
            Notice = $"{opened.DisplayName} 해금!";
            return UnlockClickResult.Opened;
        }

        /// <summary>저장 실패처럼 화면 밖에서 생긴 안내를 덧붙인다.</summary>
        public void AppendNotice(string text) => Notice = string.IsNullOrEmpty(Notice) ? text : Notice + " " + text;

        /// <summary>왼쪽 트랙 버튼 글자: 이름과 열린 수.</summary>
        public string TrackText(UnlockTrackDefinition track) =>
            track == null ? string.Empty : $"{track.DisplayName}\n<size=75%>{OpenedCount(track)}/{track.Entries.Count} 해금</size>";

        /// <summary>오른쪽 위 제목: 고른 트랙 이름과 열린 수.</summary>
        public string TrackTitleText
        {
            get
            {
                UnlockTrackDefinition track = SelectedTrack;
                return track == null ? string.Empty : $"{track.DisplayName}  <size=70%>{OpenedCount(track)}/{track.Entries.Count} 해금</size>";
            }
        }

        /// <summary>무작위 해금 버튼 글자. 더 열 것이 없으면 그렇게 적는다.</summary>
        public string DrawButtonText
        {
            get
            {
                UnlockTrackDefinition track = SelectedTrack;
                if (track == null) return string.Empty;
                return UnlockTracks.DrawCandidates(Progress, Catalog, track).Count == 0
                    ? "모두 해금함"
                    : $"무작위 해금  <size=75%>({track.DrawCost} 포인트)</size>";
            }
        }

        /// <summary>지금 무작위 해금을 할 수 있는지(버튼 활성화용).</summary>
        public bool CanDraw => UnlockTracks.CheckDraw(Progress, Catalog, SelectedTrack) == UnlockDrawCheck.Ok;

        public UnlockEntryState EntryState(UnlockEntry entry) => UnlockTracks.StateOf(Progress, Catalog, entry);

        /// <summary>항목 카드의 상태 줄.</summary>
        public string EntryStateText(UnlockEntry entry)
        {
            if (entry == null) return string.Empty;
            switch (EntryState(entry))
            {
                case UnlockEntryState.Opened: return "해금됨";
                case UnlockEntryState.Candidate: return "다음 후보";
                default:
                    List<string> missing = MissingPrerequisites(entry);
                    return missing.Count > 0 ? $"{string.Join(", ", missing)} 먼저" : "잠김";
            }
        }

        int OpenedCount(UnlockTrackDefinition track)
        {
            int opened = 0;
            foreach (UnlockEntry entry in track.Entries) if (UnlockTracks.IsOpened(Progress, entry)) opened++;
            return opened;
        }

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

        string Reason(UnlockDrawCheck check, UnlockTrackDefinition track) => check switch
        {
            UnlockDrawCheck.NothingLeft => $"{track.DisplayName}에서 더 해금할 것이 없습니다.",
            UnlockDrawCheck.NotEnoughPoints => $"해금 포인트가 부족합니다: {track.DrawCost} 필요, {Progress.unlockPoints} 보유.",
            _ => "고른 분류가 없습니다.",
        };
    }
}
