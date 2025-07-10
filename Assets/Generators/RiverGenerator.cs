using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class RiverGenerator : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public int numberOfRivers = 4; // Number of rivers to generate
    public int cellsPerDirection = 3; // Number of cells to move in one direction before choosing a new direction

    public void GenerateRivers()
    {
        if (mapGenerator == null || mapGenerator.map == null)
        {
            Debug.LogError("MapGenerator reference not set or map not generated.");
            return;
        }

        Random.InitState(GameManager.Instance.GameSeed + 3); // +3 to differentiate from other generators

        for (int i = 0; i < numberOfRivers; i++)
        {
            Cell startCell = SelectStartingCell();
            if (startCell != null)
            {
                Cell endCell = FindNearestWaterCell(startCell);
                if (endCell != null)
                {
                    GenerateRiverTowardsCell(startCell, endCell);
                }
                else
                {
                    Debug.LogWarning("Failed to find a suitable end cell for a river.");
                }
            }
            else
            {
                Debug.LogWarning("Failed to find a suitable starting cell for a river.");
            }
        }
    }

    Cell SelectStartingCell()
    {
        List<Cell> potentialCells = new List<Cell>();

        foreach (var cell in mapGenerator.allCells)
        {
            if (cell.Terrain != TerrainType.Water)
            {
                potentialCells.Add(cell);
            }
        }

        if (potentialCells.Count == 0) return null;

        float totalWeight = 0f;
        foreach (var cell in potentialCells)
        {
            float weight = Mathf.Max(0.1f, Mathf.Exp(-Mathf.Abs(cell.NoiseValue - 7.5f)));
            totalWeight += weight;
        }

        float randomValue = Random.value * totalWeight;
        float cumulativeWeight = 0f;

        foreach (var cell in potentialCells)
        {
            float weight = Mathf.Max(0.1f, Mathf.Exp(-Mathf.Abs(cell.NoiseValue - 7.5f)));
            cumulativeWeight += weight;

            if (randomValue <= cumulativeWeight)
            {
                return cell;
            }
        }

        return potentialCells[potentialCells.Count - 1];
    }

    Cell FindNearestWaterCell(Cell startCell)
    {
        Queue<Cell> queue = new Queue<Cell>();
        HashSet<Cell> visited = new HashSet<Cell>();
        queue.Enqueue(startCell);
        visited.Add(startCell);

        while (queue.Count > 0)
        {
            Cell currentCell = queue.Dequeue();

            if (currentCell.Terrain == TerrainType.Water)
            {
                return currentCell;
            }

            foreach (var direction in currentCell.AdjacentCells.Values)
            {
                if (direction.HasValue)
                {
                    Cell neighborCell = mapGenerator.GetCell(direction.Value);
                    if (neighborCell != null && !visited.Contains(neighborCell))
                    {
                        queue.Enqueue(neighborCell);
                        visited.Add(neighborCell);
                    }
                }
            }
        }

        return null; // Return null if no water cell is found
    }

    void GenerateRiverTowardsCell(Cell startCell, Cell endCell)
    {
        Cell currentCell = startCell;
        Cell previousCell = null;
        Vector2Int? lastDirection = null;
        int cellsMovedInCurrentDirection = 0;
        int safetyCounter = 0;

        Debug.Log($"Starting river generation at CellID: {startCell.CellID} and aiming towards CellID: {endCell.CellID}");

        while (currentCell != null && safetyCounter < mapGenerator.width * mapGenerator.height)
        {
            safetyCounter++;
            currentCell.Terrain = TerrainType.Water;
            currentCell.TerrainToDisplay = TerrainType.River;
            currentCell.isPassable = false;

            if (currentCell == endCell)
            {
                Debug.Log($"River reached the end point at CellID: {currentCell.CellID}");
                break;
            }

            // Check for water on either side before continuing
            if (IsAdjacentToWater(currentCell, lastDirection))
            {
                Debug.Log($"River ended at CellID: {currentCell.CellID} at Coordinates: {currentCell.Coordinates}. Reason: Encountered adjacent water.");
                break;
            }

            // Move in the same direction for a set number of cells before re-evaluating
            if (cellsMovedInCurrentDirection >= cellsPerDirection || lastDirection == null)
            {
                lastDirection = GetNextDirectionTowardsEnd(currentCell, previousCell, lastDirection, endCell);
                cellsMovedInCurrentDirection = 0;
            }

            if (lastDirection == null)
            {
                Debug.Log($"River ended at CellID: {currentCell.CellID} at Coordinates: {currentCell.Coordinates}. Reason: No valid direction found.");
                break;
            }

            Vector2Int nextPosition = currentCell.Coordinates + lastDirection.Value;

            if (!mapGenerator.IsPositionValid(nextPosition))
            {
                Debug.Log($"River ended at CellID: {currentCell.CellID} at Coordinates: {currentCell.Coordinates}. Reason: Out of bounds.");
                break;
            }

            Cell nextCell = mapGenerator.GetCell(nextPosition);

            if (nextCell.Terrain == TerrainType.Water)
            {
                Debug.Log($"River ended at CellID: {currentCell.CellID} at Coordinates: {currentCell.Coordinates}. Reason: Encountered water.");
                break;
            }

            previousCell = currentCell;
            currentCell = nextCell;
            cellsMovedInCurrentDirection++;
        }

        if (safetyCounter >= mapGenerator.width * mapGenerator.height)
        {
            Debug.LogWarning("River generation stopped due to safety limit. Potential infinite loop avoided.");
        }
    }


    bool IsAdjacentToWater(Cell cell, Vector2Int? lastDirection)
    {
        Vector2Int[] directions = new Vector2Int[]
        {
        new Vector2Int(0, 1),  // North
        new Vector2Int(1, 0),  // East
        new Vector2Int(0, -1), // South
        new Vector2Int(-1, 0)  // West
        };

        foreach (var direction in directions)
        {
            // Skip the direction we came from (lastDirection)
            if (lastDirection.HasValue && direction == -lastDirection.Value)
            {
                continue;
            }

            Vector2Int newPosition = cell.Coordinates + direction;
            if (mapGenerator.IsPositionValid(newPosition))
            {
                Cell adjacentCell = mapGenerator.GetCell(newPosition);
                if (adjacentCell.Terrain == TerrainType.Water)
                {
                    return true;
                }
            }
        }

        return false;
    }


    Vector2Int? GetNextDirectionTowardsEnd(Cell currentCell, Cell previousCell, Vector2Int? lastDirection, Cell endCell)
    {
        List<(Vector2Int direction, int score)> possibleDirections = new List<(Vector2Int, int)>();

        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),  // North
            new Vector2Int(1, 0),  // East
            new Vector2Int(0, -1), // South
            new Vector2Int(-1, 0)  // West
        };

        foreach (var direction in directions)
        {
            Vector2Int newPosition = currentCell.Coordinates + direction;
            if (mapGenerator.IsPositionValid(newPosition))
            {
                Cell nextCell = mapGenerator.GetCell(newPosition);
                if (nextCell.Terrain != TerrainType.Water && nextCell != previousCell)
                {
                    int score = CalculateScoreTowardsEnd(nextCell, direction, lastDirection, endCell);
                    possibleDirections.Add((direction, score));
                }
            }
        }

        if (possibleDirections.Count == 0) return null;

        int maxScore = possibleDirections.Max(d => d.score);
        var topDirections = possibleDirections.Where(d => d.score == maxScore).ToList();

        return topDirections[Random.Range(0, topDirections.Count)].direction;
    }

    int CalculateScoreTowardsEnd(Cell cell, Vector2Int direction, Vector2Int? lastDirection, Cell endCell)
    {
        int score = 0;

        score += Mathf.RoundToInt((7.5f - Mathf.Abs(cell.NoiseValue - 7.5f)) * 10);

        if (lastDirection.HasValue && direction == lastDirection.Value)
        {
            score += 2;
        }

        if (cell.Terrain == TerrainType.Land)
        {
            score += 1;
        }
        else if (cell.Terrain == TerrainType.Forest || cell.Terrain == TerrainType.Mountain)
        {
            score -= 1;
        }

        // Favor directions that bring the river closer to the end cell
        Vector2Int toEnd = endCell.Coordinates - cell.Coordinates;
        if (direction == new Vector2Int(Mathf.Clamp(toEnd.x, -1, 1), Mathf.Clamp(toEnd.y, -1, 1)))
        {
            score += 5; // Increase this value to make the river more aggressively target the end cell
        }

        return score;
    }
}
