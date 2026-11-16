using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// FLOW-02 저장된 Title·Game 씬으로 런 저장과 이어 하기를 확인한다: 저장 시점, 타이틀의 이어 하기·새 게임 확인·슬롯 삭제,
    /// 성·부대·영웅·장비·시설을 씬에 되살리기, 패배 때 지우기, 되살릴 수 없는 저장 처리. 실제 저장 폴더는 쓰지 않는다.
    /// </summary>
    public class GameRunSavePlayTests
    {
        static int cleanup;
        string saveDirectory;
        float capture;
        GameBattleController battle;
        ConstructionWorld world;
        SquadCommandCenter commander;
        CharacterActor player;

        [SetUp]
        public void SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            saveDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ProjectNeoRunSavePlayTests_" + System.Guid.NewGuid().ToString("N"));
            GameSession.UseSaveStore(new MetaSaveStore(saveDirectory));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameRunSaveCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset();
            GameSession.UseSaveStore(null);
            Time.captureDeltaTime = capture;
            if (System.IO.Directory.Exists(saveDirectory)) System.IO.Directory.Delete(saveDirectory, true);
        }

        static IEnumerator WaitForActiveScene(string name)
        {
            for (int frame = 0; frame < 300 && SceneManager.GetActiveScene().name != name; frame++) yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
            yield return null;
        }

        IEnumerator OpenTitle()
        {
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            yield return null;
        }

        // 게임을 껐다 켠 것처럼: 흐름을 처음으로 돌리고(저장 폴더는 그대로) 타이틀을 다시 연다.
        IEnumerator QuitToTitle()
        {
            GameSession.Reset();
            yield return OpenTitle();
        }

        IEnumerator EnterGame(System.Action<TitleScreen> press)
        {
            press(Object.FindAnyObjectByType<TitleScreen>());
            yield return WaitForActiveScene("Game");
            battle = Object.FindAnyObjectByType<GameBattleController>();
            world = Object.FindAnyObjectByType<ConstructionWorld>();
            commander = Object.FindAnyObjectByType<SquadCommandCenter>();
            player = Object.FindAnyObjectByType<PlayerInputController>().GetComponent<CharacterActor>();
            yield return null;
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            yield return null;
        }

        IEnumerator NewGame(int slot) => EnterGame(title => { title.SelectSlot(slot); title.StartGame(); });
        IEnumerator Continue(int slot) => EnterGame(title => { title.SelectSlot(slot); title.ContinueGame(); });

        static void Kill(CharacterActor actor) => actor.ReceiveDamage(actor.Health.Current + 1f);
        RunState State => battle.RunLink.State;
        RunContentCatalog Content => battle.Settings.RunContent;
        int[] SquadSizes() => commander.Squads.Select(squad => squad.Members.Count).ToArray();

        void AssertSceneMatchesRun()
        {
            Assert.AreEqual(State.Army.Squads.Count, commander.Squads.Count, "scene and run squads stay aligned");
            for (int i = 0; i < commander.Squads.Count; i++)
            {
                Assert.AreEqual(State.Army.Squads[i].Members.Count, commander.Squads[i].Members.Count, "squad " + i);
                foreach (UnitCombatAI member in commander.Squads[i].Members)
                {
                    int id = battle.RunLink.IdOf(member.GetComponent<CharacterActor>());
                    Assert.IsTrue(State.Army.Squads[i].Members.Any(unit => unit.Id == id), "every scene soldier is bound to a soldier of the same run squad");
                }
                Assert.AreEqual(State.Army.Squads[i].Leader != null, commander.Squads[i].Leader != null, "captain of squad " + i);
            }
            Assert.AreEqual(State.Player.Id, battle.RunLink.IdOf(player));
        }

        [UnityTest]
        public IEnumerator 준비에서_한_일이_전투_시작_때_저장되고_전투_중에_꺼도_이어_하기로_그_준비_상태가_씬에_돌아온다()
        {
            yield return OpenTitle();
            var title = Object.FindAnyObjectByType<TitleScreen>();
            Assert.IsNotNull(title.transform.Find("Panel/Button_Continue"), "Title 씬에 이어 하기 버튼이 있어야 한다.");
            title.SelectSlot(0);
            Assert.IsFalse(title.CanContinue);

            yield return NewGame(0);
            Assert.IsFalse(battle.RunLink.WasRestored);
            Assert.IsTrue(GameSession.HasRunSave(0), "새 런은 첫 준비에 들어오면 저장된다.");
            Assert.AreEqual(1, GameSession.RunSaves.Inspect(0).Snapshot.stageNumber);

            // 준비에서 한 일: 골드, 모집, 장비 착용, 시설 업그레이드, 성벽 피해와 철거.
            State.Wallet.AddGold(900);
            var spawner = battle.GetComponent<GameUnitSpawner>();
            Assert.IsNotNull(spawner.Recruit(spawner.DebugSoldier, FacilityType.TrainingGround));
            ItemDefinition equipment = Content.Items.First(item => item.Kind == ItemKind.Equipment && item.EquipModifiers.Count > 0);
            Assert.IsTrue(State.TryEquip(State.Player, State.Inventory.Add(equipment)));
            Assert.IsNotNull(battle.GetComponent<GameFacilities>().Facility(FacilityType.Shop));
            Assert.IsTrue(State.TryUpgradeFacility(FacilityType.Shop));
            PlacedStructure[] walls = world.Structures.Where(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall).ToArray();
            PlacedStructure damaged = walls[0];
            damaged.ReceiveDamage(30f);
            Vector2Int damagedCell = damaged.Placement.Cell;
            int damagedLevel = damaged.Placement.Level;
            float damagedHealth = damaged.Health.Current;
            Assert.IsTrue(walls.Skip(1).Any(piece => world.TryDismantle(piece)), "철거할 수 있는 성벽이 하나는 있어야 한다.");
            // 철거로 길을 다시 만드는 동안에는 전투를 시작할 수 없다.
            yield return null;
            for (int i = 0; i < 600 && (world.IsRebuilding || world.RebuildPending); i++) yield return null;
            yield return null;

            int gold = State.Wallet.Gold;
            int[] sizes = SquadSizes();
            int pieces = world.Structures.Count;
            float attack = player.Stats.GetValue(StatType.Attack);
            int seed = State.Seed;

            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            // 전투 중에 생긴 일은 저장되지 않는다.
            Kill(commander.Squads[0].Members[0].Actor);
            State.Wallet.AddGold(5000);
            yield return null;

            yield return QuitToTitle();
            title = Object.FindAnyObjectByType<TitleScreen>();
            title.SelectSlot(0);
            Assert.IsTrue(title.CanContinue);
            StringAssert.Contains("1", title.ContinueText);
            StringAssert.Contains("1", title.SlotText(0));

            yield return Continue(0);
            Assert.IsTrue(battle.RunLink.WasRestored);
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
            Assert.AreEqual(1, GameSession.Flow.Run.StageNumber);
            Assert.AreSame(State, GameSession.Flow.Run.State);
            Assert.AreEqual(seed, State.Seed);
            Assert.AreEqual(gold, State.Wallet.Gold);
            CollectionAssert.AreEqual(sizes, SquadSizes(), "전투 직전의 편성으로 돌아온다(전투 중 사망은 없던 일이 된다).");
            AssertSceneMatchesRun();
            Assert.AreEqual(1, State.Player.Character.Equipment.Count);
            Assert.AreSame(player.Character, State.Player.Character, "씬 플레이어와 런 영웅이 같은 캐릭터를 쓴다.");
            Assert.AreEqual(attack, player.Stats.GetValue(StatType.Attack), 0.0001f);
            Assert.AreEqual(1, State.Facility(FacilityType.Shop).Level);
            Assert.AreEqual(pieces, world.Structures.Count, "철거한 성벽은 없는 채로 돌아온다.");
            PlacedStructure restoredWall = world.Structures.Single(piece =>
                piece.Placement.Cell == damagedCell && piece.Placement.Level == damagedLevel);
            Assert.AreEqual(damagedHealth, restoredWall.Health.Current, 0.01f);
            Assert.IsFalse(battle.GateDestroyed);
            foreach (Squad<UnitCombatAI> squad in commander.Squads)
                foreach (UnitCombatAI member in squad.Members)
                    Assert.IsFalse(member.Actor.Health.IsDead);

            Assert.Greater(battle.Enemies.Count, 0);
            Assert.IsTrue(battle.TryStartBattle(), "되살린 런으로 전투를 시작할 수 있다.");
            yield return null;
            Assert.AreEqual(GamePhase.Battle, GameSession.Flow.Phase);
            Assert.IsNotNull(battle.RunLink.CurrentInput);
        }

        [UnityTest]
        public IEnumerator 승리_보상을_받으면_다음_스테이지_준비로_저장되어_보상을_다시_받지_못하고_패배하면_저장이_지워진다()
        {
            yield return OpenTitle();
            yield return NewGame(1);
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            foreach (var enemy in battle.Enemies) Kill(enemy.Actor);
            yield return null; yield return null;
            Assert.AreEqual(GamePhase.Reward, GameSession.Flow.Phase);
            RunSnapshot saved = GameSession.RunSaves.Inspect(1).Snapshot;
            Assert.AreEqual(2, saved.stageNumber, "보상을 받은 런은 다음 스테이지 준비로 저장된다.");
            Assert.AreEqual(1, saved.battlesFought);
            Assert.AreEqual(1, saved.lastRewardedBattle);
            int gold = State.Wallet.Gold;
            int commanderXp = GameSession.Meta.commanderXp, unlockPoints = GameSession.Meta.unlockPoints, level = GameSession.Meta.commanderLevel;
            Assert.AreEqual(gold, saved.gold);

            yield return QuitToTitle();
            yield return Continue(1);
            Assert.IsTrue(battle.RunLink.WasRestored);
            Assert.AreEqual(2, GameSession.Flow.Run.StageNumber);
            Assert.AreEqual(GamePhase.Preparation, GameSession.Flow.Phase);
            Assert.AreEqual(gold, State.Wallet.Gold);
            Assert.AreEqual(1, State.BattlesFought);
            Assert.IsFalse(battle.GetComponent<GameRewardController>().TryGrant(), "같은 전투의 보상은 다시 받지 못한다.");
            Assert.AreEqual(commanderXp, GameSession.Meta.commanderXp);
            Assert.AreEqual(unlockPoints, GameSession.Meta.unlockPoints);
            Assert.AreEqual(level, GameSession.Meta.commanderLevel);
            AssertSceneMatchesRun();

            // 패배하면 이어 할 런이 없다.
            Assert.IsTrue(battle.TryStartBattle());
            yield return null;
            Kill(player);
            yield return null; yield return null;
            Assert.AreEqual(GamePhase.RunResult, GameSession.Flow.Phase);
            Assert.IsFalse(GameSession.HasRunSave(1));
        }

        [UnityTest]
        public IEnumerator 저장된_편성이_기본_씬과_달라도_부대원을_줄이고_부대를_없애고_고용한_대장과_부서진_성문을_되살린다()
        {
            yield return OpenTitle();
            yield return NewGame(2);
            int squadCount = commander.Squads.Count;
            int firstSquad = commander.Squads[0].Members.Count;
            Assert.GreaterOrEqual(squadCount, 2);
            Assert.GreaterOrEqual(firstSquad, 2);

            RunSnapshot edited = battle.RunLink.CaptureSnapshot();
            edited.stageNumber = 3;
            edited.squads[0].soldiers.RemoveAt(0);
            edited.squads.RemoveAt(edited.squads.Count - 1);
            HeroDefinition hire = Content.Heroes.First(hero => hero.UnitPrefab != null && hero != State.Player.Definition);
            int heroId = edited.nextUnitId++;
            edited.heroes.Add(new HeroRecord { unitId = heroId, id = hire.Id });
            edited.squads[0].leaderUnitId = heroId;
            edited.squads[0].hasMorale = true; edited.squads[0].moraleMax = 200f; edited.squads[0].morale = 80f;
            int gateIndex = edited.castle.pieces.FindIndex(piece => Content.Piece(piece.id).Kind == BuildPieceKind.Gate);
            CastlePieceRecord gate = edited.castle.pieces[gateIndex];
            gate.health = 0f;
            edited.castle.pieces[gateIndex] = gate;
            Assert.IsTrue(GameSession.SaveRun(edited));

            yield return QuitToTitle();
            var title = Object.FindAnyObjectByType<TitleScreen>();
            title.SelectSlot(2);
            StringAssert.Contains("3", title.ContinueText);
            // 저장된 런이 있으면 새 게임은 한 번 더 눌러야 한다.
            title.StartGame();
            Assert.IsTrue(title.IsNewGameArmed);
            Assert.AreEqual("Title", SceneManager.GetActiveScene().name);
            Assert.IsTrue(GameSession.HasRunSave(2), "확인 전에는 런 저장을 버리지 않는다.");
            title.SelectSlot(2);
            Assert.IsFalse(title.IsNewGameArmed);

            yield return Continue(2);
            Assert.IsTrue(battle.RunLink.WasRestored);
            Assert.AreEqual(3, GameSession.Flow.Run.StageNumber);
            Assert.AreEqual(squadCount - 1, commander.Squads.Count);
            Assert.AreEqual(firstSquad - 1, commander.Squads[0].Members.Count);
            AssertSceneMatchesRun();
            UnitCombatAI captain = commander.Squads[0].Leader;
            Assert.IsNotNull(captain);
            Assert.IsTrue(captain.gameObject.activeInHierarchy);
            Assert.AreSame(hire, captain.GetComponent<CharacterActor>().Character.Definition);
            Assert.AreEqual(heroId, battle.RunLink.IdOf(captain.GetComponent<CharacterActor>()));
            Assert.AreEqual(80f, State.Army.Squads[0].Morale.Value, 0.0001f);
            Assert.IsTrue(battle.GateDestroyed, "부서진 성문은 부서진 채로 돌아온다.");
            Assert.AreEqual(1, world.Structures.Count(piece => piece.Placement.Definition.Kind == BuildPieceKind.Gate), "성문 조각 자체는 남아 있다.");
            int sceneSoldiers = Object.FindObjectsByType<UnitCombatAI>(FindObjectsInactive.Exclude)
                .Count(unit => unit.gameObject.scene == battle.gameObject.scene && unit.GetComponent<CharacterActor>().Team == CombatTeam.Allied
                    && unit.GetComponent<CharacterActor>().SoldierDefinition != null);
            yield return null;
            Assert.AreEqual(State.Soldiers.Count, commander.Squads.Sum(squad => squad.Members.Count));
            Assert.LessOrEqual(State.Soldiers.Count, sceneSoldiers, "남는 기본 유닛은 씬에서 사라진다(이번 프레임에 지워지는 것 제외).");
            Assert.IsTrue(battle.TryStartBattle());
        }

        [UnityTest]
        public IEnumerator 되살릴_수_없는_런_저장은_따로_치우고_1스테이지의_새_런으로_시작한다()
        {
            yield return OpenTitle();
            yield return NewGame(0);
            RunSnapshot broken = battle.RunLink.CaptureSnapshot();
            broken.stageNumber = 4;
            broken.stash.Add(new ItemRecord { id = "item_that_was_removed" });
            Assert.IsTrue(GameSession.SaveRun(broken));
            int defaultSquads = commander.Squads.Count;

            yield return QuitToTitle();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("item_that_was_removed"));
            yield return Continue(0);
            Assert.IsFalse(battle.RunLink.WasRestored);
            Assert.IsNotNull(State);
            Assert.AreEqual(1, GameSession.Flow.Run.StageNumber);
            Assert.AreEqual(defaultSquads, commander.Squads.Count, "씬은 건드리지 않았다.");
            AssertSceneMatchesRun();
            Assert.AreEqual(1, System.IO.Directory.GetFiles(saveDirectory, "run_slot1.json.corrupt-*").Length, "되살릴 수 없는 저장은 지우지 않고 따로 둔다.");
            Assert.AreEqual(1, GameSession.RunSaves.Inspect(0).Snapshot.stageNumber, "새 런이 다시 저장된다.");
        }

        [UnityTest]
        public IEnumerator 타이틀에서_새_게임을_두_번_누르면_저장된_런을_버리고_슬롯을_지우면_런_저장도_지워진다()
        {
            yield return OpenTitle();
            yield return NewGame(1);
            State.Wallet.AddGold(777);
            int richGold = State.Wallet.Gold;
            Assert.IsTrue(battle.SaveRun(RunSaveTrigger.QuitDuringPreparation), "준비 중에 끌 때의 저장.");
            Assert.AreEqual(richGold, GameSession.RunSaves.Inspect(1).Snapshot.gold);
            GameSession.Meta.unlockPoints = 9;
            Assert.IsTrue(GameSession.SaveMeta());

            GameSession.RunSavePolicy.onPreparationStart = false;
            Assert.IsFalse(battle.SaveRun(RunSaveTrigger.PreparationStart), "꺼 둔 계기로는 저장하지 않는다.");
            GameSession.RunSavePolicy = new RunSavePolicy();

            yield return QuitToTitle();
            var title = Object.FindAnyObjectByType<TitleScreen>();
            title.SelectSlot(1);
            title.DeleteSelectedSlot();
            Assert.IsTrue(title.IsDeleteArmed);
            title.DeleteSelectedSlot();
            Assert.IsFalse(GameSession.HasRunSave(1), "슬롯을 지우면 진행 중이던 런도 지워진다.");
            Assert.AreEqual(MetaLoadStatus.Empty, GameSession.SaveStore.Inspect(1).Status);
            Assert.IsFalse(title.CanContinue);
            title.ContinueGame();
            Assert.AreEqual("Title", SceneManager.GetActiveScene().name, "이어 할 런이 없으면 아무 일도 없다.");
            Assert.AreEqual(GamePhase.Title, GameSession.Flow.Phase);

            yield return NewGame(1);
            Assert.AreNotEqual(richGold, State.Wallet.Gold);
            yield return QuitToTitle();
            title = Object.FindAnyObjectByType<TitleScreen>();
            title.SelectSlot(1);
            Assert.IsTrue(title.CanContinue);
            title.StartGame();
            Assert.IsTrue(title.IsNewGameArmed);
            yield return EnterGame(screen => screen.StartGame());
            Assert.IsFalse(battle.RunLink.WasRestored, "두 번째로 누르면 저장된 런을 버리고 새로 시작한다.");
            Assert.AreEqual(1, GameSession.Flow.Run.StageNumber);
        }
    }
}
