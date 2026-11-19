using System;
using System.Collections.Generic;

namespace ProjectNeo.Meta
{
    /// <summary>
    /// 런 사이 해금 트랙 규칙(Q-76, Darkest Dungeon 2 방식). 랜덤도 중복도 없다.
    /// 선행 항목이 모두 열렸고 해금 포인트가 충분하면 고른 항목을 연다. 되돌리기(환불)는 없다.
    /// </summary>
    public static class UnlockTracks
    {
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
