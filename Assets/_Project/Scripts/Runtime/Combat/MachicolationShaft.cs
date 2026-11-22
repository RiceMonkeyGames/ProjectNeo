using UnityEngine;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 마치콜레이션 구멍 아래로 뚫린 사격 통로(SIEGE-12). 사격 자리를 원점으로, +z가 성 바깥쪽, +y가 위인 좌표의 상자다.
    /// 이 상자 안에서는 성문 조각이 아래로 쏘는 화살과 사선을 막지 않는다.
    /// </summary>
    public readonly struct MachicolationShaft
    {
        /// <summary>면을 따라 잰 폭의 절반.</summary>
        public readonly float HalfWidth;
        /// <summary>안쪽 끝(사격 자리 기준 z, 보통 음수).</summary>
        public readonly float Inner;
        /// <summary>바깥쪽 끝(사격 자리 기준 z).</summary>
        public readonly float Outer;
        /// <summary>아래 끝(사격 자리 기준 y, 음수).</summary>
        public readonly float Bottom;
        /// <summary>위 끝(사격 자리 기준 y).</summary>
        public readonly float Top;

        public MachicolationShaft(float halfWidth, float inner, float outer, float bottom, float top)
        {
            HalfWidth = Mathf.Max(0f, halfWidth);
            Inner = Mathf.Min(inner, outer);
            Outer = Mathf.Max(inner, outer);
            Bottom = Mathf.Min(bottom, top);
            Top = Mathf.Max(bottom, top);
        }

        /// <summary>
        /// 성 바깥 면에서 잰 값으로 통로를 만든다. <paramref name="standInset"/>: 사격 자리가 바깥 면에서 안쪽으로 들어온 거리.
        /// <paramref name="inward"/>·<paramref name="outward"/>: 바깥 면에서 안쪽·바깥쪽으로 뻗는 길이.
        /// <paramref name="drop"/>: 사격 자리에서 땅까지 높이. 아래 끝은 땅보다 <paramref name="belowGround"/>만큼 더 내려간다.
        /// </summary>
        public static MachicolationShaft FromFace(float width, float standInset, float inward, float outward,
            float drop, float belowGround, float above) =>
            new MachicolationShaft(width * 0.5f, standInset - inward, standInset + outward,
                -Mathf.Max(0f, drop) - Mathf.Max(0f, belowGround), Mathf.Max(0f, above));

        /// <summary>사격 자리 기준 좌표의 점이 통로 안인지.</summary>
        public bool Contains(Vector3 local) =>
            Mathf.Abs(local.x) <= HalfWidth && local.z >= Inner && local.z <= Outer && local.y >= Bottom && local.y <= Top;

        /// <summary>땅 높이 근처(아래 끝 ~ 사격 자리 아래 <paramref name="belowStand"/>)에 선 발이 통로 아래쪽에 있는지.</summary>
        public bool IsBelow(Vector3 localFeet, float belowStand) =>
            Mathf.Abs(localFeet.x) <= HalfWidth && localFeet.z >= Inner && localFeet.z <= Outer
            && localFeet.y >= Bottom && localFeet.y <= -Mathf.Max(0f, belowStand);
    }
}
