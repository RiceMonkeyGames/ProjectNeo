using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 교체용 임시 외형 부품. 유닛이 죽으면 이 오브젝트를 숨긴다.
    /// 종류 표시처럼 Visual 아래에 덧붙인 임시 장식에 붙인다.
    /// </summary>
    public sealed class PlaceholderDeathHider : MonoBehaviour
    {
        CharacterActor actor;

        void OnEnable()
        {
            actor = GetComponentInParent<CharacterActor>(true);
            if (actor == null) return;
            actor.Died += Hide;
            if (actor.Health != null && actor.Health.IsDead) Hide();
        }

        void OnDisable()
        {
            if (actor != null) actor.Died -= Hide;
        }

        void Hide() => gameObject.SetActive(false);
    }
}
