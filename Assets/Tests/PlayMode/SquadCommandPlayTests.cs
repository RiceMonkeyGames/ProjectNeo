using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Squads;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    public class SquadCommandPlayTests
    {
        Scene scene;
        SquadCommandCenter center;
        SquadCommandInput input;
        List<CharacterActor> enemies;
        float previousCapture;
        CursorLockMode previousLock;
        bool previousVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime;
            previousLock = Cursor.lockState; previousVisible = Cursor.visible;
            Time.captureDeltaTime = 1f / 30f;
            yield return SceneManager.LoadSceneAsync("SquadCommandPrototype", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            var roots = scene.GetRootGameObjects();
            center = roots.Select(root => root.GetComponent<SquadCommandCenter>()).Single(value => value != null);
            input = center.GetComponent<SquadCommandInput>();
            yield return null;
            yield return null;
            enemies = CharacterActor.ActiveActors
                .Where(actor => actor.gameObject.scene == scene && actor.Team == CombatTeam.Hostile).ToList();
            Assert.IsNotEmpty(enemies);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(scene);
            Time.captureDeltaTime = previousCapture;
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
        }

        IReadOnlyList<UnitCombatAI> Members(int squad) => center.Squads[squad].Members;
        Vector3[] Positions(int squad) => Members(squad).Select(member => member.transform.position).ToArray();

        void AssertStayed(int squad, Vector3[] before, string message)
        {
            Vector3[] now = Positions(squad);
            for (int i = 0; i < now.Length; i++)
                Assert.That(Vector3.Distance(before[i], now[i]), Is.LessThan(0.1f), message);
        }

        // 적이 스스로 움직이지 않게 해서 지시별 행동만 확인한다.
        void FreezeEnemies()
        {
            foreach (CharacterActor enemy in enemies) enemy.GetComponent<UnitCombatAI>().enabled = false;
        }

        IEnumerator WaitForHold(int squad)
        {
            int frames = 0;
            while (center.Commander.OrderOf(squad).Kind != SquadOrderKind.Hold && frames++ < 900) yield return null;
            Assert.AreEqual(SquadOrderKind.Hold, center.Commander.OrderOf(squad).Kind, "이동이 끝나지 않았다.");
        }

        [UnityTest]
        public IEnumerator SquadsHoldByDefault_AndMoveOrderMovesOnlyTheSelectedSquad_ThenHolds()
        {
            Assert.AreEqual(3, center.Commander.SquadCount);
            for (int squad = 0; squad < 3; squad++)
                Assert.IsTrue(Members(squad).All(member => member.Directive == UnitDirective.Hold));
            // 적은 아군이 가까이 오기 전까지 제자리에 있어야 지시를 차례로 시험할 수 있다.
            Vector3[] enemyStart = enemies.Select(enemy => enemy.transform.position).ToArray();
            for (int i = 0; i < 90; i++) yield return null;
            for (int i = 0; i < enemies.Count; i++)
                Assert.That(Vector3.Distance(enemyStart[i], enemies[i].transform.position), Is.LessThan(0.1f),
                    "적이 시작하자마자 움직였다. 씬의 적 시야 설정을 확인한다.");
            Vector3[] others = Positions(1);
            var destination = new Vector3(-10f, 0f, -2f);
            Assert.IsTrue(center.Commander.Select(0));
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, destination));
            Assert.IsTrue(Members(0).All(member => member.Directive == UnitDirective.Move));
            yield return WaitForHold(0);
            Vector3 centroid = Vector3.zero;
            foreach (Vector3 position in Positions(0)) centroid += position;
            centroid /= Members(0).Count;
            centroid.y = 0f;
            Assert.That(Vector3.Distance(destination, centroid), Is.LessThan(1f));
            Assert.IsTrue(Members(0).All(member => member.Directive == UnitDirective.Hold));
            Vector3[] arrived = Positions(0);
            for (int i = 0; i < arrived.Length; i++)
                for (int j = i + 1; j < arrived.Length; j++)
                    Assert.Greater(Vector3.Distance(arrived[i], arrived[j]), 0.7f, "대형 자리가 겹치면 안 된다.");
            AssertStayed(1, others, "선택하지 않은 부대가 움직였다.");
        }

        [UnityTest]
        public IEnumerator HoldingArchers_ChaseEnemyJustBeyondRange_ThenReturn()
        {
            FreezeEnemies();
            CharacterActor enemy = enemies.First(actor => actor.Stats.GetValue(ProjectNeo.Stats.StatType.AttackRange) < 3f);
            var enemyAgent = enemy.GetComponent<NavMeshAgent>();
            UnitCombatAI archer = Members(2)[0];
            float range = archer.Actor.Stats.GetValue(ProjectNeo.Stats.StatType.AttackRange);
            Assert.That(archer.HoldChaseRadius, Is.GreaterThan(range), "원거리 중지의 추격 범위는 사거리보다 넓어야 한다.");
            Vector3[] held = Positions(2);
            Vector3 middle = Vector3.zero;
            foreach (Vector3 position in held) middle += position;
            middle /= held.Length;

            // 사거리보다 3m 먼 적: 사거리가 닿을 때까지 다가가 쏜다.
            Assert.IsTrue(enemyAgent.Warp(middle + Vector3.forward * (range + 3f)));
            int frames = 0;
            while (enemy.Health.Current >= enemy.Health.Maximum && frames++ < 450) yield return null;
            Assert.Less(enemy.Health.Current, enemy.Health.Maximum, "원거리 부대가 사거리 밖의 가까운 적에게 다가가 쏘지 않았다.");
            Assert.IsTrue(Members(2).Any(member => Vector3.Distance(member.transform.position, member.HoldPost) > 1f),
                "원거리 부대가 자리에서 움직이지 않았다.");

            // 추격 범위 밖의 적: 쫓지 않고 자리로 돌아온다.
            Assert.IsTrue(enemyAgent.Warp(middle + Vector3.forward * (archer.HoldChaseRadius + 10f)));
            for (int i = 0; i < 20; i++) yield return null;
            float health = enemy.Health.Current;
            frames = 0;
            while (!Members(2).All(member => Vector3.Distance(member.transform.position, member.HoldPost) < 0.6f)
                && frames++ < 450) yield return null;
            Assert.AreEqual(health, enemy.Health.Current, "추격 범위 밖의 적을 공격했다.");
            for (int i = 0; i < held.Length; i++)
                Assert.That(Vector3.Distance(held[i], Positions(2)[i]), Is.LessThan(0.6f), "자리로 돌아오지 않았다.");
        }

        [UnityTest]
        public IEnumerator MovingSquadIgnoresEnemies_HoldingSquadEngagesNearPost_ThenReturns()
        {
            FreezeEnemies();
            // 원거리 부대의 사격이 결과에 섞이지 않게 뺀다.
            foreach (UnitCombatAI archer in Members(2)) archer.gameObject.SetActive(false);
            CharacterActor enemy = enemies.First(actor => actor.Stats.GetValue(ProjectNeo.Stats.StatType.AttackRange) < 3f);
            var enemyAgent = enemy.GetComponent<NavMeshAgent>();
            UnitCombatAI front = Members(0)[2];
            center.Commander.Select(0);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, new Vector3(-10f, 0f, 4f)));
            Assert.IsTrue(enemyAgent.Warp(front.transform.position + Vector3.forward * 1.2f));
            yield return WaitForHold(0);
            Assert.AreEqual(enemy.Health.Maximum, enemy.Health.Current, "이동 중에는 적을 공격하지 않는다.");

            Vector3[] held = Positions(0);
            const float radius = 6f;
            // 자리에서 반경(6m) 안이지만 사거리(1.5m) 밖의 적: 다가가 싸운다.
            Assert.IsTrue(enemyAgent.Warp(front.transform.position + Vector3.forward * 4f));
            int frames = 0;
            while (enemy.Health.Current >= enemy.Health.Maximum && frames++ < 300) yield return null;
            Assert.Less(enemy.Health.Current, enemy.Health.Maximum, "중지한 부대는 자리 근처의 적에게 다가가 싸운다.");
            for (int i = 0; i < held.Length; i++)
                Assert.LessOrEqual(Vector3.Distance(held[i], Positions(0)[i]), radius + 1f, "반경보다 멀리 쫓아갔다.");

            // 반경 밖의 적: 쫓지 않고 자리로 돌아온다.
            Assert.IsTrue(enemyAgent.Warp(front.transform.position + Vector3.forward * 14f));
            for (int i = 0; i < 20; i++) yield return null;
            float health = enemy.Health.Current;
            frames = 0;
            while (!Members(0).All(member => Vector3.Distance(member.transform.position, member.HoldPost) < 0.6f)
                && frames++ < 300) yield return null;
            Assert.AreEqual(health, enemy.Health.Current, "반경 밖의 적을 공격했다.");
            for (int i = 0; i < held.Length; i++)
                Assert.That(Vector3.Distance(held[i], Positions(0)[i]), Is.LessThan(0.6f), "싸운 뒤 자리로 돌아오지 않았다.");
            Assert.AreEqual(SquadOrderKind.Hold, center.Commander.OrderOf(0).Kind);
        }

        Vector3 Centroid(int squad)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 position in Positions(squad)) sum += position;
            sum /= Members(squad).Count;
            sum.y = 0f;
            return sum;
        }

        IEnumerator WaitForCentroid(int squad, Vector3 destination, string message)
        {
            int frames = 0;
            while (Vector3.Distance(destination, Centroid(squad)) > 1.5f && frames++ < 1200) yield return null;
            Assert.That(Vector3.Distance(destination, Centroid(squad)), Is.LessThanOrEqualTo(1.5f), message);
        }

        [UnityTest]
        public IEnumerator AttackOrder_AdvancesToThePoint_FightsEnemiesSeenOnTheWay_ThenWaitsThere()
        {
            FreezeEnemies();
            Vector3[] others = Positions(0);
            Vector3[] start = Positions(1);
            center.Commander.Select(1);

            // 지점 없는 공격: 시야(15m) 안에 적이 없으므로 제자리에서 기다린다.
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Attack));
            for (int i = 0; i < 45; i++) yield return null;
            AssertStayed(1, start, "시야 밖의 적을 찾아 나서면 안 된다.");

            // 적이 보이지 않는 지점으로의 공격: 그곳까지 가서 공격 지시를 유지한 채 기다린다.
            var quiet = new Vector3(12f, 0f, -2f);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Attack, quiet));
            yield return WaitForCentroid(1, quiet, "공격 지시의 지점까지 가지 않았다.");
            Assert.AreEqual(SquadOrderKind.Attack, center.Commander.OrderOf(1).Kind);
            Assert.IsTrue(enemies.All(enemy => enemy.Health.Current >= enemy.Health.Maximum));

            // 적 근처를 지나는 지점으로의 공격: 가는 길에 발견한 적과 싸운다.
            var beyond = new Vector3(0f, 0f, 18f);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Attack, beyond));
            int frames = 0;
            while (enemies.All(enemy => enemy.Health.Current >= enemy.Health.Maximum) && frames++ < 1500) yield return null;
            Assert.IsTrue(enemies.Any(enemy => enemy.Health.Current < enemy.Health.Maximum), "발견한 적과 싸우지 않았다.");
            Assert.IsTrue(Members(1).Any(member => member.Target != null));
            AssertStayed(0, others, "지시받지 않은 부대가 움직였다.");

            // 적이 사라지면 다시 지점으로 가서 기다린다.
            foreach (CharacterActor enemy in enemies) enemy.gameObject.SetActive(false);
            yield return WaitForCentroid(1, beyond, "적을 처리한 뒤 지점으로 돌아가지 않았다.");
            for (int i = 0; i < 30; i++) yield return null;
            Vector3[] waiting = Positions(1);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(SquadOrderKind.Attack, center.Commander.OrderOf(1).Kind, "적이 없어도 공격 지시는 유지한다.");
            Assert.IsTrue(Members(1).All(member => member.Target == null));
            AssertStayed(1, waiting, "도착한 뒤에는 그 자리에서 기다린다.");
        }

        [UnityTest]
        public IEnumerator Markers_ShowSelectedSquadMembers_AndTheOrderedDestinationUntilArrival()
        {
            var markers = center.GetComponentInChildren<PlaceholderSquadMarkers>();
            Assert.IsNotNull(markers);
            yield return null;
            Assert.IsFalse(Members(0).Any(markers.IsMarked));
            center.Commander.Select(0);
            yield return null;
            Assert.IsTrue(Members(0).All(markers.IsMarked));
            Assert.IsFalse(Members(1).Any(markers.IsMarked));
            Assert.IsFalse(markers.TryGetDestinationMarker(0, out _));

            var destination = new Vector3(-10f, 0f, -4f);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, destination));
            center.Commander.Select(1);
            yield return null;
            Assert.IsTrue(markers.TryGetDestinationMarker(0, out Vector3 shown), "선택을 바꿔도 이동 중인 목적지는 보인다.");
            Assert.That(Vector3.Distance(destination, new Vector3(shown.x, 0f, shown.z)), Is.LessThan(0.01f));
            Assert.IsFalse(Members(0).Any(markers.IsMarked));
            Assert.IsTrue(Members(1).All(markers.IsMarked));
            yield return WaitForHold(0);
            yield return null;
            Assert.IsFalse(markers.TryGetDestinationMarker(0, out _), "도착하면 목적지 표시를 지운다.");

            Members(1)[0].Actor.ReceiveDamage(100000f);
            yield return null;
            Assert.IsFalse(markers.IsMarked(Members(1)[0]), "죽은 유닛에는 선택 표시를 하지 않는다.");
            Assert.IsTrue(markers.IsMarked(Members(1)[1]));
        }

        [UnityTest]
        public IEnumerator Input_OrdersMoveToTheScreenCenter_AndRejectsMissingSelectionOrSquad()
        {
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(input.OrderMove());
            Assert.IsNotEmpty(input.Message);
            Assert.IsFalse(input.SelectSquad(3));
            Assert.IsTrue(input.SelectSquad(0));
            Assert.IsTrue(input.TryGetAimPoint(out Vector3 point));
            Assert.That(point.y, Is.EqualTo(0f).Within(0.3f));
            Assert.IsTrue(NavMesh.SamplePosition(point, out _, 0.2f, NavMesh.AllAreas));
            Assert.IsTrue(input.OrderMove());
            Assert.AreEqual(SquadOrderKind.Move, center.Commander.OrderOf(0).Kind);
            Assert.That(Vector3.Distance(point, center.Commander.OrderOf(0).Destination), Is.LessThan(0.01f));
            Assert.AreEqual(SquadOrderKind.Hold, center.Commander.OrderOf(1).Kind);
            input.SelectAll();
            Assert.IsTrue(input.Order(SquadOrderKind.Attack));
            for (int squad = 0; squad < 3; squad++)
                Assert.AreEqual(SquadOrderKind.Attack, center.Commander.OrderOf(squad).Kind);
            Assert.IsTrue(input.Order(SquadOrderKind.Hold));
            Assert.IsTrue(Members(0).All(member => member.Directive == UnitDirective.Hold));
        }
    }
}
