using ProjectNeo.Combat;
using ProjectNeo.Flow;
using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>플레이어 체력을 표시하며 체력 규칙에는 관여하지 않는다.</summary>
    public sealed class PlayerHealthHud : MonoBehaviour
    {
        [SerializeField] CharacterActor player;
        [SerializeField] GamePhaseView phaseView;
        [SerializeField] GameObject content;
        [SerializeField] TMP_Text valueLabel;
        [SerializeField] RectTransform fill;
        Health health;

        public string ValueText => valueLabel != null ? valueLabel.text : string.Empty;
        public float FillRatio => fill != null ? fill.anchorMax.x : 0f;
        public bool IsShowing => content != null && content.activeSelf;

        public void Configure(CharacterActor actor, GamePhaseView view, GameObject panel,
            TMP_Text label, RectTransform barFill)
        {
            player = actor;
            phaseView = view;
            content = panel;
            valueLabel = label;
            fill = barFill;
        }

        void LateUpdate()
        {
            // 초기화 순서 및 같은 씬에서 플레이어 체력 객체가 교체되는 경우를 처리한다.
            Health next = player != null ? player.Health : null;
            if (!ReferenceEquals(health, next))
            {
                Unbind();
                health = next;
                if (health != null) health.Changed += RefreshValues;
                RefreshValues();
            }
            bool gameplay = phaseView != null && (phaseView.IsExplorationView || phaseView.IsBattleView);
            GamePhase phase = GameSession.Flow.Phase;
            if (content != null) content.SetActive(health != null && gameplay
                && (phase == GamePhase.Preparation || phase == GamePhase.Battle));
        }

        void RefreshValues()
        {
            if (health == null) return;
            if (valueLabel != null)
                valueLabel.text = $"HP {Mathf.CeilToInt(health.Current)} / {Mathf.CeilToInt(health.Maximum)}";
            if (fill != null)
                fill.anchorMax = new Vector2(health.Maximum > 0f
                    ? Mathf.Clamp01(health.Current / health.Maximum) : 0f, 1f);
        }

        void OnDisable()
        {
            Unbind();
            if (content != null) content.SetActive(false);
        }

        void Unbind()
        {
            if (health != null) health.Changed -= RefreshValues;
            health = null;
        }
    }
}
