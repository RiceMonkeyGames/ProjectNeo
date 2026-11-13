using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Core;
using ProjectNeo.Flow;
using ProjectNeo.Morale;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>B-8.2: 실제 Game 씬의 아군 부대 사기 연결(Q-44).</summary>
    public class GameMoralePlayTests
    {
        // 한계 판정을 언제나 붕괴(공포)로 만든다. 각성(사기 상승·굳건함)이 도망을 막지 않게 한다.
        sealed class CollapseRandom : IRandomSource
        {
            public int NextInt(int maxExclusive) => 0;
            public float NextFloat() => 0.99f;
        }

        static int cleanup;
        GameBattleController battle;
        ConstructionWorld world;
        SquadCommandCenter center;
        GameSquadMorale morale;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
            world = Object.FindAnyObjectByType<ConstructionWorld>();
            center = Object.FindAnyObjectByType<SquadCommandCenter>();
            morale = battle.Morale;
            Assert.IsNotNull(morale, "Battle.asset에 사기 설정이 연결되어 있어야 한다.");
            morale.UseRandom(new CollapseRandom());
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameMoraleCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        static void Kill(CharacterActor actor) => actor.ReceiveDamage(actor.Health.Current + 1f);

        List<UnitCombatAI> AliveMembers(int squad) => center.Squads[squad].Members
            .Where(member => member != null && member.isActiveAndEnabled && !member.Actor.Health.IsDead).ToList();

        float[] Values() => Enumerable.Range(0, center.Squads.Count)
            .Select(i => morale.BattleOf(i) != null ? morale.BattleOf(i).Morale.Value : -1f).ToArray();

        bool Near(int squad, Vector3 point) => AliveMembers(squad)
            .Any(member => (member.transform.position - point).sqrMagnitude <= morale.Settings.NearbyRadius * morale.Settings.NearbyRadius);

        [UnityTest]
        public IEnumerator 부대원_사망_근처_성벽_붕괴_성문_파괴가_아군_부대_사기를_깎는다()
        {
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            BattleSquadMorale first = morale.BattleOf(0);
            Assert.IsNotNull(first);
            Assert.AreEqual(150f, first.Morale.Value, 0.01f, "새 부대는 150에서 시작한다.");
            int members = first.StartingMembers;
            Assert.AreEqual(AliveMembers(0).Count, members);

            Kill(AliveMembers(0)[0].Actor);
            float expected = 150f - morale.Settings.FullSquadLoss / members;
            Assert.AreEqual(expected, first.Morale.Value, 0.01f, "한 명이 죽으면 시작 인원 비율만큼 준다.");

            // 성벽 한 조각 옆으로 1부대를 옮겨 무너뜨린다. 함께 부서지는 조각이 있어도 한 번만 깎는다.
            PlacedStructure wall = world.Structures.Where(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall)
                .OrderBy(piece => (piece.transform.position - AliveMembers(0)[0].transform.position).sqrMagnitude).First();
            Assert.IsTrue(NavMesh.SamplePosition(wall.transform.position, out NavMeshHit beside, 8f, NavMesh.AllAreas));
            foreach (UnitCombatAI member in AliveMembers(0)) member.GetComponent<NavMeshAgent>().Warp(beside.position);
            float far = morale.Settings.NearbyRadius + 10f;
            var distant = Enumerable.Range(1, center.Squads.Count - 1).Where(i => morale.BattleOf(i) != null
                && AliveMembers(i).All(member => (member.transform.position - wall.transform.position).magnitude > far)).ToList();
            float[] beforeWall = Values();
            world.Collapse(wall);
            Assert.AreEqual(beforeWall[0] - morale.Settings.NearbyWallCollapseLoss, first.Morale.Value, 0.01f);
            foreach (int i in distant) Assert.AreEqual(beforeWall[i], morale.BattleOf(i).Morale.Value, 0.01f, "먼 부대는 그대로다.");
            yield return null;

            float[] beforeGate = Values();
            PlacedStructure gate = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            world.Collapse(gate);
            for (int i = 0; i < beforeGate.Length; i++)
                if (beforeGate[i] >= 0f)
                    Assert.LessOrEqual(morale.BattleOf(i).Morale.Value, beforeGate[i] - morale.Settings.GateDestroyedLoss + 0.01f,
                        "메인 성문이 부서지면 모든 부대가 깎인다.");
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase, "아군이 살아 있으면 성문이 부서져도 전투는 계속된다.");
        }

        [UnityTest]
        public IEnumerator 사기_0_부대는_지시를_무시하고_퇴각_지점에서_빠지며_결과에_도망으로_남고_다음_준비에_돌아온다()
        {
            Assert.IsNotNull(GameSession.Flow.Run?.State, "Game 씬이 런 데이터와 이어져 있어야 한다.");
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            List<UnitCombatAI> members = AliveMembers(0);
            Assert.GreaterOrEqual(members.Count, 2);
            BattleSquadMorale routed = morale.BattleOf(0);
            int alliesBefore = battle.AlliesAlive;

            routed.Morale.Add(-1000f);
            Assert.IsTrue(routed.IsRouted);
            Vector3 retreat = morale.RetreatPoint;
            Vector3 castle = Vector3.zero;
            foreach (PlacedStructure piece in world.Structures) castle += piece.transform.position;
            castle /= world.Structures.Count;
            Assert.Less(retreat.z, castle.z, "퇴각 지점은 적이 오는 북쪽의 반대편이다.");
            foreach (UnitCombatAI member in members)
            {
                Assert.IsTrue(morale.IsRouting(member));
                Assert.IsTrue(center.IsDetached(member));
                Assert.AreEqual(UnitDirective.Move, member.Directive);
            }

            center.Commander.ClearSelection();
            center.Commander.Select(0);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Attack));
            foreach (UnitCombatAI member in members)
            {
                Assert.AreEqual(UnitDirective.Move, member.Directive, "도망치는 부대는 지시를 받지 않는다.");
                Assert.Less(Vector3.Distance(member.MoveDestination, retreat), 0.01f);
            }
            Assert.AreEqual(alliesBefore, battle.AlliesAlive, "달리는 동안은 아직 전투에 남아 있다.");

            UnitCombatAI runner = members[members.Count - 1];
            List<UnitCombatAI> arrived = members.Take(members.Count - 1).ToList();
            foreach (UnitCombatAI member in arrived) member.GetComponent<NavMeshAgent>().Warp(retreat);
            yield return null; yield return null;
            foreach (UnitCombatAI member in arrived)
            {
                Assert.IsFalse(member.gameObject.activeSelf, "퇴각 지점에 닿으면 숨겨 전투에서 뺀다.");
                Assert.IsTrue(morale.HasExited(member));
            }
            Assert.AreEqual(alliesBefore - arrived.Count, battle.AlliesAlive, "빠진 아군은 살아 있는 아군 수에서 빠진다.");
            Assert.IsTrue(runner.gameObject.activeSelf);

            foreach (var enemy in battle.Enemies) Kill(enemy.Actor);
            for (int i = 0; i < 10 && GameSession.Flow.Phase == GamePhase.Battle; i++) yield return null;
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
            RunState state = GameSession.Flow.Run.State;
            BattleResult result = state.LastBattleResult;
            Assert.IsNotNull(result);
            foreach (UnitCombatAI member in members)
                Assert.AreEqual(UnitFate.Fled, result.FateOf(battle.RunLink.IdOf(member.Actor)),
                    "빠진 유닛과 전투가 끝날 때 달리던 유닛 모두 도망으로 남는다.");
            Assert.AreEqual(80f, state.Army.Squads[0].Morale.Value, 0.01f, "도망친 부대는 전투 후 80이 된다.");
            Assert.AreEqual(members.Count, state.Army.Squads[0].Members.Count, "도망친 유닛은 런 부대에 남는다.");

            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return null;
            foreach (UnitCombatAI member in arrived)
            {
                Assert.IsTrue(member.gameObject.activeSelf, "다음 준비에 다시 나타난다.");
                Vector3 offset = member.transform.position - retreat; offset.y = 0f;
                Assert.Less(offset.magnitude, 3f, "퇴각 지점에서 다시 나타난다.");
            }
            for (int i = 0; i < 300 && members.Any(member => center.IsDetached(member)); i++) yield return null;
            foreach (UnitCombatAI member in members)
            {
                Assert.IsFalse(center.IsDetached(member), "부대 지시를 다시 받는다.");
                Assert.IsTrue(member.isActiveAndEnabled);
                Assert.AreEqual(member.Actor.Health.Maximum, member.Actor.Health.Current, 0.01f);
            }

            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            BattleSquadMorale next = morale.BattleOf(0);
            Assert.AreEqual(80f, next.Morale.Value, 0.01f, "사기는 런 부대에 이어진다.");
            Assert.IsTrue(next.StartedShaken, "100 미만으로 시작하면 약한 붕괴를 안고 시작한다.");
            Assert.AreEqual(members.Count, next.StartingMembers, "돌아온 유닛도 이번 전투 시작 인원에 든다.");
        }

        [UnityTest]
        public IEnumerator 모든_부대가_빠져도_성문이_있고_플레이어가_살아_있으면_전투는_계속된다()
        {
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            for (int i = 0; i < center.Squads.Count; i++)
                if (morale.BattleOf(i) != null) morale.BattleOf(i).Morale.Add(-1000f);
            Vector3 retreat = morale.RetreatPoint;
            for (int i = 0; i < center.Squads.Count; i++)
                foreach (UnitCombatAI member in AliveMembers(i))
                {
                    Assert.IsTrue(morale.IsRouting(member));
                    member.GetComponent<NavMeshAgent>().Warp(retreat);
                }
            yield return null; yield return null;
            Assert.AreEqual(0, battle.AlliesAlive);
            Assert.IsFalse(battle.GateDestroyed);
            yield return null;
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase, "플레이어 혼자 싸운다.");
        }
    }
}
