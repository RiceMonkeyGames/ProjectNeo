using System;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 비행 적이 대상을 공격할 때 머무는 자리. 낮게 붙는 근접형, 높이 떠서 쏘는 원거리형 등은
    /// 이 형식의 구현과 데이터로 고른다. 공격 자체는 몸통(CharacterActor)의 근접·원거리 공격을 쓴다.
    /// </summary>
    public abstract class FlyerEngagement : ScriptableObject
    {
        /// <param name="targetFeet">대상의 발 위치.</param>
        /// <param name="surfaceAt">대상 높이 근처에서 그 점 아래 발판의 높이. 발판이 없으면 null.</param>
        public Vector3 HoverPoint(Vector3 flyer, Vector3 targetFeet, float attackRange, float flyerRadius,
            float targetRadius, Func<Vector3, float?> surfaceAt) =>
            HoverPoint(new FlyerHoverQuery(flyer, targetFeet, attackRange, flyerRadius, targetRadius, surfaceAt));

        /// <summary>다른 비행 적 자리와 겹치지 않고(SIEGE-16) 몸이 들어가며 대상이 보이는 자리를 고른다.</summary>
        public abstract Vector3 HoverPoint(in FlyerHoverQuery query);
    }
}
