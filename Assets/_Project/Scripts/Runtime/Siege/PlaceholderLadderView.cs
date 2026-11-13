using UnityEngine;

namespace ProjectNeo.Siege
{
    /// <summary>
    /// 교체용 임시 사다리 외형. 세우기와 밀기 진행도에 맞춰 기울어진다.
    /// 사다리 상태를 읽기만 하며 규칙에 영향을 주지 않는다.
    /// </summary>
    public sealed class PlaceholderLadderView : MonoBehaviour
    {
        [SerializeField] SiegeLadder ladder;
        [SerializeField] Transform rails;
        [SerializeField, Min(0.1f)] float width = 0.7f;
        [SerializeField, Min(0.01f)] float thickness = 0.1f;
        [SerializeField, Min(0f)] float lift = 0.08f;

        public void Configure(SiegeLadder owner, Transform railBody) { ladder = owner; rails = railBody; }

        void LateUpdate()
        {
            if (ladder.State == null) return;
            Vector3 foot = ladder.BasePoint + Vector3.up * lift;
            Vector3 lean = ladder.EdgePoint - ladder.BasePoint;
            float length = lean.magnitude + 0.4f;
            // 바닥에서는 성 바깥쪽으로 누워 있고, 세우면 발을 축으로 성벽에 기댄다.
            Vector3 direction = ladder.CurrentDirection;
            // 사다리가 수직을 지나도 뒤집히지 않도록, 눕는 면 안에서 직각인 방향을 위쪽으로 준다.
            Vector3 side = Vector3.Cross(Vector3.up, ladder.OutwardWorld);
            transform.SetPositionAndRotation(foot, Quaternion.LookRotation(direction, Vector3.Cross(direction, side)));
            rails.localPosition = new Vector3(0f, 0f, length * 0.5f);
            rails.localScale = new Vector3(width, thickness, length);
        }
    }
}
