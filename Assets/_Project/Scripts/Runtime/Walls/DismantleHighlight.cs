using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProjectNeo.Walls
{
    /// <summary>
    /// 해체 도구가 켜진 동안 마우스가 가리키는 조각(또는 그 위 무기)을 지을 수 없는 자리의 배치 미리보기처럼 보여 준다
    /// (UI-13, 사용자 2026-11-24). 같은 프리팹으로 만든 반투명 미리보기를 같은 자리·방향에 놓고
    /// <see cref="PlacementViewSettings.InvalidColor"/>로 칠한다(<see cref="PlacementController"/>의 미리보기와 같은 재질).
    /// 그동안 대상의 렌더러와, 성 킷(<see cref="CastleKitDresser"/>)이 그 칸에 그린 모델을 잠깐 끈다. 킷 모델은 칸의 모든 층을
    /// 한 번에 그리므로, 같은 칸의 다른 층은 임시 외형(Visual)을 잠깐 켜서 대신 보인다.
    /// 대상이 바뀌거나 꺼지면 미리보기를 없애고 모든 렌더러를 되돌린다. 규칙에는 관여하지 않는다.
    /// </summary>
    public sealed class DismantleHighlight
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        readonly List<Renderer> hidden = new List<Renderer>();
        readonly List<Renderer> shown = new List<Renderer>();
        MaterialPropertyBlock block;
        GameObject target;
        GameObject ghost;

        public GameObject Target => target;
        public GameObject Ghost => ghost;

        /// <summary>
        /// <paramref name="next"/>를 붉은 미리보기로 바꿔 보인다. 같은 대상이면 아무 일도 하지 않는다. null이면 지운다.
        /// <paramref name="kitCell"/>은 숨길 킷 칸(없으면 null), <paramref name="standIns"/>는 그동안 켜 둘 같은 칸 다른 층의 외형이다.
        /// </summary>
        public void Show(GameObject next, GameObject prefab, PlacementViewSettings view, GameObject kitCell = null,
            IEnumerable<Renderer> standIns = null)
        {
            if (next == target) return;
            Clear();
            if (next == null || prefab == null || view == null || view.GhostMaterial == null) return;
            target = next;
            ghost = Object.Instantiate(prefab, next.transform.parent);
            ghost.name = "DismantlePreview";
            ghost.transform.SetLocalPositionAndRotation(next.transform.localPosition, next.transform.localRotation);
            ghost.transform.localScale = next.transform.localScale;
            foreach (Collider collider in ghost.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (MonoBehaviour behaviour in ghost.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            block ??= new MaterialPropertyBlock();
            block.Clear();
            block.SetColor(BaseColor, view.InvalidColor);
            foreach (Renderer renderer in ghost.GetComponentsInChildren<Renderer>())
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = view.GhostMaterial;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.SetPropertyBlock(block);
            }
            Hide(next);
            if (kitCell != null) Hide(kitCell);
            if (standIns != null)
                foreach (Renderer renderer in standIns)
                {
                    if (renderer == null || renderer.enabled) continue;
                    renderer.enabled = true;
                    shown.Add(renderer);
                }
        }

        void Hide(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer.GetComponent<TMP_SubMesh>() != null || renderer.GetComponent<TextMeshPro>() != null) continue;
                renderer.enabled = false;
                hidden.Add(renderer);
            }
        }

        public void Clear()
        {
            foreach (Renderer renderer in hidden)
                if (renderer != null) renderer.enabled = true;
            foreach (Renderer renderer in shown)
                if (renderer != null) renderer.enabled = false;
            hidden.Clear();
            shown.Clear();
            if (ghost != null) Object.Destroy(ghost);
            ghost = null;
            target = null;
        }
    }
}
