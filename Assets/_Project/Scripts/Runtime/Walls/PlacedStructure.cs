using ProjectNeo.Combat;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Walls
{
    public sealed class PlacedStructure : MonoBehaviour
    {
        ConstructionWorld world;
        public BuildPlacement Placement { get; private set; }
        public Health Health { get; private set; }

        public void Initialize(ConstructionWorld owner, BuildPlacement placement)
        {
            world = owner;
            Placement = placement;
            var stats = new StatSheet();
            stats.SetBase(StatType.MaxHealth, placement.Definition.MaxHealth);
            Health = new Health(stats);
            Health.Died += Collapse;
        }

        public void ReceiveDamage(float amount) => Health?.TakeDamage(amount);
        public bool TryGetMountPose(string id, out Pose pose)
        {
            foreach (BuildMountPoint point in Placement.Definition.MountPoints)
                if (point.id == id)
                {
                    pose = new Pose(transform.TransformPoint(point.normalizedPosition),
                        transform.rotation * Quaternion.Euler(point.localEulerAngles));
                    return true;
                }
            pose = default;
            return false;
        }
        void Collapse() => world.Collapse(this);

        void OnDestroy()
        {
            if (Health == null) return;
            Health.Died -= Collapse;
            Health.Dispose();
        }
    }
}
