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
    }
}
