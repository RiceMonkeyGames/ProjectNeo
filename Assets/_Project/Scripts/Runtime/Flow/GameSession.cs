using ProjectNeo.Meta;
using UnityEngine;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 씬이 바뀌어도 유지되는 게임 흐름의 보관처. 어느 씬에서 Play를 눌러도 같은 방법으로 흐름에 접근한다.
    /// 실행을 시작할 때마다 새로 만든다.
    /// </summary>
    public static class GameSession
    {
        static GameFlow flow;
        static MetaProgress meta;

        public static GameFlow Flow => flow ??= new GameFlow();

        /// <summary>
        /// 런이 끝나도 남는 메타 진행(지휘관 경험치·해금 포인트 등). 파일 저장(M3)이 없어 지금은 실행하는 동안만 남는다.
        /// </summary>
        public static MetaProgress Meta => meta ??= new MetaProgress();

        /// <summary>흐름을 처음 상태로 되돌린다. 실행 시작 때 자동으로 불리며 테스트에서도 쓴다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { flow = null; meta = null; }
    }
}
