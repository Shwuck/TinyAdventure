using System.Collections.Generic;
using UnityEngine;

public class TundraNestedArea : BaseNestedArea
{
    private new const int Size = 9; // Fixed size for simplicity

    public TundraNestedArea(Cell parentCell, int regionNumber)
    {
        RegionNumber = regionNumber;

        // Set essential properties for the nested area
        ParentCell = parentCell;
        ParentCellID = parentCell.CellID;
        MainMapCellID = parentCell.CellID;

        Initialize();
        EntrancePosition = new Vector2Int(0, 0);
    }

    public override void Initialize()
    {
        AreaMap = new Cell[Size, Size];
        GenerateTundra();

        // Call the orchestrator to perform all relevant checks (e.g., player start, etc.)
        OrchestrateParentCellChecks();
    }

    private void GenerateTundra()
    {
        // Initialize all cells as snow-covered tundra and generate unique IDs
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                int cellID = GenerateUniqueCellID();
                AreaMap[x, y] = new Cell(cellID, x, y, TerrainType.Snow)
                {
                    Objects = new List<IInteractable>() // Initialize an empty objects list
                };
            }
        }
    }

    public override void HandlePlayerExitFromSpecificNestedAreaType(MapGenerator mapGenerator)
    {
        // Any special handling for when the player exits the tundra area can be added here
        Debug.Log("Player exited the TundraNestedArea.");
    }
}
