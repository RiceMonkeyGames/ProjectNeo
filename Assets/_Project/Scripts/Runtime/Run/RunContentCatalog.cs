using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Walls;
using UnityEngine;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 런 저장에 나올 수 있는 정의 에셋의 목록(FLOW-02). 저장 파일의 ID를 여기서 찾아 에셋으로 바꾼다.
    /// 에디터 메뉴(ProjectNeo/Setup/Refresh Run Save Content)가 프로젝트의 정의 에셋을 모두 모아 채우므로,
    /// 새 아이템·영웅·조각을 만든 뒤에는 그 메뉴를 다시 실행한다. 성 조각은 ID 필드가 없어 에셋 이름을 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Run/Run Content Catalog")]
    public sealed class RunContentCatalog : ScriptableObject, IRunContentLookup
    {
        [SerializeField] List<ItemDefinition> items = new List<ItemDefinition>();
        [SerializeField] List<MaterialDefinition> materials = new List<MaterialDefinition>();
        [SerializeField] List<HeroDefinition> heroes = new List<HeroDefinition>();
        [SerializeField] List<SoldierDefinition> soldiers = new List<SoldierDefinition>();
        [SerializeField] List<FacilityDefinition> facilities = new List<FacilityDefinition>();
        [SerializeField] List<BuildPieceDefinition> pieces = new List<BuildPieceDefinition>();
        [Tooltip("성 조각의 저장용 ID. pieces와 같은 순서다. 에디터 메뉴가 에셋 GUID로 채운다(이름이 같은 조각이 있어도 겹치지 않고, 이름을 바꿔도 유지된다).")]
        [SerializeField] List<string> pieceIds = new List<string>();
        [SerializeField] List<SiegeWeaponDefinition> weapons = new List<SiegeWeaponDefinition>();
        [Tooltip("런에서 찍을 수 있는 퍽(META-02). 에디터 메뉴가 프로젝트의 퍽 에셋으로 채운다.")]
        [SerializeField] List<ProjectNeo.Meta.PerkDefinition> perks = new List<ProjectNeo.Meta.PerkDefinition>();
        [Tooltip("업데이트에서 바뀐 콘텐츠 ID 표(META-10). 런 저장을 되살리기 전에 옛 ID를 새 ID로 바꾼다. 해금 목록과 같은 에셋을 쓴다.")]
        [SerializeField] ProjectNeo.Core.ContentRenameTable renames;

        /// <summary>콘텐츠 ID 이름 바꾸기 표(META-10). 없으면 이름을 바꾸지 않는다.</summary>
        public ProjectNeo.Core.ContentRenameTable Renames => renames;
        public void SetRenames(ProjectNeo.Core.ContentRenameTable table) => renames = table;

        public IReadOnlyList<ItemDefinition> Items => items;
        public IReadOnlyList<MaterialDefinition> Materials => materials;
        public IReadOnlyList<HeroDefinition> Heroes => heroes;
        public IReadOnlyList<SoldierDefinition> Soldiers => soldiers;
        public IReadOnlyList<FacilityDefinition> Facilities => facilities;
        public IReadOnlyList<BuildPieceDefinition> Pieces => pieces;
        public IReadOnlyList<SiegeWeaponDefinition> Weapons => weapons;
        public IReadOnlyList<ProjectNeo.Meta.PerkDefinition> Perks => perks;

        public ItemDefinition Item(string id) => Find(items, id, item => item.Id);
        public MaterialDefinition Material(string id) => Find(materials, id, material => material.Id);
        public HeroDefinition Hero(string id) => Find(heroes, id, hero => hero.Id);
        public SoldierDefinition Soldier(string id) => Find(soldiers, id, soldier => soldier.Id);
        public BuildPieceDefinition Piece(string id) => Find(pieces, id, PieceId);
        public SiegeWeaponDefinition Weapon(string id) => Find(weapons, id, weapon => weapon.Id);
        public ProjectNeo.Meta.PerkDefinition Perk(string id) => Find(perks, id, perk => perk.Id);

        /// <summary>퍽 목록을 바꾼다(META-02). 에디터 설정 스크립트와 테스트가 쓴다. <see cref="Set"/>은 퍽을 건드리지 않는다.</summary>
        public void SetPerks(IEnumerable<ProjectNeo.Meta.PerkDefinition> newPerks) => Fill(perks, newPerks);
        /// <summary>조각의 저장용 ID. 목록에 적힌 ID가 있으면 그것을, 없으면 에셋 이름을 쓴다.</summary>
        public string PieceId(BuildPieceDefinition piece)
        {
            if (piece == null) return null;
            int index = pieces.IndexOf(piece);
            return index >= 0 && index < pieceIds.Count && !string.IsNullOrEmpty(pieceIds[index]) ? pieceIds[index] : piece.name;
        }

        /// <summary>성 조각의 저장용 ID를 pieces와 같은 순서로 정한다. 에디터 설정 스크립트가 에셋 GUID를 넣는다.</summary>
        public void SetPieceIds(IEnumerable<string> ids)
        {
            pieceIds.Clear();
            if (ids != null) pieceIds.AddRange(ids);
        }

        public FacilityDefinition Facility(FacilityType type)
        {
            foreach (FacilityDefinition facility in facilities)
                if (facility != null && facility.Type == type) return facility;
            return null;
        }

        static T Find<T>(List<T> list, string id, System.Func<T, string> idOf) where T : Object
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (T entry in list)
                if (entry != null && idOf(entry) == id) return entry;
            return null;
        }

        /// <summary>
        /// 같은 종류 안에서 ID가 비었거나 겹치는 항목을 설명한 글. 없으면 빈 목록. 겹치면 저장을 되살릴 때 다른 에셋이 나올 수 있다.
        /// </summary>
        public List<string> FindIdProblems()
        {
            var problems = new List<string>();
            Check(items, item => item.Id, "Item", problems);
            Check(materials, material => material.Id, "Material", problems);
            Check(heroes, hero => hero.Id, "Hero", problems);
            Check(soldiers, soldier => soldier.Id, "Soldier", problems);
            Check(pieces, PieceId, "Piece", problems);
            Check(weapons, weapon => weapon.Id, "Weapon", problems);
            Check(perks, perk => perk.Id, "Perk", problems);
            var types = new HashSet<FacilityType>();
            foreach (FacilityDefinition facility in facilities)
                if (facility != null && !types.Add(facility.Type)) problems.Add("Facility type appears twice: " + facility.Type);
            return problems;
        }

        static void Check<T>(List<T> list, System.Func<T, string> idOf, string kind, List<string> problems) where T : Object
        {
            var seen = new HashSet<string>();
            foreach (T entry in list)
            {
                if (entry == null) continue;
                string id = idOf(entry);
                if (string.IsNullOrEmpty(id)) problems.Add($"{kind} has an empty id: {entry.name}");
                else if (!seen.Add(id)) problems.Add($"{kind} id appears twice: {id}");
            }
        }

        public static RunContentCatalog Create(IEnumerable<ItemDefinition> items = null,
            IEnumerable<MaterialDefinition> materials = null, IEnumerable<HeroDefinition> heroes = null,
            IEnumerable<SoldierDefinition> soldiers = null, IEnumerable<FacilityDefinition> facilities = null,
            IEnumerable<BuildPieceDefinition> pieces = null, IEnumerable<SiegeWeaponDefinition> weapons = null)
        {
            var catalog = CreateInstance<RunContentCatalog>();
            catalog.Set(items, materials, heroes, soldiers, facilities, pieces, weapons);
            return catalog;
        }

        /// <summary>목록을 통째로 바꾼다. 에디터 설정 스크립트와 테스트가 쓴다.</summary>
        public void Set(IEnumerable<ItemDefinition> newItems, IEnumerable<MaterialDefinition> newMaterials,
            IEnumerable<HeroDefinition> newHeroes, IEnumerable<SoldierDefinition> newSoldiers,
            IEnumerable<FacilityDefinition> newFacilities, IEnumerable<BuildPieceDefinition> newPieces,
            IEnumerable<SiegeWeaponDefinition> newWeapons)
        {
            Fill(items, newItems); Fill(materials, newMaterials); Fill(heroes, newHeroes); Fill(soldiers, newSoldiers);
            Fill(facilities, newFacilities); Fill(pieces, newPieces); Fill(weapons, newWeapons);
            pieceIds.Clear();
        }

        static void Fill<T>(List<T> target, IEnumerable<T> source) where T : Object
        {
            target.Clear();
            if (source == null) return;
            foreach (T entry in source)
                if (entry != null && !target.Contains(entry)) target.Add(entry);
        }
    }
}
