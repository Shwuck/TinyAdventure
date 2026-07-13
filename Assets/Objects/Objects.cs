using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

// Interactable Object Interface
public interface IInteractable
{
    int IInteractableID { get; set; }
    string Description { get; set; }
    string Name { get; set; }
    char Symbol { get; } // Symbol representing the object on the map
    string Color { get; } // Color of the symbol for display
    Vector2Int Position { get; set; }
    Vector2Int NestedMapPosition { get; set; }
    bool IsActive { get; set; }
    bool IsInNestedArea { get; set; }
    INestedArea CurrentNestedArea { get; set; }
    bool IsHostile { get; set; }
    bool IsPassable { get; set; }
    CoverType CoverType { get; set; }

    IEnumerable<IInteraction> GetAvailableInteractions(PlayerInventory inventory);

    // Method to remove a single object from a nested area
    public void RemoveObjectFromNestedArea(IInteractable obj)
    {
        // Check if the object is currently in a nested area
        if (obj.IsInNestedArea && obj.CurrentNestedArea != null)
        {
            INestedArea nestedArea = obj.CurrentNestedArea;
            Cell[,] nestedMap = nestedArea.GetNestedMap();

            // Make sure the object's NestedMapPosition is valid within the nested map bounds
            if (obj.NestedMapPosition.x >= 0 && obj.NestedMapPosition.x < nestedMap.GetLength(0) &&
                obj.NestedMapPosition.y >= 0 && obj.NestedMapPosition.y < nestedMap.GetLength(1))
            {
                // Retrieve the cell where the object was located
                Cell cell = nestedMap[obj.NestedMapPosition.x, obj.NestedMapPosition.y];

                // Optionally, if objects block movement, you might want to make the cell passable again
                cell.isPassable = true;

                // If the cell contains a reference to the object in its Objects list, consider removing it
                cell.Objects.Remove(obj);

                // Add debugging information about object removal
                Debug.Log($"Object '{obj.Name}' removed from nested area at position {obj.NestedMapPosition}");

            }
            else
            {
                // Add debugging information if object NestedMapPosition is invalid
                Debug.LogWarning($"Object '{obj.Name}' position ({obj.NestedMapPosition}) is outside the bounds of the nested area.");
            }

            // Reset the object's nested area properties
            obj.IsInNestedArea = false;
            obj.CurrentNestedArea = null;

            // Add logging to indicate removal completion
            Debug.Log($"Object '{obj.Name}' removed from nested area.");
        }
        else
        {
            // Add debugging information if object is not in a nested area
            Debug.LogWarning($"Object '{obj.Name}' is not in a nested area.");
        }
    }
}

// Enum to define different cover types
public enum CoverType
{
    Full,     // NPCs can't see beyond the object
    Partial,  // NPCs' line of sight is partially obstructed
    None      // Object provides no cover
}

public interface IAnimated
{
    bool IsAnimated { get; set; }
    List<SymbolColorSet> AnimationFrames { get; set; } // Stores the different symbols and colors for animation
    int CurrentFrameIndex { get; set; } // Keeps track of the current frame in the animation sequence
    void Animate(); // Method to update the object's symbol and color based on the next frame
}

public interface IFlammable
{
    bool IsFlammable { get; set; }
    float Flammability { get; set; } // A value indicating how easily the object catches fire
    bool IsOnFire { get; set; } // Indicates whether the object is currently on fire

    void CatchFire(); // Method to handle the object catching fire
    void Extinguish(); // Optional: Method to handle extinguishing the fire
}

public interface IDestructible
{
    void Destroy();
}

public interface IContainer
{
    ContainerInventory Inventory { get; }

    void AddItem(Item item, int amount = 1);
    void RemoveItem(Item item, int amount = 1);
    Item RemoveItem(string itemName, int amount = 1);
    List<Item> GetItems();

    List<InventoryContainer> GetInventoryContainers();
}



public interface IActionable : IInteractable
{
    int Health { get; set; }
    int MaxHealth { get; set; }
    int ActionPoints { get; set; }
    int MaxActionPoints { get; set; }

    void ExecuteTurnActions();
    void OnTurnEnd();

    // Combat-related methods
    int GetAttackDamage(string primaryStat);
    int GetDefence();
    int GetResistance(string damageType);
    int GetCriticalHitChance();
    int GetCriticalHitMultiplier();
    bool IsPlayerInRange();
    void AttackPlayer();
}

public class SymbolColorSet
{
    public char Symbol { get; private set; }
    public string Color { get; private set; }

    public SymbolColorSet(char symbol, string color)
    {
        Symbol = symbol;
        Color = color;
    }
}

public abstract class BaseObject : IInteractable, IFlammable, IDestructible
{
    public int IInteractableID { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public virtual char Symbol { get; protected set; }
    public virtual string Color { get; protected set; }
    public virtual char DefaultSymbol { get; protected set; }
    public virtual string DefaultColor { get; protected set; }
    public bool IsActive { get; set; } = true;
    public bool IsHostile { get; set; } = false;
    public virtual bool IsPassable { get; set; } = true;
    public CoverType CoverType { get; set; } = CoverType.None;
    public Vector2Int Position { get; set; }
    public Vector2Int NestedMapPosition { get; set; }
    public bool IsInNestedArea { get; set; } = false;
    public INestedArea CurrentNestedArea { get; set; }
    public virtual bool IsFlammable { get; set; } = true;
    public virtual float Flammability { get; set; }
    public virtual bool IsOnFire { get; set; } = false;

    // Three interaction lists
    protected List<IInteraction> baseInteractionList = new List<IInteraction>();
    protected List<IInteraction> objectInteractionList = new List<IInteraction>();
    protected List<IInteraction> subObjectInteractionList = new List<IInteraction>();

    // Constructor should initialize interactions
    public BaseObject()
    {
        InitializeBaseInteractions();
        InitializeObjectInteractions();
        InitializeSubObjectInteractions();
    }

    protected virtual void InitializeBaseInteractions()
    {
        baseInteractionList.Add(new InspectInteraction());
        if (IsFlammable)
        {
            baseInteractionList.Add(new ExtinguishInteraction());
            baseInteractionList.Add(new CookInteraction());
        }
    }

    protected abstract void InitializeObjectInteractions();
    protected virtual void InitializeSubObjectInteractions() { }

    public IEnumerable<IInteraction> GetAvailableInteractions(PlayerInventory inventory)
    {
        List<IInteraction> availableInteractions = baseInteractionList
            .Concat(objectInteractionList)
            .Concat(subObjectInteractionList)
            .Where(interaction => interaction.IsAvailable(this, inventory))
            .ToList();

        List<IInteraction> uniqueInteractions = availableInteractions
            .GroupBy(GetInteractionDeduplicationKey)
            .Select(group => group.First())
            .ToList();

        if (uniqueInteractions.Count != availableInteractions.Count)
        {
            string duplicateNames = string.Join(", ", availableInteractions
                .GroupBy(interaction => interaction.Name)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key} x{group.Count()}"));

            ActionAAMDiagnosticsLogger.LogEvent("[PROVIDER DEDUPE]", "Duplicate object interactions suppressed",
                $"Provider: {Name} [{IInteractableID}] ({GetType().Name})\n" +
                $"RawAvailableInteractions: {availableInteractions.Count}\n" +
                $"UniqueAvailableInteractions: {uniqueInteractions.Count}\n" +
                $"DuplicateActions: {duplicateNames}");
        }

        return uniqueInteractions;
    }

    private static string GetInteractionDeduplicationKey(IInteraction interaction)
    {
        if (interaction == null)
        {
            return "NULL";
        }

        return $"{interaction.GetType().FullName}|{interaction.Name}|{interaction.Type}|{interaction.ActionPointCost}";
    }

    public virtual void Destroy()
    {
        Debug.Log($"{Name} has been destroyed.");
        OnDestroy();
    }

    protected virtual void OnDestroy()
    {
        Debug.Log($"{Name} has no special destruction behavior.");
    }

    // Method to handle object catching fire
    public virtual void CatchFire()
    {
        if (IsFlammable && !IsOnFire)
        {
            IsOnFire = true;

            // Store the original color if DefaultColor is not set
            if (string.IsNullOrEmpty(DefaultColor))
            {
                DefaultColor = Color;
            }

            // Change the object's color to red to indicate it is on fire
            Color = "#FF4500";

            Debug.Log($"{Name} has caught fire! Color changed.");
        }
    }

    // Method to handle extinguishing the fire
    public virtual void Extinguish()
    {
        if (IsFlammable && IsOnFire)
        {
            IsOnFire = false;

            // Restore the original color from DefaultColor
            Color = DefaultColor;

            Debug.Log($"{Name}'s fire has been extinguished. Color restored to {DefaultColor}.");
        }
    }

    public void RemoveObjectFromNestedArea()
    {
        // Check if the object is currently in a nested area
        if (IsInNestedArea && CurrentNestedArea != null)
        {
            Cell[,] nestedMap = CurrentNestedArea.GetNestedMap();

            // Make sure the object's NestedMapPosition is valid within the nested map bounds
            if (NestedMapPosition.x >= 0 && NestedMapPosition.x < nestedMap.GetLength(0) &&
                NestedMapPosition.y >= 0 && NestedMapPosition.y < nestedMap.GetLength(1))
            {
                // Retrieve the cell where the object was located
                Cell cell = nestedMap[NestedMapPosition.x, NestedMapPosition.y];

                // Optionally, if objects block movement, you might want to make the cell passable again
                cell.isPassable = true;

                // If the cell contains a reference to the object in its Objects list, remove it
                cell.Objects.Remove(this);

                // Add debugging information about object removal
                Debug.Log($"Object '{Name}' removed from nested area at position {NestedMapPosition}");
            }
            else
            {
                // Add debugging information if object NestedMapPosition is invalid
                Debug.LogWarning($"Object '{Name}' position ({NestedMapPosition}) is outside the bounds of the nested area.");
            }

            // Reset the object's nested area properties
            IsInNestedArea = false;
            CurrentNestedArea = null;

            // Add logging to indicate removal completion
            Debug.Log($"Object '{Name}' removed from nested area.");
        }
        else
        {
            // Add debugging information if object is not in a nested area
            Debug.LogWarning($"Object '{Name}' is not in a nested area.");
        }
    }


}

public abstract class ContainerBase : BaseObject, IContainer
{
    public ContainerInventory Inventory { get; private set; } = new ContainerInventory();

    public ContainerBase()
    {
        IsPassable = false;
        CoverType = CoverType.None;
        InitializeBaseInteractions();
        InitializeObjectInteractions();
        InitializeSubObjectInteractions();
    }

    public void GenerateAndAddItem(string itemName = null, ItemType? itemType = null, int amount = 1)
    {
        for (int i = 0; i < amount; i++)
        {
            Item item = null;

            if (!string.IsNullOrEmpty(itemName))
            {
                item = ItemGenerator.Instance.GenerateItem(itemName);
            }
            else if (itemType.HasValue)
            {
                item = ItemGenerator.Instance.GenerateRandomItem(itemType.Value);
            }
            else
            {
                Debug.LogError("Item name or ItemType must be specified for item generation.");
                return;
            }

            if (item != null)
            {
                item.UpdateItemName();
                Inventory.AddItem(item);
                Debug.Log($"Added {item.ItemInGameName} to {Name}.");
            }
            else
            {
                Debug.LogError("Item generation failed.");
            }
        }
    }

    public void AddItem(Item item, int amount = 1) => Inventory.AddItem(item, amount);

    public void RemoveItem(Item item, int amount = 1) => Inventory.RemoveItem(item.ItemInGameName, amount);

    public Item RemoveItem(string itemName, int amount = 1)
    {
        // Attempt to remove the item from inventory
        Item removedItem = Inventory.GetInventoryContainers()
            .SelectMany(container => container.Items)
            .FirstOrDefault(i => i.ItemInGameName == itemName);

        if (removedItem != null)
        {
            Inventory.RemoveItem(itemName, amount);
        }

        return removedItem; // Return the removed item, or null if not found
    }

    public List<InventoryContainer> GetInventoryContainers() => Inventory.GetInventoryContainers();

    public List<Item> GetItems()
    {
        foreach (var container in Inventory.GetInventoryContainers())
        {
            foreach (var item in container.Items)
            {
                item.UpdateItemName();
            }
        }
        return Inventory.GetInventoryContainers().SelectMany(ic => ic.Items).ToList();
    }

    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new OpenContainerInteraction());
        objectInteractionList.Add(new EmptyContainerInteraction());
    }
}

public class Chest : ContainerBase
{
    protected string chestType;
    protected int dungeonLevel; // Added field to scale loot for dungeons

    public Chest(string chestType, int dungeonLevel = 1)
    {
        Name = "Chest";
        Symbol = 'C';
        Color = "#CD7F32";
        Description = $"A {chestType} chest containing items related to {chestType}.";
        this.chestType = chestType.ToLower();
        this.dungeonLevel = dungeonLevel; // Store dungeon level for better loot scaling

        GenerateContents();
    }

    private void GenerateContents()
    {
        switch (chestType)
        {
            case "explorer":
                GenerateAndAddItem(itemType: ItemType.Tool, amount: 1);
                break;

            case "vegetable":
                GenerateAndAddItem(itemType: ItemType.Vegetable, amount: 5);
                break;

            case "weapon":
                GenerateAndAddItem(itemType: ItemType.Weapon, amount: 2);
                break;

            case "starting":
                GenerateAndAddItem(itemType: ItemType.Weapon, amount: 1);
                GenerateAndAddItem(itemType: ItemType.Tool, amount: 1);
                GenerateAndAddItem(itemType: ItemType.Armour, amount: 1);
                GenerateAndAddItem(itemType: ItemType.Component, amount: 3);
                break;

            case "dungeon": // NEW: Dungeon Chest with level-scaling loot
                AddDungeonLoot();
                break;

            case "empty":
                Debug.Log("Chest is empty.");
                break;

            default:
                Debug.LogWarning($"Unknown chest type: {chestType}. Generating default empty chest.");
                break;
        }
    }

    private void AddDungeonLoot()
    {
        Debug.Log($"Generating loot for Dungeon Chest (Level {dungeonLevel})");

        // Add 1-2 level-appropriate weapons or armor
        int numWeapons = UnityEngine.Random.Range(1, 3);
        for (int i = 0; i < numWeapons; i++)
        {
            Item weapon = ItemGenerator.Instance.GenerateRandomWeapon(dungeonLevel);
            if (weapon != null) Inventory.AddItem(weapon);
        }

        // Add 1-2 pieces of armor
        int numArmors = UnityEngine.Random.Range(1, 3);
        for (int i = 0; i < numArmors; i++)
        {
            Item armor = ItemGenerator.Instance.GenerateRandomEquipment(dungeonLevel);
            if (armor != null) Inventory.AddItem(armor);
        }

        Debug.Log($"Dungeon Chest generated with {numWeapons} weapons and {numArmors} armor pieces.");
    }

    public override void Destroy()
    {
        Debug.Log($"{Name} chest has been destroyed.");
        base.Destroy();
    }

    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new InspectInteraction());
        objectInteractionList.Add(new OpenContainerInteraction());
        objectInteractionList.Add(new EmptyContainerInteraction());
    }
}

public class Corpse : ContainerBase
{
    public override char Symbol => 'c';
    public override string Color => "#006400";
    public Race Race { get; set; }
    public override bool IsPassable { get; set; } = false;
    public string Description { get; set; }

    public string OriginalName { get; set; }
    public string BodyType { get; set; }

    // Stats for reanimation
    public int OriginalMaxHealth { get; set; }
    public int OriginalStrength { get; set; }
    public int OriginalDexterity { get; set; }
    public int OriginalConstitution { get; set; }
    public int OriginalSpeed { get; set; }
    public Anatomy Anatomy { get; set; }
    public Dictionary<EquipmentSlot, Item> EquippedItems { get; set; } = new();

    public static Corpse GenerateCorpse(NPC npc, string name)
    {
        GameDebugger.Instance.LogInfo("Generating a Corpse");

        Corpse corpse = new Corpse
        {
            Name = name,
            Race = npc.Race,
            Description = $"The corpse of a {npc.Race.Name}, now motionless. The stench of decay lingers.",
            Position = npc.NestedMapPosition,
            NestedMapPosition = npc.NestedMapPosition,
            IsActive = true,
            IsInNestedArea = true,
            CurrentNestedArea = npc.CurrentNestedArea,
            IsHostile = false,
            IsPassable = false,
            CoverType = CoverType.None,

            OriginalName = npc.Name,
            BodyType = npc.BodyType,

            // Store NPC stats for later reanimation
            OriginalMaxHealth = npc.MaxHealth,
            OriginalStrength = npc.Strength,
            OriginalDexterity = npc.Dexterity,
            OriginalConstitution = npc.Constitution,
            OriginalSpeed = npc.Speed,
            Anatomy = npc.Anatomy,
            EquippedItems = new Dictionary<EquipmentSlot, Item>(npc.EquippedItems)
    };

        // Place the corpse in the nested map
        Cell[,] nestedMap = npc.CurrentNestedArea.GetNestedMap();
        if (corpse.IsValidPosition())
        {
            nestedMap[corpse.NestedMapPosition.x, corpse.NestedMapPosition.y].Objects.Add(corpse);

            // Transfer NPC inventory to corpse
            corpse.TransferInventory(npc.Inventory);
            GameDebugger.Instance.LogInfo($"Corpse generated at position {corpse.Position}");
        }
        else
        {
            GameDebugger.Instance.LogWarning($"NPC '{npc.Name}' position is outside bounds.");
        }

        return corpse;
    }

    private bool IsValidPosition()
    {
        return NestedMapPosition.x >= 0 && NestedMapPosition.y >= 0;
    }

    private void TransferInventory(CharacterInventory npcInventory)
    {
        foreach (var container in npcInventory.GetInventoryContainers().ToList())
        {
            foreach (var item in container.Items.ToList())
            {
                this.Inventory.AddItem(item, 1);
                npcInventory.RemoveItem(item.ItemInGameName, 1);
            }
        }
    }
}

public class Carcass : ContainerBase
{
    public override char Symbol => 'x';
    public override string Color => "#8B4513";
    public AnimalSize Size { get; set; }
    public string Description { get; set; }

    public string OriginalName { get; set; }
    public string BodyType { get; set; }

    // Stats for reanimation
    public int OriginalMaxHealth { get; set; }
    public int OriginalStrength { get; set; }
    public int OriginalDexterity { get; set; }
    public int OriginalConstitution { get; set; }
    public int OriginalSpeed { get; set; }
    public Anatomy Anatomy { get; set; }

    public static Carcass GenerateCarcass(Animal animal, string name)
    {
        GameDebugger.Instance.LogInfo("Generating a Carcass");

        Carcass carcass = new Carcass
        {
            Name = name,
            Size = animal.Size,
            Position = animal.NestedMapPosition,
            NestedMapPosition = animal.NestedMapPosition,
            IsActive = true,
            IsInNestedArea = true,
            CurrentNestedArea = animal.CurrentNestedArea,
            IsHostile = false,
            IsPassable = false,
            CoverType = CoverType.Partial,
            Description = $"The carcass of a {animal.Name}.",

            OriginalName = animal.Name,
            BodyType = animal.BodyType,

            // Store stats for reanimation
            OriginalMaxHealth = animal.MaxHealth,
            OriginalStrength = animal.Strength,
            OriginalDexterity = animal.Dexterity,
            OriginalConstitution = animal.Constitution,
            OriginalSpeed = animal.Speed,
            Anatomy = animal.Anatomy
        };

        // Place the carcass in the nested map
        Cell[,] nestedMap = animal.CurrentNestedArea.GetNestedMap();
        if (carcass.IsValidPosition())
        {
            nestedMap[carcass.NestedMapPosition.x, carcass.NestedMapPosition.y].Objects.Add(carcass);
            GameDebugger.Instance.LogInfo($"Carcass generated at position {carcass.Position}");
        }
        else
        {
            GameDebugger.Instance.LogWarning($"Animal '{animal.Name}' position is outside bounds.");
        }

        // Generate loot from the carcass
        carcass.GenerateLoot(animal);

        return carcass;
    }

    private bool IsValidPosition()
    {
        return NestedMapPosition.x >= 0 && NestedMapPosition.y >= 0;
    }

    private void GenerateLoot(Animal animal)
    {
        Dictionary<string, int> loot = animal.GenerateBasicLoot();

        foreach (var lootItem in loot)
        {
            for (int i = 0; i < lootItem.Value; i++)
            {
                Item generatedItem = ItemGenerator.Instance.GenerateAnimalLootItem(lootItem.Key, animal.Name);
                if (generatedItem != null)
                {
                    generatedItem.Quantity = 1;
                    AddItem(generatedItem);
                    GameDebugger.Instance.LogInfo($"Added 1 x {generatedItem.ItemInGameName} to carcass.");
                }
            }
        }
    }
}

public class MonsterRemains : ContainerBase
{
    public override char Symbol => 'r'; // 'r' for remains
    public override string Color => "#8B4513"; // Brown color for remains
    public override bool IsPassable { get; set; } = false;
    public string Description { get; set; }

    public string MonsterType { get; set; }
    public int MonsterLevel { get; set; }  // Store the original level
    public bool WasBoss { get; set; }

    // Stats for potential reanimation
    public int OriginalMaxHealth { get; set; }
    public int OriginalStrength { get; set; }
    public int OriginalDexterity { get; set; }
    public int OriginalConstitution { get; set; }
    public int OriginalSpeed { get; set; }
    public List<Item> DroppedItems { get; set; } = new List<Item>();

    public static MonsterRemains GenerateRemains(Monster monster)
    {
        GameDebugger.Instance.LogInfo($"Generating remains for {monster.MonsterName}");

        MonsterRemains remains = new MonsterRemains
        {
            Name = $"{monster.MonsterName} remains",
            Description = $"The remains of a {monster.MonsterName}. A lingering dark energy surrounds it.",
            Position = monster.NestedMapPosition,
            NestedMapPosition = monster.NestedMapPosition,
            IsActive = true,
            IsInNestedArea = true,
            CurrentNestedArea = monster.CurrentNestedArea,
            IsPassable = false,
            CoverType = CoverType.None,

            MonsterType = monster.MonsterName,
            MonsterLevel = monster.Level, // Store the monster's level
            WasBoss = monster.IsBoss,

            // Store Monster stats for potential reanimation
            OriginalMaxHealth = monster.MaxHealth,
            OriginalStrength = monster.Strength,
            OriginalDexterity = monster.Dexterity,
            OriginalConstitution = monster.Constitution,
            OriginalSpeed = monster.Speed
        };

        // Generate loot upon death
        remains.GenerateLoot(monster);

        // Place the remains on the map
        Cell[,] nestedMap = monster.CurrentNestedArea.GetNestedMap();
        if (remains.IsValidPosition())
        {
            nestedMap[remains.NestedMapPosition.x, remains.NestedMapPosition.y].Objects.Add(remains);
            GameDebugger.Instance.LogInfo($"Remains of {monster.MonsterName} (Level {monster.Level}) placed at {remains.Position}");
        }
        else
        {
            GameDebugger.Instance.LogWarning($"Monster '{monster.MonsterName}' remains placement failed.");
        }

        return remains;
    }

    private bool IsValidPosition()
    {
        return NestedMapPosition.x >= 0 && NestedMapPosition.y >= 0;
    }

    private void GenerateLoot(Monster monster)
    {
        List<Item> loot = ItemGenerator.Instance.GenerateMonsterLoot(monster.MonsterName, monster.Level, monster.IsBoss);

        foreach (Item lootItem in loot)
        {
            lootItem.Quantity = 1;
            AddItem(lootItem);
            GameDebugger.Instance.LogInfo($"Added {lootItem.ItemInGameName} to remains of {monster.MonsterName}");
        }
    }
}

public class VillageSignPost : BaseObject
{
    public Village VillageToShow { get; set; } // Add this property to hold a reference to the village

    public VillageSignPost(Village villageToShow)
    {
        Name = "Village Signpost";
        Symbol = 'S';
        Color = "#8B4513"; // Brown color for signposts
        IsPassable = false; // Signposts are not passable
        CoverType = CoverType.None;
        VillageToShow = villageToShow; // Assign the village that the signpost is pointing to
        Description = $"A wooden signpost containing details for {villageToShow.Name}. It seems very informative.";

        // Initialize interactions
        InitializeObjectInteractions();
    }

    // Initialize interactions specific to the VillageSignPost
    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new ViewVillageSignPostInteraction());
    }

    public void DisplaySignText()
    {
        Debug.Log($"The signpost reads: Welcome to {VillageToShow.Name}");
    }
}


public class DonationCrate : BaseObject
{
    public Village AssignedVillage { get; set; } // Reference to the assigned village

    public DonationCrate(Village village)
    {
        Name = "Donation Crate";
        Symbol = 'D';
        Color = "#FFD700"; // Gold color for donation crates
        IsPassable = false; // Not passable
        CoverType = CoverType.Partial; // Provides partial cover
        AssignedVillage = village;
        Description = $"A sturdy crate designated for donations to the village of {village.Name}. It looks like it can hold a lot.";

        // Initialize custom interactions
        InitializeObjectInteractions();
    }

    // Override the InitializeObjectInteractions method to add custom interactions
    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new DonateInteraction()); // Players can donate items
        objectInteractionList.Add(new InspectInteraction()); // Players can inspect the crate
    }

    public void AcceptDonation(PlayerInventory inventory)
    {
        // Logic for accepting donations from the player's inventory
        Debug.Log("Donation crate accepts your generous donation.");
        // Example: inventory.RemoveItem("Gold", 10);
    }
}


public class Anvil : BaseObject
{
    public Village AssignedVillage { get; set; } // Reference to the assigned village

    public Anvil()
    {
        Name = "Anvil";
        Symbol = 'A'; // A for Anvil
        Color = "#A9A9A9"; // Dark grey for anvil
        IsPassable = false; // Anvils are not passable
        CoverType = CoverType.Full; // Anvils provide full cover
        Description = "A heavy iron anvil, perfect for forging weapons and armour.";

        // Initialize interactions
        InitializeObjectInteractions();
    }

    // Override the InitializeObjectInteractions method to add custom interactions
    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new SmithInteraction()); // Players can interact to smith items

    }
}

public class RuinedPillar : BaseObject
{
    public RuinedPillar(string color)
    {
        Name = "Ruined Pillar";
        Symbol = 'P';
        Color = color;
        IsPassable = false;
        CoverType = CoverType.Full; // Assuming the pillar provides full cover
        Description = "A broken pillar, once part of an ancient structure. Time has not been kind to it, and now it crumbles away.";
    }


    // Override InitializeObjectInteractions to add RuinedPillar-specific interactions (if any)
    protected override void InitializeObjectInteractions()
    {
        // No need to add InspectInteraction, as it's already in BaseObject
        // Add any specific interactions for RuinedPillar here, if needed
    }

    // Optional: Add any specific methods or logic for RuinedPillar here
}

public class StoneRuinedPillar : RuinedPillar
{
    public StoneRuinedPillar() : base("#808080") // Gray color for stone
    {
        Name = "Stone Ruined Pillar";
        CoverType = CoverType.Partial; // Adjust cover type as needed
        Description = "A stone pillar, worn and fractured by the elements. Its rough surface is covered in moss, a testament to its age.";
    }
}

public class SandRuinedPillar : RuinedPillar
{
    public SandRuinedPillar() : base("#D2B48C") // Tan color for sand
    {
        Name = "Sand Ruined Pillar";
        CoverType = CoverType.Partial; // Adjust cover type as needed
        Description = "A weathered pillar, its sandstone surface eroded by years of desert winds. It stands precariously amidst the sands.";
    }
}

public abstract class Block : BaseObject
{
    public int Health { get; protected set; } // Tracks block health
    public int MaxHealth { get; protected set; } // Maximum block health

    protected Block(string name, char symbol, string color, int maxHealth, bool isPassable)
    {
        Name = name;
        Symbol = symbol;
        Color = color;
        MaxHealth = maxHealth;
        Health = maxHealth;
        IsPassable = isPassable;
    }

    // Override interactions and add block-specific interactions (if any)
    protected override void InitializeObjectInteractions()
    {
        // Optionally add custom interactions like Inspecting or mining the block
        objectInteractionList.Add(new InspectInteraction()); // Default interaction for inspecting the block
    }

    // Method to handle block damage
    public void TakeDamage(int damage)
    {
        Health -= damage;
        if (Health <= 0)
        {
            Destroy();
        }
    }

    // Custom destruction method to handle when the block is destroyed
    protected override void OnDestroy()
    {
        base.OnDestroy();
        Debug.Log($"{Name} has been destroyed.");
    }
}

// Derived DirtBlock class
public class DirtBlock : Block
{
    public DirtBlock() : base("Dirt", 'B', "#8B4513", 25, true) // Brown color for dirt
    {
        CoverType = CoverType.Full; // Full cover
        IsPassable = false; // Dirt blocks are not passable
        Description = "A patch of dense dirt, blocking the way. With a shovel, it could probably be cleared.";
    }

    // Optionally add any unique interactions for DirtBlock
    protected override void InitializeObjectInteractions()
    {
        base.InitializeObjectInteractions();
        objectInteractionList.Add(new ClearShovelInteraction());
    }
}


// Derived SandBlock class
public class SandBlock : Block
{
    public SandBlock() : base("Sand", 'S', "#F5DEB3", 10, true) // Light yellow color for sand
    {
        CoverType = CoverType.Full; // Full cover
        Description = "A mound of loose sand. A shovel might be able to clear it away easily.";
    }

    // Optionally add any unique interactions for SandBlock
    protected override void InitializeObjectInteractions()
    {
        base.InitializeObjectInteractions();
        objectInteractionList.Add(new ClearShovelInteraction());
    }
}


// Derived StoneBlock class
public class StoneBlock : Block
{
    public StoneBlock() : base("Stone", 'S', "#A9A9A9", 50, false) // Gray color for stone
    {
        CoverType = CoverType.Full; // Full cover
        IsPassable = false; // Stone blocks are not passable
        Description = "A solid block of stone, too heavy to move by hand. With a pickaxe, you could probably break it down.";
    }

    // Optionally add any unique interactions for StoneBlock
    protected override void InitializeObjectInteractions()
    {
        base.InitializeObjectInteractions();
        objectInteractionList.Add(new ClearPickaxeInteraction());
    }
}

public class Hole : BaseObject
{
    public bool HasRope { get; set; }
    public int DescendsToAreaID { get; set; }

    public Hole()
    {
        Name = "Hole";
        Symbol = 'O';
        Color = "#A52A2A"; // Brown color
        IsPassable = true;
        CoverType = CoverType.None;
        Description = "A deep hole leading down to an unknown area. You might need a rope to safely descend.";

        // Manually initialize interactions
        InitializeObjectInteractions();
    }

    // Override to add Hole-specific interactions
    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new DescendInteraction());
    }
}

public class Rope : BaseObject
{
    public Rope()
    {
        Name = "Rope";
        Symbol = 'R';
        Color = "#A52A2A"; // Brown color
        IsPassable = true;
        CoverType = CoverType.None;
        Description = "A sturdy rope that would allow you to safely climb up.";

        // Manually initialize interactions
        InitializeObjectInteractions();
    }

    // Override to add Rope-specific interactions
    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new AscendInteraction());
    }
}

public class Staircase : BaseObject
{
    public Staircase()
    {
        Name = "Staircase";
        Symbol = 'S';
        Color = "#A52A2A"; // Brown color
        IsPassable = true;
        CoverType = CoverType.None;
        Description = "A sturdy staircase leading to another level. It looks safe to ascend.";

        // Manually initialize interactions
        InitializeObjectInteractions();
    }

    // Override to add Staircase-specific interactions
    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new AscendInteraction());
    }
}


public class UpwardStaircase : BaseObject
{
    public UpwardStaircase()
    {
        Name = "Upward Staircase";
        Symbol = 'U';
        Color = "#FFD700"; // Gold color
        IsPassable = true;
        CoverType = CoverType.None;
        Description = "A grand staircase leading upward, inviting you to ascend to a higher floor.";

        // Manually initialize interactions
        InitializeObjectInteractions();
    }

    // Override to add UpwardStaircase-specific interactions
    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new AscendInteraction());
    }
}


public class DownwardStaircase : BaseObject
{
    public DownwardStaircase()
    {
        Name = "Downward Staircase";
        Symbol = 'D';
        Color = "#8B4513"; // Brown color
        IsPassable = true;
        CoverType = CoverType.None;
        Description = "A set of steps leading downward, beckoning you to descend into the depths below.";

        // Manually initialize interactions
        InitializeObjectInteractions();
    }

    // Override to add DownwardStaircase-specific interactions
    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new DescendInteraction());
    }
}

public class DungeonEntrance : BaseObject
{
    public int DungeonID { get; set; }

    public DungeonEntrance(int dungeonID)
    {
        Name = "Dungeon Entrance";
        Symbol = 'D';
        Color = "#8B0000";
        IsPassable = false;
        CoverType = CoverType.None;
        DungeonID = dungeonID;

        // Ensure the object interactions are added here to avoid lifecycle issues
        InitializeObjectInteractions(); // Explicitly call this in the constructor
    }

    // Override to add DungeonEntrance-specific interactions
    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new EnterDungeonInteraction());
    }
}

public class CaveEntrance : BaseObject
{
    public int CaveID { get; set; } // Unique ID for the cave

    public CaveEntrance(int caveID)
    {
        Name = "Cave Entrance";
        Symbol = 'C'; // A symbol that visually represents a cave entrance
        Color = "#696969"; // Dark grey color for the cave entrance
        IsPassable = false; // Players cannot walk through the entrance without entering
        CoverType = CoverType.None; // Caves don't provide cover by default
        CaveID = caveID;

        // Add custom description for the cave
        Description = "A dark and foreboding cave entrance.";

        // Initialize interactions
        InitializeObjectInteractions();
    }

    // Initialize custom interactions for the CaveEntrance
    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new EnterCaveInteraction()); // Add interaction to enter the cave
        objectInteractionList.Add(new InspectInteraction());   // Players can inspect the cave
    }

}

public class Rock : ContainerBase
{
    public int Level { get; private set; } // Depth level where the rock is found

    // Constructor with level parameter
    public Rock(int level)
    {
        Name = "Rock";
        Symbol = 'R';
        Color = "#A0A0A0"; // Default gray color for rocks
        IsPassable = false;
        CoverType = CoverType.Full;
        Level = level; // Assign the level where the rock is found

        GenerateResources(); // Generate stone, ore, and gem resources

        // Initialize object interactions
        InitializeObjectInteractions();
    }

    // Generates stone, ore, and gems based on level
    protected virtual void GenerateResources()
    {
        // Always generate stone at any level
        var stone = ItemGenerator.Instance.GenerateItem("Stone");
        if (stone != null)
        {
            AddItem(stone, UnityEngine.Random.Range(3, 6)); // Add a random amount of stone
        }

        // Generate ore only if the level is -3 or lower
        if (Level <= -3)
        {
            var ore = ItemGenerator.Instance.GenerateRandomItem(ItemType.Ore);
            if (ore != null)
            {
                AddItem(ore, UnityEngine.Random.Range(1, 4)); // Add a random amount of ore
                Debug.Log("Ore was found in the rock!");
            }
        }

        // Gems can appear at any level, but with a higher chance at deeper levels
        if (UnityEngine.Random.value < GetGemChance(Level))
        {
            var gem = ItemGenerator.Instance.GenerateRandomItem(ItemType.Gemstone);
            if (gem != null)
            {
                AddItem(gem, 1); // Add one gem if found
                Debug.Log("A gem was found in the rock!");
            }
        }
    }

    // Calculates gem chance based on level
    protected virtual float GetGemChance(int level)
    {
        if (level == 0) return 0.01f; // Minimal chance at ground level

        // Gems become more likely as the level goes deeper (e.g., -1 is a 2% chance, -10 is a 55% chance)
        return Mathf.Clamp(0.01f + (-level * 0.05f), 0.01f, 0.6f); // Max chance capped at 60%
    }


    // Optionally override destruction to customize behavior
    protected override void OnDestroy()
    {
        base.OnDestroy();
        Debug.Log("Rock destroyed. Resources collected.");
    }

    // Initialize mining interaction for rocks
    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new MineInteraction());
    }
}


public class SmallRock : Rock
{
    public SmallRock() : base(level: 1) // Small rocks are typically found at shallower levels
    {
        Name = "Small Rock";
        Symbol = 'r'; // Lowercase 'r' for small rock
        Color = "#A9A9A9"; // Lighter grey for small rocks
        CoverType = CoverType.None; // Small rocks don't provide cover
        IsPassable = true; // Small rocks can be walked over

        // Manually initialize interactions
        InitializeObjectInteractions();
    }

    // Override OnDestroy to customize destruction behavior for small rocks
    protected override void OnDestroy()
    {
        base.OnDestroy();
        Debug.Log("Small Rock destroyed. Few resources collected.");
    }

    // Optionally override the resource generation logic to yield fewer items if necessary
    protected override void GenerateResources()
    {
        // Smaller quantity of stone
        var stone = ItemGenerator.Instance.GenerateItem("Stone");
        if (stone != null)
        {
            AddItem(stone, UnityEngine.Random.Range(1, 3)); // Generate 1 to 2 stones
        }

        // Small chance for a gem, because small rocks are often at shallow levels
        if (UnityEngine.Random.value < GetGemChance(Level))
        {
            var gem = ItemGenerator.Instance.GenerateItem("Gem");
            if (gem != null)
            {
                AddItem(gem, 1); // Add one gem if found
                Debug.Log("A gem was found in the small rock!");
            }
        }
    }
}

public class LargeRock : Rock
{
    public LargeRock() : base(level: 3) // Large rocks are more likely to be found deeper
    {
        Name = "Large Rock";
        Symbol = 'R'; // Retain the same symbol as a generic rock
        Color = "#505050"; // Darker grey for large rocks
        CoverType = CoverType.Full; // Large rocks provide full cover
        IsPassable = false; // Large rocks cannot be walked over

        // Manually initialize interactions
        InitializeObjectInteractions();
    }

    // Override OnDestroy to customize destruction behavior for large rocks
    protected override void OnDestroy()
    {
        base.OnDestroy();
        Debug.Log("Large Rock destroyed. More resources collected.");
    }

    // Optionally override the resource generation logic to yield more items if necessary
    protected override void GenerateResources()
    {
        // Larger quantity of stone
        var stone = ItemGenerator.Instance.GenerateItem("Stone");
        if (stone != null)
        {
            AddItem(stone, UnityEngine.Random.Range(5, 10)); // Generate 5 to 10 stones
        }

        // Higher chance for gems due to being a large rock at a deeper level
        if (UnityEngine.Random.value < GetGemChance(Level))
        {
            var gem = ItemGenerator.Instance.GenerateItem("Gem");
            if (gem != null)
            {
                AddItem(gem, 1); // Add one gem if found
                Debug.Log("A gem was found in the large rock!");
            }
        }

        // Chance for ore based on rock size and depth
        var ore = ItemGenerator.Instance.GenerateItem("Ore");
        if (ore != null)
        {
            AddItem(ore, UnityEngine.Random.Range(1, 3)); // Generate 1 to 2 ores
        }
    }
}


public enum DoorState
{
    Open,
    Closed
}

// Door class inheriting from BaseObject
public class Door : BaseObject
{
    private DoorState _state = DoorState.Closed;
    public DoorState State
    {
        get => _state;
        private set
        {
            _state = value;
            // Update passability and cover type whenever the state changes
            IsPassable = _state == DoorState.Open;
            CoverType = _state == DoorState.Closed ? CoverType.Full : CoverType.None;
        }
    }

    public override char Symbol => State == DoorState.Open ? '/' : '+';
    public override string Color => State == DoorState.Open ? "#00FF00" : "#8B4513";

    public Door()
    {
        Name = "Door";
        IsPassable = false;
        CoverType = CoverType.Full; // Closed doors provide full cover

        // Manually initialize interactions
        InitializeObjectInteractions();
    }

    // Handle custom interactions for Door objects
    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new OpenDoorInteraction());
        objectInteractionList.Add(new CloseDoorInteraction());
    }

    // Method to open the door
    public void OpenDoor()
    {
        if (State == DoorState.Closed)
        {
            State = DoorState.Open;
            Debug.Log("The door is now open.");
        }
    }

    // Method to close the door
    public void CloseDoor()
    {
        if (State == DoorState.Open)
        {
            State = DoorState.Closed;
            Debug.Log("The door is now closed.");
        }
    }

    // Optional OnDestroy override if you want to destroy the door
    protected override void OnDestroy()
    {
        Debug.Log($"{Name} has been destroyed.");
        base.OnDestroy();
    }
}

// BaseWall class inheriting from BaseObject
public abstract class BaseWall : BaseObject
{
    public override bool IsPassable { get; set; } = false; // Walls are not passable by default
    public BaseWall(string name)
    {
        Name = name;
        CoverType = CoverType.Full; // Walls generally provide full cover

        // Manually initialize interactions
        InitializeObjectInteractions();
    }

    // Walls don't need custom interactions by default, but can override if needed
    protected override void InitializeObjectInteractions()
    {
        // Walls typically don't have specific interactions, just inspecting
    }
}

public class WoodenWall : BaseWall
{
    public override char Symbol => 'W';
    public override string Color => "#8B4513"; // Brown color for wood

    public WoodenWall() : base("Wooden Wall")
    {
    }
}

public class StoneWall : BaseWall
{
    public override char Symbol => 'W';
    public override string Color => "#A9A9A9"; // Grey color for stone

    public StoneWall() : base("Stone Wall")
    {
    }
}

public class DungeonWall : BaseWall
{
    public override char Symbol => 'W';
    public override string Color => "#A9A9A9"; // Grey color for stone

    public DungeonWall() : base("Dungeon Wall")
    {
    }
}

public class CaveWall : BaseWall
{
    public override char Symbol => 'W';
    public override string Color => "#A9A9A9"; // Grey color for Cave Walls

    public CaveWall() : base("Cave Wall")
    {
    }
}


// Campfire class inheriting from BaseObject
public class Campfire : BaseObject
{
    public Campfire()
    {
        Name = "Campfire";
        Symbol = 'F'; // F for fire
        Color = "#8B4513";
        IsPassable = false; // Players can walk over campfires when they're out
        CoverType = CoverType.None; // Campfires provide no cover

        // Manually initialize interactions
        InitializeObjectInteractions();
    }

    // Handle any specific campfire interactions if necessary
    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new LightCampfireInteraction());
    }

    // Optional OnDestroy override if you want to destroy the campfire
    protected override void OnDestroy()
    {
        Debug.Log("The campfire has burned out.");
        base.OnDestroy();
    }
}


