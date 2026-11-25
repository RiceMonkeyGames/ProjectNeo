using System;
using System.Collections.Generic;
using ProjectNeo.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// ART-03/ART-04: 유닛 프리팹의 <c>Visual</c> 아래에 Synty 캐릭터 모델과 손 무기를 넣고 임시 몸(Body)은 숨긴다.
    /// 서브모듈 머티리얼은 Built-in Standard 셰이더라 URP Lit 복사본을 <see cref="MaterialFolder"/>에 만들어 덮어쓴다.
    /// 애니메이터 컨트롤러·상체 마스크·애니메이션 프로필은 매번 다시 만든다(생성물). 다시 실행해도 같은 결과가 나오며,
    /// 유닛 프리팹을 새로 생성하는 설정 메뉴를 돌린 뒤에는 이 메뉴를 다시 실행한다.
    /// </summary>
    public static class CharacterModelSetup
    {
        public const string ModelName = "Model";
        public const string WeaponName = "Weapon";
        public const string SheathName = "WeaponSheath";
        public const string MaterialFolder = "Assets/_Project/Art/Characters/Materials";
        public const string AnimationFolder = "Assets/_Project/Art/Characters/Animation";
        public const string UpperBodyMaskPath = AnimationFolder + "/AM_UpperBody.mask";
        public const string LowerBodyMaskPath = AnimationFolder + "/AM_LowerBody.mask";
        const string Units = "Assets/_Project/Prefabs/UnitBattlePrototype/";
        const string Siege = "Assets/_Project/Prefabs/SiegePrototype/";
        const string Kingdom = "Assets/ThirdParty/PolygonFantasyKingdom/Prefabs/";
        const string Rivals = "Assets/ThirdParty/PolygonFantasyRivals/Prefabs/";
        const string Elven = "Assets/ThirdParty/PolygonElvenRealm/Prefabs/";
        const string Locomotion = "Assets/ThirdParty/Synty/AnimationBaseLocomotion/Animations/Polygon/";
        const string Human = "Assets/ThirdParty/Kevin Iglesias/Human Animations/Animations/";
        const string ArcherPack = "Assets/ThirdParty/ExplosiveLLC/Warrior Pack Bundle 2 FREE/Archer Warrior Mecanim Animation Pack/Animations/";
        const int MeleeAttackCount = 5;
        const int DeathCount = 6;

        /// <summary>몸 동작 묶음. 지상 남·여는 Synty 이동 + Kevin Iglesias 전투 클립, 비행은 공중 자세 + 남성 전투 클립.</summary>
        enum Body { Masculine = 0, Feminine = 1, Flyer = 2 }

        /// <summary>손에 드는 무기 종류. 활이면 왼손, 근접이면 오른손에 든다.</summary>
        enum Style { Melee = 0, Bow = 1 }

        /// <summary>손에 붙일 무기와 손 뼈 기준 위치·회전. 모델 비율은 유닛 키에 맞춰 줄어든 뒤라 위치는 뼈 공간 값이다.</summary>
        readonly struct Weapon
        {
            public readonly string Path;
            public readonly Vector3 Position;
            public readonly Vector3 Euler;
            public Weapon(string path, Vector3 position, Vector3 euler) { Path = path; Position = position; Euler = euler; }
        }

        // 근접 무기 메쉬는 손잡이에서 날이 Y로 뻗는데, 그대로 붙이면 Kevin 클립의 주먹에서 날이 새끼손가락 쪽으로 나와 몸을 관통한다.
        // X축 180도로 뒤집어 날이 엄지 쪽(전투 대기에서 앞·위)으로 나오게 한다(ART-04 피드백 7차).
        static readonly Vector3 MeleeEuler = new Vector3(180f, 0f, 0f);
        static Weapon Sword => new Weapon(Kingdom + "Weapons/SM_Wep_Sword_01.prefab", Vector3.zero, MeleeEuler);
        // 활 메쉬: 긴 축 Y, 휜 방향 Z(시위는 -Z 쪽), 얇은 축 X. 왼손 뼈(Synty 공통 축)는 앞(Z)이 위, 오른쪽(X)이 궁수 쪽(뒤)이다.
        // 메쉬 Y를 손 Z(위)로, 메쉬 Z를 손 -X(과녁 쪽)로 돌려 활을 세우고 시위가 궁수 쪽에 오게 한다 = LookRotation(-X, Z).
        static readonly Vector3 BowEuler = new Vector3(0f, 270f, 270f);
        static Weapon Bow => new Weapon(Kingdom + "Weapons/SM_Prop_Bow_01.prefab", Vector3.zero, BowEuler);
        // 다크엘프·붉은 악마 전용 무기(곡도·지팡이)는 활이 아니라 궁수에게는 Kingdom 활을 쥐여 준다.
        static Weapon Bow2 => new Weapon(Kingdom + "Weapons/SM_Prop_Bow_02.prefab", Vector3.zero, BowEuler);
        static Weapon Rival(string name) => new Weapon(Rivals + "Weapons/SM_Wep_" + name + "_01.prefab", Vector3.zero, MeleeEuler);

        /// <summary>유닛 프리팹과 넣을 모델·무기. 변형 프리팹은 기반 프리팹을 먼저 처리해야 하므로 순서를 지킨다.</summary>
        static readonly (string unit, string model, Body body, Style style, Weapon weapon)[] Assignments =
        {
            (Units + "allied_melee.prefab", Kingdom + "Characters/SM_Chr_Soldier_Male_01.prefab", Body.Masculine, Style.Melee, Sword),
            // 아군 궁수는 근접병(Kingdom 병사)과 구분되도록 엘프 왕국 병사 모델을 쓴다(ART-04 피드백 6차).
            (Units + "allied_ranged.prefab", Elven + "Characters/SM_Chr_Soldier_Female_01.prefab", Body.Masculine, Style.Bow, Bow),
            // 기반 프리팹: enemy_ladder는 hostile_melee를, enemy_archer는 hostile_ranged를 그대로 물려받는다.
            (Units + "hostile_melee.prefab", Rivals + "Characters/Character_BR_Slayer_01.prefab", Body.Masculine, Style.Melee, Rival("Slayer")),
            (Units + "hostile_ranged.prefab", Rivals + "Characters/Character_DarkElf_01.prefab", Body.Masculine, Style.Bow, Bow2),
            (Siege + "enemy_gate_breaker.prefab", Rivals + "Characters/Character_BR_BigOrk_01.prefab", Body.Masculine, Style.Melee, Rival("BigOrk")),
            (Siege + "enemy_wall_breaker.prefab", Rivals + "Characters/Character_BR_Troll_01.prefab", Body.Masculine, Style.Melee, Rival("Troll")),
            (Siege + "enemy_flyer.prefab", Rivals + "Characters/Character_SpiritDemon_01.prefab", Body.Flyer, Style.Melee, Rival("SpiritDemon")),
            (Siege + "enemy_flyer_high_archer.prefab", Rivals + "Characters/Character_BR_RedDemon_01.prefab", Body.Flyer, Style.Bow, Bow2),
        };

        [MenuItem("ProjectNeo/Setup/Apply Character Models")]
        public static void Apply()
        {
            var converted = new Dictionary<Material, Material>();
            // ART-03의 이동 전용 컨트롤러는 몸·무기별 컨트롤러로 바뀌었다.
            // 궁수도 남성 동작 묶음을 쓰게 되어(ART-04 피드백 5차) 여성 궁수 묶음은 지운다.
            foreach (string profileLegacy in new[] { "CAP_Unit_Ground_Femn_Bow" })
                AssetDatabase.DeleteAsset(AnimationFolder + "/" + profileLegacy + ".asset");
            foreach (string legacy in new[] { "AC_Unit_Ground_Masc", "AC_Unit_Ground_Femn", "AC_Unit_Flyer", "AC_Unit_Ground_Femn_Bow" })
                AssetDatabase.DeleteAsset(AnimationFolder + "/" + legacy + ".controller");
            AvatarMask mask = UpperBodyMask();
            var sets = new Dictionary<(Body, Style), (RuntimeAnimatorController controller, CharacterAnimationProfile profile)>();
            foreach (var (unit, model, body, style, weapon) in Assignments)
            {
                if (!sets.TryGetValue((body, style), out var set))
                {
                    set = BuildSet(body, style, mask, model);
                    sets[(body, style)] = set;
                }
                ApplyTo(unit, model, body, weapon, style, set.controller, set.profile, converted);
            }
            AssetDatabase.SaveAssets();
        }

        const string PlayerPrefab = "Assets/_Project/Prefabs/CombatPrototype/Player.prefab";
        const string PlayerModel = "Assets/ThirdParty/PolygonKnights/Prefabs/Characters/Character_Knight_01_Blue.prefab";
        // 플레이어 모델 키(m). 충돌 몸체(2m)보다 작게 해 눈(키의 약 92%)이 1인칭 눈높이(1.6m + 웅크림 없음)와 맞게 한다.
        const float PlayerModelHeight = 1.78f;

        /// <summary>
        /// 플레이어(지휘관)에 PolygonKnights 기사 모델과 칼을 붙인다(SIEGE-36, 사용자 결정 2026-11-25).
        /// 근접 아군과 같은 남성·근접 애니메이터와 동작 프로필을 그대로 쓴다(이미 있는 에셋만 읽고 새로 만들지 않는다).
        /// 1인칭에서 몸이 보이는 처리는 FirstPersonCamera가 맡는다.
        /// </summary>
        [MenuItem("ProjectNeo/Setup/Apply Player Model (SIEGE-36)")]
        public static void ApplyPlayerModel()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimationFolder + "/AC_Unit_Ground_Masc_Melee.controller")
                ?? throw new InvalidOperationException("Run Apply Character Models first (missing AC_Unit_Ground_Masc_Melee).");
            var profile = AssetDatabase.LoadAssetAtPath<CharacterAnimationProfile>(AnimationFolder + "/CAP_Unit_Ground_Masc_Melee.asset")
                ?? throw new InvalidOperationException("Run Apply Character Models first (missing CAP_Unit_Ground_Masc_Melee).");
            ApplyTo(PlayerPrefab, PlayerModel, Body.Masculine, Sword, Style.Melee, controller, profile, new Dictionary<Material, Material>(),
                PlayerModelHeight);
            AssetDatabase.SaveAssets();
        }

        // ── 컨트롤러 ────────────────────────────────────────────

        static (RuntimeAnimatorController, CharacterAnimationProfile) BuildSet(Body body, Style style, AvatarMask mask, string measureModel)
        {
            EnsureFolder(AnimationFolder);
            string name = (body == Body.Flyer ? "Flyer" : "Ground_" + (body == Body.Feminine ? "Femn" : "Masc")) + "_" + style;
            string path = AnimationFolder + "/AC_Unit_" + name + ".controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(CharacterModelAnimator.SpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterModelAnimator.AimPitchParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterModelAnimator.MoveXParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterModelAnimator.MoveZParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = CharacterModelAnimator.ActionSpeedParameter,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,
            });

            string sex = body == Body.Feminine ? "Female" : "Male";
            string tag = body == Body.Feminine ? "HumanF@" : "HumanM@";
            Func<string, AnimationClip> human = file => Clip(Human + sex + "/" + file.Replace("@", tag) + ".fbx");

            // 기본 레이어: 이동·교전 이동·웅크리기·사망.
            AnimatorStateMachine root = controller.layers[0].stateMachine;
            bool ground = body != Body.Flyer;
            if (ground)
            {
                string synty = body == Body.Feminine ? "Femn" : "Masc";
                string folder = body == Body.Feminine ? "Feminine/" : "Masculine/";
                AnimationClip idle = Clip(Locomotion + folder + "Idle/A_Idle_Standing_" + synty + ".fbx");
                AnimationClip walk = Clip(Locomotion + folder + "Locomotion/Walk/A_Walk_F_" + synty + ".fbx");
                AnimationClip run = Clip(Locomotion + folder + "Locomotion/Run/A_Run_F_" + synty + ".fbx");
                float walkSpeed = Clip(Locomotion + folder + "Locomotion/Walk/A_Walk_F_RootMotion_" + synty + ".fbx").averageSpeed.magnitude;
                float runSpeed = Clip(Locomotion + folder + "Locomotion/Run/A_Run_F_RootMotion_" + synty + ".fbx").averageSpeed.magnitude;
                if (walkSpeed <= 0.1f) walkSpeed = 1.4f;
                if (runSpeed <= walkSpeed) runSpeed = walkSpeed * 2.5f;
                AnimationClip combatIdle = human(style == Style.Bow ? "Combat/Bow/@BowIdle01" : "Combat/1H/@CombatIdle1H01");
                if (style == Style.Bow)
                {
                    // 궁수는 대상을 보며 옆·뒤로 비켜서는 일이 많아 앞으로 걷는 클립만 쓰면 미끄러져 보인다.
                    // 근접과 같은 Synty Base Locomotion의 8방향 걷기·달리기를 몸 기준 이동 방향(MoveX·MoveZ)으로 섞는다.
                    // 앞으로 걷기만 ExplosiveLLC 궁수 팩의 Archer@Walk(사용자 선택). 무료판이라 옆·뒤 클립이 없어 나머지는 Synty를 쓴다.
                    AnimationClip archerWalk = Clip(ArcherPack + "Archer@Walk.FBX");
                    root.defaultState = AddDirectional(controller, root, CharacterModelAnimator.LocomotionState, idle, folder, synty, walkSpeed, runSpeed, archerWalk);
                    AddDirectional(controller, root, CharacterModelAnimator.CombatLocomotionState, combatIdle, folder, synty, walkSpeed, runSpeed, archerWalk);
                }
                else
                {
                    root.defaultState = AddBlend(controller, root, CharacterModelAnimator.LocomotionState, CharacterModelAnimator.SpeedParameter,
                        (idle, 0f), (walk, walkSpeed), (run, runSpeed));
                    AddBlend(controller, root, CharacterModelAnimator.CombatLocomotionState, CharacterModelAnimator.SpeedParameter,
                        (combatIdle, 0f), (walk, walkSpeed), (run, runSpeed));
                }
                AnimationClip crouchWalk = human("Movement/Crouch/CrouchWalk/@Crouch01_Walk_Forward");
                float crouchSpeed = human("Movement/Crouch/CrouchWalk/RootMotion/@Crouch01_Walk_Forward [RM]").averageSpeed.magnitude;
                if (crouchSpeed <= 0.1f) crouchSpeed = 0.8f;
                AddBlend(controller, root, CharacterModelAnimator.CrouchState, CharacterModelAnimator.SpeedParameter,
                    (human("Movement/Crouch/@Crouch01_Idle"), 0f), (crouchWalk, crouchSpeed));
                for (int i = 0; i < DeathCount; i++)
                    root.AddState(CharacterModelAnimator.DeathState(i)).motion = human("Combat/@CombatDeath0" + (i + 1));
            }
            else
            {
                AnimatorState hover = root.AddState(CharacterModelAnimator.HoverState);
                hover.motion = Clip(Locomotion + "Masculine/InAir/A_InAir_FallShort_Masc.fbx");
                root.defaultState = hover;
            }

            // 동작 레이어(상체): 비어 있는 상태가 기본이며 레이어 무게는 늘 1이다.
            controller.AddLayer("Action");
            AnimatorControllerLayer[] layers = controller.layers;
            layers[CharacterModelAnimator.ActionLayer].avatarMask = mask;
            layers[CharacterModelAnimator.ActionLayer].defaultWeight = 1f;
            layers[CharacterModelAnimator.ActionLayer].blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;
            AnimatorStateMachine action = controller.layers[CharacterModelAnimator.ActionLayer].stateMachine;
            action.defaultState = action.AddState(CharacterModelAnimator.EmptyState);
            AnimationClip hit = human("Combat/@Damage01");
            action.AddState(CharacterModelAnimator.HitState).motion = hit;

            var contacts = new List<float>();
            var lengths = new List<float>();
            float load = 0.83f, release = 0.8f;
            (float down, float middle, float up)? bowPitch = null;
            (float grab, float unsheatheLength, float release, float sheatheLength) sheathMotion = default;
            if (style == Style.Melee)
            {
                for (int i = 0; i < MeleeAttackCount; i++)
                {
                    AnimationClip attack = human("Combat/1H/@Attack1H0" + (i + 1) + "_R");
                    action.AddState(CharacterModelAnimator.AttackState(i)).motion = attack;
                    contacts.Add(MeasureContact(measureModel, attack));
                    lengths.Add(attack.length);
                }
                if (ground)
                {
                    // 허리 칼집에서 뽑고 넣는 동작(오른손). 손이 허리에 가장 가까운 순간에 무기를 옮긴다.
                    AnimationClip unsheathe = UnsheatheClip(sex, tag);
                    AnimationClip sheathe = Clip(Human + sex + "/Misc/Unsheathe/" + tag + "SheatheHips01_R.fbx");
                    action.AddState(CharacterModelAnimator.UnsheatheState).motion = unsheathe;
                    action.AddState(CharacterModelAnimator.SheatheState).motion = sheathe;
                    sheathMotion = (HandNearHipsTime(measureModel, unsheathe), unsheathe.length,
                        HandNearHipsTime(measureModel, sheathe), sheathe.length);
                }
            }
            else
            {
                AnimationClip Bow(string part, string aim) => human("Combat/Bow/@BowShot01" + aim + " - " + part);
                foreach (var (state, part) in new[]
                {
                    (CharacterModelAnimator.BowLoadState, "Load"),
                    (CharacterModelAnimator.BowHoldState, "Hold"),
                    (CharacterModelAnimator.BowReleaseState, "Release"),
                })
                {
                    AnimatorState added = AddBlend(controller, action, state, CharacterModelAnimator.AimPitchParameter,
                        (Bow(part, "_Down"), -1f), (Bow(part, ""), 0f), (Bow(part, "_Up"), 1f));
                    added.speedParameter = CharacterModelAnimator.ActionSpeedParameter;
                    added.speedParameterActive = true;
                }
                load = Bow("Load", "").length;
                release = Bow("Release", "").length;
                // 활을 당긴 자세에서 화살 선이 몇 도를 보는지 클립마다 잰다(COMBAT-11 활 기울기).
                bowPitch = (MeasureBowPitch(measureModel, Bow("Hold", "_Down")), MeasureBowPitch(measureModel, Bow("Hold", "")),
                    MeasureBowPitch(measureModel, Bow("Hold", "_Up")));
                if (ground)
                {
                    // 동작 레이어와 같은 상태를 다리에만 적용하는 동기화 레이어. 무게는 런타임에 서 있을 때만 올린다.
                    var legs = new AnimatorControllerLayer
                    {
                        name = "BowLegs",
                        avatarMask = LowerBodyMask(),
                        syncedLayerIndex = CharacterModelAnimator.ActionLayer,
                        defaultWeight = 0f,
                        blendingMode = AnimatorLayerBlendingMode.Override,
                        stateMachine = new AnimatorStateMachine { name = "BowLegs", hideFlags = HideFlags.HideInHierarchy },
                    };
                    AssetDatabase.AddObjectToAsset(legs.stateMachine, controller);
                    controller.AddLayer(legs);
                }
            }
            EditorUtility.SetDirty(controller);

            string profilePath = AnimationFolder + "/CAP_Unit_" + name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<CharacterAnimationProfile>(profilePath);
            bool created = profile == null;
            if (created) profile = ScriptableObject.CreateInstance<CharacterAnimationProfile>();
            profile.Set(ground, ground, ground, contacts.ToArray(), lengths.ToArray(), ground ? DeathCount : 0,
                hit.length, load, release, engagedSeconds: 2.5f, crossFadeSeconds: 0.25f);
            profile.SetSheathMotion(sheathMotion.grab, sheathMotion.unsheatheLength, sheathMotion.release, sheathMotion.sheatheLength);
            if (bowPitch.HasValue) profile.SetBowPitch(bowPitch.Value.down, bowPitch.Value.middle, bowPitch.Value.up);
            if (created) AssetDatabase.CreateAsset(profile, profilePath);
            else EditorUtility.SetDirty(profile);
            return (controller, profile);
        }

        static AnimatorState AddBlend(AnimatorController controller, AnimatorStateMachine machine, string name, string parameter,
            params (AnimationClip clip, float threshold)[] children)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = parameter,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            foreach (var (clip, threshold) in children) tree.AddChild(clip, threshold);
            AnimatorState state = machine.AddState(name);
            state.motion = tree;
            return state;
        }

        /// <summary>
        /// 제자리 + 8방향 걷기 + 8방향 달리기를 몸 기준 이동 속도(MoveX·MoveZ)로 섞는 2D 블렌드.
        /// 클립은 근접 유닛과 같은 Synty Base Locomotion이다. 앞은 A_Walk_F/A_Run_F, 옆·앞옆은 FwdStrafe, 뒤·뒤옆은 BckStrafe.
        /// 위치는 앞으로 걷기·달리기 루트모션판의 실제 이동 속도다.
        /// <paramref name="forwardWalk"/>가 있으면 앞으로 걷기만 그 클립으로 바꾸고, 클립 자체의 이동 속도 위치에 둔다(발 미끄러짐 방지).
        /// </summary>
        static AnimatorState AddDirectional(AnimatorController controller, AnimatorStateMachine machine, string name,
            AnimationClip idle, string syntyFolder, string synty, float walkSpeed, float runSpeed, AnimationClip forwardWalk = null)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = CharacterModelAnimator.MoveXParameter,
                blendParameterY = CharacterModelAnimator.MoveZParameter,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, Vector2.zero);
            var directions = new (string code, Vector2 dir)[]
            {
                ("F", new Vector2(0f, 1f)), ("FwdStrafeFR", new Vector2(0.7071f, 0.7071f)), ("FwdStrafeR", new Vector2(1f, 0f)),
                ("BckStrafeBR", new Vector2(0.7071f, -0.7071f)), ("BckStrafeB", new Vector2(0f, -1f)),
                ("BckStrafeBL", new Vector2(-0.7071f, -0.7071f)), ("FwdStrafeL", new Vector2(-1f, 0f)), ("FwdStrafeFL", new Vector2(-0.7071f, 0.7071f)),
            };
            foreach (var (gait, speed) in new[] { ("Walk", walkSpeed), ("Run", runSpeed) })
                foreach (var (code, dir) in directions)
                {
                    if (forwardWalk != null && gait == "Walk" && code == "F")
                    {
                        float own = forwardWalk.averageSpeed.magnitude;
                        tree.AddChild(forwardWalk, dir * (own > 0.1f && own < runSpeed ? own : speed));
                        continue;
                    }
                    tree.AddChild(Clip(Locomotion + syntyFolder + "Locomotion/" + gait + "/A_" + gait + "_" + code + "_" + synty + ".fbx"), dir * speed);
                }
            AnimatorState state = machine.AddState(name);
            state.motion = tree;
            return state;
        }

        /// <summary>팔·몸통·머리만 움직이고 다리와 뿌리는 기본 레이어(이동)를 따르는 마스크.</summary>
        static AvatarMask UpperBodyMask() => BodyMask(UpperBodyMaskPath, upper: true);

        /// <summary>다리만 움직이는 마스크. 활을 당긴 채 서 있을 때 다리도 활 자세(옆으로 선 자세)를 따르게 한다.</summary>
        static AvatarMask LowerBodyMask() => BodyMask(LowerBodyMaskPath, upper: false);

        static AvatarMask BodyMask(string path, bool upper)
        {
            EnsureFolder(AnimationFolder);
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            bool created = mask == null;
            if (created) mask = new AvatarMask();
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                bool isUpper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                    || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                    || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers
                    || part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
                bool isLower = part == AvatarMaskBodyPart.LeftLeg || part == AvatarMaskBodyPart.RightLeg
                    || part == AvatarMaskBodyPart.LeftFootIK || part == AvatarMaskBodyPart.RightFootIK;
                mask.SetHumanoidBodyPartActive(part, upper ? isUpper : isLower);
            }
            if (created) AssetDatabase.CreateAsset(mask, path);
            else EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>
        /// 근접 공격 클립에서 무기가 닿는 순간(초)을 잰다. 오른손이 가슴 기준으로 가장 빠르게 움직이는 순간을 닿는 순간으로 본다.
        /// </summary>
        static AnimationClip UnsheatheClip(string sex, string tag) => Clip(Human + sex + "/Misc/Unsheathe/" + tag + "UnsheatheHips01_R.fbx");

        /// <summary>뽑기·넣기 클립에서 오른손이 엉덩이 뼈에 가장 가까운 순간(초). 손이 허리 칼자루를 잡거나 놓는 순간으로 본다.</summary>
        static float HandNearHipsTime(string modelPath, AnimationClip clip)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), scene);
                var animator = instance.GetComponent<Animator>();
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                float best = clip.length * 0.5f, nearest = float.PositiveInfinity;
                for (float t = clip.length * 0.1f; t <= clip.length * 0.9f; t += 1f / 60f)
                {
                    clip.SampleAnimation(instance, t);
                    float distance = (hand.position - hips.position).sqrMagnitude;
                    if (distance < nearest) { nearest = distance; best = t; }
                }
                return best;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        /// <summary>
        /// 허리 칼집 자리(엉덩이 뼈 기준). 위치는 뽑기 클립에서 손이 칼자루를 잡는 순간의 오른손 위치(뽑을 때 손이 바로 칼자루에 닿게).
        /// 방향은 날이 다리를 따라 아래·뒤로 늘어지고 납작한 면이 바깥을 보게 하며, 긴 무기는 땅에 닿지 않도록 길이에 맞춰 뒤로 눕힌다.
        /// 무기는 칼집 아래에서도 손에서와 같은 상대 자세(<paramref name="weaponInHand"/>)를 쓰므로, 칼집 회전에서 그만큼 빼 둔다.
        /// </summary>
        static (Vector3 position, Quaternion rotation) MeasureSheathPose(string modelPath, AnimationClip unsheathe, float grabSeconds,
            string weaponPath, Quaternion weaponInHand)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), scene);
                var animator = instance.GetComponent<Animator>();
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                unsheathe.SampleAnimation(instance, grabSeconds);
                Vector3 position = hips.InverseTransformPoint(hand.position);

                // 평소 서 있는 자세에서 몸 기준 방향과 엉덩이 높이를 잰다.
                Clip(Locomotion + "Masculine/Idle/A_Idle_Standing_Masc.fbx").SampleAnimation(instance, 0.5f);
                Transform root = instance.transform;
                // 칼자루(손잡이) 높이: 평소 자세에서 칼집 자리가 땅에서 얼마나 위인지.
                float gripHeight = hips.TransformPoint(position).y - root.position.y;
                float tip = 0f, thickness = 0f;
                foreach (var filter in AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath).GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    Bounds b = filter.sharedMesh.bounds;
                    tip = Mathf.Max(tip, b.max.y);
                    thickness = Mathf.Max(thickness, Mathf.Max(b.size.x, b.size.z) * 0.5f);
                }
                tip *= hand.lossyScale.y;
                thickness *= hand.lossyScale.y;
                // 끝(망치 머리 두께 포함)이 땅 위 15cm에 오도록, 길면 뒤로 더 눕힌다(10~75도).
                float drop = gripHeight - 0.15f - thickness;
                float sin = tip > 0.01f ? Mathf.Clamp(drop / tip, Mathf.Sin(10f * Mathf.Deg2Rad), Mathf.Sin(75f * Mathf.Deg2Rad))
                    : Mathf.Sin(70f * Mathf.Deg2Rad);
                float cos = Mathf.Sqrt(1f - sin * sin);
                Vector3 blade = new Vector3(0f, -sin, -cos);
                Vector3 edge = Vector3.Cross(Vector3.right, blade);
                Quaternion inBody = Quaternion.LookRotation(edge, blade);
                Quaternion inHips = Quaternion.Inverse(hips.rotation) * (root.rotation * inBody);
                return (position, inHips * Quaternion.Inverse(weaponInHand));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static float MeasureContact(string modelPath, AnimationClip clip)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), scene);
                var animator = instance.GetComponent<Animator>();
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
                const float step = 1f / 60f;
                float best = clip.length * 0.4f, bestSpeed = -1f;
                Vector3 previous = Vector3.zero;
                for (float t = 0f; t <= clip.length * 0.8f; t += step)
                {
                    clip.SampleAnimation(instance, t);
                    Vector3 local = chest.InverseTransformPoint(hand.position);
                    if (t > 0f && t >= clip.length * 0.1f)
                    {
                        float speed = (local - previous).magnitude / step;
                        if (speed > bestSpeed) { bestSpeed = speed; best = t; }
                    }
                    previous = local;
                }
                return best;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        // 활을 당긴 채 멈춘 클립 가운데 순간에 시위 손(오른손)→활 손(왼손) 선의 높이각(도). 런타임 AimOffset과 같은 선이다.
        static float MeasureBowPitch(string modelPath, AnimationClip clip)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), scene);
                var animator = instance.GetComponentInChildren<Animator>();
                Transform bowHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform stringHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                clip.SampleAnimation(instance, clip.length * 0.5f);
                return CharacterAnimationRules.PitchDegrees(bowHand.position - stringHand.position);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static AnimationClip Clip(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview", StringComparison.Ordinal)) return clip;
            throw new InvalidOperationException("Missing animation " + path);
        }

        // ── 프리팹 ──────────────────────────────────────────────

        static void ApplyTo(string unitPath, string modelPath, Body motion, Weapon weapon, Style style, RuntimeAnimatorController controller,
            CharacterAnimationProfile profile, Dictionary<Material, Material> converted, float modelHeight = 0f)
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
                ConvertMaterials(instance, converted);
                FitHeight(model, modelHeight > 0f ? modelHeight : actor.Profile.Height);

                var body = visual.Find("Body");
                if (body != null && body.TryGetComponent(out Renderer bodyRenderer)) bodyRenderer.enabled = false;
                view.ConfigureModel(model);
                EditorUtility.SetDirty(view);

                if (!instance.TryGetComponent(out Animator animator)) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                // 외부 클립의 애니메이션 이벤트(발걸음 등)를 받는 빈 수신기. 없으면 이벤트마다 오류 로그가 난다.
                if (!instance.TryGetComponent(out CharacterAnimationEventSink _)) instance.AddComponent<CharacterAnimationEventSink>();
                Transform held = AttachWeapon(animator, weapon, style, converted);
                Transform sheath = null;
                if (style == Style.Melee && motion != Body.Flyer && profile.HasSheathMotion)
                {
                    // 지상 근접병은 평소 무기를 허리에 찬다. 칼집 자리는 이 모델로 잰 값(엉덩이 뼈 기준).
                    bool female = motion == Body.Feminine;
                    var (position, rotation) = MeasureSheathPose(modelPath,
                        UnsheatheClip(female ? "Female" : "Male", female ? "HumanF@" : "HumanM@"), profile.UnsheatheGrabSeconds,
                        weapon.Path, Quaternion.Euler(weapon.Euler));
                    sheath = new GameObject(SheathName).transform;
                    sheath.SetParent(animator.GetBoneTransform(HumanBodyBones.Hips), false);
                    sheath.localPosition = position;
                    sheath.localRotation = rotation;
                    // 무기는 손에서와 같은 상대 자세로 칼집 자리에 둔다(뽑을 때 튀지 않게).
                    held.SetParent(sheath, false);
                }
                if (!visual.TryGetComponent(out CharacterModelAnimator driver)) driver = visual.gameObject.AddComponent<CharacterModelAnimator>();
                driver.Configure(actor, animator, profile, held, sheath);
                EditorUtility.SetDirty(driver);
                PrefabUtility.SaveAsPrefabAsset(root, unitPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>무기 프리팹을 손 뼈 아래에 붙인다. 활은 왼손, 근접 무기는 오른손. 충돌체는 지운다(판정은 유닛 몸통이 맡는다).</summary>
        static Transform AttachWeapon(Animator animator, Weapon weapon, Style style, Dictionary<Material, Material> converted)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(weapon.Path);
            if (asset == null) throw new InvalidOperationException("Missing weapon " + weapon.Path);
            Transform hand = animator.GetBoneTransform(style == Style.Bow ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (hand == null) throw new InvalidOperationException(animator.name + " has no hand bone.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, hand);
            instance.name = WeaponName;
            instance.transform.localPosition = weapon.Position;
            instance.transform.localRotation = Quaternion.Euler(weapon.Euler);
            instance.transform.localScale = Vector3.one;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            ConvertMaterials(instance, converted);
            return instance.transform;
        }

        static void ConvertMaterials(GameObject instance, Dictionary<Material, Material> converted)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = ToUrp(materials[i], converted);
                renderer.sharedMaterials = materials;
            }
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
        internal static Material ToUrp(Material source, Dictionary<Material, Material> converted, string folder = MaterialFolder)
        {
            if (source == null || source.shader.name.StartsWith("Universal Render Pipeline", StringComparison.Ordinal)) return source;
            if (converted.TryGetValue(source, out Material cached)) return cached;
            EnsureFolder(folder);
            string path = folder + "/" + source.name + "_URP.mat";
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
