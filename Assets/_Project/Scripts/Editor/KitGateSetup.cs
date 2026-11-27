using System;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 7.5m 칸·5m 층 성문 프리팹(SIEGE-22, 사용자 결정 2026-11-27). 충돌체를 킷 성문 틀(<c>SM_Bld_Castle_Wall_Gate_01</c>, 5m 모듈을
    /// 칸 가운데에 둔다)에 맞춘다: 가장 좁은 문 홈 2.92m 폭, 문짝 3.85m 높이, 문짝은 바깥 면에서 0.54m 안쪽(모델 실측).
    /// 바깥·안쪽 면의 틀(양옆 막이와 문 위 인방), 통로 양옆 벽, 통로 천장, 문짝(부서지는 부분)으로 나눈다.
    /// 조각은 <see cref="ConstructionWorld.SetPose"/>가 칸 크기로 늘리므로 프리팹 값은 칸 기준 정규화 값이다.
    /// 5m 칸 시험 씬(SyntyGridSpike)은 옛 <c>MainGate.prefab</c>을 그대로 쓴다.
    /// </summary>
    public static class KitGateSetup
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/ConstructionPrototype/MainGate75.prefab";
        const float Cell = Grid75Migration.Cell, Height = Grid75Migration.Storey;
        // 킷 성문 실측(m). 바깥 면이 로컬 +z(회전 방향)다.
        const float HalfOpening = 1.46f;
        const float DoorHeight = 3.85f;
        const float DoorDepth = 0.55f;
        const float DoorThickness = 0.45f;
        // 틀 두께(면에서 안쪽으로), 통로 옆 벽판 두께, 통로 천장(위층 바닥) 두께.
        const float FrameDepth = 0.75f;
        const float SideThickness = 0.31f;
        const float CeilingThickness = 0.2f;

        static readonly string[] SevenAndHalfGates =
        {
            "Assets/_Project/Data/ConstructionPrototype/Gate.asset",
            "Assets/_Project/Data/SiegePrototype/Gate.asset",
            "Assets/_Project/Data/SyntyGrid75Spike/Gate.asset",
        };

        [MenuItem("ProjectNeo/Setup/Apply 7.5m Kit Gate (SIEGE-22)")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            GameObject prefab = Prefab();
            int changed = 0;
            foreach (string path in SevenAndHalfGates)
            {
                var gate = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(path);
                if (gate == null) continue;
                var data = new SerializedObject(gate);
                data.FindProperty("prefab").objectReferenceValue = prefab;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(gate);
                changed++;
            }
            AssetDatabase.SaveAssets();
            return $"MainGate75.prefab rebuilt; {changed} gate definitions use it.";
        }

        /// <summary>프리팹을 (다시) 만든다. 같은 경로에 덮어써 GUID를 지킨다.</summary>
        public static GameObject Prefab()
        {
            Material stone = CombatPrototypeSetup.MaterialAsset("ConstructionStone", new Color(0.58f, 0.66f, 0.75f));
            Material wood = CombatPrototypeSetup.MaterialAsset("ConstructionGate", new Color(0.63f, 0.44f, 0.24f));
            var root = new GameObject("MainGate75");
            try
            {
                var placed = root.AddComponent<PlacedStructure>();
                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                var arch = new GameObject("StoneArch").transform;
                arch.SetParent(visual, false);
                float half = Cell * 0.5f;
                foreach (float face in new[] { 1f, -1f })
                {
                    // 면 안쪽 FrameDepth 두께의 틀: 문 구멍 양옆 막이와 문 위 인방.
                    float zMin = face > 0f ? half - FrameDepth : -half, zMax = face > 0f ? half : -half + FrameDepth;
                    string side = face > 0f ? "Outer" : "Inner";
                    Solid(root, arch, side + "Left", -half, -HalfOpening, 0f, Height, zMin, zMax, stone);
                    Solid(root, arch, side + "Right", HalfOpening, half, 0f, Height, zMin, zMax, stone);
                    Solid(root, arch, side + "Lintel", -HalfOpening, HalfOpening, DoorHeight, Height, zMin, zMax, stone);
                }
                // 통로 양옆 벽(킷은 통로 쪽을 보는 벽판), 통로 천장(위층 바닥·맨 위면 걷는 면).
                Solid(root, arch, "PassageLeft", -half, -half + SideThickness, 0f, Height, -half, half, stone);
                Solid(root, arch, "PassageRight", half - SideThickness, half, 0f, Height, -half, half, stone);
                Solid(root, arch, "Ceiling", -half, half, Height - CeilingThickness, Height, -half, half, stone);

                float doorZ = half - DoorDepth;
                BoxCollider door = Collider(root, -HalfOpening, HalfOpening, 0f, DoorHeight,
                    doorZ - DoorThickness * 0.5f, doorZ + DoorThickness * 0.5f);
                GameObject doorVisual = Visual(visual, "Door", -HalfOpening, HalfOpening, 0f, DoorHeight,
                    doorZ - DoorThickness * 0.5f, doorZ + DoorThickness * 0.5f, wood);
                placed.SetDoorParts(door, doorVisual);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // 미터 범위(칸 가운데 기준, 바닥 0)를 칸 크기로 정규화한 상자.
        static void Solid(GameObject root, Transform parent, string name, float x0, float x1, float y0, float y1,
            float z0, float z1, Material material)
        {
            Collider(root, x0, x1, y0, y1, z0, z1);
            Visual(parent, name, x0, x1, y0, y1, z0, z1, material);
        }

        static BoxCollider Collider(GameObject root, float x0, float x1, float y0, float y1, float z0, float z1)
        {
            var collider = root.AddComponent<BoxCollider>();
            collider.center = Center(x0, x1, y0, y1, z0, z1);
            collider.size = Size(x0, x1, y0, y1, z0, z1);
            return collider;
        }

        static GameObject Visual(Transform parent, string name, float x0, float x1, float y0, float y1,
            float z0, float z1, Material material)
        {
            GameObject box = CombatPrototypeSetup.Box(name, Vector3.zero, Vector3.one, material);
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = Center(x0, x1, y0, y1, z0, z1);
            box.transform.localScale = Size(x0, x1, y0, y1, z0, z1);
            return box;
        }

        static Vector3 Center(float x0, float x1, float y0, float y1, float z0, float z1) =>
            new Vector3((x0 + x1) * 0.5f / Cell, (y0 + y1) * 0.5f / Height, (z0 + z1) * 0.5f / Cell);

        static Vector3 Size(float x0, float x1, float y0, float y1, float z0, float z1) =>
            new Vector3((x1 - x0) / Cell, (y1 - y0) / Height, (z1 - z0) / Cell);
    }
}
