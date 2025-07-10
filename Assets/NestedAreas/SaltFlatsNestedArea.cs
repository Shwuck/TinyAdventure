using System.Collections.Generic;
using UnityEngine;

public class SaltFlatsNestedArea : BaseNestedArea
{
    private List<string> waterEdges;
    private new const int Size = 9; // Fixed size for simplicity

    public SaltFlatsNestedArea(List<string> initialWaterEdges, List<Animal> parentAnimals, Cell parentCell, int regionNumber)
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
        GenerateSaltFlats();

        // Call the orchestrator to perform all relevant checks (e.g., player start, etc.)
        OrchestrateParentCellChecks();

        // Generate and place adapted animals, like birds or lizards
        GenerateAnimalsForCellID(ParentCellID);
    }

    private void GenerateSaltFlats()
    {

        // Use the fully qualified name to avoid conflict with any custom 'Random' class
        System.Random random = new System.Random();

        // Initialize all cells with a 20% chance of being TerrainType.Salt
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                int cellID = GenerateUniqueCellID();

                // Determine if the cell should be Saltflat or Salt (20% chance for Salt)
                TerrainType terrain = random.NextDouble() < 0.2 ? TerrainType.Salt : TerrainType.Saltflat;

                AreaMap[x, y] = new Cell(cellID, x, y, terrain)
                {
                    Objects = new List<IInteractable>() // Initialize objects list
                };
            }
        }


        // Set water edges using the method from the base class, but it's optional for salt flats
        SetWaterEdges(AreaMap, waterEdges, Size);
    }

    public override void HandlePlayerExitFromSpecificNestedAreaType(MapGenerator mapGenerator)
    {
        // Any special handling for when the player exits the salt flats area can be added here
        Debug.Log("Player exited the SaltFlatsNestedArea.");
    }
}
