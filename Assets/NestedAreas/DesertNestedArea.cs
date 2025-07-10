using System.Collections.Generic;
using UnityEngine;

public class DesertNestedArea : BaseNestedArea
{
    private List<string> waterEdges;
    private new const int Size = 9; // Fixed size for simplicity

    public DesertNestedArea(List<string> initialWaterEdges, List<Animal> parentAnimals, Cell parentCell, int regionNumber)
    {
        RegionNumber = regionNumber;
        waterEdges = initialWaterEdges;
        ParentCellID = parentCell.CellID;
        MainMapCellID = parentCell.CellID;

        // Pass animals to base class
        GeneratedAnimals = parentAnimals;
        Initialize();
        EntrancePosition = new Vector2Int(0, 0); // Set the entrance position
    }

    public override void Initialize()
    {
        AreaMap = new Cell[Size, Size];
        GenerateDesert();

        // Add sparse rocks based on a coin flip
        if (Random.value > 0.5f)
        {
            PlaceSparseRocks();
        }

        // Generate and place animals (if applicable)
        GenerateAnimalsForCellID(ParentCellID);
    }

    private void GenerateDesert()
    {
        // Initialize all cells as Sand and generate unique IDs
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                int cellID = GenerateUniqueCellID();
                AreaMap[x, y] = new Cell(cellID, x, y, TerrainType.Sand)
                {
                    Objects = new List<IInteractable>()
                };
            }
        }

        // Set water edges
        SetWaterEdges(AreaMap, waterEdges, Size);
    }

    private void PlaceSparseRocks()
    {
        int numberOfRocks = Random.Range(2, 6); // Randomly place between 2 to 6 rocks

        for (int i = 0; i < numberOfRocks; i++)
        {
            int x = Random.Range(0, Size);
            int y = Random.Range(0, Size);

            Vector2Int rockPosition = new Vector2Int(x, y);

            if (AreaMap[x, y].Objects.Count == 0) // Ensure the cell is empty before placing
            {
                Rock rock = new SmallRock(); // You can switch between SmallRock and LargeRock if you wish
                rock.Position = rockPosition;
                AreaMap[x, y].Objects.Add(rock);
            }
        }
    }
}
