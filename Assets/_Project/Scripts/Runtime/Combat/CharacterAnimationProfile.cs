using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 한 모델 애니메이션 묶음(남·여·비행)의 클립 정보와 연출 수치(ART-04).
    /// 클립 길이와 무기 닿는 순간은 설정 메뉴가 클립을 재서 채운다.
    /// </summary>
    public sealed class CharacterAnimationProfile : ScriptableObject
    {
        [SerializeField] bool groundLocomotion = true;
        [SerializeField] bool combatIdle = true;
        [SerializeField] bool crouch = true;
        [Tooltip("근접 공격 상태(Attack0, Attack1...)마다 무기가 닿는 순간(초).")]
        [SerializeField] float[] meleeContactSeconds = new float[0];
        [SerializeField] float[] meleeClipSeconds = new float[0];
        [SerializeField, Min(0)] int deathCount;
        [SerializeField, Min(0.05f)] float hitSeconds = 0.6f;
        [SerializeField, Min(0.05f)] float bowLoadSeconds = 0.83f;
        [SerializeField, Min(0.05f)] float bowReleaseSeconds = 0.8f;
        [Tooltip("마지막 공격 뒤 전투 대기 자세를 유지하는 시간(초).")]
        [SerializeField, Min(0f)] float engagedSeconds = 2.5f;
        [Header("활 위·아래 조준 클립의 화살 선 각도 (COMBAT-11)")]
        [Tooltip("아래 조준 클립(AimPitch -1)에서 시위 손→활 손 선의 높이각(도). 설정 메뉴가 잰다.")]
        [SerializeField, Range(-90f, 0f)] float bowPitchDown = -48.5f;
        [Tooltip("정면 클립(AimPitch 0)의 높이각(도). 정면 클립도 살짝 아래를 본다.")]
        [SerializeField, Range(-30f, 30f)] float bowPitchMiddle = -3.8f;
        [Tooltip("위 조준 클립(AimPitch +1)의 높이각(도).")]
        [SerializeField, Range(0f, 90f)] float bowPitchUp = 41.5f;
        [SerializeField, Min(0f)] float crossFadeSeconds = 0.25f;
        [Tooltip("이 속도(m/s) 이상으로 움직이는 궁수는 활을 당긴 채 두지 않고 내린다(달리며 조준하는 어색한 자세 방지).")]
        [SerializeField, Min(0f)] float bowLowerSpeed = 1.5f;
        [Header("허리에 찬 근접 무기 (ART-04)")]
        [Tooltip("대상이 이 거리(m) 안이면 무기를 뽑는다.")]
        [SerializeField, Min(0f)] float drawDistance = 8f;
        [Tooltip("뽑을 이유가 없어진 뒤 이 시간(초)이 지나면 다시 넣는다.")]
        [SerializeField, Min(0f)] float sheatheDelay = 3f;
        [Tooltip("뽑기 클립에서 손이 칼자루를 잡는 순간(초). 0이면 뽑기·넣기 동작이 없다. 설정 메뉴가 잰다.")]
        [SerializeField, Min(0f)] float unsheatheGrabSeconds;
        [SerializeField, Min(0f)] float unsheatheSeconds;
        [Tooltip("넣기 클립에서 손이 칼을 놓는 순간(초). 설정 메뉴가 잰다.")]
        [SerializeField, Min(0f)] float sheatheReleaseSeconds;
        [SerializeField, Min(0f)] float sheatheSeconds;

        public float DrawDistance => drawDistance;
        public float SheatheDelay => sheatheDelay;
        public bool HasSheathMotion => unsheatheSeconds > 0f && sheatheSeconds > 0f;
        public float UnsheatheGrabSeconds => unsheatheGrabSeconds;
        public float UnsheatheSeconds => unsheatheSeconds;
        public float SheatheReleaseSeconds => sheatheReleaseSeconds;
        public float SheatheSeconds => sheatheSeconds;

        /// <summary>설정 메뉴가 잰 뽑기·넣기 클립 시각을 넣는다.</summary>
        public void SetSheathMotion(float grabSeconds, float unsheatheLength, float releaseSeconds, float sheatheLength)
        {
            unsheatheGrabSeconds = Mathf.Max(0f, grabSeconds);
            unsheatheSeconds = Mathf.Max(0f, unsheatheLength);
            sheatheReleaseSeconds = Mathf.Max(0f, releaseSeconds);
            sheatheSeconds = Mathf.Max(0f, sheatheLength);
        }
        public bool GroundLocomotion => groundLocomotion;
        public bool CombatIdle => combatIdle;
        public bool Crouch => crouch;
        public int MeleeCount => Mathf.Min(meleeContactSeconds.Length, meleeClipSeconds.Length);
        public float MeleeContactSeconds(int index) => meleeContactSeconds[index];
        public float MeleeClipSeconds(int index) => meleeClipSeconds[index];
        public int DeathCount => deathCount;
        public float HitSeconds => hitSeconds;
        public float BowLoadSeconds => bowLoadSeconds;
        public float BowReleaseSeconds => bowReleaseSeconds;
        public float EngagedSeconds => engagedSeconds;
        public float BowPitchDown => bowPitchDown;
        public float BowPitchMiddle => bowPitchMiddle;
        public float BowPitchUp => bowPitchUp;

        /// <summary>설정 메뉴가 잰 활 조준 클립 각도(도)를 넣는다. 아래 ≤ 정면 ≤ 위 순서로 맞춘다.</summary>
        public void SetBowPitch(float down, float middle, float up)
        {
            bowPitchMiddle = Mathf.Clamp(middle, -30f, 30f);
            bowPitchDown = Mathf.Clamp(down, -90f, bowPitchMiddle);
            bowPitchUp = Mathf.Clamp(up, bowPitchMiddle, 90f);
        }
        public float CrossFadeSeconds => crossFadeSeconds;
        public float BowLowerSpeed => bowLowerSpeed;

        public static CharacterAnimationProfile Create(bool groundLocomotion = true, bool combatIdle = true, bool crouch = true,
            float[] meleeContactSeconds = null, float[] meleeClipSeconds = null, int deathCount = 0,
            float hitSeconds = 0.6f, float bowLoadSeconds = 0.83f, float bowReleaseSeconds = 0.8f,
            float engagedSeconds = 2.5f, float crossFadeSeconds = 0.25f, float bowLowerSpeed = 1.5f)
        {
            var profile = CreateInstance<CharacterAnimationProfile>();
            profile.Set(groundLocomotion, combatIdle, crouch, meleeContactSeconds, meleeClipSeconds, deathCount,
                hitSeconds, bowLoadSeconds, bowReleaseSeconds, engagedSeconds, crossFadeSeconds);
            profile.bowLowerSpeed = Mathf.Max(0f, bowLowerSpeed);
            return profile;
        }

        /// <summary>설정 메뉴가 기존 에셋을 다시 채울 때 쓴다.</summary>
        public void Set(bool groundLocomotion, bool combatIdle, bool crouch, float[] meleeContactSeconds, float[] meleeClipSeconds,
            int deathCount, float hitSeconds, float bowLoadSeconds, float bowReleaseSeconds,
            float engagedSeconds, float crossFadeSeconds)
        {
            this.groundLocomotion = groundLocomotion;
            this.combatIdle = combatIdle;
            this.crouch = crouch;
            this.meleeContactSeconds = meleeContactSeconds ?? new float[0];
            this.meleeClipSeconds = meleeClipSeconds ?? new float[0];
            this.deathCount = Mathf.Max(0, deathCount);
            this.hitSeconds = Mathf.Max(0.05f, hitSeconds);
            this.bowLoadSeconds = Mathf.Max(0.05f, bowLoadSeconds);
            this.bowReleaseSeconds = Mathf.Max(0.05f, bowReleaseSeconds);
            this.engagedSeconds = Mathf.Max(0f, engagedSeconds);
            this.crossFadeSeconds = Mathf.Max(0f, crossFadeSeconds);
        }
    }
}
