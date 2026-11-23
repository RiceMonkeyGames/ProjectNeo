using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectNeo.Characters;
using ProjectNeo.Economy;
using ProjectNeo.Run;
using ProjectNeo.Squads;
using ProjectNeo.Stats;

namespace ProjectNeo.Tests
{
    /// <summary>D-7 런 영웅 장비 착용.</summary>
    public class RunEquipmentTests
    {
        HeroDefinition hero;
        ItemDefinition sword, potion;

        [SetUp]
        public void SetUp()
        {
            hero = HeroDefinition.Create("player", equipmentSlots: 1,
                baseStats: new[] { new StatValue(StatType.Attack, 10f), new StatValue(StatType.Defense, 2f) });
            sword = ItemDefinition.Create("sword", ItemKind.Equipment, new Cost(40), true, StatModifier.Flat(StatType.Attack, 5f));
            potion = ItemDefinition.Create("potion", ItemKind.Consumable, new Cost(10));
        }

        RunState NewRun(out RunHero player)
        {
            var run = new RunState(SquadConfig.Create(4, 10));
            player = run.AddHero(hero, true);
            return run;
        }

        [Test]
        public void 보관함의_장비를_끼우면_스탯이_오르고_벗으면_돌아온다()
        {
            RunState run = NewRun(out RunHero player);
            ItemInstance item = run.Inventory.Add(sword);
            Assert.IsTrue(run.TryEquip(player, item));
            Assert.AreEqual(15f, player.Character.Stats.GetValue(StatType.Attack));
            CollectionAssert.Contains(player.Character.Equipment, item);
            Assert.IsFalse(run.Inventory.Contains(item));

            Assert.IsTrue(run.TryUnequip(player, item));
            Assert.AreEqual(10f, player.Character.Stats.GetValue(StatType.Attack));
            Assert.IsTrue(run.Inventory.Contains(item));
        }

        [Test]
        public void 소모품_보관함에_없는_아이템은_거부하고_같은_칸은_바꿔_낀다()
        {
            RunState run = NewRun(out RunHero player);
            Assert.IsFalse(run.TryEquip(player, run.Inventory.Add(potion)), "소모품");
            Assert.IsFalse(run.TryEquip(player, new ItemInstance(sword)), "보관함에 없음");
            ItemInstance first = run.Inventory.Add(sword);
            Assert.IsTrue(run.TryEquip(player, first));
            ItemInstance second = run.Inventory.Add(sword);
            Assert.IsTrue(run.TryEquip(player, second), "UI-07: 같은 무기 칸이면 바꿔 낀다.");
            Assert.IsTrue(run.Inventory.Contains(first), "벗은 장비는 보관함으로 돌아온다.");
            Assert.AreEqual(1, player.Character.Equipment.Count);
            Assert.AreEqual(15f, player.Character.Stats.GetValue(StatType.Attack));
        }

        [Test]
        public void 다른_런의_영웅에게는_끼우지_않는다()
        {
            RunState run = NewRun(out RunHero player);
            RunState other = NewRun(out RunHero stranger);
            Assert.IsFalse(run.TryEquip(stranger, run.Inventory.Add(sword)));
            Assert.IsFalse(run.TryUnequip(stranger, null));
            Assert.AreEqual(10f, stranger.Character.Stats.GetValue(StatType.Attack));
        }

        [Test]
        public void 착용은_런_변경을_알린다()
        {
            RunState run = NewRun(out RunHero player);
            int changes = 0;
            run.Changed += () => changes++;
            run.TryEquip(player, run.Inventory.Add(sword));
            Assert.Greater(changes, 0);
        }

        [Test]
        public void 씬_캐릭터를_런_영웅으로_공유하면_런에서_끼운_장비가_같은_스탯에_붙는다()
        {
            var sceneCharacter = new Character(hero);
            RunUnitBinding<object> binding = RunUnitBinding<object>.Create(SquadConfig.Create(4, 10), hero, new object(),
                new List<IReadOnlyList<KeyValuePair<object, SoldierDefinition>>>(), 0, sceneCharacter);
            RunState run = binding.State;
            Assert.AreSame(sceneCharacter, run.Player.Character);
            Assert.IsTrue(run.TryEquip(run.Player, run.Inventory.Add(sword)));
            Assert.AreEqual(15f, sceneCharacter.Stats.GetValue(StatType.Attack));
            Assert.Throws<ArgumentException>(() => run.AddHero(sceneCharacter), "같은 캐릭터를 두 번 더하지 않는다.");
        }

        [Test]
        public void 다른_영웅_정의의_캐릭터는_플레이어로_공유하지_않는다()
        {
            var other = new Character(HeroDefinition.Create("other"));
            Assert.Throws<ArgumentException>(() => RunUnitBinding<object>.Create(SquadConfig.Create(4, 10), hero, new object(),
                new List<IReadOnlyList<KeyValuePair<object, SoldierDefinition>>>(), 0, other));
        }
    }
}
