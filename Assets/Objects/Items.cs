using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public interface IWeapon
{
    WeaponType WeaponType { get; set; }
    DamageType DamageType { get; set; }
    float LifeStealPercentage { get; set; }
    int DamageOutput { get; set; }
}

public interface ITool
{
    bool IsTool { get; set; }
}

public interface IWearable
{
    List<EquipmentSlot> EquipmentSlots { get; set; }
    int ArmourValue { get; set; }
}

public interface IContainerItem
{
    List<Item> Contents { get; set; }
    void AddItem(Item item);
    Item RemoveItem(string itemName);
}

public interface IEdible
{
    int HungerValue { get; set; }
    int ThirstValue { get; set; }
    bool IsEdible { get; set; }
    void Consume();
}

public interface IComponent
{
    string ComponentName { get; set; }
}

public interface ISeed
{
    string SeedType { get; set; }
}

public class WeaponComponent : Item, IComponent
{
    public string ComponentName { get; set; }

    public WeaponComponent(string name, string componentName, ObjectMaterial material)
    {
        Name = name;
        ComponentName = componentName;
        Material = material;
        ItemTypes = new List<ItemType> { ItemType.Component };
    }
}

public class Item : IWeapon, ITool, IWearable, IContainerItem, IEdible, ISeed
{
    public int ItemID { get; set; }
    public string Name { get; set; }
    private string itemInGameName;
    public string ItemInGameName
    {
        get => string.IsNullOrEmpty(itemInGameName) ? Name : itemInGameName;
        set => itemInGameName = value;
    }
    public string Description { get; set; }
    public ObjectMaterial Material { get; set; }
    public string PrimaryStat { get; set; }
    public string SecondaryStat { get; set; }
    public bool IsUnique { get; set; } = false;
    public bool IsIdentified { get; set; } = true;
    public bool IsHistoric { get; set; } = false;
    public bool IsActive { get; set; } = false;
    public bool IsActiveInInventory { get; set; } = false;
    public int Quantity { get; set; } = 1;
    public int Value { get; set; } = 0;
    public bool IsTradable { get; set; } = true;
    public bool Reserved { get; set; } = false;
    public List<ItemType> ItemTypes { get; set; } = new List<ItemType>();
    public List<string> Interfaces { get; set; } = new List<string>();
    public List<MaterialType> ExcludedMaterialTypes { get; set; } = new List<MaterialType>();
    public List<ComponentRequirement> ComponentsRequired { get; set; } = new List<ComponentRequirement>();
    public string ObjectString { get; set; }
    public List<Item> Components { get; set; } = new List<Item>();

    // Equipment and Active-In-Inventory properties
    public bool IsEquipped { get; set; }

    // IWeapon properties
    public WeaponType WeaponType { get; set; } = WeaponType.None;
    public DamageType DamageType { get; set; } = DamageType.None;
    public float LifeStealPercentage { get; set; } = 0f;
    public int DamageOutput { get; set; } = 0;

    // IWearable properties
    public List<EquipmentSlot> EquipmentSlots { get; set; } = new List<EquipmentSlot>();
    public int ArmourValue { get; set; } = 0;

    // ITool properties
    public bool IsTool { get; set; } = false;

    // IContainerItem properties
    public List<Item> Contents { get; set; } = new List<Item>();

    // Stat modifiers dictionary
    public Dictionary<string, int> StatModifiers { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, BuffDebuff> Modifiers { get; set; } = new Dictionary<string, BuffDebuff>();
    public List<OnHitEffect> OnHitTakenEffects { get; set; } = new List<OnHitEffect>();
    public List<OnHitEffect> OnHitEffects { get; set; } = new List<OnHitEffect>();

    public int ItemLevel { get; set; } 
    public int ModifierPoints { get; set; } 

    // Damage resistance dictionary (percentage values)
    public Dictionary<string, float> Resistances { get; set; } = new Dictionary<string, float>();

    // ISeed properties
    public string SeedType { get; set; }

    public void AddItem(Item item)
    {
        if (item != null)
        {
            Contents.Add(item);
        }
    }

    public Item RemoveItem(string itemName)
    {
        var item = Contents.Find(i => i.Name == itemName);
        if (item != null)
        {
            Contents.Remove(item);
        }
        return item;
    }

    // IEdible properties
    public int HungerValue { get; set; } = 0;
    public int ThirstValue { get; set; } = 0;
    public bool IsEdible { get; set; } = false;
    public int ConsumptionCapacityCost { get; set; } = 1;

    public void Consume()
    {
        Debug.Log($"Consuming {Name}. Hunger: {HungerValue}, Thirst: {ThirstValue}");
    }

    public List<IItemInteraction> GetAvailableInteractions(PlayerInventory inventory)
    {
        var interactions = new List<IItemInteraction>();

        // Debug: Log current item and interfaces it implements
        Debug.Log($"Getting interactions for item: {Name}");

        if (this is IWeapon || this is IWearable)
        {
            var equipInteraction = new EquipInteraction();
            if (equipInteraction.IsAvailable(this, inventory))
            {
                Debug.Log($"Equip interaction available for item: {Name}");
                interactions.Add(equipInteraction);
            }

            if (IsEquipped)
            {
                var unequipInteraction = new UnEquipInteraction();
                if (unequipInteraction.IsAvailable(this, inventory))
                {
                    Debug.Log($"Unequip interaction available for item: {Name}");
                    interactions.Add(unequipInteraction);
                }
            }
        }


        if (this is IEdible)
        {
            var consumeInteraction = new ConsumeInteraction();
            if (consumeInteraction.IsAvailable(this, inventory))
            {
                Debug.Log($"Consume interaction available for item: {Name}");
                interactions.Add(consumeInteraction);
            }
        }

        var dropInteraction = new DropInteraction();
        if (dropInteraction.IsAvailable(this, inventory))
        {
            Debug.Log($"Drop interaction available for item: {Name}");
            interactions.Add(dropInteraction);
        }

        Debug.Log($"Total interactions for {Name}: {interactions.Count}");
        return interactions;
    }

    public void AddModifier(BuffDebuff modifier)
    {
        if (modifier == null) return;

        string key = modifier.AffectedStat ?? modifier.AffectedResistance?.ToString();
        if (key == null) return;

        if (Modifiers.ContainsKey(key))
        {
            Debug.Log($"Replacing existing modifier: {Modifiers[key]} with {modifier}");
        }
        else
        {
            Debug.Log($"Adding new modifier: {modifier}");
        }

        Modifiers[key] = modifier;
    }

    public void RemoveModifier(string stat)
    {
        if (Modifiers.ContainsKey(stat))
        {
            Modifiers.Remove(stat);
        }
    }

   public void UpdateItemName()
	{
    if (PermaLists.Instance.ItemNamingData == null)
    {
        Debug.LogError("ItemNamingData not loaded!");
        return;
    }

    string materialName = (this.Material != null && !string.IsNullOrWhiteSpace(this.Material.MaterialName))
        ? this.Material.MaterialName
        : null;

    string baseName = materialName != null ? $"{materialName} {Name}" : Name;

    string prefix = "";
    string suffix = "";
    var impactScores = new Dictionary<string, float>();

    foreach (var resistance in Resistances)
        impactScores[resistance.Key] = resistance.Value;

    foreach (var modifier in Modifiers)
        impactScores[modifier.Key] = modifier.Value.EffectAmount;

    if (DamageType != DamageType.None)
        impactScores[DamageType.ToString()] = 10;

    foreach (var effect in OnHitEffects)
        impactScores[effect.EffectName] = 8;

    foreach (var effect in OnHitTakenEffects)
        impactScores[effect.EffectName] = 5;

    var sorted = impactScores.OrderByDescending(x => Mathf.Abs(x.Value)).ToList();

    if (sorted.Count > 0)
    {
        prefix = sorted[0].Key;
        sorted.RemoveAt(0);
    }

    if (sorted.Count > 0)
        suffix = string.Join(" and ", sorted.Select(x => x.Key));

    prefix = GetModifierName(prefix, impactScores);
    suffix = GetModifierName(suffix, impactScores);

    // Build and tidy up whitespace
    ItemInGameName = $"{prefix} {baseName} {suffix}".Trim();
    while (ItemInGameName.Contains("  "))
        ItemInGameName = ItemInGameName.Replace("  ", " ");

    Debug.Log($"Updated Item Name: {ItemInGameName}");
	}


    private string GetModifierName(string stat, Dictionary<string, float> impactScores)
    {
        if (string.IsNullOrEmpty(stat) || !impactScores.ContainsKey(stat))
            return "";

        float effectValue = impactScores[stat];

        if (PermaLists.Instance.ItemNamingData.Prefixes.TryGetValue(stat, out var prefixList))
        {
            foreach (var nameThreshold in prefixList)
            {
                if (effectValue >= nameThreshold.Min && effectValue <= nameThreshold.Max)
                {
                    return nameThreshold.Name;
                }
            }
        }

        if (PermaLists.Instance.ItemNamingData.Suffixes.TryGetValue(stat, out var suffixList))
        {
            foreach (var nameThreshold in suffixList)
            {
                if (effectValue >= nameThreshold.Min && effectValue <= nameThreshold.Max)
                {
                    return nameThreshold.Name;
                }
            }
        }

        return "";
    }

    private (string prefix, string suffix) DetermineMostImpactfulModifier()
    {
        Dictionary<string, float> impactScores = new Dictionary<string, float>();

        foreach (var resistance in Resistances)
        {
            impactScores[resistance.Key] = resistance.Value;
        }

        foreach (var modifier in Modifiers)
        {
            float weightedValue = modifier.Value.EffectAmount * 2;
            impactScores[modifier.Key] = weightedValue;
        }

        if (DamageType != DamageType.None)
        {
            impactScores[DamageType.ToString()] = 10;
        }

        foreach (var effect in OnHitEffects)
        {
            impactScores[effect.EffectName] = 8;
        }

        foreach (var effect in OnHitTakenEffects)
        {
            impactScores[effect.EffectName] = 5;
        }


        var sortedModifiers = impactScores
            .OrderByDescending(x => Mathf.Abs(x.Value))
            .ThenByDescending(x => IsStat(x.Key)) 
            .ThenByDescending(x => x.Value > 0) 
            .ThenBy(x => GetStatPriority(x.Key))
            .ToList();

        string prefix = sortedModifiers.Count > 0 ? GetModifierName(sortedModifiers[0].Key, impactScores) : "";
        string suffix = sortedModifiers.Count > 1 ? GetModifierName(sortedModifiers[1].Key, impactScores) : "";

        return (prefix, suffix);
    }

    private bool IsStat(string key)
    {
        List<string> stats = new List<string>
    {
        "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Perception", "Charisma", "Luck"
    };
        return stats.Contains(key);
    }

    private int GetStatPriority(string stat)
    {
        Dictionary<string, int> priorityOrder = new Dictionary<string, int>
    {
        { "Strength", 1 },
        { "Dexterity", 2 },
        { "Constitution", 3 },
        { "Intelligence", 4 },
        { "Wisdom", 5 },
        { "Perception", 6 },
        { "Charisma", 7 },
        { "Luck", 8 },
        { "FireDamage", 9 },
        { "IceDamage", 10 },
        { "LightningDamage", 11 },
        { "PoisonDamage", 12 },
        { "OnHitEffects", 13 },
        { "OnHitTakenEffects", 14 },
        { "Resistances", 15 }
    };

        return priorityOrder.TryGetValue(stat, out int priority) ? priority : 99;
    }


}

// Enum definitions
public enum WeaponType
    {
        Ranged,
        Sharp,
        Blunt,
        Serrated,
        Magic,
        None
    }

    public enum EquipmentSlot
    {
        Head,
        Face,
        Body,
        Legs,
        MainHand,
        OffHand,
        Feet,
        Neck,
        Waist
    }

    public enum ItemType
    {
        Consumable,
        Clothing,
        Equipment,
        Edible,
        Weapon,
        Armour,
        Meat,
        Fish,
        Vegetable,
        Fruit,
        TreeFruit,
        VineFruit,
        BushFruit,
        Water,
        BuildingMaterial,
        CraftingMaterial,
        AlchemicalIngredient,
        Constructable,
        Craftable,
        Component,
        Wood,
        Stone,
        Junk,
        Tool,
        Container,
        Fungi,
        Ingredient,
        Gemstone,
        Ore,
        Instrument,
        Bone,
        Meal,
        Pelt,
        Seed,
        Miscellaneous
    }

    public enum ItemSize
    {
        Tiny,
        Small,
        Medium,
        Large,
        Huge
    }

    public enum DamageType
    {
        // Physical Damage Types
        Piercing,    // Arrows, Spears, Stab attacks
        Slashing,    // Swords, Claws, Axes
        Bludgeoning, // Maces, Clubs, Warhammers
        Crushing,    // Boulders, Giant's stomp, Siege weapons
        Rending,     // Torn flesh, deep cuts, lacerations
        Blunt,
        Unarmed,     // Punches, Kicks, Martial arts

        // Elemental Damage Types
        Fire,        // Flames, Lava, Burning effects
        Ice,         // Frostbite, Blizzard, Ice Spears
        Lightning,   // Electric shocks, Thunder magic
        Earth,       // Rockfall, Quakes, Petrification
        Water,       // High-pressure water jets, Drowning
        Wind,        // Tornado slashes, Air pressure damage

        // Magic & Supernatural Damage Types
        Magic,       // Pure magical force, Arcane blasts
        Holy,        // Divine energy, Healing burn, Smite
        Unholy,      // Necrotic energy, Curses, Dark magic
        Poison,      // Toxins, Venom, Slow-acting damage
        Acid,        // Corrosive substances, Melting
        Psychic,     // Mental attacks, Sanity damage
        Shadow,      // Dark energy, Void attacks
        Radiant,     // Light-based damage, Solar flares
        Sonic,       // Sound waves, Thunderous booms

        // Miscellaneous Damage Types
        True,        // **Ignores resistances**, absolute damage
        Chaos,       // Randomized effects, Reality warping
        Disease,     // Plague, Infection, Rot
        Bleeding,    // Ongoing HP loss from wounds
        Explosion,   // Shockwave, Heat, Debris impact
        Drowning,    // Water suffocation damage

        // Default / Uncategorized
        None         // No damage type (for debugging or placeholder)
    }
