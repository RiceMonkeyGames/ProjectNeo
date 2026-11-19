using System;
using System.Collections.Generic;
using ProjectNeo.Core;

namespace ProjectNeo.Meta
{
    /// <summary>
    /// 런이 끝나도(죽어도) 유지되는 데이터. 저장 파일에 들어가는 내용이 이것이다.
    /// 찍은 퍽 내역은 여기에 없다. 런마다 초기화되기 때문이다.
    /// </summary>
    [Serializable]
    public sealed class MetaProgress
    {
        public int commanderLevel = 1;
        /// <summary>현재 레벨에서 쌓은 경험치.</summary>
        public int commanderXp;
        /// <summary>지금까지 얻은 퍽 포인트 총개수.</summary>
        public int perkPoints;
        /// <summary>해금 트랙에 쓰는 재화. 런에서 져도 남는다.</summary>
        public int unlockPoints;
        /// <summary>
        /// 영웅·아이템·퍽 id 기준의 예전 해금 목록(가챠 시절). 런 콘텐츠가 해금 트랙을 읽게 되기 전까지(META-07)
        /// <see cref="IsUnlocked"/>가 계속 쓴다. 트랙으로 옮길 때는 <see cref="UnlockTracks.ImportLegacyUnlocks"/>.
        /// </summary>
        public List<string> unlockedIds = new List<string>();
        /// <summary>해금 트랙에서 연 항목 id(META-05). 처음부터 열린 항목은 넣지 않는다.</summary>
        public List<string> openedEntryIds = new List<string>();

        public bool IsUnlocked(IUnlockable unlockable)
        {
            if (unlockable == null) return false;
            return unlockable.UnlockedByDefault || unlockedIds.Contains(unlockable.Id);
        }
    }
}
