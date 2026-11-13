using ProjectNeo.Combat;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 성을 공격하는 적 하나의 공통 판단 창구. 생성·후퇴·승패 집계(SiegeDirector, GameBattleController)는
    /// 이 형식만 본다. 걷는 적(SiegeAttackerBrain)과 나는 적(FlyerBrain)이 각자 이동 방식을 구현한다.
    /// </summary>
    [RequireComponent(typeof(CharacterActor))]
    public abstract class SiegeAttacker : MonoBehaviour
    {
        public CharacterActor Actor { get; protected set; }
        public abstract SiegeAttackerRole Role { get; }
        /// <summary>전투가 시작된 뒤 한 번이라도 할 일을 정했는지. 후퇴 판정은 모두 정한 뒤에 한다.</summary>
        public abstract bool HasDecided { get; }
        /// <summary>아군과 직접 싸우는 중인지.</summary>
        public abstract bool IsAssaulting { get; }
        public abstract bool IsRetreating { get; }
        /// <summary>후퇴해 전장을 떠났다. 승패에서는 죽은 적과 같이 남은 적 수에서 뺀다.</summary>
        public bool Fled { get; protected set; }
        /// <summary>성벽 붕괴·사다리 전복 때 떨어지는 몸인지. 생성하는 쪽이 낙하 부품을 붙일지 정한다.</summary>
        public virtual bool UsesFallController => true;
        /// <summary>오르기·떨어지기처럼 끝날 때까지 다른 판단을 미루는 동작 중인지.</summary>
        public abstract bool IsMidAction { get; }

        /// <summary>프리팹으로 만든 적을 씬의 성과 사다리에 연결한다. 활성화하기 전에 부른다.</summary>
        public abstract void Bind(ConstructionWorld owner, SiegeLadder[] ladderSpots, Vector2Int outwardDirection,
            SiegeSettings battleSettings = null);

        /// <summary>판 바깥으로 스스로 물러나 떠난다(Q-30).</summary>
        public abstract void Retreat();

        /// <summary>지금 자기 방식으로 닿을 수 있는 아군이 있는지.</summary>
        public abstract bool CanReachAnyEnemy();

        /// <summary>
        /// 전투 진행 쪽이 정한 출구로 보낸다. 스스로의 판단은 멈춘다. 실제로 향하는 지점을 돌려준다.
        /// 도착 처리는 부른 쪽이 맡는다.
        /// </summary>
        public abstract Vector3 BeginExit(Vector3 exit, float searchRadius);
    }
}
