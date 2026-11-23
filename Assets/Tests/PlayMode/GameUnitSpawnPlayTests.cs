using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>D-0b·UI-07: 준비 중에 모집한 잡졸은 시설 앞에 숨겨진 채 보관되고, 부대에 넣으면 나타나 지휘·전투 집계에 들어간다.</summary>
    public class GameUnitSpawnPlayTests
    {
        static int cleanup;
        GameBattleController battle;
        GameUnitSpawner spawner;
        SquadCommandCenter center;
        ConstructionWorld world;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            battle = Object.FindAnyObjectByType<GameBattleController>();
            spawner = Object.FindAnyObjectByType<GameUnitSpawner>();
            center = Object.FindAnyObjectByType<SquadCommandCenter>();
            world = Object.FindAnyObjectByType<ConstructionWorld>();
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameUnitSpawnCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        [UnityTest]
        public IEnumerator 모집한_잡졸은_숨겨진_채_보관되고_부대에_넣으면_나타나_걸어가고_다음_전투에_들어간다()
        {
            Assert.IsNotNull(spawner, "Game 씬에 GameUnitSpawner가 있어야 한다.");
            RunState state = GameSession.Flow.Run.State;
            int squadsBefore = center.Squads.Count;
            int membersBefore = center.Squads[0].Members.Count;
            Assert.IsTrue(center.TryGetGatherPoint(0, out Vector3 gather));
            Vector3 barracks = Object.FindObjectsByType<FacilityStation>()
                .First(station => station.Type == FacilityType.TrainingGround).transform.position;

            UnitCombatAI unit = spawner.Recruit(spawner.DebugSoldier, FacilityType.TrainingGround);
            Assert.IsNotNull(unit);
            Assert.IsFalse(unit.gameObject.activeSelf, "보관 병사는 씬에서 숨겨진다(UI-07).");
            Assert.AreEqual(gameObjectScene(), unit.gameObject.scene);
            Assert.Less(Flat(unit.transform.position, barracks), 12f, "병영 앞에 만든다.");
            Assert.AreEqual(squadsBefore, center.Squads.Count, "부대가 늘지 않는다.");
            Assert.AreEqual(membersBefore, center.Squads[0].Members.Count, "씬 부대에 들지 않는다.");
            int id = battle.RunLink.IdOf(unit.GetComponent<CharacterActor>());
            Assert.Greater(id, 0);
            var soldier = (RunSoldier)state.Find(id);
            Assert.IsNull(state.SquadOf(soldier));

            Assert.AreEqual(OrganizeCheck.Ok, battle.RunLink.TryMoveSoldier(soldier, 0));
            Assert.IsTrue(unit.isActiveAndEnabled, "부대에 넣으면 나타난다.");
            Assert.Contains(unit, center.Squads[0].Members.ToList());
            Assert.AreSame(state.Army.Squads[0], state.SquadOf(soldier));
            Assert.AreEqual(UnitDirective.Move, unit.Directive);
            Assert.IsTrue(unit.GetComponent<NavMeshAgent>().isOnNavMesh);

            float start = Flat(unit.transform.position, gather);
            for (int i = 0; i < 90; i++) yield return null;
            Assert.Less(Flat(unit.transform.position, gather), start - 2f, "부대가 모인 곳으로 걸어간다.");

            Assert.AreEqual(OrganizeCheck.Ok, battle.RunLink.TryStoreSoldier(soldier));
            Assert.IsFalse(unit.gameObject.activeSelf, "보관으로 돌리면 다시 숨겨진다.");
            Assert.IsFalse(center.Squads[0].Members.Contains(unit));
            Assert.AreEqual(OrganizeCheck.Ok, battle.RunLink.TryMoveSoldier(soldier, 0));

            Assert.IsTrue(battle.TryStartBattle());
            Assert.IsTrue(battle.RunLink.CurrentInput.Squads[0].Members.Any(member => member.Id == id), "다음 전투 입력에 들어간다.");
            Assert.IsNull(spawner.Recruit(spawner.DebugSoldier, FacilityType.TrainingGround), "전투 중에는 모집하지 않는다.");

            center.Commander.Select(0);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Attack));
            yield return null;
            Assert.AreEqual(UnitDirective.Engage, unit.Directive, "부대 지시를 받는다.");

            unit.Actor.ReceiveDamage(unit.Actor.Health.Current + 1f);
            foreach (var enemy in battle.Enemies) enemy.Actor.ReceiveDamage(enemy.Actor.Health.Current + 1f);
            yield return null; yield return null;
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
            Assert.IsNull(state.Find(id), "전투에서 죽은 모집 잡졸은 결과 집계로 런에서 빠진다.");
        }

        Scene gameObjectScene() => battle.gameObject.scene;

        [UnityTest]
        public IEnumerator 새_빈_부대를_만들어_넣으면_지휘_번호로_고를_수_있고_부대를_없애면_보관으로_돌아간다()
        {
            RunState state = GameSession.Flow.Run.State;
            int squads = center.Squads.Count;
            Assert.AreEqual(OrganizeCheck.Ok, battle.RunLink.TryAddSquad(out int added));
            Assert.AreEqual(squads, added);
            Assert.AreEqual(squads + 1, center.Squads.Count);
            Assert.AreEqual(squads + 1, center.Commander.SquadCount);
            Assert.AreEqual(squads + 1, state.Army.Squads.Count);

            UnitCombatAI extra = spawner.Recruit(spawner.DebugSoldier, FacilityType.TrainingGround);
            Assert.IsNotNull(extra);
            Assert.AreEqual(OrganizeCheck.Ok, battle.RunLink.TryMoveSoldier(state.StoredSoldiers.Single(), added));
            Assert.AreSame(extra, center.Squads[added].Members.Single());
            Assert.AreEqual(UnitDirective.Hold, extra.Directive, "빈 새 부대는 모일 곳이 없어 나타난 자리를 지킨다.");
            yield return null;

            var input = Object.FindAnyObjectByType<SquadCommandInput>();
            Assert.IsTrue(input.SelectSquad(added));
            Assert.IsTrue(center.Commander.IsSelected(added));
            yield return null;
            var markers = Object.FindAnyObjectByType<PlaceholderSquadMarkers>();
            if (markers != null) Assert.IsTrue(markers.IsMarked(extra), "새 부대원도 선택 표시가 붙는다.");

            Assert.AreEqual(OrganizeCheck.Ok, battle.RunLink.TryRemoveSquad(added));
            Assert.AreEqual(squads, state.Army.Squads.Count);
            Assert.AreEqual(squads, center.Squads.Count);
            Assert.AreEqual(squads, center.Commander.SquadCount);
            Assert.AreEqual(1, state.StoredSoldiers.Count, "부대원은 보관으로 돌아간다.");
            Assert.IsFalse(extra.gameObject.activeSelf);
        }
    }
}
