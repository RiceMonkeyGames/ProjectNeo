using System;
using System.Linq;
using ProjectNeo.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>기존 씬의 참조를 유지하며 기본 시점을 1인칭으로 바꾼다.</summary>
    public static class FirstPersonSetup
    {
        [MenuItem("ProjectNeo/Setup/Upgrade Player Views To First Person")]
        public static void Upgrade()
        {
            if (EditorSceneManager.GetActiveScene().isDirty || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode and save the active scene first.");
            foreach (string path in new[] { SquadCommandSetup.ScenePath, "Assets/_Project/Scenes/CombatPrototype.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (ThirdPersonCamera previous in scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ThirdPersonCamera>(true)).ToArray())
                {
                    var oldData = new SerializedObject(previous);
                    var target = (Transform)oldData.FindProperty("target").objectReferenceValue;
                    var settings = (PlayerControlSettings)oldData.FindProperty("settings").objectReferenceValue;
                    GameObject cameraRoot = previous.gameObject;
                    var view = cameraRoot.AddComponent<FirstPersonCamera>();
                    view.Configure(target, settings);
                    foreach (PlayerInputController input in scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<PlayerInputController>(true)))
                    {
                        var data = new SerializedObject(input);
                        if (data.FindProperty("view").objectReferenceValue != previous) continue;
                        data.FindProperty("view").objectReferenceValue = view;
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    cameraRoot.name = "FirstPersonCamera";
                    cameraRoot.transform.SetPositionAndRotation(target.position + Vector3.up * settings.CameraHeight,
                        Quaternion.Euler(settings.InitialPitch, target.eulerAngles.y, 0f));
                    UnityEngine.Object.DestroyImmediate(previous);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
