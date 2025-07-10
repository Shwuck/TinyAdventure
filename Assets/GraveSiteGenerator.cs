using System.Collections.Generic;
using UnityEngine;

public class GraveSiteGenerator : MonoBehaviour
{
    public Cell[,] map; // 2D array of Cell objects
    public int width = 7;  // Width of the map
    public int height = 7; // Height of the map
    public List<Cell> allCells = new List<Cell>(); // List of all cells

    void Start()
    {
        GenerateGraveSite();
    }

    public void GenerateGraveSite()
    {
        map = new Cell[width, height];
        InitializeMap();
    }

    void InitializeMap()
    {
        Debug.Log("Initializing the Grave Site!");

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int cellID = x * width + y;
                var cell = new Cell(cellID, x, y, TerrainType.Land);

                map[x, y] = cell;

                cell.TerrainToDisplay = TerrainType.Land;

                // Add the cell to the allCells list
                allCells.Add(cell);

                // Simple initialization; all cells are land
                cell.Terrain = TerrainType.Land;
            }
        }

        Debug.Log("Grave Site map generation complete.");
    }

    // Utility method to get a cell by its coordinates
    public Cell GetCell(Vector2Int coordinates)
    {
        if (coordinates.x >= 0 && coordinates.x < width && coordinates.y >= 0 && coordinates.y < height)
        {
            return map[coordinates.x, coordinates.y];
        }
        else
        {
            Debug.LogError($"Coordinates out of range: {coordinates}");
            return null;
        }
    }
}
