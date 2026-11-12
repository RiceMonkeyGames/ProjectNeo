using System;
using ProjectNeo.Economy;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 적 한 종류를 처치했을 때의 보상. <see cref="enemyId"/>는 전투 결과의 처치 수 키와 같다(지금은 적 프리팹 이름).
    /// 기획의 "그 스테이지에서 잡은 몬스터의 재료"를 데이터로 정한다.
    /// </summary>
    [Serializable]
    public struct KillReward
    {
        public string enemyId;
        public int goldPerKill;
        public MaterialDefinition material;
        public int materialPerKill;

        public KillReward(string enemyId, int goldPerKill, MaterialDefinition material = null, int materialPerKill = 0)
        {
            this.enemyId = enemyId;
            this.goldPerKill = goldPerKill;
            this.material = material;
            this.materialPerKill = materialPerKill;
        }
    }
}
