using System.Collections.Generic;
using UnityEngine;

public class SandNestedArea : BaseNestedArea
{
    private List<string> waterEdges;
    private new const int Size = 9; // Fixed size for simplicity

    public SandNestedArea(List<string> initialWaterEdges, List<Animal> parentAnimals, Cell parentCell, int regionNumber)
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
        GenerateSand();

        // Call the orchestrator to perform all relevant checks (e.g., player start, etc.)
        OrchestrateParentCellChecks();

        // Place some driftwood or shells, based on sand terrain
     //   PlaceBeachObjects();

        // Place some random animals on the beach, like crabs or seabirds
        GenerateAnimalsForCellID(ParentCellID);
    }

    private void GenerateSand()
    {
        // Initialize all cells as sand and generate unique IDs
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                int cellID = GenerateUniqueCellID();
                AreaMap[x, y] = new Cell(cellID, x, y, TerrainType.Sand)
                {
                    Objects = new List<IInteractable>() // Initialize objects list
                };
            }
        }

        // Set water edges (e.g., sea) using the method from the base class
        SetWaterEdges(AreaMap, waterEdges, Size);
    }

  /*  private void PlaceBeachObjects()
    {
        // Randomly place objects like driftwood, shells, or small beach plants
        int numberOfBeachObjects = UnityEngine.Random.Range(3, 7);

        for (int i = 0; i < numberOfBeachObjects; i++)
        {
            Vector2Int position = GetRandomValidPosition();
            if (position != Vector2Int.zero)
            {
                IInteractable beachObject;

                // Randomly decide between a shell or driftwood
                if (UnityEngine.Random.value > 0.5f)
                {
                    beachObject = new Shell();  // Custom Shell class, not provided in your current scripts
                }
                else
                {
                    beachObject = new Driftwood();  // Custom Driftwood class, not provided in your current scripts
                }

                AddObjectToArea(beachObject);  // Add the object to the map
                Debug.Log($"Placed {beachObject.GetType().Name} at position {position}.");
            }
        }
    }

    */

    public override void HandlePlayerExitFromSpecificNestedAreaType(MapGenerator mapGenerator)
    {
        // Any special handling for when the player exits the beach area can be added here
        Debug.Log("Player exited the SandNestedArea.");
    }
}
