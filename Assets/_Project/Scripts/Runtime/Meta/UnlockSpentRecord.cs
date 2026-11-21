using System;

namespace ProjectNeo.Meta
{
    /// <summary>
    /// 해금 항목 하나를 열 때 쓴 포인트(META-10). 업데이트로 그 항목이 없어지면 비용을 읽을 수 없으므로
    /// 메타 저장에 적어 두고 돌려준다. 예전(가챠) 해금에서 옮긴 항목은 공짜로 열려 0이다.
    /// </summary>
    [Serializable]
    public struct UnlockSpentRecord
    {
        public string entryId;
        public int points;

        public UnlockSpentRecord(string entryId, int points)
        {
            this.entryId = entryId;
            this.points = points;
        }
    }
}
