using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 비행 적 한 마리가 공격할 자리를 구할 때 넘기는 값(SIEGE-16). 씬 검사는 함수로만 받아 자리 규칙은 순수하게 둔다.
    /// </summary>
    public readonly struct FlyerHoverQuery
    {
        /// <summary>다가오는 쪽을 정하는 비행 적 위치. 대상 발에서 이 점 쪽이 첫 자리다.</summary>
        public readonly Vector3 Flyer;
        /// <summary>대상의 발 위치(대포는 받침 위 바닥).</summary>
        public readonly Vector3 TargetFeet;
        public readonly float AttackRange;
        public readonly float FlyerRadius;
        public readonly float TargetRadius;
        /// <summary>대상 높이 근처에서 그 점 아래 발판의 높이. 발판이 없으면 null.</summary>
        public readonly Func<Vector3, float?> SurfaceAt;
        /// <summary>같은 대상을 쫓는 다른 비행 적이 이미 잡은 자리. 비워 두면 겹침을 따지지 않는다.</summary>
        public readonly IReadOnlyList<Vector3> Occupied;
        /// <summary>다른 비행 적 자리와 떨어질 최소 수평 거리. 몸통 지름보다 작으면 지름을 쓴다.</summary>
        public readonly float Separation;
        /// <summary>이 자리에 몸이 들어가고 대상이 보이는지. 비워 두면 따지지 않는다.</summary>
        public readonly Func<Vector3, bool> ClearAt;

        public FlyerHoverQuery(Vector3 flyer, Vector3 targetFeet, float attackRange, float flyerRadius, float targetRadius,
            Func<Vector3, float?> surfaceAt, IReadOnlyList<Vector3> occupied = null, float separation = 0f,
            Func<Vector3, bool> clearAt = null)
        {
            Flyer = flyer;
            TargetFeet = targetFeet;
            AttackRange = attackRange;
            FlyerRadius = flyerRadius;
            TargetRadius = targetRadius;
            SurfaceAt = surfaceAt;
            Occupied = occupied;
            Separation = separation;
            ClearAt = clearAt;
        }
    }
}
