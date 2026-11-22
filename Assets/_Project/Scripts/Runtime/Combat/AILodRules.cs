namespace ProjectNeo.Combat
{
    /// <summary>대규모 전투 AI 상세도 등급(PERF-ai-lod).</summary>
    public enum AILodTier
    {
        Lod0 = 0, // 근경·직접 교전: 원래 주기와 모든 정밀 기능.
        Lod1 = 1, // 중경·근거리 시야 밖·탑다운 기본 상한: 주기 2배, 근접 자리 길찾기 간소화.
        Lod2 = 2, // 원경·시야 밖 대기/행군: 주기 4배, 근접 자리 생략, 비전투 회피 완화.
    }

    /// <summary>
    /// 카메라와의 거리, 화면 안(시야) 여부, 영웅·플레이어 교전 상태, 탑다운 카메라 모드로
    /// 유닛의 <see cref="AILodTier"/>와 등급별 배율·후보 수를 계산하는 순수 규칙(PERF-ai-lod).
    /// </summary>
    public static class AILodRules
    {
        /// <summary>
        /// 유닛의 AI LOD 등급을 정한다.
        /// 1) LOD가 꺼져 있거나(<paramref name="enabled"/> == false), 플레이어/영웅과 직접 교전 중이면 언제나 <see cref="AILodTier.Lod0"/>.
        /// 2) 거리 제곱이 <paramref name="lod0Distance"/> 안이고 화면 안이면 <see cref="AILodTier.Lod0"/>(단, <paramref name="topDownMode"/>면 최소 <see cref="AILodTier.Lod1"/>).
        /// 3) 거리 제곱이 <paramref name="lod1Distance"/> 안이면(근거리 시야 밖 포함) <see cref="AILodTier.Lod1"/>,
        ///    단 시야 밖이고 싸울 대상이 없는 대기 상태면서 <paramref name="lod0Distance"/> 밖이면 <see cref="AILodTier.Lod2"/>.
        /// 4) <paramref name="lod1Distance"/> 밖이면 <see cref="AILodTier.Lod2"/>.
        /// </summary>
        public static AILodTier Evaluate(
            bool enabled,
            float sqrDistanceToView,
            float lod0Distance,
            float lod1Distance,
            bool inViewCone,
            bool highPriorityCombat,
            bool hasTarget,
            bool topDownMode)
        {
            if (!enabled || highPriorityCombat) return AILodTier.Lod0;

            float d0 = lod0Distance > 0f ? lod0Distance * lod0Distance : 0f;
            float d1 = lod1Distance > lod0Distance ? lod1Distance * lod1Distance : d0;

            AILodTier tier;
            if (sqrDistanceToView <= d0)
            {
                tier = inViewCone ? AILodTier.Lod0 : AILodTier.Lod1;
            }
            else if (sqrDistanceToView <= d1)
            {
                tier = (!inViewCone && !hasTarget) ? AILodTier.Lod2 : AILodTier.Lod1;
            }
            else
            {
                tier = AILodTier.Lod2;
            }

            if (topDownMode && tier == AILodTier.Lod0)
                return AILodTier.Lod1;

            return tier;
        }

        /// <summary>등급에 따른 판단 주기(<see cref="UnitAISettings.DecisionInterval"/>) 배율.</summary>
        public static float IntervalMultiplier(AILodTier tier) => tier switch
        {
            AILodTier.Lod1 => 2f,
            AILodTier.Lod2 => 4f,
            _ => 1f,
        };

        /// <summary>등급에 따라 길찾기(CalculatePath)를 시험할 최대 상위 후보 수(K).</summary>
        public static int PathCandidateLimit(AILodTier tier, int baseMaxCandidates)
        {
            int max = baseMaxCandidates > 1 ? baseMaxCandidates : 1;
            return tier switch
            {
                AILodTier.Lod0 => max,
                AILodTier.Lod1 => max > 2 ? 2 : max,
                _ => 1,
            };
        }

        /// <summary>등급에 따라 근접 링 하나에서 시험할 최대 빈자리 수. LOD 2는 0(중심점으로 바로 이동).</summary>
        public static int MeleeSlotTries(AILodTier tier, int baseTries = 3) => tier switch
        {
            AILodTier.Lod0 => baseTries,
            AILodTier.Lod1 => 1,
            _ => 0,
        };

        /// <summary>근접 자리 후보에 대해 NavMesh.SamplePosition 뒤에 별도 CalculatePath까지 수행할지(LOD 0만 수행).</summary>
        public static bool UsesFullMeleeSlotPath(AILodTier tier) => tier == AILodTier.Lod0;

        /// <summary>애니메이터의 세부 조준(활 상하 각도·모델 회전·무기 칼집)을 매 몇 프레임마다 갱신할지.</summary>
        public static int AnimatorDetailStride(AILodTier tier, bool inViewCone) => tier switch
        {
            AILodTier.Lod0 => 1,
            AILodTier.Lod1 => 2,
            _ => inViewCone ? 4 : 8,
        };
    }
}
