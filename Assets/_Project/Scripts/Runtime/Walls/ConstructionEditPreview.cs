using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 이 성(<see cref="ConstructionWorld"/>)의 런 시작 배치와 킷 꾸밈을 에디터에서도 보이게 한다(SIEGE-40 분지 배치 작업).
    /// 표시만 하는 표식이다. 그리기는 에디터 도구(<c>ConstructionEditPreviewDriver</c>)가 저장되지 않는 복사본으로 하고,
    /// Play에 들어가기 직전에 지운다. 게임 동작에는 영향이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ConstructionWorld))]
    public sealed class ConstructionEditPreview : MonoBehaviour
    {
    }
}
