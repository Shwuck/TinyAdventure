using System.Collections;
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
        if (isCycleRunning)
        {
            GameDebugger.Instance.LogWarning($"{GetType().Name}.StartTurnCycle: Ignored because a cycle is already running.");
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogWarning("Turn cycle start ignored because cycle is already running",
                $"{GetType().Name}.StartTurnCycle ignored duplicate start request. RegisteredCount: {characterTurnDataDict.Count}");
            return;
        }

        if (characterTurnDataDict.Count == 0)
        {
            GameDebugger.Instance.LogInfo($"{GetType().Name}.StartTurnCycle: No characters registered.");
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogWarning("Turn cycle requested with zero participants", $"{GetType().Name}.StartTurnCycle found no registered characters.");
            return;
        }

        bool hasPlayer = characterTurnDataDict.Values.Any(d => d.IsPlayer);
        if (!hasPlayer)
        {
            GameDebugger.Instance.LogWarning($"{GetType().Name}.StartTurnCycle: No player character registered.");
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogWarning("Turn cycle has no registered player", $"{GetType().Name}.StartTurnCycle found no player character.");
        }

        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[TURN CYCLE]", $"{GetType().Name}.StartTurnCycle", $"RegisteredCount: {characterTurnDataDict.Count}");

        isCycleRunning = true;
        SortCharacters();

        currentTurnIndex = 0;
        isPlayerTurn = false;

        // Hook into TurnOrchestrator audit, if present
        if (TurnOrchestrator.Instance != null)
        {
            var orderedChars = sortedCharacterList
                .Select(d => d.Character)
                .Where(c => c != null)
                .ToList();

            TurnOrchestrator.Instance.AuditBeginCycle(orderedChars);
        }

        ExecuteNextTurn();
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
    }

    /// <summary>
    /// Advances to the next valid character and begins their turn.
    /// </summary>
    protected virtual void ExecuteNextTurn()
    {
        // No characters at all
        if (sortedCharacterList.Count == 0)
        {
            GameDebugger.Instance.LogInfo($"{GetType().Name}.ExecuteNextTurn: No entries in sortedCharacterList.");
            EndCycle();
            return;
        }

        // Cycle finished
        if (currentTurnIndex >= sortedCharacterList.Count)
        {
            EndCycle();
            return;
        }

        var data = sortedCharacterList[currentTurnIndex];
        var character = data.Character;

        if (character == null)
        {
            GameDebugger.Instance.LogWarning(
                $"{GetType().Name}.ExecuteNextTurn: NULL Character at index {currentTurnIndex}. Skipping.");
            currentTurnIndex++;
            ExecuteNextTurn();
            return;
        }

        if (ShouldSkipCharacter(character))
        {
            GameDebugger.Instance.LogInfo(
                $"{GetType().Name}.ExecuteNextTurn: Skipping [{character.IInteractableID}] {character.Name}.");
            currentTurnIndex++;
            ExecuteNextTurn();
            return;
        }

        character.InTurn = true;
        float delay = GetTurnDelay(character);

        GameDebugger.Instance.LogInfo(
            $"{GetType().Name}.ExecuteNextTurn: Starting turn for [{character.IInteractableID}] {character.Name} Delay={delay}");
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[ENTITY TURN]", $"{GetType().Name}.ExecuteNextTurn starting entity turn", $"Delay: {delay}", character);

        if (TurnOrchestrator.Instance != null)
        {
            TurnOrchestrator.Instance.AuditMarkTurn(character, "Start");
        }

        StartCoroutine(ExecuteTurnWithDelay(delay, data));
    }

    /// <summary>
    /// Coroutine wrapper that handles optional delay, then executes player or NPC logic.
    /// </summary>
    private IEnumerator ExecuteTurnWithDelay(float delay, CharacterTurnData data)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        var character = data.Character;

        if (character == null)
        {
            GameDebugger.Instance.LogWarning(
                $"{GetType().Name}.ExecuteTurnWithDelay: Character became NULL. Skipping.");
            currentTurnIndex++;
            ExecuteNextTurn();
            yield break;
        }

        if (data.IsPlayer)
        {
            isPlayerTurn = true;
            OnPlayerTurnStart(character);
        }
        else
        {
            isPlayerTurn = false;
            OnNPCTurnExecute(character);
            EndTurnForCharacter(character);
        }
    }

    /// <summary>
    /// Called by external systems when the player has finished their actions.
    /// </summary>
    public virtual void PlayerTurnCompleted()
    {
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

        if (player == null)
        {
            GameDebugger.Instance.LogError(
                $"{GetType().Name}.PlayerTurnCompleted: Player Character is NULL at index {currentTurnIndex}.");
            isPlayerTurn = false;
            currentTurnIndex++;
            ExecuteNextTurn();
            return;
        }

        isPlayerTurn = false;
        EndTurnForCharacter(player);
    }

    /// <summary>
    /// Ends the current character's turn and advances the cycle.
    /// </summary>
    protected virtual void EndTurnForCharacter(Character character, bool advanceIndex = true)
    {
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

        character.InTurn = false;
        character.OnTurnEnd();

        if (advanceIndex)
        {
            currentTurnIndex++;
        }

        ExecuteNextTurn();
    }

    /// <summary>
    /// Called when the cycle reaches the end of the list.
    /// </summary>
    protected virtual void EndCycle()
    {
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
    /// Subclasses decide whether to immediately restart, reset state, etc.
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
