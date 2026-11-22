using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 적 궁수가 대상을 사거리 안에 둘 방향을 정하는 순수 규칙(SIEGE-15). 씬을 알지 않는다. 모든 벡터는 성(격자) 기준 x·z 평면이다.
    /// 대상 칸의 바깥으로 열린 면 중 궁수 쪽을 가장 많이 향한 면의 바깥 법선을 쓴다(옆 성벽·튀어나온 보루).
    /// 그런 면이 없으면(안뜰의 대상) 성 중심에서 대상 쪽 방향이 궁수 쪽이면 그 방향, 아니면 대상에서 궁수 쪽 방향을 쓴다.
    /// </summary>
    public static class ArcherApproachRules
    {
        // 이보다 궁수 반대쪽을 향한 면은 고르지 않는다. 성을 빙 돌아가지 않게 한다.
        const float MinimumFacing = 0.05f;

        /// <summary>
        /// 면 비트(비트 i = <see cref="BattlementRules.Directions"/>[i]) 중 <paramref name="toArcher"/>와 가장 같은 쪽을 향한 면.
        /// 같으면 앞 번호를 쓴다. 고를 면이 없거나 모두 궁수 반대쪽이면 -1.
        /// </summary>
        public static int ChooseFace(int faceMask, Vector2 toArcher)
        {
            if (faceMask == 0 || toArcher.sqrMagnitude < 0.0001f) return faceMask == 0 ? -1 : FirstFace(faceMask);
            Vector2 toward = toArcher.normalized;
            int best = -1;
            float bestDot = MinimumFacing;
            for (int i = 0; i < BattlementRules.Directions.Length; i++)
            {
                if ((faceMask & (1 << i)) == 0) continue;
                Vector2Int d = BattlementRules.Directions[i];
                float dot = d.x * toward.x + d.y * toward.y;
                if (dot <= bestDot) continue;
                bestDot = dot;
                best = i;
            }
            return best;
        }

        static int FirstFace(int faceMask)
        {
            for (int i = 0; i < BattlementRules.Directions.Length; i++) if ((faceMask & (1 << i)) != 0) return i;
            return -1;
        }

        /// <summary>
        /// 궁수가 설 방향(대상 기준, 단위 벡터). 면을 고르면 그 면의 바깥 법선, 아니면 성 중심에서 대상 쪽(궁수 쪽일 때만),
        /// 그것도 아니면 대상에서 궁수 쪽, 마지막으로 <paramref name="fallback"/>.
        /// </summary>
        public static Vector2 ApproachDirection(int faceMask, Vector2 target, Vector2 archer, Vector2 castleCenter,
            bool hasCenter, Vector2 fallback)
        {
            Vector2 toArcher = archer - target;
            int face = ChooseFace(faceMask, toArcher);
            if (face >= 0)
            {
                Vector2Int d = BattlementRules.Directions[face];
                return new Vector2(d.x, d.y);
            }
            if (hasCenter)
            {
                Vector2 radial = target - castleCenter;
                if (radial.sqrMagnitude > 0.0001f && Vector2.Dot(radial, toArcher) > 0f) return radial.normalized;
            }
            if (toArcher.sqrMagnitude > 0.0001f) return toArcher.normalized;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.up;
        }
    }
}
