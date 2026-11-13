using System;
using System.Collections.Generic;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Stats;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// D-0 시설 공통 틀. 게임용 시설 정의 5개(프로토타입 임의 값)를 만들어 시작 설정(RunStart)에 넣고,
    /// Game 씬 안뜰의 주점·상점·훈련장·대장간 건물에 F 상호작용을, 화면에 시설 팝업(uGUI+TMP 임시, Q-33)을 붙인다.
    /// 이미 있는 에셋은 덮어쓰지 않고, 여러 번 실행해도 팝업·컴포넌트를 중복으로 만들지 않는다.
    /// </summary>
    public static class GameFacilitySetup
    {
        const string Folder = "Assets/_Project/Data/GameFlow/Facilities";
        const string StartPath = "Assets/_Project/Data/GameFlow/Build/RunStart.asset";
        const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        static readonly (FacilityType type, string name, string building)[] Facilities =
        {
            (FacilityType.Tavern, "주점", "Tavern"),
            (FacilityType.Shop, "상점", "Shop"),
            (FacilityType.TrainingGround, "훈련장", "Training"),
            (FacilityType.Smithy, "대장간", "Smithy"),
            (FacilityType.Defense, "수성", null),
        };

        [MenuItem("ProjectNeo/Setup/Connect Facilities To Game Scene")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            CreateData();
            CreateShopData();
            CreateSmithyData();
            AssetDatabase.SaveAssets();
            // 씬을 연 뒤 만든 에셋은 경로로 다시 불러와 연결한다(C-4 GameRewardSetup과 같은 주의).
            var start = AssetDatabase.LoadAssetAtPath<RunConfig>(StartPath)
                ?? throw new InvalidOperationException("Run Connect Build Costs To Game Scene first: " + StartPath);
            AddFacilitiesTo(start);

            GameBattleController battle = Find<GameBattleController>(scene);
            var facilities = battle.GetComponent<GameFacilities>();
            if (facilities == null) facilities = battle.gameObject.AddComponent<GameFacilities>();
            GamePhaseView view = Find<GamePhaseView>(scene);
            GameFlowHud hud = Find<GameFlowHud>(scene);
            Popup popup = BuildPopup(hud, facilities);
            facilities.Configure(start, view, popup.panel, popup.title, popup.status, popup.body);
            facilities.ConfigureActions(popup.upgrade, popup.upgradeLabel, popup.trade, popup.offers, popup.inventory, popup.row);
            EditorUtility.SetDirty(facilities);

            // D-7 장비 화면(I 키). 상호작용 설정 에셋에 새 키 칸을 저장한다.
            var interaction = Find<PreparationInteraction>(scene);
            var interactionSettings = (PreparationInteractionSettings)new SerializedObject(interaction)
                .FindProperty("settings").objectReferenceValue;
            if (interactionSettings != null) EditorUtility.SetDirty(interactionSettings);
            var equipment = battle.GetComponent<GameEquipment>();
            if (equipment == null) equipment = battle.gameObject.AddComponent<GameEquipment>();
            Popup gear = BuildEquipmentPopup(hud, equipment);
            equipment.Configure(view, interaction, gear.panel, gear.status, gear.body, gear.offers, gear.inventory, gear.row);
            EditorUtility.SetDirty(equipment);

            ConstructionWorld world = Find<ConstructionWorld>(scene);
            foreach (var entry in Facilities)
            {
                if (entry.building == null) continue;
                Transform building = world.transform.Find("Geometry/CourtyardLayout/" + entry.building)
                    ?? throw new InvalidOperationException("Apply Default Castle first. Missing building: " + entry.building);
                var station = building.GetComponent<FacilityStation>();
                if (station == null) station = building.gameObject.AddComponent<FacilityStation>();
                station.Configure(view, facilities, entry.type);
                EditorUtility.SetDirty(station);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static string PathOf(FacilityType type) => $"{Folder}/Facility_{type}.asset";

        /// <summary>프로토타입 시설 데이터를 만들거나 기존 것을 둔다.</summary>
        public static void CreateData()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Data/GameFlow", "Facilities");
            foreach (var entry in Facilities)
            {
                string path = PathOf(entry.type);
                if (AssetDatabase.LoadAssetAtPath<FacilityDefinition>(path) != null) continue;
                // 프로토타입 임의 값(2026-11-12). 레벨 3단계, 업그레이드 골드 60·120. 매물·풀·스탯은 각 시설 작업에서 채운다.
                AssetDatabase.CreateAsset(FacilityDefinition.Create(entry.type, entry.name,
                    Level(0, 0, 3), Level(60, 1, 4), Level(120, 2, 5)), path);
            }
        }

        static FacilityLevel Level(int gold, int tier, int offers) => new FacilityLevel
        {
            upgradeCost = new Cost(gold),
            qualityTier = tier,
            offerCount = offers,
            soldierModifiers = new List<ProjectNeo.Stats.StatModifier>(),
            recruitableSoldiers = new List<ProjectNeo.Characters.SoldierDefinition>(),
            itemPool = new List<ProjectNeo.Core.WeightedEntry<ItemDefinition>>(),
            heroPool = new List<ProjectNeo.Core.WeightedEntry<ProjectNeo.Characters.HeroDefinition>>(),
        };

        static void AddFacilitiesTo(RunConfig start)
        {
            var data = new SerializedObject(start);
            SerializedProperty list = data.FindProperty("facilities");
            list.ClearArray();
            foreach (var entry in Facilities)
            {
                var definition = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(PathOf(entry.type));
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = definition;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(start);
        }

        const string EconomyPath = "Assets/_Project/Data/GameFlow/Economy.asset";

        /// <summary>
        /// D-5 상점 프로토타입 데이터. 거래 수치(판매 50%)와 아이템 4종을 더 만들고, 상점 시설 정의의 품목 풀이 모두 비어 있을 때만
        /// 레벨별 풀을 채운다(인스펙터에서 바꾼 값은 덮어쓰지 않는다). 시작 설정에 거래 수치를 넣는다.
        /// </summary>
        public static void CreateShopData()
        {
            if (AssetDatabase.LoadAssetAtPath<EconomyConfig>(EconomyPath) == null)
                AssetDatabase.CreateAsset(EconomyConfig.Create(0.5f), EconomyPath);
            // 프로토타입 임의 값(2026-11-12). 철검·회복약은 C-4 보상 아이템을 같이 쓴다.
            ItemDefinition potion = GameRewardSetup.Item("item_potion", "회복약", ItemKind.Consumable, 10);
            ItemDefinition sword = GameRewardSetup.Item("item_iron_sword", "철검", ItemKind.Equipment, 40);
            ItemDefinition shield = GameRewardSetup.Item("item_wood_shield", "나무 방패", ItemKind.Equipment, 25);
            ItemDefinition leather = GameRewardSetup.Item("item_leather_armor", "가죽 갑옷", ItemKind.Equipment, 30);
            ItemDefinition chain = GameRewardSetup.Item("item_chain_armor", "사슬 갑옷", ItemKind.Equipment, 60);
            ItemDefinition steel = GameRewardSetup.Item("item_steel_sword", "강철검", ItemKind.Equipment, 80);
            // D-7 장비 효과(프로토타입 임의 값). 이미 효과가 있으면 인스펙터에서 바꾼 값을 둔다.
            // 최대 체력은 준비 중 착용하면 현재 체력이 가득 차지 않아 이번에는 공격력·방어력만 쓴다.
            SetModifiers(sword, StatModifier.Flat(StatType.Attack, 4f));
            SetModifiers(steel, StatModifier.Flat(StatType.Attack, 8f));
            SetModifiers(shield, StatModifier.Flat(StatType.Defense, 2f));
            SetModifiers(leather, StatModifier.Flat(StatType.Defense, 3f));
            SetModifiers(chain, StatModifier.Flat(StatType.Defense, 6f), StatModifier.Percent(StatType.MoveSpeed, -0.1f));

            var shop = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(PathOf(FacilityType.Shop));
            if (shop != null)
            {
                var data = new SerializedObject(shop);
                SerializedProperty levels = data.FindProperty("levels");
                bool empty = true;
                for (int i = 0; i < levels.arraySize; i++)
                    if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("itemPool").arraySize > 0) empty = false;
                if (empty)
                {
                    var pools = new[]
                    {
                        new[] { (potion, 2f), (sword, 1f), (shield, 1f), (leather, 1f) },
                        new[] { (potion, 2f), (sword, 1f), (shield, 1f), (leather, 1f), (chain, 1f), (steel, 0.5f) },
                        new[] { (potion, 1f), (sword, 1f), (shield, 0.5f), (leather, 1f), (chain, 1.5f), (steel, 1.5f) },
                    };
                    for (int i = 0; i < levels.arraySize && i < pools.Length; i++)
                    {
                        SerializedProperty pool = levels.GetArrayElementAtIndex(i).FindPropertyRelative("itemPool");
                        foreach ((ItemDefinition item, float weight) in pools[i])
                        {
                            pool.InsertArrayElementAtIndex(pool.arraySize);
                            SerializedProperty entry = pool.GetArrayElementAtIndex(pool.arraySize - 1);
                            entry.FindPropertyRelative("item").objectReferenceValue = item;
                            entry.FindPropertyRelative("weight").floatValue = weight;
                        }
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(shop);
                }
            }

            var start = AssetDatabase.LoadAssetAtPath<RunConfig>(StartPath);
            if (start != null)
            {
                var startData = new SerializedObject(start);
                startData.FindProperty("economy").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EconomyConfig>(EconomyPath);
                startData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(start);
            }
        }

        const string MaterialFolder = "Assets/_Project/Data/GameFlow/Rewards";

        /// <summary>대장간 레시피 데이터만 만든다(D-6). 씬은 건드리지 않는다.</summary>
        [MenuItem("ProjectNeo/Setup/Create Smithy Recipes")]
        public static void ApplySmithyData()
        {
            CreateSmithyData();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// D-6 대장간 고정 레시피(Q-35). 대장간 시설 정의의 레시피가 모두 비어 있을 때만 레벨별 전체 목록을 채운다
        /// (인스펙터에서 바꾼 값은 덮어쓰지 않는다). 1레벨에 재료 없이 만들 수 있는 레시피를 하나 두어 시작부터 확인할 수 있게 한다.
        /// </summary>
        public static void CreateSmithyData()
        {
            var smithy = AssetDatabase.LoadAssetAtPath<FacilityDefinition>(PathOf(FacilityType.Smithy));
            if (smithy == null) return;
            // 프로토타입 임의 값(2026-11-13). 상점 아이템을 같이 쓰고, 상점보다 골드는 싸고 재료를 더 쓴다.
            ItemDefinition sword = GameRewardSetup.Item("item_iron_sword", "철검", ItemKind.Equipment, 40);
            ItemDefinition shield = GameRewardSetup.Item("item_wood_shield", "나무 방패", ItemKind.Equipment, 25);
            ItemDefinition leather = GameRewardSetup.Item("item_leather_armor", "가죽 갑옷", ItemKind.Equipment, 30);
            ItemDefinition chain = GameRewardSetup.Item("item_chain_armor", "사슬 갑옷", ItemKind.Equipment, 60);
            ItemDefinition steel = GameRewardSetup.Item("item_steel_sword", "강철검", ItemKind.Equipment, 80);
            MaterialDefinition hide = AssetDatabase.LoadAssetAtPath<MaterialDefinition>($"{MaterialFolder}/mat_hide.asset");
            MaterialDefinition bone = AssetDatabase.LoadAssetAtPath<MaterialDefinition>($"{MaterialFolder}/mat_bone.asset");
            MaterialDefinition feather = AssetDatabase.LoadAssetAtPath<MaterialDefinition>($"{MaterialFolder}/mat_feather.asset");

            var data = new SerializedObject(smithy);
            SerializedProperty levels = data.FindProperty("levels");
            for (int i = 0; i < levels.arraySize; i++)
                if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("recipes").arraySize > 0) return;

            var lv1 = new[]
            {
                new CraftingRecipe(shield, new Cost(20)),
                new CraftingRecipe(sword, new Cost(30, new MaterialAmount(hide, 2))),
                new CraftingRecipe(leather, new Cost(20, new MaterialAmount(hide, 3))),
            };
            var lv2 = new List<CraftingRecipe>(lv1) { new CraftingRecipe(chain, new Cost(45, new MaterialAmount(hide, 4), new MaterialAmount(bone, 2))) };
            var lv3 = new List<CraftingRecipe>(lv2) { new CraftingRecipe(steel, new Cost(60, new MaterialAmount(bone, 4), new MaterialAmount(feather, 2))) };
            var perLevel = new IReadOnlyList<CraftingRecipe>[] { lv1, lv2, lv3 };
            for (int i = 0; i < levels.arraySize && i < perLevel.Length; i++)
            {
                SerializedProperty recipes = levels.GetArrayElementAtIndex(i).FindPropertyRelative("recipes");
                foreach (CraftingRecipe recipe in perLevel[i])
                {
                    recipes.InsertArrayElementAtIndex(recipes.arraySize);
                    SerializedProperty entry = recipes.GetArrayElementAtIndex(recipes.arraySize - 1);
                    entry.FindPropertyRelative("item").objectReferenceValue = recipe.item;
                    SerializedProperty cost = entry.FindPropertyRelative("cost");
                    cost.FindPropertyRelative("gold").intValue = recipe.cost.gold;
                    SerializedProperty materials = cost.FindPropertyRelative("materials");
                    materials.ClearArray();
                    foreach (MaterialAmount amount in recipe.cost.materials)
                    {
                        if (amount.material == null) continue;
                        materials.InsertArrayElementAtIndex(materials.arraySize);
                        SerializedProperty material = materials.GetArrayElementAtIndex(materials.arraySize - 1);
                        material.FindPropertyRelative("material").objectReferenceValue = amount.material;
                        material.FindPropertyRelative("amount").intValue = amount.amount;
                    }
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(smithy);
        }

        struct Popup
        {
            public GameObject panel, trade;
            public TMP_Text title, status, body, upgradeLabel;
            public Button upgrade, row;
            public RectTransform offers, inventory;
        }

        static void SetModifiers(ItemDefinition item, params StatModifier[] modifiers)
        {
            var data = new SerializedObject(item);
            SerializedProperty list = data.FindProperty("equipModifiers");
            if (list.arraySize > 0) return;
            foreach (StatModifier modifier in modifiers)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("stat").enumValueIndex = (int)modifier.stat;
                entry.FindPropertyRelative("kind").enumValueIndex = (int)modifier.kind;
                entry.FindPropertyRelative("value").floatValue = modifier.value;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        /// <summary>D-7 장비 화면. 시설 팝업과 같은 모양으로 착용 중·보관함 장비 두 목록을 둔다.</summary>
        static Popup BuildEquipmentPopup(GameFlowHud hud, GameEquipment equipment)
        {
            var font = (RuntimeSystemFont)new SerializedObject(hud).FindProperty("font").objectReferenceValue;
            Transform parent = font != null && !hud.transform.IsChildOf(font.transform) ? font.transform : hud.transform;
            Transform existing = parent.Find("EquipmentPanel");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var popup = new Popup();
            popup.panel = new GameObject("EquipmentPanel", typeof(RectTransform), typeof(Image));
            popup.panel.transform.SetParent(parent, false);
            var rect = popup.panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(780f, 540f);
            popup.panel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.1f, 0.94f);
            popup.title = Text(popup.panel.transform, "Title", "장비 · 플레이어", 32f, new Vector2(24f, -20f), new Vector2(-24f, -66f));
            popup.title.fontStyle = FontStyles.Bold;
            popup.status = Text(popup.panel.transform, "Status", "Slots", 18f, new Vector2(24f, -70f), new Vector2(-24f, -122f));
            popup.status.color = new Color(1f, 0.9f, 0.55f);
            popup.body = Text(popup.panel.transform, "Body", "", 17f, new Vector2(24f, -126f), new Vector2(-24f, -180f));

            popup.trade = new GameObject("Lists", typeof(RectTransform));
            popup.trade.transform.SetParent(popup.panel.transform, false);
            var listsRect = popup.trade.GetComponent<RectTransform>();
            listsRect.anchorMin = Vector2.zero; listsRect.anchorMax = Vector2.one;
            listsRect.offsetMin = new Vector2(24f, 84f); listsRect.offsetMax = new Vector2(-24f, -188f);
            popup.offers = Column(popup.trade.transform, "Equipped", "착용 중 (클릭해서 벗기)", 0f, 0.5f);
            popup.inventory = Column(popup.trade.transform, "Gear", "보관함 장비 (클릭해서 착용)", 0.5f, 1f);

            popup.row = ButtonAt(popup.panel.transform, "RowTemplate", "Item", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(340f, 34f), null, out TMP_Text rowLabel);
            rowLabel.fontSize = 16f; rowLabel.alignment = TextAlignmentOptions.MidlineLeft;
            rowLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
            popup.row.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            popup.row.gameObject.SetActive(false);

            ButtonAt(popup.panel.transform, "Button_Close", "닫기 (Esc)", new Vector2(0.5f, 0f), new Vector2(0f, 20f),
                new Vector2(200f, 48f), equipment.Close, out _);
            popup.panel.SetActive(false);
            return popup;
        }

        static Popup BuildPopup(GameFlowHud hud, GameFacilities facilities)
        {
            // 한글 글꼴(RuntimeSystemFont)이 실행 중에 바꿔 주는 범위 안에 둔다. 매물 줄은 실행 중 rowTemplate을 복제한다.
            var font = (RuntimeSystemFont)new SerializedObject(hud).FindProperty("font").objectReferenceValue;
            Transform parent = font != null && !hud.transform.IsChildOf(font.transform) ? font.transform : hud.transform;
            Transform existing = parent.Find("FacilityPanel");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var popup = new Popup();
            popup.panel = new GameObject("FacilityPanel", typeof(RectTransform), typeof(Image));
            popup.panel.transform.SetParent(parent, false);
            var rect = popup.panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(780f, 540f);
            popup.panel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.1f, 0.94f);

            popup.title = Text(popup.panel.transform, "Title", "Facility", 32f, new Vector2(24f, -20f), new Vector2(-270f, -66f));
            popup.title.fontStyle = FontStyles.Bold;
            popup.status = Text(popup.panel.transform, "Status", "Level", 18f, new Vector2(24f, -70f), new Vector2(-24f, -122f));
            popup.status.color = new Color(1f, 0.9f, 0.55f);
            popup.body = Text(popup.panel.transform, "Body", "", 17f, new Vector2(24f, -126f), new Vector2(-24f, -196f));

            popup.upgrade = ButtonAt(popup.panel.transform, "Button_Upgrade", "Upgrade", new Vector2(1f, 1f),
                new Vector2(-24f, -20f), new Vector2(230f, 44f), facilities.TryUpgradeFromButton, out popup.upgradeLabel);

            popup.trade = new GameObject("Trade", typeof(RectTransform));
            popup.trade.transform.SetParent(popup.panel.transform, false);
            var tradeRect = popup.trade.GetComponent<RectTransform>();
            tradeRect.anchorMin = Vector2.zero; tradeRect.anchorMax = Vector2.one;
            tradeRect.offsetMin = new Vector2(24f, 84f); tradeRect.offsetMax = new Vector2(-24f, -204f);
            popup.offers = Column(popup.trade.transform, "Offers", "매물 (클릭해서 구매)", 0f, 0.5f);
            popup.inventory = Column(popup.trade.transform, "Inventory", "보관함 (클릭해서 판매)", 0.5f, 1f);

            popup.row = ButtonAt(popup.panel.transform, "RowTemplate", "Item", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(340f, 34f), null, out TMP_Text rowLabel);
            rowLabel.fontSize = 16f; rowLabel.alignment = TextAlignmentOptions.MidlineLeft;
            rowLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
            popup.row.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            popup.row.gameObject.SetActive(false);

            ButtonAt(popup.panel.transform, "Button_Close", "닫기 (Esc)", new Vector2(0.5f, 0f), new Vector2(0f, 20f),
                new Vector2(200f, 48f), facilities.Close, out _);
            popup.panel.SetActive(false);
            return popup;
        }

        internal static RectTransform Column(Transform parent, string name, string header, float left, float right)
        {
            var column = new GameObject(name + "Column", typeof(RectTransform));
            column.transform.SetParent(parent, false);
            var rect = column.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(left, 0f); rect.anchorMax = new Vector2(right, 1f);
            rect.offsetMin = new Vector2(left > 0f ? 8f : 0f, 0f); rect.offsetMax = new Vector2(right < 1f ? -8f : 0f, 0f);
            TMP_Text title = Text(column.transform, "Header", header, 17f, Vector2.zero, new Vector2(0f, -26f));
            title.color = new Color(0.7f, 0.85f, 1f);
            var list = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            list.transform.SetParent(column.transform, false);
            var listRect = list.GetComponent<RectTransform>();
            listRect.anchorMin = Vector2.zero; listRect.anchorMax = Vector2.one;
            listRect.offsetMin = Vector2.zero; listRect.offsetMax = new Vector2(0f, -30f);
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4f; layout.childControlHeight = true; layout.childControlWidth = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            return listRect;
        }

        internal static Button ButtonAt(Transform parent, string name, string text, Vector2 anchor, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction action, out TMP_Text label)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = root.GetComponent<Image>(); image.color = new Color(0.15f, 0.24f, 0.29f);
            var button = root.GetComponent<Button>(); button.targetGraphic = image;
            if (action != null) UnityEventTools.AddPersistentListener(button.onClick, action);
            label = Text(root.transform, "Label", text, 18f, Vector2.zero, Vector2.zero);
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        /// <summary>패널 위쪽 기준으로 왼쪽 위·오른쪽 아래 여백을 받아 글 칸을 만든다.</summary>
        internal static TMP_Text Text(Transform parent, string name, string text, float size, Vector2 topLeft, Vector2 bottomRight)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(topLeft.x, bottomRight.y); rect.offsetMax = new Vector2(bottomRight.x, topLeft.y);
            var label = root.GetComponent<TextMeshProUGUI>();
            label.text = text; label.fontSize = size; label.color = new Color(0.92f, 0.96f, 1f); label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            return label;
        }

        internal static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            throw new InvalidOperationException("Game scene has no " + typeof(T).Name);
        }
    }
}
