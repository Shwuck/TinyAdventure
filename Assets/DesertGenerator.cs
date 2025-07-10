using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DesertGenerator : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public int desertClusterSize = 50; // Adjust as needed for desired desert sizes
    public int numberOfDeserts = 3;    // Number of desert regions to generate
    public int desertsGenerated;

    // Minimum distance from water to consider a cell as a potential desert start point
    public int minDistanceFromWater = 5;

    public void GenerateDeserts()
    {
        if (mapGenerator == null || mapGenerator.map == null)
        {
            Debug.LogError("MapGenerator reference not set or map not generated.");
            return;
        }

        // Use the same seed to ensure consistency
        Random.InitState(GameManager.Instance.GameSeed + desertsGenerated);

        List<Cell> potentialDesertCells = IdentifyPotentialDesertAreas();

        int attempts = 0;
        int maxAttempts = 1000; // To prevent potential infinite loops

        while (desertsGenerated < numberOfDeserts && attempts < maxAttempts)
        {
            attempts++;
            if (potentialDesertCells.Count == 0)
            {
                Debug.LogWarning("Not enough suitable areas to generate more deserts.");
                break;
            }

            int randomIndex = Random.Range(0, potentialDesertCells.Count);
            Cell desertStartCell = potentialDesertCells[randomIndex];
            potentialDesertCells.RemoveAt(randomIndex);

            if (IsSuitableForDesert(desertStartCell))
            {
                GenerateDesertCluster(desertStartCell.Coordinates, desertClusterSize);
                desertsGenerated++;
            }
        }

        if (attempts >= maxAttempts)
        {
            Debug.LogWarning("Reached maximum attempts while generating deserts.");
        }
    }

    List<Cell> IdentifyPotentialDesertAreas()
    {
        List<Cell> potentialCells = new List<Cell>();

        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                Cell cell = mapGenerator.map[x, y];

                if (cell.Terrain == TerrainType.Land || cell.Terrain == TerrainType.Plains)
                {
                    if (GetDistanceToNearestWater(cell) >= minDistanceFromWater)
                    {
                        potentialCells.Add(cell);
                    }
                }
            }
        }

        return potentialCells;
    }

    int GetDistanceToNearestWater(Cell cell)
    {
        // Perform a breadth-first search to find the nearest water cell
        Queue<(Cell, int)> queue = new Queue<(Cell, int)>();
        HashSet<Cell> visited = new HashSet<Cell>();

        queue.Enqueue((cell, 0));
        visited.Add(cell);

        while (queue.Count > 0)
        {
            var (currentCell, distance) = queue.Dequeue();

            if (currentCell.Terrain == TerrainType.Water || currentCell.Terrain == TerrainType.River || currentCell.Terrain == TerrainType.Lake)
            {
                return distance;
            }

            foreach (var neighborCoords in currentCell.AdjacentCells.Values)
            {
                if (neighborCoords.HasValue)
                {
                    Cell neighborCell = mapGenerator.GetCell(neighborCoords.Value);
                    if (neighborCell != null && !visited.Contains(neighborCell))
                    {
                        queue.Enqueue((neighborCell, distance + 1));
                        visited.Add(neighborCell);
                    }
                }
            }
        }

        // If no water found, return a large number
        return int.MaxValue;
    }

    bool IsSuitableForDesert(Cell cell)
    {
        // Ensure the cell is not already a desert and not water or river
        if (cell.Terrain == TerrainType.Desert ||
            cell.Terrain == TerrainType.Water ||
            cell.Terrain == TerrainType.Lake ||
            cell.Terrain == TerrainType.Mountain ||
            cell.Terrain == TerrainType.River)
        {
            return false;
        }
        return true;
    }

    void GenerateDesertCluster(Vector2Int start, int size)
    {
        Queue<Vector2Int> cellsToCheck = new Queue<Vector2Int>();
        HashSet<Vector2Int> visitedCells = new HashSet<Vector2Int>();
        cellsToCheck.Enqueue(start);

        int cellsAdded = 0;

        // Determine the terrain type based on the climate
        TerrainType targetTerrain = (GameManager.Instance.climate == Climate.Polar) ? TerrainType.Tundra : TerrainType.Desert;

        while (cellsToCheck.Count > 0 && cellsAdded < size)
        {
            Vector2Int current = cellsToCheck.Dequeue();
            int x = current.x;
            int y = current.y;

            if (x >= 0 && x < mapGenerator.width && y >= 0 && y < mapGenerator.height && !visitedCells.Contains(current))
            {
                visitedCells.Add(current);
                Cell cell = mapGenerator.map[x, y];

                if (cell.Terrain != TerrainType.Water && cell.Terrain != TerrainType.Mountain && cell.Terrain != TerrainType.MountainPeak)
                {
                    cell.Terrain = targetTerrain;
                    cell.TerrainToDisplay = targetTerrain;
                    cellsAdded++;

                    // Randomly decide whether to enqueue adjacent cells to create organic shapes
                    foreach (var direction in cell.AdjacentCells.Values)
                    {
                        if (direction.HasValue && Random.value < 0.8f) // 80% chance to expand in this direction
                        {
                            cellsToCheck.Enqueue(direction.Value);
                        }
                    }
                }
            }
        }
    }

}
