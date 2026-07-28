using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// Item Interactions
#region Item Interactions

public class ConsumeInteraction : IItemInteraction, ITypedActionEconomyProfileProvider
{
    public InteractionType Type => InteractionType.Item;
    public string Name => "Consume";
    public ActionEconomyMigrationState MigrationState => ActionEconomyMigrationState.TypedActionEconomy;

    public ActionCostProfile ResolveActionCostProfile(bool isCombatContext)
    {
        return new ActionCostProfile
        {
            MigrationState = MigrationState,
            ExplorationBehaviour = ExplorationActionBehaviour.TriggerCycle,
            CombatBehaviour = CombatActionBehaviour.Flexible,
            IsFree = false,
            WorldTimeCost = 0,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
            StaminaCost = 0,
            CombatExertionCost = isCombatContext ? FixedPointResourceMath.FromPoints(1f) : 0,
            ConsumptionCapacityCost = 1,
            CanOverexert = true,
            EndsPlayerTurn = false,
            CandidateForFutureStamina = false,
            PredictedStaminaCost = 0,
            IsContextual = false,
            CostLabel = string.Empty,
            Notes = "Typed consumable action. Consumption capacity is committed live; stamina remains free."
        };
    }

    public void ExecuteInteraction(Item item, Inventory inventory)
    {
        if (item is not IEdible edibleItem || !edibleItem.IsEdible)
        {
            GameDebugger.Instance.LogWarning($"ConsumeInteraction: {item.ItemInGameName} is not edible.");
            return;
        }

        Debug.Log("Current Satiety: " + PlayerStats.Instance.Satiety);
        PlayerStats.Instance.Satiety = Mathf.Min(PlayerStats.Instance.MaxSatiety, PlayerStats.Instance.Satiety + edibleItem.HungerValue);
        Debug.Log("New Satiety: " + PlayerStats.Instance.Satiety);
        PlayerStats.Instance.HasEaten = true;

        Debug.Log($"{item.ItemInGameName} has been consumed. Restoring {edibleItem.HungerValue} worth of Hunger");
        inventory.RemoveItem(item.ItemInGameName, 1);
    }

    public bool IsAvailable(Item item, Inventory inventory)
    {
        return item is IEdible edibleItem && edibleItem.IsEdible;
    }
}

public class EquipInteraction : IItemInteraction, ITypedActionEconomyProfileProvider
{
    public InteractionType Type => InteractionType.Item;
    public string Name => "Equip";
    public ActionEconomyMigrationState MigrationState => ActionEconomyMigrationState.TypedActionEconomy;

    public ActionCostProfile ResolveActionCostProfile(bool isCombatContext)
    {
        ActionCostProfile profile = ActionCostProfileResolver.BuildForItemInteraction(this);
        profile.MigrationState = MigrationState;
        profile.ExplorationBehaviour = ExplorationActionBehaviour.Free;
        profile.CombatBehaviour = CombatActionBehaviour.Flexible;
        profile.IsFree = false;
        profile.StaminaCost = 0;
        profile.CombatExertionCost = isCombatContext ? FixedPointResourceMath.FromPoints(1f) : 0;
        profile.CanOverexert = true;
        profile.Notes = "Typed tactical equipment action. Exploration is free; combat spends one combat exertion.";
        return profile;
    }

    public void ExecuteInteraction(Item item, Inventory inventory)
    {
        if (inventory is not CharacterInventory characterInventory)
        {
            GameDebugger.Instance.LogWarning("EquipInteraction: Inventory is not a CharacterInventory.");
            return;
        }

        Character character = PlayerStats.Instance.CurrentPlayerCharacter;
        if (character == null)
        {
            GameDebugger.Instance.LogWarning("EquipInteraction: Character reference is missing.");
            return;
        }

        if (character.EquippedItems.ContainsValue(item))
        {
            GameDebugger.Instance.LogWarning($"EquipInteraction: {item.ItemInGameName} is already equipped.");
            return;
        }

        List<EquipmentSlot> availableSlots = character.Anatomy.GetActiveEquipmentSlots();

        if (item is IWearable wearableItem)
        {
            EquipmentSlot slot = wearableItem.EquipmentSlots.FirstOrDefault(s => availableSlots.Contains(s));

            if (slot == default)
            {
                GameDebugger.Instance.LogWarning($"EquipInteraction: No valid equipment slot available for {item.ItemInGameName}. Expected slots: {string.Join(", ", (item as IWearable)?.EquipmentSlots ?? new List<EquipmentSlot>())}. Available slots: {string.Join(", ", availableSlots)}");
          //      characterInventory.DebugEquipmentSlots();
                return;
            }

            characterInventory.EquipItem(item, slot);
            GameDebugger.Instance.LogInfo($"{item.ItemInGameName} was equipped to {slot}.");
        }
        else if (item is IWeapon)
        {
            EquipmentSlot slot = EquipmentSlot.MainHand;

            if (!availableSlots.Contains(slot))
            {
                GameDebugger.Instance.LogWarning($"EquipInteraction: Cannot equip {item.ItemInGameName} to {slot}, slot is unavailable.");
                return;
            }

            characterInventory.EquipItem(item, slot);
            GameDebugger.Instance.LogInfo($"{item.ItemInGameName} was equipped to {slot}.");
        }
    }

    public bool IsAvailable(Item item, Inventory inventory)
    {
        if (inventory is not CharacterInventory characterInventory)
        {
            return false;
        }

        Character character = PlayerStats.Instance.CurrentPlayerCharacter;
        if (character == null)
        {
            return false;
        }

        List<EquipmentSlot> availableSlots = character.Anatomy.GetActiveEquipmentSlots();

        if (item is IWearable wearableItem)
        {
            return wearableItem.EquipmentSlots.Any(slot => availableSlots.Contains(slot));
        }
        else if (item is IWeapon)
        {
            return availableSlots.Contains(EquipmentSlot.MainHand);
        }

        return false;
    }
}


public class UnEquipInteraction : IItemInteraction
{
    public InteractionType Type => InteractionType.Item;
    public string Name => "Unequip";

    public void ExecuteInteraction(Item item, Inventory inventory)
    {
        if (inventory is not CharacterInventory characterInventory)
        {
            GameDebugger.Instance.LogWarning("UnEquipInteraction: Inventory is not a CharacterInventory.");
            return;
        }

        Character character = PlayerStats.Instance.CurrentPlayerCharacter;
        if (character == null)
        {
            GameDebugger.Instance.LogWarning("UnEquipInteraction: Character reference is missing.");
            return;
        }

        if (!character.EquippedItems.ContainsValue(item))
        {
            GameDebugger.Instance.LogWarning($"UnEquipInteraction: {item.ItemInGameName} is not currently equipped.");
            return;
        }

        var slot = character.EquippedItems.First(kv => kv.Value == item).Key;

        characterInventory.UnEquipItem(slot);
        GameDebugger.Instance.LogInfo($"{item.ItemInGameName} was unequipped from {slot} and returned to inventory.");
    }

    public bool IsAvailable(Item item, Inventory inventory)
    {
        Character character = PlayerStats.Instance.CurrentPlayerCharacter;
        return character != null && character.EquippedItems.ContainsValue(item);
    }
}



public class MakeActiveInteraction : IItemInteraction
{
    public InteractionType Type => InteractionType.Item;
    public string Name { get; private set; }

    public MakeActiveInteraction()
    {
        Name = "Make Active";
    }

    public void ExecuteInteraction(Item item, Inventory inventory)
    {
        if (item.IsTool)
        {
            item.IsActiveInInventory = true;
            Debug.Log($"{item.ItemInGameName} is now active in inventory.");
        }
    }

    public bool IsAvailable(Item item, Inventory inventory)
    {
        return item.IsTool && !item.IsActiveInInventory;
    }
}

public class DeactivateInteraction : IItemInteraction
{
    public InteractionType Type => InteractionType.Item;
    public string Name { get; private set; }

    public DeactivateInteraction()
    {
        Name = "Deactivate";
    }

    public void ExecuteInteraction(Item item, Inventory inventory)
    {
        if (item.IsTool)
        {
            item.IsActiveInInventory = false;
            Debug.Log($"{item.ItemInGameName} is now inactive in inventory.");
        }
    }

    public bool IsAvailable(Item item, Inventory inventory)
    {
        return item.IsTool && item.IsActiveInInventory;
    }
}

    public class DropInteraction : IItemInteraction
    {
        public InteractionType Type => InteractionType.Item;
        public string Name { get; private set; }

        public DropInteraction()
        {
            Name = "Drop";
        }

        public void ExecuteInteraction(Item item, Inventory inventory)
        {
            if (!PlayerStats.Instance.IsInNestedArea)
            {
                Debug.LogWarning("DropInteraction: Cannot drop items outside of a nested area.");
                return;
            }

            // Remove the item from the inventory
            inventory.RemoveItem(item.ItemInGameName, 1);

            // Get the facing cell from PlayerStats
            Cell facingCell = PlayerStats.Instance.FacingCell;

            if (facingCell == null)
            {
                Debug.LogWarning("DropInteraction: Facing cell is null. Cannot drop item.");
                return;
            }

            // Add the item to the cell's item list
            facingCell.Items.Add(item);
            Debug.Log($"{item.ItemInGameName} has been dropped at {facingCell.Coordinates}.");
        }

        public bool IsAvailable(Item item, Inventory inventory)
        {
            return PlayerStats.Instance.IsInNestedArea; // Ensure dropping is only allowed in nested areas
        }
    }

    public class DeseedInteraction : IItemInteraction
{
    public InteractionType Type => InteractionType.Item;
    public string Name { get; private set; }

    public DeseedInteraction()
    {
        Name = "Deseed";
    }

    public void ExecuteInteraction(Item item, Inventory inventory)
    {
        if (item.ItemTypes.Contains(ItemType.Fruit))
        {
            Debug.Log($"Deseeding {item.ItemInGameName}");

            // Create seeds based on the fruit's name
            Item seeds = new Item
            {
                Name = $"{item.Name} Seeds",
                SeedType = item.Name,
                ItemTypes = new List<ItemType> { ItemType.Seed },
                Description = $"Seeds extracted from a {item.Name}"
            };

            // Remove the fruit and add the seeds to the inventory
            inventory.RemoveItem(item.ItemInGameName, 1);
            inventory.AddItem(seeds);

            Debug.Log($"{item.ItemInGameName} has been deseeded. {seeds.Name} added to inventory.");
        }
    }

    public bool IsAvailable(Item item, Inventory inventory)
    {
        bool isFruit = item.ItemTypes.Contains(ItemType.Fruit);
        bool hasKnife = inventory.HasItem("Skinning Knife") || inventory.HasItem("Pocket Knife");
        return isFruit && hasKnife;
    }
}

#endregion
