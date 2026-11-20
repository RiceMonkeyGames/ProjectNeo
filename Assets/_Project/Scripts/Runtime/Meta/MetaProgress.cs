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
        /// 영웅·아이템·퍽 id 기준의 예전 해금 목록(가챠 시절). 영웅·아이템은 해금 트랙이 연결되면 트랙을 보고(META-07)
        /// 이 목록은 <see cref="UnlockTracks.ImportLegacyUnlocks"/>로 트랙 항목에 옮긴다. 퍽과 트랙 없는 설정은
        /// 계속 <see cref="IsUnlocked"/>로 이 목록을 본다.
        /// </summary>
        public List<string> unlockedIds = new List<string>();
        /// <summary>해금 트랙에서 연 항목 id(META-05). 처음부터 열린 항목은 넣지 않는다.</summary>
        public List<string> openedEntryIds = new List<string>();
        /// <summary>
        /// 새 런에서 고를 수 있는 가장 높은 승천 단계(FLOW-03, Q-78). 0이면 기본 게임만 있다. 예전 저장에는 없어 0으로 읽힌다.
        /// 규칙은 <see cref="AscensionProgress"/>.
        /// </summary>
        public int ascensionUnlocked;

        public bool IsUnlocked(IUnlockable unlockable)
        {
            if (unlockable == null) return false;
            return unlockable.UnlockedByDefault || unlockedIds.Contains(unlockable.Id);
        }
    }
}
