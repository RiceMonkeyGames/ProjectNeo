using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 성벽 위 사다리 방어 배정(SIEGE-31). 순수 계산이며 버퍼를 다시 써서 매번 할당하지 않는다.
    /// 1) 지킬 자리가 사다리 위쪽 끝에서 감지 반경 안(같은 높이)인 수비병만 그 사다리를 맡을 수 있다.
    /// 2) 사다리마다 지금 위치가 가까운 순서로 밀기 자리 수(최대 인원)만큼 밀기를 맡긴다. 한 명은 사다리 하나만 맡는다.
    /// 3) 길목이 필요한 사다리는 남은 수비병을 같은 순서로 길목 자리 수만큼 둘러세운다.
    /// 이미 같은 사다리·같은 일을 맡은 수비병은 거리에서 <c>stickiness</c>만큼 빼 주어 임무가 자꾸 바뀌지 않게 하고,
    /// 맡던 자리 번호가 아직 비어 있으면 그대로 쓴다. 나머지는 가장 가까운 빈자리를 받는다.
    /// </summary>
    public sealed class LadderDefensePlanner
    {
        struct Pair
        {
            public float Score;
            public int Defender;
            public int Site;
        }

        sealed class PairComparer : IComparer<Pair>
        {
            public int Compare(Pair a, Pair b)
            {
                int byScore = a.Score.CompareTo(b.Score);
                if (byScore != 0) return byScore;
                int byDefender = a.Defender.CompareTo(b.Defender);
                return byDefender != 0 ? byDefender : a.Site.CompareTo(b.Site);
            }
        }

        static readonly PairComparer Comparer = new PairComparer();
        readonly List<Pair> pairs = new List<Pair>();
        readonly List<int> pushCounts = new List<int>();
        readonly List<int> chokeCounts = new List<int>();
        readonly List<bool> spotTaken = new List<bool>();
        readonly List<int> members = new List<int>();

        public void Plan(IReadOnlyList<LadderDefenseSite> sites, IReadOnlyList<LadderDefender> defenders,
            float radius, float heightTolerance, float stickiness, List<LadderDuty> result)
        {
            result.Clear();
            for (int d = 0; d < defenders.Count; d++) result.Add(LadderDuty.None);
            if (sites.Count == 0 || defenders.Count == 0 || radius <= 0f) return;

            pairs.Clear();
            for (int d = 0; d < defenders.Count; d++)
            {
                LadderDefender defender = defenders[d];
                for (int s = 0; s < sites.Count; s++)
                {
                    LadderDefenseSite site = sites[s];
                    if (defender.Excludes(s) || !HoldDutyRules.IsNear(defender.Post, site.Top, radius, heightTolerance)) continue;
                    Vector3 offset = site.Top - defender.Position;
                    offset.y = 0f;
                    float score = offset.magnitude;
                    if (!defender.Current.IsNone && defender.Current.Site == s) score -= stickiness;
                    pairs.Add(new Pair { Score = score, Defender = d, Site = s });
                }
            }
            if (pairs.Count == 0) return;
            pairs.Sort(Comparer);

            Fill(pushCounts, sites.Count);
            Fill(chokeCounts, sites.Count);
            // 1) 밀기: 사다리마다 가까운 순서로 밀기 자리 수만큼.
            for (int i = 0; i < pairs.Count; i++)
            {
                Pair pair = pairs[i];
                if (!result[pair.Defender].IsNone) continue;
                if (pushCounts[pair.Site] >= sites[pair.Site].PushSpots.Count) continue;
                pushCounts[pair.Site]++;
                result[pair.Defender] = new LadderDuty(pair.Site, LadderDutyRole.Push, -1);
            }
            // 2) 길목: 길목이 필요한 사다리만, 남은 수비병을 같은 순서로.
            for (int i = 0; i < pairs.Count; i++)
            {
                Pair pair = pairs[i];
                LadderDefenseSite site = sites[pair.Site];
                if (!result[pair.Defender].IsNone || !site.NeedsChokepoint) continue;
                if (chokeCounts[pair.Site] >= site.ChokeSpots.Count) continue;
                chokeCounts[pair.Site]++;
                result[pair.Defender] = new LadderDuty(pair.Site, LadderDutyRole.Chokepoint, -1);
            }
            for (int s = 0; s < sites.Count; s++)
            {
                AssignSpots(s, LadderDutyRole.Push, sites[s].PushSpots, defenders, result);
                AssignSpots(s, LadderDutyRole.Chokepoint, sites[s].ChokeSpots, defenders, result);
            }
        }

        // 같은 사다리·같은 일을 맡은 수비병에게 자리 번호를 준다. 맡던 번호가 비어 있으면 유지하고, 아니면 가장 가까운 빈자리.
        void AssignSpots(int site, LadderDutyRole role, List<Vector3> spots, IReadOnlyList<LadderDefender> defenders,
            List<LadderDuty> result)
        {
            members.Clear();
            for (int i = 0; i < pairs.Count; i++)
            {
                int d = pairs[i].Defender;
                if (pairs[i].Site != site || result[d].Site != site || result[d].Role != role || result[d].Spot >= 0
                    || members.Contains(d)) continue;
                members.Add(d);
            }
            if (members.Count == 0) return;
            spotTaken.Clear();
            for (int i = 0; i < spots.Count; i++) spotTaken.Add(false);
            for (int m = 0; m < members.Count; m++)
            {
                int d = members[m];
                LadderDuty current = defenders[d].Current;
                if (current.Site != site || current.Role != role || current.Spot < 0 || current.Spot >= spots.Count
                    || spotTaken[current.Spot]) continue;
                spotTaken[current.Spot] = true;
                result[d] = new LadderDuty(site, role, current.Spot);
            }
            for (int m = 0; m < members.Count; m++)
            {
                int d = members[m];
                if (result[d].Spot >= 0) continue;
                int best = -1;
                float bestDistance = float.PositiveInfinity;
                for (int i = 0; i < spots.Count; i++)
                {
                    if (spotTaken[i]) continue;
                    float distance = (spots[i] - defenders[d].Position).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = i;
                }
                if (best < 0) { result[d] = LadderDuty.None; continue; }
                spotTaken[best] = true;
                result[d] = new LadderDuty(site, role, best);
            }
        }

        static void Fill(List<int> list, int count)
        {
            list.Clear();
            for (int i = 0; i < count; i++) list.Add(0);
        }
    }
}
