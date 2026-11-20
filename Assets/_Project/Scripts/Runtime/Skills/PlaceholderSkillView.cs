using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Skills
{
    /// <summary>
    /// 시전 신호(<see cref="SkillCaster.Cast"/>)를 받아 효과 범위에 잠깐 원판을 띄우는 교체용 임시 외형(COMBAT-06).
    /// 적에게 거는 스킬은 붉게, 아군에게 거는 스킬은 푸르게 보인다. VFX가 들어오면 이 컴포넌트만 바꾼다.
    /// </summary>
    [RequireComponent(typeof(SkillCaster))]
    public sealed class PlaceholderSkillView : MonoBehaviour
    {
        const float Lifetime = 0.6f;
        const float MinimumRadius = 0.8f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color EnemyColor = new Color(1f, 0.35f, 0.2f, 1f);
        static readonly Color AllyColor = new Color(0.3f, 0.8f, 1f, 1f);

        readonly List<(Transform disc, float until)> discs = new List<(Transform, float)>();
        SkillCaster caster;
        MaterialPropertyBlock properties;

        public int ActiveDiscs => discs.Count;

        void Awake()
        {
            caster = GetComponent<SkillCaster>();
            properties = new MaterialPropertyBlock();
        }

        void OnEnable() => caster.Cast += OnCast;

        void OnDisable()
        {
            caster.Cast -= OnCast;
            foreach ((Transform disc, float _) in discs)
                if (disc != null) Destroy(disc.gameObject);
            discs.Clear();
        }

        void OnCast(SkillCastInfo info)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "SkillCast_" + info.Skill.Id;
            DestroyImmediate(disc.GetComponent<Collider>());
            SceneManager.MoveGameObjectToScene(disc, gameObject.scene);
            float radius = Mathf.Max(MinimumRadius, info.Skill.Radius);
            disc.transform.position = info.Center + Vector3.up * 0.05f;
            disc.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            properties.SetColor(BaseColor, HitsEnemies(info.Skill) ? EnemyColor : AllyColor);
            disc.GetComponent<Renderer>().SetPropertyBlock(properties);
            discs.Add((disc.transform, Time.time + Lifetime));
        }

        void Update()
        {
            for (int i = discs.Count - 1; i >= 0; i--)
            {
                if (discs[i].disc != null && Time.time < discs[i].until) continue;
                if (discs[i].disc != null) Destroy(discs[i].disc.gameObject);
                discs.RemoveAt(i);
            }
        }

        static bool HitsEnemies(SkillDefinition skill)
        {
            foreach (SkillEffect effect in skill.Effects)
                if (SkillEffectRules.AffectsEnemies(effect.kind)) return true;
            return false;
        }
    }
}
