using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ForestNestedArea : BaseNestedArea
{
    private List<string> waterEdges;
    private new const int Size = 9; // Fixed size for simplicity
    private List<Vector2Int> treePositions = new List<Vector2Int>(); // List to store positions of trees

    // Constructor
    public ForestNestedArea(List<string> initialWaterEdges, List<Animal> parentAnimals, Cell parentCell, int regionNumber)
    {
        // Set essential properties for the nested area
        ParentCell = parentCell;
        ParentCellID = parentCell.CellID;
        MainMapCellID = parentCell.CellID;
        waterEdges = initialWaterEdges;
        RegionNumber = regionNumber; // Ensure the region number is set correctly
        GeneratedAnimals = parentAnimals; // Initialize animals from parent cell
        Initialize();
        EntrancePosition = new Vector2Int(0, 0);
    }

    // Initialize the nested area map and generate trees
    public override void Initialize()
    {
        int nestedAreaID = GameManager.Instance.GetNestedAreaID();
        NestedAreaID = nestedAreaID;
        AreaMap = new Cell[Size, Size];
        GenerateForest();

        // Call GetDistanceToNearestCellWithTerrainType to determine the distance to the nearest mountain
        int parentCellID = ParentCellID; // Ensure ParentCellID is set
        int distanceToMountain = MapGenerator.Instance.GetDistanceToNearestCellWithTerrainType(parentCellID, TerrainType.Mountain);

        // Determine the number of rocks to place based on the distance
        int numberOfRocks = DetermineNumberOfRocks(distanceToMountain);

        // Place the rocks in the nested area
        PlaceRocks(numberOfRocks, distanceToMountain);

        // Generate and place animals using the base class methods
        GenerateAnimalsForCellID(ParentCellID);
    }

    private void GenerateForest()
    {
        // Initialize all cells as Land initially
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                int cellID = GenerateUniqueCellID(); // Generate a unique cell ID
                AreaMap[x, y] = new Cell(cellID, x, y, TerrainType.Land); // Create a new Cell with the cellID parameter
                if (AreaMap[x, y].Objects == null)
                {
                    AreaMap[x, y].Objects = new List<IInteractable>();
                }
            }
        }

        // Apply water edges
        SetWaterEdges(AreaMap, waterEdges, Size);

        // Custom tree placement logic for the forest
        PlaceTreesInForest();
    }

    private void PlaceTreesInForest()
    {
        int numberOfTrees = Random.Range(10, 16); // Generate between 10 and 16 trees
        Debug.Log($"Generating {numberOfTrees} trees in the forest.");

        // Retrieve the native tree fruit for this region
        RegionInfo regionInfo = RegionManager.Instance.GetRegionInfo(RegionNumber);

        if (regionInfo == null)
        {
            Debug.LogError($"RegionInfo is null for RegionNumber: {RegionNumber}. Defaulting to 'Apple'.");
        }

        string nativeTreeFruit = regionInfo?.NativeTreeFruit ?? "Apple"; // Default to "Apple" if no fruit found

        if (regionInfo == null || string.IsNullOrEmpty(nativeTreeFruit))
        {
            Debug.LogWarning($"No native tree fruit found for region {RegionNumber}. Defaulting to 'Apple'.");
        }
        else
        {
            Debug.Log($"Native tree fruit for region {RegionNumber}: {nativeTreeFruit}");
        }

        for (int i = 1; i <= numberOfTrees; i++)
        {
            int x = Random.Range(0, Size);
            int y = Random.Range(0, Size);
            Vector2Int treePosition = new Vector2Int(x, y);

            // Ensure no tree is already placed at the position and that the cell is empty
            if (!treePositions.Contains(treePosition) && AreaMap[x, y].Objects.Count == 0)
            {
                treePositions.Add(treePosition); // Store the position

                // Roll to determine if this tree will produce fruit
                bool producesFruit = UnityEngine.Random.value > 0.5f; // 50% chance to produce fruit
                Debug.Log($"Tree {i}: Position {treePosition}, Produces fruit: {producesFruit}");

                List<Item> initialFruits = new List<Item>();
                string treeType = producesFruit ? nativeTreeFruit : "Non-Fruit";

                if (producesFruit)
                {
                    // Log before generating fruits
                    Debug.Log($"Tree {i} at position {treePosition} is a fruit tree, generating fruits of type: {nativeTreeFruit}");

                    initialFruits = GenerateInitialFruitsForTree(nativeTreeFruit);

                    // Check if fruits were successfully generated
                    if (initialFruits == null || initialFruits.Count == 0)
                    {
                        Debug.LogWarning($"No initial fruits generated for tree {nativeTreeFruit} at position {treePosition}.");
                    }
                    else
                    {
                        Debug.Log($"Generated {initialFruits.Count} fruits for {nativeTreeFruit} tree at position {treePosition}.");
                    }
                }

                // Create the tree, passing whether it grows fruit
                Tree tree = new Tree(producesFruit ? nativeTreeFruit : "Non-Fruit", initialFruits, producesFruit);
                tree.Name = producesFruit ? $"{nativeTreeFruit} Tree {i}" : $"Non-Fruit Tree {i}"; // Name it accordingly
                tree.Position = treePosition;
                tree.IsPassable = false;
                tree.NestedMapPosition = treePosition;
                tree.CurrentNestedArea = this;
                tree.IsInNestedArea = true;

                // Log the tree details before placing it
                Debug.Log($"Placing tree '{tree.Name}' at position {treePosition}. Produces fruit: {producesFruit}");

                // Place the tree in the area
                AreaMap[x, y].Objects.Add(tree);
                AreaMap[x, y].Terrain = TerrainType.Dirt; // Set the terrain to Dirt

                // Add the tree to the list of plants
                AddPlantToArea(tree);

                Debug.Log($"Successfully placed {tree.Name} at {treePosition} with fruit status: {producesFruit}");
            }
            else
            {
                Debug.LogWarning($"Skipped tree placement for {treePosition} as it's already occupied or not suitable.");
            }
        }
    }



    private List<Item> GenerateInitialFruitsForTree(string nativeTreeFruit)
    {
        List<Item> initialFruits = new List<Item>();
        Item fruitItem = ItemGenerator.Instance.GenerateItem(nativeTreeFruit);

        if (fruitItem != null)
        {
            int quantity = Random.Range(1, 5); // Randomly decide how many fruits the tree starts with
            for (int i = 0; i < quantity; i++)
            {
                initialFruits.Add(fruitItem);
            }
        }
        else
        {
            Debug.LogWarning($"Could not generate initial fruits for tree with fruit type: {nativeTreeFruit}");
        }

        return initialFruits;
    }
}
