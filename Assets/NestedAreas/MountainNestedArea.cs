using System.Collections.Generic;
using UnityEngine;

public class MountainNestedArea : BaseNestedArea
{
    private List<string> waterEdges;
    private new const int Size = 9; // Fixed size for simplicity

    public MountainNestedArea(List<string> initialWaterEdges, List<Animal> parentAnimals, Cell parentCell, int regionNumber)
    {
        RegionNumber = regionNumber;
        waterEdges = initialWaterEdges;
        ParentCell = parentCell;
        ParentCellID = ParentCell.CellID;
        MainMapCellID = ParentCell.CellID;

        SetParentCell();
        UpdateNestedAreaLevel();

        // Pass animals to base class
        GeneratedAnimals = parentAnimals;
        Initialize();
        EntrancePosition = new Vector2Int(0, 0); // Entrance always at the same place
    }

    public override void Initialize()
    {
        AreaMap = new Cell[Size, Size];

        GenerateMountain();

        SetUpNestedArea();
        // Generate and place animals
        GenerateAnimalsForCellID(ParentCellID);
    }

    private void GenerateMountain()
    {
        // Use the fully qualified name to avoid any potential conflict with a custom 'Random' class
        System.Random random = new System.Random();

        // Initialize all cells with a 20% chance of being TerrainType.Slate
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                int cellID = GenerateUniqueCellID(); // Generate a unique cell ID

                // Determine if the cell should be Stone or Slate (20% chance for Slate)
                TerrainType terrain = random.NextDouble() < 0.2 ? TerrainType.Slate : TerrainType.Stone;

                // Set the terrain type and initialize the cell
                AreaMap[x, y] = new Cell(cellID, x, y, terrain);

                // Initialize Objects list if it is null
                if (AreaMap[x, y].Objects == null)
                {
                    AreaMap[x, y].Objects = new List<IInteractable>();
                }
            }
        }
        // Apply water edges
        SetWaterEdges(AreaMap, waterEdges, Size);

        // Place rocks in the mountain area
        PlaceRocksInMountain();
    }

    private void PlaceRocksInMountain()
    {
        int areaLevel = NestedAreaLevel;

        int numberOfRocks = Random.Range(10, 16); // Generate between 10 and 16 rocks
        Debug.Log($"Generating {numberOfRocks} rocks in the mountain area.");

        for (int i = 1; i <= numberOfRocks; i++)
        {
            int x = Random.Range(0, Size);
            int y = Random.Range(0, Size);
            Vector2Int rockPosition = new Vector2Int(x, y);

            // Ensure no rock is already placed at the position and that the cell is empty
            if (AreaMap[x, y].Objects.Count == 0)
            {
                // Log the rock placement before adding it
                Debug.Log($"Rock {i}: Position {rockPosition}");

                // Create the rock object
                Rock rock = new Rock(areaLevel);
                rock.Name = $"Rock {i}";
                rock.Position = rockPosition;
                rock.IsPassable = false;
                rock.NestedMapPosition = rockPosition;
                rock.CurrentNestedArea = this;

                // Log the rock details before placing it
                Debug.Log($"Placing rock '{rock.Name}' at position {rockPosition}.");

                // Place the rock in the area
                AreaMap[x, y].Objects.Add(rock);
                AreaMap[x, y].Terrain = TerrainType.Stone; // Set the terrain to Stone

                // Add the rock to the area
                Debug.Log($"Successfully placed {rock.Name} at {rockPosition}");
            }
            else
            {
                Debug.LogWarning($"Skipped rock placement for {rockPosition} as it's already occupied.");
            }
        }
    }
}
