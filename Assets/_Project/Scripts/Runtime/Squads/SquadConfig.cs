using UnityEngine;

namespace ProjectNeo.Squads
{
    /// <summary>부대 수와 부대당 인원 상한. 값은 에셋에서 바꾼다.</summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Squad Config", fileName = "SquadConfig")]
    public sealed class SquadConfig : ScriptableObject
    {
        [SerializeField, Min(1)] int maxSquads = 4;
        [SerializeField, Min(1)] int maxMembersPerSquad = 10;

        public int MaxSquads => maxSquads;
        public int MaxMembersPerSquad => maxMembersPerSquad;

        /// <summary>코드에서 설정을 만든다(테스트용).</summary>
        public static SquadConfig Create(int maxSquads, int maxMembersPerSquad)
        {
            var config = CreateInstance<SquadConfig>();
            config.maxSquads = maxSquads;
            config.maxMembersPerSquad = maxMembersPerSquad;
            return config;
        }
    }
}
