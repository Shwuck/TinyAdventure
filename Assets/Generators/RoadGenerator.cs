using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine;
using System.Linq;

public class RoadGenerator : MonoBehaviour
{
    public MapGenerator mapGenerator;

    public void StartRoadGeneration()
    {
        if (mapGenerator == null || mapGenerator.map == null)
        {
            Debug.LogError("MapGenerator reference not set or map not generated.");
            return;
        }

        StartCoroutine(GenerateRoadCoroutine());
    }


    private IEnumerator GenerateRoadCoroutine()
    {
        if (mapGenerator == null || mapGenerator.map == null)
        {
            Debug.LogError("MapGenerator reference not set or map not generated.");
            yield break; // Exit the coroutine
        }

        Cell startCell = null;
        Cell endCell = null;

        if (!GameManager.Instance.manualRoadOverride)
        {
            List<List<Cell>> edges = new List<List<Cell>> { mapGenerator.NorthMapEdge, mapGenerator.SouthMapEdge, mapGenerator.EastMapEdge, mapGenerator.WestMapEdge };
            int startEdgeIndex = UnityEngine.Random.Range(0, edges.Count);
            List<Cell> startEdge = edges[startEdgeIndex];
            edges.RemoveAt(startEdgeIndex); // Remove the chosen start edge
            List<Cell> endEdge = edges[UnityEngine.Random.Range(0, edges.Count)]; // Choose an end edge from the remaining edges

            startCell = SelectValidEdgeCell(startEdge);
            endCell = SelectValidEdgeCell(endEdge);
        }
        else
        {
            // Use the coordinates stored in GameManager to retrieve the start and end cells
            startCell = mapGenerator.GetCell(GameManager.Instance.startCellCoordinates);
            endCell = mapGenerator.GetCell(GameManager.Instance.endCellCoordinates);
        }

        if (startCell == null || endCell == null)
        {
            Debug.LogError("Failed to select valid start or end cell.");
            yield break; // Exit the coroutine if cells are not valid
        }

        Debug.Log($"Start Cell Selected: {startCell.Coordinates.x}, {startCell.Coordinates.y}");
        Debug.Log($"End Cell Selected: {endCell.Coordinates.x}, {endCell.Coordinates.y}");

        // Mark the start and end cells as roads
        startCell.Terrain = TerrainType.Road;
        endCell.Terrain = TerrainType.Road;

        Vector2Int lastDirection = Vector2Int.zero;
        int stepsInCurrentDirection = 0;
        bool isFirstStep = true;

        Cell currentCell = startCell;

        while (currentCell != endCell)
        {
            if (IsAdjacentTo(currentCell, endCell))
            {
                // If the current cell is adjacent to the end cell, stop the road here.
                Debug.Log("Road has reached a cell adjacent to the end cell.");
                break;
            }
            if (stepsInCurrentDirection >= 2 || isFirstStep)
            {
                Vector2Int directionDecision = DetermineDirection(currentCell, endCell, ref lastDirection, isFirstStep);
                lastDirection = directionDecision;
                stepsInCurrentDirection = 1;
                isFirstStep = false;
            }
            else
            {
                stepsInCurrentDirection++;
            }

            Vector2Int nextPosition = currentCell.Coordinates + lastDirection;
            if (IsValidPosition(nextPosition))
            {
                currentCell = mapGenerator.map[nextPosition.x, nextPosition.y];
                // Check if the next cell is a River, and if so, place a Bridge; otherwise, place a Road
                if (currentCell.Terrain == TerrainType.River)
                {
                    currentCell.isPassable = true;
                    currentCell.Terrain = TerrainType.Bridge; // Set terrain to Bridge if it's currently a River
                }
                else
                {
                    currentCell.Terrain = TerrainType.Road; // Otherwise, set terrain to Road
                }
            }
            else
            {
                // If the next position is not valid, try a different direction
                AdjustDirection(ref lastDirection, currentCell);
            }

            yield return null; // Pause for processing
        }

        CheckRoadEndConsistency(endCell);

        Debug.Log("Road generation completed.");

        // Set manualRoadOverride to false after road generation is complete
        GameManager.Instance.manualRoadOverride = false;
    }

    private void AdjustDirection(ref Vector2Int lastDirection, Cell currentCell)
    {
        Debug.Log("Adjust Direction Had To Be Called");

        Dictionary<Vector2Int, int> directionOptions = new Dictionary<Vector2Int, int>
    {
        { Vector2Int.up, 0 },
        { Vector2Int.down, 0 },
        { Vector2Int.left, 0 },
        { Vector2Int.right, 0 }
    };

        int minAdjacentRoads = int.MaxValue;
        foreach (var dir in directionOptions.Keys.ToList())
        {
            Vector2Int newPos = currentCell.Coordinates + dir;
            if (IsValidPosition(newPos))
            {
                Cell potentialCell = mapGenerator.GetCell(newPos);
                int adjacentRoads = 0;
                foreach (var adjDir in directionOptions.Keys)
                {
                    Vector2Int adjPos = potentialCell.Coordinates + adjDir;
                    if (IsValidPosition(adjPos) && mapGenerator.map[adjPos.x, adjPos.y].Terrain == TerrainType.Road)
                    {
                        adjacentRoads++;
                    }
                }
                directionOptions[dir] = adjacentRoads;
                minAdjacentRoads = Math.Min(minAdjacentRoads, adjacentRoads);
            }
            else
            {
                directionOptions[dir] = int.MaxValue; // Invalidate direction leading outside the map
            }
        }

        // Filter directions with the fewest adjacent roads
        var bestDirections = directionOptions.Where(d => d.Value == minAdjacentRoads).Select(d => d.Key).ToList();

        // Prefer to keep the last direction if it's among the best options
        Vector2Int newDirection = bestDirections.Contains(lastDirection) ? lastDirection : bestDirections.First();

        // Update lastDirection only if a new direction was chosen
        if (newDirection != Vector2Int.zero)
        {
            lastDirection = newDirection;
        }
        else
        {
            Debug.LogError("No valid direction to adjust to.");
            // Handle the case where no valid direction is found
        }
    }


    private Vector2Int DetermineDirection(Cell current, Cell finish, ref Vector2Int lastDir, bool isFirstStep)
    {
        // Enhanced logic considering adjacency to prefer continuing on paths or avoiding dead ends
        int dx = finish.Coordinates.x - current.Coordinates.x;
        int dy = finish.Coordinates.y - current.Coordinates.y;

        // Example adjustment: prefer a direction that continues the road or leads towards open cells
        if (Mathf.Abs(dx) > Mathf.Abs(dy))
        {
            Vector2Int preferredDirection = new Vector2Int(Math.Sign(dx), 0);
            // Check if preferred direction is viable; if not, consider the perpendicular direction
            if (CanExtendRoadInDirection(current, preferredDirection))
            {
                return preferredDirection;
            }
        }

        // Fallback to the other axis if the preferred direction is blocked
        Vector2Int alternativeDirection = new Vector2Int(0, Math.Sign(dy));
        if (CanExtendRoadInDirection(current, alternativeDirection))
        {
            return alternativeDirection;
        }

        // If both directions are blocked, you may need to adjust your approach or backtrack
        return lastDir; // This is a simple fallback and might need refinement
    }

    private bool CanExtendRoadInDirection(Cell current, Vector2Int direction)
    {
        Vector2Int nextPos = current.Coordinates + direction;
        // Check if the next position is within bounds and not already a road
        return IsValidPosition(nextPos) && mapGenerator.map[nextPos.x, nextPos.y].Terrain != TerrainType.Road;
    }

    private bool IsValidPosition(Vector2Int pos)
    {
        return pos.x > 0 && pos.x < mapGenerator.width - 1 && pos.y > 0 && pos.y < mapGenerator.height - 1;
    }

    private Cell SelectValidEdgeCell(List<Cell> edge)
    {
        // Filter out cells that are either invalid for road start/end or are corner cells
        List<Cell> validCells = edge.FindAll(cell => IsCellValidForRoadStartOrEnd(cell) && !IsCornerCell(cell));
        if (validCells.Count == 0) return null; // Return null if no valid cells are found
        return validCells[UnityEngine.Random.Range(0, validCells.Count)]; // Select a random valid cell
    }

    private bool IsCellValidForRoadStartOrEnd(Cell cell)
    {
        if (IsCornerCell(cell)) return false;
        return !IsRoadOrAdjacentToRoad(cell);
    }

    private bool IsCornerCell(Cell cell)
    {
        bool isCorner = (cell.Coordinates.x == 0 || cell.Coordinates.x == mapGenerator.width - 1) &&
                        (cell.Coordinates.y == 0 || cell.Coordinates.y == mapGenerator.height - 1);
        return isCorner;
    }

    private bool IsRoadOrAdjacentToRoad(Cell cell)
    {
        if (cell.Terrain == TerrainType.Road) return true;
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in directions)
        {
            Vector2Int neighborPos = cell.Coordinates + dir;
            if (neighborPos.x >= 0 && neighborPos.x < mapGenerator.width && neighborPos.y >= 0 && neighborPos.y < mapGenerator.height)
            {
                if (mapGenerator.map[neighborPos.x, neighborPos.y].Terrain == TerrainType.Road) return true;
            }
        }
        return false;
    }

    private Cell GetCellFromEdgeSelection(int edgeSelection)
    {
        List<Cell> selectedEdge = null;
        switch (edgeSelection)
        {
            case 0: // North
                selectedEdge = mapGenerator.NorthMapEdge;
                break;
            case 1: // East
                selectedEdge = mapGenerator.EastMapEdge;
                break;
            case 2: // South
                selectedEdge = mapGenerator.SouthMapEdge;
                break;
            case 3: // West
                selectedEdge = mapGenerator.WestMapEdge;
                break;
        }

        // Here, we simply return the first cell of the selected edge as an example.
        // You might want to adjust this logic to fit your game's requirements.
        return selectedEdge?.FirstOrDefault();
    }

    private void CheckRoadEndConsistency(Cell endCell)
    {
        if (endCell.AdjacentCellCount < 4) // If it's an edge or corner cell
        {
            foreach (var direction in endCell.AdjacentCells.Keys)
            {
                Vector2Int? adjacentPosition = endCell.AdjacentCells[direction];
                if (adjacentPosition.HasValue && IsValidPosition(adjacentPosition.Value))
                {
                    Cell adjacentCell = mapGenerator.GetCell(adjacentPosition.Value);
                    if (adjacentCell.Terrain != TerrainType.Road)
                    {
                        // Extend the road to this cell if it's more central or has more connections
                        adjacentCell.Terrain = TerrainType.Road;
                        break; // Stop after extending to one cell
                    }
                }
            }
        }
    }

    // Helper method to check if two cells are adjacent
    private bool IsAdjacentTo(Cell current, Cell target)
    {
        Vector2Int direction = target.Coordinates - current.Coordinates;
        // Check if the target cell is exactly one step away in any cardinal direction
        return Math.Abs(direction.x) <= 1 && Math.Abs(direction.y) <= 1 && direction != Vector2Int.zero;
    }

    private Cell GetCellAwayFromEdge(Cell cell)
    {
        // Determine direction away from the closest edge
        int dx = cell.Coordinates.x < mapGenerator.width / 2 ? 1 : -1;
        int dy = cell.Coordinates.y < mapGenerator.height / 2 ? 1 : -1;

        Vector2Int direction = Math.Abs(dx) > Math.Abs(dy) ? new Vector2Int(dx, 0) : new Vector2Int(0, dy);
        Vector2Int newEndPos = cell.Coordinates + direction;

        if (IsValidPosition(newEndPos))
        {
            return mapGenerator.GetCell(newEndPos);
        }

        return null; // Return null if no valid position found
    }


}
