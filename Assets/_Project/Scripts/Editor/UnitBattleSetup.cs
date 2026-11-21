using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Stats;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    public static class UnitBattleSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/UnitBattlePrototype.unity";
        const string Data = "Assets/_Project/Data/UnitBattlePrototype";
        const string Prefabs = "Assets/_Project/Prefabs/UnitBattlePrototype";

        [MenuItem("ProjectNeo/Setup/Create Unit Battle Prototype")]
        public static void Create()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the active scene first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("UnitBattlePrototype already exists; refusing to overwrite it.");
            UpgradeTrainingTeams();
            CombatPrototypeSetup.EnsureFolder(Data);
            CombatPrototypeSetup.EnsureFolder(Prefabs);
            var ai = CombatPrototypeSetup.Save(UnitAISettings.Create(), Data + "/AI.asset");
            Material blue = CombatPrototypeSetup.MaterialAsset("Player", new Color(0.12f, 0.48f, 0.9f));
            Material red = CombatPrototypeSetup.MaterialAsset("Dummy", new Color(0.85f, 0.25f, 0.16f));
            Material green = CombatPrototypeSetup.MaterialAsset("Health", new Color(0.2f, 0.9f, 0.35f));
            Material yellow = CombatPrototypeSetup.MaterialAsset("Attack", new Color(1f, 0.75f, 0.1f));
            Material ground = CombatPrototypeSetup.MaterialAsset("Ground", new Color(0.22f, 0.3f, 0.25f));
            Material stone = CombatPrototypeSetup.MaterialAsset("Stone", new Color(0.38f, 0.43f, 0.49f));
            GameObject projectile = CreateProjectile(yellow);
            // 임시 구체를 화살 모델로 바꾸고 박혀 남게 한다(ART-05).
            ArrowModelSetup.Apply();
            var melee = CombatPrototypeSetup.Save(CombatProfile.Create(), Data + "/Melee.asset");
            var ranged = CombatPrototypeSetup.Save(CombatProfile.Create(0.3f, 55f, 0.4f, 2f,
                AttackKind.Projectile, projectile), Data + "/Ranged.asset");
            var alliedMelee = Soldier("allied_melee", 100f, 22f, 4f, 1.5f);
            // 원거리 사거리·탄퍼짐은 COMBAT-09, 공격 속도는 COMBAT-10 값(ArcherRangeMigration)과 같다.
            var alliedRanged = Soldier("allied_ranged", 80f, 18f, 2f, ArcherRangeMigration.SoldierRange, ArcherRangeMigration.DefaultSpread,
                ArcherRangeMigration.ArcherAttackSpeed);
            var hostileMelee = Soldier("적 근접병", 80f, 14f, 2f, 1.5f);
            var hostileRanged = Soldier("적 원거리병", 65f, 13f, 1f, ArcherRangeMigration.SoldierRange, ArcherRangeMigration.DefaultSpread,
                ArcherRangeMigration.ArcherAttackSpeed);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.65f, 0.7f, 0.8f);
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            var geometry = new GameObject("NavigationGeometry");
            AddGeometry(geometry, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), ground);
            AddGeometry(geometry, "Obstacle", new Vector3(0f, 1.5f, 0f), new Vector3(3f, 3f, 2f), stone);
            AddGeometry(geometry, "NorthWall", new Vector3(0f, 1.5f, 20f), new Vector3(40f, 3f, 1f), stone);
            AddGeometry(geometry, "SouthWall", new Vector3(0f, 1.5f, -20f), new Vector3(40f, 3f, 1f), stone);
            AddGeometry(geometry, "WestWall", new Vector3(-20f, 1.5f, 0f), new Vector3(1f, 3f, 40f), stone);
            AddGeometry(geometry, "EastWall", new Vector3(20f, 1.5f, 0f), new Vector3(1f, 3f, 40f), stone);
            var surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData, Data + "/Navigation.asset");

            GameObject allyMeleePrefab = UnitPrefab(alliedMelee, melee, ai, CombatTeam.Allied, blue, green, yellow);
            GameObject allyRangedPrefab = UnitPrefab(alliedRanged, ranged, ai, CombatTeam.Allied, blue, green, yellow);
            GameObject enemyMeleePrefab = UnitPrefab(hostileMelee, melee, ai, CombatTeam.Hostile, red, green, yellow);
            GameObject enemyRangedPrefab = UnitPrefab(hostileRanged, ranged, ai, CombatTeam.Hostile, red, green, yellow);
            for (int i = 0; i < 10; i++)
            {
                bool archer = i >= 7;
                float x = archer ? (i - 8) * 4f : (i - 3) * 1.8f;
                float z = archer ? 11f : 6f;
                Spawn(archer ? allyRangedPrefab : allyMeleePrefab, "Ally_" + i, new Vector3(x, 0f, -z), 0f);
                Spawn(archer ? enemyRangedPrefab : enemyMeleePrefab, "Enemy_" + i, new Vector3(x, 0f, z), 180f);
            }
            var camera = new GameObject("BattleCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(15f, 18f, -23f);
            camera.transform.LookAt(Vector3.zero);
            camera.farClipPlane = 150f;
            camera.gameObject.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        static SoldierDefinition Soldier(string id, float health, float attack, float defense, float range, float spread = 0f,
            float attackSpeed = 1f)
        {
            return CombatPrototypeSetup.Save(SoldierDefinition.Create(id, new Cost(0),
                new StatValue(StatType.MaxHealth, health), new StatValue(StatType.Attack, attack),
                new StatValue(StatType.Defense, defense), new StatValue(StatType.AttackRange, range),
                new StatValue(StatType.AttackSpeed, attackSpeed), new StatValue(StatType.MoveSpeed, 3.5f),
                new StatValue(StatType.AimSpread, spread)), Data + "/" + id + ".asset");
        }

        static GameObject UnitPrefab(SoldierDefinition soldier, CombatProfile profile, UnitAISettings ai,
            CombatTeam team, Material body, Material health, Material attack)
        {
            // 기존 임시 외형 생성 경로를 공유하고 데이터만 잡졸로 연결한다.
            GameObject unit = CombatPrototypeSetup.Actor(soldier.Id, null, profile, body, health, attack, false);
            unit.GetComponent<CharacterActor>().Configure(soldier, profile, team);
            var agent = unit.AddComponent<NavMeshAgent>();
            agent.radius = profile.Radius;
            agent.height = profile.Height;
            unit.AddComponent<UnitCombatAI>().Configure(ai);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(unit, Prefabs + "/" + soldier.Id + ".prefab");
            UnityEngine.Object.DestroyImmediate(unit);
            return prefab;
        }

        static GameObject CreateProjectile(Material material)
        {
            var root = new GameObject("StraightProjectile");
            root.AddComponent<CombatProjectile>();
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.16f;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = material;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/StraightProjectile.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static void AddGeometry(GameObject parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            CombatPrototypeSetup.Box(name, position, scale, material).transform.SetParent(parent.transform, true);
        }

        static void Spawn(GameObject prefab, string name, Vector3 position, float yaw)
        {
            var unit = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            unit.name = name;
            unit.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            unit.SetActive(true);
        }

        public static void UpgradeTrainingTeams()
        {
            const string path = "Assets/_Project/Prefabs/CombatPrototype/TrainingDummy.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                contents.GetComponent<CharacterActor>().SetTeam(CombatTeam.Hostile);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }
}
