using ProjectNeo.Siege;
using TMPro;
using UnityEngine;

namespace ProjectNeo.UI
{
    /// <summary>수성전 프로토타입의 단계·남은 인원·결과 표시. 두 시점 모두에서 보인다. 시험용이다.</summary>
    public sealed class SiegeStatusHud : MonoBehaviour
    {
        [SerializeField] SiegeDirector director;
        [SerializeField] SiegeLadder[] ladders = new SiegeLadder[0];
        [SerializeField] TMP_Text label;
        TMP_FontAsset runtimeFont;

        public void Configure(SiegeDirector siegeDirector, SiegeLadder[] ladderSpots, TMP_Text statusLabel)
        {
            director = siegeDirector; ladders = ladderSpots; label = statusLabel;
        }

        void Awake()
        {
            runtimeFont = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular");
            if (runtimeFont == null) return;
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) text.font = runtimeFont;
        }

        void Update()
        {
            if (runtimeFont == null) return;
            switch (director.Phase)
            {
                case SiegePhase.Preparation:
                    label.text = director.IsConstructionView
                        ? "전투 준비 · Tab 1인칭으로 둘러보기"
                        : "전투 준비 · Tab 건설 화면 (전투 시작은 건설 화면에서)";
                    break;
                case SiegePhase.Battle:
                    string text = "전투 중 · 남은 적 " + director.EnemiesAlive + " · 남은 부대원 " + director.AlliesAlive
                        + (director.GateDestroyed ? " · 성문 파괴됨" : "")
                        + (director.RetreatOrdered ? "\n공성 수단을 잃은 적이 물러납니다" : "");
                    foreach (SiegeLadder ladder in ladders)
                    {
                        if (ladder == null || !ladder.PlayerCanPush) continue;
                        text += "\nF 누르고 있기: 사다리 밀기  " + Mathf.RoundToInt(ladder.State.PushProgress * 100f) + "%";
                        break;
                    }
                    foreach (var actor in ProjectNeo.Combat.CharacterActor.ActiveActors)
                    {
                        if (actor.gameObject.scene != gameObject.scene || actor.GetComponent<ProjectNeo.Characters.PlayerInputController>() == null) continue;
                        var traveller = actor.GetComponent<LadderTraveller>();
                        if (traveller != null && traveller.PlayerHint.Length > 0) text += "\n" + traveller.PlayerHint;
                        break;
                    }
                    label.text = text;
                    break;
                default:
                    label.text = (director.Outcome != SiegeOutcome.Victory ? "패배"
                            : director.EnemiesFled > 0 ? "승리! 남은 적이 물러났습니다." : "승리! 적을 모두 물리쳤습니다.")
                        + "\nR 다시 시작";
                    break;
            }
        }

        void OnDestroy()
        {
            if (runtimeFont == null) return;
            foreach (Texture2D texture in runtimeFont.atlasTextures) if (texture != null) Destroy(texture);
            if (runtimeFont.material != null) Destroy(runtimeFont.material);
            Destroy(runtimeFont);
        }
    }
}
