using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Squads
{
    /// <summary>
    /// 준비 단계에서 부대마다 마지막으로 배치한 자리(PREP-05). 다음 준비 단계가 시작되면 이 자리로 돌려보낸다.
    /// 부대 번호로 기억하고, 부대가 없어지면 뒤 부대의 기억을 한 칸씩 당긴다. 씬은 알지 않는다.
    /// </summary>
    public sealed class SquadPlacementMemory
    {
        readonly List<Entry?> entries = new List<Entry?>();

        public readonly struct Entry
        {
            public Vector3 Position { get; }
            public float Facing { get; }
            public Entry(Vector3 position, float facing) { Position = position; Facing = facing; }
        }

        public void Remember(int squad, Vector3 position, float facing)
        {
            if (squad < 0) return;
            while (entries.Count <= squad) entries.Add(null);
            entries[squad] = new Entry(position, facing);
        }

        public bool TryGet(int squad, out Entry entry)
        {
            entry = default;
            if (squad < 0 || squad >= entries.Count || !entries[squad].HasValue) return false;
            entry = entries[squad].Value;
            return true;
        }

        public void Forget(int squad)
        {
            if (squad >= 0 && squad < entries.Count) entries[squad] = null;
        }

        /// <summary>부대가 없어졌다. 뒤 부대 번호가 하나씩 당겨지므로 기억도 따라 당긴다.</summary>
        public void RemoveSquad(int squad)
        {
            if (squad >= 0 && squad < entries.Count) entries.RemoveAt(squad);
        }

        public void Clear() => entries.Clear();
    }
}
