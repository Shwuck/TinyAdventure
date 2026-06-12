using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CombatTurnManager : BaseTurnManager
{
    public static CombatTurnManager Instance { get; private set; }

    public delegate void PlayerTurnHandler();
    public event PlayerTurnHandler OnPlayerTurn;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameDebugger.Instance.LogInfo("CombatTurnManager Awake complete. Instance set.");
        }
        else
        {
            Destroy(gameObject);
            GameDebugger.Instance.LogInfo("CombatTurnManager Awake: Duplicate instance destroyed.");
        }
    }

    #region Overrides

    protected override bool ShouldSkipCharacter(Character character)
    {
        if (character == null) return true;

        if (!GameManager.Instance.ActiveTurnManager)
        {
            GameDebugger.Instance.LogInfo("[CombatTurnManager] ActiveTurnManager is false. Skipping all turns.");
            return true;
        }

        if (!character.IsActive)
        {
            GameDebugger.Instance.LogInfo($"[CombatTurnManager] Skipping {character.Name} as they are not active.");
            return true;
        }

        if (character.CurrentNestedArea == null)
        {
            GameDebugger.Instance.LogWarning($"[CombatTurnManager] Skipping {character.Name} as their NestedArea is NULL.");
            return true;
        }

        var player = PlayerStats.Instance.CurrentPlayerCharacter;
        if (player == null || player.CurrentNestedArea == null)
        {
            GameDebugger.Instance.LogWarning("[CombatTurnManager] Player or Player NestedArea is NULL. Skipping.");
            return true;
        }

        if (character.CurrentNestedArea != player.CurrentNestedArea)
        {
            GameDebugger.Instance.LogInfo(
                $"[CombatTurnManager] Skipping {character.Name} as they are not in the active NestedArea.");
            return true;
        }

        return false;
    }

    protected override float GetTurnDelay(Character character)
    {
        var area = PlayerStats.Instance.CurrentPlayerCharacter?.CurrentNestedArea;
        bool hasHostiles = area?.IsHostileArea ?? false;   // mirrors old hostile-delay logic

        return hasHostiles ? 3f : 0f;
    }

    protected override void OnPlayerTurnStart(Character playerCharacter)
    {
        GameDebugger.Instance.LogInfo($"[CombatTurnManager] Player turn started for {playerCharacter.Name}.");
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[PLAYER TURN]", "CombatTurnManager.OnPlayerTurnStart", null, playerCharacter);

        if (!GameManager.Instance.ActiveTurnManager)
        {
            GameDebugger.Instance.LogInfo("[CombatTurnManager] ActiveTurnManager is false. Auto-completing player turn.");
            base.EndTurnForCharacter(playerCharacter);
            return;
        }

        UIController.Instance.UpdateTurnOrderUI();

        PlayerStats.Instance.ResetActionPoints();
        PlayerStats.Instance.ResetMovePoints();
        PlayerController.Instance.UpdateAdaptiveActionMenu();

        OnPlayerTurn?.Invoke();
    }

    protected override void OnNPCTurnExecute(Character npc)
    {
        UIController.Instance.UpdateTurnOrderUI();

        GameDebugger.Instance.LogInfo($"[CombatTurnManager] Executing NPC turn for {npc.Name}.");
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[ENTITY TURN]", "CombatTurnManager.OnNPCTurnExecute", null, npc);

        Vector2Int positionBefore = npc != null ? npc.NestedMapPosition : Vector2Int.zero;
        int apBefore = npc != null ? npc.ActionPoints : -1;
        int mpBeforeReset = npc != null ? npc.MovePoints : -1;
        int maxMovePoints = npc != null ? npc.MaxMovePoints : -1;

        if (npc != null)
        {
            npc.ResetMovePointsForTurn();
        }

        int mpAfterReset = npc != null ? npc.MovePoints : -1;
        int apAfterMovementReset = npc != null ? npc.ActionPoints : -1;
        // CODEXLOG002_MOVEMENT_AI: temporary NPC combat movement-resource diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[ENTITY TURN]", "CombatTurnManager.OnNPCTurnExecute movement point reset",
            $"Movement points before combat NPC turn reset: {mpBeforeReset}\n" +
            $"Movement points after combat NPC turn reset: {mpAfterReset}\n" +
            $"MaxMovePoints used: {maxMovePoints}\n" +
            $"AP before combat NPC turn reset: {apBefore}\n" +
            $"AP after combat NPC turn reset: {apAfterMovementReset}\n" +
            "Reset source/method: CombatTurnManager.OnNPCTurnExecute -> Character.ResetMovePointsForTurn\n" +
            "ExecuteTurnActions will reset AP: True",
            npc);

        // CODEXLOG002_MOVEMENT_AI: temporary combat entity-turn movement diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[ENTITY TURN]", "CombatTurnManager.OnNPCTurnExecute begin",
            $"Calling ExecuteTurnActions: {npc != null}\n" +
            $"Position before: {positionBefore}\n" +
            $"AP before: {apBefore}\n" +
            $"MP before reset: {mpBeforeReset}\n" +
            $"MP after reset: {mpAfterReset}",
            npc);

        npc.ExecuteTurnActions();

        // CODEXLOG002_MOVEMENT_AI: temporary combat entity-turn movement diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[ENTITY TURN]", "CombatTurnManager.OnNPCTurnExecute end",
            $"Position before: {positionBefore}\n" +
            $"Position after: {npc?.NestedMapPosition.ToString() ?? "NULL"}\n" +
            $"Position changed: {npc != null && npc.NestedMapPosition != positionBefore}\n" +
            $"AP before: {apBefore}\n" +
            $"AP after: {npc?.ActionPoints.ToString() ?? "NULL"}\n" +
            $"MP before reset: {mpBeforeReset}\n" +
            $"MP after reset: {mpAfterReset}\n" +
            $"MP after: {npc?.MovePoints.ToString() ?? "NULL"}",
            npc);
    }

    protected override void OnCycleEnded()
    {
        GameDebugger.Instance.LogInfo("[CombatTurnManager] Turn cycle ended.");

        if (!GameManager.Instance.ActiveTurnManager)
        {
            GameDebugger.Instance.LogInfo("[CombatTurnManager] Not restarting cycle. ActiveTurnManager is false.");
            return;
        }

        bool hasPlayer = characterTurnDataDict.Values.Any(d => d.IsPlayer);
        if (characterTurnDataDict.Count > 0 && hasPlayer)
        {
            StartTurnCycle();
        }
        else
        {
            GameDebugger.Instance.LogInfo("[CombatTurnManager] Not restarting cycle: missing characters or player.");
        }
    }

    #endregion

    #region Validation / Utilities

    public override void ValidateCharacterNestedAreas()
    {
        if (PlayerStats.Instance.CurrentNestedArea == null)
        {
            GameDebugger.Instance.LogWarning("[CombatTurnManager] ValidateCharacterNestedAreas: Player NestedArea is null.");
            return;
        }

        INestedArea playerArea = PlayerStats.Instance.CurrentNestedArea;
        List<int> toDeregister = new List<int>();

        foreach (var kv in characterTurnDataDict)
        {
            var entry = kv.Value;
            var c = entry.Character;
            if (c == null)
            {
                toDeregister.Add(kv.Key);
                continue;
            }

            if (entry.NestedArea != playerArea)
            {
                GameDebugger.Instance.LogWarning(
                    $"[CombatTurnManager] {c.Name} is in wrong NestedArea. Expected {playerArea}, Found {entry.NestedArea}");

                if (c.IsInNestedArea)
                {
                    entry.UpdateNestedArea(playerArea);
                    GameDebugger.Instance.LogInfo(
                        $"[CombatTurnManager] Updated {c.Name} to correct NestedArea.");
                }
                else
                {
                    toDeregister.Add(kv.Key);
                }
            }
        }

        foreach (int id in toDeregister.ToList())
        {
            if (characterTurnDataDict.TryGetValue(id, out var data))
            {
                DeregisterCharacter(data.Character);
            }
        }
    }

    public void DebugNestedArea()
    {
        List<string> validCharacters = new List<string>();
        List<string> nullCharacters = new List<string>();

        foreach (var entry in characterTurnDataDict.Values)
        {
            Character character = entry.Character;
            if (character.CurrentNestedArea != null)
            {
                validCharacters.Add(
                    $"[{character.IInteractableID}] {character.Name} - NestedAreaID: {character.CurrentNestedArea.NestedAreaID}");
            }
            else
            {
                nullCharacters.Add($"[{character.IInteractableID}] {character.Name} (NULL CurrentNestedArea)");
            }
        }

        string debugMessage = "=== Debugging Nested Areas for Registered Characters ===\n";

        if (validCharacters.Count > 0)
        {
            debugMessage += "Characters with NestedArea:\n" + string.Join("\n", validCharacters) + "\n";
        }
        if (nullCharacters.Count > 0)
        {
            debugMessage += "Characters with NULL CurrentNestedArea:\n" + string.Join("\n", nullCharacters) + "\n";
        }

        debugMessage += "=== End of Nested Area Debugging ===";

        GameDebugger.Instance.LogInfo(debugMessage);
    }

    public override void RegisterCharacter(Character character, bool isPlayer = false)
    {
        base.RegisterCharacter(character, isPlayer);
        UIController.Instance.UpdateTurnOrderUI();
    }

    public override void DeregisterCharacter(Character character)
    {
        base.DeregisterCharacter(character);
        UIController.Instance.UpdateTurnOrderUI();
    }

    public override void DeregisterAllCharacters()
    {
        base.DeregisterAllCharacters();
        UIController.Instance.UpdateTurnOrderUI();
    }

    #endregion
}
