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

    public override void StartTurnCycle()
    {
        PruneInvalidCombatants("CombatTurnManager.StartTurnCycle");

        if (!HasRestartableCombatParticipants())
        {
            CombatActionResolutionDiagnosticsLogger.LogWarning("CombatTurnManager.StartTurnCycle aborted because no valid combat participants remain",
                $"RegisteredCount={DiagnosticRegisteredCount}\nCurrentContext={TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "NULL"}");
            TurnOrchestrator.Instance?.TryUpdateTurnContext();
            return;
        }

        base.StartTurnCycle();
    }

    protected override bool ShouldSkipCharacter(Character character)
    {
        if (character == null) return true;

        if (!GameManager.Instance.ActiveTurnManager)
        {
            GameDebugger.Instance.LogInfo("[CombatTurnManager] ActiveTurnManager is false. Skipping all turns.");
            return true;
        }

        if (!character.IsAlive)
        {
            GameDebugger.Instance.LogInfo($"[CombatTurnManager] Skipping {character.Name} as they are dead.");
            return true;
        }

        if (!character.IsActive)
        {
            GameDebugger.Instance.LogInfo($"[CombatTurnManager] Skipping {character.Name} as they are not active.");
            return true;
        }

        if (!character.InCombat)
        {
            GameDebugger.Instance.LogInfo($"[CombatTurnManager] Skipping {character.Name} as they are no longer in combat.");
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
        // CODEXLOG001_TURNLIFECYCLE: temporary combat pacing diagnostic.
        TurnDiagnosticsLogger.LogEvent("[COMBAT PACING]", "CombatTurnManager.GetTurnDelay",
            $"TurnDelay: 0\n" +
            "Reason: InstantCombatDecisions\n" +
            $"Actor: {character?.Name ?? "NULL"} [{character?.IInteractableID.ToString() ?? "NULL"}]\n" +
            $"Role: {BaseTurnManager.GetCombatParticipantRole(character)}",
            character);
        return 0f;
    }

    protected override void OnPlayerTurnStart(Character playerCharacter)
    {
        GameDebugger.Instance.LogInfo($"[CombatTurnManager] Player turn started for {playerCharacter.Name}.");
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[PLAYER TURN]", "CombatTurnManager.OnPlayerTurnStart", null, playerCharacter);

        if (!GameManager.Instance.ActiveTurnManager)
        {
            GameDebugger.Instance.LogInfo("[CombatTurnManager] ActiveTurnManager is false. Auto-completing player turn.");
            isPlayerTurn = false;
            base.EndTurnForCharacter(playerCharacter);
            return;
        }

        UIController.Instance.UpdateTurnOrderUI();

        int playerStatsAPBeforeReset = PlayerStats.Instance.ActionPoints;
        int playerStatsMPBeforeReset = PlayerStats.Instance.MovePoints;
        int playerStatsCombatExertionBeforeReset = PlayerStats.Instance.CombatExertion;
        int characterAPBeforeReset = playerCharacter != null ? playerCharacter.ActionPoints : -1;
        int characterMPBeforeReset = playerCharacter != null ? playerCharacter.MovePoints : -1;
        int characterCombatExertionBeforeReset = playerCharacter != null ? playerCharacter.CurrentCombatExertion : -1;
        PlayerStats.Instance.ResetActionPoints();
        PlayerStats.Instance.ResetMovePoints();
        if (playerCharacter != null)
        {
            playerCharacter.ActionPoints = PlayerStats.Instance.ActionPoints;
            playerCharacter.MovePoints = PlayerStats.Instance.MovePoints;
            playerCharacter.ResetCombatExertionForTurn("CombatTurnManager.OnPlayerTurnStart");
            playerCharacter.ResetConsumptionCapacityForTurn("CombatTurnManager.OnPlayerTurnStart");
            PlayerStats.Instance.SyncStaminaFromCurrentPlayerCharacter();
        }
        UIController.Instance.UpdateTurnOrderUI();
        // CODEXLOG001_TURNLIFECYCLE: temporary player combat resource reset diagnostic.
        TurnDiagnosticsLogger.LogEvent("[PLAYER TURN START]", "CombatTurnManager.OnPlayerTurnStart AP/MP reset",
            $"APReset: {playerStatsAPBeforeReset} -> {PlayerStats.Instance.ActionPoints}\n" +
            $"MPReset: {playerStatsMPBeforeReset} -> {PlayerStats.Instance.MovePoints}\n" +
            $"CombatExertionReset: {FixedPointResourceMath.Format(playerStatsCombatExertionBeforeReset)} -> {FixedPointResourceMath.Format(PlayerStats.Instance.CombatExertion)}\n" +
            $"PlayerStatsAP: {PlayerStats.Instance.ActionPoints}\n" +
            $"CharacterAPBefore: {characterAPBeforeReset}\n" +
            $"CharacterAP: {playerCharacter?.ActionPoints.ToString() ?? "NULL"}\n" +
            $"PlayerStatsMP: {PlayerStats.Instance.MovePoints}\n" +
            $"CharacterMPBefore: {characterMPBeforeReset}\n" +
            $"CharacterMP: {playerCharacter?.MovePoints.ToString() ?? "NULL"}\n" +
            $"CharacterCombatExertionBefore: {FixedPointResourceMath.Format(characterCombatExertionBeforeReset)}\n" +
            $"CharacterCombatExertionAfter: {FixedPointResourceMath.Format(playerCharacter?.CurrentCombatExertion ?? 0)}\n" +
            $"CharacterConsumptionCapacityAfter: {playerCharacter?.CurrentConsumptionCapacity.ToString() ?? "NULL"}\n" +
            "InputAccepted: True\nPlayerTurn: True",
            playerCharacter);
        // CODEXLOG003_ACTIONS_AAM: temporary combat AP ownership diagnostic.
        ActionAAMDiagnosticsLogger.LogEvent("[AP RESET]", "CombatTurnManager.OnPlayerTurnStart synced player character AP",
            $"PlayerStats.ActionPoints after reset: {PlayerStats.Instance.ActionPoints}\n" +
            $"PlayerCharacter.ActionPoints before sync: {characterAPBeforeReset}\n" +
            $"PlayerCharacter.ActionPoints after sync: {playerCharacter?.ActionPoints.ToString() ?? "NULL"}\n" +
            $"PlayerStats.MovePoints after reset: {PlayerStats.Instance.MovePoints}");
        CombatActionResolutionDiagnosticsLogger.LogEvent("[AP RESET]", "CombatTurnManager.OnPlayerTurnStart synced player combat resources",
            $"PlayerStatsAPBeforeReset={playerStatsAPBeforeReset}\n" +
            $"PlayerStatsAPAfterReset={PlayerStats.Instance.ActionPoints}\n" +
            $"CharacterAPBeforeReset={characterAPBeforeReset}\n" +
            $"CharacterAPAfterReset={playerCharacter?.ActionPoints.ToString() ?? "NULL"}\n" +
            $"PlayerStatsMPBeforeReset={playerStatsMPBeforeReset}\n" +
            $"PlayerStatsMPAfterReset={PlayerStats.Instance.MovePoints}\n" +
            $"CharacterMPBeforeReset={characterMPBeforeReset}\n" +
            $"CharacterMPAfterReset={playerCharacter?.MovePoints.ToString() ?? "NULL"}\n" +
            $"PlayerStatsCombatExertionBeforeReset={FixedPointResourceMath.Format(playerStatsCombatExertionBeforeReset)}\n" +
            $"PlayerStatsCombatExertionAfterReset={FixedPointResourceMath.Format(PlayerStats.Instance.CombatExertion)}\n" +
            $"CharacterCombatExertionBeforeReset={FixedPointResourceMath.Format(characterCombatExertionBeforeReset)}\n" +
            $"CharacterCombatExertionAfterReset={FixedPointResourceMath.Format(playerCharacter?.CurrentCombatExertion ?? 0)}\n" +
            $"CharacterConsumptionCapacityAfterReset={playerCharacter?.CurrentConsumptionCapacity.ToString() ?? "NULL"}\n" +
            $"ResetSource=CombatTurnManager.OnPlayerTurnStart",
            playerCharacter);
        LogTurnOrderDiagnostic("[COMBAT TURN ORDER]", "CombatTurnManager.OnPlayerTurnStart resources reset",
            $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}\n" +
            $"PlayerStats.MovePoints: {PlayerStats.Instance.MovePoints}\n" +
            $"PlayerCharacter.ActionPoints: {playerCharacter?.ActionPoints.ToString() ?? "NULL"}\n" +
            $"PlayerCharacter.MovePoints: {playerCharacter?.MovePoints.ToString() ?? "NULL"}\n" +
            $"PlayerCharacter.CombatExertion: {FixedPointResourceMath.Format(playerCharacter?.CurrentCombatExertion ?? 0)}\n" +
            $"PlayerCharacter.ConsumptionCapacity: {playerCharacter?.CurrentConsumptionCapacity.ToString() ?? "NULL"}\n" +
            "InputAccepted: True\nPlayerTurn: True");
        MessageLogManager.Instance?.Log("combat_player_turn",
            playerCharacter != null ? playerCharacter.Name : "Player",
            PlayerStats.Instance.ActionPoints,
            PlayerStats.Instance.MovePoints);
        PlayerController.Instance.UpdateAdaptiveActionMenu();

        OnPlayerTurn?.Invoke();
    }

    protected override void OnNPCTurnExecute(Character npc)
    {
        if (npc == null)
        {
            CombatActionResolutionDiagnosticsLogger.LogWarning("CombatTurnManager.OnNPCTurnExecute skipped null combatant",
                $"CurrentContext={TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "NULL"}");
            return;
        }

        if (!npc.IsCombatActorAvailable() || !npc.InCombat)
        {
            CombatActionResolutionDiagnosticsLogger.LogWarning("CombatTurnManager.OnNPCTurnExecute skipped invalid combatant",
                $"Actor={npc.Name} [{npc.IInteractableID}]\n" +
                $"IsAlive={npc.IsAlive}\n" +
                $"IsActive={npc.IsActive}\n" +
                $"InCombat={npc.InCombat}",
                npc);
            DeregisterCharacter(npc);
            TurnOrchestrator.Instance?.TryUpdateTurnContext();
            return;
        }

        if ((npc.IsHostile || npc.Stance == NPCStance.Hostile) &&
            !npc.TryRefreshCombatTarget("CombatTurnManager.OnNPCTurnExecute hostile actor validation", out Character replacementTarget))
        {
            npc.IsHostile = false;
            npc.InCombat = false;
            npc.Stance = NPCStance.Default;
            npc.stateMachine?.HandleStanceChange(npc.Stance);
            CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT TARGET]", "CombatTurnManager.OnNPCTurnExecute removed hostile combatant with no valid target",
                $"Actor={npc.Name} [{npc.IInteractableID}]\n" +
                $"ReplacementTarget={replacementTarget?.Name ?? "NULL"}\n" +
                $"IsHostileAfter={npc.IsHostile}\n" +
                $"StanceAfter={npc.Stance}\n" +
                $"InCombatAfter={npc.InCombat}",
                npc);
            DeregisterCharacter(npc);
            TurnOrchestrator.Instance?.TryUpdateTurnContext();
            return;
        }

        UIController.Instance.UpdateTurnOrderUI();

        GameDebugger.Instance.LogInfo($"[CombatTurnManager] Executing NPC turn for {npc.Name}.");
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[ENTITY TURN]", "CombatTurnManager.OnNPCTurnExecute", null, npc);
        LogTurnOrderDiagnostic("[COMBAT TURN ORDER]", "CombatTurnManager.OnNPCTurnExecute begin",
            $"InputAccepted: False\nPlayerTurn: False\nActorRole: {BaseTurnManager.GetCombatParticipantRole(npc)}");
        LogCombatActorTurnMessage(npc);

        Vector2Int positionBefore = npc != null ? npc.NestedMapPosition : Vector2Int.zero;
        int apBefore = npc != null ? npc.ActionPoints : -1;
        int mpBeforeReset = npc != null ? npc.MovePoints : -1;
        int combatExertionBeforeReset = npc != null ? npc.CurrentCombatExertion : -1;
        int maxMovePoints = npc != null ? npc.MaxMovePoints : -1;

        if (npc != null)
        {
            npc.ResetMovePointsForTurn();
            npc.ResetCombatExertionForTurn("CombatTurnManager.OnNPCTurnExecute");
            npc.ResetConsumptionCapacityForTurn("CombatTurnManager.OnNPCTurnExecute");
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
            $"CombatExertion before combat NPC turn reset: {FixedPointResourceMath.Format(combatExertionBeforeReset)}\n" +
            $"CombatExertion after combat NPC turn reset: {FixedPointResourceMath.Format(npc?.CurrentCombatExertion ?? 0)}\n" +
            $"ConsumptionCapacity after combat NPC turn reset: {npc?.CurrentConsumptionCapacity.ToString() ?? "NULL"}\n" +
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

        bool mapRefreshRequested = UIController.Instance != null;
        if (mapRefreshRequested)
        {
            UIController.Instance.UpdateMapsAfterAction();
        }
        // CODEXLOG001_TURNLIFECYCLE: temporary visual refresh diagnostic after combat NPC action.
        TurnDiagnosticsLogger.LogEvent("[VISUAL REFRESH]", "CombatTurnManager.OnNPCTurnExecute refreshed map after NPC action",
            $"Reason: NPC combat action completed\n" +
            $"Actor: {npc?.Name ?? "NULL"} [{npc?.IInteractableID.ToString() ?? "NULL"}]\n" +
            $"RefreshMethod: UIController.UpdateMapsAfterAction\n" +
            $"RefreshRequested: {mapRefreshRequested}\n" +
            $"Position before: {positionBefore}\n" +
            $"Position after: {npc?.NestedMapPosition.ToString() ?? "NULL"}\n" +
            $"Position changed: {npc != null && npc.NestedMapPosition != positionBefore}",
            npc);
        // CODEXLOG002_MOVEMENT_AI: temporary movement visibility diagnostic after combat NPC action.
        MovementAIDiagnosticsLogger.LogEvent("[MAP REFRESH]", "CombatTurnManager.OnNPCTurnExecute requested map refresh after NPC action",
            $"RefreshMethod: UIController.UpdateMapsAfterAction\n" +
            $"RefreshRequested: {mapRefreshRequested}\n" +
            $"Position before: {positionBefore}\n" +
            $"Position after: {npc?.NestedMapPosition.ToString() ?? "NULL"}",
            npc);

        // CODEXLOG002_MOVEMENT_AI: temporary combat entity-turn movement diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[ENTITY TURN]", "CombatTurnManager.OnNPCTurnExecute end",
            $"Position before: {positionBefore}\n" +
            $"Position after: {npc?.NestedMapPosition.ToString() ?? "NULL"}\n" +
            $"Position changed: {npc != null && npc.NestedMapPosition != positionBefore}\n" +
            $"AP before: {apBefore}\n" +
            $"AP after: {npc?.ActionPoints.ToString() ?? "NULL"}\n" +
            $"MP before reset: {mpBeforeReset}\n" +
            $"MP after reset: {mpAfterReset}\n" +
            $"MP after: {npc?.MovePoints.ToString() ?? "NULL"}\n" +
            $"CombatExertion after: {FixedPointResourceMath.Format(npc?.CurrentCombatExertion ?? 0)}",
            npc);

        if (!npc.IsCombatActorAvailable() || !npc.InCombat)
        {
            CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT CONTEXT]", "CombatTurnManager.OnNPCTurnExecute deregistered actor after turn",
                $"Actor={npc.Name} [{npc.IInteractableID}]\n" +
                $"IsAlive={npc.IsAlive}\n" +
                $"IsActive={npc.IsActive}\n" +
                $"InCombat={npc.InCombat}",
                npc);
            DeregisterCharacter(npc);
        }
    }

    protected override void OnCycleEnded()
    {
        GameDebugger.Instance.LogInfo("[CombatTurnManager] Turn cycle ended.");

        if (!GameManager.Instance.ActiveTurnManager)
        {
            GameDebugger.Instance.LogInfo("[CombatTurnManager] Not restarting cycle. ActiveTurnManager is false.");
            return;
        }

        TurnOrchestrator.Instance?.TryUpdateTurnContext();
        PruneInvalidCombatants("CombatTurnManager.OnCycleEnded");
        if (TurnOrchestrator.Instance == null || TurnOrchestrator.Instance.CurrentContext != TurnContext.Combat)
        {
            GameDebugger.Instance.LogInfo("[CombatTurnManager] Not restarting cycle: combat context ended.");
            return;
        }

        if (!HasRestartableCombatParticipants())
        {
            CombatActionResolutionDiagnosticsLogger.LogWarning("CombatTurnManager.OnCycleEnded aborted cycle restart because no valid actors remain",
                $"RegisteredCount={DiagnosticRegisteredCount}\nCurrentContext={TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "NULL"}");
            TurnOrchestrator.Instance?.TryUpdateTurnContext();
        }
    }

    #endregion

    private void LogCombatActorTurnMessage(Character actor)
    {
        if (actor == null)
        {
            MessageLogManager.Instance?.Log("combat_bystander_turn", "Unknown");
            return;
        }

        if (actor.IsHostile || actor.Stance == NPCStance.Hostile)
        {
            MessageLogManager.Instance?.Log(actor is Monster ? "combat_monster_turn" : "combat_enemy_turn", actor.Name);
            return;
        }

        if (actor is Animal)
        {
            MessageLogManager.Instance?.Log("combat_animal_turn", actor.Name);
            return;
        }

        if (actor is Monster)
        {
            MessageLogManager.Instance?.Log("combat_monster_turn", actor.Name);
            return;
        }

        MessageLogManager.Instance?.Log("combat_bystander_turn", actor.Name);
    }

    #region Validation / Utilities

    private void PruneInvalidCombatants(string source)
    {
        List<Character> invalidCombatants = DiagnosticGetRegisteredCharactersSnapshot()
            .Where(character => character == null || !character.IsAlive || !character.IsActive || !character.InCombat)
            .ToList();

        foreach (Character combatant in invalidCombatants)
        {
            if (combatant == null)
            {
                continue;
            }

            CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT CONTEXT]", "CombatTurnManager.PruneInvalidCombatants removed invalid combatant",
                $"Source={source}\n" +
                $"Actor={combatant.Name} [{combatant.IInteractableID}]\n" +
                $"IsAlive={combatant.IsAlive}\n" +
                $"IsActive={combatant.IsActive}\n" +
                $"InCombat={combatant.InCombat}",
                combatant);
            DeregisterCharacter(combatant);
        }
    }

    private bool HasRestartableCombatParticipants()
    {
        Character playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        bool hasPlayer = DiagnosticGetRegisteredCharactersSnapshot()
            .Any(character => character != null && character == playerCharacter && character.IsAlive && character.IsActive && character.InCombat);
        bool hasOtherCombatant = DiagnosticGetRegisteredCharactersSnapshot()
            .Any(character => character != null &&
                              character != playerCharacter &&
                              character.IsAlive &&
                              character.IsActive &&
                              character.InCombat &&
                              (character.IsHostile || character.Stance == NPCStance.Hostile || playerCharacter?.Target == character || character.Target == playerCharacter));

        return hasPlayer && hasOtherCombatant;
    }

    protected override bool ShouldAutoStartNextCycle()
    {
        if (!GameManager.Instance.ActiveTurnManager)
        {
            return false;
        }

        if (TurnOrchestrator.Instance == null || TurnOrchestrator.Instance.CurrentContext != TurnContext.Combat)
        {
            return false;
        }

        PruneInvalidCombatants("CombatTurnManager.ShouldAutoStartNextCycle");
        return HasRestartableCombatParticipants();
    }

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
