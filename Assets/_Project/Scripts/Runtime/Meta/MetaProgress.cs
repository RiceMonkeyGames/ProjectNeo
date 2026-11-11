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
        public int unlockPoints;
        public List<string> unlockedIds = new List<string>();
        /// <summary>새로 해금해서 다음 런에 확정으로 등장시킬 것들의 id.</summary>
        public List<string> guaranteedNextRunIds = new List<string>();

        public bool IsUnlocked(IUnlockable unlockable)
        {
            if (unlockable == null) return false;
            return unlockable.UnlockedByDefault || unlockedIds.Contains(unlockable.Id);
        }
    }
}
