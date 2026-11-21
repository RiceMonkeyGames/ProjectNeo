using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Squads
{
    /// <summary>
    /// 전열을 끄는 동안 병사가 설 자리(COMBAT-08). 입력이 채우고 표시 부품이 읽기만 한다.
    /// </summary>
    public sealed class FormationPreview
    {
        readonly List<Vector3> slots = new List<Vector3>();
        public IReadOnlyList<Vector3> Slots => slots;
        public bool Active { get; private set; }

        public void Begin() { slots.Clear(); Active = true; }
        public void Add(IReadOnlyList<Vector3> values) { for (int i = 0; i < values.Count; i++) slots.Add(values[i]); }
        public void Clear() { slots.Clear(); Active = false; }
    }
}
