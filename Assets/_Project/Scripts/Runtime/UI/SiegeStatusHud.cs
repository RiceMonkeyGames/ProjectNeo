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
                        ? "Preparation - Tab: explore in first person"
                        : "Preparation - Tab: construction view (start battle there)";
                    break;
                case SiegePhase.Battle:
                    string text = "Battle - Enemies " + director.EnemiesAlive + " - Allies " + director.AlliesAlive
                        + (director.GateDestroyed ? " - Gate destroyed" : "")
                        + (director.RetreatOrdered ? "\nEnemies are retreating after losing their siege equipment" : "");
                    foreach (SiegeLadder ladder in ladders)
                    {
                        if (ladder == null || !ladder.PlayerCanPush) continue;
                        text += "\nHold F: Push ladder  " + Mathf.RoundToInt(ladder.State.PushProgress * 100f) + "%";
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
                    label.text = (director.Outcome != SiegeOutcome.Victory ? "Defeat"
                            : director.EnemiesFled > 0 ? "Victory! Remaining enemies retreated." : "Victory! All enemies defeated.")
                        + "\nR: Restart";
                    break;
            }
        }

    }
}
