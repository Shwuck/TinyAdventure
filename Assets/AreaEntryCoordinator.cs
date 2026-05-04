using System.Linq;
using UnityEngine;

public class AreaEntryCoordinator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private NPCManager npcManager;
    [SerializeField] private AnimalManager animalManager;

    #region Entry Orchestration

    // Main map → first-level nested area
    public void HandleOnEnterFromMainMap(Cell parentCell, INestedArea nestedArea, int mainMapCellID, bool wasPlayerStart)
    {
        CallTrace.Mark(this);
        GameDebugger.Instance.LogInfo($"AreaEntryCoordinator: MainMap → Nested | {nestedArea?.Name}");

        ApplyEnvironment(parentCell, nestedArea, applyFertility: true);
        PlaceNPCs(parentCell, nestedArea);
        PlaceAnimals(parentCell, nestedArea, mainMapCellID, wasPlayerStart);

        PostPopulationAudit(nestedArea);
    }

    // Nested area → deeper nested area
    public void HandleOnEnterFromNestedArea(Cell parentCell, INestedArea nestedArea)
    {
        CallTrace.Mark(this);
        GameDebugger.Instance.LogInfo($"AreaEntryCoordinator: Nested → Nested | {nestedArea?.Name}");

        // Keep parity with your old behaviour: no extra fertility shift for deeper layers.
        ApplyEnvironment(parentCell, nestedArea, applyFertility: false);
        PlaceNPCs(parentCell, nestedArea);

        // If you later decide deeper layers should spawn animals too,
        // you can call PlaceAnimals here with suitable parameters.

        PostPopulationAudit(nestedArea);
    }

    #endregion

    #region Environment

    private void ApplyEnvironment(Cell parentCell, INestedArea nestedArea, bool applyFertility)
    {
        if (!applyFertility) return;
        if (parentCell == null || nestedArea == null) return;

        FertilityService.ApplyOverallFertilityAdjustment(parentCell, nestedArea);
        MessageLogManager.Instance?.Log("exploration", "Env Updated",
            $"Fertility balanced for {nestedArea.Name}");
    }

    #endregion

    #region NPCs

    private void PlaceNPCs(Cell parentCell, INestedArea nestedArea)
    {
        if (parentCell == null || nestedArea == null) return;

        if (parentCell.Terrain == TerrainType.Village)
            npcManager.PlaceVillageNPCs(parentCell, nestedArea);

        if (parentCell.isNPCGroupPresent)
            npcManager.PlaceNPCGroupInNestedArea(parentCell, nestedArea);
    }

    #endregion

    #region Animals

    private void PlaceAnimals(Cell parentCell, INestedArea nestedArea, int mainMapCellID, bool wasPlayerStart)
    {
        if (nestedArea == null) return;

        // Generate once per parent-cell context
        if (!nestedArea.GetAllAnimalsInArea().Any())
            nestedArea.GenerateAnimalsForCellID(mainMapCellID);

        // Skip placement if it’s the literal player start
        if (!wasPlayerStart)
            animalManager.PlaceAnimalsForNestedArea(nestedArea);

        MessageLogManager.Instance?.Log("exploration", "Fauna", $"Animals placed in {nestedArea.Name}");
    }

    #endregion

    #region Audit

    private void PostPopulationAudit(INestedArea nestedArea)
    {
        var orchestrator = TurnOrchestrator.Instance;
        orchestrator.LogAllRegisteredCharacters();
        GameDebugger.Instance.LogInfo(
            $"AreaEntry Summary: {nestedArea?.Name} | Registered={orchestrator.GetRegisteredCharacters().Count}");
    }

    #endregion
}

public static class FertilityService
{
    public static void ApplyOverallFertilityAdjustment(Cell parentCell, INestedArea nestedArea)
    {
        if (parentCell == null)
        {
            GameDebugger.Instance.LogError("FertilityService: parentCell is NULL. Aborting.");
            return;
        }

        if (nestedArea == null)
        {
            GameDebugger.Instance.LogError("FertilityService: nestedArea is NULL. Aborting.");
            return;
        }

        int overallAdjustment = parentCell.OverallFertilityAdjustment;
        Cell[,] nestedMap = nestedArea.GetNestedMap();

        if (nestedMap == null)
        {
            GameDebugger.Instance.LogError("FertilityService: nestedMap is NULL. Aborting.");
            return;
        }

        GameDebugger.Instance.LogInfo(
            $"Applying overall fertility adjustment of {overallAdjustment} to NestedArea ID {nestedArea.NestedAreaID}");

        int width = nestedMap.GetLength(0);
        int height = nestedMap.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Cell nestedCell = nestedMap[x, y];
                if (nestedCell == null)
                {
                    GameDebugger.Instance.LogWarning(
                        $"FertilityService: Null cell at [{x},{y}] skipped.");
                    continue;
                }

                int oldFertility = nestedCell.FertilityValue;
                int newFertility = Mathf.Clamp(oldFertility + overallAdjustment, 0, 100);

                nestedCell.FertilityValue = newFertility;
                nestedCell.OverallFertilityAdjustment += overallAdjustment;

                if (newFertility == 0)
                {
                    nestedCell.isFertile = false;

                    if (nestedCell.Terrain == TerrainType.Land)
                    {
                        nestedCell.Terrain = TerrainType.Dirt;
                        GameDebugger.Instance.LogInfo(
                            $"Cell {nestedCell.CellID} turned to Dirt due to fertility drop.");
                    }
                }

                GameDebugger.Instance.LogInfo(
                    $"Adjusted fertility for Cell {nestedCell.CellID}: {oldFertility} → {newFertility} (Δ {overallAdjustment})");
            }
        }
    }
}
