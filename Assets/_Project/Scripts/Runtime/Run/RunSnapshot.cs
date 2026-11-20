using System;
using System.Collections.Generic;

namespace ProjectNeo.Run
{
    /// <summary>
    /// 저장 파일에 들어가는 런 한 개의 기록(FLOW-02). 에셋 참조 대신 정의 ID만 담아 파일로 쓸 수 있다.
    /// 규칙은 없고 값만 있다. 만들기는 <see cref="RunState.Capture"/>, 되살리기는 <see cref="RunState.Restore"/>가 한다.
    /// 항목을 더할 때는 새 필드를 추가하면 되고(없는 필드는 기본값으로 읽힌다), 뜻이 바뀌면 저장 버전을 올린다.
    /// </summary>
    [Serializable]
    public sealed class RunSnapshot
    {
        /// <summary>이어 하면 준비 단계로 들어갈 스테이지(1부터).</summary>
        public int stageNumber = 1;
        public int seed;
        public int battlesFought;
        public int lastRewardedBattle;
        public int nextUnitId = 1;
        public int gold;
        public List<MaterialRecord> materials = new List<MaterialRecord>();
        /// <summary>보관함에 있는 아이템. 착용한 장비는 영웅 기록에 있다.</summary>
        public List<ItemRecord> stash = new List<ItemRecord>();
        public List<HeroRecord> heroes = new List<HeroRecord>();
        public List<HeroRecord> fallenHeroes = new List<HeroRecord>();
        public List<SquadRecord> squads = new List<SquadRecord>();
        /// <summary>어느 부대에도 없는 잡졸.</summary>
        public List<SoldierRecord> looseSoldiers = new List<SoldierRecord>();
        public List<FacilityRecord> facilities = new List<FacilityRecord>();
        public ShopRecord shop = new ShopRecord();
        public TavernRecord tavern = new TavernRecord();
        public CastleRecord castle = new CastleRecord();
        /// <summary>준비 시계(PREP-06). 예전 저장에는 없어서 기본값(-1)이면 다음 준비에서 예산을 새로 채운다.</summary>
        public PrepTimeRecord prepTime = new PrepTimeRecord();
        /// <summary>이번 런에서 찍은 퍽 id(META-02). 예전 저장에는 없어서 비어 있다.</summary>
        public List<string> perks = new List<string>();
    }

    [Serializable]
    public sealed class PrepTimeRecord
    {
        /// <summary>시계를 시작한 준비 번호. -1이면 아직 시작하지 않았다.</summary>
        public int preparation = -1;
        public float budget;
        public float used;
    }

    [Serializable]
    public struct MaterialRecord
    {
        public string id;
        public int amount;
    }

    [Serializable]
    public struct ItemRecord
    {
        public string id;
        public int upgradeLevel;
        public long upgradeGoldSpent;
    }

    [Serializable]
    public sealed class HeroRecord
    {
        public int unitId;
        public string id;
        public bool isPlayer;
        public List<ItemRecord> equipment = new List<ItemRecord>();
    }

    [Serializable]
    public struct SoldierRecord
    {
        public int unitId;
        public string id;
    }

    [Serializable]
    public sealed class SquadRecord
    {
        /// <summary>대장 영웅의 런 번호. 없으면 0.</summary>
        public int leaderUnitId;
        public List<SoldierRecord> soldiers = new List<SoldierRecord>();
        public bool hasMorale;
        public float moraleMax;
        public float morale;
    }

    [Serializable]
    public struct FacilityRecord
    {
        public int type;
        public int level;
    }

    [Serializable]
    public sealed class ShopRecord
    {
        public int stockedFor = -1;
        public int stockedLevel;
        public int rerolls;
        public List<string> stock = new List<string>();
        public List<bool> locked = new List<bool>();
    }

    [Serializable]
    public sealed class TavernRecord
    {
        public int stockedFor = -1;
        public List<string> offers = new List<string>();
    }

    [Serializable]
    public sealed class CastleRecord
    {
        public List<CastlePieceRecord> pieces = new List<CastlePieceRecord>();
        public List<CastleWeaponRecord> weapons = new List<CastleWeaponRecord>();
    }

    [Serializable]
    public struct CastlePieceRecord
    {
        public string id;
        public int cellX;
        public int cellY;
        public int rotation;
        public int level;
        public bool isFixed;
        public float health;
        // 걸린 작업 주문(SIEGE-08). workKind 0이면 없다. 예전 저장에는 없어 0으로 읽힌다.
        public int workKind;
        public int workOrder;
        public float workHours;
        public float workDone;
        public int workGold;
        public float workStartHealth;
    }

    [Serializable]
    public struct CastleWeaponRecord
    {
        public string id;
        /// <summary><see cref="CastleRecord.pieces"/> 안에서 받치는 조각의 순번.</summary>
        public int supportIndex;
        public string mountId;
        public int rotation;
        public float health;
        /// <summary>다 짓지 못한 무기(SIEGE-08). 쏘지 않는다.</summary>
        public bool unfinished;
        public int workKind;
        public int workOrder;
        public float workHours;
        public float workDone;
        public int workGold;
        public float workStartHealth;
    }
}
