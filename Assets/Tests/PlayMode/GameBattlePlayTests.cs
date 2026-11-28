using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Flow;
using ProjectNeo.Run;
using ProjectNeo.Siege;
using ProjectNeo.Squads;
using ProjectNeo.Walls;
using ProjectNeo.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    public class GameBattlePlayTests
    {
        static int cleanup;
        GameBattleController battle;
        ConstructionWorld world;
        CharacterActor player;
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
            player = Object.FindAnyObjectByType<PlayerInputController>().GetComponent<CharacterActor>();
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameBattleCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }
        IEnumerator Navigation()
        {
            yield return null;
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            Assert.IsFalse(world.IsRebuilding || world.RebuildPending);
            yield return null;
        }
        static void Kill(CharacterActor actor) => actor.ReceiveDamage(actor.Health.Current + 1f);
        static Rect Bounds(Transform target)
        {
            var corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [UnityTest]
        public IEnumerator 전투_HUD의_부대_단계_상태_조작_영역은_겹치지_않고_건설_안내도_도구를_가리지_않는다()
        {
            Assert.IsTrue(battle.TryStartBattle());
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var hud = Object.FindAnyObjectByType<GameFlowHud>();
            var command = Object.FindAnyObjectByType<SquadCommandHud>();
            Assert.IsFalse(hud.transform.Find("InteractionPrompt").gameObject.activeSelf);
            Assert.IsTrue(hud.transform.Find("BattleStatus").gameObject.activeSelf);
            Transform[] panels = { hud.transform.Find("Panel"), hud.transform.Find("BattleStatus"),
                command.transform.Find("SquadsBacking"), command.transform.Find("HelpBacking") };
            foreach (string name in new[] { "Squads", "Help" })
            {
                Rect backing = Bounds(command.transform.Find(name + "Backing"));
                Rect label = Bounds(command.transform.Find(name));
                Assert.IsTrue(backing.Contains(label.min) && backing.Contains(label.max), name + " 배경이 글자를 감싸야 한다.");
            }
            for (int a = 0; a < panels.Length; a++)
                for (int b = a + 1; b < panels.Length; b++)
                    Assert.IsFalse(Bounds(panels[a]).Overlaps(Bounds(panels[b])), panels[a].name + " / " + panels[b].name);
            GameSession.Flow.FinishBattle(BattleOutcome.Victory);
            GameSession.Flow.ContinueToNextStage();
            yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<GamePhaseView>().EnterConstruction());
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var construction = Object.FindAnyObjectByType<ConstructionHud>();
            // UI-13: 건설 막대가 아래 가운데에 서고, 아래 안내 판은 막대 옆 키 가이드로 대신해 숨긴다.
            Assert.IsFalse(hud.transform.Find("InteractionPrompt").gameObject.activeInHierarchy
                && Bounds(hud.transform.Find("InteractionPrompt")).Overlaps(Bounds(construction.transform.Find("BuildPanel"))));
            foreach (string part in new[] { "BuildTabs", "BuildStatus" })
                Assert.IsFalse(Bounds((RectTransform)construction.transform.Find(part)).Overlaps(Bounds(construction.transform.Find("BuildPanel"))),
                    part + " must not cover the build bar");
            // 건설 키는 탐색·배치 화면과 같은 오른쪽 아래 키 가이드(KeyListHud)가 보여 준다.
            var keys = Object.FindAnyObjectByType<KeyListHud>();
            Assert.IsTrue(keys.IsShowing, "건설 모드에서도 키 가이드가 보인다.");
            Assert.IsFalse(Bounds(keys.Area).Overlaps(Bounds(construction.transform.Find("BuildPanel"))), "키 가이드가 건설 막대를 덮지 않는다.");
        }

        [UnityTest]
        public IEnumerator 기존_깃발은_즉시_시작하지_않고_전투_시작_후_먼_들판의_적이_성을_향해_온다()
        {
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
            Assert.Greater(battle.Enemies.Count, 0);
            Assert.IsTrue(battle.Enemies.All(enemy => !enemy.gameObject.activeSelf));
            var station = Object.FindAnyObjectByType<BattleStartStation>();
            var interaction = Object.FindAnyObjectByType<PreparationInteraction>();
            var bounds = station.GetComponent<BoxCollider>().bounds;
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            player.transform.position = new Vector3(bounds.center.x, 0f, bounds.min.z - 2f);
            motor.enabled = true;
            yield return null;
            Cursor.lockState = CursorLockMode.Locked;
            var camera = Object.FindAnyObjectByType<FirstPersonCamera>();
            camera.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Physics.SyncTransforms();
            interaction.RefreshTarget();
            Assert.IsNull(interaction.Target);
            Assert.IsFalse(interaction.TryInteract());
            Assert.IsFalse(station.Interact());
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
            Assert.IsTrue(battle.TryStartBattle());
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase);
            Assert.IsTrue(battle.IsRunning);
            Assert.IsTrue(battle.Enemies.All(enemy => enemy.gameObject.activeSelf));
            Assert.IsFalse(station.Interact(), "중복 전투 시작을 막는다.");
            Assert.IsFalse(Object.FindAnyObjectByType<GamePhaseView>().EnterConstruction());
            Vector3 start = battle.Enemies[0].transform.position;
            // 북쪽 외벽 바깥면(z 277.5m, SIEGE-36)에서 70m 넘게 떨어진 들판에서 나온다.
            Assert.Greater(start.z - world.transform.TransformPoint(new Vector3(176.25f, 0f, 277.5f)).z, 70f);
            for (int i = 0; i < 120; i++) yield return null;
            Assert.Less(battle.Enemies[0].transform.position.z, start.z - 1f, "적이 먼 들판에서 성으로 접근해야 한다.");
        }

        [UnityTest]
        public IEnumerator 실제_적_전멸로_보상에_가고_전투를_멈춘_뒤_같은_성에서_다음_준비를_한다()
        {
            Assert.IsTrue(battle.TryStartBattle());
            var ally = Object.FindAnyObjectByType<SquadCommandCenter>().Squads[0].Members[0];
            var wall = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall);
            wall.ReceiveDamage(12f);
            float health = wall.Health.Current;
            int enemyCount = battle.Enemies.Count;
            var shot = new GameObject("PendingBattleShot").AddComponent<CombatProjectile>();
            shot.transform.position = battle.Enemies[0].Actor.AimPoint;
            shot.Launch(battle.Enemies[0].Actor, 1f, Vector3.up * 18f);
            foreach (var enemy in battle.Enemies) Kill(enemy.Actor);
            yield return null; yield return null;
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
            Assert.AreEqual(BattleOutcome.Victory, GameSession.Flow.LastOutcome);
            Assert.IsFalse(battle.IsRunning || ally.enabled || player.enabled);
            Assert.IsTrue(shot == null, "결과 화면에서 투사체가 계속 날아가지 않아야 한다.");
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return null;
            Assert.AreEqual(2, GameSession.Flow.Run.StageNumber);
            Assert.AreEqual(health, wall.Health.Current, "성벽 피해를 유지한다.");
            Assert.IsTrue(ally.enabled && player.enabled);
            Assert.AreEqual(enemyCount, battle.Enemies.Count);
            Assert.IsTrue(battle.Enemies.All(enemy => !enemy.gameObject.activeSelf));
            Assert.AreEqual(enemyCount, battle.GetComponentsInChildren<SiegeAttacker>(true).Length);
            Assert.IsTrue(battle.TryStartBattle());
            Assert.AreEqual(2, battle.BattleNumber);
        }

        // 성벽 조각들의 가운데 근처 걸을 수 있는 곳(안뜰)과, 적이 나오는 먼 들판의 걸을 수 있는 곳.
        Vector3 Courtyard(float offset = 0f)
        {
            var walls = world.Structures.Where(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).ToArray();
            Vector3 middle = walls.Aggregate(Vector3.zero, (sum, piece) => sum + piece.transform.position) / walls.Length;
            middle.y = 0f;
            // 성 가운데는 분수 광장이라(SIEGE-36) 그 남쪽 큰길 위 빈 땅을 쓴다.
            middle += Vector3.right * offset + Vector3.back * 15f;
            Assert.IsTrue(NavMesh.SamplePosition(middle, out NavMeshHit hit, 10f, NavMesh.AllAreas));
            return hit.position;
        }
        Vector3 Field()
        {
            Vector3 point = world.transform.TransformPoint(new Vector3(battle.Settings.SpawnCell.x * world.Grid.CellSize,
                0f, battle.Settings.SpawnCell.y * world.Grid.CellSize));
            Assert.IsTrue(NavMesh.SamplePosition(point, out NavMeshHit hit, 10f, NavMesh.AllAreas));
            return hit.position;
        }
        static Ray Down(Vector3 point) => new Ray(point + Vector3.up * 40f, Vector3.down);

        [UnityTest]
        public IEnumerator 준비_탐색에서_부대_지휘가_켜지고_성_밖_목적지는_거절하며_전투_중에는_막지_않는다()
        {
            yield return Navigation();
            var view = Object.FindAnyObjectByType<GamePhaseView>();
            var command = Object.FindAnyObjectByType<SquadCommandInput>();
            var hud = Object.FindAnyObjectByType<SquadCommandHud>(FindObjectsInactive.Include);
            Assert.IsTrue(view.IsExplorationView && command.enabled && hud.gameObject.activeInHierarchy);
            GamePrepPlacement prep = view.PrepPlacement;
            Assert.IsNotNull(prep);
            Assert.IsNull(prep.CheckDestination(Courtyard()));
            Assert.IsTrue(prep.IsInsideCastle(Courtyard()));
            Assert.AreEqual(GamePrepPlacement.OutsideMessage, prep.CheckDestination(Field()));
            Assert.AreEqual(GamePrepPlacement.OutsideMessage, command.DestinationCheck(Field()), "1인칭 지휘도 같은 심사를 쓴다.");
            Assert.IsTrue(battle.TryStartBattle());
            Assert.IsNull(prep.CheckDestination(Field()), "전투 중 지시는 성 밖도 허용한다.");
        }

        [UnityTest]
        public IEnumerator 부대_배치_화면은_건설과_따로_열리고_키와_유닛_클릭으로_고르며_좌클릭한_성_안으로_걸어간다()
        {
            yield return Navigation();
            var view = Object.FindAnyObjectByType<GamePhaseView>();
            var center = Object.FindAnyObjectByType<SquadCommandCenter>();
            var placementTool = Object.FindAnyObjectByType<PlacementController>(FindObjectsInactive.Include);
            var buildHud = Object.FindAnyObjectByType<ConstructionHud>(FindObjectsInactive.Include);
            var commandHud = Object.FindAnyObjectByType<SquadCommandHud>(FindObjectsInactive.Include);
            var command = Object.FindAnyObjectByType<SquadCommandInput>(FindObjectsInactive.Include);
            GamePrepPlacement prep = view.PrepPlacement;
            Assert.IsFalse(commandHud.HelpVisible, "준비 단계에서는 전투용 키 안내 상자를 숨긴다.");
            Assert.IsTrue(view.EnterPlacement());
            Assert.IsTrue(view.IsPlacementView && prep.PlacementMode);
            Assert.IsFalse(view.IsBuildView || placementTool.enabled || buildHud.gameObject.activeInHierarchy,
                "부대 배치 화면에는 건설 도구가 없다.");
            Assert.IsFalse(view.EnterConstruction() || view.EnterPanel(), "다른 화면과 겹쳐 열리지 않는다.");
            Assert.IsFalse(command.enabled, "배치 화면은 1인칭 지휘 대신 클릭으로 배치한다.");
            Assert.IsTrue(commandHud.gameObject.activeInHierarchy && !commandHud.HelpVisible);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);

            UnitCombatAI member = center.Squads[1].Members[0];
            Assert.IsTrue(prep.ClickAt(Down(member.transform.position)), "유닛을 클릭하면 그 부대를 고른다.");
            Assert.IsTrue(center.Commander.IsSelected(1) && !center.Commander.IsSelected(0));
            Assert.IsTrue(prep.SelectSquadForPlacement(0));
            Assert.IsTrue(center.Commander.IsSelected(0) && !center.Commander.IsSelected(1));

            Vector3 target = Courtyard(6f);
            Assert.IsFalse(prep.ClickAt(Down(Field())));
            Assert.AreEqual(GamePrepPlacement.OutsideMessage, prep.Message);
            Assert.AreNotEqual(SquadOrderKind.Move, center.Commander.OrderOf(0).Kind);
            Assert.IsTrue(prep.ClickAt(Down(target)));
            SquadOrder order = center.Commander.OrderOf(0);
            Assert.AreEqual(SquadOrderKind.Move, order.Kind);
            Assert.Less(Vector3.Distance(order.Destination, target), 1.5f);
            Assert.IsTrue(prep.Memory.TryGet(0, out SquadPlacementMemory.Entry remembered));
            Assert.Less(Vector3.Distance(remembered.Position, target), 1.5f);

            Vector3 start = center.Squads[0].Members[0].transform.position;
            for (int i = 0; i < 60; i++) yield return null;
            Assert.Less(Vector3.Distance(center.Squads[0].Members[0].transform.position, target),
                Vector3.Distance(start, target) - 0.5f, "순간이동이 아니라 걸어서 간다.");
            view.ExitPlacement();
            yield return null;
            Assert.IsTrue(view.IsExplorationView && command.enabled && !prep.PlacementMode);
            Assert.AreEqual(0, center.Commander.SelectedCount, "배치 화면을 닫으면 고른 부대를 놓는다.");
        }

        [UnityTest]
        public IEnumerator 준비_단계에_성문을_F로_열고_닫으며_전투가_시작되면_닫히고_문_자리의_아군은_안쪽으로_옮겨진다()
        {
            yield return Navigation();
            var gate = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            var station = gate.GetComponent<PreparationGateStation>();
            Assert.IsNotNull(station, "성문에 준비 단계 상호작용이 붙는다.");
            Assert.IsFalse(gate.IsDoorOpen);
            Assert.AreEqual("성문 열기", station.Prompt);
            Assert.IsTrue(station.Interact());
            Assert.IsTrue(gate.IsHeldOpen && gate.IsDoorOpen);
            Assert.AreEqual("성문 닫기", station.Prompt);
            yield return Navigation();
            Assert.IsTrue(station.Interact());
            Assert.IsFalse(gate.IsHeldOpen || gate.IsDoorOpen);
            yield return Navigation();

            Assert.IsTrue(station.Interact());
            yield return Navigation();
            // 열린 문 자리에 아군 한 명을 세워 둔다.
            var center = Object.FindAnyObjectByType<SquadCommandCenter>();
            UnitCombatAI member = center.Squads[0].Members[0];
            Vector3 doorway = gate.DoorCollider.bounds.center;
            Assert.IsTrue(NavMesh.SamplePosition(doorway, out NavMeshHit hit, 3f, NavMesh.AllAreas));
            member.GetComponent<NavMeshAgent>().Warp(hit.position);
            Assert.IsTrue(battle.TryStartBattle());
            Assert.IsFalse(gate.IsHeldOpen || gate.IsDoorOpen, "전투가 시작되면 성문이 닫힌다.");
            Assert.IsFalse(gate.DoorCollider.bounds.Contains(new Vector3(member.transform.position.x,
                gate.DoorCollider.bounds.center.y, member.transform.position.z)), "문 자리의 아군은 안쪽으로 옮겨진다.");
            Vector3 outward = world.transform.TransformDirection(new Vector3(battle.Settings.Outward.x, 0f, battle.Settings.Outward.y));
            Assert.Less(Vector3.Dot(member.transform.position - gate.DoorCollider.bounds.center, outward), 0f,
                "옮겨진 자리는 성 안쪽이다.");
            Assert.IsFalse(station.Interact(), "전투 중에는 열 수 없다.");
        }

        [UnityTest]
        public IEnumerator 준비_때_마지막_배치_자리를_기억해_다음_준비_단계에_걸어서_돌아가고_전투_중_지시는_기억하지_않는다()
        {
            yield return Navigation();
            var view = Object.FindAnyObjectByType<GamePhaseView>();
            var center = Object.FindAnyObjectByType<SquadCommandCenter>();
            GamePrepPlacement prep = view.PrepPlacement;
            Vector3 placed = Courtyard(6f);
            center.Commander.Select(0);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, placed, 0f));
            center.Commander.ClearSelection();
            Assert.IsTrue(prep.Memory.TryGet(0, out _));

            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Vector3 elsewhere = Courtyard(-6f);
            center.Commander.Select(0);
            center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, elsewhere, 0f));
            center.Commander.ClearSelection();
            Assert.IsTrue(prep.Memory.TryGet(0, out SquadPlacementMemory.Entry kept));
            Assert.Less(Vector3.Distance(kept.Position, placed), 0.01f, "전투 중 지시로 기억이 바뀌지 않는다.");
            foreach (var enemy in battle.Enemies) Kill(enemy.Actor);
            yield return null; yield return null;
            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return Navigation();
            yield return null; yield return null;
            SquadOrder order = center.Commander.OrderOf(0);
            Assert.IsTrue(order.HasDestination);
            Assert.Less(Vector3.Distance(order.Destination, placed), 1.5f, "다음 준비 단계에서 기억한 자리로 돌아간다.");
            Assert.AreEqual(0, center.Commander.SelectedCount, "복귀 뒤 선택은 원래대로 비어 있다.");
        }

        [UnityTest]
        public IEnumerator 스테이지_1에서_성문이_부서진_채로_넘어간_스테이지_2에서도_적이_성으로_다가온다()
        {
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            // 성문이 부서진 채 이기면 다음 스테이지 시작부터 먼 들판에서 성 안까지 걸어갈 길이 열려 있다.
            var gate = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            gate.ReceiveDamage(gate.Health.Current + 1f);
            yield return Navigation();
            foreach (var enemy in battle.Enemies) Kill(enemy.Actor);
            yield return null; yield return null;
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return Navigation();
            Assert.AreEqual(2, GameSession.Flow.Run.StageNumber);
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Vector3[] start = battle.Enemies.Select(enemy => enemy.transform.position).ToArray();
            for (int i = 0; i < 120; i++) yield return null;
            int moved = 0;
            string idle = "";
            for (int i = 0; i < start.Length; i++)
            {
                SiegeAttacker enemy = battle.Enemies[i];
                if (enemy.transform.position.z < start[i].z - 1f) { moved++; continue; }
                var agent = enemy.GetComponent<NavMeshAgent>();
                idle += $"\n{enemy.name}: active={enemy.gameObject.activeInHierarchy} brain={enemy.enabled} " +
                    $"actor={enemy.Actor.enabled} agent={(agent != null && agent.enabled)} onMesh={(agent != null && agent.enabled && agent.isOnNavMesh)} " +
                    $"stopped={(agent != null && agent.enabled && agent.isOnNavMesh && agent.isStopped)} decided={enemy.HasDecided}";
            }
            Assert.AreEqual(start.Length, moved, "스테이지 2의 적이 성으로 다가와야 한다." + idle);
        }

        [UnityTest]
        public IEnumerator 플레이어_사망과_적_전멸이_동시에_일어나면_패배하고_타이틀로_돌아갈_수_있다()
        {
            Assert.IsTrue(battle.TryStartBattle());
            foreach (var enemy in battle.Enemies) Kill(enemy.Actor);
            Kill(player);
            yield return null; yield return null;
            Assert.AreEqual(GamePhase.RunResult, GameSession.Flow.Phase);
            Assert.AreEqual(BattleOutcome.Defeat, GameSession.Flow.LastOutcome);
            Assert.IsTrue(GameSession.Flow.ReturnToTitle());
            for (int i = 0; i < 300 && SceneManager.GetActiveScene().name != "Title"; i++) yield return null;
            Assert.AreEqual("Title", SceneManager.GetActiveScene().name);
        }

        // BUG-approach-navmesh-gap: 출현 들판(건설 줄 밖)에서 성문 앞·성벽 양끝 앞까지 걸어서 닿고,
        // 성 안(성벽 남쪽)으로는 성문·성벽을 거치지 않고 돌아 들어갈 수 없다(분지는 북쪽 성벽으로만 열린다).
        [UnityTest]
        public IEnumerator 출현_들판에서_성벽_앞까지_길이_이어지고_성_안으로는_돌아_들어갈_수_없다()
        {
            yield return Navigation();
            float cell = world.Grid.CellSize;
            int width = world.Grid.Width;
            Vector3 Sample(float x, float z)
            {
                Assert.IsTrue(NavMesh.SamplePosition(world.transform.TransformPoint(new Vector3(x, 0f, z)),
                    out NavMeshHit hit, 6f, NavMesh.AllAreas), "길찾기 메시가 있어야 한다: " + x + ", " + z);
                return hit.position;
            }
            var path = new NavMeshPath();
            Vector2 spawn = battle.Settings.SpawnCell;
            float[] spawnX = { (spawn.x - 12f) * cell, spawn.x * cell, (spawn.x + 12f) * cell };
            float[] wallFrontX = { 2f * cell, (spawn.x + 0.5f) * cell, (width - 2f) * cell };
            foreach (float sx in spawnX)
            {
                Vector3 from = Sample(sx, spawn.y * cell);
                foreach (float wx in wallFrontX)
                {
                    Assert.IsTrue(NavMesh.CalculatePath(from, Sample(wx, cell * 1.4f), NavMesh.AllAreas, path));
                    Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "출현 들판 " + sx + " → 성벽 앞 " + wx);
                }
                foreach (float ix in new[] { 4f * cell, (spawn.x + 0.5f) * cell, (width - 4f) * cell })
                {
                    NavMesh.CalculatePath(from, Sample(ix, -3f * cell), NavMesh.AllAreas, path);
                    Assert.AreNotEqual(NavMeshPathStatus.PathComplete, path.status, "성 안 " + ix + "로 돌아 들어가는 길이 없어야 한다.");
                }
            }
        }

        [UnityTest]
        public IEnumerator 공성병이_없고_진입로가_없으면_남은_궁수가_퇴각하고_승리한다()
        {
            Assert.IsTrue(battle.TryStartBattle());
            var archer = battle.Enemies.Last(enemy => enemy.Role == SiegeAttackerRole.Archer);
            foreach (var enemy in battle.Enemies) if (enemy != archer) Kill(enemy.Actor);
            for (int i = 0; i < 20 && !battle.IsRetreating; i++) yield return null;
            Assert.IsTrue(battle.IsRetreating);
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase, "퇴각 이동 없이 즉시 승리하지 않는다.");
            Vector3 start = archer.transform.position;
            for (int i = 0; i < 900 && GameSession.Flow.Phase == GamePhase.Battle; i++) yield return null;
            Assert.Greater(archer.transform.position.z, start.z + 1f);
            Assert.AreEqual(1, battle.RetreatedCount);
            Assert.IsFalse(archer.Actor.Health.IsDead, "퇴각은 사망과 구분한다.");
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
        }

        [UnityTest]
        public IEnumerator 성문이_열려_진입로가_있으면_궁수만_남아도_퇴각하지_않는다()
        {
            Assert.IsTrue(battle.TryStartBattle());
            var gate = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            gate.ReceiveDamage(gate.Health.Current);
            var archer = battle.Enemies.Last(enemy => enemy.Role == SiegeAttackerRole.Archer);
            foreach (var enemy in battle.Enemies) if (enemy != archer) Kill(enemy.Actor);
            yield return Navigation();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsFalse(battle.IsRetreating);
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase);
        }

        // 북쪽 성벽 바깥 면의 사다리 자리 중, 칸과 양옆 두 칸이 모두 1층인 칸의 자리(SIEGE-37).
        // 모서리 탑·성문탑(2층) 바로 옆 칸을 올리면 그 사이 칸이 골짜기가 된다(SIEGE-36).
        LadderSlot FlatNorthSlot()
        {
            var catalog = new LadderSlotCatalog();
            catalog.Refresh(world, battle.Settings.Siege, battle.Settings.Outward);
            bool Flat(Vector2Int cell)
            {
                for (int dx = -2; dx <= 2; dx++)
                    if (world.Layout.ColumnHeight(cell + new Vector2Int(dx, 0)) != 1) return false;
                return true;
            }
            var slots = catalog.Slots.Where(s => s.Outward == battle.Settings.Outward && Flat(s.Cell))
                .OrderBy(s => s.Cell.x).ThenBy(s => s.Offset).ToList();
            Assert.IsNotEmpty(slots, "북쪽 성벽에 운반 사다리를 걸 빈틈 자리가 있어야 한다.");
            return slots[slots.Count / 2];
        }

        string CarrierReport(SiegeAttackerBrain carrier)
        {
            var catalog = new LadderSlotCatalog();
            catalog.Refresh(world, battle.Settings.Siege, battle.Settings.Outward);
            var agent = carrier.GetComponent<NavMeshAgent>();
            var path = new NavMeshPath();
            int reachable = catalog.Slots.Count(s => agent.isOnNavMesh && agent.CalculatePath(s.BasePoint, path) && path.status == NavMeshPathStatus.PathComplete);
            object mode = typeof(SiegeAttackerBrain).GetField("mode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(carrier);
            LadderSlot near = catalog.Slots.OrderBy(s => (s.BasePoint - carrier.transform.position).sqrMagnitude).First();
            agent.CalculatePath(near.BasePoint, path);
            string end = path.corners.Length > 0 ? path.corners[path.corners.Length - 1].ToString() : "none";
            var gate = world.Structures.First(p => p.Placement.Definition.Kind == BuildPieceKind.Gate).transform.position;
            var gatePath = new NavMeshPath();
            agent.CalculatePath(gate + world.transform.forward * 8f, gatePath);
            return $"near={near.BasePoint} status={path.status} end={end} corners={path.corners.Length} gate={gate} gateStatus={gatePath.status} gateEnd={(gatePath.corners.Length > 0 ? gatePath.corners[gatePath.corners.Length - 1].ToString() : "none")} slots={catalog.Slots.Count} reachable={reachable} onMesh={agent.isOnNavMesh} pos={carrier.transform.position} mode={mode} rebuilding={world.IsRebuilding || world.RebuildPending} directive={carrier.GetComponent<UnitCombatAI>().Directive}";
        }

        // 북쪽 성벽 가운데 자리 밑에서 바깥으로 12m 나간 들판(길찾기 메시 위).
        Vector3 InFrontOfTheWall()
        {
            LadderSlot slot = FlatNorthSlot();
            Vector3 outward = world.transform.TransformDirection(new Vector3(slot.Outward.x, 0f, slot.Outward.y));
            Assert.IsTrue(NavMesh.SamplePosition(slot.BasePoint + outward * 12f, out NavMeshHit hit, 4f, NavMesh.AllAreas));
            return hit.position;
        }

        SiegeLadder PlacedLadderClone(LadderSlot slot)
        {
            Assert.IsNotNull(battle.LadderTemplate, "Game 전투에 운반 사다리 원본이 있어야 한다(SIEGE-37).");
            SiegeLadder ladder = Object.Instantiate(battle.LadderTemplate, battle.LadderTemplate.transform.parent);
            ladder.gameObject.SetActive(true);
            Assert.IsTrue(ladder.State.TryLift());
            Assert.IsTrue(ladder.PlaceAt(slot));
            return ladder;
        }

        [UnityTest]
        public IEnumerator 사다리는_삼층_성벽_맨_위까지_연결되어_적이_오른다()
        {
            yield return Navigation();
            LadderSlot slot = FlatNorthSlot();
            SiegeLadder probe = PlacedLadderClone(slot);
            var wall = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
            SiegeSettings siege = battle.Settings.Siege;
            // 5m·10m·15m 성벽마다 사다리가 받침 높이까지 닿고, 75도로 기대며, 층수만큼 세우기·오르기가 길어진다(SIEGE-21).
            for (int storey = 1; storey <= 3; storey++)
            {
                if (storey > 1) Assert.IsTrue(world.TryPlace(wall, slot.Cell, 0, out _));
                Assert.IsTrue(probe.HasWall);
                Assert.AreEqual(wall.Height * storey, probe.WallHeight, 0.001f);
                Assert.AreEqual(storey, probe.Storeys);
                Assert.AreEqual(siege.LadderClimbSecondsPerStorey * storey, probe.ClimbSeconds, 0.001f);
                Assert.AreEqual(siege.LadderRaiseSeconds + siege.LadderRaiseSecondsPerExtraStorey * (storey - 1),
                    probe.RaiseSeconds, 0.001f);
                Vector3 lean = probe.EdgePoint - probe.BasePoint;
                Assert.GreaterOrEqual(probe.EdgePoint.y - world.transform.position.y, wall.Height * storey - 0.001f, "사다리 끝은 성벽 위(받침)까지 닿아야 한다.");
                Assert.AreEqual(siege.LadderLeanDegrees, Vector3.Angle(new Vector3(lean.x, 0f, lean.z), lean), 0.5f);
            }
            Object.Destroy(probe.gameObject);
            yield return Navigation();
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            // 운반병이 들고 온 사다리를 그 자리에 걸고 세우면, 그 운반병이 3층 꼭대기까지 오른다.
            var climber = battle.Enemies.OfType<SiegeAttackerBrain>().First(enemy => enemy.gameObject.activeSelf && enemy.IsCarryingLadder);
            SiegeLadder ladder = climber.CarriedLadder;
            Assert.IsTrue(ladder.PlaceAt(slot));
            Assert.IsTrue(ladder.HasWall);
            Assert.AreEqual(3, ladder.Storeys);
            ladder.State.Tick(0.1f, true, false, false);
            ladder.State.Tick(ladder.RaiseSeconds, true, false, false);
            Assert.AreEqual(LadderPhase.Up, ladder.State.Phase);
            Assert.IsTrue(climber.GetComponent<NavMeshAgent>().Warp(ladder.BasePoint));
            float ground = world.transform.position.y;
            for (int i = 0; i < 1200 && climber.transform.position.y - ground < wall.Height * 3f - 0.2f; i++) yield return null;
            Assert.Greater(climber.transform.position.y - ground, wall.Height * 3f - 0.2f);
        }

        [UnityTest]
        public IEnumerator 사다리_적은_저마다_사다리를_들고_나와_느리게_걸어_빈틈에_건다()
        {
            int ladderEnemies = battle.Enemies.Count(enemy => enemy.Role == SiegeAttackerRole.Ladder);
            Assert.Greater(ladderEnemies, 0);
            Assert.AreEqual(ladderEnemies, battle.Ladders.Count, "사다리 적마다 사다리 하나(SIEGE-37).");
            Assert.IsTrue(battle.Ladders.All(l => !l.gameObject.activeSelf), "전투 전에는 사다리를 깔아 두지 않는다.");
            yield return Navigation();
            Assert.IsTrue(battle.TryStartBattle());
            yield return null; yield return null;
            var carrier = battle.Enemies.OfType<SiegeAttackerBrain>().First(enemy => enemy.gameObject.activeSelf && enemy.IsCarryingLadder);
            SiegeLadder ladder = carrier.CarriedLadder;
            Assert.IsTrue(ladder.gameObject.activeSelf && ladder.IsLoose && ladder.Carrier == carrier);
            Assert.IsFalse(ladder.HasWall, "들고 가는 사다리는 아직 성벽에 걸리지 않았다.");
            float carryingSpeed = carrier.Actor.Stats.GetValue(ProjectNeo.Stats.StatType.MoveSpeed);

            // 90m 걷는 시간을 줄이려고 성벽 앞 들판에서 시작한다(출현 들판과 성벽 앞은 이어져 있다, BUG-approach-navmesh-gap).
            Assert.IsTrue(carrier.GetComponent<NavMeshAgent>().Warp(InFrontOfTheWall()), "성벽 앞 들판에 길찾기 메시가 있어야 한다.");
            // 고른 자리 가까이로 옮기면 사다리를 그 빈틈에 내려놓는다.
            LadderSlot slot = default;
            for (int i = 0; i < 120 && !carrier.TryGetCarrySlot(out slot); i++) yield return null;
            Assert.IsTrue(carrier.TryGetCarrySlot(out slot), "운반병이 걸 자리를 골라야 한다. " + CarrierReport(carrier));
            Assert.AreEqual(battle.Settings.Outward, slot.Outward, "분지 성벽은 북쪽 바깥 면에만 건다.");
            Assert.AreEqual(BuildPieceKind.Wall, world.Layout.Top(slot.Cell).Definition.Kind);
            Assert.IsTrue(carrier.GetComponent<NavMeshAgent>().Warp(slot.BasePoint));
            for (int i = 0; i < 120 && ladder.IsLoose; i++) yield return null;
            Assert.IsFalse(ladder.IsLoose, "자리에 닿으면 사다리를 내려놓는다.");
            Assert.IsTrue(ladder.HasWall);
            Assert.AreEqual(slot.Cell, ladder.Cell);
            float walkingSpeed = carrier.Actor.Stats.GetValue(ProjectNeo.Stats.StatType.MoveSpeed);
            Assert.AreEqual(battle.Settings.Siege.LadderCarrySpeedMultiplier, carryingSpeed / walkingSpeed, 0.02f,
                "들고 가는 동안 0.7배로 걷고, 내려놓으면 제 속도로 돌아온다.");
            for (int i = 0; i < 300 && ladder.State.Phase == LadderPhase.Down; i++) yield return null;
            Assert.AreNotEqual(LadderPhase.Down, ladder.State.Phase, "내려놓은 운반병이 바로 세우기 시작한다.");
        }

        [UnityTest]
        public IEnumerator 운반병이_죽으면_사다리가_떨어지고_다른_사다리_적이_주워_든다()
        {
            yield return Navigation();
            Assert.IsTrue(battle.TryStartBattle());
            yield return null; yield return null;
            var carriers = battle.Enemies.OfType<SiegeAttackerBrain>()
                .Where(enemy => enemy.gameObject.activeSelf && enemy.IsCarryingLadder).Take(2).ToList();
            Assert.AreEqual(2, carriers.Count, "첫 웨이브에 사다리 적이 둘 이상 있어야 한다.");
            SiegeLadder dropped = carriers[0].CarriedLadder;
            Kill(carriers[0].Actor);
            yield return null;
            Assert.IsTrue(dropped.IsLoose, "떨어진 사다리는 아직 성벽에 걸리지 않았다.");
            Assert.IsNull(dropped.Carrier);
            Assert.IsTrue(dropped.IsFreeToPickUp(carriers[1]));

            // 다른 사다리 적이 자기 사다리를 내려놓은 뒤 가까이 있으면 떨어진 사다리를 주워 든다.
            SiegeAttackerBrain picker = carriers[1];
            SiegeLadder own = picker.CarriedLadder;
            Assert.IsTrue(own.PlaceAt(FlatNorthSlot()));
            Assert.IsTrue(picker.GetComponent<NavMeshAgent>().Warp(dropped.LoosePosition));
            for (int i = 0; i < 300 && dropped.Carrier != picker; i++) yield return null;
            Assert.AreEqual(picker, dropped.Carrier, "가까운 사다리 적이 떨어진 사다리를 주워 든다.");
            Assert.IsTrue(picker.IsCarryingLadder);
            Assert.AreEqual(dropped, picker.CarriedLadder);
        }

        [UnityTest]
        public IEnumerator 성문이_부서져도_위_성벽은_그대로_있고_성문_파괴병은_아치를_치지_않고_진입한다()
        {
            var gate = world.Structures.Single(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate);
            var wall = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).Placement.Definition;
            // 기본 성문은 고정 성벽 한 층을 얹은 2층 문루다(SIEGE-18). 옆 칸도 2층으로 올린다.
            PlacedStructure left = world.Structures.Single(piece => piece.Placement.Cell == gate.Placement.Cell && piece.Placement.Level == 1);
            Assert.IsTrue(world.TryPlace(wall, gate.Placement.Cell + Vector2Int.right, 0, out PlacedStructure right));
            yield return Navigation();
            Assert.IsTrue(battle.TryStartBattle());
            var breaker = battle.Enemies.First(enemy => enemy.Role == SiegeAttackerRole.GateBreaker);
            foreach (var enemy in battle.Enemies) if (enemy != breaker) Kill(enemy.Actor);
            Vector3 position = gate.transform.position + Vector3.forward * 3f;
            gate.ReceiveDamage(gate.Health.Current);
            yield return Navigation();
            Assert.AreEqual(1, left.Placement.Level); Assert.AreEqual(1, right.Placement.Level);
            Assert.IsTrue(breaker.GetComponent<NavMeshAgent>().Warp(position));
            Assert.IsTrue(breaker.CanReachAnyEnemy(), "문짝이 부서지면 성문 파괴병이 성 내부 아군에게 도달할 수 있어야 한다.");
            float total = left.Health.Current + right.Health.Current;
            for (int i = 0; i < 120 && breaker.transform.position.z > gate.transform.position.z - 0.5f; i++) yield return null;
            Assert.AreEqual(total, left.Health.Current + right.Health.Current, "아치와 위층 성벽은 추가 공격받지 않아야 한다.");
            Assert.Less(breaker.transform.position.z, gate.transform.position.z, "성문 파괴병이 열린 성문 아래로 진입해야 한다.");
        }

        [UnityTest]
        public IEnumerator 런_데이터가_씬_편성으로_만들어지고_전투_결과를_반영하며_다음_준비에_생존자를_회복한다()
        {
            RunState state = GameSession.Flow.Run.State;
            Assert.IsNotNull(state, "첫 준비에서 씬 편성으로 런 데이터를 붙여야 한다.");
            var center = Object.FindAnyObjectByType<SquadCommandCenter>();
            int soldiers = center.Squads.Sum(squad => squad.Members.Count);
            Assert.AreEqual(soldiers, state.Soldiers.Count);
            Assert.AreEqual(center.Squads.Count, state.Army.Squads.Count);
            Assert.AreEqual(state.Player.Id, battle.RunLink.IdOf(player));
            Assert.AreEqual(world.Structures.Count, state.Castle.Pieces.Count);

            var fallen = center.Squads[0].Members[0].Actor;
            var wounded = center.Squads[0].Members[1].Actor;
            int fallenId = battle.RunLink.IdOf(fallen);
            Assert.Greater(fallenId, 0);
            Assert.IsTrue(battle.TryStartBattle());
            Assert.IsNotNull(battle.RunLink.CurrentInput);
            Assert.AreEqual(1, battle.RunLink.CurrentInput.BattleNumber);

            var wall = world.Structures.First(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall);
            wall.ReceiveDamage(15f);
            Kill(fallen);
            wounded.ReceiveDamage(10f);
            player.ReceiveDamage(5f);
            string ladderName = battle.Enemies.First(enemy => enemy.Role == SiegeAttackerRole.Ladder).name;
            string ladderId = ladderName.Substring(0, ladderName.LastIndexOf('_'));
            int killed = battle.Enemies.Count;
            foreach (var enemy in battle.Enemies) Kill(enemy.Actor);
            yield return null; yield return null;

            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
            Assert.AreEqual(1, state.BattlesFought);
            BattleResult result = state.LastBattleResult;
            Assert.AreEqual(BattleOutcome.Victory, result.Outcome);
            Assert.AreEqual(BattleEndReason.EnemiesDefeated, result.EndReason);
            Assert.AreEqual(killed, result.EnemiesKilled.Values.Sum());
            Assert.IsTrue(result.EnemiesKilled.ContainsKey(ladderId), "적 프리팹 이름으로 종류별 처치 수를 센다.");
            Assert.IsNull(state.Find(fallenId), "죽은 잡졸은 런에서 빠진다.");
            Assert.AreEqual(soldiers - 1, state.Soldiers.Count);
            Assert.IsTrue(state.Castle.Pieces.Any(piece => piece.Health < piece.Definition.MaxHealth), "성벽 피해를 기록한다.");

            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return Navigation();
            Assert.AreEqual(wounded.Health.Maximum, wounded.Health.Current, "살아남은 잡졸은 다음 준비에 회복한다.");
            Assert.AreEqual(player.Health.Maximum, player.Health.Current);
            Assert.IsTrue(fallen.Health.IsDead, "죽은 유닛은 되살리지 않는다.");
            Assert.IsNotNull(fallen, "죽은 유닛의 몸은 씬에 남긴다.");
            Assert.Less(wall.Health.Current, wall.Health.Maximum, "성벽 피해는 회복하지 않는다.");

            Assert.IsTrue(battle.TryStartBattle());
            Assert.AreEqual(2, battle.RunLink.CurrentInput.BattleNumber);
            Assert.AreEqual(soldiers - 1, battle.RunLink.CurrentInput.Squads.Sum(squad => squad.Members.Count));
        }

        [UnityTest]
        public IEnumerator 승리하면_보상_표대로_골드_재료_메타를_한_번_지급하고_보상_화면과_다음_준비에_보인다()
        {
            var rewards = battle.GetComponent<GameRewardController>();
            Assert.IsNotNull(rewards, "Game 씬의 전투 오브젝트에 보상 컴포넌트가 있어야 한다.");
            Assert.IsNotNull(rewards.Table);
            RunState state = GameSession.Flow.Run.State;
            int ladders = battle.Enemies.Count(enemy => enemy.Role == SiegeAttackerRole.Ladder);
            int goldBefore = state.Wallet.Gold; // 시작 골드(D-2)가 먼저 들어와 있을 수 있다.
            Assert.IsTrue(battle.TryStartBattle());
            foreach (var enemy in battle.Enemies) Kill(enemy.Actor);
            yield return null; yield return null;

            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
            StageReward reward = rewards.LastReward;
            Assert.IsNotNull(reward);
            Assert.AreEqual(1, reward.BattleNumber);
            Assert.AreEqual(goldBefore + reward.Gold, state.Wallet.Gold);
            Assert.Greater(reward.Gold, rewards.Table.BaseGold, "처치 골드가 더해진다.");
            // 처치 재료는 적 종류(CONTENT-04)에 있다.
            Stages.EnemyMaterialDrop ladderDrop = rewards.Table.Enemies.Find("enemy_ladder").MaterialDrops[0];
            Assert.AreEqual(ladders * ladderDrop.amountPerKill, state.Wallet.GetMaterial(ladderDrop.material));
            Assert.AreEqual(0, GameSession.Meta.unlockPoints, "해금 포인트는 승리마다 주지 않고 런이 끝날 때 준다(META-08).");
            Assert.IsFalse(rewards.TryGrant(), "같은 승리의 보상은 다시 주지 않는다.");
            Assert.AreEqual(goldBefore + reward.Gold, state.Wallet.Gold);

            yield return null;
            var hud = Object.FindAnyObjectByType<GameFlowHud>();
            Transform status = hud.transform.Find("BattleStatus");
            Assert.IsTrue(status.gameObject.activeSelf, "보상 화면에 지급 내역을 보여준다.");
            StringAssert.Contains(reward.Gold.ToString(), hud.StatusText);

            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return null;
            StringAssert.Contains(state.Wallet.Gold.ToString(), hud.PhaseText, "준비 제목에 보유 골드가 보인다.");
        }
    }
}
