using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemGenerator : MonoBehaviour
{
    public static ItemGenerator Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public Item GenerateItem(string itemName)
    {
        var itemData = GetItemData(itemName);
        if (itemData == null)
        {
            Debug.LogError($"Item '{itemName}' not found in creation data.");
            return null;
        }

        return ItemFactory.CreateItem(itemData);
    }

    public Item GenerateRandomItem(ItemType itemType)
    {
        List<ItemCreationData> itemsOfType;

        // Allow multiple types for all item searches
        itemsOfType = PermaLists.Instance.ItemCreationData
            .Where(item => item.ItemTypes.Contains(itemType)) // No Count == 1 restriction
            .ToList();

        if (itemsOfType.Count == 0)
        {
            Debug.LogError($"No items of type '{itemType}' found in creation data.");
            return null;
        }

        var itemData = itemsOfType[UnityEngine.Random.Range(0, itemsOfType.Count)];
        return ItemFactory.CreateItem(itemData);
    }

    private ItemCreationData GetItemData(string itemName)
    {
        if (PermaLists.Instance.ItemCreationData == null)
        {
            Debug.LogError("ItemCreationData is not loaded!");
            return null;
        }

        return PermaLists.Instance.ItemCreationData
            .Find(item => item.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));
    }


    public Item GenerateAnimalLootItem(string itemName, string animalName)
    {
        var itemData = GetItemData(itemName);
        if (itemData == null)
        {
            Debug.LogError($"Item '{itemName}' not found in creation data.");
            return null;
        }

        // Create the item using the ItemFactory
        var item = ItemFactory.CreateItem(itemData);
        if (item == null)
        {
            Debug.LogError($"Failed to create item '{itemName}' for animal '{animalName}'.");
            return null;
        }

        // Set the InGameItemName to "AnimalName Item", e.g., "Camel Bone"
        item.ItemInGameName = $"{animalName} {item.Name}";

        Debug.Log($"Generated Animal Loot Item: {item.ItemInGameName}");
        return item;
    }

    public Item GenerateRandomAnimalLootItem(ItemType itemType, string animalName)
    {
        // Use the existing method to generate a random item based on itemType
        var randomItem = GenerateRandomItem(itemType);
        if (randomItem == null)
        {
            Debug.LogError($"Failed to generate random item of type '{itemType}' for animal '{animalName}'.");
            return null;
        }

        // Set the InGameItemName to "AnimalName Item", e.g., "Camel Bone"
        randomItem.UpdateItemName();
        randomItem.ItemInGameName = $"{animalName} {randomItem.Name}";

        Debug.Log($"Generated Random Animal Loot Item: {randomItem.ItemInGameName}");
        return randomItem;
    }

    public Item GenerateRandomWeaponOrEquipment(int level = 1)
    {
        bool generateWeapon = UnityEngine.Random.value < 0.5f; // 50% chance to generate either
        return generateWeapon ? GenerateRandomWeapon(level) : GenerateRandomEquipment(level);
    }

    public List<Item> GenerateMonsterLoot(string monsterName, int monsterLevel, bool isBossMonster)
    {
        List<Item> loot = new List<Item>();

        // Boss monsters drop more loot
        int lootCount = UnityEngine.Random.Range(2, 5 + (monsterLevel / 5)); // Bosses drop extra items

        for (int i = 0; i < lootCount; i++)
        {
            float roll = UnityEngine.Random.value;

            if (isBossMonster)
            {
                roll -= 0.2f; // Skews the roll towards better loot (Weapons & Equipment)
            }

            if (roll < 0.55f) // Increased chance for weapons or equipment (55% for bosses, 40% otherwise)
            {
                Item weaponOrEquipment = GenerateRandomWeaponOrEquipment(monsterLevel);

                // Bosses have a 50% chance to receive an extra item boost
                if (isBossMonster && UnityEngine.Random.value < 0.5f)
                {
                    ApplyRandomModifiers(weaponOrEquipment); // Enhancing weapon/equipment
                }

                loot.Add(weaponOrEquipment);
            }
            else if (roll < 0.8f) // 25% chance for junk loot (normal monsters get 30%)
            {
                loot.Add(GenerateRandomItem(ItemType.Junk));
            }
            else // 20% chance for monster-themed loot (normal monsters get 30%)
            {
                ItemType lootType = UnityEngine.Random.value < 0.5f ? ItemType.Meat : ItemType.Bone;
                loot.Add(GenerateRandomAnimalLootItem(lootType, monsterName));
            }
        }

        Debug.Log($"Generated {loot.Count} loot items for {monsterName} (Level {monsterLevel}) | Boss: {isBossMonster}");
        return loot;
    }


    public Item GenerateRandomWeapon(int level = 1)
    {
        List<ItemCreationData> weapons = PermaLists.Instance.ItemCreationData
            .Where(item => item.ItemTypes.Contains(ItemType.Weapon))
            .ToList();

        if (weapons.Count == 0)
        {
            Debug.LogError("No weapons found in item creation data.");
            return null;
        }

        var weaponData = weapons[UnityEngine.Random.Range(0, weapons.Count)];
        Item weapon = ItemFactory.CreateItem(weaponData);

        weapon.ItemLevel = level;

        // Get the min/max points for the item's level
        if (LevelToPointsRange.TryGetValue(level, out var pointRange))
        {
            weapon.ModifierPoints = UnityEngine.Random.Range(pointRange.minPoints, pointRange.maxPoints + 1);
        }
        else
        {
            weapon.ModifierPoints = 0; // Fallback if level is out of bounds
        }

        ApplyDebuffs(weapon);  // Apply debuffs first to gain extra points
        ApplyModifiersBasedOnPoints(weapon);  // Spend available points on buffs
        ApplyRandomDamageType(weapon);

        weapon.UpdateItemName();

        Debug.Log($"Generated Level {level} Weapon with {weapon.ModifierPoints} Points");
        return weapon;
    }

    public Item GenerateRandomEquipment(int level = 1)
    {
        List<ItemCreationData> equipmentDataList = PermaLists.Instance.ItemCreationData
            .Where(item => item.ItemTypes.Contains(ItemType.Armour) || item.ItemTypes.Contains(ItemType.Clothing))
            .ToList();

        if (equipmentDataList.Count == 0)
        {
            Debug.LogError("No equipment found in item creation data.");
            return null;
        }

        var equipmentData = equipmentDataList[UnityEngine.Random.Range(0, equipmentDataList.Count)];
        Item equipment = ItemFactory.CreateItem(equipmentData);

        equipment.ItemLevel = level;

        // Assign a random points value within the level's range
        if (LevelToPointsRange.TryGetValue(level, out var pointRange))
        {
            equipment.ModifierPoints = UnityEngine.Random.Range(pointRange.minPoints, pointRange.maxPoints + 1);
        }
        else
        {
            equipment.ModifierPoints = 0;
        }

        ApplyDebuffs(equipment);
        ApplyResistancesBasedOnPoints(equipment);
        ApplyModifiersBasedOnPoints(equipment);
        ApplyRandomOnHitTakenEffects(equipment);

        equipment.UpdateItemName();

        Debug.Log($"Generated Level {level} Equipment with {equipment.ModifierPoints} Points");
        return equipment;
    }


    public void ApplyRandomResistances(Item armor)
    {
        if (armor == null || (!armor.ItemTypes.Contains(ItemType.Armour) && !armor.ItemTypes.Contains(ItemType.Clothing))) return;

        if (UnityEngine.Random.Range(0, 100) < 40) // 40% chance for resistance
        {
            List<DamageType> resistanceTypes = new List<DamageType> { DamageType.Fire, DamageType.Ice, DamageType.Lightning, DamageType.Poison };
            DamageType chosenResistance = resistanceTypes[UnityEngine.Random.Range(0, resistanceTypes.Count)];

            armor.Resistances[chosenResistance.ToString()] = UnityEngine.Random.Range(5, 20);
            Debug.Log($"{armor.Name} has gained resistance to {chosenResistance}");
        }
    }

    public void ApplyResistancesBasedOnPoints(Item armor)
    {
        if (armor == null || (!armor.ItemTypes.Contains(ItemType.Armour) && !armor.ItemTypes.Contains(ItemType.Clothing))) return;

        int availablePoints = armor.ModifierPoints;
        List<string> resistanceModifiers = ModifierCategories["Resistances"].Keys.ToList();

        while (availablePoints > 0 && resistanceModifiers.Count > 0)
        {
            string selectedResistance = resistanceModifiers[UnityEngine.Random.Range(0, resistanceModifiers.Count)];
            var (costPerUnit, minValue, maxValue) = ModifierCategories["Resistances"][selectedResistance];

            int maxAffordableResistance = Mathf.FloorToInt(availablePoints / costPerUnit) * 5;
            int resistanceValue = Mathf.Clamp(UnityEngine.Random.Range((int)minValue, maxAffordableResistance + 1), (int)minValue, (int)maxValue);

            int finalCost = Mathf.RoundToInt((resistanceValue / 5) * costPerUnit);

            if (finalCost > availablePoints)
                break;  // Prevents infinite loops where no modifiers can be applied

            armor.Resistances[selectedResistance] = resistanceValue;
            availablePoints -= finalCost;

            Debug.Log($"Applied {selectedResistance} (+{resistanceValue} Resistance), Cost: {finalCost}, Remaining Points: {availablePoints}");

            resistanceModifiers.Remove(selectedResistance); // Prevent duplicates
        }
    }


    public void ApplyRandomDamageType(Item weapon)
    {
        if (weapon == null || !weapon.ItemTypes.Contains(ItemType.Weapon)) return;

        if (UnityEngine.Random.Range(0, 100) < 30) // 30% chance to gain elemental damage
        {
            List<DamageType> elementalTypes = new List<DamageType> { DamageType.Fire, DamageType.Ice, DamageType.Lightning, DamageType.Poison };
            DamageType chosenType = elementalTypes[UnityEngine.Random.Range(0, elementalTypes.Count)];
            weapon.DamageType = chosenType;

            weapon.DamageOutput += UnityEngine.Random.Range(3, 10);

            Debug.Log($"{weapon.Name} has gained {chosenType} Damage and increased damage output.");
        }
    }

    public void ApplyRandomOnHitTakenEffects(Item armor)
    {
        if (armor == null || (!armor.ItemTypes.Contains(ItemType.Armour) && !armor.ItemTypes.Contains(ItemType.Clothing))) return;

        if (UnityEngine.Random.Range(0, 100) < 30) // 30% chance for an On-Hit Taken effect
        {
            if (PermaLists.Instance.OnHitEffects.Count == 0)
            {
                Debug.LogWarning("No On-Hit Taken Effects found in PermaLists!");
                return;
            }

            // Select a random effect from PermaLists
            OnHitEffect chosenEffect = PermaLists.Instance.OnHitEffects[UnityEngine.Random.Range(0, PermaLists.Instance.OnHitEffects.Count)];
            armor.OnHitTakenEffects.Add(chosenEffect);

            Debug.Log($"{armor.Name} has gained On-Hit Taken Effect: {chosenEffect.EffectName}");
        }
    }


    public void ApplyRandomModifiers(Item item)
    {
        if (item == null) return;

        int modifierCount = UnityEngine.Random.Range(1, 3); // 1-2 modifiers
        List<string> availableModifiers = new List<string>();

        if (item.ItemTypes.Contains(ItemType.Weapon))
        {
            availableModifiers.AddRange(new List<string> { "Strength", "Dexterity", "CriticalChance", "PhysicalDamage" });
        }
        else if (item.ItemTypes.Contains(ItemType.Armour) || item.ItemTypes.Contains(ItemType.Clothing))
        {
            availableModifiers.AddRange(new List<string> { "Constitution", "PhysicalResistance", "FireResistance", "IceResistance", "PoisonResistance" });
        }

        for (int i = 0; i < modifierCount; i++)
        {
            string selectedModifier = availableModifiers[UnityEngine.Random.Range(0, availableModifiers.Count)];
            float effectValue = UnityEngine.Random.Range(3, 15);

            item.Modifiers.Add(selectedModifier, new BuffDebuff(
                selectedModifier,
                "Random Generation",
                selectedModifier,
                ModifierType.Flat,
                effectValue,
                -1 // Permanent
            ));

            Debug.Log($"Applied modifier: {selectedModifier} (+{effectValue}) to {item.Name}");
        }
    }

    public void ApplyRandomOnHitEffects(Item weapon)
    {
        if (weapon == null || !weapon.ItemTypes.Contains(ItemType.Weapon)) return;

        if (UnityEngine.Random.Range(0, 100) < 30) // 30% chance for an On-Hit Effect
        {
            List<(OnHitEffect effect, int weight)> weightedEffects = new List<(OnHitEffect, int)>
        {
            (new BleedEffect(3, 5), 5),
            (new StunEffect(2), 5),
            (new BurnEffect(3, 4), 5),
            (new ShockEffect(2, 5), 5),
            (new WeaknessEffect(4, 3), 5)
        };

            // Increase the weight based on weapon type
            switch (weapon.WeaponType)
            {
                case WeaponType.Sharp:
                    weightedEffects.Add((new BleedEffect(3, 5), 10));
                    break;
                case WeaponType.Blunt:
                    weightedEffects.Add((new StunEffect(2), 10));
                    break;
                case WeaponType.Serrated:
                    weightedEffects.Add((new BleedEffect(4, 6), 12)); // Stronger Bleed Effect
                    break;
                case WeaponType.Magic:
                    weightedEffects.Add((new BurnEffect(3, 4), 10));
                    weightedEffects.Add((new ShockEffect(2, 5), 10));
                    break;
                case WeaponType.Ranged:
                    weightedEffects.Add((new WeaknessEffect(4, 3), 10)); // Weakness from piercing shots
                    break;
            }

            // Weighted selection
            int totalWeight = weightedEffects.Sum(e => e.weight);
            int randomNumber = UnityEngine.Random.Range(0, totalWeight);
            int cumulativeWeight = 0;

            foreach (var (effect, weight) in weightedEffects)
            {
                cumulativeWeight += weight;
                if (randomNumber < cumulativeWeight)
                {
                    weapon.OnHitEffects.Add(effect);
                    Debug.Log($"{weapon.Name} has gained On-Hit Effect: {effect.EffectName}");
                    return;
                }
            }
        }
    }

    public void ApplyDebuffs(Item item)
    {
        int totalDebuffPoints = 0;
        float debuffChance = 0.3f;

        if (UnityEngine.Random.value < debuffChance)
        {
            List<string> possibleDebuffs = new List<string> { "Strength", "Dexterity", "Constitution", "Speed", "Luck", "Wisdom", "Intelligence" };

            if (!string.IsNullOrWhiteSpace(item.PrimaryStat) && item.Modifiers.ContainsKey(item.PrimaryStat))
                possibleDebuffs.Remove(item.PrimaryStat);
            if (!string.IsNullOrWhiteSpace(item.SecondaryStat) && item.Modifiers.ContainsKey(item.SecondaryStat))
                possibleDebuffs.Remove(item.SecondaryStat);

            if (possibleDebuffs.Count == 0)
                return; // No valid debuff options left

            string selectedDebuff = possibleDebuffs[UnityEngine.Random.Range(0, possibleDebuffs.Count)];

            var (costPerUnit, minValue, maxValue) = ModifierCategories["Stats"][selectedDebuff];
            int debuffValue = UnityEngine.Random.Range((int)minValue, (int)(maxValue / 2)) * -1;
            int refundedPoints = Mathf.RoundToInt(Mathf.Abs(debuffValue) * costPerUnit);

            item.Modifiers[selectedDebuff] = new BuffDebuff(
                selectedDebuff,
                "Random Generation",
                selectedDebuff,
                ModifierType.Flat,
                debuffValue,
                -1
            );

            totalDebuffPoints += refundedPoints;
            Debug.Log($"Applied DEBUFF {selectedDebuff} ({debuffValue}), Refunded Points: {refundedPoints}");
        }

        item.ModifierPoints += totalDebuffPoints;
    }


    public void ApplyModifiersBasedOnPoints(Item item)
    {
        if (item == null) return;

        int availablePoints = item.ModifierPoints;
        if (availablePoints <= 0) return;

        string primaryPriority = RollPrimaryPriority();
        Dictionary<string, int> spentPoints = new Dictionary<string, int> { { "Stats", 0 }, { "Elemental", 0 }, { "OnHit", 0 }, { "Utility", 0 } };

        switch (primaryPriority)
        {
            case "Stat-Heavy":
                SpendPointsOnCategory(item, "Stats", ref availablePoints, 80, spentPoints);
                break;
            case "Elemental-Focused":
                SpendPointsOnCategory(item, "Elemental", ref availablePoints, 80, spentPoints);
                break;
            case "Balanced Hybrid":
                SpendPointsOnCategory(item, "Stats", ref availablePoints, 40, spentPoints);
                SpendPointsOnCategory(item, "Elemental", ref availablePoints, 40, spentPoints);
                break;
            case "On-Hit Specialist":
                SpendPointsOnCategory(item, "OnHit", ref availablePoints, 100, spentPoints);
                break;
        }

        while (availablePoints > 0)
        {
            string leftoverPriority = RollLeftoverPriority();
            SpendPointsOnCategory(item, leftoverPriority, ref availablePoints, 100, spentPoints);
        }

        Debug.Log($"Final Modifiers for {item.Name}: {string.Join(", ", spentPoints.Select(kv => kv.Key + ": " + kv.Value))}");
    }


    private string RollPrimaryPriority()
    {
        int roll = UnityEngine.Random.Range(0, 100);
        if (roll < 40) return "Stat-Heavy";
        if (roll < 65) return "Elemental-Focused";
        if (roll < 85) return "Balanced Hybrid";
        return "On-Hit Specialist";
    }

    private string RollLeftoverPriority()
    {
        int roll = UnityEngine.Random.Range(0, 100);
        if (roll < 40) return "Fill More of Primary Focus";
        if (roll < 60) return "Elemental";
        if (roll < 80) return "OnHit";
        return "Utility";
    }

    private void SpendPointsOnCategory(Item item, string category, ref int availablePoints, int percentage, Dictionary<string, int> spentPoints)
    {
        int pointsToSpend = Mathf.RoundToInt((availablePoints * percentage) / 100f);
        pointsToSpend = Mathf.Min(pointsToSpend, availablePoints);

        switch (category)
        {
            case "Stats":
                int statPoints = Mathf.Min(pointsToSpend, 3 * (pointsToSpend / 3));
                if (item.Modifiers.ContainsKey("Strength"))
                {
                    item.Modifiers["Strength"].ModifyEffectAmount(statPoints / 3);
                }
                else
                {
                    item.Modifiers["Strength"] = new BuffDebuff("Strength Boost", "ItemGen", "Strength", ModifierType.Flat, statPoints / 3, -1);
                }
                spentPoints["Stats"] += statPoints;
                availablePoints -= statPoints;
                break;

            case "Elemental":
                DamageType elementalType = GetRandomElementalType();
                int extraElementalDamage = pointsToSpend / 5;
                if (!item.Modifiers.ContainsKey("Damage"))
                {
                    item.Modifiers["Damage"] = new BuffDebuff("Elemental Boost", "ItemGen", "Damage", ModifierType.Flat, extraElementalDamage, -1);
                }
                else
                {
                    item.Modifiers["Damage"].ModifyEffectAmount(extraElementalDamage);
                }
                spentPoints["Elemental"] += pointsToSpend;
                availablePoints -= pointsToSpend;
                break;

            case "OnHit":
                if (item.OnHitEffects.Count < 2 || UnityEngine.Random.Range(0, 100) < 20)
                {
                    item.OnHitEffects.Add(GetRandomOnHitEffect());
                    spentPoints["OnHit"] += 10;
                    availablePoints -= 10;
                }
                break;

            case "Utility":
                if (!item.Modifiers.ContainsKey("Luck"))
                {
                    item.Modifiers["Luck"] = new BuffDebuff("Lucky Boost", "ItemGen", "Luck", ModifierType.Flat, pointsToSpend / 5, -1);
                }
                else
                {
                    item.Modifiers["Luck"].ModifyEffectAmount(pointsToSpend / 5);
                }
                spentPoints["Utility"] += pointsToSpend;
                availablePoints -= pointsToSpend;
                break;
        }
    }


    private DamageType GetRandomElementalType()
    {
        DamageType[] elementalTypes = { DamageType.Fire, DamageType.Ice, DamageType.Lightning, DamageType.Poison };
        return elementalTypes[UnityEngine.Random.Range(0, elementalTypes.Length)];
    }

    private OnHitEffect GetRandomOnHitEffect()
    {
        OnHitEffect[] effects = { new BleedEffect(3, 5), new SlownessEffect(3, 2) };
        return effects[UnityEngine.Random.Range(0, effects.Length)];
    }

    // Helper function to find the category of a modifier
    private string GetModifierCategory(string modifier)
    {
        foreach (var category in ModifierCategories.Keys)
        {
            if (ModifierCategories[category].ContainsKey(modifier))
                return category;
        }
        return null;
    }


    private static readonly Dictionary<int, (int minPoints, int maxPoints)> LevelToPointsRange = new()
    {
        { 1, (0, 0) },       // No modifiers at Level 1
        { 2, (2, 10) },      // Level 2 gets between 2 and 10 points
        { 3, (5, 15) },      // Level 3 gets between 5 and 15 points
        { 4, (10, 20) },     // Level 4 gets between 10 and 20 points
        { 5, (15, 30) },     // Level 5 gets between 15 and 30 points
        { 6, (20, 40) },     // Level 6 gets between 20 and 40 points
        { 7, (30, 50) },     // Level 7 gets between 30 and 50 points
        { 8, (40, 60) },     // Level 8 gets between 40 and 60 points
        { 9, (50, 75) },     // Level 9 gets between 50 and 75 points
        { 10, (60, 100) }    // Level 10 gets between 60 and 100 points
    };

    private static readonly Dictionary<string, Dictionary<string, (float costPerUnit, float minValue, float maxValue)>> ModifierCategories = new()
    {
        {
            "Stats",
            new()
            {
                { "Strength", (3, 1, 10) },
                { "Dexterity", (3, 1, 10) },
                { "Constitution", (3, 1, 10) },
                { "Wisdom", (3, 1, 10) },
                { "Intelligence", (3, 1, 10) },
                { "Luck", (3, 1, 10) }
            }
        },
        {
            "Damage",
            new()
            {
                { "PhysicalDamage", (1, 2, 20) }
            }
        },
        {
            "Resistances",
            new()
            {
                { "FireResistance", (0.2f, 5, 50) },
                { "IceResistance", (0.2f, 5, 50) },
                { "PoisonResistance", (0.2f, 5, 50) },
                { "LightningResistance", (0.2f, 5, 50) }
            }
        },
        {
            "OnHitEffects",
            new()
            {
                { "BleedEffect", (10, 1, 1) },
                { "SlownessEffect", (10, 1, 1) },
                { "BurnEffect", (10, 1, 1) },
                { "ShockEffect", (10, 1, 1) }
            }
        }
    };

}

public static class ItemFactory
{
    public static Item CreateItem(ItemCreationData itemData)
    {
        if (itemData == null)
        {
            Debug.LogError("ItemCreationData is null");
            return null;
        }

        int itemID = GameManager.Instance.GetItemID();

        Item item = new Item
        {
            ItemID = itemID,
            Name = itemData.Name,
            ItemTypes = itemData.ItemTypes ?? new List<ItemType>(),
            Description = itemData.Description,
            IsUnique = itemData.IsUnique,
            IsIdentified = itemData.IsIdentified,
            IsHistoric = itemData.IsHistoric,
            IsActive = itemData.IsActive,
            Quantity = itemData.Quantity > 0 ? itemData.Quantity : 1,
            Value = itemData.Value,
            IsTradable = true,
            Reserved = itemData.Reserved,
            WeaponType = itemData.WeaponType,
            DamageOutput = itemData.DamageOutput,
            ArmourValue = itemData.ArmourValue,
            HungerValue = itemData.HungerValue,
            ThirstValue = itemData.ThirstValue,
            Interfaces = itemData.Interfaces ?? new List<string>(),
            ExcludedMaterialTypes = itemData.ExcludedMaterialTypes ?? new List<MaterialType>(),
            ObjectString = itemData.ObjectString,
            ComponentsRequired = itemData.ComponentsRequired,
            IsEdible = itemData.IsEdible,
            EquipmentSlots = new List<EquipmentSlot>() // Always initialize the list
        };

        // Validate and assign EquipmentSlots properly
        item.EquipmentSlots = itemData.EquipmentSlots?
            .Select(slot => Enum.TryParse(slot.Trim(), true, out EquipmentSlot parsedSlot) ? parsedSlot : (EquipmentSlot?)null)
            .Where(slot => slot.HasValue)
            .Select(slot => slot.Value)
            .ToList() ?? new List<EquipmentSlot>();

        if (item.EquipmentSlots.Count == 0 && itemData.EquipmentSlots?.Count > 0)
        {
            Debug.LogWarning($"Item '{item.Name}' contains invalid EquipmentSlots: {string.Join(", ", itemData.EquipmentSlots)}");
        }

        // Check if the item type is in the excluded list
        if (!item.ItemTypes.Any(type => ExcludedItemTypesForMaterial.Contains(type)))
        {
            item.Material = GetRandomMaterial(itemData.ExcludedMaterialTypes ?? new List<MaterialType>(), item.ItemTypes.FirstOrDefault());
        }
   
        // Assign required components
        if (item.ComponentsRequired != null && item.ComponentsRequired.Any())
        {
            foreach (var requirement in item.ComponentsRequired)
            {
                var validMaterials = PermaLists.Instance.ObjectMaterials
                    .Where(m => requirement.AllowedMaterialTypes.Contains(m.Type) && !item.ExcludedMaterialTypes.Contains(m.Type))
                    .ToList();

                if (validMaterials.Any())
                {
                    var material = SelectMaterialBasedOnRarity(validMaterials);
                    var component = new Item
                    {
                        Name = requirement.ComponentName,
                        ItemTypes = new List<ItemType> { ItemType.Component },
                        Material = material // Assign the selected material to the component
                    };
                    item.Components.Add(component);
                }
                else
                {
                    Debug.LogWarning($"No valid materials found for component '{requirement.ComponentName}' in item '{item.Name}'");
                }
            }
        }
        else if (item.ItemTypes.Contains(ItemType.Weapon))
        {
            var smithingRecipe = PermaLists.Instance.SmithingRecipeList.FirstOrDefault(recipe => recipe.ResultingWeapon.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            if (smithingRecipe != null)
            {
                AddSmithingRecipeComponents(item, smithingRecipe);
            }
            else
            {
                Debug.LogWarning($"No smithing recipe found for weapon '{item.Name}'");
            }
        }

        // Apply Modifiers
        if (itemData.Modifiers != null)
        {
            foreach (var effect in itemData.Modifiers)
            {
                item.AddModifier(effect);
            }
        }

        item.ItemInGameName = GenerateInGameName(item);
        item.UpdateItemName();
        Debug.Log($"ItemFactory has created {item.ItemInGameName}, EquipSlots: {string.Join(", ", item.EquipmentSlots)}");
        return item;
    }

private static void AddSmithingRecipeComponents(Item item, SmithingRecipe smithingRecipe)
    {
        var bodyComponentData = PermaLists.Instance.ItemCreationData.FirstOrDefault(data => data.Name.Equals(smithingRecipe.BodyComponent, StringComparison.OrdinalIgnoreCase));
        var headComponentData = PermaLists.Instance.ItemCreationData.FirstOrDefault(data => data.Name.Equals(smithingRecipe.HeadComponent, StringComparison.OrdinalIgnoreCase));

        if (bodyComponentData != null)
        {
            var bodyComponent = CreateItem(bodyComponentData);
            item.Components.Add(bodyComponent);
        }
        else
        {
            Debug.LogWarning($"No item creation data found for body component '{smithingRecipe.BodyComponent}'");
        }

        if (headComponentData != null)
        {
            var headComponent = CreateItem(headComponentData);
            item.Components.Add(headComponent);
        }
        else
        {
            Debug.LogWarning($"No item creation data found for head component '{smithingRecipe.HeadComponent}'");
        }
    }

    private static ObjectMaterial SelectMaterialBasedOnRarity(List<ObjectMaterial> materials)
    {
        var sortedMaterials = materials.OrderBy(m => (int)m.Rarity).ToList();
        int totalWeight = sortedMaterials.Sum(m => (int)m.Rarity + 1);

        int randomNumber = UnityEngine.Random.Range(0, totalWeight);
        int cumulativeWeight = 0;

        foreach (var material in sortedMaterials)
        {
            cumulativeWeight += (int)material.Rarity + 1;
            if (randomNumber < cumulativeWeight)
            {
                return material;
            }
        }

        return sortedMaterials.Last();
    }

    private static ObjectMaterial GetRandomMaterial(List<MaterialType> excludedMaterialTypes, ItemType itemType)
    {
        // Determine the appropriate MaterialType list to search based on the itemType
        List<ObjectMaterial> materials;

        if (itemType == ItemType.Ore)
        {
            // Search for materials of type Metal
            materials = PermaLists.Instance.ObjectMaterials
                .Where(m => m.Type == MaterialType.Metal && !excludedMaterialTypes.Contains(m.Type))
                .ToList();
        }
        else if (itemType == ItemType.Gemstone)
        {
            // Search for materials of type Gemstone
            materials = PermaLists.Instance.ObjectMaterials
                .Where(m => m.Type == MaterialType.Gemstone && !excludedMaterialTypes.Contains(m.Type))
                .ToList();
        }
        else
        {
            // Default behavior: search all materials except excluded ones
            materials = PermaLists.Instance.ObjectMaterials
                .Where(m => !excludedMaterialTypes.Contains(m.Type))
                .ToList();
        }

        // Sort by rarity and perform weighted random selection as before
        var sortedMaterials = materials.OrderBy(m => (int)m.Rarity).ToList();
        int totalWeight = sortedMaterials.Sum(m => GetRarityWeight(m.Rarity));

        int randomNumber = UnityEngine.Random.Range(0, totalWeight);
        int cumulativeWeight = 0;

        foreach (var material in sortedMaterials)
        {
            cumulativeWeight += GetRarityWeight(material.Rarity);
            if (randomNumber < cumulativeWeight)
            {
                return material;
            }
        }

        return sortedMaterials.Last(); // Fallback, should not normally hit this point
    }


    private static int GetRarityWeight(MaterialRarity rarity)
    {
        return rarity switch
        {
            MaterialRarity.Common => 100,
            MaterialRarity.Uncommon => 30,
            MaterialRarity.Rare => 10,
            MaterialRarity.VeryRare => 1,
            MaterialRarity.Legendary => 1,
            _ => 0,
        };
    }

    private static string GenerateInGameName(Item item)
    {
        // Check if there are any components
        if (item.Components != null && item.Components.Any())
        {
            // Include the Material of each component in the name
            var componentNames = item.Components.Select(c =>
            {
                if (c.Material != null)
                {
                    return $"{c.Material.MaterialName} {c.Name}";
                }
                return c.Name;
            });
            return $"{item.Name} ({string.Join(", ", componentNames)})";
        }
        else
        {
            // Check if the item itself has a Material
            if (item.Material != null)
            {
                return $"{item.Material.MaterialName} {item.Name}";
            }
            return item.Name;
        }
    }

    private static readonly List<ItemType> ExcludedItemTypesForMaterial = new List<ItemType>
    {
    ItemType.Fruit,
    ItemType.Vegetable,
    ItemType.Meat,
    ItemType.Consumable,
    ItemType.Junk,
    ItemType.Container,
    ItemType.Fungi,
    ItemType.Meal,
    ItemType.Bone,
    ItemType.Pelt,
    ItemType.Miscellaneous,
    ItemType.BuildingMaterial,
    ItemType.Constructable,
    ItemType.CraftingMaterial
    };


    public static Item SmithItem(string smithedItemName, List<string> components)
    {
        var itemData = PermaLists.Instance.ItemCreationData.Find(item => item.Name.Equals(smithedItemName, StringComparison.OrdinalIgnoreCase));
        if (itemData == null)
        {
            Debug.LogError($"No item data found for item '{smithedItemName}'");
            return null;
        }

        Item newItem = new Item
        {
            Name = smithedItemName,
            IsUnique = itemData.IsUnique,
            IsIdentified = itemData.IsIdentified,
            IsHistoric = itemData.IsHistoric,
            IsActive = itemData.IsActive,
            Quantity = 1,
            Value = itemData.Value,
            IsTradable = itemData.IsTradable,
            Reserved = itemData.Reserved,
            ItemTypes = itemData.ItemTypes,
            Interfaces = itemData.Interfaces,
            Components = components.Select(componentName =>
            {
                var material = PermaLists.Instance.ObjectMaterials.FirstOrDefault(m => m.MaterialName == componentName);
                return new Item
                {
                    Name = componentName,
                    ItemTypes = new List<ItemType> { ItemType.Component }
                };
            }).ToList()
        };

        newItem.ItemInGameName = GenerateInGameName(newItem);

        return newItem;
    }
}
