using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Squads
{
    /// <summary>
    /// 진형 모양 규칙(COMBAT-08). 씬과 길찾기를 알지 않는다.
    /// 폭(m)으로 한 줄 인원(열)을 정하고, 남는 깊이가 모자라면 줄 수를 줄이는 대신 줄을 옆으로 늘린다.
    /// </summary>
    public static class FormationLayout
    {
        /// <summary>깊이·폭 비교에서 반올림 오차를 흡수하는 여유.</summary>
        public const float Tolerance = 0.05f;

        /// <summary>
        /// 전열 폭(첫 사람과 끝 사람 사이 거리, m)에 들어가는 한 줄 인원. 1명 이상, 전체 인원 이하.
        /// </summary>
        public static int FilesForWidth(float width, float spacing, int count)
        {
            if (count <= 0) return 1;
            spacing = Mathf.Max(0.01f, spacing);
            int files = Mathf.RoundToInt(Mathf.Max(0f, width) / spacing) + 1;
            return Mathf.Clamp(files, 1, count);
        }

        /// <summary>한 줄 인원이 <paramref name="files"/>일 때의 전열 폭.</summary>
        public static float WidthForFiles(int files, float spacing) => Mathf.Max(0, files - 1) * spacing;

        /// <summary>
        /// 요청한 한 줄 인원으로 서되, 깊이 <paramref name="availableDepth"/>(앞줄과 뒷줄 사이에 쓸 수 있는 거리)에 들어가는 줄 수로 자른다.
        /// 줄을 자르면 남는 인원은 줄을 옆으로 늘려 세운다. 깊이가 무한이면 자르지 않는다.
        /// </summary>
        public static Vector2Int Shape(int count, int requestedFiles, float spacing, float availableDepth)
        {
            if (count <= 0) return new Vector2Int(1, 0);
            int files = Mathf.Clamp(requestedFiles, 1, count);
            int ranks = (count + files - 1) / files;
            int maxRanks = MaxRanks(spacing, availableDepth);
            if (ranks > maxRanks)
            {
                ranks = maxRanks;
                files = (count + ranks - 1) / ranks;
                ranks = (count + files - 1) / files;
            }
            return new Vector2Int(files, ranks);
        }

        /// <summary>깊이에 들어가는 최대 줄 수. 최소 1줄.</summary>
        public static int MaxRanks(float spacing, float availableDepth)
        {
            if (float.IsPositiveInfinity(availableDepth)) return int.MaxValue;
            spacing = Mathf.Max(0.01f, spacing);
            return Mathf.Max(1, Mathf.FloorToInt((Mathf.Max(0f, availableDepth) + Tolerance) / spacing) + 1);
        }

        /// <summary>
        /// 대형 가운데가 기준점에서 앞쪽(바라보는 쪽)으로 얼마나 떨어지는지.
        /// 앞줄 기준이면 앞줄이 기준점에 오고 나머지는 뒤에 선다. 아니면 대형 가운데가 기준점이다.
        /// 그 자리가 앞(<paramref name="forwardRoom"/>)이나 뒤(<paramref name="backRoom"/>)의 걸을 수 있는 범위를 넘으면 안쪽으로 민다.
        /// </summary>
        public static float CenterOffset(int ranks, float spacing, bool frontAnchored, float forwardRoom, float backRoom)
        {
            float depth = Mathf.Max(0, ranks - 1) * spacing;
            float offset = frontAnchored ? -depth * 0.5f : 0f;
            forwardRoom = Mathf.Max(0f, forwardRoom); backRoom = Mathf.Max(0f, backRoom);
            if (float.IsPositiveInfinity(forwardRoom) && float.IsPositiveInfinity(backRoom)) return offset;
            if (depth > forwardRoom + backRoom) return (Finite(forwardRoom) - Finite(backRoom)) * 0.5f;
            float low = -backRoom + depth * 0.5f, high = forwardRoom - depth * 0.5f;
            return Mathf.Clamp(offset, low, high);
        }

        static float Finite(float value) => float.IsPositiveInfinity(value) ? 0f : value;

        /// <summary>
        /// 대형 가운데를 중심으로 한 자리. 앞줄부터 채우고 각 줄은 가운데 정렬한다. 첫 자리가 앞줄 가운데 쪽이다.
        /// </summary>
        public static List<Vector3> Slots(Vector3 center, float facing, int count, int files, float spacing)
        {
            var slots = new List<Vector3>(Mathf.Max(0, count));
            if (count <= 0) return slots;
            files = Mathf.Max(1, files);
            int ranks = (count + files - 1) / files;
            Quaternion rotation = Quaternion.Euler(0f, facing, 0f);
            for (int i = 0; i < count; i++)
            {
                int rank = i / files;
                int inRank = Mathf.Min(files, count - rank * files);
                float x = (i % files - (inRank - 1) * 0.5f) * spacing;
                float z = ((ranks - 1) * 0.5f - rank) * spacing;
                slots.Add(center + rotation * new Vector3(x, 0f, z));
            }
            return slots;
        }

        /// <summary>
        /// 부대원이 바뀐 뒤 자리를 다시 나눈다(COMBAT-08). 지금 자리(<paramref name="posts"/>)가 새 자리 중 하나와
        /// <paramref name="keepDistance"/> 안이면 그대로 두고, 남은 자리는 앞줄(번호가 작은 자리)부터 가장 가까운 남은 부대원이 맡는다.
        /// 그래서 앞에서 한 명이 빠지면 뒤에서 한 명만 당겨 오고, 새로 온 부대원은 남은 뒷자리를 채운다.
        /// <paramref name="hasPost"/>가 false인 부대원(새로 왔거나 자리가 없던)은 지금 위치로 거리만 잰다. 결과는 부대원별 자리 번호.
        /// </summary>
        public static int[] AssignSlots(IReadOnlyList<Vector3> posts, IReadOnlyList<bool> hasPost, IReadOnlyList<Vector3> slots,
            float keepDistance)
        {
            int count = Mathf.Min(posts.Count, slots.Count);
            var result = new int[posts.Count];
            for (int i = 0; i < result.Length; i++) result[i] = -1;
            var taken = new bool[slots.Count];
            float keep = keepDistance * keepDistance;
            for (int unit = 0; unit < count; unit++)
            {
                if (!hasPost[unit]) continue;
                int best = -1; float nearest = keep;
                for (int slot = 0; slot < slots.Count; slot++)
                {
                    if (taken[slot]) continue;
                    float distance = Flat(posts[unit] - slots[slot]);
                    if (distance <= nearest) { nearest = distance; best = slot; }
                }
                if (best >= 0) { result[unit] = best; taken[best] = true; }
            }
            for (int slot = 0; slot < slots.Count; slot++)
            {
                if (taken[slot]) continue;
                int best = -1; float nearest = float.PositiveInfinity;
                for (int unit = 0; unit < count; unit++)
                {
                    if (result[unit] >= 0) continue;
                    float distance = Flat(posts[unit] - slots[slot]);
                    if (distance < nearest) { nearest = distance; best = unit; }
                }
                if (best < 0) break;
                result[best] = slot; taken[slot] = true;
            }
            return result;
        }

        static float Flat(Vector3 offset) { offset.y = 0f; return offset.sqrMagnitude; }

        /// <summary>
        /// 땅에 그은 선에서 부대가 바라볼 방향(도). 선에 수직이고, 보는 쪽(<paramref name="viewForward"/>)과 같은 쪽을 고른다.
        /// </summary>
        public static float FacingFromLine(Vector3 start, Vector3 end, Vector3 viewForward)
        {
            Vector3 along = end - start; along.y = 0f;
            if (along.sqrMagnitude < 1e-6f)
            {
                viewForward.y = 0f;
                return viewForward.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(viewForward.x, viewForward.z) * Mathf.Rad2Deg;
            }
            var normal = new Vector3(along.z, 0f, -along.x);
            if (Vector3.Dot(normal, viewForward) < 0f) normal = -normal;
            return Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 선을 <paramref name="parts"/>개로 나눈 조각 중 <paramref name="index"/>번째. 바라보는 쪽에서 볼 때 왼쪽부터 센다.
        /// 여러 부대를 한 선에 나란히 세울 때 쓴다.
        /// </summary>
        public static void Segment(Vector3 start, Vector3 end, float facing, int parts, int index, out Vector3 from, out Vector3 to)
        {
            parts = Mathf.Max(1, parts);
            Vector3 right = Quaternion.Euler(0f, facing, 0f) * Vector3.right;
            Vector3 left = start, rightEnd = end;
            if (Vector3.Dot(end - start, right) < 0f) { left = end; rightEnd = start; }
            from = Vector3.Lerp(left, rightEnd, (float)index / parts);
            to = Vector3.Lerp(left, rightEnd, (float)(index + 1) / parts);
        }
    }
}
