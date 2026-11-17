using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Combat;
using ProjectNeo.Core;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using ProjectNeo.Morale;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using ProjectNeo.UI;
using ProjectNeo.Facilities;
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

        sealed class EffectRandom : IRandomSource
        {
            readonly int effect;
            public float Roll = 0.99f;
            public int FloatCalls;
            public EffectRandom(int effect) { this.effect = effect; }
            public int NextInt(int maxExclusive) => effect % maxExclusive;
            public float NextFloat() { FloatCalls++; return Roll; }
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

        IEnumerator WinBattle()
        {
            foreach (var enemy in battle.Enemies) Kill(enemy.Actor);
            for (int i = 0; i < 10 && GameSession.Flow.Phase == GamePhase.Battle; i++) yield return null;
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
        }

        [UnityTest]
        public IEnumerator StartedShaken_FearIsWeaker_AndCaptainSharesPanicAndAttackEffects()
        {
            var facilities = Object.FindAnyObjectByType<GameFacilities>();
            GameSession.Flow.Run.State.Wallet.AddGold(200);
            Assert.IsTrue(Object.FindObjectsByType<FacilityStation>().Single(station => station.Type == FacilityType.Tavern).Interact());
            Assert.IsTrue(facilities.ChooseCaptainSquad(0));
            Assert.IsTrue(facilities.TryHire(0));
            facilities.Close();
            yield return null;
            var captain = center.Squads[0].Leader;
            Assert.IsNotNull(captain);
            morale.MoraleOf(0).Add(-70f);
            var random = new EffectRandom(0);
            morale.UseRandom(random);
            Assert.IsTrue(battle.TryStartBattle());
            var rules = morale.BattleOf(0);
            Assert.IsTrue(rules.StartedShaken);
            Assert.That(captain.Actor.Stats.GetValue(StatType.Attack),
                Is.EqualTo(captain.Actor.Stats.GetBase(StatType.Attack) * 0.9f).Within(0.001f));
            center.Commander.Select(0);
            random.Roll = 0.3f; // 약한 공포는 25%, 일반 공포는 50%.
            Assert.AreEqual(1, center.Commander.Issue(new SquadOrder(SquadOrderKind.Attack)));
            random.Roll = 0.2f;
            Assert.AreEqual(0, center.Commander.Issue(new SquadOrder(SquadOrderKind.Hold)));
            yield return WinBattle();
            Assert.That(captain.Actor.Stats.GetValue(StatType.Attack),
                Is.EqualTo(captain.Actor.Stats.GetBase(StatType.Attack)).Within(0.001f));
            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return null;
            morale.UseRandom(new EffectRandom(1));
            Assert.IsTrue(battle.TryStartBattle());
            rules = morale.BattleOf(0); // 전투 후 100으로 회복했으므로 한계 판정을 새로 할 수 있다.
            rules.Morale.Add(-1f);
            rules.Tick(morale.Settings.PanicInterval + 0.01f);
            Assert.IsTrue(morale.IsPanicking(captain));
            Assert.IsTrue(center.IsDetached(captain), "대장도 부대원과 함께 명령에서 분리된다.");
            Assert.AreEqual(0, center.Commander.Issue(new SquadOrder(SquadOrderKind.Attack)));
        }

        [UnityTest]
        public IEnumerator RallyRaisesSelfAndNearbySquad_AndSteadfastAtZeroDoesNotRout()
        {
            morale.UseRandom(new EffectRandom(0) { Roll = 0f });
            Assert.IsTrue(battle.TryStartBattle());
            Vector3 origin = AliveMembers(0)[0].transform.position;
            foreach (var unit in AliveMembers(1)) unit.GetComponent<NavMeshAgent>().Warp(origin);
            var rules = morale.BattleOf(0);
            float nearBefore = morale.MoraleOf(1).Value;
            rules.Morale.Add(-50f);
            Assert.AreEqual(AwakenEffect.Rally, rules.Awaken);
            Assert.AreEqual(100f + morale.Settings.RallyAmount, rules.Morale.Value, 0.01f);
            Assert.AreEqual(nearBefore + morale.Settings.RallyAmount, morale.MoraleOf(1).Value, 0.01f);
            yield return WinBattle();
            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return null;
            morale.UseRandom(new EffectRandom(2) { Roll = 0f });
            Assert.IsTrue(battle.TryStartBattle());
            rules = morale.BattleOf(0);
            rules.Morale.Add(-1000f);
            Assert.AreEqual(AwakenEffect.Steadfast, rules.Awaken);
            Assert.IsFalse(rules.IsRouted);
            foreach (var unit in AliveMembers(0))
            {
                Assert.IsFalse(morale.IsRouting(unit));
                Assert.IsFalse(morale.HasFled(unit.Actor));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AttackBands_UpdateImmediately_AndRemoveOnlyMoraleAtBattleEnd()
        {
            var actor = AliveMembers(0)[0].Actor;
            float attack = actor.Stats.GetBase(StatType.Attack);
            object equipment = new object();
            actor.Stats.AddModifier(StatModifier.Percent(StatType.Attack, 0.2f), equipment);
            Assert.IsTrue(battle.TryStartBattle());
            var enemy = battle.Enemies[0].Actor;
            float enemyAttack = enemy.Stats.GetValue(StatType.Attack);
            Assert.That(actor.Stats.GetValue(StatType.Attack), Is.EqualTo(attack * 1.3f).Within(0.001f));
            var rules = morale.BattleOf(0);
            rules.Morale.Add(-60f);
            Assert.That(actor.Stats.GetValue(StatType.Attack), Is.EqualTo(attack * 1.1f).Within(0.001f));
            rules.Morale.Add(80f);
            Assert.That(actor.Stats.GetValue(StatType.Attack), Is.EqualTo(attack * 1.3f).Within(0.001f));
            yield return null;
            Assert.AreEqual(enemyAttack, enemy.Stats.GetValue(StatType.Attack), "적에게 사기 보정이 적용되지 않는다.");
            Assert.That(actor.Stats.GetValue(StatType.Attack), Is.EqualTo(attack * 1.3f).Within(0.001f), "매 프레임 보정이 누적되면 안 된다.");
            yield return WinBattle();
            Assert.That(actor.Stats.GetValue(StatType.Attack), Is.EqualTo(attack * 1.2f).Within(0.001f));
            Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
            yield return null;
            Assert.IsTrue(battle.TryStartBattle());
            Assert.That(actor.Stats.GetValue(StatType.Attack), Is.EqualTo(attack * 1.3f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator LastStand_UsesCasualtyShareInRealActorStats()
        {
            morale.UseRandom(new EffectRandom(1) { Roll = 0f });
            Assert.IsTrue(battle.TryStartBattle());
            var rules = morale.BattleOf(0);
            var members = AliveMembers(0);
            var survivor = members[0].Actor;
            float attack = survivor.Stats.GetBase(StatType.Attack);
            rules.Morale.Add(-50f);
            Assert.AreEqual(AwakenEffect.LastStand, rules.Awaken);
            Kill(members[1].Actor);
            Assert.That(survivor.Stats.GetValue(StatType.Attack), Is.EqualTo(attack * rules.AttackMultiplier).Within(0.001f));
            Assert.That(rules.LostShare, Is.EqualTo(1f / rules.StartingMembers).Within(0.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fear_RejectsEachSquadOrderOnce_PreservingOldOrderAndOtherSquads()
        {
            var random = new EffectRandom(0);
            morale.UseRandom(random);
            Assert.IsTrue(battle.TryStartBattle());
            morale.BattleOf(0).Morale.Add(-60f);
            random.Roll = 0f;
            int rolls = random.FloatCalls;
            var old = center.Commander.OrderOf(0);
            center.Commander.SelectAll();
            Assert.AreEqual(center.Squads.Count - 1, center.Commander.Issue(new SquadOrder(SquadOrderKind.Attack)));
            Assert.AreEqual(rolls + 1, random.FloatCalls);
            Assert.AreEqual(old.Kind, center.Commander.OrderOf(0).Kind);
            foreach (var member in AliveMembers(0)) Assert.AreEqual(UnitDirective.Hold, member.Directive);
            StringAssert.Contains("Order ignored", morale.NoticeOf(0));
            center.Commander.Select(0);
            random.Roll = 0.99f;
            Assert.AreEqual(1, center.Commander.Issue(new SquadOrder(SquadOrderKind.Move, AliveMembers(0)[0].transform.position)));
            rolls = random.FloatCalls;
            Assert.IsTrue(center.Commander.CompleteMove(0));
            Assert.AreEqual(rolls, random.FloatCalls);
            yield return WinBattle();
            Assert.AreEqual(1, center.Commander.Issue(new SquadOrder(SquadOrderKind.Attack)), "전투 종료 후 공포 지시 심사가 남지 않는다.");
        }

        [UnityTest]
        public IEnumerator Panic_WaitsAtPointUntilBattleEnd_StaysAliveAndDoesNotFlee()
        {
            morale.UseRandom(new EffectRandom(1));
            Assert.IsTrue(battle.TryStartBattle());
            var rules = morale.BattleOf(0);
            rules.Morale.Add(-60f);
            int alive = battle.AlliesAlive;
            var members = AliveMembers(0);
            rules.Tick(morale.Settings.PanicInterval + 0.01f);
            Assert.AreEqual(CollapseEffect.Panic, rules.Collapse);
            Assert.IsNotNull(morale.transform.Find("PanicRetreatPoint"));
            foreach (var unit in members)
            {
                Assert.IsTrue(morale.IsPanicking(unit));
                Assert.IsTrue(center.IsDetached(unit));
                Assert.That(Vector3.Distance(unit.MoveDestination, morale.PanicRetreatPoint), Is.LessThan(0.01f));
                Assert.IsTrue(unit.GetComponent<NavMeshAgent>().Warp(morale.PanicRetreatPoint));
            }
            yield return null; yield return null;
            center.Commander.Select(0);
            Assert.AreEqual(0, center.Commander.Issue(new SquadOrder(SquadOrderKind.Attack)));
            rules.Tick(morale.Settings.PanicInterval * 2f);
            foreach (var unit in members)
            {
                Assert.AreEqual(UnitDirective.Hold, unit.Directive);
                Assert.IsTrue(unit.gameObject.activeSelf);
                Assert.IsFalse(morale.HasFled(unit.Actor));
            }
            Assert.AreEqual(alive, battle.AlliesAlive);
            yield return WinBattle();
            foreach (var unit in members)
            {
                Assert.IsFalse(center.IsDetached(unit));
                Assert.IsFalse(morale.IsPanicking(unit));
                Assert.AreEqual(UnitFate.Alive, GameSession.Flow.Run.State.LastBattleResult.FateOf(battle.RunLink.IdOf(unit.Actor)));
            }
        }

        [UnityTest]
        public IEnumerator Panic_AtZeroMoraleEscalatesToRout()
        {
            morale.UseRandom(new EffectRandom(1));
            Assert.IsTrue(battle.TryStartBattle());
            var rules = morale.BattleOf(0);
            rules.Morale.Add(-60f);
            rules.Tick(morale.Settings.PanicInterval + 0.01f);
            var unit = AliveMembers(0)[0];
            rules.Morale.Add(-1000f);
            Assert.IsFalse(morale.IsPanicking(unit));
            Assert.IsTrue(morale.IsRouting(unit));
            Assert.IsTrue(morale.HasFled(unit.Actor));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContagionTicksInScene_OnlyNearbyAlliesLoseMorale_AndHudShowsCheck()
        {
            morale.UseRandom(new EffectRandom(2));
            Assert.IsTrue(battle.TryStartBattle());
            // 실시간 Tick을 빠르게 검증하되 공유 에셋은 바꾸지 않는다.
            var settings = morale.Settings;
            var rules = morale.BattleOf(0);
            rules.Morale.Add(-60f);
            Vector3 origin = AliveMembers(0)[0].transform.position;
            foreach (var unit in AliveMembers(1)) unit.GetComponent<NavMeshAgent>().Warp(origin);
            int farSquad = center.Squads.Count - 1;
            Vector3 distant = origin + Vector3.right * (settings.NearbyRadius + 20f);
            Assert.IsTrue(NavMesh.SamplePosition(distant, out var hit, 10f, NavMesh.AllAreas));
            foreach (var unit in AliveMembers(farSquad)) unit.GetComponent<NavMeshAgent>().Warp(hit.position);
            float nearBefore = morale.MoraleOf(1).Value, farBefore = morale.MoraleOf(farSquad).Value;
            for (int i = 0; i < Mathf.CeilToInt(settings.ContagionInterval * 30f) + 4; i++) yield return null;
            Assert.AreEqual(nearBefore - settings.ContagionAmount, morale.MoraleOf(1).Value, 0.01f);
            Assert.AreEqual(farBefore, morale.MoraleOf(farSquad).Value, 0.01f);
            var hud = Object.FindAnyObjectByType<SquadCommandHud>();
            StringAssert.Contains("Morale", hud.DisplayText);
            StringAssert.Contains("Contagion", hud.DisplayText);
            StringAssert.Contains("Limit check", morale.NoticeOf(0));
        }

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

        // --- COMBAT-07: 퍽·스킬·부대장 사기 연결 ---

        [UnityTest]
        public IEnumerator BattleStartPerk_RaisesEverySquadOnce_SkipsRouted_AndStopsAfterBattle()
        {
            MoraleBoostPerkEffect effect = MoraleBoostPerkEffect.Create(PerkTrigger.BattleStart, 10f);
            PerkDefinition perk = PerkDefinition.Create("combat07_morale_boost", PerkCategory.UnitBoost, null,
                new PerkSpecialEffect[] { effect });
            try
            {
                var loadout = new PerkLoadout(new MetaProgress { perkPoints = 1 });
                Assert.IsTrue(loadout.TryAllocate(perk));
                morale.UsePerks(loadout);
                Assert.GreaterOrEqual(center.Squads.Count, 2);
                float[] before = Enumerable.Range(0, center.Squads.Count).Select(i => morale.MoraleOf(i).Value).ToArray();

                Assert.IsTrue(battle.TryStartBattle());
                for (int i = 0; i < center.Squads.Count; i++)
                    if (morale.BattleOf(i) != null)
                        Assert.AreEqual(Mathf.Min(before[i] + 10f, 200f), morale.BattleOf(i).Morale.Value, 0.01f, $"{i}번 부대");
                float[] started = Values();
                yield return null; yield return null;
                CollectionAssert.AreEqual(started, Values(), "전투 시작 퍽은 한 번만 발동한다.");

                // 도망 중인 부대는 오르지 않고, 나머지는 오른다.
                BattleSquadMorale routed = morale.BattleOf(0);
                routed.Morale.Add(-1000f);
                Assert.IsTrue(routed.IsRouted);
                float[] beforeSkill = Values();
                morale.AddMoraleToAllySquads(5f);
                Assert.AreEqual(0f, routed.Morale.Value);
                Assert.IsTrue(routed.IsRouted);
                for (int i = 1; i < center.Squads.Count; i++)
                    if (morale.BattleOf(i) != null)
                        Assert.AreEqual(Mathf.Min(beforeSkill[i] + 5f, 200f), Values()[i], 0.01f, $"{i}번 부대");
                Assert.AreEqual(0f, morale.AddMorale(0, 5f));

                yield return WinBattle();
                float[] recovered = Enumerable.Range(0, center.Squads.Count).Select(i => morale.MoraleOf(i).Value).ToArray();
                morale.AddMoraleToAllySquads(50f);
                loadout.RaiseTrigger(PerkTrigger.BattleStart, morale);
                CollectionAssert.AreEqual(recovered,
                    Enumerable.Range(0, center.Squads.Count).Select(i => morale.MoraleOf(i).Value).ToArray(),
                    "전투가 끝나면 퍽·스킬 창구는 아무것도 바꾸지 않는다.");

                Assert.IsTrue(GameSession.Flow.ContinueToNextStage());
                yield return null;
                Assert.IsTrue(battle.TryStartBattle());
                for (int i = 0; i < center.Squads.Count; i++)
                    if (morale.BattleOf(i) != null)
                        Assert.AreEqual(Mathf.Min(recovered[i] + 10f, 200f), morale.BattleOf(i).Morale.Value, 0.01f,
                            $"{i}번 부대: 다음 전투에서도 정확히 한 번(중복 구독 없음)");
            }
            finally
            {
                morale.UsePerks(null);
                Object.DestroyImmediate(perk);
                Object.DestroyImmediate(effect);
            }
        }

        [UnityTest]
        public IEnumerator CaptainAwakenBonus_AppliesOnlyToSquadsWithLivingCaptain()
        {
            var facilities = Object.FindAnyObjectByType<GameFacilities>();
            GameSession.Flow.Run.State.Wallet.AddGold(200);
            Assert.IsTrue(Object.FindObjectsByType<FacilityStation>().Single(station => station.Type == FacilityType.Tavern).Interact());
            Assert.IsTrue(facilities.ChooseCaptainSquad(0));
            Assert.IsTrue(facilities.TryHire(0));
            facilities.Close();
            yield return null;
            UnitCombatAI captain = center.Squads[0].Leader;
            Assert.IsNotNull(captain);
            UnitCombatAI asked = null;
            morale.UseCaptainAwakenBonus(leader => { asked = leader; return 0.5f; });
            try
            {
                Assert.IsTrue(battle.TryStartBattle());
                Assert.AreEqual(captain, asked);
                Assert.AreEqual(0.5f, morale.BattleOf(0).AwakenChanceBonus, 0.001f);
                for (int i = 1; i < center.Squads.Count; i++)
                    if (morale.BattleOf(i) != null && center.Squads[i].Leader == null)
                        Assert.AreEqual(0f, morale.BattleOf(i).AwakenChanceBonus);
            }
            finally
            {
                morale.UseCaptainAwakenBonus(null);
            }
        }
    }
}
