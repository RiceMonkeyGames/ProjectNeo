using System;
using System.Collections.Generic;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Run
{
    /// <summary>성 조각 하나의 기록. 체력은 깎인 채로 다음 전투에 이어진다.</summary>
    public readonly struct CastlePieceState
    {
        public BuildPieceDefinition Definition { get; }
        public Vector2Int Cell { get; }
        public int Rotation { get; }
        public int Level { get; }
        public bool IsFixed { get; }
        public float Health { get; }

        public CastlePieceState(BuildPieceDefinition definition, Vector2Int cell, int rotation, int level,
            bool isFixed, float health)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            Cell = cell;
            Rotation = ((rotation % 4) + 4) % 4;
            Level = Mathf.Max(0, level);
            IsFixed = isFixed;
            Health = Mathf.Clamp(health, 0f, definition.MaxHealth);
        }
    }

    /// <summary>
    /// 수성 무기 하나의 기록. 받치는 조각은 <see cref="CastleState.Pieces"/>의 순번으로 가리킨다.
    /// 체력은 성벽처럼 깎인 채로 다음 전투에 이어진다(SIEGE-02). 체력을 주지 않으면 최대 체력이다.
    /// </summary>
    public readonly struct CastleWeaponState
    {
        public SiegeWeaponDefinition Definition { get; }
        public int SupportIndex { get; }
        public string MountId { get; }
        /// <summary>설치 지점 방향에서 더 돌린 90도 단위 횟수(0~3).</summary>
        public int Rotation { get; }
        public float Health { get; }

        public CastleWeaponState(SiegeWeaponDefinition definition, int supportIndex, string mountId,
            int rotation = 0, float? health = null)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            SupportIndex = supportIndex;
            MountId = mountId;
            Rotation = MountedWeapon.NormalizeRotation(rotation);
            Health = Mathf.Clamp(health ?? definition.MaxHealth, 0f, definition.MaxHealth);
        }
    }

    /// <summary>
    /// 런이 들고 있는 성의 배치·체력 기록. 씬의 조각 번호(BuildPlacement.Id)는 씬 실행 중에만 쓰므로 담지 않는다.
    /// 조각은 아래층부터 정렬되어 있어 그 순서대로 놓으면 같은 성이 된다.
    /// 부서진 조각은 기록에 없다. 부서진 성문 자리의 보호(빈 자리 신규 배치 금지)는 아직 복원하지 않는다.
    /// </summary>
    public sealed class CastleState
    {
        readonly List<CastlePieceState> pieces;
        readonly List<CastleWeaponState> weapons;

        public IReadOnlyList<CastlePieceState> Pieces => pieces;
        public IReadOnlyList<CastleWeaponState> Weapons => weapons;

        public CastleState() : this(new List<CastlePieceState>(), new List<CastleWeaponState>()) { }

        CastleState(List<CastlePieceState> pieces, List<CastleWeaponState> weapons)
        {
            this.pieces = pieces;
            this.weapons = weapons;
        }

        /// <summary>
        /// 이미 있는 기록으로 성 상태를 만든다(FLOW-02 저장 파일). 조각은 아래층부터 놓을 순서여야 한다.
        /// </summary>
        public static CastleState FromRecords(IEnumerable<CastlePieceState> pieceRecords, IEnumerable<CastleWeaponState> weaponRecords)
        {
            return new CastleState(
                pieceRecords != null ? new List<CastlePieceState>(pieceRecords) : new List<CastlePieceState>(),
                weaponRecords != null ? new List<CastleWeaponState>(weaponRecords) : new List<CastleWeaponState>());
        }

        public bool HasGate
        {
            get
            {
                foreach (CastlePieceState piece in pieces)
                    if (piece.Definition.Kind == BuildPieceKind.Gate) return true;
                return false;
            }
        }

        public CastleState Clone() =>
            new CastleState(new List<CastlePieceState>(pieces), new List<CastleWeaponState>(weapons));

        /// <summary>
        /// 지금 배치를 기록한다. <paramref name="healthOf"/>는 조각의, <paramref name="weaponHealthOf"/>는 무기의
        /// 현재 체력이며 null이면 최대 체력으로 기록한다.
        /// </summary>
        public static CastleState Capture(BuildLayout layout, MountLayout mounts, Func<BuildPlacement, float> healthOf = null,
            Func<MountedWeapon, float> weaponHealthOf = null)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var ordered = new List<BuildPlacement>(layout.Placements);
            ordered.Sort((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level) : a.Id.CompareTo(b.Id));

            var pieceList = new List<CastlePieceState>(ordered.Count);
            var indexById = new Dictionary<int, int>();
            foreach (BuildPlacement placement in ordered)
            {
                float health = healthOf != null ? healthOf(placement) : placement.Definition.MaxHealth;
                indexById[placement.Id] = pieceList.Count;
                pieceList.Add(new CastlePieceState(placement.Definition, placement.Cell, placement.Rotation,
                    placement.Level, placement.IsFixed, health));
            }

            var weaponList = new List<CastleWeaponState>();
            if (mounts != null)
            {
                var mounted = new List<MountedWeapon>(mounts.Mounted);
                mounted.Sort((a, b) => a.Id.CompareTo(b.Id));
                foreach (MountedWeapon weapon in mounted)
                    if (indexById.TryGetValue(weapon.SupportId, out int index))
                        weaponList.Add(new CastleWeaponState(weapon.Definition, index, weapon.MountId, weapon.Rotation,
                            weaponHealthOf != null ? weaponHealthOf(weapon) : (float?)null));
            }
            return new CastleState(pieceList, weaponList);
        }

        /// <summary>
        /// 빈 격자에 기록대로 조각과 무기를 다시 놓는다. <paramref name="placed"/>는 <see cref="Pieces"/>와 같은 순서다.
        /// 기록과 다른 층에 놓이거나 놓을 수 없으면 false이며, 그때 격자는 버리고 새로 만든다.
        /// 체력은 씬이 <see cref="CastlePieceState.Health"/>·<see cref="CastleWeaponState.Health"/>를 읽어 적용한다.
        /// </summary>
        public bool TryRestore(BuildLayout layout, MountLayout mounts, out List<BuildPlacement> placed)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            placed = new List<BuildPlacement>(pieces.Count);
            if (layout.Placements.Count > 0) return false;
            foreach (CastlePieceState piece in pieces)
            {
                if (!layout.TryPlace(piece.Definition, piece.Cell, piece.Rotation, out BuildPlacement placement,
                        piece.IsFixed && piece.Definition.Kind == BuildPieceKind.Gate)) return false;
                if (placement.Level != piece.Level) return false;
                placed.Add(placement);
            }
            if (weapons.Count == 0) return true;
            if (mounts == null) return false;
            foreach (CastleWeaponState weapon in weapons)
            {
                if (weapon.SupportIndex < 0 || weapon.SupportIndex >= placed.Count) return false;
                if (!mounts.TryMount(weapon.Definition, placed[weapon.SupportIndex].Id, weapon.MountId, out _,
                        weapon.Rotation)) return false;
            }
            return true;
        }
    }
}
