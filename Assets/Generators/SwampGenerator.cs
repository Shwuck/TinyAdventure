using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwampGenerator : MonoBehaviour
{
    public int swampClusterSize = 20; // Size of each swamp cluster

    public void GenerateSwamps(int numberOfSwamps)
    {
        // Use the same seed to ensure consistency
        Random.InitState(GameManager.Instance.GameSeed);

        List<Cell> potentialSwampCells = IdentifySwampAreas();

        int swampsGenerated = 0;
        while (swampsGenerated < numberOfSwamps && potentialSwampCells.Count > 0)
        {
            int randomIndex = Random.Range(0, potentialSwampCells.Count);
            Cell swampStartCell = potentialSwampCells[randomIndex];
            potentialSwampCells.RemoveAt(randomIndex);

            GenerateSwampCluster(swampStartCell.Coordinates, swampClusterSize);
            swampsGenerated++;
        }
    }

    List<Cell> IdentifySwampAreas()
    {
        List<Cell> potentialSwampCells = new List<Cell>();

        for (int x = 0; x < MapGenerator.Instance.width; x++)
        {
            for (int y = 0; y < MapGenerator.Instance.height; y++)
            {
                Cell cell = MapGenerator.Instance.map[x, y];

                if (cell.Terrain == TerrainType.Land || cell.Terrain == TerrainType.Forest)
                {
                    bool adjacentToForest = IsAdjacentToTerrainType(x, y, TerrainType.Forest);
                    bool adjacentToWater = IsAdjacentToTerrainType(x, y, TerrainType.Water);

                    if (adjacentToForest && adjacentToWater)
                    {
                        potentialSwampCells.Add(cell);
                    }
                }
            }
        }

        return potentialSwampCells;
    }

    bool IsAdjacentToTerrainType(int x, int y, TerrainType terrainType)
    {
        if (x > 0 && MapGenerator.Instance.map[x - 1, y].Terrain == terrainType) return true;
        if (x < MapGenerator.Instance.width - 1 && MapGenerator.Instance.map[x + 1, y].Terrain == terrainType) return true;
        if (y > 0 && MapGenerator.Instance.map[x, y - 1].Terrain == terrainType) return true;
        if (y < MapGenerator.Instance.height - 1 && MapGenerator.Instance.map[x, y + 1].Terrain == terrainType) return true;

        // Check diagonals
        if (x > 0 && y > 0 && MapGenerator.Instance.map[x - 1, y - 1].Terrain == terrainType) return true;
        if (x < MapGenerator.Instance.width - 1 && y > 0 && MapGenerator.Instance.map[x + 1, y - 1].Terrain == terrainType) return true;
        if (x > 0 && y < MapGenerator.Instance.height - 1 && MapGenerator.Instance.map[x - 1, y + 1].Terrain == terrainType) return true;
        if (x < MapGenerator.Instance.width - 1 && y < MapGenerator.Instance.height - 1 && MapGenerator.Instance.map[x + 1, y + 1].Terrain == terrainType) return true;

        return false;
    }

    void GenerateSwampCluster(Vector2Int start, int size)
    {
        // Use the same seed to ensure consistency
        Random.InitState(GameManager.Instance.GameSeed);

        Queue<Vector2Int> cellsToCheck = new Queue<Vector2Int>();
        HashSet<Vector2Int> visitedCells = new HashSet<Vector2Int>();
        cellsToCheck.Enqueue(start);

        int cellsAdded = 0;

        while (cellsToCheck.Count > 0 && cellsAdded < size)
        {
            Vector2Int current = cellsToCheck.Dequeue();
            int x = current.x;
            int y = current.y;

            if (x >= 0 && x < MapGenerator.Instance.width && y >= 0 && y < MapGenerator.Instance.height && !visitedCells.Contains(current))
            {
                visitedCells.Add(current);
                Cell cell = MapGenerator.Instance.map[x, y];

                if (cell.Terrain == TerrainType.Land || cell.Terrain == TerrainType.Forest)
                {
                    cell.Terrain = TerrainType.Swamp;
                    cell.TerrainToDisplay = TerrainType.Swamp;
                    cellsAdded++;

                    // Enqueue adjacent cells to grow the swamp
                    cellsToCheck.Enqueue(new Vector2Int(x + 1, y));
                    cellsToCheck.Enqueue(new Vector2Int(x - 1, y));
                    cellsToCheck.Enqueue(new Vector2Int(x, y + 1));
                    cellsToCheck.Enqueue(new Vector2Int(x, y - 1));
                }
            }
        }
    }
}
