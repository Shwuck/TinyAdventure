using System.Collections.Generic;
using UnityEngine;

public class VillageGenerator : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public CivilisationManager civilisationManager;
    public int proximityRadius = 8; // Radius within which to penalize positions near existing villages
    public int proximityPenalty = 5; // Penalty for positions within the proximity radius of existing villages

    public void PlaceVillage()
    {
        if (mapGenerator == null || mapGenerator.map == null)
        {
            Debug.LogError("MapGenerator not set or map not generated.");
            return;
        }

        List<(Vector2Int, int)> scoredCells = new List<(Vector2Int, int)>();

        // Get list of village cells
        List<Cell> villageCells = mapGenerator.GetCellsByTerrain(TerrainType.Village);

        // Score cells
        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                Cell currentCell = mapGenerator.map[x, y];
                if (currentCell.Terrain != TerrainType.River && currentCell.Terrain != TerrainType.Forest && currentCell.Terrain != TerrainType.Bridge && !currentCell.hasNestedArea)
                {
                    int score = ScoreCell(x, y, villageCells); // Pass villageCells to avoid redundant iteration
                    if (score > 0) // Only consider cells with a positive score
                    {
                        scoredCells.Add((new Vector2Int(x, y), score));
                    }
                }
            }
        }

        // Sort and select location for the village
        scoredCells.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        Vector2Int villageLocation = SelectLocation(scoredCells);

        if (villageLocation != Vector2Int.zero)
        {
            // Update the cell's terrain type to Village
            mapGenerator.map[villageLocation.x, villageLocation.y].Terrain = TerrainType.Village;

            // Instantiate and assign a Village to the cell
            Village newVillage = new Village(); // Assuming Village implements INestedArea
            mapGenerator.map[villageLocation.x, villageLocation.y].SetNestedArea(newVillage);
            Debug.Log($"Village placed at {villageLocation.x}, {villageLocation.y}");

            // Link the village to the nearest road
            LinkVillageToRoad(mapGenerator.map[villageLocation.x, villageLocation.y]);

            // Update the cell's terrain type to Village
            mapGenerator.map[villageLocation.x, villageLocation.y].Terrain = TerrainType.Village;
            if (civilisationManager != null)
            {
                civilisationManager.AddVillage(newVillage);
            }
        }
    }


    private int ScoreCell(int x, int y, List<Cell> villageCells)
    {
        int score = 5;

        // Check adjacent cells
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in directions)
        {
            Vector2Int adjacentPos = new Vector2Int(x, y) + dir;
            if (IsValidPosition(adjacentPos))
            {
                Cell adjacentCell = mapGenerator.map[adjacentPos.x, adjacentPos.y];
                if (adjacentCell.Terrain == TerrainType.River)
                {
                    score += 10;
                }
                else if (adjacentCell.Terrain == TerrainType.Forest)
                {
                    score += 5;
                }
            }
        }

        // Penalize position based on proximity to existing villages
        foreach (var cell in villageCells)
        {
            Vector2Int villagePos = cell.Coordinates; // Accessing the Coordinates property
            float distance = Vector2Int.Distance(new Vector2Int(x, y), villagePos);
            if (distance <= proximityRadius)
            {
                score -= proximityPenalty;
            }
        }

        return score;
    }

    private bool IsValidPosition(Vector2Int position)
    {
        return position.x >= 0 && position.x < mapGenerator.width && position.y >= 0 && position.y < mapGenerator.height;
    }

    private Vector2Int SelectLocation(List<(Vector2Int, int)> scoredCells)
    {
        // If there are no positive-scored cells, select a random cell among all scored cells
        if (scoredCells.Count == 0)
        {
            Debug.LogWarning("No positive-scored cells found. Placing village randomly.");
            if (scoredCells.Count > 0)
            {
                return scoredCells[Random.Range(0, scoredCells.Count)].Item1;
            }
            else
            {
                // If there are no scored cells at all, return Vector2Int.zero as a fallback
                Debug.LogError("No scored cells available for village placement.");
                return Vector2Int.zero;
            }
        }

        // Sort the scored cells by score (descending order)
        scoredCells.Sort((a, b) => b.Item2.CompareTo(a.Item2));

        // Select a random cell among the scored cells
        int index = Random.Range(0, scoredCells.Count);
        return scoredCells[index].Item1;
    }



    public void LinkVillageToRoad(Cell villageCell)
    {
        // Find the nearest road cell
        Cell nearestRoadCell = FindNearestRoadCell(villageCell);

        if (nearestRoadCell != null)
        {
            // Call the RoadGenerator to generate road between village and nearest road
            GameManager.Instance.startCellCoordinates = villageCell.Coordinates;
            GameManager.Instance.endCellCoordinates = nearestRoadCell.Coordinates;
            GameManager.Instance.manualRoadOverride = true;
            RoadGenerator roadGenerator = FindObjectOfType<RoadGenerator>(); // Assuming there's only one RoadGenerator in the scene
            roadGenerator.StartRoadGeneration();
        }
        else
        {
            Debug.LogWarning("No road found near the village. The village will generate without a road.");
        }
  
    }

    private Cell FindNearestRoadCell(Cell villageCell)
    {
        float minDistance = float.MaxValue;
        Cell nearestRoadCell = null;

        // Loop through all cells in the map to find the nearest road cell
        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                Cell currentCell = mapGenerator.map[x, y];
                // Check if the current cell is a road and calculate its distance from the village
                if (currentCell.Terrain == TerrainType.Road)
                {
                    float distance = Vector2Int.Distance(villageCell.Coordinates, currentCell.Coordinates);
                    // Update nearest road cell if this road cell is closer
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestRoadCell = currentCell;
                    }
                }
            }
        }

        return nearestRoadCell;
    }

}
