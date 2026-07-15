using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Item
{
    public class ItemManager : NetworkBehaviour
    {
        public static ItemManager Instance { get; private set; }

        [Header("Item Registry")]
        [SerializeField] ItemDataSO[] allItems;

        [Header("Basic Item IDs (index in allItems)")]
        [SerializeField] short fanItemId = 0;
        [SerializeField] short windbreakerItemId = 1;
        [SerializeField] short warmTeaItemId = 2;
        [SerializeField] short catItemId = 3;

        [Header("Test Mode")]
        [SerializeField] bool useTestItems = true;

        ItemDropTable _dropTable;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Instance = this;

            if (useTestItems || allItems == null || allItems.Length == 0)
                CreateTestItems();

            if (IsServer)
                _dropTable = new ItemDropTable(allItems);
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        void CreateTestItems()
        {
            allItems = new ItemDataSO[21];
            int idx = 0;

            // === Basic Items (0-3) ===
            var fan = ScriptableObject.CreateInstance<AttackItemDataSO>();
            fan.ItemName = "Fan";
            fan.Description = "Blow cold air at opponent";
            fan.Category = ItemCategory.Attack;
            fan.Persistence = ItemPersistence.Permanent;
            fan.SlotType = ItemSlotType.Main;
            fan.MaxUses = 0;
            fan.Damage = 3f;
            fan.AttackFilter = DamageFilter.Temperature;
            allItems[idx++] = fan; // 0

            var windbreaker = ScriptableObject.CreateInstance<DefenseItemDataSO>();
            windbreaker.ItemName = "Windbreaker";
            windbreaker.Description = "Block incoming cold damage";
            windbreaker.Category = ItemCategory.Defense;
            windbreaker.Persistence = ItemPersistence.Permanent;
            windbreaker.SlotType = ItemSlotType.Main;
            windbreaker.MaxUses = 0;
            windbreaker.BlockAmount = 4f;
            windbreaker.Filter = DamageFilter.Temperature;
            allItems[idx++] = windbreaker; // 1

            var warmTea = ScriptableObject.CreateInstance<RecoveryItemDataSO>();
            warmTea.ItemName = "Warm Tea";
            warmTea.Description = "Drink warm tea to recover temperature";
            warmTea.Category = ItemCategory.Recovery;
            warmTea.Persistence = ItemPersistence.BasicConsumable;
            warmTea.SlotType = ItemSlotType.Main;
            warmTea.MaxUses = 2;
            warmTea.HealPerUse = new[] { 7f };
            allItems[idx++] = warmTea; // 2

            var cat = ScriptableObject.CreateInstance<SabotageItemDataSO>();
            cat.ItemName = "Cat";
            cat.Description = "Cat rerolls all opponent random items";
            cat.Category = ItemCategory.Sabotage;
            cat.Persistence = ItemPersistence.BasicConsumable;
            cat.SlotType = ItemSlotType.Sub;
            cat.MaxUses = 1;
            cat.SabotageType = SabotageType.Reroll;
            allItems[idx++] = cat; // 3

            // === Random Items (4-20) ===

            // ATK
            var handFan = ScriptableObject.CreateInstance<AttackItemDataSO>();
            handFan.ItemName = "Hand Fan";
            handFan.Category = ItemCategory.Attack;
            handFan.Persistence = ItemPersistence.RandomConsumable;
            handFan.SlotType = ItemSlotType.Main;
            handFan.MaxUses = 1;
            handFan.Damage = 4f;
            handFan.DropWeight = 12f;
            allItems[idx++] = handFan; // 4

            var iceCream = ScriptableObject.CreateInstance<AttackItemDataSO>();
            iceCream.ItemName = "Ice Cream";
            iceCream.Category = ItemCategory.Attack;
            iceCream.Persistence = ItemPersistence.RandomConsumable;
            iceCream.SlotType = ItemSlotType.Main;
            iceCream.MaxUses = 1;
            iceCream.Damage = 5f;
            iceCream.DropWeight = 10f;
            allItems[idx++] = iceCream; // 5

            var icedAmericano = ScriptableObject.CreateInstance<AttackItemDataSO>();
            icedAmericano.ItemName = "Iced Americano";
            icedAmericano.Category = ItemCategory.Attack;
            icedAmericano.Persistence = ItemPersistence.RandomConsumable;
            icedAmericano.SlotType = ItemSlotType.Main;
            icedAmericano.MaxUses = 1;
            icedAmericano.Damage = 5f;
            icedAmericano.DropWeight = 10f;
            allItems[idx++] = icedAmericano; // 6

            var waterGun = ScriptableObject.CreateInstance<AttackItemDataSO>();
            waterGun.ItemName = "Water Gun";
            waterGun.Category = ItemCategory.Attack;
            waterGun.Persistence = ItemPersistence.RandomConsumable;
            waterGun.SlotType = ItemSlotType.Main;
            waterGun.MaxUses = 1;
            waterGun.Damage = 7f;
            waterGun.DropWeight = 6f;
            waterGun.RequiresMiniGame = true;
            waterGun.MiniGameType = MiniGameType.HitTargets;
            waterGun.MiniGameTimeLimit = 5f;
            allItems[idx++] = waterGun; // 7

            var hugShirt = ScriptableObject.CreateInstance<AttackItemDataSO>();
            hugShirt.ItemName = "Hug T-shirt";
            hugShirt.Category = ItemCategory.Attack;
            hugShirt.Persistence = ItemPersistence.RandomConsumable;
            hugShirt.SlotType = ItemSlotType.Main;
            hugShirt.MaxUses = 1;
            hugShirt.EqualizeToUserTemp = true;
            hugShirt.DropWeight = 4f;
            hugShirt.RequiresMiniGame = true;
            hugShirt.MiniGameType = MiniGameType.HugCharacter;
            hugShirt.MiniGameTimeLimit = 10f;
            allItems[idx++] = hugShirt; // 8

            // REC
            var hotAmericano = ScriptableObject.CreateInstance<RecoveryItemDataSO>();
            hotAmericano.ItemName = "Hot Americano";
            hotAmericano.Category = ItemCategory.Recovery;
            hotAmericano.Persistence = ItemPersistence.RandomConsumable;
            hotAmericano.SlotType = ItemSlotType.Main;
            hotAmericano.MaxUses = 1;
            hotAmericano.HealPerUse = new[] { 5f };
            hotAmericano.DropWeight = 10f;
            allItems[idx++] = hotAmericano; // 9

            var smartphone = ScriptableObject.CreateInstance<RecoveryItemDataSO>();
            smartphone.ItemName = "Smartphone";
            smartphone.Category = ItemCategory.Recovery;
            smartphone.Persistence = ItemPersistence.RandomConsumable;
            smartphone.SlotType = ItemSlotType.Main;
            smartphone.MaxUses = 1;
            smartphone.HealPerUse = new[] { 3f, 5f, 7f };
            smartphone.DropWeight = 6f;
            smartphone.RequiresMiniGame = true;
            smartphone.MiniGameType = MiniGameType.PatternUnlock;
            smartphone.MiniGameTimeLimit = 5f;
            allItems[idx++] = smartphone; // 10

            var hotPack = ScriptableObject.CreateInstance<RecoveryItemDataSO>();
            hotPack.ItemName = "Hot Pack";
            hotPack.Category = ItemCategory.Recovery;
            hotPack.Persistence = ItemPersistence.RandomConsumable;
            hotPack.SlotType = ItemSlotType.Main;
            hotPack.MaxUses = 1;
            hotPack.HealPerUse = new[] { 10f };
            hotPack.DropWeight = 4f;
            hotPack.RequiresMiniGame = true;
            hotPack.MiniGameType = MiniGameType.TapRepeat;
            hotPack.MiniGameTimeLimit = 10f;
            allItems[idx++] = hotPack; // 11

            // DEF
            var mask = ScriptableObject.CreateInstance<DefenseItemDataSO>();
            mask.ItemName = "Mask";
            mask.Category = ItemCategory.Defense;
            mask.Persistence = ItemPersistence.RandomConsumable;
            mask.SlotType = ItemSlotType.Main;
            mask.MaxUses = 1;
            mask.BlockAmount = float.MaxValue;
            mask.Filter = DamageFilter.Food;
            mask.DropWeight = 8f;
            allItems[idx++] = mask; // 12

            // DBF
            var samgyetang = ScriptableObject.CreateInstance<DebuffItemDataSO>();
            samgyetang.ItemName = "Samgyetang";
            samgyetang.Category = ItemCategory.Debuff;
            samgyetang.Persistence = ItemPersistence.RandomConsumable;
            samgyetang.SlotType = ItemSlotType.Main;
            samgyetang.MaxUses = 1;
            samgyetang.ImmediateTempDelta = 3f;
            samgyetang.DelayedTempDelta = -7f;
            samgyetang.AttackFilter = DamageFilter.Food;
            samgyetang.DropWeight = 8f;
            allItems[idx++] = samgyetang; // 13

            // BUF
            var soda = ScriptableObject.CreateInstance<BuffItemDataSO>();
            soda.ItemName = "Soda";
            soda.Category = ItemCategory.Buff;
            soda.Persistence = ItemPersistence.RandomConsumable;
            soda.SlotType = ItemSlotType.Main;
            soda.MaxUses = 1;
            soda.ImmediateTempDelta = -5f;
            soda.DelayedTempDelta = 15f;
            soda.DropWeight = 6f;
            soda.RequiresMiniGame = true;
            soda.MiniGameType = MiniGameType.GaugeMatch;
            soda.MiniGameTimeLimit = 5f;
            allItems[idx++] = soda; // 14

            var buldak = ScriptableObject.CreateInstance<BuffItemDataSO>();
            buldak.ItemName = "Buldak Noodles";
            buldak.Category = ItemCategory.Buff;
            buldak.Persistence = ItemPersistence.RandomConsumable;
            buldak.SlotType = ItemSlotType.Main;
            buldak.MaxUses = 1;
            buldak.ImmediateTempDelta = 0f;
            buldak.DelayedTempDelta = 17f;
            buldak.DropWeight = 2f;
            buldak.RequiresMiniGame = true;
            buldak.MiniGameType = MiniGameType.BoilWater;
            buldak.MiniGameTimeLimit = 10f;
            allItems[idx++] = buldak; // 15

            // SPC
            var screwdriver = ScriptableObject.CreateInstance<SpecialItemDataSO>();
            screwdriver.ItemName = "Screwdriver";
            screwdriver.Category = ItemCategory.Special;
            screwdriver.Persistence = ItemPersistence.RandomConsumable;
            screwdriver.SlotType = ItemSlotType.Main;
            screwdriver.MaxUses = 1;
            screwdriver.SpecialEffect = SpecialEffectType.FanSpeedChange;
            screwdriver.EffectValue = 2f;
            screwdriver.TargetsSelf = false;
            screwdriver.DelayTurns = 1;
            screwdriver.DropWeight = 4f;
            screwdriver.RequiresMiniGame = true;
            screwdriver.MiniGameType = MiniGameType.TightenScrews;
            screwdriver.MiniGameTimeLimit = 5f;
            allItems[idx++] = screwdriver; // 16

            var tarotCard = ScriptableObject.CreateInstance<SpecialItemDataSO>();
            tarotCard.ItemName = "Tarot Card";
            tarotCard.Category = ItemCategory.Special;
            tarotCard.Persistence = ItemPersistence.RandomConsumable;
            tarotCard.SlotType = ItemSlotType.Sub;
            tarotCard.MaxUses = 1;
            tarotCard.SpecialEffect = SpecialEffectType.RevealOpponent;
            tarotCard.DropWeight = 2f;
            tarotCard.RequiresMiniGame = true;
            tarotCard.MiniGameType = MiniGameType.PickCard;
            tarotCard.MiniGameTimeLimit = 5f;
            allItems[idx++] = tarotCard; // 17

            // SAB
            var clawMachine = ScriptableObject.CreateInstance<SabotageItemDataSO>();
            clawMachine.ItemName = "Claw Machine";
            clawMachine.Category = ItemCategory.Sabotage;
            clawMachine.Persistence = ItemPersistence.RandomConsumable;
            clawMachine.SlotType = ItemSlotType.Sub;
            clawMachine.MaxUses = 1;
            clawMachine.SabotageType = SabotageType.Steal;
            clawMachine.DropWeight = 4f;
            clawMachine.RequiresMiniGame = true;
            clawMachine.MiniGameType = MiniGameType.ClawGrab;
            clawMachine.MiniGameTimeLimit = 7f;
            allItems[idx++] = clawMachine; // 18

            var blueTape = ScriptableObject.CreateInstance<SabotageItemDataSO>();
            blueTape.ItemName = "Blue Tape";
            blueTape.Category = ItemCategory.Sabotage;
            blueTape.Persistence = ItemPersistence.RandomConsumable;
            blueTape.SlotType = ItemSlotType.Sub;
            blueTape.MaxUses = 1;
            blueTape.SabotageType = SabotageType.BlockBasic;
            blueTape.DropWeight = 4f;
            blueTape.RequiresMiniGame = true;
            blueTape.MiniGameType = MiniGameType.TimingCut;
            blueTape.MiniGameTimeLimit = 5f;
            allItems[idx++] = blueTape; // 19

            var redCard = ScriptableObject.CreateInstance<SabotageItemDataSO>();
            redCard.ItemName = "Red Card";
            redCard.Category = ItemCategory.Sabotage;
            redCard.Persistence = ItemPersistence.RandomConsumable;
            redCard.SlotType = ItemSlotType.Sub;
            redCard.MaxUses = 1;
            redCard.SabotageType = SabotageType.Neutralize;
            redCard.DropWeight = 2f;
            redCard.RequiresMiniGame = true;
            redCard.MiniGameType = MiniGameType.TapCard;
            redCard.MiniGameTimeLimit = 5f;
            allItems[idx++] = redCard; // 20

            fanItemId = 0;
            windbreakerItemId = 1;
            warmTeaItemId = 2;
            catItemId = 3;

            Debug.Log($"[ItemManager] {idx} items created (4 basic + {idx - 4} random)");
        }

        public void InitializePlayerInventory(PlayerInventory inventory)
        {
            if (!IsServer) return;

            inventory.Initialize(allItems);
            inventory.InitializeBasicItems(fanItemId, windbreakerItemId, warmTeaItemId, catItemId);
        }

        public void InitializeClientRegistry(PlayerInventory inventory)
        {
            if (allItems != null)
                inventory.Initialize(allItems);
        }

        public ItemDropTable GetDropTable() => _dropTable;
        public ItemDataSO[] GetAllItems() => allItems;

        public ItemDataSO GetItemData(short itemId)
        {
            if (itemId < 0 || itemId >= allItems.Length) return null;
            return allItems[itemId];
        }
    }
}
