using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 중지(자리 지키기) 중인 유닛이 따라갈 수 있는 길인지 높이로 가르는 규칙(BUG-wall-archer-leaves-wall).
    /// 길의 꺾이는 점이 하나라도 바닥 높이보다 낮으면 자리 높이를 벗어나는 길이다.
    /// </summary>
    public static class HoldDescentRule
    {
        public static bool StaysAbove(Vector3[] corners, float floorY)
        {
            if (corners == null) return true;
            for (int i = 0; i < corners.Length; i++)
                if (corners[i].y < floorY) return false;
            return true;
        }

        /// <summary>
        /// 후보의 도달 목표 높이가 자리 허용 바닥(<paramref name="floorY"/>)보다 확연히 아래인지 사전에 거른다(BUG-ai-core-defects).
        /// NavMesh 스냅 오차를 감안해 작은 여유(<paramref name="tolerance"/>)를 둔다.
        /// </summary>
        public static bool EndpointMayStayAbove(float targetY, float floorY, float tolerance = 0.25f) =>
            targetY >= floorY - tolerance;
    }
}
