using System.Collections.Generic;
using System.Text;
using ProjectNeo.Combat;
using ProjectNeo.Squads;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 200 vs 200 대규모 전투 검증 씬(`LargeBattleVerification.unity`) 전용 실시간 모니터 & 조작 컨트롤러.
    /// FPS, 진영별 생존 수, 현재 AI LOD(LOD 0 / 1 / 2) 및 분대 대기 슬립 분포, 프레임당 길찾기 횟수를 표시하고,
    /// 단축키로 AI LOD On/Off(L), 유닛 발밑 LOD 색상 디스크 표시(V), 전군 돌격/중지(M/H), 탑다운/1인칭 카메라 전환(Tab)을 제공한다.
    /// </summary>
    public sealed class LargeBattleDebugHud : MonoBehaviour
    {
        [SerializeField] TMP_Text statsLabel;
        [SerializeField] SquadCommandCenter commandCenter;
        [SerializeField] UnitAISettings[] aiSettings = new UnitAISettings[0];
        [SerializeField] Camera firstPersonCamera;
        [SerializeField] Camera topDownCamera;
        [SerializeField] Material lod0Material;
        [SerializeField] Material lod1Material;
        [SerializeField] Material lod2Material;
        [SerializeField] Material sleepMaterial;

        readonly Dictionary<UnitCombatAI, MeshRenderer> rings = new Dictionary<UnitCombatAI, MeshRenderer>(512);
        readonly StringBuilder sb = new StringBuilder(512);
        Transform ringRoot;
        bool showRings = true;
        bool lodEnabled = true;
        bool topDownActive;
        float smoothedDelta = 1f / 60f;
        int lastTotalQueries;
        int queriesPerFrame;
        int peakQueriesPerFrame;
        float peakResetRemaining;

        public void Configure(
            TMP_Text label,
            SquadCommandCenter center,
            UnitAISettings[] settings,
            Camera fpCamera,
            Camera tdCamera,
            Material matLod0,
            Material matLod1,
            Material matLod2,
            Material matSleep)
        {
            statsLabel = label;
            commandCenter = center;
            aiSettings = settings ?? new UnitAISettings[0];
            firstPersonCamera = fpCamera;
            topDownCamera = tdCamera;
            lod0Material = matLod0;
            lod1Material = matLod1;
            lod2Material = matLod2;
            sleepMaterial = matSleep;
        }

        void Awake()
        {
            ringRoot = new GameObject("LodRings").transform;
            ringRoot.SetParent(transform, false);
            if (topDownCamera != null) topDownCamera.gameObject.SetActive(false);
        }

        void Start()
        {
            // 씬을 Play하자마자 200 vs 200 전면 교전을 바로 볼 수 있도록 전군 총공격(Engage)으로 시작한다.
            IssueAll(UnitDirective.Engage);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.0001f) smoothedDelta = Mathf.Lerp(smoothedDelta, dt, 0.1f);

            int totalNow = UnitCombatAI.Scheduler.TotalQueries;
            queriesPerFrame = Mathf.Max(0, totalNow - lastTotalQueries);
            lastTotalQueries = totalNow;
            if (queriesPerFrame > peakQueriesPerFrame) peakQueriesPerFrame = queriesPerFrame;
            peakResetRemaining -= dt;
            if (peakResetRemaining <= 0f)
            {
                peakResetRemaining = 2f;
                peakQueriesPerFrame = queriesPerFrame;
            }

            HandleHotkeys();
            UpdateUnitsAndHud();
        }

        void HandleHotkeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.lKey.wasPressedThisFrame)
            {
                lodEnabled = !lodEnabled;
                ApplyLodFlag(lodEnabled);
            }
            if (kb.vKey.wasPressedThisFrame)
            {
                showRings = !showRings;
                ringRoot.gameObject.SetActive(showRings);
            }
            if (kb.tabKey.wasPressedThisFrame && firstPersonCamera != null && topDownCamera != null)
            {
                topDownActive = !topDownActive;
                firstPersonCamera.gameObject.SetActive(!topDownActive);
                topDownCamera.gameObject.SetActive(topDownActive);
            }
            if (kb.mKey.wasPressedThisFrame) IssueAll(UnitDirective.Engage);
            if (kb.hKey.wasPressedThisFrame) IssueAll(UnitDirective.Hold);
        }

        void ApplyLodFlag(bool enabled)
        {
            // 런타임에 UnitAISettings.EnableAiLod과 동등하게 강제 적용한다.
            if (!enabled)
                AILodContext.SetViewOverride(Vector3.zero, Vector3.forward, false);
            else
                AILodContext.ClearViewOverride();
        }

        void IssueAll(UnitDirective directive)
        {
            if (commandCenter != null && commandCenter.Commander != null)
            {
                commandCenter.Commander.SelectAll();
                commandCenter.Commander.Issue(new SquadOrder(directive == UnitDirective.Engage ? SquadOrderKind.Attack : SquadOrderKind.Hold));
            }
            IReadOnlyList<CharacterActor> actors = CharacterActor.ActiveActors;
            for (int i = 0; i < actors.Count; i++)
            {
                CharacterActor actor = actors[i];
                if (actor == null || actor.Team != CombatTeam.Hostile) continue;
                UnitCombatAI ai = actor.GetComponent<UnitCombatAI>();
                if (ai != null) ai.SetDirective(directive);
            }
        }

        void UpdateUnitsAndHud()
        {
            int allies = 0, enemies = 0;
            int lod0 = 0, lod1 = 0, lod2 = 0, sleeping = 0, engaged = 0;

            IReadOnlyList<CharacterActor> actors = CharacterActor.ActiveActors;
            for (int i = 0; i < actors.Count; i++)
            {
                CharacterActor actor = actors[i];
                if (actor == null || actor.Health == null || actor.Health.IsDead) continue;
                if (actor.Team == CombatTeam.Allied) allies++;
                else enemies++;

                UnitCombatAI ai = actor.GetComponent<UnitCombatAI>();
                if (ai == null) continue;
                if (ai.Target != null) engaged++;
                if (ai.IsSquadNeighborhoodClear) sleeping++;

                AILodTier tier = lodEnabled ? ai.LodTier : AILodTier.Lod0;
                switch (tier)
                {
                    case AILodTier.Lod0: lod0++; break;
                    case AILodTier.Lod1: lod1++; break;
                    default: lod2++; break;
                }

                if (showRings) UpdateRing(ai, tier, ai.IsSquadNeighborhoodClear);
            }

            if (statsLabel == null) return;
            float fps = 1f / Mathf.Max(0.0001f, smoothedDelta);
            float frameMs = smoothedDelta * 1000f;
            sb.Clear();
            sb.AppendFormat("<b>[200 vs 200 AI LOD 검증 씬]</b>  FPS: {0:F1} ({1:F2} ms)\n", fps, frameMs);
            sb.AppendFormat("생존 유닛: 아군 <b>{0}</b> vs 적군 <b>{1}</b> (교전 중: {2})\n", allies, enemies, engaged);
            sb.AppendFormat("AI LOD ({0}): <color=#33FF55>LOD0(근경): {1}</color> | <color=#FFCC22>LOD1(중경): {2}</color> | <color=#99AABB>LOD2(원경): {3}</color> | <color=#44CCFF>분대슬립: {4}</color>\n",
                lodEnabled ? "ON" : "OFF", lod0, lod1, lod2, sleeping);
            sb.AppendFormat("CalculatePath: 이번 프레임 <b>{0}</b>회 (최근 2초 피크: {1}회 / 누적: {2}회)\n",
                queriesPerFrame, peakQueriesPerFrame, lastTotalQueries);
            sb.Append("단축키: <b>[M]</b> 전군 총공격  <b>[H]</b> 전군 제자리 대기  <b>[Tab]</b> 1인칭/탑다운 뷰 전환  <b>[V]</b> 발밑 LOD 링 표시  <b>[L]</b> 카메라 오버라이드 토글");
            statsLabel.text = sb.ToString();
        }

        void UpdateRing(UnitCombatAI ai, AILodTier tier, bool isSleeping)
        {
            if (!rings.TryGetValue(ai, out MeshRenderer renderer) || renderer == null)
            {
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "LodRing_" + ai.name;
                Destroy(disc.GetComponent<Collider>());
                disc.transform.SetParent(ringRoot, false);
                disc.transform.localScale = new Vector3(1.1f, 0.02f, 1.1f);
                renderer = disc.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rings[ai] = renderer;
            }
            if (!ai.isActiveAndEnabled || ai.Actor.Health.IsDead)
            {
                if (renderer.gameObject.activeSelf) renderer.gameObject.SetActive(false);
                return;
            }
            if (!renderer.gameObject.activeSelf) renderer.gameObject.SetActive(true);
            renderer.transform.position = ai.transform.position + Vector3.up * 0.05f;
            Material targetMat = isSleeping && sleepMaterial != null ? sleepMaterial
                : tier == AILodTier.Lod0 ? lod0Material
                : tier == AILodTier.Lod1 ? lod1Material
                : lod2Material;
            if (targetMat != null && renderer.sharedMaterial != targetMat)
                renderer.sharedMaterial = targetMat;
        }
    }
}
