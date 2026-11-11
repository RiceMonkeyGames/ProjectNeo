using System;
using System.Collections.Generic;
using ProjectNeo.Core;

namespace ProjectNeo.Meta
{
    /// <summary>해금 포인트를 써서 한 종류 안에서 랜덤으로 하나를 해금한다.</summary>
    public static class UnlockGacha
    {
        /// <summary>이 종류에서 아직 해금 안 된 후보 수.</summary>
        public static int CountRemaining(MetaProgress progress, UnlockCategory category, IEnumerable<IUnlockable> candidates)
        {
            return GetLocked(progress, category, candidates).Count;
        }

        /// <summary>
        /// 해금에 성공하면 포인트를 차감하고 해금한 것을 돌려준다. 다음 런 확정 등장 목록에도 넣는다.
        /// 포인트가 모자라거나 남은 후보가 없으면 아무것도 바꾸지 않고 null을 돌려준다.
        /// </summary>
        public static IUnlockable TryRoll(
            MetaProgress progress,
            MetaConfig config,
            UnlockCategory category,
            IEnumerable<IUnlockable> candidates,
            IRandomSource random)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (random == null) throw new ArgumentNullException(nameof(random));

            int cost = config.GetUnlockCost(category);
            if (progress.unlockPoints < cost) return null;

            List<IUnlockable> locked = GetLocked(progress, category, candidates);
            if (locked.Count == 0) return null;

            IUnlockable picked = locked[random.NextInt(locked.Count)];
            progress.unlockPoints -= cost;
            progress.unlockedIds.Add(picked.Id);
            progress.guaranteedNextRunIds.Add(picked.Id);
            return picked;
        }

        static List<IUnlockable> GetLocked(MetaProgress progress, UnlockCategory category, IEnumerable<IUnlockable> candidates)
        {
            var locked = new List<IUnlockable>();
            if (candidates == null) return locked;
            foreach (IUnlockable candidate in candidates)
            {
                if (candidate == null || candidate.UnlockCategory != category) continue;
                if (progress.IsUnlocked(candidate)) continue;
                if (locked.Exists(l => l.Id == candidate.Id)) continue;
                locked.Add(candidate);
            }
            return locked;
        }
    }
}
