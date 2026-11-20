using UnityEngine;

namespace ProjectNeo.Stages
{
    /// <summary>
    /// 적이 나오는 지점 하나. 씬 표식이 아니라 건설 격자의 칸 좌표로 정한다.
    /// 지점을 늘리려면 이 에셋을 하나 더 만들면 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Stages/Spawn Point")]
    public sealed class SpawnPointDefinition : ScriptableObject
    {
        [SerializeField] string displayName = "출현 지점";
        [Tooltip("건설 격자의 칸 좌표(x, z). 무리의 맨 앞줄 가운데가 이 칸에 선다.")]
        [SerializeField] Vector2 cell = new Vector2(40f, 90f);

        public string DisplayName => displayName;
        public Vector2 Cell => cell;

        public static SpawnPointDefinition Create(string displayName, Vector2 cell)
        {
            var point = CreateInstance<SpawnPointDefinition>();
            point.displayName = displayName; point.cell = cell;
            return point;
        }
    }
}
