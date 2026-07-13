using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class BaseTurnManager : MonoBehaviour
{
    #region Fields

    // CharacterID -> turn data
    protected readonly Dictionary<int, CharacterTurnData> characterTurnDataDict = new Dictionary<int, CharacterTurnData>();

    // Ordered list used by the turn engine
    protected readonly List<CharacterTurnData> sortedCharacterList = new List<CharacterTurnData>();

    protected int currentTurnIndex = 0;
    protected bool isPlayerTurn = false;
    protected bool isCycleRunning = false;
    protected bool isAdvancingTurnSequence = false;

    #endregion

    #region CODEXLOG001_TURNLIFECYCLE Diagnostics

    // CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
    public int DiagnosticRegisteredCount => characterTurnDataDict.Count;

    // CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
    public bool DiagnosticContainsCharacter(Character character)
    {
        return character != null && characterTurnDataDict.ContainsKey(character.IInteractableID);
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
    public List<Character> DiagnosticGetRegisteredCharactersSnapshot()
    {
        return characterTurnDataDict.Values
            .Select(data => data.Character)
            .Where(character => character != null)
            .ToList();
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
    public Character DiagnosticCurrentTurnActor =>
        currentTurnIndex >= 0 && currentTurnIndex < sortedCharacterList.Count
            ? sortedCharacterList[currentTurnIndex].Character
            : null;

    #endregion

    #region Registration

    /// <summary>
    /// Registers a character into the turn system.
    /// </summary>
    public virtual void RegisterCharacter(Character character, bool isPlayer = false)
    {
        if (character == null)
        {
            GameDebugger.Instance.LogError($"{GetType().Name}.RegisterCharacter: NULL character provided.");
            return;
        }

        int id = character.IInteractableID;

        if (characterTurnDataDict.ContainsKey(id))
        {
            GameDebugger.Instance.LogWarning(
                $"{GetType().Name}.RegisterCharacter: [{id}] {character.Name} is already registered.");
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogWarning("Duplicate registration ignored", $"{GetType().Name}.RegisterCharacter already contains [{id}] {character.Name}.", character);
            return;
        }

        int placementID = UnityEngine.Random.Range(0, int.MaxValue);
        int speed = character.Speed;

        var data = new CharacterTurnData(character, speed, isPlayer, placementID);
        characterTurnDataDict[id] = data;
        sortedCharacterList.Add(data);

        GameDebugger.Instance.LogInfo(
            $"{GetType().Name}.RegisterCharacter: [{id}] {character.Name} Speed={speed} PlacementID={placementID} IsPlayer={isPlayer}");
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogRegistration(GetType().Name, character, isPlayer);
    }

    /// <summary>
    /// Deregisters a character from the turn system.
    /// </summary>
	
	public virtual void ClearCharacters()
	{
		DeregisterAllCharacters();
	}

    public virtual void DeregisterCharacter(Character character)
    {
        if (character == null)
        {
            GameDebugger.Instance.LogError($"{GetType().Name}.DeregisterCharacter: NULL character provided.");
            return;
        }

        int id = character.IInteractableID;

        if (!characterTurnDataDict.TryGetValue(id, out var data))
        {
            GameDebugger.Instance.LogWarning(
                $"{GetType().Name}.DeregisterCharacter: [{id}] {character.Name} not found in registry.");
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogWarning("Deregister missing character", $"{GetType().Name}.DeregisterCharacter could not find [{id}] {character.Name}.", character);
            return;
        }

        // Adjust currentTurnIndex if we remove someone earlier in the list
        int removedIndex = sortedCharacterList.IndexOf(data);

        characterTurnDataDict.Remove(id);
        sortedCharacterList.Remove(data);

        if (removedIndex >= 0 && removedIndex <= currentTurnIndex && currentTurnIndex > 0)
        {
            currentTurnIndex--;
        }

        character.InTurn = false;

        GameDebugger.Instance.LogInfo(
            $"{GetType().Name}.DeregisterCharacter: [{id}] {character.Name} removed from turn system.");
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogDeregistration(GetType().Name, character);
    }

    /// <summary>
    /// Deregisters all characters whose NestedArea matches the given INestedArea.
    /// </summary>
    public virtual void DeregisterCharactersInNestedArea(INestedArea area)
    {
        if (area == null)
        {
            GameDebugger.Instance.LogWarning(
                $"{GetType().Name}.DeregisterCharactersInNestedArea: NULL area supplied.");
            return;
        }

        var idsToRemove = characterTurnDataDict
            .Where(kv => kv.Value.NestedArea == area || kv.Value.Character.CurrentNestedArea == area)
            .Select(kv => kv.Key)
            .ToList();

        foreach (int id in idsToRemove)
        {
            if (characterTurnDataDict.TryGetValue(id, out var data))
            {
                DeregisterCharacter(data.Character);
            }
        }

        GameDebugger.Instance.LogInfo(
            $"{GetType().Name}.DeregisterCharactersInNestedArea: Removed {idsToRemove.Count} characters from area {area.NestedAreaID}.");
    }

    /// <summary>
    /// Clears all turn data.
    /// </summary>
    public virtual void DeregisterAllCharacters()
    {
        GameDebugger.Instance.LogInfo($"{GetType().Name}.DeregisterAllCharacters: Clearing all turn data.");
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[DEREGISTRATION]", $"{GetType().Name}.DeregisterAllCharacters before clear", $"RegisteredCount: {characterTurnDataDict.Count}");

        foreach (var data in sortedCharacterList)
        {
            if (data.Character != null)
            {
                data.Character.InTurn = false;
            }
        }

        characterTurnDataDict.Clear();
        sortedCharacterList.Clear();
        currentTurnIndex = 0;
        isPlayerTurn = false;
        isCycleRunning = false;
        isAdvancingTurnSequence = false;
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[DEREGISTRATION]", $"{GetType().Name}.DeregisterAllCharacters after clear", "RegisteredCount: 0");
    }

    #endregion

    #region Turn Cycle

    /// <summary>
    /// Starts a full turn cycle by sorting characters and executing the first turn.
    /// </summary>
    public virtual void StartTurnCycle()
    {
        if (!CanExecuteForCurrentContext("StartTurnCycle"))
        {
            return;
        }

        if (!BeginCycleInternal($"{GetType().Name}.StartTurnCycle"))
        {
            return;
        }

        ContinueTurnSequence($"{GetType().Name}.StartTurnCycle");
    }

    /// <summary>
    /// Sorts characters by speed (desc) then placement ID (asc).
    /// </summary>
    protected virtual void SortCharacters()
    {
        sortedCharacterList.Sort((a, b) =>
        {
            int speedCompare = b.Speed.CompareTo(a.Speed);
            return speedCompare != 0 ? speedCompare : a.PlacementID.CompareTo(b.PlacementID);
        });

        for (int i = 0; i < sortedCharacterList.Count; i++)
        {
            sortedCharacterList[i].CurrentTurnID = i + 1;
        }

        string order = string.Join(
            " -> ",
            sortedCharacterList.Select(d => $"{d.Character?.Name ?? "NULL"}(ID:{d.Character?.IInteractableID})"));

        GameDebugger.Instance.LogInfo(
            $"{GetType().Name}.SortCharacters: Count={sortedCharacterList.Count} Order={order}");

        LogTurnOrderDiagnostic("[COMBAT TURN ORDER]", $"{GetType().Name}.SortCharacters completed", null);
    }

    /// <summary>
    /// Called by external systems when the player has finished their actions.
    /// </summary>
    public virtual void PlayerTurnCompleted()
    {
        if (!CanExecuteForCurrentContext("PlayerTurnCompleted"))
        {
            return;
        }

        if (!isPlayerTurn)
        {
            GameDebugger.Instance.LogWarning(
                $"{GetType().Name}.PlayerTurnCompleted: Called when it is not currently the player turn.");
            return;
        }

        if (currentTurnIndex < 0 || currentTurnIndex >= sortedCharacterList.Count)
        {
            GameDebugger.Instance.LogError(
                $"{GetType().Name}.PlayerTurnCompleted: currentTurnIndex out of range.");
            isPlayerTurn = false;
            return;
        }

        var data = sortedCharacterList[currentTurnIndex];
        var player = data.Character;

        TurnDiagnosticsLogger.LogEvent("[PLAYER TURN]", $"{GetType().Name}.PlayerTurnCompleted",
            $"CurrentTurnIndex: {currentTurnIndex}\n" +
            $"SortedCharacterCount: {sortedCharacterList.Count}\n" +
            $"IsPlayerTurn: {isPlayerTurn}\n" +
            $"Player: {player?.Name ?? "NULL"}\n" +
            $"Player.IsActive: {player?.IsActive.ToString() ?? "NULL"}\n" +
            $"Player.IsAlive: {player?.IsAlive.ToString() ?? "NULL"}\n" +
            $"Player.InTurn: {player?.InTurn.ToString() ?? "NULL"}\n" +
            $"Player.InCombat: {player?.InCombat.ToString() ?? "NULL"}",
            player);

        if (player == null)
        {
            GameDebugger.Instance.LogError(
                $"{GetType().Name}.PlayerTurnCompleted: Player Character is NULL at index {currentTurnIndex}.");
            isPlayerTurn = false;
            currentTurnIndex++;
            ContinueTurnSequence($"{GetType().Name}.PlayerTurnCompleted null player");
            return;
        }

        isPlayerTurn = false;
        LogTurnOrderDiagnostic("[COMBAT TURN ADVANCE]", $"{GetType().Name}.PlayerTurnCompleted", "Completing player turn before EndTurnForCharacter.");
        EndTurnForCharacter(player);
        ContinueTurnSequence($"{GetType().Name}.PlayerTurnCompleted");
    }

    /// <summary>
    /// Ends the current character's turn and advances the cycle.
    /// </summary>
    protected virtual void EndTurnForCharacter(Character character, bool advanceIndex = true)
    {
        if (!CanExecuteForCurrentContext("EndTurnForCharacter"))
        {
            return;
        }

        if (character == null)
        {
            GameDebugger.Instance.LogError($"{GetType().Name}.EndTurnForCharacter: NULL character.");
            return;
        }

        if (TurnOrchestrator.Instance != null)
        {
            TurnOrchestrator.Instance.AuditMarkTurn(character, "End");
        }

        GameDebugger.Instance.LogInfo(
            $"{GetType().Name}.EndTurnForCharacter: [{character.IInteractableID}] {character.Name} turn ended.");

        LogTurnOrderDiagnostic("[COMBAT TURN ADVANCE]", $"{GetType().Name}.EndTurnForCharacter before advance",
            $"PreviousActor: {FormatTurnActor(character)}\nAdvanceIndex: {advanceIndex}");

        character.InTurn = false;
        character.OnTurnEnd();

        if (advanceIndex)
        {
            currentTurnIndex++;
        }

        LogTurnOrderDiagnostic("[COMBAT TURN ADVANCE]", $"{GetType().Name}.EndTurnForCharacter after advance",
            $"PreviousActor: {FormatTurnActor(character)}\nAdvanceIndex: {advanceIndex}");
    }

    /// <summary>
    /// Called when the cycle reaches the end of the list.
    /// </summary>
    protected virtual void EndCycle()
    {
        if (!CanExecuteForCurrentContext("EndCycle"))
        {
            isCycleRunning = false;
            isPlayerTurn = false;
            isAdvancingTurnSequence = false;
            return;
        }

        GameDebugger.Instance.LogInfo($"{GetType().Name}.EndCycle: Turn cycle complete.");
        isCycleRunning = false;
        isPlayerTurn = false;
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[TURN CYCLE]", $"{GetType().Name}.EndCycle", $"RegisteredCount: {characterTurnDataDict.Count}");

        if (TurnOrchestrator.Instance != null)
        {
            TurnOrchestrator.Instance.AuditEndCycle();
        }

        OnCycleEnded();
    }

    #endregion

    #region Turn Order Diagnostics

    // CODEXLOG001_TURNLIFECYCLE: temporary combat turn-order diagnostic helper.
    protected void LogTurnOrderDiagnostic(string category, string eventName, string extraDetails)
    {
        if (TurnOrchestrator.Instance == null ||
            TurnOrchestrator.Instance.CurrentContext != TurnContext.Combat)
        {
            return;
        }

        CharacterTurnData currentData = currentTurnIndex >= 0 && currentTurnIndex < sortedCharacterList.Count
            ? sortedCharacterList[currentTurnIndex]
            : null;
        CharacterTurnData nextData = GetNextTurnData();

        string participantLines = sortedCharacterList.Count == 0
            ? "NONE"
            : string.Join("\n", sortedCharacterList.Select((data, index) =>
            {
                Character character = data.Character;
                return $"{index + 1}. {FormatTurnActor(character)} Role={GetCombatParticipantRole(character, data.IsPlayer)} IsPlayer={data.IsPlayer} Speed={data.Speed} Inactive={character != null && !character.IsActive} MissingArea={character != null && character.CurrentNestedArea == null}";
            }));

        string details =
            $"Manager: {GetType().Name}\n" +
            $"CurrentTurnIndex: {currentTurnIndex}\n" +
            $"ParticipantCount: {sortedCharacterList.Count}\n" +
            $"IsPlayerTurnFlag: {isPlayerTurn}\n" +
            $"CurrentActor: {FormatTurnActor(currentData?.Character)}\n" +
            $"CurrentActorRole: {GetCombatParticipantRole(currentData?.Character, currentData?.IsPlayer ?? false)}\n" +
            $"CurrentActorIsPlayer: {currentData?.IsPlayer.ToString() ?? "NULL"}\n" +
            $"CurrentActorAP: {currentData?.Character?.ActionPoints.ToString() ?? "NULL"}\n" +
            $"CurrentActorMP: {currentData?.Character?.MovePoints.ToString() ?? "NULL"}\n" +
            $"CurrentActorInTurn: {currentData?.Character?.InTurn.ToString() ?? "NULL"}\n" +
            $"CurrentActorInCombat: {currentData?.Character?.InCombat.ToString() ?? "NULL"}\n" +
            $"CurrentActorIsHostile: {currentData?.Character?.IsHostile.ToString() ?? "NULL"}\n" +
            $"NextActor: {FormatTurnActor(nextData?.Character)}\n" +
            $"NextActorRole: {GetCombatParticipantRole(nextData?.Character, nextData?.IsPlayer ?? false)}\n" +
            $"NextActorIsPlayer: {nextData?.IsPlayer.ToString() ?? "NULL"}\n" +
            $"Participants:\n{participantLines}";

        if (!string.IsNullOrEmpty(extraDetails))
        {
            details += $"\n{extraDetails}";
        }

        TurnDiagnosticsLogger.LogEvent(category, eventName, details, currentData?.Character);
    }

    private CharacterTurnData GetNextTurnData()
    {
        int nextIndex = currentTurnIndex + 1;
        while (nextIndex < sortedCharacterList.Count)
        {
            CharacterTurnData next = sortedCharacterList[nextIndex];
            if (next?.Character != null)
            {
                return next;
            }

            nextIndex++;
        }

        return null;
    }

    private string FormatTurnActor(Character character)
    {
        if (character == null) return "NULL";
        return $"{character.Name} [{character.IInteractableID}] ({character.GetType().Name})";
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary combat participant role diagnostic helper.
    public static string GetCombatParticipantRole(Character character, bool isPlayer = false)
    {
        if (character == null) return "Invalid/Null";
        if (isPlayer || character == PlayerStats.Instance?.CurrentPlayerCharacter) return "Player";
        if (!character.IsActive) return "Inactive/Removed";
        if (character.CurrentNestedArea == null) return "MissingAreaContext";

        string disposition = character.IsHostile || character.Stance == NPCStance.Hostile
            ? "Hostile"
            : "Neutral";

        if (character is Monster) return $"{disposition} Monster";
        if (character is Animal) return $"{disposition} Animal";
        if (character is NPC) return character.IsHostile || character.Stance == NPCStance.Hostile
            ? "Hostile NPC"
            : "Neutral/Bystander NPC";

        return $"{disposition} Character";
    }

    #endregion

    #region Validation / Utilities

    /// <summary>
    /// Ensures CharacterTurnData.NestedArea matches Character.CurrentNestedArea.
    /// </summary>
    public virtual void ValidateCharacterNestedAreas()
    {
        foreach (var kv in characterTurnDataDict)
        {
            var data = kv.Value;
            var character = data.Character;

            if (character == null)
            {
                GameDebugger.Instance.LogWarning(
                    $"{GetType().Name}.ValidateCharacterNestedAreas: NULL Character for ID={kv.Key}.");
                continue;
            }

            var currentArea = character.CurrentNestedArea;
            if (currentArea != data.NestedArea)
            {
                GameDebugger.Instance.LogInfo(
                    $"{GetType().Name}.ValidateCharacterNestedAreas: Syncing NestedArea for [{character.IInteractableID}] {character.Name}.");

                data.UpdateNestedArea(currentArea);
            }
        }
    }

    public bool IsCharacterRegistered(Character c)
    {
        if (c == null) return false;
        return characterTurnDataDict.ContainsKey(c.IInteractableID);
    }

    public virtual Dictionary<int, string> GetRegisteredCharacters()
    {
        Dictionary<int, string> registeredCharacters = new Dictionary<int, string>();
        List<string> duplicates = new List<string>();

        foreach (var data in characterTurnDataDict.Values)
        {
            if (data == null) continue;

            var character = data.Character;
            if (character == null) continue;

            int id = character.IInteractableID;
            if (registeredCharacters.ContainsKey(id))
            {
                duplicates.Add($"[{id}] {character.Name} ({character.GetType().Name})");
                continue;
            }

            registeredCharacters[id] = character.Name;
        }

        if (duplicates.Count > 0)
        {
            string duplicateDetails = string.Join(", ", duplicates);
            GameDebugger.Instance.LogWarning($"{GetType().Name}.GetRegisteredCharacters: Duplicate character IDs skipped: {duplicateDetails}");
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogWarning("Duplicate registered-character IDs found",
                $"{GetType().Name}.GetRegisteredCharacters skipped duplicate IDs: {duplicateDetails}");
        }

        return registeredCharacters;
    }

    public virtual void LogAllRegisteredCharacters()
    {
        if (sortedCharacterList.Count == 0)
        {
            GameDebugger.Instance.LogInfo($"{GetType().Name}.LogAllRegisteredCharacters: No characters registered.");
            return;
        }

        var lines = sortedCharacterList
            .Select(d =>
            {
                var c = d.Character;
                if (c == null) return "[NULL Character]";
                return $"[{c.IInteractableID}] {c.Name} Speed={d.Speed} TurnID={d.CurrentTurnID}";
            })
            .ToList();

        string report = string.Join("\n", lines);

        GameDebugger.Instance.LogInfo($"{GetType().Name}.LogAllRegisteredCharacters:\n{report}");
    }

    public virtual List<string> GetTurnOrderList()
    {
        if (sortedCharacterList.Count == 0)
        {
            return new List<string> { "No registered characters." };
        }

        return sortedCharacterList.Select(d =>
        {
            var c = d.Character;
            if (c == null) return "NULL Character";
            return $"{d.CurrentTurnID}. {c.Name} (Speed: {d.Speed})";
        }).ToList();
    }

    public virtual List<Character> GetAllRegisteredCharacters()
    {
        return characterTurnDataDict.Values
            .Select(d => d.Character)
            .Where(c => c != null)
            .ToList();
    }

    #endregion

    #region Hooks

    /// <summary>
    /// Implement context-specific skip logic (dead, inactive, wrong area, etc.).
    /// </summary>
    protected abstract bool ShouldSkipCharacter(Character character);

    protected void ContinueTurnSequence(string source)
    {
        if (!CanExecuteForCurrentContext("ContinueTurnSequence"))
        {
            isCycleRunning = false;
            isPlayerTurn = false;
            return;
        }

        if (isAdvancingTurnSequence)
        {
            TurnDiagnosticsLogger.LogWarning("Turn sequence advance ignored because advancement is already in progress",
                $"{GetType().Name}.ContinueTurnSequence ignored.\nSource={source}\nCurrentTurnIndex={currentTurnIndex}\nRegisteredCount={characterTurnDataDict.Count}");
            return;
        }

        isAdvancingTurnSequence = true;

        try
        {
            while (CanExecuteForCurrentContext("ContinueTurnSequence.Loop"))
            {
                if (!isCycleRunning)
                {
                    if (!BeginCycleInternal($"{GetType().Name}.ContinueTurnSequence begin from {source}"))
                    {
                        return;
                    }
                }

                if (sortedCharacterList.Count == 0)
                {
                    GameDebugger.Instance.LogInfo($"{GetType().Name}.ContinueTurnSequence: No entries in sortedCharacterList.");
                    EndCycle();
                    if (!ShouldAutoStartNextCycle())
                    {
                        return;
                    }

                    continue;
                }

                if (currentTurnIndex >= sortedCharacterList.Count)
                {
                    EndCycle();
                    if (!ShouldAutoStartNextCycle())
                    {
                        return;
                    }

                    continue;
                }

                CharacterTurnData data = sortedCharacterList[currentTurnIndex];
                Character character = data?.Character;

                if (character == null)
                {
                    GameDebugger.Instance.LogWarning(
                        $"{GetType().Name}.ContinueTurnSequence: NULL Character at index {currentTurnIndex}. Skipping.");
                    currentTurnIndex++;
                    continue;
                }

                if (ShouldSkipCharacter(character))
                {
                    GameDebugger.Instance.LogInfo(
                        $"{GetType().Name}.ContinueTurnSequence: Skipping [{character.IInteractableID}] {character.Name}.");
                    currentTurnIndex++;
                    continue;
                }

                character.InTurn = true;
                float delay = GetTurnDelay(character);

                GameDebugger.Instance.LogInfo(
                    $"{GetType().Name}.ContinueTurnSequence: Starting turn for [{character.IInteractableID}] {character.Name} Delay={delay}");
                TurnDiagnosticsLogger.LogEvent("[ENTITY TURN]", $"{GetType().Name}.ContinueTurnSequence starting entity turn", $"Delay: {delay}", character);
                LogTurnOrderDiagnostic("[COMBAT TURN ADVANCE]", $"{GetType().Name}.ContinueTurnSequence starting entity turn",
                    $"Delay: {delay}");

                if (TurnOrchestrator.Instance != null)
                {
                    TurnOrchestrator.Instance.AuditMarkTurn(character, "Start");
                }

                if (data.IsPlayer)
                {
                    isPlayerTurn = true;
                    LogTurnOrderDiagnostic("[COMBAT TURN ADVANCE]", $"{GetType().Name}.ContinueTurnSequence entering player turn", null);
                    OnPlayerTurnStart(character);
                    if (isPlayerTurn)
                    {
                        return;
                    }

                    continue;
                }

                isPlayerTurn = false;
                LogTurnOrderDiagnostic("[COMBAT TURN ADVANCE]", $"{GetType().Name}.ContinueTurnSequence entering NPC turn", null);
                OnNPCTurnExecute(character);
                EndTurnForCharacter(character);
            }
        }
        finally
        {
            isAdvancingTurnSequence = false;
        }
    }

    private bool BeginCycleInternal(string source)
    {
        if (!CanExecuteForCurrentContext("BeginCycleInternal"))
        {
            return false;
        }

        if (isCycleRunning)
        {
            GameDebugger.Instance.LogWarning($"{GetType().Name}.BeginCycleInternal: Ignored because a cycle is already running.");
            TurnDiagnosticsLogger.LogWarning("Turn cycle begin ignored because cycle is already running",
                $"{GetType().Name}.BeginCycleInternal ignored.\nSource={source}\nRegisteredCount: {characterTurnDataDict.Count}");
            return false;
        }

        if (characterTurnDataDict.Count == 0)
        {
            GameDebugger.Instance.LogInfo($"{GetType().Name}.BeginCycleInternal: No characters registered.");
            TurnDiagnosticsLogger.LogWarning("Turn cycle begin requested with zero participants",
                $"{GetType().Name}.BeginCycleInternal found no registered characters.\nSource={source}");
            return false;
        }

        bool hasPlayer = characterTurnDataDict.Values.Any(d => d.IsPlayer);
        if (!hasPlayer)
        {
            GameDebugger.Instance.LogWarning($"{GetType().Name}.BeginCycleInternal: No player character registered.");
            TurnDiagnosticsLogger.LogWarning("Turn cycle begin has no registered player",
                $"{GetType().Name}.BeginCycleInternal found no player character.\nSource={source}");
        }

        TurnDiagnosticsLogger.LogEvent("[TURN CYCLE]", $"{GetType().Name}.BeginCycleInternal",
            $"RegisteredCount: {characterTurnDataDict.Count}\nSource: {source}");

        isCycleRunning = true;
        SortCharacters();
        currentTurnIndex = 0;
        isPlayerTurn = false;

        if (TurnOrchestrator.Instance != null)
        {
            List<Character> orderedChars = sortedCharacterList
                .Select(d => d.Character)
                .Where(c => c != null)
                .ToList();

            TurnOrchestrator.Instance.AuditBeginCycle(orderedChars);
        }

        return true;
    }

    protected virtual bool ShouldAutoStartNextCycle()
    {
        return false;
    }

    private bool CanExecuteForCurrentContext(string action)
    {
        if (TurnOrchestrator.Instance == null)
        {
            return true;
        }

        TurnContext currentContext = TurnOrchestrator.Instance.CurrentContext;
        bool allowed =
            (this is ExplorationTurnManager && currentContext == TurnContext.Exploration) ||
            (this is CombatTurnManager && currentContext == TurnContext.Combat);

        // CODEXLOG001_TURNLIFECYCLE: temporary turn manager ownership diagnostic.
        TurnDiagnosticsLogger.LogEvent("[TURN MANAGER OWNERSHIP]", $"{GetType().Name}.{action}",
            $"CurrentContext: {currentContext}\n" +
            $"ExecutingManager: {GetType().Name}\n" +
            $"Action: {(allowed ? "Allowed" : "BlockedOwnershipViolation")}\n" +
            $"RegisteredCount: {characterTurnDataDict.Count}\n" +
            $"IsCycleRunning: {isCycleRunning}\n" +
            $"IsPlayerTurn: {isPlayerTurn}");

        if (!allowed)
        {
            GameDebugger.Instance.LogWarning($"{GetType().Name}.{action}: blocked because current context is {currentContext}.");
        }

        return allowed;
    }

    /// <summary>
    /// Implement context-specific delay before a character's turn runs.
    /// </summary>
    protected abstract float GetTurnDelay(Character character);

    /// <summary>
    /// Called when it is the player character's turn.
    /// </summary>
    protected abstract void OnPlayerTurnStart(Character playerCharacter);

    /// <summary>
    /// Called to execute an NPC's turn logic.
    /// </summary>
    protected abstract void OnNPCTurnExecute(Character npc);

    /// <summary>
    /// Called when a full cycle completes (end of round).
    /// Subclasses handle context cleanup only. Cycle continuation is owned by BaseTurnManager.
    /// </summary>
    protected abstract void OnCycleEnded();

    #endregion
}

#region CharacterTurnData

/// <summary>
/// Lightweight container for turn-related metadata.
/// </summary>
public class CharacterTurnData
{
    public Character Character { get; private set; }
    public INestedArea NestedArea { get; private set; }
    public string Name { get; private set; }
    public int Speed { get; private set; }
    public bool IsPlayer { get; private set; }
    public int PlacementID { get; private set; }
    public int CurrentTurnID { get; set; }

    public CharacterTurnData(Character character, int speed, bool isPlayer, int placementID)
    {
        Character = character;
        NestedArea = character != null ? character.CurrentNestedArea : null;
        Name = character != null ? character.Name : "NULL";
        Speed = speed;
        IsPlayer = isPlayer;
        PlacementID = placementID;
    }

    public void UpdateNestedArea(INestedArea newArea)
    {
        NestedArea = newArea;

        if (Character != null)
        {
            Character.CurrentNestedArea = newArea;
        }
    }
}

#endregion
