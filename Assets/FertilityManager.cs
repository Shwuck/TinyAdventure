using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FertilityManager : MonoBehaviour
{
    public static FertilityManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameDebugger.Instance.LogInfo("FertilityManager: Instance initialized.");
        }
        else
        {
            Destroy(gameObject);
            GameDebugger.Instance.LogWarning("FertilityManager: Duplicate instance found. Destroying this instance.");
        }
    }

    public void AdjustFertilityWeekly()
    {
        GameDebugger.Instance.LogInfo("FertilityManager: Adjusting fertility for the week.");
        StartCoroutine(AdjustFertilityWeeklyCoroutine());
    }

    private IEnumerator AdjustFertilityWeeklyCoroutine()
    {
        Random.InitState(GameManager.Instance.GameSeed + TimeManager.Instance.TotalDaysPassed);
        var cells = PermaLists.Instance.AllMapCells;
        int cellsPerFrame = 10; // Number of cells to process per frame (adjust as needed)

        for (int i = 0; i < cells.Count; i += cellsPerFrame)
        {
            for (int j = 0; j < cellsPerFrame && (i + j) < cells.Count; j++)
            {
                var cell = cells[i + j];
                if (cell.isMainMapCell && cell.isFertile)
                {
                    AdjustCellFertility(cell);
                }
            }
            yield return null; // Wait for the next frame before continuing the loop
        }

        GameDebugger.Instance.LogInfo("FertilityManager: Finished adjusting fertility for the week.");
    }

    private void AdjustCellFertility(Cell cell)
    {
        // Subtract PassedThroughCount from FertilityValue before making any other adjustments
        cell.FertilityValue -= cell.PassedThroughCount;
        GameDebugger.Instance.LogInfo($"FertilityManager: Cell {cell.CellID} fertility reduced by PassedThroughCount of {cell.PassedThroughCount}.");

        // Reset PassedThroughCount to 0 after adjusting fertility
        cell.PassedThroughCount = 0;

        bool isWinter = TimeManager.Instance.currentSeason == Season.Winter;
        int fertilityChange = Random.Range(0, 6);

        if (isWinter)
        {
            if (cell.isCurated)
            {
                if (Random.Range(0, 2) == 0)
                {
                    cell.FertilityValue -= fertilityChange;
                    cell.OverallFertilityAdjustment -= fertilityChange;
                    GameDebugger.Instance.LogInfo($"FertilityManager: Cell {cell.CellID} fertility reduced by {fertilityChange}.");
                }
            }
            else
            {
                cell.FertilityValue -= fertilityChange;
                cell.OverallFertilityAdjustment -= fertilityChange;
                GameDebugger.Instance.LogInfo($"FertilityManager: Cell {cell.CellID} fertility reduced by {fertilityChange}.");
            }
        }
        else // Spring, Summer, Autumn
        {
            if (cell.FertilityValue < 100)
            {
                if (cell.isCurated)
                {
                    if (Random.Range(0, 2) == 0)
                    {
                        cell.FertilityValue += fertilityChange;
                        cell.OverallFertilityAdjustment += fertilityChange;
                        GameDebugger.Instance.LogInfo($"FertilityManager: Cell {cell.CellID} fertility increased by {fertilityChange}.");
                    }
                    else
                    {
                        cell.FertilityValue += 5;
                        cell.OverallFertilityAdjustment += 5;
                        GameDebugger.Instance.LogInfo($"FertilityManager: Cell {cell.CellID} fertility increased by 5.");
                    }
                }
                else
                {
                    cell.FertilityValue += fertilityChange;
                    cell.OverallFertilityAdjustment += fertilityChange;
                    GameDebugger.Instance.LogInfo($"FertilityManager: Cell {cell.CellID} fertility increased by {fertilityChange}.");
                }
            }
        }

        // Cap fertility between 0 and 100
        if (cell.FertilityValue > 100)
        {
            cell.FertilityValue = 100;
        }
        else if (cell.FertilityValue < 0)
        {
            cell.FertilityValue = 0;
            cell.isFertile = false;
            if (cell.Terrain == TerrainType.Land)
            {
                cell.Terrain = TerrainType.Dirt;
                GameDebugger.Instance.LogInfo($"FertilityManager: Cell {cell.CellID} changed to Dirt.");
            }
        }
    }

    public void AdjustForestGrowth()
    {
        GameDebugger.Instance.LogInfo("FertilityManager: Adjusting forest growth for the year.");
        StartCoroutine(AdjustForestGrowthCoroutine());
    }

    private IEnumerator AdjustForestGrowthCoroutine()
    {
        var cells = PermaLists.Instance.AllMapCells;
        int cellsPerFrame = 10; // Number of cells to process per frame (adjust as needed)

        for (int i = 0; i < cells.Count; i += cellsPerFrame)
        {
            for (int j = 0; j < cellsPerFrame && (i + j) < cells.Count; j++)
            {
                var cell = cells[i + j];
                if (cell.isMainMapCell)
                {
                    AdjustCellForestGrowth(cell);
                }
            }
            yield return null; // Wait for the next frame before continuing the loop
        }

        GameDebugger.Instance.LogInfo("FertilityManager: Finished adjusting forest growth for the year.");
    }

    private void AdjustCellForestGrowth(Cell cell)
    {
        if (cell.Terrain == TerrainType.Land && cell.FertilityValue == 100)
        {
            foreach (var adjacentCellCoord in cell.AdjacentCells.Values)
            {
                if (adjacentCellCoord.HasValue)
                {
                    var adjacentCell = MapGenerator.Instance.GetCell(adjacentCellCoord.Value);
                    if (adjacentCell != null && adjacentCell.Terrain == TerrainType.Forest)
                    {
                        cell.Terrain = TerrainType.Forest;
                        GameDebugger.Instance.LogInfo($"FertilityManager: Cell {cell.CellID} changed to Forest.");
                        break;
                    }
                }
            }
        }
        else if (cell.Terrain == TerrainType.Forest && cell.FertilityValue < 25)
        {
            cell.Terrain = TerrainType.Land;
            GameDebugger.Instance.LogInfo($"FertilityManager: Cell {cell.CellID} changed to Land.");
        }
    }
}
