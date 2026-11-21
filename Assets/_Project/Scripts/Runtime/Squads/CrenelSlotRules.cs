using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Squads
{
    /// <summary>
    /// 원거리 분대의 앞줄을 성가퀴 빈틈(크레넬) 사격 자리에 세우는 규칙(COMBAT-10). 씬과 길찾기를 알지 않는다.
    /// 앞줄 가운데에서 가까운 빈틈부터 한 줄 인원만큼 고르고(빈틈 하나에 한 명), 빈틈을 못 받은 인원은 그 뒤 줄에 선다.
    /// 쓸 수 있는 빈틈이 없으면 원래 자리를 그대로 둔다.
    /// </summary>
    public static class CrenelSlotRules
    {
        /// <summary>빈틈이 앞줄 선에서 앞뒤로 이만큼(m) 안이어야 쓴다.</summary>
        public const float MaxDepthOffset = 2f;
        /// <summary>빈틈이 앞줄 높이에서 위아래로 이만큼(m) 안이어야 쓴다(다른 층 성벽 제외).</summary>
        public const float MaxHeightOffset = 1f;
        static readonly List<Vector3> chosenBuffer = new List<Vector3>();
        static readonly List<Vector3> candidateBuffer = new List<Vector3>();

        /// <summary>
        /// <paramref name="slots"/>(앞줄부터, 한 줄 <paramref name="files"/>명)를 빈틈 자리에 맞춰 <paramref name="output"/>에 다시 채운다.
        /// 앞줄 폭 양옆으로 한 간격까지의 빈틈만 쓴다. 결과 순서는 빈틈 자리(왼쪽부터) 다음 뒷줄이다. 쓴 빈틈 수를 돌려준다.
        /// </summary>
        public static int Arrange(IReadOnlyList<Vector3> slots, int files, float facing, float spacing,
            IReadOnlyList<Vector3> crenels, List<Vector3> output)
        {
            output.Clear();
            int count = slots.Count;
            for (int i = 0; i < count; i++) output.Add(slots[i]);
            if (count == 0 || crenels == null || crenels.Count == 0) return 0;
            files = Mathf.Clamp(files, 1, count);
            Quaternion rotation = Quaternion.Euler(0f, facing, 0f);
            Vector3 forward = rotation * Vector3.forward, right = rotation * Vector3.right;
            Vector3 front = Vector3.zero;
            for (int i = 0; i < files; i++) front += slots[i];
            front /= files;
            float halfWidth = (files - 1) * spacing * 0.5f + spacing;

            // 전열 미리보기에서 매 프레임 부르므로 목록을 다시 쓴다(메인 스레드 전용).
            List<Vector3> chosen = chosenBuffer, candidates = candidateBuffer;
            chosen.Clear(); candidates.Clear();
            foreach (Vector3 crenel in crenels)
            {
                Vector3 offset = crenel - front;
                if (Mathf.Abs(offset.y) > MaxHeightOffset) continue;
                if (Mathf.Abs(Vector3.Dot(offset, forward)) > MaxDepthOffset) continue;
                if (Mathf.Abs(Vector3.Dot(offset, right)) > halfWidth) continue;
                candidates.Add(crenel);
            }
            candidates.Sort((a, b) => Mathf.Abs(Vector3.Dot(a - front, right)).CompareTo(Mathf.Abs(Vector3.Dot(b - front, right))));
            for (int i = 0; i < candidates.Count && chosen.Count < files; i++) chosen.Add(candidates[i]);
            if (chosen.Count == 0) return 0;
            chosen.Sort((a, b) => Vector3.Dot(a - front, right).CompareTo(Vector3.Dot(b - front, right)));

            output.Clear();
            output.AddRange(chosen);
            int rest = count - chosen.Count;
            if (rest <= 0) return chosen.Count;
            // 빈틈 자리 선(앞뒤 평균)에서 한 간격 뒤부터 같은 한 줄 인원으로 뒷줄을 세운다.
            float line = 0f;
            foreach (Vector3 point in chosen) line += Vector3.Dot(point - front, forward);
            line /= chosen.Count;
            int ranks = (rest + files - 1) / files;
            Vector3 rearCenter = front + forward * (line - spacing * (1f + (ranks - 1) * 0.5f));
            output.AddRange(ProjectNeo.Squads.FormationLayout.Slots(rearCenter, facing, rest, files, spacing));
            return chosen.Count;
        }
    }
}
