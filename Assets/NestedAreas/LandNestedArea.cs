using System.Collections.Generic;
using UnityEngine;

public class LandNestedArea : BaseNestedArea
{
    private List<string> waterEdges;
    private new const int Size = 9; // Fixed size for simplicity

    public LandNestedArea(List<string> initialWaterEdges, List<Animal> parentAnimals, Cell parentCell, int regionNumber)
    {
        RegionNumber = regionNumber;
        waterEdges = initialWaterEdges;

        // Set essential properties for the nested area

        ParentCell = parentCell;

        ParentCellID = parentCell.CellID;
        MainMapCellID = parentCell.CellID;

        // Pass animals to base class
        GeneratedAnimals = parentAnimals;
        Initialize();
        EntrancePosition = new Vector2Int(0, 0);
    }

    public override void Initialize()
    {
        AreaMap = new Cell[Size, Size];
        GenerateLand();

        // Call the orchestrator to perform all relevant checks (e.g., player start, etc.)
        OrchestrateParentCellChecks();

        // Determine the distance to the nearest forest and place trees accordingly
        int distanceToForest = MapGenerator.Instance.GetDistanceToNearestCellWithTerrainType(ParentCellID, TerrainType.Forest);
        AddTreesBasedOnProximityToForest(distanceToForest); // Use base class method

        // Determine the distance to the nearest mountain and place rocks accordingly
        int distanceToMountain = MapGenerator.Instance.GetDistanceToNearestCellWithTerrainType(ParentCellID, TerrainType.Mountain);
        int numberOfRocks = DetermineNumberOfRocks(distanceToMountain);
        PlaceRocks(numberOfRocks, distanceToMountain);

        int numberOfGrassPatchs = 12 - (NumberOfRocks + NumberOfTrees);
        PlaceLongGrass(numberOfGrassPatchs);

        // Generate and place animals using the base class methods
        GenerateAnimalsForCellID(ParentCellID);
    }

    private void GenerateLand()
    {
        // Initialize all cells as land and generate unique IDs
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                int cellID = GenerateUniqueCellID();
                AreaMap[x, y] = new Cell(cellID, x, y, TerrainType.Land)
                {
                    Objects = new List<IInteractable>() // Initialize objects list
                };
            }
        }

        // Set water edges using the method from the base class
        SetWaterEdges(AreaMap, waterEdges, Size);
    }
}
