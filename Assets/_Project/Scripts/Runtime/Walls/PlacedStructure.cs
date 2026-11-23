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
        Collider[] bodies;
        /// <summary>
        /// 조각 루트의 충돌체 목록(PERF-ai-alloc-and-split). 처음 물을 때 한 번 찾아 두고 같은 배열을 돌려준다.
        /// 켜짐 여부는 바뀔 수 있으니 쓰는 쪽이 <see cref="Collider.enabled"/>를 본다. 배열은 고치지 않는다.
        /// </summary>
        public Collider[] Bodies => bodies ??= GetComponents<Collider>();
        /// <summary>준비 중 짓고 있는 조각(SIEGE-08). 이 위에는 조각을 쌓거나 무기를 올릴 수 없다.</summary>
        public bool IsUnderConstruction { get; private set; }
        public void SetUnderConstruction(bool value) => IsUnderConstruction = value;

        public void SetDoorParts(Collider collider, GameObject visual)
        {
            doorCollider = collider;
            doorVisual = visual;
            // 문짝 충돌체가 루트에 새로 붙었을 수 있으니 다음에 다시 찾는다.
            bodies = null;
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
