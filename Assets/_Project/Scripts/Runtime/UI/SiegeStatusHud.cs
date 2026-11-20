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

        public void Configure(SiegeDirector siegeDirector, SiegeLadder[] ladderSpots, TMP_Text statusLabel)
        {
            director = siegeDirector; ladders = ladderSpots; label = statusLabel;
        }


        void Update()
        {
            switch (director.Phase)
            {
                case SiegePhase.Preparation:
                    label.text = director.IsConstructionView
                        ? "준비 - Tab: 1인칭으로 둘러보기"
                        : "준비 - Tab: 건설 화면 (거기서 전투 시작)";
                    break;
                case SiegePhase.Battle:
                    string text = "전투 - 적 " + director.EnemiesAlive + " - 아군 " + director.AlliesAlive
                        + (director.GateDestroyed ? " - 성문 파괴됨" : "")
                        + (director.RetreatOrdered ? "\n공성 장비를 잃은 적이 후퇴하고 있습니다" : "");
                    foreach (SiegeLadder ladder in ladders)
                    {
                        if (ladder == null || !ladder.PlayerCanPush) continue;
                        text += "\nF 길게 누르기: 사다리 밀기  " + Mathf.RoundToInt(ladder.State.PushProgress * 100f) + "%";
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
                            : director.EnemiesFled > 0 ? "승리! 남은 적이 후퇴했습니다." : "승리! 모든 적을 물리쳤습니다.")
                        + "\nR: 다시 시작";
                    break;
            }
        }

    }
}
