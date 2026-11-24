using System.Linq;
using NUnit.Framework;
using ProjectNeo.Flow;
using UnityEngine;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// UI-13: the tavern hires every hero as a reserve hero, so tests that need a captain make the last hired reserve hero a
    /// captain through the organization window (O), the same path the player uses.
    /// </summary>
    static class TavernCaptainHelper
    {
        /// <summary>Makes the most recently hired reserve hero the captain of <paramref name="squad"/>. Call with no popup open.</summary>
        public static void MakeLastReserveCaptain(int squad)
        {
            var state = GameSession.Flow.Run.State;
            Assert.IsNotEmpty(state.ReserveHeroes, "hire a hero first");
            var organization = Object.FindAnyObjectByType<GameSquadOrganization>();
            Assert.IsTrue(organization.Open(), "the organization window opens in preparation exploration");
            Assert.IsTrue(organization.SelectSquad(squad));
            Assert.IsTrue(organization.Assign(state.ReserveHeroes.Last()));
            organization.Close();
        }
    }
}
