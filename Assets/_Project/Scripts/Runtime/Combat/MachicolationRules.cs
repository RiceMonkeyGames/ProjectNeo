using System.Collections.Generic;

namespace ProjectNeo.Combat
{
    /// <summary>AI가 고를 수 있는 마치콜레이션 구멍 하나의 요약(SIEGE-12). 길찾기는 고른 뒤 따로 확인한다.</summary>
    public readonly struct MachicolationCandidate
    {
        /// <summary>지키는 자리(Hold)에서 사격 자리까지 수평 거리의 제곱.</summary>
        public readonly float SqrDistance;
        /// <summary>같은 층인지(사격 자리와 지키는 자리 높이 차가 허용 안).</summary>
        public readonly bool SameLevel;
        /// <summary>다른 유닛이 맡지 않았고 다른 누구도 그 자리에 서 있지 않다.</summary>
        public readonly bool IsFree;
        /// <summary>구멍 아래에 적이 있다.</summary>
        public readonly bool HasEnemyBelow;

        public MachicolationCandidate(float sqrDistance, bool sameLevel, bool isFree, bool hasEnemyBelow)
        {
            SqrDistance = sqrDistance;
            SameLevel = sameLevel;
            IsFree = isFree;
            HasEnemyBelow = hasEnemyBelow;
        }
    }

    /// <summary>
    /// 마치콜레이션 사용 규칙(SIEGE-12, 사용자 결정 2026-11-22). 아군 원거리 AI는 중지(Hold) 중 쏠 대상이 보이지 않을 때,
    /// 근처의 빈 구멍 아래에 적이 있으면 그 구멍으로 가서 아래로 쏘고, 아래에 적이 없어지면 자리로 돌아간다. 구멍 하나에 한 명.
    /// </summary>
    public static class MachicolationRules
    {
        /// <summary>
        /// 갈 수 있는 구멍을 가까운 순서로 <paramref name="order"/>에 담는다(목록 번호). 빈 구멍, 같은 층, 반경 안,
        /// 아래에 적이 있는 것만 담는다. 거리가 같으면 목록 순서를 따른다.
        /// </summary>
        public static void Rank(IReadOnlyList<MachicolationCandidate> candidates, float radius, List<int> order)
        {
            order.Clear();
            if (candidates == null || radius <= 0f) return;
            float limit = radius * radius;
            for (int i = 0; i < candidates.Count; i++)
            {
                MachicolationCandidate candidate = candidates[i];
                if (!candidate.IsFree || !candidate.SameLevel || !candidate.HasEnemyBelow || candidate.SqrDistance > limit) continue;
                int at = order.Count;
                while (at > 0 && candidates[order[at - 1]].SqrDistance > candidate.SqrDistance) at--;
                order.Insert(at, i);
            }
        }

        /// <summary>쓰던 구멍을 놓을지. 구멍이 사라졌거나 아래에 적이 더는 없으면 놓는다.</summary>
        public static bool ShouldRelease(bool openingActive, bool hasEnemyBelow) => !openingActive || !hasEnemyBelow;

        /// <summary>
        /// 다른 유닛이 맡았는지 따질 때, 맡은 유닛이 이제 쓸 수 없는 상태(죽음·꺼짐)면 비어 있는 것으로 본다.
        /// 자기가 맡은 구멍은 자기에게 비어 있다.
        /// </summary>
        public static bool IsClaimFree(bool hasClaimant, bool claimantIsSelf, bool claimantActive) =>
            !hasClaimant || claimantIsSelf || !claimantActive;

        /// <summary>
        /// 성문 조각 충돌체를 이번 사격이 지나도 되는지. 그 충돌체가 성문(받치는 칸) 조각이고,
        /// 선이 들어가는 점과 나오는 점이 모두 통로 안이어야 한다. 통로 밖으로 벽 몸통을 뚫는 선은 막힌다.
        /// </summary>
        public static bool PassesThrough(bool exemptCollider, bool entryInside, bool exitInside) =>
            exemptCollider && entryInside && exitInside;
    }
}
