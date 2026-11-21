using System;
using System.Collections.Generic;

namespace ProjectNeo.Meta
{
    /// <summary>
    /// 런 사이 해금 트랙 규칙(Q-76). 화면은 트랙을 골라 무작위로 연다(<see cref="TryDraw"/>, UI-06 사용자 결정 2026-11-21):
    /// 트랙의 고정 비용을 내고, 선행 항목이 모두 열린 미해금 항목 가운데 하나를 같은 확률로 연다. 중복도 환불도 없다.
    /// 예외로 업데이트에서 항목이 없어지면 쓴 포인트를 돌려준다(<see cref="Migrate"/>, META-10).
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
            // 업데이트로 이 항목이 없어지면 낸 트랙 비용을 돌려준다(META-10).
            SetSpent(progress, entry.Id, track.DrawCost);
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
            SetSpent(progress, entry.Id, entry.Cost);
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
                // 공짜로 옮긴 항목이라 없어져도 돌려줄 포인트가 없다(META-10).
                SetSpent(progress, entry.Id, 0);
                opened++;
            }
            return opened;
        }

        /// <summary>
        /// 업데이트로 바뀌거나 없어진 해금 항목을 고친다(META-10, 사용자 결정 2026-11-21). 연 항목 id를 이름 바꾸기 표로
        /// 새 id로 바꾸고, 그래도 목록에 없는 항목은 빼고 쓴 포인트를 돌려준다. 쓴 포인트 기록이 없는 예전 항목은
        /// 지금 비용으로 채운다(예전 가챠 해금에서 옮긴 항목은 0). 무엇을 했는지는 log에만 적는다.
        /// 고친 곳의 수를 돌려준다. 0이 아니면 메타를 저장해야 한다.
        /// </summary>
        public static int Migrate(MetaProgress progress, UnlockCatalog catalog, ProjectNeo.Core.ContentRenameTable renames,
            List<string> log = null)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            progress.openedEntryIds ??= new List<string>();
            progress.unlockSpent ??= new List<UnlockSpentRecord>();
            int changes = 0;

            if (renames != null)
            {
                for (int i = 0; i < progress.openedEntryIds.Count; i++)
                {
                    string renamed = renames.Resolve(ProjectNeo.Core.ContentKind.UnlockEntry, progress.openedEntryIds[i]);
                    if (renamed == progress.openedEntryIds[i]) continue;
                    log?.Add($"renamed unlock entry '{progress.openedEntryIds[i]}' -> '{renamed}'");
                    progress.openedEntryIds[i] = renamed;
                    changes++;
                }
                for (int i = 0; i < progress.unlockSpent.Count; i++)
                {
                    UnlockSpentRecord spent = progress.unlockSpent[i];
                    spent.entryId = renames.Resolve(ProjectNeo.Core.ContentKind.UnlockEntry, spent.entryId);
                    progress.unlockSpent[i] = spent;
                }
            }

            var seen = new HashSet<string>();
            for (int i = progress.openedEntryIds.Count - 1; i >= 0; i--)
            {
                string id = progress.openedEntryIds[i];
                if (catalog.FindEntry(id) != null && seen.Add(id)) continue;
                progress.openedEntryIds.RemoveAt(i);
                changes++;
                if (catalog.FindEntry(id) != null) { log?.Add($"unlock entry '{id}' was listed twice"); continue; }
                int points = SpentOn(progress, id);
                progress.unlockPoints += points;
                progress.unlockSpent.RemoveAll(record => record.entryId == id);
                log?.Add($"removed unlock entry '{id}'; refunded {points} unlock points");
            }
            progress.unlockSpent.RemoveAll(record => !progress.openedEntryIds.Contains(record.entryId));

            var legacy = new HashSet<string>(progress.unlockedIds ?? new List<string>());
            foreach (string id in progress.openedEntryIds)
            {
                if (progress.unlockSpent.Exists(record => record.entryId == id)) continue;
                UnlockEntry entry = catalog.FindEntry(id);
                bool imported = entry.Payload.IsContent && legacy.Contains(entry.Payload.TargetId);
                SetSpent(progress, id, imported ? 0 : entry.Cost);
                changes++;
            }
            return changes;
        }

        static int SpentOn(MetaProgress progress, string entryId)
        {
            int points = 0;
            foreach (UnlockSpentRecord record in progress.unlockSpent)
                if (record.entryId == entryId) points = Math.Max(points, record.points);
            return Math.Max(0, points);
        }

        static void SetSpent(MetaProgress progress, string entryId, int points)
        {
            progress.unlockSpent ??= new List<UnlockSpentRecord>();
            progress.unlockSpent.RemoveAll(record => record.entryId == entryId);
            progress.unlockSpent.Add(new UnlockSpentRecord(entryId, Math.Max(0, points)));
        }
    }
}
