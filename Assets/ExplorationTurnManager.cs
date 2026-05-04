using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ExplorationTurnManager : BaseTurnManager
{
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
        npc.ExecuteTurnActions();
    }

    protected override void OnCycleEnded()
    {
        GameDebugger.Instance.LogInfo("[ExplorationTurnManager] Exploration cycle ended. Restarting.");

        // Continuous cycles: after everyone (including player) has had a turn,
        // we sort and start a new cycle. The cycle will pause naturally when
        // we hit the player again and wait for PlayerTurnCompleted().
        StartTurnCycle();
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
