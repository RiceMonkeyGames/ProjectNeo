using System;
using System.Collections.Generic;
using System.Linq;
using ProjectNeo.Combat;
using ProjectNeo.Core;
using ProjectNeo.Flow;
using ProjectNeo.Town;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 성 안 주민(SIEGE-36): 시설마다 입구 옆에 NPC 한 명(F 상호작용 대상), 길을 걸어 다니는 시민 여덟 명, 이들을 움직이는 TownDirector.
    /// 씬 맨 위 "Town" 아래에 새로 만든다(Geometry 밖이라 길찾기 메시에 들어가지 않는다). 여러 번 실행해도 같은 결과다.
    /// </summary>
    public static class GameTownSetup
    {
        const string RootName = "Town";
        const string Characters = "Characters/";
        const string ArtFolder = "Assets/_Project/Art/Town";
        const string MaterialFolder = ArtFolder + "/Materials";
        const string SettingsPath = "Assets/_Project/Data/GameFlow/Town.asset";
        const string Locomotion = "Assets/ThirdParty/Synty/AnimationBaseLocomotion/Animations/Polygon/";
        const int CitizenSeed = 37;

        /// <summary>시설 이름별 NPC 모델·이름·몸 동작(여성 동작이면 true). 사용자 결정 2026-11-25(추천 배정).</summary>
        static readonly Dictionary<string, (string model, string name, bool feminine)> Npcs = new Dictionary<string, (string, string, bool)>
        {
            ["Tavern"] = ("SM_Chr_Bartender_01", "바텐더", false),
            ["Shop"] = ("SM_Chr_Merchant_01", "상인", false),
            ["Training"] = ("SM_Chr_Rider_01", "교관", false),
            ["Smithy"] = ("SM_Chr_Blacksmith_Male_01", "대장장이", false),
            ["Construction"] = ("SM_Chr_Peasant_Male_01", "건설 감독", false),
            [HeroHallSetup.BuildingName] = ("SM_Chr_Priest_01", "사제", false),
        };

        /// <summary>시민 여덟 명: 농부 남녀, 수도사, 수녀, 광대, 점쟁이.</summary>
        static readonly (string model, bool feminine)[] Citizens =
        {
            ("SM_Chr_Peasant_Male_01", false), ("SM_Chr_Peasant_Female_01", true), ("SM_Chr_Peasant_Female_01", true),
            ("SM_Chr_Peasant_Male_01", false), ("SM_Chr_Monk_01", false), ("SM_Chr_Nun_01", true),
            ("SM_Chr_Jester_01", false), ("SM_Chr_FortuneTeller_01", true),
        };

        internal static void Apply(Scene scene, ConstructionWorld world, GameCourtyardArtSetup.Plan plan)
        {
            var settings = CombatPrototypeSetup.Save(TownSettings.Create(), SettingsPath);
            var masculine = Controller("Masc");
            var feminine = Controller("Femn");
            foreach (GameObject existing in scene.GetRootGameObjects())
                if (existing.name == RootName) UnityEngine.Object.DestroyImmediate(existing);
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var converted = new Dictionary<Material, Material>();
            var residents = new List<TownResident>();
            Vector3 World(Vector2 point) => world.transform.TransformPoint(new Vector3(point.x, 0f, point.y));
            float worldYaw = world.transform.eulerAngles.y;

            foreach (GameCourtyardArtSetup.BuildingSpot spot in plan.Buildings)
            {
                if (!Npcs.TryGetValue(spot.Name, out var npc)) continue;
                var station = spot.Root.GetComponent<PreparationStation>();
                if (station == null) throw new InvalidOperationException("Facility has no station yet: " + spot.Name + " (run the facility setup menus first).");
                TownResident resident = Resident(root.transform, "Npc_" + spot.Name, npc.model, npc.feminine ? feminine : masculine,
                    World(spot.NpcSpot), spot.Yaw + worldYaw, true, converted);
                var link = resident.gameObject.AddComponent<StationNpc>();
                link.Configure(station, npc.name);
                EditorUtility.SetDirty(station);
                residents.Add(resident);
            }

            var random = new SeededRandom(CitizenSeed);
            var homes = new List<Vector2>(plan.WanderPoints);
            for (int i = 0; i < Citizens.Length && homes.Count > 0; i++)
            {
                int pick = random.NextInt(homes.Count);
                Vector2 home = homes[pick];
                homes.RemoveAt(pick);
                (string model, bool female) = Citizens[i];
                residents.Add(Resident(root.transform, "Citizen_" + (i + 1), model, female ? feminine : masculine, World(home),
                    random.NextFloat() * 360f, false, converted));
            }

            var director = root.AddComponent<TownDirector>();
            director.Configure(settings, residents.ToArray(), plan.WanderPoints.Select(World).ToArray(),
                plan.Buildings.Select(b => World(b.Entrance)).Concat(plan.HouseDoors.Select(World)).ToArray(), 3610);
            EditorUtility.SetDirty(director);
        }

        /// <summary>
        /// 주민 하나: 루트에 상호작용·부딪힘용 캡슐, 길찾기 몸(처음엔 꺼 둔다), TownResident. 모델은 Visual 자식.
        /// </summary>
        static TownResident Resident(Transform parent, string name, string model, RuntimeAnimatorController controller, Vector3 position,
            float yaw, bool stationNpc, Dictionary<Material, Material> converted)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var body = root.AddComponent<CapsuleCollider>();
            body.radius = 0.4f; body.height = 1.85f; body.center = Vector3.up * 0.925f;
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f; agent.height = 1.85f; agent.speed = 1.4f; agent.acceleration = 10f; agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.2f; agent.avoidancePriority = 80;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.enabled = false;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            GameObject instance = GameCourtyardArtSetup.Spawn(visual, "Model", Characters + model, 0f, converted, false,
                materialFolder: MaterialFolder);
            if (!instance.TryGetComponent(out Animator animator)) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            // 외부 클립의 애니메이션 이벤트(발걸음 등)를 받는 빈 수신기. 없으면 이벤트마다 오류 로그가 난다.
            if (!instance.TryGetComponent(out CharacterAnimationEventSink _)) instance.AddComponent<CharacterAnimationEventSink>();

            var resident = root.AddComponent<TownResident>();
            resident.Configure(agent, animator, stationNpc, position, yaw);
            return resident;
        }

        /// <summary>서기·걷기·달리기를 속도(Speed)로 섞는 컨트롤러. 있으면 그대로 쓴다.</summary>
        static RuntimeAnimatorController Controller(string body)
        {
            string path = ArtFolder + "/AC_Town_" + body + ".controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;
            GameCourtyardArtSetup.EnsureFolder(ArtFolder);
            string folder = body == "Femn" ? "Feminine/" : "Masculine/";
            AnimationClip idle = Clip(Locomotion + folder + "Idle/A_Idle_Standing_" + body + ".fbx");
            AnimationClip walk = Clip(Locomotion + folder + "Locomotion/Walk/A_Walk_F_" + body + ".fbx");
            AnimationClip run = Clip(Locomotion + folder + "Locomotion/Run/A_Run_F_" + body + ".fbx");
            float walkSpeed = Clip(Locomotion + folder + "Locomotion/Walk/A_Walk_F_RootMotion_" + body + ".fbx").averageSpeed.magnitude;
            float runSpeed = Clip(Locomotion + folder + "Locomotion/Run/A_Run_F_RootMotion_" + body + ".fbx").averageSpeed.magnitude;
            if (walkSpeed <= 0.1f) walkSpeed = 1.4f;
            if (runSpeed <= walkSpeed) runSpeed = walkSpeed * 2.5f;

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(TownResident.SpeedParameter, AnimatorControllerParameterType.Float);
            var tree = new BlendTree
            {
                name = "Locomotion", blendType = BlendTreeType.Simple1D, blendParameter = TownResident.SpeedParameter,
                useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, walkSpeed);
            tree.AddChild(run, runSpeed);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.AddState("Locomotion");
            state.motion = tree;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimationClip Clip(string path)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview", StringComparison.Ordinal)) return clip;
            throw new InvalidOperationException("Missing animation " + path);
        }
    }
}
