using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 근접 접근 자리 계산(COMBAT-12, 사용자 결정 2026-11-22). 대상 둘레 원(링) 위에 몸 크기 간격으로 자리를 두고,
    /// 각 근접 유닛이 가장 가까운 빈 자리로 간다. 안쪽 링이 차면 바깥 링에서 기다린다.
    /// 자리 방향은 세계 좌표 기준으로 고정해, 대상이 움직여도 같은 번호의 자리는 같은 쪽에 있다.
    /// </summary>
    public static class MeleeRingRules
    {
        public const int InnerRing = 0;
        public const int OuterRing = 1;
        public const int MaxSlots = 16;

        /// <summary>
        /// 안쪽 링 반지름(대상 중심에서). 대상 반지름 + 공격자 반지름 + 틈이고, 그 자리에서 사거리가 닿도록
        /// (대상 몸 표면까지가 사거리의 80% 안) 줄인다.
        /// </summary>
        public static float InnerRadius(float targetRadius, float attackerRadius, float gap, float attackRange)
        {
            float reach = Mathf.Max(0.05f, attackRange * 0.8f);
            return Mathf.Max(0.05f, targetRadius) + Mathf.Min(Mathf.Max(0f, attackerRadius) + Mathf.Max(0f, gap), reach);
        }

        /// <summary>링 반지름. 바깥 링은 안쪽 링에서 <paramref name="outerOffset"/>만큼 더 나간다.</summary>
        public static float RingRadius(int ring, float innerRadius, float outerOffset) =>
            ring == InnerRing ? innerRadius : innerRadius + Mathf.Max(0f, outerOffset);

        /// <summary>그 반지름의 원에 <paramref name="spacing"/> 간격으로 놓을 수 있는 자리 수(1 ~ <see cref="MaxSlots"/>).</summary>
        public static int SlotCount(float radius, float spacing)
        {
            if (spacing <= 0f || radius <= 0f) return 1;
            return Mathf.Clamp(Mathf.FloorToInt(2f * Mathf.PI * radius / spacing), 1, MaxSlots);
        }

        /// <summary>대상 중심에서 그 자리까지의 수평 변위. 0번은 +z(북), 번호가 늘면 시계 방향(+x 쪽)으로 돈다.</summary>
        public static Vector3 SlotOffset(float radius, int index, int count)
        {
            float angle = count <= 0 ? 0f : index * (2f * Mathf.PI / count);
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
        }

        /// <summary>
        /// 자리 번호를 <paramref name="from"/>(공격자 위치)에서 가까운 순으로 담는다. 거리가 같으면 번호 순.
        /// 높이는 보지 않는다.
        /// </summary>
        public static void OrderByDistance(Vector3 from, Vector3 center, float radius, int count, List<int> output)
        {
            output.Clear();
            var flat = new Vector3(from.x - center.x, 0f, from.z - center.z);
            for (int i = 0; i < count; i++)
            {
                float distance = (SlotOffset(radius, i, count) - flat).sqrMagnitude;
                int at = output.Count;
                while (at > 0 && (SlotOffset(radius, output[at - 1], count) - flat).sqrMagnitude > distance + 1e-6f) at--;
                output.Insert(at, i);
            }
        }
    }
}
