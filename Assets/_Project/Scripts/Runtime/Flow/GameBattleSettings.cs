using ProjectNeo.Siege;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Flow
{
    [CreateAssetMenu(menuName = "ProjectNeo/Flow/Game Battle")]
    public sealed class GameBattleSettings : ScriptableObject
    {
        [SerializeField] SiegeSettings siege;
        [Tooltip("스테이지 목록이 비어 있을 때 쓰는 단일 적 목록(전투 시작과 함께 모두 나온다).")]
        [SerializeField] SiegeUnitGroup[] enemies = new SiegeUnitGroup[0];
        [Tooltip("스테이지별 웨이브 편성(B-7.1). 비우면 위의 단일 적 목록을 쓴다.")]
        [SerializeField] ProjectNeo.Stages.StageCatalog stages;
        [Tooltip("나올 웨이브가 남아 있어도 공성 수단을 잃은 적이 후퇴할지(Q-52). 기본은 후퇴하지 않고 다음 웨이브와 합류한다.")]
        [SerializeField] bool retreatWhileWavesPending;
        [Tooltip("기본 스폰 칸. 스폰 지점을 지정하지 않은 무리와 단일 적 목록이 쓴다.")]
        [SerializeField] Vector2 spawnCell = new Vector2(16f, 36f);
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
        /// <summary>스킬 칸 1~3 키의 기본값(COMBAT-06, 사용자 결정 2026-11-20). 최종 배치는 Q-11.</summary>
        public static readonly string[] DefaultCastSkillBindings = { "<Keyboard>/z", "<Keyboard>/x", "<Keyboard>/c" };
        /// <summary>선택 분대 대장 영웅이 스킬을 쓰게 하는 보조 키의 기본값(COMBAT-06).</summary>
        public const string DefaultCaptainCastModifierBinding = "<Keyboard>/alt";
        [Tooltip("플레이어 스킬 칸 1~3 키(COMBAT-06). 보조 키와 함께 누르면 선택 분대 대장 영웅이 그 칸을 쓴다.")]
        [SerializeField] InputAction[] castSkill =
        {
            new InputAction("CastSkill1", InputActionType.Button, DefaultCastSkillBindings[0]),
            new InputAction("CastSkill2", InputActionType.Button, DefaultCastSkillBindings[1]),
            new InputAction("CastSkill3", InputActionType.Button, DefaultCastSkillBindings[2]),
        };
        [Tooltip("누른 채 스킬 키를 누르면 선택한 분대의 대장 영웅이 시전한다(COMBAT-06 임시 배치, Q-11).")]
        [SerializeField] InputAction captainCastModifier =
            new InputAction("CaptainCastModifier", InputActionType.Button, DefaultCaptainCastModifierBinding);
        [Tooltip("스킬 조준 광선 최대 거리(m).")]
        [SerializeField, Min(1f)] float skillAimDistance = 80f;
        [Tooltip("조준선 가까이 있는 유닛을 대상 유닛으로 잡는 반경(m).")]
        [SerializeField, Min(0f)] float skillUnitAimRadius = 0.75f;
        public System.Collections.Generic.IReadOnlyList<InputAction> CastSkill => castSkill;
        public InputAction CaptainCastModifier => captainCastModifier;
        public float SkillAimDistance => skillAimDistance;
        public float SkillUnitAimRadius => skillUnitAimRadius;
        [Tooltip("영웅 AI 자동 시전 판단 수치(COMBAT-06). 비우면 기본값을 쓴다.")]
        [SerializeField] ProjectNeo.Skills.SkillAutoSettings skillAuto;
        public ProjectNeo.Skills.SkillAutoSettings SkillAuto => skillAuto;
        /// <summary>자동 시전 수치를 바꾼다(에디터 설정·테스트용).</summary>
        public void ConfigureSkillAuto(ProjectNeo.Skills.SkillAutoSettings auto) => skillAuto = auto;
        public InputAction UseConsumable => useConsumable;
        public float ConsumableCooldown => consumableCooldown;
        public SiegeSettings Siege => siege;
        public ProjectNeo.Morale.MoraleSettings Morale => morale;
        public System.Collections.Generic.IReadOnlyList<SiegeUnitGroup> Enemies => enemies;
        [Tooltip("런 저장의 ID를 에셋으로 바꾸는 목록(FLOW-02). 비우면 런을 저장하지 않고 이어 하기도 되지 않는다.")]
        [SerializeField] ProjectNeo.Run.RunContentCatalog runContent;
        public ProjectNeo.Run.RunContentCatalog RunContent => runContent;
        public void ConfigureRunContent(ProjectNeo.Run.RunContentCatalog catalog) => runContent = catalog;
        public ProjectNeo.Stages.StageCatalog Stages => stages;
        public bool RetreatWhileWavesPending => retreatWhileWavesPending;
        /// <summary>스테이지 편성과 웨이브 중 후퇴 여부를 바꾼다(에디터 설정·테스트용).</summary>
        public void ConfigureStages(ProjectNeo.Stages.StageCatalog catalog, bool retreatWhilePending = false)
        { stages = catalog; retreatWhileWavesPending = retreatWhilePending; }
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
