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
        // 부서져 열린 문. 수리되면 닫힌다(SIEGE-06).
        bool doorOpened;
        public BuildPlacement Placement { get; private set; }
        public Health Health { get; private set; }
        /// <summary>준비 단계에 일부러 열어 둔 성문(PREP-05). 부서짐과 따로 기억해 수리해도 열린 채로 둔다.</summary>
        public bool IsHeldOpen { get; private set; }
        /// <summary>문짝이 지금 열려 있다(부서졌거나 열어 두었다). 문짝이 없는 조각은 false.</summary>
        public bool IsDoorOpen => doorCollider != null && !doorCollider.enabled;
        public Collider DoorCollider => doorCollider;

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
            RefreshDoor();
        }

        /// <summary>문을 열어 두거나 닫는다. 부서진 문은 닫아도 열린 채다.</summary>
        public void SetHeldOpen(bool open)
        {
            IsHeldOpen = open;
            RefreshDoor();
        }

        void RefreshDoor()
        {
            bool open = doorOpened || IsHeldOpen;
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
