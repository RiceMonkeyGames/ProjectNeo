using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// PERF-ai-alloc-and-split: 실제 SiegePrototype 씬에서 바꾼 경로가 실제로 쓰이는지 확인한다.
    /// 할당은 같은 스레드의 GC 할당 바이트로 재고, 캐시·정리는 내부 상태를 직접 본다.
    /// </summary>
    public class AiAllocAndSplitPlayTests
    {
        const int Calls = 200;
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        delegate void FindPushersCall(out int pushers, out bool blocked);

        Scene scene;
        ConstructionWorld world;
        SiegeDirector director;
        SquadCommandCenter center;
        CharacterActor player;
        float previousCapture;
        CursorLockMode previousLock;
        bool previousVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCapture = Time.captureDeltaTime;
            previousLock = Cursor.lockState; previousVisible = Cursor.visible;
            Time.captureDeltaTime = 1f / 30f;
            yield return SceneManager.LoadSceneAsync("SiegePrototype", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            world = Find<ConstructionWorld>(); director = Find<SiegeDirector>(); center = Find<SquadCommandCenter>();
            yield return null;
            director.enabled = false;
            Find<SquadCommandInput>().enabled = false;
            var input = Find<PlayerInputController>();
            input.enabled = false;
            player = input.GetComponent<CharacterActor>();
            yield return NavigationReady();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.captureDeltaTime = previousCapture;
            Cursor.lockState = previousLock; Cursor.visible = previousVisible;
        }

        T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
        IEnumerable<UnitCombatAI> Allies() => center.Squads.SelectMany(s => s.Members).Where(m => m != null);

        IEnumerator NavigationReady()
        {
            for (int frame = 0; frame < 600; frame++)
            {
                yield return null;
                if (!world.IsRebuilding && !world.RebuildPending) yield break;
            }
            Assert.Fail("Navigation rebuild did not finish.");
        }

        // 한 번 미리 부른 뒤(지연 초기화 제외) Calls번 부르는 동안 GC 할당이 없는지. Unity의 GC.Alloc 기록으로 잰다.
        static void AssertNoAllocation(Action call, string message)
        {
            call();
            Assert.That(() => { for (int i = 0; i < Calls; i++) call(); }, Is.Not.AllocatingGCMemory(), message);
        }

        // 비교용: 예전 방식은 실제로 할당했다는 것을 같은 측정으로 보인다(측정이 동작한다는 확인도 겸한다).
        static void AssertAllocates(Action call, string message)
        {
            call();
            Assert.That(() => { for (int i = 0; i < Calls; i++) call(); }, Is.AllocatingGCMemory(), message);
        }

        static object Field(object owner, string name) => owner.GetType().GetField(name, Instance).GetValue(owner);
        static T Field<T>(object owner, string name) => (T)Field(owner, name);

        Vector3 WallTop()
        {
            PlacedStructure wall = world.Structures.First(p => p.Placement.Definition.Kind == BuildPieceKind.Wall
                && p.Placement.Level == 0 && !world.Layout.IsCovered(p.Placement));
            return wall.transform.position + Vector3.up * wall.Placement.Definition.Height;
        }

        FlyerBrain SpawnFlyer(Vector3 at)
        {
            GameObject prefab = director.Settings.EnemyGroups.Select(g => g.unitPrefab)
                .First(p => p != null && p.GetComponent<FlyerBrain>() != null);
            GameObject unit = Object.Instantiate(prefab, at, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(unit, scene);
            var brain = unit.GetComponent<FlyerBrain>();
            brain.Bind(world, new SiegeLadder[0], Vector2Int.up, director.Settings);
            unit.SetActive(true);
            return brain;
        }

        Vector3 Outside(Vector3 from, float distance)
        {
            Vector3 point = from + world.transform.TransformDirection(Vector3.forward) * distance;
            point.y = world.transform.position.y;
            return point;
        }

        [Test]
        public void AllocationProbe_SeesAllocations()
        {
            AssertAllocates(() => GC.KeepAlive(new byte[64]), "할당 측정이 동작해야 다른 무할당 검사가 의미가 있다.");
        }

        [UnityTest]
        public IEnumerator Flyer_RoofAndGroundProbes_DoNotAllocate_AndGroundProbeIsThrottled()
        {
            foreach (UnitCombatAI member in Allies()) { member.enabled = false; member.gameObject.SetActive(false); }
            player.gameObject.SetActive(false);
            Vector3 top = WallTop();
            UnitCombatAI ally = Allies().First();
            ally.transform.position = top;
            ally.gameObject.SetActive(true);
            Physics.SyncTransforms();
            FlyerBrain flyer = SpawnFlyer(Outside(top, 12f));
            for (int frame = 0; frame < 60 && flyer.Target == null; frame++) yield return null;
            Assert.AreSame(ally.Actor, flyer.Target);

            // 광선이 실제로 무언가에 맞는 경로를 재도록 머리 위에 지붕을 둔다(빈 광선은 예전 방식도 빈 배열을 재사용한다).
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(roof, scene);
            roof.transform.position = top + Vector3.up * (ally.Actor.Profile.Height + 1f);
            roof.transform.localScale = new Vector3(4f, 0.2f, 4f);
            Physics.SyncTransforms();
            Assert.IsTrue(flyer.IsSheltered(ally.Actor));
            Vector3 head = ally.transform.position + Vector3.up * (ally.Actor.Profile.Height + 0.05f);
            AssertNoAllocation(() => flyer.IsSheltered(ally.Actor), "지붕 검사는 공유 버퍼를 써서 할당하지 않는다.");
            AssertAllocates(() => GC.KeepAlive(Physics.RaycastAll(head, Vector3.up, flyer.Settings.RoofProbeDistance, ~0,
                QueryTriggerInteraction.Ignore)), "비교용: 예전 RaycastAll은 호출마다 배열을 만든다.");

            flyer.enabled = false;
            var updateBody = (Action)Delegate.CreateDelegate(typeof(Action), flyer,
                typeof(FlyerBrain).GetMethod("UpdateBody", Instance));
            AssertNoAllocation(updateBody, "발밑 검사는 할당하지 않는다.");
            // 허용 거리보다 크게 오가며 매번 다시 재게 해도(실제 광선 검사 경로) 할당하지 않는다.
            Vector3 home = flyer.transform.position;
            Vector3 step = Vector3.right * flyer.Settings.GroundProbeMoveTolerance * 2f;
            bool away = false;
            Vector3 lastProbe = Field<Vector3>(flyer, "groundProbePosition");
            AssertNoAllocation(() =>
            {
                away = !away;
                flyer.transform.position = away ? home + step : home;
                updateBody();
            }, "발밑 광선 검사 자체도 할당하지 않는다.");
            Assert.AreNotEqual(lastProbe, Field<Vector3>(flyer, "groundProbePosition"), "오가는 동안 실제로 다시 쟀다.");
            flyer.transform.position = home;
            updateBody();

            // 같은 시각에 조금 움직이면 다시 재지 않고, 허용 거리보다 많이 움직이면 다시 잰다.
            Vector3 probed = Field<Vector3>(flyer, "groundProbePosition");
            flyer.transform.position += Vector3.right * flyer.Settings.GroundProbeMoveTolerance * 0.4f;
            updateBody();
            Assert.AreEqual(probed, Field<Vector3>(flyer, "groundProbePosition"), "조금 움직인 것으로는 다시 재지 않는다.");
            flyer.transform.position += Vector3.right * flyer.Settings.GroundProbeMoveTolerance * 2f;
            updateBody();
            Assert.AreEqual(flyer.transform.position, Field<Vector3>(flyer, "groundProbePosition"), "많이 움직이면 다시 잰다.");
            Assert.IsTrue(Field<bool>(flyer, "groundFound"), "성 근처에서는 발판을 찾는다.");
        }

        [UnityTest]
        public IEnumerator Flyers_SpawnedTogether_StaggerLaterDecisions_AndShareShelterSamples()
        {
            foreach (UnitCombatAI member in Allies()) { member.enabled = false; member.gameObject.SetActive(false); }
            player.transform.position = world.transform.TransformPoint(new Vector3(world.Grid.CellSize * 2.5f, 0f, world.Grid.CellSize * 2f));
            Physics.SyncTransforms();
            FlyerBrain.ResetStaggerCounter();
            Vector3 start = Outside(WallTop(), 14f);
            var flyers = new[] { SpawnFlyer(start), SpawnFlyer(start + Vector3.right), SpawnFlyer(start + Vector3.left),
                SpawnFlyer(start + Vector3.right * 2f) };
            for (int frame = 0; frame < 30 && !flyers.All(f => f.HasDecided); frame++) yield return null;
            Assert.IsTrue(flyers.All(f => f.HasDecided));
            float[] waits = flyers.Select(f => Field<float>(f, "think")).ToArray();
            Debug.Log("PERF-ai-alloc-and-split flyer next-decision waits: " + string.Join(", ", waits.Select(w => w.ToString("0.000"))));
            Assert.AreEqual(flyers.Length, waits.Select(w => Mathf.Round(w * 1000f)).Distinct().Count(),
                "함께 생긴 비행 적의 다음 판단 시점이 서로 어긋난다.");
            yield return null;
            Assert.IsTrue(flyers.All(f => f.Target != null), "모두 노릴 대상을 잡는다.");
            Assert.IsTrue(flyers.All(f => f.LodTier == AILodTier.Lod0), "대상을 쫓는 비행 적은 바로 LOD 0이다.");
            object cache = typeof(FlyerBrain).GetField("ShelterCache", Static).GetValue(null);
            int samples = (int)cache.GetType().GetProperty("Count").GetValue(cache);
            Assert.Greater(samples, 0, "지붕 검사 결과를 공유 캐시에 남긴다.");
        }

        [UnityTest]
        public IEnumerator Ladder_FindPushers_DoesNotAllocate_AndDropsHandlesOfDisabledPushers()
        {
            SiegeLadder ladder = scene.GetRootGameObjects().Select(r => r.GetComponent<SiegeLadder>())
                .Where(l => l != null && l.HasWall).OrderBy(l => l.Cell.x).First();
            UnitCombatAI pusher = Allies().First(m => m.Actor.Profile.AttackKind == AttackKind.Melee);
            foreach (UnitCombatAI member in Allies())
                if (member != pusher) member.enabled = false;
            ladder.State.Tick(0.1f, true, false, false);
            ladder.State.Tick(ladder.Settings.LadderRaiseSeconds, true, false, false);
            Assert.AreEqual(LadderPhase.Up, ladder.State.Phase);
            Assert.IsTrue(pusher.GetComponent<NavMeshAgent>().Warp(ladder.TopPoint));
            pusher.enabled = false;
            yield return null;
            yield return null;

            var handles = (IDictionary)Field(ladder, "aiPushHandles");
            var parts = (IDictionary)Field(ladder, "pusherParts");
            Assert.IsTrue(handles.Contains(pusher.Actor), "꼭대기에서 대상 없는 아군 AI는 미는 쪽으로 잡힌다.");

            var findPushers = (FindPushersCall)Delegate.CreateDelegate(typeof(FindPushersCall), ladder,
                typeof(SiegeLadder).GetMethod("FindPushers", Instance));
            findPushers(out int counted, out bool blocked);
            Assert.AreEqual(1, counted, "꼭대기의 아군 한 명만 센다.");
            Assert.IsFalse(blocked);
            AssertNoAllocation(() => findPushers(out _, out _), "밀기 후보 검색은 할당하지 않는다.");

            pusher.gameObject.SetActive(false);
            yield return null;
            Assert.IsFalse(handles.Contains(pusher.Actor), "꺼진 아군의 밀기 핸들은 다음 프레임에 놓는다.");
            Assert.IsFalse(parts.Contains(pusher.Actor), "꺼진 아군의 부품 캐시도 비운다.");
        }

        [UnityTest]
        public IEnumerator PlacedStructure_Bodies_IsCachedAndAllocationFree()
        {
            PlacedStructure piece = world.Structures.First(p => p.Placement.Definition.Kind == BuildPieceKind.Wall);
            Collider[] first = piece.Bodies;
            Assert.IsNotEmpty(first);
            CollectionAssert.AreEquivalent(piece.GetComponents<Collider>(), first);
            Assert.AreSame(first, piece.Bodies, "같은 배열을 다시 준다.");
            AssertNoAllocation(() => GC.KeepAlive(piece.Bodies), "캐시한 충돌체 목록은 할당하지 않는다.");
            AssertAllocates(() => GC.KeepAlive(piece.GetComponents<Collider>()), "비교용: 예전 GetComponents는 호출마다 배열을 만든다.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator WallBreaker_Decide_UsesCachedBodies()
        {
            // 감독이 설정과 성을 연결해 둔 실제 적 중 성벽 파괴병을 쓴다.
            WallBreakerAttack breaker = director.Enemies.Select(e => e.GetComponent<WallBreakerAttack>())
                .FirstOrDefault(b => b != null);
            if (breaker == null) Assert.Ignore("이 씬 설정에는 성벽 파괴병이 없다.");
            foreach (var attacker in breaker.GetComponents<SiegeAttacker>()) attacker.enabled = false;
            breaker.Bind(world);
            Vector3 at = Outside(WallTop(), 3f);
            breaker.transform.position = at;
            breaker.gameObject.SetActive(true);
            Assert.IsTrue(breaker.GetComponent<NavMeshAgent>().Warp(at));
            yield return null;
            breaker.Decide();
            Assert.IsNotNull(breaker.Target, "닿는 성벽 조각을 고른다.");
            Assert.AreSame(breaker.Target.Bodies, Field(breaker, "targetBodies"), "대상 충돌체는 조각의 캐시 배열을 쓴다.");
        }

        [UnityTest]
        public IEnumerator UnitCombatAI_TargetChange_DirectiveAndDisable_ClearTransientState()
        {
            UnitCombatAI unit = Allies().First(m => m.Actor.Profile.AttackKind == AttackKind.Melee);
            foreach (UnitCombatAI member in Allies()) member.enabled = member == unit;
            // 꺼진 적은 Awake 전이라 SiegeAttacker.Actor가 비어 있으므로 부품을 직접 찾는다.
            CharacterActor[] enemies = director.Enemies.Take(2).Select(e => e.GetComponent<CharacterActor>()).ToArray();
            Assert.AreNotSame(enemies[0], enemies[1]);
            yield return null;

            var board = (MeleeSlotBoard<UnitCombatAI>)typeof(UnitCombatAI).GetField("meleeSlotBoard", Static).GetValue(null);
            PropertyInfo target = typeof(UnitCombatAI).GetProperty("Target");
            MethodInfo setTarget = target.GetSetMethod(true);
            var fallback = Field<RangedFallbackState>(unit, "fallback");
            var flow = Field<FlowFieldAdvanceState>(unit, "flowField");

            // 대상이 바뀌면 이전 대상의 근접 자리를 놓는다.
            setTarget.Invoke(unit, new object[] { enemies[0] });
            board.Take(unit, enemies[0], 0, 0);
            Assert.IsTrue(board.TryGet(unit, enemies[0], out _, out _));
            setTarget.Invoke(unit, new object[] { enemies[1] });
            Assert.AreSame(enemies[1], unit.Target);
            Assert.IsFalse(board.TryGet(unit, enemies[0], out _, out _),
                $"대상을 바꾸면 이전 대상 자리를 놓는다. same={ReferenceEquals(enemies[0], enemies[1])} claims={board.Count}");

            // 지시를 바꾸면 후퇴·Flow Field·근접 자리·대상이 한꺼번에 비워진다.
            board.Take(unit, enemies[1], 0, 0);
            fallback.Begin(5f);
            flow.Begin(Vector3.forward);
            Assert.IsTrue(unit.IsFallingBack && unit.IsUsingFlowField);
            unit.SetDirective(UnitDirective.Hold);
            Assert.IsFalse(unit.IsFallingBack, "지시 변경은 후퇴를 멈춘다.");
            Assert.IsFalse(unit.IsUsingFlowField, "지시 변경은 Flow Field 전진을 멈춘다.");
            Assert.AreEqual(Vector3.zero, flow.Direction);
            Assert.IsNull(unit.Target);
            Assert.IsFalse(board.TryGet(unit, enemies[1], out _, out _));

            // 꺼질 때도 같은 정리를 한다(예전에는 Flow Field 표시가 남았다).
            setTarget.Invoke(unit, new object[] { enemies[0] });
            board.Take(unit, enemies[0], 0, 0);
            fallback.Begin(5f);
            flow.Begin(Vector3.forward);
            unit.enabled = false;
            Assert.IsFalse(unit.IsFallingBack);
            Assert.IsFalse(unit.IsUsingFlowField, "꺼진 유닛에 Flow Field 전진이 남지 않는다.");
            Assert.IsNull(unit.Target);
            Assert.IsFalse(board.TryGet(unit, enemies[0], out _, out _));
        }
    }
}
