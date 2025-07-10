using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class NuanceGenerator : MonoBehaviour
{
    public MapGenerator mapGenerator;
    private List<Vector2Int> checkedCells = new List<Vector2Int>();

    public void AddMapNuances()
    {
        if (mapGenerator == null || mapGenerator.map == null)
        {
            Debug.LogError("MapGenerator reference not set or map not generated.");
            return;
        }

        AddSandAtRiverBends();
        GenerateSwampBetweenForestAndRiver();
        GenerateGladesInForests();
    }

    private void GenerateGladesInForests()
    {
        if (mapGenerator == null || mapGenerator.map == null)
        {
            Debug.LogError("MapGenerator reference not set or map not generated.");
            return;
        }

        checkedCells.Clear(); // Clear the list of checked cells

        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                if (!checkedCells.Contains(cellPosition) && mapGenerator.map[x, y].Terrain == TerrainType.Forest)
                {
                    List<Vector2Int> forestCluster = FindForestCluster(cellPosition);
                    if (forestCluster.Count >= 50) // Check if the forest cluster is large enough for a glade
                    {
                        PlaceGladeInForest(forestCluster);
                    }
                }
            }
        }
    }

    private List<Vector2Int> FindForestCluster(Vector2Int startCell)
    {
        List<Vector2Int> cluster = new List<Vector2Int>();
        Queue<Vector2Int> cellsToCheck = new Queue<Vector2Int>();
        cellsToCheck.Enqueue(startCell);

        while (cellsToCheck.Count > 0)
        {
            Vector2Int cell = cellsToCheck.Dequeue();
            if (!checkedCells.Contains(cell) && IsWithinBounds(cell.x, cell.y) && mapGenerator.map[cell.x, cell.y].Terrain == TerrainType.Forest)
            {
                checkedCells.Add(cell);
                cluster.Add(cell);

                // Enqueue all adjacent forest cells
                EnqueueIfValid(cellsToCheck, cell + Vector2Int.up);
                EnqueueIfValid(cellsToCheck, cell + Vector2Int.down);
                EnqueueIfValid(cellsToCheck, cell + Vector2Int.left);
                EnqueueIfValid(cellsToCheck, cell + Vector2Int.right);
            }
        }

        return cluster;
    }

    private void EnqueueIfValid(Queue<Vector2Int> queue, Vector2Int cell)
    {
        if (IsWithinBounds(cell.x, cell.y) && !checkedCells.Contains(cell))
        {
            queue.Enqueue(cell);
        }
    }

    private void PlaceGladeInForest(List<Vector2Int> forestCluster)
    {
        // Randomly select a cell within the cluster for the glade, ensuring it's somewhat central
        Vector2Int gladeCell = forestCluster[Random.Range(forestCluster.Count / 4, 3 * forestCluster.Count / 4)];
        mapGenerator.map[gladeCell.x, gladeCell.y].Terrain = TerrainType.Glade;
    }

    private void GenerateSwampBetweenForestAndRiver()
    {
        if (mapGenerator == null || mapGenerator.map == null)
        {
            Debug.LogError("MapGenerator reference not set or map not generated.");
            return;
        }

        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                Cell currentCell = mapGenerator.map[x, y];

                if (currentCell.Terrain == TerrainType.Forest)
                {
                    CheckAndGenerateSwamp(x, y, TerrainType.River, TerrainType.Swamp, 3);
                }
            }
        }
    }

    private void CheckAndGenerateSwamp(int startX, int startY, TerrainType targetTerrain, TerrainType fillTerrain, int maxDistance)
    {
        for (int x = startX - maxDistance; x <= startX + maxDistance; x++)
        {
            for (int y = startY - maxDistance; y <= startY + maxDistance; y++)
            {
                if (IsWithinBounds(x, y) && mapGenerator.map[x, y].Terrain == targetTerrain)
                {
                    int distanceX = Mathf.Abs(x - startX);
                    int distanceY = Mathf.Abs(y - startY);

                    if ((distanceX == maxDistance && distanceY < maxDistance) || (distanceY == maxDistance && distanceX < maxDistance))
                    {
                        FillIntermediateTerrain(startX, startY, x, y, fillTerrain);
                    }
                }
            }
        }
    }

    private bool IsWithinBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < mapGenerator.width && y < mapGenerator.height;
    }

    private void FillIntermediateTerrain(int startX, int startY, int endX, int endY, TerrainType fillTerrain)
    {
        int directionX = Mathf.Clamp(endX - startX, -1, 1);
        int directionY = Mathf.Clamp(endY - startY, -1, 1);

        int currentX = startX + directionX;
        int currentY = startY + directionY;

        while (currentX != endX || currentY != endY)
        {
            if (IsWithinBounds(currentX, currentY))
            {
                mapGenerator.map[currentX, currentY].Terrain = fillTerrain;
            }

            if (currentX != endX) currentX += directionX;
            if (currentY != endY) currentY += directionY;
        }
    }

    private void AddSandAtRiverBends()
    {
        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                // Check cells adjacent to rivers for potential conversion to sand
                if (mapGenerator.map[x, y].Terrain != TerrainType.River && AdjacentToRiverBend(x, y))
                {
                    // Convert this cell's terrain to Sand if it's adjacent to a river bend
                    mapGenerator.map[x, y].Terrain = TerrainType.Sand;
                }
            }
        }
    }

    private bool AdjacentToRiverBend(int x, int y)
    {
        // Checks for river cells in all four directions and diagonally to identify river bends
        bool hasNorthRiver = y + 1 < mapGenerator.height && mapGenerator.map[x, y + 1].Terrain == TerrainType.River;
        bool hasSouthRiver = y - 1 >= 0 && mapGenerator.map[x, y - 1].Terrain == TerrainType.River;
        bool hasEastRiver = x + 1 < mapGenerator.width && mapGenerator.map[x + 1, y].Terrain == TerrainType.River;
        bool hasWestRiver = x - 1 >= 0 && mapGenerator.map[x - 1, y].Terrain == TerrainType.River;

        // Diagonal checks
        bool hasNorthEastRiver = x + 1 < mapGenerator.width && y + 1 < mapGenerator.height && mapGenerator.map[x + 1, y + 1].Terrain == TerrainType.River;
        bool hasNorthWestRiver = x - 1 >= 0 && y + 1 < mapGenerator.height && mapGenerator.map[x - 1, y + 1].Terrain == TerrainType.River;
        bool hasSouthEastRiver = x + 1 < mapGenerator.width && y - 1 >= 0 && mapGenerator.map[x + 1, y - 1].Terrain == TerrainType.River;
        bool hasSouthWestRiver = x - 1 >= 0 && y - 1 >= 0 && mapGenerator.map[x - 1, y - 1].Terrain == TerrainType.River;

        // Random chance to add some unpredictability
        float randomChance = Random.Range(1, 11); // Generates a number between 1 and 10

        // Logic to determine if adjacent to a river bend based on the surrounding river cells
        if ((hasNorthRiver || hasSouthRiver) && (hasEastRiver || hasWestRiver) && randomChance <= 3.5)
        {
            return true;
        }
        else if ((hasNorthEastRiver || hasNorthWestRiver || hasSouthEastRiver || hasSouthWestRiver) &&
                 (hasNorthRiver || hasSouthRiver || hasEastRiver || hasWestRiver) && randomChance <= 0.5)
        {
            return true;
        }

        return false;
    }
}
