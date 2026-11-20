using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 외부 애니메이션 클립에 박힌 이벤트를 받는 외형 전용 부품(ART-04). 모델(Animator와 같은 오브젝트)에 붙는다.
    /// ExplosiveLLC 클립(예: Archer@Walk)은 발걸음 이벤트(FootL·FootR)를 부르는데, 받는 쪽이 없으면 매번 오류 로그가 난다.
    /// 지금은 아무것도 하지 않으며, 나중에 발소리 등을 붙일 자리다. 게임 판정에는 쓰지 않는다.
    /// </summary>
    public sealed class CharacterAnimationEventSink : MonoBehaviour
    {
        public void FootL() { }
        public void FootR() { }
        public void Hit() { }
        public void Shoot() { }
        public void Land() { }
    }
}
