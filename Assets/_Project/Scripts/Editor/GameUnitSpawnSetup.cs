using System;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// D-0b 모집 유닛 생성. 씬 부대원이 쓰는 아군 프리팹을 그 잡졸 정의의 씬 유닛 프리팹으로 넣고,
    /// Game 씬의 GameBattleController 옆에 GameUnitSpawner를 붙인다. AI·낙하 설정은 기존 부대원과 같은 에셋을 쓴다.
    /// 여러 번 실행해도 컴포넌트를 중복으로 만들지 않는다.
    /// </summary>
    public static class GameUnitSpawnSetup
    {
        [MenuItem("ProjectNeo/Setup/Connect Unit Spawning To Game Scene")]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
            Scene scene = EditorSceneManager.OpenScene(GameFlowSetup.GamePath, OpenSceneMode.Single);
            ConfigureScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureScene(Scene scene)
        {
            var battle = Find<GameBattleController>(scene) ?? throw new InvalidOperationException("GameBattleController is missing.");
            var center = Find<SquadCommandCenter>(scene) ?? throw new InvalidOperationException("SquadCommandCenter is missing.");
            var world = Find<ConstructionWorld>(scene) ?? throw new InvalidOperationException("ConstructionWorld is missing.");

            var rosters = new SerializedObject(center).FindProperty("rosters");
            UnitAISettings ai = null;
            FallSettings fall = null;
            SoldierDefinition first = null;
            for (int i = 0; i < rosters.arraySize; i++)
            {
                var members = rosters.GetArrayElementAtIndex(i).FindPropertyRelative("members");
                for (int j = 0; j < members.arraySize; j++)
                {
                    var member = members.GetArrayElementAtIndex(j).objectReferenceValue as UnitCombatAI;
                    if (member == null) continue;
                    if (ai == null) ai = new SerializedObject(member).FindProperty("settings").objectReferenceValue as UnitAISettings;
                    var falling = member.GetComponent<UnitFallController>();
                    if (fall == null && falling != null)
                        fall = new SerializedObject(falling).FindProperty("settings").objectReferenceValue as FallSettings;
                    SoldierDefinition soldier = member.Actor != null ? member.Actor.SoldierDefinition
                        : member.GetComponent<CharacterActor>().SoldierDefinition;
                    if (soldier == null) continue;
                    if (first == null) first = soldier;
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(member.gameObject);
                    if (source == null || soldier.UnitPrefab != null) continue;
                    soldier.SetUnitPrefab(source);
                    EditorUtility.SetDirty(soldier);
                }
            }
            if (ai == null || fall == null || first == null)
                throw new InvalidOperationException("Squad members with AI, fall settings and soldier definitions are required.");

            var spawner = battle.GetComponent<GameUnitSpawner>();
            if (spawner == null) spawner = battle.gameObject.AddComponent<GameUnitSpawner>();
            spawner.Configure(battle, center, world, ai, fall, first);
            EditorUtility.SetDirty(spawner);
        }

        static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            { var component = root.GetComponentInChildren<T>(true); if (component != null) return component; }
            return null;
        }
    }
}
