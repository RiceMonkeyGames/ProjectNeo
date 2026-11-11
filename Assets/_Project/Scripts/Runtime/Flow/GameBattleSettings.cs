using ProjectNeo.Siege;
using UnityEngine;

namespace ProjectNeo.Flow
{
    [CreateAssetMenu(menuName = "ProjectNeo/Flow/Game Battle")]
    public sealed class GameBattleSettings : ScriptableObject
    {
        [SerializeField] SiegeSettings siege;
        [SerializeField] SiegeUnitGroup[] enemies = new SiegeUnitGroup[0];
        [SerializeField] Vector2 spawnCell = new Vector2(40f, 90f);
        [SerializeField] Vector2Int outward = Vector2Int.up;
        [SerializeField, Min(1)] int columns = 10;
        [SerializeField, Min(0.1f)] float spacing = 2.5f;
        [SerializeField, Min(0.1f)] float rowSpacing = 2.5f;
        [SerializeField, Min(1f)] float retreatExitMargin = 5f;
        [SerializeField, Min(0.1f)] float retreatArrivalDistance = 2f;
        public SiegeSettings Siege => siege;
        public System.Collections.Generic.IReadOnlyList<SiegeUnitGroup> Enemies => enemies;
        public Vector2 SpawnCell => spawnCell;
        public Vector2Int Outward => outward;
        public int Columns => columns;
        public float Spacing => spacing;
        public float RowSpacing => rowSpacing;
        public float RetreatExitMargin => retreatExitMargin;
        public float RetreatArrivalDistance => retreatArrivalDistance;
        public static GameBattleSettings Create(SiegeSettings siege, SiegeUnitGroup[] enemies)
        {
            var settings = CreateInstance<GameBattleSettings>();
            settings.siege = siege; settings.enemies = enemies ?? new SiegeUnitGroup[0];
            return settings;
        }
    }
}
