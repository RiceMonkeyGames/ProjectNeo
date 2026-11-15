using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 대포 한 종류의 사격 수치(Q-21 임시값). 플레이 테스트 뒤 에셋에서 조정한다.
    /// 대포 체력은 무기 공통 정의(SiegeWeaponDefinition)에 둔다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Siege/Cannon Settings")]
    public sealed class CannonSettings : ScriptableObject
    {
        [SerializeField, Min(0f)] float minimumRange = 6f;
        [SerializeField, Min(0.1f)] float maximumRange = 60f;
        [SerializeField, Range(0f, 180f)] float aimHalfAngle = 45f;
        [SerializeField, Range(0f, 180f)] float manualAimHalfAngle = 90f;
        [SerializeField, Min(1f)] float swivelSpeed = 180f;
        [SerializeField, Min(0.1f)] float blastRadius = 3f;
        [SerializeField, Min(0f)] float centerDamage = 40f;
        [SerializeField, Range(0f, 1f)] float edgeDamageRatio = 0.5f;
        [SerializeField, Min(0f)] float minimumDamage = 1f;
        [SerializeField, Min(0.1f)] float reloadSeconds = 6f;
        [SerializeField, Min(0f)] float autoScatterRadius = 2f;
        [SerializeField, Min(0.1f)] float shellSpeed = 30f;
        [SerializeField, Min(0.1f)] float shellGravity = 9.81f;
        [SerializeField, Min(0.1f)] float shellLifetime = 10f;
        [SerializeField, Min(0.01f)] float targetSearchInterval = 0.25f;
        [SerializeField] bool friendlyFire;

        /// <summary>수평 거리 기준 최소 사거리. 성벽 바로 아래는 맞히지 못한다.</summary>
        public float MinimumRange => minimumRange;
        public float MaximumRange => Mathf.Max(minimumRange, maximumRange);
        /// <summary>설치 방향 기준 좌우 회전 한계(도).</summary>
        public float AimHalfAngle => aimHalfAngle;
        /// <summary>사람이 탔을 때의 좌우 회전 한계(도). 자동 사격 한계보다 좁아지지 않는다(SIEGE-07).</summary>
        public float ManualAimHalfAngle => Mathf.Max(aimHalfAngle, manualAimHalfAngle);
        /// <summary>포신이 조준 방향을 따라 도는 속도(도/초). 쏠 수 있는지는 바꾸지 않는 외형 속도다.</summary>
        public float SwivelSpeed => swivelSpeed;
        public float BlastRadius => blastRadius;
        public float CenterDamage => centerDamage;
        /// <summary>폭발 가장자리에서 중심 피해에 곱하는 비율.</summary>
        public float EdgeDamageRatio => edgeDamageRatio;
        public float MinimumDamage => minimumDamage;
        public float ReloadSeconds => reloadSeconds;
        /// <summary>자동 사격 탄착점이 흩어지는 최대 반경. 직접 조작에는 적용하지 않는다.</summary>
        public float AutoScatterRadius => autoScatterRadius;
        public float ShellSpeed => shellSpeed;
        public float ShellGravity => shellGravity;
        public float ShellLifetime => shellLifetime;
        public float TargetSearchInterval => targetSearchInterval;
        /// <summary>켜면 아군과 우리 성벽도 폭발 피해를 받는다. 현재 기본값은 꺼짐.</summary>
        public bool FriendlyFire => friendlyFire;

        public static CannonSettings Create(float minimumRange = 6f, float maximumRange = 60f, float aimHalfAngle = 45f,
            float blastRadius = 3f, float centerDamage = 40f, float edgeDamageRatio = 0.5f, float reloadSeconds = 6f,
            float autoScatterRadius = 2f, float shellSpeed = 30f, float shellGravity = 9.81f, bool friendlyFire = false,
            float manualAimHalfAngle = 90f, float swivelSpeed = 180f)
        {
            var value = CreateInstance<CannonSettings>();
            value.minimumRange = Mathf.Max(0f, minimumRange);
            value.maximumRange = Mathf.Max(0.1f, maximumRange);
            value.aimHalfAngle = Mathf.Clamp(aimHalfAngle, 0f, 180f);
            value.blastRadius = Mathf.Max(0.1f, blastRadius);
            value.centerDamage = Mathf.Max(0f, centerDamage);
            value.edgeDamageRatio = Mathf.Clamp01(edgeDamageRatio);
            value.reloadSeconds = Mathf.Max(0.1f, reloadSeconds);
            value.autoScatterRadius = Mathf.Max(0f, autoScatterRadius);
            value.shellSpeed = Mathf.Max(0.1f, shellSpeed);
            value.shellGravity = Mathf.Max(0.1f, shellGravity);
            value.friendlyFire = friendlyFire;
            value.manualAimHalfAngle = Mathf.Clamp(manualAimHalfAngle, 0f, 180f);
            value.swivelSpeed = Mathf.Max(1f, swivelSpeed);
            return value;
        }
    }
}
