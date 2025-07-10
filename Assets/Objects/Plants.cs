using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public abstract class PlantBase : BaseObject, IContainer
{
    protected int daysToNextStage;
    protected int currentDays = 0;

    public bool IsContinuousGrowth { get; protected set; }
    public bool IsWithered { get; private set; } = false;
    public bool IsTree { get; set; }

    public string CropType { get; protected set; }
    public List<ItemType> CropItemTypes { get; protected set; }

    public PlantStage CurrentStage { get; protected set; } = PlantStage.SeededSoil;

    public ContainerInventory Inventory { get; private set; } = new ContainerInventory();

    protected PlantBase(string cropType, bool isContinuousGrowth)
    {
        CropType = cropType;
        IsContinuousGrowth = isContinuousGrowth;
        InitializeItemTypes();
    }

    private void InitializeItemTypes()
    {
        var itemData = PermaLists.Instance.ItemCreationData.FirstOrDefault(data => data.Name == CropType);
        if (itemData != null)
        {
            CropItemTypes = itemData.ItemTypes;
        }
        else
        {
            CropItemTypes = new List<ItemType>();
            Debug.LogWarning($"ItemCreationData not found for crop type: {CropType}");
        }

        AdjustGrowthCycle();
    }

    protected virtual void AdjustGrowthCycle()
    {
        if (CropItemTypes.Contains(ItemType.TreeFruit))
        {
            daysToNextStage += 3;
        }
        else if (CropItemTypes.Contains(ItemType.Vegetable))
        {
            daysToNextStage -= 1;
        }
    }

    public void ProgressGrowth()
    {
        if (IsWithered) return;

        currentDays++;
        if (currentDays >= daysToNextStage)
        {
            GrowToNextStage();
        }

        CheckForWithering();
    }

    public void AdvanceGrowthByDays(int days, Season lastSeasonVisited, Season currentSeason)
    {
        if (IsWithered) return;

        currentDays += days;

        if (currentDays >= daysToNextStage)
        {
            GrowToNextStage();
        }

        if (!IsTree && HasCrossedWinter(lastSeasonVisited, currentSeason))
        {
            WitherPlant();
        }
    }

    protected bool HasCrossedWinter(Season lastSeason, Season currentSeason)
    {
        int lastSeasonIndex = (int)lastSeason;
        int currentSeasonIndex = (int)currentSeason;

        return (lastSeasonIndex < (int)Season.Winter && currentSeasonIndex >= (int)Season.Winter) ||
               (lastSeasonIndex > currentSeasonIndex && currentSeasonIndex < (int)Season.Winter);
    }

    protected void CheckForWithering()
    {
        if (TimeManager.Instance.currentSeason == Season.Winter)
        {
            if (CropItemTypes.Contains(ItemType.TreeFruit))
            {
                CurrentNestedArea.ReplaceObject(this, new Tree(CropType, new List<Item>(), true) { IsDormant = true });
            }
            else
            {
                WitherPlant();
            }
        }
    }

    public void WitherPlant()
    {
        IsWithered = true;
        ReplaceWithNewPlant(new WitheredPlant(CropType));
    }

    protected void ReplaceWithNewPlant(PlantBase newPlant)
    {
        newPlant.Position = Position;
        newPlant.NestedMapPosition = NestedMapPosition;
        newPlant.CurrentNestedArea = CurrentNestedArea;

        CurrentNestedArea.ReplaceObject(this, newPlant);
        IsActive = false;
    }

    protected void AddFruitToPlant()
    {
        Item fruit = ItemGenerator.Instance.GenerateItem(CropType);
        if (fruit != null)
        {
            Inventory.AddItem(fruit, 1);
            Debug.Log($"{fruit.Name} has grown on the {Name}.");
        }
        else
        {
            Debug.LogWarning("Failed to generate fruit item.");
        }
    }

    protected List<Item> GenerateInitialFruitsForTree(string cropType)
    {
        List<Item> initialFruits = new List<Item>();
        Item fruitItem = ItemGenerator.Instance.GenerateItem(cropType);

        if (fruitItem != null)
        {
            int quantity = UnityEngine.Random.Range(1, 5);
            for (int i = 0; i < quantity; i++)
            {
                initialFruits.Add(fruitItem);
            }
        }
        else
        {
            Debug.LogWarning($"Could not generate initial fruits for tree with fruit type: {cropType}");
        }

        return initialFruits;
    }

    protected abstract void GrowToNextStage();

    #region Inventory Management (Updated for ContainerInventory)

    public void AddItem(Item item, int amount = 1) => Inventory.AddItem(item, amount);

    public void RemoveItem(Item item, int amount = 1)
    {
        if (item != null)
        {
            Inventory.RemoveItem(item.ItemInGameName, amount);
        }
        else
        {
            Debug.LogWarning("PlantsClass: Attempted to remove a null item.");
        }
    }

    public Item RemoveItem(string itemName, int amount = 1)
    {
        Item removedItem = Inventory.GetInventoryContainers()
            .SelectMany(container => container.Items)
            .FirstOrDefault(i => i.ItemInGameName == itemName);

        if (removedItem != null)
        {
            Inventory.RemoveItem(itemName, amount);
        }

        return removedItem;
    }

    public List<Item> GetItems() => Inventory.GetInventoryContainers().SelectMany(ic => ic.Items).ToList();

    public List<InventoryContainer> GetInventoryContainers() => Inventory.GetInventoryContainers();

    #endregion


    protected override void InitializeObjectInteractions()
    {
        objectInteractionList.Add(new InspectInteraction());
    }

    protected override void OnDestroy()
    {
        CurrentNestedArea.RemoveObjectFromArea(this);
        this.IsActive = false;
        Debug.Log($"{Name} has withered away.");
    }
}


//////////////////
// WitheredPlant
//////////////////
public class WitheredPlant : PlantBase
{
    public WitheredPlant(string cropType) : base(cropType, false)
    {
        Name = $"Withered {cropType} Plant";
        daysToNextStage = 0; // No further growth
        CurrentStage = PlantStage.Withered;
    }

    protected override void GrowToNextStage()
    {
        // No further growth; this is the final stage
    }

    public override char Symbol => 'W';
    public override string Color => "#808080"; // Gray color for withered plant
}

//////////////////
// SeededSoil
//////////////////
public class SeededSoil : PlantBase
{
    public SeededSoil(string cropType) : base(cropType, DetermineIsContinuousGrowth(cropType))
    {
        Name = $"{cropType} Seeded Soil";
        daysToNextStage = 3;
        CurrentStage = PlantStage.SeededSoil;
    }

    private static bool DetermineIsContinuousGrowth(string cropType)
    {
        var itemData = PermaLists.Instance.ItemCreationData.FirstOrDefault(data => data.Name == cropType);
        if (itemData != null)
        {
            return itemData.ItemTypes.Contains(ItemType.TreeFruit);
        }
        return false;
    }

    protected override void GrowToNextStage()
    {
        if (CropItemTypes.Contains(ItemType.TreeFruit))
        {
            ReplaceWithNewPlant(new Sapling(CropType));
        }
        else
        {
            ReplaceWithNewPlant(new Sprout(CropType, IsContinuousGrowth));
        }
    }

    public override char Symbol => 'S';
    public override string Color => "#8B4513"; // Brown color for soil
}

//////////////////
// Sprout
//////////////////
public class Sprout : PlantBase
{
    public Sprout(string cropType, bool isContinuousGrowth) : base(cropType, isContinuousGrowth)
    {
        Name = $"{cropType} Sprout";
        daysToNextStage = 5;
        CurrentStage = PlantStage.Sprout;
    }

    protected override void GrowToNextStage()
    {
        ReplaceWithNewPlant(new PartGrownPlant(CropType, IsContinuousGrowth));
    }

    public override char Symbol => 'T';
    public override string Color => "#228B22"; // Green for sprout
}

//////////////////
// PartGrownPlant
//////////////////
public class PartGrownPlant : PlantBase
{
    public PartGrownPlant(string cropType, bool isContinuousGrowth) : base(cropType, isContinuousGrowth)
    {
        Name = $"Part-Grown {cropType} Plant";
        daysToNextStage = 7;
        CurrentStage = PlantStage.PartGrownPlant;
    }

    protected override void GrowToNextStage()
    {
        ReplaceWithNewPlant(new Plant(CropType, IsContinuousGrowth));
    }

    public override char Symbol => 'P';
    public override string Color => "#32CD32"; // Light green for part-grown
}

//////////////////
// Plant (Fully Grown)
//////////////////
public class Plant : PlantBase
{
    public Plant(string cropType, bool isContinuousGrowth) : base(cropType, isContinuousGrowth)
    {
        Name = $"{cropType} Plant";
        CurrentStage = PlantStage.FullyGrown;

        // Initialize interactions, including GatherInteraction
        InitializeSubObjectInteractions();
    }

    protected override void InitializeSubObjectInteractions()
    {
        subObjectInteractionList.Add(new GatherInteraction());  // Add gather interaction for plant
    }

    protected override void GrowToNextStage()
    {
        if (IsContinuousGrowth)
        {
            currentDays = 0;  // Reset the days for the next harvest cycle
            AddFruitToPlant();  // Add fruit during each growth cycle using the base method
        }
        else
        {
            WitherPlant();  // Singular growth plants wither after harvest
        }
    }
}

//////////////////
// Sapling
//////////////////
public class Sapling : PlantBase
{
    public Sapling(string cropType) : base(cropType, true) // Saplings are continuous as they grow into trees
    {
        Name = $"{cropType} Sapling";
        daysToNextStage = 5;
        CurrentStage = PlantStage.Sapling;
    }

    protected override void GrowToNextStage()
    {
        bool growsFruit = UnityEngine.Random.value > 0.5f; // 50% chance for fruit tree
        List<Item> initialFruits = growsFruit ? GenerateInitialFruitsForTree(CropType) : new List<Item>();
        ReplaceWithNewPlant(new Tree(CropType, initialFruits, growsFruit));
    }

    public override char Symbol => 'Y';
    public override string Color => "#32CD32"; // Light green for sapling
}

//////////////////
// Tree (Final Stage for TreeFruit)
//////////////////
public class Tree : PlantBase
{
    public int WoodQuantity { get; set; } = 10; // Default wood quantity for all trees
    public bool GrowsFruit { get; set; } = true;
    public int MaxFruit { get; set; } = 8;
    public bool IsDormant { get; set; } = false;

    public Tree(string cropType, List<Item> initialFruits, bool growsFruit) : base(cropType, true)
    {
        Name = $"{cropType} Tree";
        IsPassable = false;
        CoverType = CoverType.Full;
        GrowsFruit = growsFruit;

        if (GrowsFruit)
        {
            foreach (var fruit in initialFruits)
            {
                Inventory.AddItem(fruit, 1); // Use `ContainerInventory` to add fruits
            }

            CheckForFruitProduction(); // Ensure the initial fruit production check is still called
        }

        // Manually initialize interactions
        InitializeSubObjectInteractions();
    }

    public void SetDormant(bool isDormant)
    {
        IsDormant = isDormant;

        if (IsDormant)
        {
            Inventory.RemoveAllItems(); // Clear any existing fruit
            Debug.Log($"{Name} has gone dormant and will not produce fruit.");
        }
        else
        {
            Debug.Log($"{Name} is no longer dormant and will resume fruit production.");
        }
    }

    protected override void GrowToNextStage()
    {
        // No further growth; this is the final stage for the tree
    }

    public void CheckForFruitProduction()
    {
        if (!GrowsFruit || IsDormant) return; // Only run if the tree grows fruit and is not dormant

        int fruitsToAdd = 0;

        switch (TimeManager.Instance.currentSeason)
        {
            case Season.Spring:
                fruitsToAdd = UnityEngine.Random.Range(1, 4); // Add a few fruits
                break;
            case Season.Summer:
                fruitsToAdd = UnityEngine.Random.Range(4, MaxFruit + 1); // Add a lot of fruits
                break;
            case Season.Autumn:
                fruitsToAdd = UnityEngine.Random.Range(1, 4); // Add a few fruits
                break;
            default:
                fruitsToAdd = 0; // No fruits during winter or other undefined seasons
                break;
        }

        for (int i = 0; i < fruitsToAdd; i++)
        {
            AddFruitToPlant(); // Call the base method to add fruits
        }
    }

    public override char Symbol => IsDormant ? 'D' : 'T'; // 'D' for dormant, 'T' for tree
    public override string Color => IsDormant ? "#A9A9A9" : "#006400"; // Dark gray for dormant, dark green for active tree

    // Mid-level class (PlantBase) has already locked ObjectInteractions.
    protected override void InitializeSubObjectInteractions()
    {
        subObjectInteractionList.Add(new ChopInteraction());  // Trees can be chopped
        subObjectInteractionList.Add(new ShakeInteraction()); // Trees can be shaken for fruit
    }
}

//////////////////
// Fungi (Final Stage for Fungi)
//////////////////

public class Fungi : PlantBase
{
    public string FungiType { get; private set; }  // The specific type of fungi, e.g., Shitake Mushroom, Morel, etc.
    public int MaxFungi { get; private set; } = 5;  // Maximum fungi generated

    public Fungi(string fungiType) : base(fungiType, false)  // Fungi do not have continuous growth
    {
        FungiType = fungiType;
        Name = $"{fungiType} Patch";
        IsPassable = true;  // You can walk through fungi
        IsTree = false;  // Fungi are not trees
        CoverType = CoverType.None;

        // Generate and add the fungi to the inventory upon initialization
        GenerateFungi();

        // Manually initialize interactions (optional, if you want interactions like inspecting the fungi)
        InitializeSubObjectInteractions();
    }

    protected override void GrowToNextStage()
    {
        // No growth stages for fungi, so nothing happens here
    }

    private void GenerateFungi()
    {
        // Randomly determine how many fungi to generate (between 1 and MaxFungi)
        int fungiToAdd = UnityEngine.Random.Range(1, MaxFungi + 1);

        // Add the generated fungi to the inventory
        for (int i = 0; i < fungiToAdd; i++)
        {
            var fungiItem = ItemGenerator.Instance.GenerateItem(FungiType);  // Generate the appropriate fungi item
            if (fungiItem != null)
            {
                AddItem(fungiItem, 1);  // Add fungi to the container inventory
                Debug.Log($"{fungiItem.Name} has been added to {Name}.");
            }
            else
            {
                Debug.LogWarning($"Failed to generate item for {FungiType}.");
            }
        }
    }

    public override char Symbol => 'M';  // Represent fungi with 'M' for Mushroom
    public override string Color => "#A52A2A";  // Brownish color for mushrooms

    // (Optional) Initialize additional interactions
    protected override void InitializeSubObjectInteractions()
    {
        // If you want to inspect the fungi or add other interactions, you can do that here
        subObjectInteractionList.Add(new InspectInteraction());
        subObjectInteractionList.Add(new GatherInteraction());
    }
}


//////////////////
// LongGrass
//////////////////

public class LongGrass : PlantBase
{
    public LongGrass() : base("Long Grass", false) // No continuous growth for LongGrass
    {
        Name = "Long Grass";
        Symbol = 'L'; // Represents LongGrass on the map
        Color = "#228B22"; // Green color for long grass
        IsPassable = true; // Players can walk through long grass
        CoverType = CoverType.None; // No special cover from long grass

        // Initialize interactions specific to LongGrass
        InitializeObjectInteractions();
    }

    // LongGrass does not have any growth stages, so we override the method but leave it empty
    protected override void GrowToNextStage()
    {
        // LongGrass does not grow to a next stage
    }

    // Add custom interactions, like cutting the grass
    protected override void InitializeObjectInteractions()
    {
        // Add a CutInteraction to allow players to cut the grass
        objectInteractionList.Add(new CutInteraction());
    }

    // Method that is triggered when the grass is cut
    public void CutGrass()
    {
        // Remove the LongGrass from the game world and reward seeds
        Debug.Log($"{Name} has been cut and removed.");
        GiveSeedsToPlayer();
        RemoveLongGrass();
    }

    // Grant seeds to the player when the grass is cut
    private void GiveSeedsToPlayer()
    {
        // Generate a seed item based on the crop type (in this case, "Long Grass Seed")
        Item seedItem = ItemGenerator.Instance.GenerateItem($"{Name} Seed"); // Ensure "Long Grass Seed" is defined in the item system
        if (seedItem != null)
        {
            // Add the seed to the player's inventory
            PlayerInventory.Instance.AddItem(seedItem, 1); // Adds 1 seed to the player's inventory
            Debug.Log($"{seedItem.Name} has been added to the player's inventory.");
        }
        else
        {
            Debug.LogWarning($"Failed to generate seeds for {Name}.");
        }
    }

    // Remove LongGrass from the map or area
    private void RemoveLongGrass()
    {
        // Remove LongGrass from the nested area or map
        CurrentNestedArea.RemoveObjectFromArea(this);
        IsActive = false; // Deactivate the object after it's been cut
    }
}


//////////////////
// Flower
//////////////////

public class Flower : PlantBase
{
    public Flower() : base("Flower", false) // No continuous growth for Flower
    {
        Name = "Flower";
        Symbol = 'F'; // Represents Flower on the map
        Color = "#FF69B4"; // Pinkish color for the flower, adjust if needed
        IsPassable = true; // Players can walk through flowers
        CoverType = CoverType.None; // No special cover from flowers

        // Initialize interactions specific to flowers
        InitializeObjectInteractions();
    }

    // Flowers do not have any growth stages, so we override but leave empty
    protected override void GrowToNextStage()
    {
        // Flowers do not grow to a next stage
    }

    // Initialize the custom interactions for flowers
    protected override void InitializeObjectInteractions()
    {
        // Add a PickFlowerInteraction to allow players to pick the flower
        objectInteractionList.Add(new PickFlowerInteraction());
    }

    // Method that is triggered when the flower is picked
    public void PickFlower()
    {
        Debug.Log($"{Name} has been picked and added to the inventory.");
        GiveFlowerToPlayer();
        RemoveFlower();
    }

    // Grant the flower item to the player when picked
    private void GiveFlowerToPlayer()
    {
        // Generate a generic "Flower" item
        Item flowerItem = ItemGenerator.Instance.GenerateItem("Flower"); // Ensure "Flower" is defined in the item system
        if (flowerItem != null)
        {
            // Add the flower to the player's inventory
            PlayerInventory.Instance.AddItem(flowerItem, 1); // Adds 1 flower to the player's inventory
            Debug.Log($"1 Flower has been added to the player's inventory.");
        }
        else
        {
            Debug.LogWarning("Failed to generate Flower item.");
        }
    }

    // Remove the flower from the map or area
    private void RemoveFlower()
    {
        CurrentNestedArea.RemoveObjectFromArea(this);
        IsActive = false; // Deactivate the object after it's been picked
    }
}



public enum PlantStage
{
    SeededSoil,
    Sapling,
    Sprout,
    PartGrownPlant,
    FullyGrown,
    Withered
}
