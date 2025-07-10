using System.Collections.Generic;
using UnityEngine;

public class TerrainPainterTool : MonoBehaviour
{
    public static TerrainPainterTool Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Paints the terrain based on the provided arguments.
    /// </summary>
    /// <param name="initialTerrain">The terrain type to look for.</param>
    /// <param name="terrainToChangeTo">The terrain type to apply.</param>
    /// <param name="cellsToChange">Number of cells to change. 1 modifies the initial cell, larger numbers affect surrounding cells.</param>
    /// <param name="secondaryTerrainType">An optional terrain type to apply to surrounding cells.</param>
    /// <param name="overrideProminentLandmarks">Whether to override prominent landmarks within the affected area.</param>
    public void PaintTerrain(TerrainType initialTerrain, TerrainType terrainToChangeTo, int cellsToChange, TerrainType? secondaryTerrainType = null, bool overrideProminentLandmarks = false)
    {
        // Find all cells with the initial terrain type
        List<Cell> cellsToPaint = new List<Cell>();
        foreach (var cell in MapGenerator.Instance.allCells)
        {
            if (cell.Terrain == initialTerrain)
            {
                cellsToPaint.Add(cell);
            }
        }

        // If no cells match the initial terrain type, exit early
        if (cellsToPaint.Count == 0)
        {
            Debug.LogWarning("No cells found with the specified initial terrain type.");
            return;
        }

        // Iterate through the cells to paint
        for (int i = 0; i < Mathf.Min(cellsToChange, cellsToPaint.Count); i++)
        {
            Cell cell = cellsToPaint[i];

            // Check if the cell has any significant features (landmarks, dungeons, villages, player home)
            if (!overrideProminentLandmarks && (cell.HasLandmark || cell.HasDungeon || cell.HasVillage || cell.IsPlayerHome))
            {
                continue;
            }

            // Apply the primary terrain change
            cell.Terrain = terrainToChangeTo;

            // Apply the secondary terrain change to surrounding cells if specified
            if (secondaryTerrainType.HasValue)
            {
                ApplySecondaryTerrain(cell, secondaryTerrainType.Value, overrideProminentLandmarks);
            }
        }

        // Optionally log the terrain change
        Debug.Log($"Painted {Mathf.Min(cellsToChange, cellsToPaint.Count)} cells from {initialTerrain} to {terrainToChangeTo}.");
    }

    /// <summary>
    /// Applies the secondary terrain type to surrounding cells.
    /// </summary>
    /// <param name="centerCell">The central cell that was changed.</param>
    /// <param name="secondaryTerrain">The secondary terrain type to apply to surrounding cells.</param>
    /// <param name="overrideProminentLandmarks">Whether to override prominent landmarks within the affected area.</param>
    private void ApplySecondaryTerrain(Cell centerCell, TerrainType secondaryTerrain, bool overrideProminentLandmarks)
    {
        foreach (var adjacentCoord in centerCell.AdjacentCells.Values)
        {
            if (adjacentCoord.HasValue)
            {
                Cell adjacentCell = MapGenerator.Instance.GetCell(adjacentCoord.Value);
                if (adjacentCell != null)
                {
                    // Check if the cell has any significant features
                    if (!overrideProminentLandmarks && (adjacentCell.HasLandmark || adjacentCell.HasDungeon || adjacentCell.HasVillage || adjacentCell.IsPlayerHome))
                    {
                        continue;
                    }

                    // Change the terrain type of the adjacent cell to the secondary terrain type
                    adjacentCell.Terrain = secondaryTerrain;
                }
            }
        }
    }
}
