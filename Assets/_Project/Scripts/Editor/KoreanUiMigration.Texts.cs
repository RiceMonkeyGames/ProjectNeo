using System.Collections.Generic;

namespace ProjectNeo.Editor
{
    public static partial class KoreanUiMigration
    {
        /// <summary>Authored English text -> Korean text.</summary>
        static readonly Dictionary<string, string> Texts = new Dictionary<string, string>
        {
            // Build pieces, facilities, items, materials
            { "Wall", "성벽" }, { "Stairs", "계단" }, { "Cannon", "대포" }, { "Main gate", "메인 성문" },
            { "Battlements", "흉벽" },
            { "Tavern", "주점" }, { "Shop", "상점" }, { "Smithy", "대장간" }, { "Training ground", "훈련장" },
            { "Defense", "수성" }, { "Hero Hall", "영웅관" },
            { "Training", "훈련장" }, { "Construction", "건설" },
            { "Healing potion", "회복약" }, { "Iron sword", "철검" }, { "Steel sword", "강철검" },
            { "Wooden shield", "나무 방패" }, { "Leather armor", "가죽 갑옷" }, { "Chain armor", "사슬 갑옷" },
            { "Bone shard", "뼈 조각" }, { "Tough hide", "질긴 가죽" }, { "Feather", "깃털" },

            // Units, heroes, bosses
            { "Melee soldier", "근접 잡졸" }, { "Ranged soldier", "원거리 잡졸" },
            { "Mercenary swordsman", "용병 검사" }, { "Mercenary archer", "용병 궁수" },
            { "Knight", "기사" }, { "Paladin", "성기사" },
            { "Champion", "투사" }, { "Duelist", "결투가" }, { "Marksman", "명사수" },
            { "War Captain", "전쟁 대장" }, { "Warlord", "군벌" },
            { "Sky Hunter", "하늘 사냥꾼" }, { "Siege Giant", "공성 거인" }, { "Gate Crusher", "성문 분쇄자" },
            { "prototype_player", "시험용 플레이어" }, { "prototype_dummy", "시험용 허수아비" },
            { "construction_test_unit", "건설 시험 유닛" },
            { "hostile_melee", "적 근접병" }, { "hostile_ranged", "적 원거리병" },

            // Stages and spawn points
            { "Stage 1", "스테이지 1" }, { "Stage 2", "스테이지 2" }, { "Stage 3", "스테이지 3" },
            { "Stage 4", "스테이지 4" }, { "Stage 5", "스테이지 5" }, { "Stage 6", "스테이지 6" },
            { "Stage 7", "스테이지 7" },
            { "Left", "왼쪽" }, { "Center", "가운데" }, { "Right", "오른쪽" },

            // Perks, perk trees, unlock tracks and entries
            { "Combat", "전투" }, { "Economy", "경제" }, { "Heroes", "영웅" },
            { "Avenge Them", "복수" }, { "Bounty", "현상금" }, { "Rally", "재집결" },
            { "Drilled Ranks", "훈련된 대열" }, { "Iron Skin", "강철 피부" }, { "Sharpened Blade", "벼린 칼날" },
            { "Shield Wall", "방패벽" }, { "Swift Strikes", "빠른 일격" }, { "Toughness", "강인함" },
            { "Haggler", "흥정꾼" }, { "Long Days", "긴 하루" }, { "Master Trader", "거상" },
            { "War Chest", "군자금" }, { "Veteran Captains", "노련한 대장" }, { "Unit Enhancement", "유닛 강화" },
            { "Starting bonuses", "시작 보너스" }, { "Shop items", "상점 물품" }, { "Equipment recipes", "장비 제작법" },
            { "Starting gold +50", "시작 골드 +50" }, { "Starting gold +100", "시작 골드 +100" },
            { "Starting grunts +2", "시작 잡졸 +2" }, { "Starting potions x2", "시작 회복약 x2" },

            // Ascension levels
            { "Enemy HP +15%", "적 HP +15%" }, { "Enemy attack +15%", "적 공격력 +15%" },
            { "Enemy move speed +15%", "적 이동 속도 +15%" }, { "Enemies per wave +20%", "웨이브당 적 +20%" },
            { "Starting gold -25%", "시작 골드 -25%" }, { "Victory gold -20%", "승리 골드 -20%" },
            { "Preparation time 12h -> 10h", "준비 시간 12시간 -> 10시간" },
            { "Wall repair time x2", "성벽 수리 시간 x2" },
            { "Squad starting morale 150 -> 120", "부대 시작 사기 150 -> 120" },
            { "Boss HP +30%, attack +20%", "보스 HP +30%, 공격력 +20%" },

            // Title
            { "ProjectNeo", "ProjectNeo" }, { "New Game", "새 게임" }, { "Continue", "이어 하기" },
            { "Quit", "종료" }, { "Unlocks", "해금" }, { "Close", "닫기" },
            { "Delete selected slot", "고른 슬롯 삭제" }, { "Slot 1", "슬롯 1" }, { "Slot 2", "슬롯 2" },
            { "Slot 3", "슬롯 3" }, { "Unlock points: 0", "해금 포인트: 0" }, { "Track", "분류" }, { "Row", "항목" },
            { "Choose Ascension", "승천 고르기" }, { "Ascension 0", "승천 0" }, { "Start run", "런 시작" },

            // Game HUD
            { "Phase", "단계" }, { "Facility", "시설" }, { "Level", "레벨" }, { "Upgrade", "업그레이드" },
            { "Item", "아이템" }, { "Slots", "장비 칸" }, { "Squads", "부대" }, { "Squad", "부대" },
            { "Reroll", "리롤" }, { "Close (Esc)", "닫기 (Esc)" },
            { "Equipment - Player", "장비 · 플레이어" }, { "Equipped (click to unequip)", "착용 중 (클릭해서 벗기)" },
            { "Stash equipment (click to equip)", "보관함 장비 (클릭해서 착용)" },
            { "Offers (click to buy)", "매물 (클릭해서 구매)" }, { "Stash (click to sell)", "보관함 (클릭해서 판매)" },
            { "Squad organization", "부대 편성" }, { "Start battle", "전투 시작" }, { "Start Battle", "전투 시작" },
            { "Next stage", "다음 스테이지" }, { "Finish run", "런 끝내기" }, { "Back to title", "타이틀로" },
            { "Win (placeholder)", "승리 (임시)" }, { "Lose (placeholder)", "패배 (임시)" },
            { "Preparation: build, then start the battle", "준비: 건설한 뒤 전투를 시작하세요" },
            { "WASD move - Visit the construction station and press F", "WASD 이동 - 건설 작업대로 가서 F를 누르세요" },
            { "Next battle - enemies", "다음 전투 - 적" }, { "Battle status", "전투 상황" },
            { "HP -- / --", "HP -- / --" }, { "Prep time -- / --", "준비 시간 -- / --" },
            { "Gold", "골드" }, { "Repair all", "전체 수리" },

            // Construction / siege HUD
            { "WALL BUILDER", "성벽 건설" }, { "MOVEMENT / COLLAPSE TEST", "이동 / 붕괴 시험" },
            { "SIEGE PREP", "공성 준비" }, { "PLACE SQUADS", "부대 배치" },
            { "Choose a piece", "조각을 고르세요" }, { "Cancel", "취소" }, { "Dismantle", "해체" },
            { "Rotate (R)", "회전 (R)" }, { "Rotate", "회전" }, { "Walk onto wall", "성벽 위로 걷기" },
            { "Unit health", "유닛 체력" }, { "Break support", "받침 부수기" }, { "Break weapon support", "무기 받침 부수기" },
            { "Click: place / Right click: cancel", "클릭: 배치 / 우클릭: 취소" },
            { "Build walls and stairs.", "성벽과 계단을 지으세요." },
            { "Tab: first-person commands", "Tab: 1인칭 지휘" },
            { "Cost", "비용" }, { "Selection", "선택" }, { "Enemies", "적" }, { "Status", "상태" },
            { "Start", "시작" }, { "Siege", "공성" },
            { "Squad1", "1부대" }, { "Squad2", "2부대" }, { "Squad3", "3부대" }, { "Squad4", "4부대" },

            // Squad command help
            { "F1~F10 select squad · ` select all\n1 move to aim point · 2 attack · 3 hold\nWASD move · Mouse: look · Click: attack · Esc release cursor\nHold F: push nearby ladder",
              "F1~F10 부대 선택 · ` 전체 선택\n1 조준점으로 이동 · 2 공격 · 3 정지\nWASD 이동 · 마우스: 시점 · 클릭: 공격 · Esc 커서 해제\nF 길게 누르기: 가까운 사다리 밀기" },
            { "F1~F4 select squad · ` select all\n1 move to aim point · 2 attack · 3 hold\nWASD move · Mouse: look · Click: attack · Esc release cursor\nTab: construction view",
              "F1~F4 부대 선택 · ` 전체 선택\n1 조준점으로 이동 · 2 공격 · 3 정지\nWASD 이동 · 마우스: 시점 · 클릭: 공격 · Esc 커서 해제\nTab: 건설 화면" },
            { "F1~F4 select squad · ` select all\n1 move to aim point · 2 attack · 3 hold\nWASD move · Mouse: look · Click: attack · Esc release cursor",
              "F1~F4 부대 선택 · ` 전체 선택\n1 조준점으로 이동 · 2 공격 · 3 정지\nWASD 이동 · 마우스: 시점 · 클릭: 공격 · Esc 커서 해제" },
            { "F1~F3 select squad · ` select all · 1 move · 2 attack · 3 hold\nWASD move · Mouse: look · Click: attack · Esc release cursor",
              "F1~F3 부대 선택 · ` 전체 선택 · 1 이동 · 2 공격 · 3 정지\nWASD 이동 · 마우스: 시점 · 클릭: 공격 · Esc 커서 해제" },
        };

        /// <summary>Authored text that stays as is (key names, numbers, short technical labels).</summary>
        static readonly HashSet<string> Keep = new HashSet<string>
        {
            "ProjectNeo", "HP -- / --",
        };
    }
}
