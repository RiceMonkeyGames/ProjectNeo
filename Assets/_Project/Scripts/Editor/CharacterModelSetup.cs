using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// ART-03: 유닛 프리팹의 <c>Visual</c> 아래에 Synty 캐릭터 모델을 넣고 임시 몸(Body)은 숨긴다.
    /// 서브모듈 머티리얼은 Built-in Standard 셰이더라 URP Lit 복사본을 <see cref="MaterialFolder"/>에 만들어 덮어쓴다.
    /// 다시 실행해도 같은 결과가 나오며, 유닛 프리팹을 새로 생성하는 설정 메뉴를 돌린 뒤에는 이 메뉴를 다시 실행한다.
    /// 애니메이션은 아직 붙이지 않는다(모델은 기본 자세).
    /// </summary>
    public static class CharacterModelSetup
    {
        public const string ModelName = "Model";
        public const string MaterialFolder = "Assets/_Project/Art/Characters/Materials";
        const string Units = "Assets/_Project/Prefabs/UnitBattlePrototype/";
        const string Siege = "Assets/_Project/Prefabs/SiegePrototype/";
        const string Kingdom = "Assets/ThirdParty/PolygonFantasyKingdom/Prefabs/Characters/";
        const string Rivals = "Assets/ThirdParty/PolygonFantasyRivals/Prefabs/Characters/";

        public const string AnimationFolder = "Assets/_Project/Art/Characters/Animation";
        const string Locomotion = "Assets/ThirdParty/Synty/AnimationBaseLocomotion/Animations/Polygon/";

        /// <summary>모델에 붙일 이동 애니메이션 묶음. Synty Base Locomotion(Humanoid)을 리타게팅한다.</summary>
        enum MotionSet { Masculine = 0, Feminine = 1, Flyer = 2 }

        /// <summary>유닛 프리팹과 넣을 모델. 변형 프리팹은 기반 프리팹을 먼저 처리해야 하므로 순서를 지킨다.</summary>
        static readonly (string unit, string model, MotionSet motion)[] Assignments =
        {
            (Units + "allied_melee.prefab", Kingdom + "SM_Chr_Soldier_Male_01.prefab", MotionSet.Masculine),
            (Units + "allied_ranged.prefab", Kingdom + "SM_Chr_Soldier_Female_01.prefab", MotionSet.Feminine),
            // 기반 프리팹: enemy_ladder는 hostile_melee를, enemy_archer는 hostile_ranged를 그대로 물려받는다.
            (Units + "hostile_melee.prefab", Rivals + "Character_BR_Slayer_01.prefab", MotionSet.Masculine),
            (Units + "hostile_ranged.prefab", Rivals + "Character_DarkElf_01.prefab", MotionSet.Feminine),
            (Siege + "enemy_gate_breaker.prefab", Rivals + "Character_BR_BigOrk_01.prefab", MotionSet.Masculine),
            (Siege + "enemy_wall_breaker.prefab", Rivals + "Character_BR_Troll_01.prefab", MotionSet.Masculine),
            (Siege + "enemy_flyer.prefab", Rivals + "Character_SpiritDemon_01.prefab", MotionSet.Flyer),
            (Siege + "enemy_flyer_high_archer.prefab", Rivals + "Character_BR_RedDemon_01.prefab", MotionSet.Flyer),
        };

        [MenuItem("ProjectNeo/Setup/Apply Character Models")]
        public static void Apply()
        {
            var converted = new Dictionary<Material, Material>();
            var controllers = new Dictionary<MotionSet, RuntimeAnimatorController>
            {
                [MotionSet.Masculine] = Ground("Masc"),
                [MotionSet.Feminine] = Ground("Femn"),
                [MotionSet.Flyer] = Flyer(),
            };
            foreach (var (unit, model, motion) in Assignments) ApplyTo(unit, model, controllers[motion], converted);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Speed 하나로 Idle → Walk → Run을 섞는 지상용 컨트롤러. 문턱값은 루트모션판 클립의 실제 이동 속도다.</summary>
        static RuntimeAnimatorController Ground(string body)
        {
            string path = AnimationFolder + "/AC_Unit_Ground_" + body + ".controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;
            EnsureFolder(AnimationFolder);
            string folder = body == "Femn" ? "Feminine/" : "Masculine/";
            AnimationClip idle = Clip(folder + "Idle/A_Idle_Standing_" + body + ".fbx");
            AnimationClip walk = Clip(folder + "Locomotion/Walk/A_Walk_F_" + body + ".fbx");
            AnimationClip run = Clip(folder + "Locomotion/Run/A_Run_F_" + body + ".fbx");
            float walkSpeed = Clip(folder + "Locomotion/Walk/A_Walk_F_RootMotion_" + body + ".fbx").averageSpeed.magnitude;
            float runSpeed = Clip(folder + "Locomotion/Run/A_Run_F_RootMotion_" + body + ".fbx").averageSpeed.magnitude;
            if (walkSpeed <= 0.1f) walkSpeed = 1.4f;
            if (runSpeed <= walkSpeed) runSpeed = walkSpeed * 2.5f;

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(CharacterModelAnimator.SpeedParameter, AnimatorControllerParameterType.Float);
            controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = CharacterModelAnimator.SpeedParameter;
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, walkSpeed);
            tree.AddChild(run, runSpeed);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>비행 적은 날개 짓 대신 공중 낙하 반복 자세를 계속 재생한다.</summary>
        static RuntimeAnimatorController Flyer()
        {
            string path = AnimationFolder + "/AC_Unit_Flyer.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;
            EnsureFolder(AnimationFolder);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(CharacterModelAnimator.SpeedParameter, AnimatorControllerParameterType.Float);
            controller.layers[0].stateMachine.AddState("Hover").motion = Clip("Masculine/InAir/A_InAir_FallShort_Masc.fbx");
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimationClip Clip(string relativePath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(Locomotion + relativePath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview", StringComparison.Ordinal)) return clip;
            throw new InvalidOperationException("Missing animation " + Locomotion + relativePath);
        }

        static void ApplyTo(string unitPath, string modelPath, RuntimeAnimatorController controller, Dictionary<Material, Material> converted)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null) throw new InvalidOperationException("Missing model " + modelPath + " (init the ThirdParty submodule).");
            GameObject root = PrefabUtility.LoadPrefabContents(unitPath);
            try
            {
                var actor = root.GetComponent<CharacterActor>();
                var view = root.GetComponentInChildren<PlaceholderCombatView>(true);
                if (actor == null || view == null) throw new InvalidOperationException(unitPath + " has no actor/view.");
                Transform visual = view.transform;

                // 변형 프리팹이 물려받은 모델은 지울 수 없으니 끄고, 자기 모델을 새로 단다.
                for (int i = visual.childCount - 1; i >= 0; i--)
                {
                    Transform child = visual.GetChild(i);
                    if (!child.name.StartsWith(ModelName, StringComparison.Ordinal)) continue;
                    if (IsInherited(root, child)) child.gameObject.SetActive(false);
                    else UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, visual);
                instance.name = ModelName;
                Transform model = instance.transform;
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = ToUrp(materials[i], converted);
                    renderer.sharedMaterials = materials;
                }
                FitHeight(model, actor.Profile.Height);

                var body = visual.Find("Body");
                if (body != null && body.TryGetComponent(out Renderer bodyRenderer)) bodyRenderer.enabled = false;
                view.ConfigureModel(model);
                EditorUtility.SetDirty(view);

                if (!instance.TryGetComponent(out Animator animator)) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                if (!visual.TryGetComponent(out CharacterModelAnimator driver)) driver = visual.gameObject.AddComponent<CharacterModelAnimator>();
                driver.Configure(actor, animator);
                EditorUtility.SetDirty(driver);
                PrefabUtility.SaveAsPrefabAsset(root, unitPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static bool IsInherited(GameObject root, Transform child)
        {
            // 변형 프리팹(루트에 원본이 있음)에서 이 변형이 직접 추가하지 않은 자식은 기반에서 물려받은 것이다.
            return PrefabUtility.GetCorrespondingObjectFromSource(root) != null
                && !PrefabUtility.IsAddedGameObjectOverride(child.gameObject);
        }

        /// <summary>켜진 렌더러의 높이를 전투 프로필 키에 맞춘다(충돌 캡슐과 같은 높이).</summary>
        static void FitHeight(Transform model, float height)
        {
            // 유닛 프리팹 루트가 꺼진 채 저장된 경우가 있어 모델 안에서의 activeSelf만 본다.
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!ShownWithin(renderer.transform, model)) continue;
                Bounds b = renderer is SkinnedMeshRenderer skinned ? SkinnedBounds(skinned) : renderer.bounds;
                if (first) { bounds = b; first = false; }
                else bounds.Encapsulate(b);
            }
            if (first) return;
            float current = bounds.max.y - model.position.y;
            if (current > 0.01f) model.localScale *= height / current;
        }

        static bool ShownWithin(Transform node, Transform top)
        {
            for (Transform t = node; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                if (t == top) return true;
            }
            return true;
        }

        static Bounds SkinnedBounds(SkinnedMeshRenderer skinned)
        {
            // 프리팹 편집 장면에서는 스킨 메쉬의 bounds가 갱신되지 않으므로 현재 자세로 구워서 잰다.
            var mesh = new Mesh();
            try
            {
                skinned.BakeMesh(mesh, true);
                Bounds local = mesh.bounds;
                Matrix4x4 toWorld = skinned.transform.localToWorldMatrix;
                var world = new Bounds(toWorld.MultiplyPoint3x4(local.center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? local.min.x : local.max.x, (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z);
                    world.Encapsulate(toWorld.MultiplyPoint3x4(corner));
                }
                return world;
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        /// <summary>Built-in 머티리얼의 텍스처·색을 옮긴 URP Lit 복사본을 만들거나 다시 쓴다.</summary>
        static Material ToUrp(Material source, Dictionary<Material, Material> converted)
        {
            if (source == null || source.shader.name.StartsWith("Universal Render Pipeline", StringComparison.Ordinal)) return source;
            if (converted.TryGetValue(source, out Material cached)) return cached;
            EnsureFolder(MaterialFolder);
            string path = MaterialFolder + "/" + source.name + "_URP.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created) material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = source.name + "_URP";
            if (source.HasProperty("_MainTex")) material.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            material.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            material.SetFloat("_Smoothness", 0.1f);
            material.SetFloat("_Metallic", 0f);
            material.enableInstancing = true;
            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            converted[source] = material;
            return material;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
