using System.Collections.Generic;

namespace ProjectNeo.Walls
{
    public enum MountIssue
    {
        None = 0, NoWeapon = 1, NoSupport = 2, NoMountPoint = 3, Occupied = 4,
    }

    public sealed class MountedWeapon
    {
        public int Id { get; }
        public SiegeWeaponDefinition Definition { get; }
        public int SupportId { get; }
        public string MountId { get; }
        public MountedWeapon(int id, SiegeWeaponDefinition definition, int supportId, string mountId)
        {
            Id = id; Definition = definition; SupportId = supportId; MountId = mountId;
        }
    }

    /// <summary>
    /// 설치 지점 점유 규칙. 바닥 칸 점유(BuildLayout)와 별개로 "지지 조각 ID + 지점 ID"마다 무기 하나를 둔다.
    /// 무기 종류를 구분하지 않으며 씬과 외형은 알지 않는다.
    /// </summary>
    public sealed class MountLayout
    {
        readonly BuildLayout layout;
        readonly Dictionary<(int, string), MountedWeapon> slots = new Dictionary<(int, string), MountedWeapon>();
        readonly Dictionary<int, MountedWeapon> mounted = new Dictionary<int, MountedWeapon>();
        int nextId = 1;
        public IReadOnlyCollection<MountedWeapon> Mounted => mounted.Values;
        public MountLayout(BuildLayout layout) => this.layout = layout;

        public MountedWeapon At(int supportId, string mountId) =>
            mountId != null && slots.TryGetValue((supportId, mountId), out var value) ? value : null;

        public MountIssue Check(SiegeWeaponDefinition definition, int supportId, string mountId)
        {
            if (definition == null) return MountIssue.NoWeapon;
            BuildPlacement support = layout.Find(supportId);
            if (support == null) return MountIssue.NoSupport;
            if (!HasPoint(support.Definition, mountId)) return MountIssue.NoMountPoint;
            // 위에 다른 조각이 얹힌 조각의 설치 지점은 막혀 있다.
            if (layout.IsCovered(support)) return MountIssue.Occupied;
            return slots.ContainsKey((supportId, mountId)) ? MountIssue.Occupied : MountIssue.None;
        }

        public bool TryMount(SiegeWeaponDefinition definition, int supportId, string mountId, out MountedWeapon weapon)
        {
            weapon = null;
            if (Check(definition, supportId, mountId) != MountIssue.None) return false;
            weapon = new MountedWeapon(nextId++, definition, supportId, mountId);
            mounted.Add(weapon.Id, weapon);
            slots.Add((supportId, mountId), weapon);
            return true;
        }

        public bool HasWeaponOn(int supportId)
        {
            foreach (MountedWeapon weapon in mounted.Values)
                if (weapon.SupportId == supportId) return true;
            return false;
        }

        public bool Remove(int id)
        {
            if (!mounted.TryGetValue(id, out var weapon)) return false;
            mounted.Remove(id);
            slots.Remove((weapon.SupportId, weapon.MountId));
            return true;
        }

        /// <summary>지지 조각이 사라질 때 그 위의 무기를 모두 빼고 돌려준다.</summary>
        public List<MountedWeapon> RemoveSupportedBy(int supportId)
        {
            var removed = new List<MountedWeapon>();
            foreach (MountedWeapon weapon in mounted.Values)
                if (weapon.SupportId == supportId) removed.Add(weapon);
            foreach (MountedWeapon weapon in removed) Remove(weapon.Id);
            return removed;
        }

        static bool HasPoint(BuildPieceDefinition piece, string mountId)
        {
            if (string.IsNullOrEmpty(mountId)) return false;
            foreach (BuildMountPoint point in piece.MountPoints)
                if (point.id == mountId) return true;
            return false;
        }
    }
}
