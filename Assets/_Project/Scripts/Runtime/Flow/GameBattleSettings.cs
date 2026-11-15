using ProjectNeo.Siege;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [Tooltip("아군 부대 사기(B-8.2). 비우면 Game 씬에서 사기를 쓰지 않는다.")]
        [SerializeField] ProjectNeo.Morale.MoraleSettings morale;
        /// <summary>전투 소모품 사용 키의 기본값(PREP-04).</summary>
        public const string DefaultUseConsumableBinding = "<Keyboard>/q";
        [Tooltip("전투 중 소모품(회복약) 사용 키(PREP-04).")]
        [SerializeField] InputAction useConsumable = new InputAction("UseConsumable", InputActionType.Button, DefaultUseConsumableBinding);
        [Tooltip("소모품을 쓴 뒤 다시 쓸 수 있을 때까지의 시간(초, PREP-04 임시값).")]
        [SerializeField, Min(0f)] float consumableCooldown = 10f;
        public InputAction UseConsumable => useConsumable;
        public float ConsumableCooldown => consumableCooldown;
        public SiegeSettings Siege => siege;
        public ProjectNeo.Morale.MoraleSettings Morale => morale;
        public System.Collections.Generic.IReadOnlyList<SiegeUnitGroup> Enemies => enemies;
        public Vector2 SpawnCell => spawnCell;
        public Vector2Int Outward => outward;
        public int Columns => columns;
        public float Spacing => spacing;
        public float RowSpacing => rowSpacing;
        public float RetreatExitMargin => retreatExitMargin;
        public float RetreatArrivalDistance => retreatArrivalDistance;
        public static GameBattleSettings Create(SiegeSettings siege, SiegeUnitGroup[] enemies,
            ProjectNeo.Morale.MoraleSettings morale = null, float consumableCooldown = 10f)
        {
            var settings = CreateInstance<GameBattleSettings>();
            settings.siege = siege; settings.enemies = enemies ?? new SiegeUnitGroup[0]; settings.morale = morale;
            settings.consumableCooldown = Mathf.Max(0f, consumableCooldown);
            return settings;
        }
    }
}
