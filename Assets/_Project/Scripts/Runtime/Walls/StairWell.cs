using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 한 칸 안에서 한 층을 오르는 꺾인 계단(SIEGE-27, 망루 방 SIEGE-29도 같이 쓴다). 씬을 알지 않는다.
    /// 칸을 계단 발치 면(<c>footFace</c>)에서 맞은편 면으로 보는 길이 u, 첫 계단 쪽 면(<c>firstFlightFace</c>)에서 맞은편으로 보는 폭 v로 나눈다.
    /// <list type="bullet">
    /// <item>발치 띠: 발치 면 안쪽, 칸 폭 전체. 그 층 계단이 시작하고 아래층 계단이 끝나는 바닥이다.</item>
    /// <item>첫 계단: 첫 계단 쪽 벽에 붙어 반 층 높이만큼 약 45°로 오른다(u 방향).</item>
    /// <item>층참: 맞은편 면 쪽, 칸 폭 전체, 반 층 높이.</item>
    /// <item>둘째 계단: 맞은편 벽에 붙어 층참에서 발치 쪽으로 반 층 더 올라 위층(또는 지붕) 바닥의 구멍으로 나온다.</item>
    /// <item>가운데 통로: 계단 폭을 좁히면 두 줄 사이가 발치 띠에서 층참 밑까지 이어진 바닥이 된다(모든 층에서 발치·맞은편 면을 잇는다).</item>
    /// </list>
    /// 층마다 같은 짜임을 그대로 쌓아 아래층 둘째 계단 끝이 위층 첫 계단 발치와 만난다. 위층 바닥 구멍은 둘째 계단 위만 뚫는다.
    /// 좌표는 성 좌표 m이다. 사각형은 x·z 평면(<see cref="Rect.x"/> = x, <see cref="Rect.y"/> = z)이다.
    /// </summary>
    public readonly struct StairWell
    {
        /// <summary>첫 계단 디딤면 가운데 선의 아래 끝(바닥 높이)과 위 끝(층참 높이).</summary>
        public readonly Vector3 FirstBottom, FirstTop;
        /// <summary>둘째 계단 디딤면 가운데 선의 아래 끝(층참 높이)과 위 끝(천장 높이).</summary>
        public readonly Vector3 SecondBottom, SecondTop;
        /// <summary>계단 한 줄의 폭(벽 안쪽에서 칸 가운데까지).</summary>
        public readonly float FlightWidth;
        public readonly Rect Landing;
        public readonly float LandingHeight;
        /// <summary>천장(위층 바닥)에서 둘째 계단 위로 뚫린 곳.</summary>
        public readonly Rect Hole;
        public readonly float Ceiling;
        /// <summary>
        /// 꺾이는 곳을 가로지르는 길찾기 연결의 두 끝(층참 가운데·발치 띠 가운데에서 첫 계단 줄 쪽, 둘째 계단 줄 쪽).
        /// 꺾이는 곳은 벽과 두 계단 사이 낭떠러지에 끼어, 길찾기 메시가 유닛 반경(0.5m)만큼 깎여 끊길 수 있다.
        /// 발치 띠 연결은 그 층 바닥 높이, 곧 아래층 둘째 계단이 끝나는 곳과 그 층 첫 계단이 시작하는 곳을 잇는다.
        /// </summary>
        public readonly Vector3 LandingFirst, LandingSecond, FootFirst, FootSecond;
        readonly Rect cell;

        StairWell(Vector3 firstBottom, Vector3 firstTop, Vector3 secondBottom, Vector3 secondTop, float flightWidth,
            Rect landing, float landingHeight, Rect hole, float ceiling, Rect cell,
            Vector3 landingFirst, Vector3 landingSecond, Vector3 footFirst, Vector3 footSecond)
        {
            FirstBottom = firstBottom; FirstTop = firstTop; SecondBottom = secondBottom; SecondTop = secondTop;
            FlightWidth = flightWidth; Landing = landing; LandingHeight = landingHeight; Hole = hole; Ceiling = ceiling;
            this.cell = cell;
            LandingFirst = landingFirst; LandingSecond = landingSecond; FootFirst = footFirst; FootSecond = footSecond;
        }

        /// <param name="cell">칸.</param>
        /// <param name="size">칸 크기(m).</param>
        /// <param name="floor">그 층 바닥 높이.</param>
        /// <param name="storeyHeight">층 높이. 계단 한 줄이 그 절반을 같은 거리만큼(45°) 오른다.</param>
        /// <param name="footFace">발치 면(0 북, 1 동, 2 남, 3 서).</param>
        /// <param name="firstFlightFace">첫 계단이 붙는 면. 발치 면과 직각이어야 한다.</param>
        /// <param name="wallInset">면에서 안쪽 벽면까지(벽판 두께 절반).</param>
        /// <param name="landingDepth">층참 깊이.</param>
        /// <param name="holeOverhang">천장 구멍을 둘째 계단 발치에서 층참 쪽으로 더 넓히는 거리. 층참에서 둘째 계단을 오르기 시작할 때
        /// 플레이어 몸통(반경)의 뒤쪽 위가 천장 끝에 걸리지 않게 한다.</param>
        /// <param name="flightWidth">계단 한 줄의 폭. 0 이하면 안쪽 폭의 절반(두 줄이 맞붙는다). 좁히면 두 줄 사이가 바닥 통로로 남는다.</param>
        public static StairWell Create(Vector2Int cell, float size, float floor, float storeyHeight, int footFace, int firstFlightFace,
            float wallInset, float landingDepth, float holeOverhang = 0f, float flightWidth = 0f)
        {
            Vector2Int f = BattlementRules.Directions[Mod4(footFace)], a = BattlementRules.Directions[Mod4(firstFlightFace)];
            var foot = new Vector3(f.x, 0f, f.y);
            var first = new Vector3(a.x, 0f, a.y);
            var origin = new Vector3((cell.x + 0.5f) * size, floor, (cell.y + 0.5f) * size) + (foot + first) * (size * 0.5f);
            Vector3 Point(float u, float v, float y) => origin - foot * u - first * v + Vector3.up * y;
            Rect Area(float u0, float u1, float v0, float v1)
            {
                Vector3 p = Point(u0, v0, 0f), q = Point(u1, v1, 0f);
                return Rect.MinMaxRect(Mathf.Min(p.x, q.x), Mathf.Min(p.z, q.z), Mathf.Max(p.x, q.x), Mathf.Max(p.z, q.z));
            }

            float half = storeyHeight * 0.5f, run = half;
            float inner = size - 2f * wallInset;
            float landing = Mathf.Clamp(landingDepth, 0f, Mathf.Max(0f, inner - run));
            float strip = Mathf.Max(0f, inner - run - landing);
            float u0 = wallInset + strip, u1 = u0 + run;
            float width = flightWidth > 0f ? Mathf.Min(flightWidth, inner * 0.5f) : inner * 0.5f;
            float lane1 = wallInset + width * 0.5f, lane2 = size - wallInset - width * 0.5f;
            float landingMid = (u1 + size - wallInset) * 0.5f, footMid = (wallInset + u0) * 0.5f;
            return new StairWell(
                Point(u0, lane1, 0f), Point(u1, lane1, half),
                Point(u1, lane2, half), Point(u0, lane2, storeyHeight),
                width,
                Area(u1, size - wallInset, wallInset, size - wallInset), floor + half,
                Area(u0, Mathf.Min(u1 + Mathf.Max(0f, holeOverhang), size - wallInset), size - wallInset - width, size), floor + storeyHeight,
                Area(0f, size, 0f, size),
                Point(landingMid, lane1, half), Point(landingMid, lane2, half),
                Point(footMid, lane1, 0f), Point(footMid, lane2, 0f));
        }

        /// <summary>천장(위층 바닥)에서 구멍을 뺀 나머지를 사각형으로 나눠 <paramref name="result"/>에 더한다(구멍이 한 면에 닿아 셋).</summary>
        public void CeilingAreas(List<Rect> result) => Subtract(result, cell, Hole);

        // 사각형 whole에서 hole을 뺀 나머지를 사각형으로(겹치는 부분 기준으로 최대 넷) 더한다.
        static void Subtract(List<Rect> result, Rect whole, Rect hole)
        {
            float x0 = Mathf.Max(whole.xMin, hole.xMin), x1 = Mathf.Min(whole.xMax, hole.xMax);
            float z0 = Mathf.Max(whole.yMin, hole.yMin), z1 = Mathf.Min(whole.yMax, hole.yMax);
            if (x1 <= x0 || z1 <= z0) { result.Add(whole); return; }
            Keep(result, Rect.MinMaxRect(whole.xMin, whole.yMin, x0, whole.yMax));
            Keep(result, Rect.MinMaxRect(x1, whole.yMin, whole.xMax, whole.yMax));
            Keep(result, Rect.MinMaxRect(x0, whole.yMin, x1, z0));
            Keep(result, Rect.MinMaxRect(x0, z1, x1, whole.yMax));
        }

        static void Keep(List<Rect> result, Rect area)
        {
            if (area.width > 1e-4f && area.height > 1e-4f) result.Add(area);
        }

        static int Mod4(int value) => ((value % 4) + 4) % 4;
    }
}
