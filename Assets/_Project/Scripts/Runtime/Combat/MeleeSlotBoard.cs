using System;
using System.Collections.Generic;

namespace ProjectNeo.Combat
{
    /// <summary>
    /// 누가 어느 대상의 어느 근접 자리(링, 번호)를 맡았는지(COMBAT-12). 한 유닛은 한 자리만 맡는다.
    /// 맡은 유닛이 더는 그 대상을 노리지 않거나 쓸 수 없는 상태면(<paramref name="stillValid"/>가 false)
    /// 그 자리는 빈 것으로 보고 정리한다. 그래서 대상을 바꾸는 모든 길에서 따로 놓아 줄 필요가 없다.
    /// </summary>
    public sealed class MeleeSlotBoard<TUnit> where TUnit : class
    {
        readonly struct Claim
        {
            public readonly object Target;
            public readonly int Ring;
            public readonly int Index;
            public Claim(object target, int ring, int index) { Target = target; Ring = ring; Index = index; }
        }

        readonly Dictionary<TUnit, Claim> claims = new Dictionary<TUnit, Claim>();
        readonly List<TUnit> stale = new List<TUnit>();

        public int Count => claims.Count;
        /// <summary>자리를 맡거나 놓을 때마다 오른다(COMBAT-17 혼잡 수 다시 세기).</summary>
        public int Revision { get; private set; }

        /// <summary>
        /// <paramref name="asker"/>가 아닌 다른 유효한 유닛이 그 자리를 맡았는지. 유효하지 않은 맡음은 지운다.
        /// <paramref name="stillValid"/>(unit, target): 그 유닛이 아직 그 대상을 노리는 쓸 수 있는 유닛인지.
        /// </summary>
        public bool IsTaken(object target, int ring, int index, TUnit asker, Func<TUnit, object, bool> stillValid)
        {
            stale.Clear();
            bool taken = false;
            foreach (KeyValuePair<TUnit, Claim> entry in claims)
            {
                if (!ReferenceEquals(entry.Value.Target, target) || entry.Value.Ring != ring || entry.Value.Index != index) continue;
                if (ReferenceEquals(entry.Key, asker)) continue;
                if (stillValid != null && !stillValid(entry.Key, target)) { stale.Add(entry.Key); continue; }
                taken = true;
            }
            foreach (TUnit unit in stale) claims.Remove(unit);
            return taken;
        }

        /// <summary>그 자리를 맡는다. 전에 맡은 자리는 놓는다.</summary>
        public void Take(TUnit unit, object target, int ring, int index)
        {
            if (unit == null) return;
            claims[unit] = new Claim(target, ring, index);
            Revision++;
        }

        public void Release(TUnit unit)
        {
            if (unit != null && claims.Remove(unit)) Revision++;
        }

        /// <summary>그 유닛이 그 대상에서 맡은 자리. 다른 대상의 자리면 false.</summary>
        public bool TryGet(TUnit unit, object target, out int ring, out int index)
        {
            ring = index = -1;
            if (unit == null || !claims.TryGetValue(unit, out Claim claim) || !ReferenceEquals(claim.Target, target)) return false;
            ring = claim.Ring;
            index = claim.Index;
            return true;
        }

        /// <summary>
        /// 대상마다 그 링에서 유효한 유닛이 맡은 자리 수를 <paramref name="output"/>에 센다(COMBAT-17 근접 혼잡).
        /// 유효하지 않은 맡음은 지운다.
        /// </summary>
        public void CountByTarget(int ring, Func<TUnit, object, bool> stillValid, Dictionary<object, int> output)
        {
            output.Clear();
            stale.Clear();
            foreach (KeyValuePair<TUnit, Claim> entry in claims)
            {
                if (stillValid != null && !stillValid(entry.Key, entry.Value.Target)) { stale.Add(entry.Key); continue; }
                if (entry.Value.Ring != ring) continue;
                output.TryGetValue(entry.Value.Target, out int count);
                output[entry.Value.Target] = count + 1;
            }
            foreach (TUnit unit in stale) claims.Remove(unit);
        }

        public void Clear()
        {
            claims.Clear();
            Revision++;
        }
    }
}
