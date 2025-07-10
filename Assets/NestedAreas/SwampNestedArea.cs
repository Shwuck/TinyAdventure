using System.Collections.Generic;
using UnityEngine;

public class SwampNestedArea : BaseNestedArea
{
    private List<string> waterEdges;
    private new const int Size = 9; // Fixed size for simplicity
    public int numberOfShrooms;
    public float waterChance;
    public float fungiChance;

    public SwampNestedArea(List<string> initialWaterEdges, List<Animal> parentAnimals, Cell parentCell, int regionNumber)
    {
        RegionNumber = regionNumber;
        waterEdges = initialWaterEdges;
        ParentCellID = parentCell.CellID;
        MainMapCellID = parentCell.CellID;


        // Pass animals to base class
        GeneratedAnimals = parentAnimals;
        Initialize();
        EntrancePosition = new Vector2Int(0, 0); // Entrance always at the same place
    }

    public override void Initialize()
    {
        AreaMap = new Cell[Size, Size];
        GenerateSwamp();

        // Determine proximity to other terrains for object placement
        int distanceToForest = MapGenerator.Instance.GetDistanceToNearestCellWithTerrainType(ParentCellID, TerrainType.Forest);
        AddTreesBasedOnProximityToForest(distanceToForest);

        int distanceToMountain = MapGenerator.Instance.GetDistanceToNearestCellWithTerrainType(ParentCellID, TerrainType.Mountain);
        int numberOfRocks = DetermineNumberOfRocks(distanceToMountain);
        PlaceRocks(numberOfRocks, distanceToMountain);


        GenerateRandomChances();


        if (waterChance <= 0.3f) // 30% chance
        {
            // Place random water patches within the swamp
            PlaceRandomWaterPatches();
        }

        if (fungiChance <= 0.3f)
        {
            numberOfShrooms = Random.Range(3, 15);
        }

        PlaceFungiNearWallsTreesOrRocks(numberOfShrooms);

        // Generate and place animals
        GenerateAnimalsForCellID(ParentCellID);
    }

    private void GenerateSwamp()
    {
        // Set all cells to Swamp and generate unique IDs
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                int cellID = GenerateUniqueCellID();
                AreaMap[x, y] = new Cell(cellID, x, y, TerrainType.Swamp)
                {
                    Objects = new List<IInteractable>()
                };
            }
        }

        // Set water edges
        SetWaterEdges(AreaMap, waterEdges, Size);
    }

    private void PlaceRandomWaterPatches()
    {
        int waterPatchCount = Random.Range(3, 7); // Place between 3 to 7 random water patches
        Debug.Log($"Placing {waterPatchCount} water patches in the swamp.");

        for (int i = 0; i < waterPatchCount; i++)
        {
            int x = Random.Range(1, Size - 1); // Random x position (avoid edges)
            int y = Random.Range(1, Size - 1); // Random y position (avoid edges)
            Vector2Int waterPosition = new Vector2Int(x, y);

            // Ensure the position is not already water or occupied
            if (AreaMap[x, y].Terrain != TerrainType.Water && AreaMap[x, y].Objects.Count == 0)
            {
                AreaMap[x, y].Terrain = TerrainType.Water;
                AreaMap[x, y].isPassable = false; // Make water impassable
                AreaMap[x, y].isFishable = true;  // Mark water as fishable if needed

                Debug.Log($"Placed water at {waterPosition}");
            }
            else
            {
                Debug.LogWarning($"Skipped water placement at {waterPosition} as it's already occupied or water.");
            }
        }
    }

    private void GenerateRandomChances()
    {
        waterChance = Random.Range(0f, 1f);
        fungiChance = Random.Range(0f, 1f);

    }


}
