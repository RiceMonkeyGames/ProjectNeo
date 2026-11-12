using System;
using ProjectNeo.Core;
using ProjectNeo.Economy;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// C-4 스테이지 보상의 프로토타입 데이터(재료·아이템·보상 표·메타 설정)를 만들고 Game 씬의 전투 오브젝트에
    /// <see cref="GameRewardController"/>를 붙인다. 이미 있는 에셋은 덮어쓰지 않아 인스펙터에서 바꾼 값이 남는다.
    /// 여러 번 실행해도 결과가 같다.
    /// </summary>
    public static class GameRewardSetup
    {
        const string Folder = "Assets/_Project/Data/GameFlow/Rewards";

        [MenuItem("ProjectNeo/Setup/Connect Rewards To Game Scene")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            CreateData(out _);
            AssetDatabase.SaveAssets();
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            // 씬을 열면 방금 만든 에셋 참조가 풀릴 수 있어 경로로 다시 불러온다(Context 2장 SiegePrototypeSetup 주의).
            var table = AssetDatabase.LoadAssetAtPath<StageRewardTable>(Folder + "/StageRewards.asset");
            var meta = AssetDatabase.LoadAssetAtPath<MetaConfig>(Folder + "/Meta.asset");
            if (table == null || meta == null) throw new InvalidOperationException("Reward assets were not created.");
            GameBattleController battle = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                battle = root.GetComponentInChildren<GameBattleController>(true);
                if (battle != null) break;
            }
            if (battle == null) throw new InvalidOperationException("Game scene has no GameBattleController.");
            var rewards = battle.GetComponent<GameRewardController>();
            if (rewards == null) rewards = battle.gameObject.AddComponent<GameRewardController>();
            var serialized = new SerializedObject(rewards);
            serialized.FindProperty("table").objectReferenceValue = table;
            serialized.FindProperty("metaConfig").objectReferenceValue = meta;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>프로토타입 보상 데이터를 만들거나 기존 것을 불러온다.</summary>
        public static StageRewardTable CreateData(out MetaConfig meta)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Data/GameFlow", "Rewards");
            MaterialDefinition bone = Material("mat_bone", "뼈 조각");
            MaterialDefinition hide = Material("mat_hide", "질긴 가죽");
            MaterialDefinition feather = Material("mat_feather", "깃털");
            ItemDefinition sword = Item("item_iron_sword", "철검", ItemKind.Equipment, 40);
            ItemDefinition potion = Item("item_potion", "회복약", ItemKind.Consumable, 10);

            meta = LoadOrCreate(Folder + "/Meta.asset", () => ScriptableObject.CreateInstance<MetaConfig>());
            // 프로토타입 임의 값(2026-11-12). 적 ID는 전투가 세는 적 프리팹 이름이다.
            return LoadOrCreate(Folder + "/StageRewards.asset", () => StageRewardTable.Create(50, 10,
                new[]
                {
                    new KillReward("enemy_ladder", 3, bone, 1),
                    new KillReward("enemy_gate_breaker", 5, hide, 2),
                    new KillReward("enemy_archer", 3, feather, 1),
                },
                0.6f, 1,
                new[] { new WeightedEntry<ItemDefinition>(sword, 1f), new WeightedEntry<ItemDefinition>(potion, 2f) },
                40, 10, 5));
        }

        static MaterialDefinition Material(string id, string name)
        {
            return LoadOrCreate(Folder + "/" + id + ".asset", () =>
            {
                MaterialDefinition material = MaterialDefinition.Create(id);
                SetName(material, name);
                return material;
            });
        }

        internal static ItemDefinition Item(string id, string name, ItemKind kind, int gold)
        {
            return LoadOrCreate(Folder + "/" + id + ".asset", () =>
            {
                ItemDefinition item = ItemDefinition.Create(id, kind, new Cost(gold));
                SetName(item, name);
                return item;
            });
        }

        static void SetName(ScriptableObject asset, string name)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("displayName").stringValue = name;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static T LoadOrCreate<T>(string path, Func<T> create) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            T asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
