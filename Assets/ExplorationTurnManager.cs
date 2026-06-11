using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine;

public class ExplorationTurnManager : BaseTurnManager
{
    private bool restartScheduled;

    #region Lifecycle

    public void Suspend() => enabled = false;

    public void Resume() => enabled = true;

    #endregion

    #region Overrides

    protected override bool ShouldSkipCharacter(Character character)
    {
        if (character == null) return true;

        var area = PlayerStats.Instance.CurrentNestedArea;
        if (area == null)
        {
            GameDebugger.Instance.LogWarning("[ExplorationTurnManager] Player area is null. Skipping all turns.");
            return true;
        }

        if (!character.IsActive)
        {
            GameDebugger.Instance.LogInfo($"[ExplorationTurnManager] Skipping {character.Name} (inactive).");
            return true;
        }

        if (!character.IsInNestedArea || character.CurrentNestedArea != area)
        {
            GameDebugger.Instance.LogInfo(
                $"[ExplorationTurnManager] Skipping {character.Name} (not in active NestedArea).");
            return true;
        }

        // Important: DO NOT skip the player here.
        // Exploration remains turn-based; the player gets a proper turn.
        return false;
    }

    protected override float GetTurnDelay(Character character)
    {
        // Exploration turns should feel immediate.
        return 0f;
    }

    protected override void OnPlayerTurnStart(Character playerCharacter)
    {
        GameDebugger.Instance.LogInfo(
            $"[ExplorationTurnManager] Player exploration turn started for {playerCharacter.Name}.");
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[PLAYER TURN]", "ExplorationTurnManager.OnPlayerTurnStart", null, playerCharacter);

        // If you want exploration movement to respect some kind of points, you can reset them here.
        // If AP is combat-only, you might just refresh movement/UI.

        // Example: light-touch reset
        PlayerStats.Instance.ResetMovePoints();
        PlayerController.Instance.UpdateAdaptiveActionMenu();
    }

    protected override void OnNPCTurnExecute(Character npc)
    {
        GameDebugger.Instance.LogInfo(
            $"[ExplorationTurnManager] Executing exploration turn for NPC {npc.Name}.");
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[ENTITY TURN]", "ExplorationTurnManager.OnNPCTurnExecute", null, npc);
        npc.ExecuteTurnActions();
    }

    protected override void OnCycleEnded()
    {
        GameDebugger.Instance.LogInfo("[ExplorationTurnManager] Exploration cycle ended. Scheduling restart.");
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[TURN CYCLE]", "ExplorationTurnManager.OnCycleEnded",
            $"RegisteredCount: {characterTurnDataDict.Count}\nRestartScheduled: {restartScheduled}");

        if (!HasValidRegisteredPlayer())
        {
            GameDebugger.Instance.LogWarning("[ExplorationTurnManager] Cycle stopped: no valid registered player.");
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogWarning("Exploration cycle stopped because no valid player was registered",
                $"RegisteredCount: {characterTurnDataDict.Count}");
            return;
        }

        if (restartScheduled)
        {
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogWarning("Exploration cycle restart ignored because one is already scheduled",
                $"RegisteredCount: {characterTurnDataDict.Count}");
            return;
        }

        restartScheduled = true;
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[TURN CYCLE]", "ExplorationTurnManager restart scheduled",
            $"RegisteredCount: {characterTurnDataDict.Count}");
        StartCoroutine(RestartCycleNextFrame());
    }

    private IEnumerator RestartCycleNextFrame()
    {
        yield return null;

        restartScheduled = false;

        if (!enabled)
        {
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogWarning("Exploration cycle restart cancelled because manager is disabled",
                $"RegisteredCount: {characterTurnDataDict.Count}");
            yield break;
        }

        if (!HasValidRegisteredPlayer())
        {
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogWarning("Exploration cycle restart cancelled because no valid player was registered",
                $"RegisteredCount: {characterTurnDataDict.Count}");
            yield break;
        }

        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[TURN CYCLE]", "ExplorationTurnManager restart executed",
            $"RegisteredCount: {characterTurnDataDict.Count}");
        StartTurnCycle();
    }

    private bool HasValidRegisteredPlayer()
    {
        return characterTurnDataDict.Values.Any(data =>
            data != null &&
            data.IsPlayer &&
            data.Character != null &&
            !ShouldSkipCharacter(data.Character));
    }

    #endregion

    #region Orchestrator Parity / Validation

    public override void ValidateCharacterNestedAreas()
    {
        var area = PlayerStats.Instance.CurrentNestedArea;
        if (area == null)
        {
            GameDebugger.Instance.LogWarning("[ExplorationTurnManager] ValidateCharacterNestedAreas: Player area is null.");
            return;
        }

        List<int> toDeregister = new List<int>();

        foreach (var kv in characterTurnDataDict)
        {
            var data = kv.Value;
            var c = data.Character;
            if (c == null)
            {
                toDeregister.Add(kv.Key);
                continue;
            }

            if (c.CurrentNestedArea != area)
            {
                if (c.IsInNestedArea)
                {
                    data.UpdateNestedArea(area);
                    GameDebugger.Instance.LogInfo(
                        $"[ExplorationTurnManager] Updated {c.Name} to correct NestedArea.");
                }
                else
                {
                    toDeregister.Add(kv.Key);
                }
            }
        }

        foreach (var id in toDeregister.ToList())
        {
            if (characterTurnDataDict.TryGetValue(id, out var data))
            {
                DeregisterCharacter(data.Character);
            }
        }
    }

    #endregion

    #region Optional Legacy Helper

    // If you still want an explicit "tick" call from somewhere, you can wire it to start the cycle.
    // In practice, for turn-based exploration, you'll usually:
    // - StartTurnCycle() when entering exploration
    // - Call TurnOrchestrator.PlayerTurnCompleted() after the player moves,
    //   which will propagate to this manager and advance NPCs.
    public void Tick()
    {
        GameDebugger.Instance.LogInfo("[ExplorationTurnManager] Tick called. Starting exploration cycle.");
        StartTurnCycle();
    }

    #endregion
}
