using System;
using System.Collections.Generic;

namespace ProjectNeo.Meta
{
    /// <summary>
    /// 런 사이 해금 트랙 규칙(Q-76). 화면은 트랙을 골라 무작위로 연다(<see cref="TryDraw"/>, UI-06 사용자 결정 2026-11-21):
    /// 트랙의 고정 비용을 내고, 선행 항목이 모두 열린 미해금 항목 가운데 하나를 같은 확률로 연다. 중복도 환불도 없다.
    /// 항목을 직접 여는 <see cref="TryOpen"/>은 항목 비용을 쓰며 옛 저장 옮기기와 테스트용으로 남아 있다.
    /// </summary>
    public static class UnlockTracks
    {
        /// <summary>트랙 화면에서 항목이 보이는 상태.</summary>
        public static UnlockEntryState StateOf(MetaProgress progress, UnlockCatalog catalog, UnlockEntry entry)
        {
            if (IsOpened(progress, entry)) return UnlockEntryState.Opened;
            return PrerequisitesOpened(progress, catalog, entry) ? UnlockEntryState.Candidate : UnlockEntryState.Locked;
        }

        /// <summary>다음 무작위 해금에서 나올 수 있는 항목들(미해금이고 선행이 모두 열림), 트랙 순서대로.</summary>
        public static List<UnlockEntry> DrawCandidates(MetaProgress progress, UnlockCatalog catalog, UnlockTrackDefinition track)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var candidates = new List<UnlockEntry>();
            if (track == null) return candidates;
            foreach (UnlockEntry entry in track.Entries)
                if (entry != null && StateOf(progress, catalog, entry) == UnlockEntryState.Candidate) candidates.Add(entry);
            return candidates;
        }

        /// <summary>이 트랙에서 지금 무작위 해금을 할 수 있는지. 아무것도 바꾸지 않는다.</summary>
        public static UnlockDrawCheck CheckDraw(MetaProgress progress, UnlockCatalog catalog, UnlockTrackDefinition track)
        {
            if (track == null) return UnlockDrawCheck.UnknownTrack;
            if (DrawCandidates(progress, catalog, track).Count == 0) return UnlockDrawCheck.NothingLeft;
            if (progress.unlockPoints < track.DrawCost) return UnlockDrawCheck.NotEnoughPoints;
            return UnlockDrawCheck.Ok;
        }

        /// <summary>
        /// 할 수 있으면 트랙 비용을 내고 후보 하나를 무작위로 열어 돌려준다. 할 수 없으면 아무것도 바꾸지 않고 null.
        /// </summary>
        public static UnlockEntry TryDraw(MetaProgress progress, UnlockCatalog catalog, UnlockTrackDefinition track,
            Core.IRandomSource random, out UnlockDrawCheck check)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            check = CheckDraw(progress, catalog, track);
            if (check != UnlockDrawCheck.Ok) return null;

            List<UnlockEntry> candidates = DrawCandidates(progress, catalog, track);
            UnlockEntry entry = candidates[Math.Min(random.NextInt(candidates.Count), candidates.Count - 1)];
            progress.unlockPoints -= track.DrawCost;
            progress.openedEntryIds.Add(entry.Id);
            return entry;
        }

        static bool PrerequisitesOpened(MetaProgress progress, UnlockCatalog catalog, UnlockEntry entry)
        {
            foreach (string prerequisite in entry.PrerequisiteIds)
                if (!IsOpened(progress, catalog.FindEntry(prerequisite))) return false;
            return true;
        }

        public static bool IsOpened(MetaProgress progress, UnlockEntry entry)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (entry == null) return false;
            return entry.OpenedByDefault || progress.openedEntryIds.Contains(entry.Id);
        }

        /// <summary>이 항목을 지금 열 수 있는지. 아무것도 바꾸지 않는다.</summary>
        public static UnlockOpenCheck Check(MetaProgress progress, UnlockCatalog catalog, string entryId)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            UnlockEntry entry = catalog.FindEntry(entryId);
            if (entry == null) return UnlockOpenCheck.UnknownEntry;
            if (IsOpened(progress, entry)) return UnlockOpenCheck.AlreadyOpened;
            foreach (string prerequisite in entry.PrerequisiteIds)
                if (!IsOpened(progress, catalog.FindEntry(prerequisite))) return UnlockOpenCheck.MissingPrerequisite;
            if (progress.unlockPoints < entry.Cost) return UnlockOpenCheck.NotEnoughPoints;
            return UnlockOpenCheck.Ok;
        }

        /// <summary>열 수 있으면 비용을 내고 연다. 열 수 없으면 아무것도 바꾸지 않고 이유를 돌려준다.</summary>
        public static UnlockOpenCheck TryOpen(MetaProgress progress, UnlockCatalog catalog, string entryId)
        {
            UnlockOpenCheck check = Check(progress, catalog, entryId);
            if (check != UnlockOpenCheck.Ok) return check;

            UnlockEntry entry = catalog.FindEntry(entryId);
            progress.unlockPoints -= entry.Cost;
            progress.openedEntryIds.Add(entry.Id);
            return UnlockOpenCheck.Ok;
        }

        /// <summary>
        /// 이 영웅·레시피·상점 아이템을 이번 런에서 쓸 수 있는지(META-07). 그 대상을 여는 항목이 하나도 없으면 쓸 수 있고
        /// (트랙 밖 콘텐츠, 사용자 결정 2026-11-19), 있으면 그중 하나라도 열려 있어야 한다.
        /// 시설 레벨에 따른 등장 조건은 따로다.
        /// </summary>
        public static bool IsContentAvailable(MetaProgress progress, UnlockCatalog catalog, UnlockPayloadKind kind, string targetId)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            bool onTrack = false;
            foreach (UnlockEntry entry in catalog.AllEntries())
            {
                if (entry.Payload.Kind != kind || entry.Payload.TargetId != targetId) continue;
                if (IsOpened(progress, entry)) return true;
                onTrack = true;
            }
            return !onTrack;
        }

        /// <summary>열린 시작 보너스 항목(골드·일반병·아이템)을 모두 더한다.</summary>
        public static UnlockStartingBonus StartingBonus(MetaProgress progress, UnlockCatalog catalog)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var bonus = new UnlockStartingBonus();
            foreach (UnlockEntry entry in catalog.AllEntries())
                if (IsOpened(progress, entry)) bonus.Add(entry.Payload);
            return bonus;
        }

        /// <summary>
        /// 예전(가챠 시절) 해금 목록 <see cref="MetaProgress.unlockedIds"/>에 든 영웅·아이템을 가리키는 항목을
        /// 비용과 선행 조건 없이 연다. 이미 해금했던 것을 잃지 않게 하려는 것이다. 새로 연 항목 수를 돌려준다.
        /// </summary>
        public static int ImportLegacyUnlocks(MetaProgress progress, UnlockCatalog catalog)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var legacy = new HashSet<string>(progress.unlockedIds);
            int opened = 0;
            foreach (UnlockEntry entry in catalog.AllEntries())
            {
                if (!entry.Payload.IsContent || !legacy.Contains(entry.Payload.TargetId)) continue;
                if (IsOpened(progress, entry)) continue;
                progress.openedEntryIds.Add(entry.Id);
                opened++;
            }
            return opened;
        }
    }
}
