using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Facilities;
using ProjectNeo.Flow;
using ProjectNeo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectNeo.Tests
{
    /// <summary>D-0: 안뜰 시설 건물에서 F로 팝업을 열고 Esc로 닫는다.</summary>
    public class GameFacilityPlayTests
    {
        static int cleanup;
        GameFacilities facilities;
        GamePhaseView view;
        PlayerInputController player;
        float capture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            GameSession.Reset();
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; yield return null;
            facilities = Object.FindAnyObjectByType<GameFacilities>();
            view = Object.FindAnyObjectByType<GamePhaseView>();
            player = Object.FindAnyObjectByType<PlayerInputController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            while (SceneSwitcher.IsLoading) yield return null;
            Scene empty = SceneManager.CreateScene("GameFacilityCleanup" + cleanup++);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                if (SceneManager.GetSceneAt(i) != empty) yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneAt(i));
            GameSession.Reset(); Time.captureDeltaTime = capture;
        }

        FacilityStation Station(FacilityType type) =>
            Object.FindObjectsByType<FacilityStation>().Single(station => station.Type == type);

        [UnityTest]
        public IEnumerator CursorCapture_RestoresAfterFocusOnlyWhileGameplayIsEnabled()
        {
            Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState, "Game starts captured.");
            Assert.IsFalse(Cursor.visible);
            player.SendMessage("OnApplicationFocus", false);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            Assert.IsTrue(Cursor.visible);
            player.SendMessage("OnApplicationFocus", true);
            Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState, "Returning to gameplay recaptures automatically.");
            Assert.IsFalse(Cursor.visible);
            Assert.IsTrue(Station(FacilityType.Shop).Interact());
            player.SendMessage("OnApplicationFocus", true);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "A disabled player must leave UI cursor access intact.");
            Assert.IsTrue(Cursor.visible);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CursorCapture_IgnoresTheEscapeThatClosedAPanel()
        {
            using var inputScope = new FocusIndependentInputScope();
            Keyboard originalKeyboard = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    Assert.IsTrue(Station(FacilityType.Shop).Interact());
                    Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
                    Assert.IsTrue(Cursor.visible);
                    player.SendMessage("OnApplicationFocus", true);
                    Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "A disabled player must leave UI cursor access intact.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    InputSystem.Update();
                    // Exercise both callback orders: close first, then player Update with the same Esc.
                    facilities.Close();
                    player.SendMessage("Update");
                    Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState, "The closing key must not release the newly captured cursor.");
                    Assert.IsFalse(Cursor.visible);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null; yield return null;
                    Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);
                    Assert.IsFalse(Cursor.visible);
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            }
        }

        [UnityTest]
        public IEnumerator 런_데이터에_시설_여섯_곳이_시작_레벨로_붙는다()
        {
            Assert.IsNotNull(facilities, "Game 씬 전투 오브젝트에 시설 컴포넌트가 있어야 한다.");
            foreach (FacilityType type in new[] { FacilityType.Tavern, FacilityType.Shop, FacilityType.TrainingGround,
                         FacilityType.Smithy, FacilityType.Defense, FacilityType.HeroHall })
            {
                FacilityState state = facilities.Facility(type);
                Assert.IsNotNull(state, type.ToString());
                Assert.AreEqual(0, state.Level);
            }
            Assert.AreEqual(6, GameSession.Flow.Run.State.Facilities.Count);
            yield break;
        }

        [UnityTest]
        public IEnumerator 영웅의_전당에서_고용한_영웅의_등급을_보고_경험치가_모자라면_승급을_거부한다()
        {
            ProjectNeo.Run.RunState state = GameSession.Flow.Run.State;
            state.Wallet.AddGold(500);
            Assert.IsTrue(Station(FacilityType.Tavern).Interact());
            Assert.IsTrue(facilities.ChooseCaptainSquad(0));
            Assert.IsTrue(facilities.TryHire(0));
            facilities.Close();
            yield return null;

            ProjectNeo.Run.RunHero hero = state.GradedHeroes.Single();
            Assert.AreEqual(state.StartingGrade(hero.Definition), hero.Grade, "a hired hero starts at its hire tier grade");
            CharacterActor actor = Object.FindObjectsByType<CharacterActor>().Single(found => found.Character == hero.Character);
            Assert.AreEqual(actor.Health.Maximum, actor.Health.Current, 0.001f, "a new hero starts at full health after the grade bonus");

            FacilityStation hall = Station(FacilityType.HeroHall);
            Assert.IsTrue(hall.Interact());
            Assert.AreEqual(FacilityType.HeroHall, facilities.OpenFacility);
            Assert.AreEqual("Hero Hall", facilities.TitleText);
            int gold = state.Wallet.Gold;
            bool promoted = facilities.TryPromote(0);
            if (hero.Grade < state.HeroPromotion.MaxGrade)
            {
                Assert.IsFalse(promoted, "a new hero has no EXP");
                StringAssert.Contains("Not enough EXP", facilities.BodyText);
                Assert.AreEqual(gold, state.Wallet.Gold, "nothing is spent on a refusal");
            }
            facilities.Close();
            yield return null;
        }

        [UnityTest]
        public IEnumerator 건물_네_곳에서_각자의_팝업이_열리고_닫힌다()
        {
            foreach (FacilityType type in new[] { FacilityType.Tavern, FacilityType.Shop, FacilityType.TrainingGround, FacilityType.Smithy })
            {
                FacilityStation station = Station(type);
                Assert.IsTrue(station.Interact(), type.ToString());
                Assert.IsTrue(view.IsPanelView && !view.IsExplorationView);
                Assert.AreEqual(type, facilities.OpenFacility);
                Assert.IsFalse(player.enabled, "팝업 중에는 이동·시점을 멈춘다.");
                Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
                Assert.AreEqual(facilities.NameOf(type), facilities.TitleText);
                StringAssert.Contains("Level 1 / 3", facilities.StatusText);
                StringAssert.Contains("Gold 100", facilities.StatusText);
                Assert.IsFalse(view.EnterConstruction(), "팝업 중에는 건설 모드에 들어가지 않는다.");
                Assert.IsFalse(station.Interact(), "이미 열려 있으면 다시 열지 않는다.");
                facilities.Close();
                yield return null;
                Assert.IsFalse(facilities.IsOpen);
                Assert.IsTrue(view.IsExplorationView && player.enabled);
                Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);
            }
        }

        [UnityTest]
        public IEnumerator 상점_팝업에서_매물을_사고_보관함_아이템을_판다()
        {
            var state = GameSession.Flow.Run.State;
            Assert.IsTrue(Station(FacilityType.Shop).Interact());
            Assert.IsNotNull(state.Shop, "시작 설정의 거래 수치로 상점 창구가 생긴다.");
            Assert.AreEqual(3, state.Shop.Stock.Count, "상점 1레벨 매물 수");
            Assert.AreEqual(0, state.ShopStockedFor);
            Assert.AreEqual(3, facilities.OfferRows);
            Assert.AreEqual(1, facilities.InventoryRows, "빈 보관함 안내 한 줄");
            var item = state.Shop.Stock[0];
            int gold = state.Wallet.Gold;
            Assert.IsTrue(facilities.TryBuy(0));
            Assert.AreEqual(gold - item.Price.gold, state.Wallet.Gold);
            Assert.AreEqual(1, state.Inventory.CountOf(item));
            Assert.AreEqual(2, facilities.OfferRows);
            StringAssert.Contains("purchased", facilities.BodyText);
            int sell = state.Shop.GetSellPrice(item);
            Assert.IsTrue(facilities.TrySell(0));
            Assert.AreEqual(gold - item.Price.gold + sell, state.Wallet.Gold);
            Assert.AreEqual(0, state.Inventory.Items.Count);
            facilities.Close();
            yield return null;
            Assert.IsTrue(Station(FacilityType.Shop).Interact());
            Assert.AreEqual(2, state.Shop.Stock.Count, "같은 준비에서는 다시 열어도 새로 뽑지 않는다.");
        }

        [UnityTest]
        public IEnumerator 상점_팝업에서_골드를_내고_리롤하며_다른_시설에는_리롤_버튼이_없다()
        {
            var state = GameSession.Flow.Run.State;
            Assert.IsTrue(Station(FacilityType.Shop).Interact());
            Assert.IsTrue(facilities.IsRerollVisible, "Game 씬 시설 팝업에 리롤 버튼이 있어야 한다.");
            StringAssert.Contains("10 gold", facilities.RerollText);
            int gold = state.Wallet.Gold;
            Assert.IsTrue(facilities.TryBuy(0));
            gold -= state.Inventory.Items[0].Definition.Price.gold;
            Assert.AreEqual(2, facilities.OfferRows);

            Assert.IsTrue(facilities.TryReroll());
            Assert.AreEqual(gold - 10, state.Wallet.Gold);
            Assert.AreEqual(3, facilities.OfferRows, "산 칸까지 다시 채운다.");
            StringAssert.Contains("20 gold", facilities.RerollText);
            StringAssert.Contains("Offers rerolled", facilities.BodyText);

            state.Wallet.TrySpend(new ProjectNeo.Economy.Cost(state.Wallet.Gold));
            Assert.IsFalse(facilities.TryReroll(), "골드가 모자라다.");
            StringAssert.Contains("Not enough funds", facilities.BodyText);
            Assert.AreEqual(1, state.ShopRerolls);
            facilities.Close();
            yield return null;

            Assert.IsTrue(Station(FacilityType.Tavern).Interact());
            Assert.IsFalse(facilities.IsRerollVisible, "상점이 아니면 리롤 버튼을 숨긴다.");
            Assert.IsFalse(facilities.TryReroll());
        }

        [UnityTest]
        public IEnumerator 대장간_팝업에서_레시피로_만들고_장비를_판다()
        {
            var state = GameSession.Flow.Run.State;
            Assert.IsTrue(Station(FacilityType.Smithy).Interact());
            var recipes = state.CraftableRecipes();
            Assert.AreEqual(3, recipes.Count, "대장간 1레벨 레시피 수");
            Assert.AreEqual(3, facilities.OfferRows);
            StringAssert.Contains("Craft", facilities.OfferHeaderText);
            Assert.AreEqual(1, facilities.InventoryRows, "팔 장비가 없다는 안내 한 줄");

            int gold = state.Wallet.Gold;
            var expensive = recipes.First(recipe => !state.Wallet.CanAfford(recipe.cost));
            Assert.IsFalse(facilities.TryCraft(recipes.ToList().IndexOf(expensive)), "재료가 모자라면 만들 수 없다.");
            StringAssert.Contains("Not enough funds", facilities.BodyText);
            Assert.AreEqual(gold, state.Wallet.Gold);

            var shield = recipes[0];
            Assert.IsTrue(facilities.TryCraft(0));
            Assert.AreEqual(gold - shield.cost.gold, state.Wallet.Gold);
            Assert.AreEqual(1, state.Inventory.CountOf(shield.item));
            Assert.AreEqual(3, facilities.OfferRows, "고정 레시피라 만든 뒤에도 남는다.");
            Assert.AreEqual(1, facilities.InventoryRows);
            StringAssert.Contains("craft", facilities.BodyText);

            int sell = state.Shop.GetSellPrice(shield.item);
            Assert.IsTrue(facilities.TrySellEquipment(0));
            Assert.AreEqual(gold - shield.cost.gold + sell, state.Wallet.Gold);
            Assert.AreEqual(0, state.Inventory.Items.Count);
            yield break;
        }

        [UnityTest]
        public IEnumerator 시설_팝업에서_업그레이드하고_비용이_모자라면_거부한다()
        {
            var state = GameSession.Flow.Run.State;
            Assert.IsTrue(Station(FacilityType.Tavern).Interact());
            StringAssert.Contains("60 gold", facilities.UpgradeText);
            Assert.AreEqual(100, state.Wallet.Gold);
            Assert.IsTrue(facilities.TryUpgrade());
            Assert.AreEqual(1, state.Facility(FacilityType.Tavern).Level);
            Assert.AreEqual(40, state.Wallet.Gold);
            StringAssert.Contains("Level 2 / 3", facilities.StatusText);
            StringAssert.Contains("120 gold", facilities.UpgradeText);
            Assert.IsFalse(facilities.TryUpgrade(), "골드가 모자라다.");
            Assert.AreEqual(1, state.Facility(FacilityType.Tavern).Level);
            Assert.AreEqual(40, state.Wallet.Gold);
            StringAssert.Contains("Not enough funds", facilities.BodyText);
            StringAssert.Contains("hire", facilities.OfferHeaderText, "주점은 영웅 고용 줄을 보여 준다(D-3).");
            yield break;
        }

        [UnityTest]
        public IEnumerator 대장간_장비를_강화하고_착용_능력치와_판매_환급에_반영한다()
        {
            var state = GameSession.Flow.Run.State;
            Assert.IsTrue(Station(FacilityType.Smithy).Interact());
            Assert.IsNotNull(state.EquipmentUpgrades, "RunStart에 강화 설정이 연결되어 있어야 한다.");
            StringAssert.Contains("max +1", facilities.BodyText);
            Assert.IsTrue(facilities.TryCraft(0));
            var item = state.Inventory.Items[0];
            Assert.AreEqual(5, item.Definition.UpgradeMaterials.Count);
            Assert.IsTrue(state.EquipmentUpgrades.TryGetNextCost(item, out var cost));
            int gold = state.Wallet.Gold;
            var row = facilities.EquipmentUpgradeButton(0);
            Assert.IsNotNull(row);
            Assert.IsTrue(row.interactable);
            row.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, item.UpgradeLevel);
            Assert.AreEqual(gold - cost.gold, state.Wallet.Gold);
            StringAssert.Contains("+1 upgraded", facilities.BodyText);
            Assert.IsFalse(facilities.EquipmentUpgradeButton(0).interactable, "Lv1의 강화 한도");
            gold = state.Wallet.Gold;
            Assert.IsFalse(facilities.TryUpgradeEquipment(0));
            Assert.AreEqual(gold, state.Wallet.Gold);

            facilities.Close();
            yield return null;
            var equipment = Object.FindAnyObjectByType<GameEquipment>();
            Assert.IsTrue(equipment.Open());
            var modifier = item.Definition.EquipModifiers[0];
            float before = state.Player.Character.Stats.GetValue(modifier.stat);
            Assert.IsTrue(equipment.TryEquip(0));
            Assert.That(state.Player.Character.Stats.GetValue(modifier.stat),
                Is.EqualTo(before + modifier.value * 1.1f).Within(0.001f));
            Assert.IsFalse(state.TryUpgradeEquipment(item), "착용 중에는 강화 대상에서 제외한다.");
            Assert.IsTrue(equipment.TryUnequip(0));
            Assert.That(state.Player.Character.Stats.GetValue(modifier.stat), Is.EqualTo(before).Within(0.001f));
            equipment.Close();
            yield return null;
            Assert.IsTrue(Station(FacilityType.Smithy).Interact());
            int sell = state.Shop.GetInstanceSellPrice(item);
            var sellButton = facilities.EquipmentUpgradeButton(0).transform.Find("Button_SellEquipment")
                .GetComponent<UnityEngine.UI.Button>();
            Assert.IsTrue(sellButton.interactable, "강화가 최대여도 판매 버튼은 사용할 수 있다.");
            sellButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(gold + sell, state.Wallet.Gold);
            Assert.AreEqual(0, state.Inventory.Items.Count);
            StringAssert.Contains($"{sell} gold", facilities.BodyText);
        }

        [UnityTest]
        public IEnumerator 대장간_강화_재료는_아이템별_목표단계에_비례하고_부족하면_거부한다()
        {
            var state = GameSession.Flow.Run.State;
            Assert.IsTrue(Station(FacilityType.Smithy).Interact());
            var recipe = state.CraftableRecipes().First(r => r.cost.materials != null && r.cost.materials.Count > 0);
            var item = state.Inventory.Add(recipe.item);
            for (int target = 1; target <= item.Definition.UpgradeMaterials.Count; target++)
                foreach (var material in recipe.cost.materials)
                    Assert.AreEqual(material.amount * target,
                        item.Definition.UpgradeMaterials[target - 1].materials.Single(m => m.material == material.material).amount);
            int gold = state.Wallet.Gold;
            Assert.IsFalse(facilities.TryUpgradeEquipment(0));
            StringAssert.Contains("Not enough funds", facilities.BodyText);
            Assert.AreEqual(gold, state.Wallet.Gold);
            foreach (var material in recipe.cost.materials) state.Wallet.AddMaterial(material.material, material.amount);
            Assert.IsTrue(facilities.TryUpgradeEquipment(0));
            foreach (var material in recipe.cost.materials) Assert.AreEqual(0, state.Wallet.GetMaterial(material.material));
            Assert.AreEqual(1, item.UpgradeLevel);
            yield break;
        }

        [UnityTest]
        public IEnumerator 상점을_바라보고_F로_열고_Esc로_닫으며_전투_중에는_열리지_않는다()
        {
            var interaction = Object.FindAnyObjectByType<PreparationInteraction>();
            var camera = Object.FindAnyObjectByType<FirstPersonCamera>();
            var hud = Object.FindAnyObjectByType<GameFlowHud>();
            var motor = player.GetComponent<CharacterController>();
            FacilityStation shop = Station(FacilityType.Shop);
            using var inputScope = new FocusIndependentInputScope();
            Keyboard originalKeyboard = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Bounds bounds = shop.GetComponent<BoxCollider>().bounds;
                Vector3 near = new Vector3(bounds.center.x, 0f, bounds.min.z - 2f);
                motor.enabled = false; player.transform.position = near; motor.enabled = true;
                yield return null;
                Cursor.lockState = CursorLockMode.Locked;
                camera.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                Physics.SyncTransforms();
                interaction.RefreshTarget();
                Assert.AreSame(shop, interaction.Target);
                Assert.AreEqual("Shop - Use", shop.Prompt);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                yield return null;
                Assert.IsTrue(facilities.IsOpen && view.IsPanelView);
                Assert.AreEqual(FacilityType.Shop, facilities.OpenFacility);
                yield return null; // 안내 글자는 LateUpdate에서 갱신한다.
                StringAssert.Contains("close", hud.InteractionHint);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                Assert.IsFalse(facilities.IsOpen, "Esc로 닫는다.");
                Assert.IsTrue(view.IsExplorationView && player.enabled);
                Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState, "닫는 Esc가 이동 커서를 다시 풀지 않아야 한다.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;

                Assert.IsTrue(shop.Interact());
                Assert.IsTrue(GameSession.Flow.StartBattle());
                yield return null;
                Assert.IsFalse(facilities.IsOpen, "전투가 시작되면 팝업을 닫는다.");
                Assert.IsFalse(shop.Interact(), "전투 중에는 열리지 않는다.");
                Assert.IsFalse(view.IsPanelView);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            }
        }

        [UnityTest]
        public IEnumerator 상점_팝업에서_매물을_잠그면_리롤에도_남고_사면_잠금이_풀린다()
        {
            var state = GameSession.Flow.Run.State;
            Assert.IsTrue(Station(FacilityType.Shop).Interact());
            Assert.AreEqual(3, facilities.OfferRows);
            Assert.AreEqual(1, facilities.ShopMaxLocked);

            int goldBefore = state.Wallet.Gold;
            var secondLockButton = facilities.OfferLockButton(1);
            Assert.IsNotNull(secondLockButton, "매물 줄마다 잠금 버튼이 있어야 한다.");
            var lockedItem = state.Shop.Stock[1];
            secondLockButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(goldBefore, state.Wallet.Gold, "잠금은 무료");
            Assert.AreEqual(1, facilities.ShopLockedCount);
            Assert.IsTrue(facilities.IsOfferLocked(1));
            StringAssert.Contains("lock", facilities.BodyText);

            // 한도 초과(레벨 1: 최대 1개) 거부
            Assert.IsFalse(facilities.TryToggleLock(0));
            StringAssert.Contains("limit 1", facilities.BodyText);

            // 리롤 후 잠긴 매물은 유지
            Assert.IsTrue(facilities.TryReroll());
            Assert.AreEqual(3, facilities.OfferRows);
            Assert.IsTrue(facilities.IsOfferLocked(0));
            Assert.AreSame(lockedItem, state.Shop.Stock[0]);

            // 잠긴 매물 구매 시 잠금이 풀린다.
            Assert.IsTrue(facilities.TryBuy(0));
            Assert.AreEqual(0, facilities.ShopLockedCount);
            Assert.IsFalse(facilities.IsOfferLocked(0));
        }
    }
}
