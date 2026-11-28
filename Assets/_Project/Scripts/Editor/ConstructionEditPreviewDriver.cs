using System.Collections.Generic;
using ProjectNeo.Walls;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// <see cref="ConstructionEditPreview"/>가 붙은 성의 런 시작 배치(성벽·성문)와 킷 꾸밈을 Play 없이 그린다(SIEGE-40 분지 배치).
    /// 성 오브젝트를 복사해 그 복사본에서만 <see cref="ConstructionWorld.BuildEditPreview"/>를 부른다. 복사본은 저장되지 않고(DontSave)
    /// 고칠 수 없으며(NotEditable), 충돌체를 꺼 물리·선택에 끼지 않는다. 성을 옮기거나 배치를 바꾸면 잠시 뒤 다시 그리고,
    /// Play에 들어가기 직전과 스크립트 다시 읽기 뒤에 지운다.
    /// </summary>
    [InitializeOnLoad]
    public static class ConstructionEditPreviewDriver
    {
        const string Prefix = "[Edit Preview] ";
        const double Interval = 0.4;
        static readonly Dictionary<ConstructionEditPreview, (GameObject clone, int signature)> previews =
            new Dictionary<ConstructionEditPreview, (GameObject, int)>();
        static double nextCheck;

        static ConstructionEditPreviewDriver()
        {
            ClearOrphans();
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) ClearAll();
            };
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += (scene, removing) => ClearAll();
        }

        [MenuItem("ProjectNeo/Preview/Redraw Construction Edit Previews")]
        public static void RedrawAll()
        {
            ClearAll();
            nextCheck = 0;
            Tick();
        }

        static void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + Interval;

            var alive = new HashSet<ConstructionEditPreview>();
            foreach (var marker in Object.FindObjectsByType<ConstructionEditPreview>(FindObjectsInactive.Exclude))
            {
                if (EditorUtility.IsPersistent(marker) || !marker.isActiveAndEnabled) continue;
                alive.Add(marker);
                int signature = Signature(marker.GetComponent<ConstructionWorld>());
                if (previews.TryGetValue(marker, out var entry) && entry.clone != null && entry.signature == signature) continue;
                if (entry.clone != null) Object.DestroyImmediate(entry.clone);
                previews[marker] = (Build(marker), signature);
            }
            foreach (var marker in new List<ConstructionEditPreview>(previews.Keys))
            {
                if (alive.Contains(marker)) continue;
                if (previews[marker].clone != null) Object.DestroyImmediate(previews[marker].clone);
                previews.Remove(marker);
            }
        }

        // 다시 그릴지 정하는 값: 성의 자리·회전·크기, 격자, 시작 배치.
        static int Signature(ConstructionWorld world)
        {
            unchecked
            {
                Transform t = world.transform;
                int hash = t.position.GetHashCode() * 31 + t.rotation.GetHashCode();
                hash = hash * 31 + t.lossyScale.GetHashCode();
                hash = hash * 31 + (world.Grid != null ? world.Grid.GetHashCode() : 0);
                foreach (InitialBuildPiece seed in world.InitialPieces)
                {
                    hash = hash * 31 + (seed.definition != null ? seed.definition.GetHashCode() : 0);
                    hash = hash * 31 + seed.cell.GetHashCode();
                    hash = hash * 31 + seed.rotation * 7 + (seed.fixedGate ? 1 : 0);
                }
                return hash;
            }
        }

        static GameObject Build(ConstructionEditPreview marker)
        {
            GameObject source = marker.gameObject;
            GameObject clone = Object.Instantiate(source, source.transform.position, source.transform.rotation);
            clone.name = Prefix + source.name;
            clone.transform.localScale = source.transform.lossyScale;
            Object.DestroyImmediate(clone.GetComponent<ConstructionEditPreview>());
            // 성 오브젝트에 함께 둔 손 배치 꾸밈(끝 탑 등)과 바닥 판은 원본에 이미 보이므로 복사본에서는 뺀다.
            foreach (Transform child in new List<Transform>(Children(clone.transform)))
                if (child.name != "Geometry") Object.DestroyImmediate(child.gameObject);
            Transform geometry = clone.transform.Find("Geometry");
            if (geometry != null)
                foreach (Transform child in new List<Transform>(Children(geometry))) Object.DestroyImmediate(child.gameObject);
            // 꾸밈 일부(흉벽 머리·바닥 판)는 씬 맨 위에 만들어지므로, 만들기 전후 루트를 비교해 새로 생긴 것을 복사본 아래로 옮긴다.
            var before = new HashSet<GameObject>(source.scene.GetRootGameObjects());
            try
            {
                clone.GetComponent<ConstructionWorld>().BuildEditPreview();
            }
            catch (System.Exception error)
            {
                Debug.LogException(error, source);
            }
            foreach (GameObject root in source.scene.GetRootGameObjects())
                if (root != clone && !before.Contains(root)) root.transform.SetParent(clone.transform, true);
            foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Transform part in clone.GetComponentsInChildren<Transform>(true))
                part.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            return clone;
        }

        static IEnumerable<Transform> Children(Transform parent)
        {
            foreach (Transform child in parent) yield return child;
        }

        static void ClearAll()
        {
            foreach (var entry in previews.Values) if (entry.clone != null) Object.DestroyImmediate(entry.clone);
            previews.Clear();
            ClearOrphans();
        }

        // 스크립트를 다시 읽으면 목록이 비므로, 남은 복사본을 이름과 표시로 찾아 지운다.
        static void ClearOrphans()
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go != null && go.transform.parent == null && !EditorUtility.IsPersistent(go)
                    && (go.hideFlags & (HideFlags.DontSave | HideFlags.NotEditable)) == (HideFlags.DontSave | HideFlags.NotEditable)
                    && (go.name.StartsWith(Prefix) || go.name.StartsWith("Battlement") || go.name == "Floor"))
                    Object.DestroyImmediate(go);
        }
    }
}
