using ProjectNeo.Combat;
using ProjectNeo.Stats;
using UnityEngine;

namespace ProjectNeo.Walls
{
    public sealed class PlacedStructure : MonoBehaviour
    {
        [SerializeField] Collider doorCollider;
        [SerializeField] GameObject doorVisual;
        ConstructionWorld world;
        bool doorOpened;
        public BuildPlacement Placement { get; private set; }
        public Health Health { get; private set; }

        public void SetDoorParts(Collider collider, GameObject visual)
        {
            doorCollider = collider;
            doorVisual = visual;
        }

        public void Initialize(ConstructionWorld owner, BuildPlacement placement)
        {
            world = owner;
            Placement = placement;
            var stats = new StatSheet();
            stats.SetBase(StatType.MaxHealth, placement.Definition.MaxHealth);
            Health = new Health(stats);
            Health.Died += Collapse;
            Health.Changed += OnHealthChanged;
        }

        public void ApplyDoorState(bool open)
        {
            doorOpened = open;
            if (doorCollider != null) doorCollider.enabled = !open;
            if (doorVisual != null) doorVisual.SetActive(!open);
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
        public bool TryGetNearestMount(Vector3 worldPoint, out string id, out Pose pose)
        {
            id = null; pose = default;
            float nearest = float.PositiveInfinity;
            foreach (BuildMountPoint point in Placement.Definition.MountPoints)
            {
                float distance = (transform.TransformPoint(point.normalizedPosition) - worldPoint).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; id = point.id;
            }
            return id != null && TryGetMountPose(id, out pose);
        }

        void Collapse()
        {
            if (Placement != null && Placement.Definition.Kind == BuildPieceKind.Gate) world.BreachGate(this);
            else world.Collapse(this);
        }

        void OnHealthChanged()
        {
            if (doorOpened && Health != null && !Health.IsDead
                && Placement != null && Placement.Definition.Kind == BuildPieceKind.Gate)
                world.NotifyGateRepaired(this);
        }

        void OnDestroy()
        {
            if (Health == null) return;
            Health.Died -= Collapse;
            Health.Changed -= OnHealthChanged;
            Health.Dispose();
        }
    }
}
