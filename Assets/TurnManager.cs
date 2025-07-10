using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    private Dictionary<int, CharacterTurnData> characterTurnDataDict = new Dictionary<int, CharacterTurnData>();
    private List<CharacterTurnData> sortedCharacterList = new List<CharacterTurnData>();
    private int currentTurnIndex = 0;
    public int turnsToSkip = 0;
    private int totalCharacters = 0;
#pragma warning disable CS0414
    private bool isPlayerTurn = false;
#pragma warning restore CS0414

    public delegate void PlayerTurnHandler();
    public event PlayerTurnHandler OnPlayerTurn;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            GameDebugger.Instance.LogInfo("TurnManager Awake complete. Instance set.");
        }
        else
        {
            Destroy(gameObject);
            GameDebugger.Instance.LogInfo("TurnManager Awake: Duplicate instance destroyed.");
        }
    }

    public void RegisterCharacter(Character character, bool isPlayer = false)
    {
        character.InTurn = false;
        int characterID = character.IInteractableID;
        string characterName = character.Name;
        int speed = character.Speed;

        GameDebugger.Instance.LogInfo($"Registering Character {characterID} ({characterName})");

        if (!characterTurnDataDict.ContainsKey(characterID))
        {
            int placementID = UnityEngine.Random.Range(0, int.MaxValue);
            var newCharacterData = new CharacterTurnData(character, speed, isPlayer, placementID);
            characterTurnDataDict[characterID] = newCharacterData;
            sortedCharacterList.Add(newCharacterData);

            GameDebugger.Instance.LogInfo($"Registered Character {characterID} ({characterName}) with speed {speed} and PlacementID {placementID}. IsPlayer: {isPlayer}");
        }
        else
        {
            GameDebugger.Instance.LogWarning($"Character {characterID} ({characterName}) is already registered.");
        }
    }

    public void DeregisterCharacter(Character character)
    {
        if (character == null)
        {
            GameDebugger.Instance.LogError("DeregisterCharacter: Attempted to deregister a NULL character!");
            return;
        }

        int characterID = character.IInteractableID;
        string characterName = character.Name;
        GameDebugger.Instance.LogInfo($"Deregistering Character {characterID} ({characterName})");

        if (characterTurnDataDict.ContainsKey(characterID))
        {
            var characterData = characterTurnDataDict[characterID];

            // **Ensure character is fully removed from the turn cycle**
            if (character.InTurn)
            {
                GameDebugger.Instance.LogInfo($"Character {characterID} ({characterName}) is currently in turn. Ending their turn before deregistering.");
                character.InTurn = false; // Ensure they are marked as NOT in turn
                EndTurnForCharacter(character);  // Properly end their turn before removing
            }

            characterTurnDataDict.Remove(characterID);
            sortedCharacterList.Remove(characterData);

            GameDebugger.Instance.LogInfo($"Deregistered Character {characterID} ({characterName}) successfully.");
        }
        else
        {
            GameDebugger.Instance.LogWarning($"Character {characterID} ({characterName}) not found in TurnManager.");
        }

        // **Ensure they cannot act after being deregistered**
        character.IsInNestedArea = false;
        character.CurrentNestedArea = null; // Ensure this is set to null AFTER removing from TurnManager

        GameDebugger.Instance.LogInfo($"Character {characterID} ({characterName}) is now fully deregistered and removed from NestedArea.");
    }

    public void DeregisterCharactersInNestedArea(INestedArea nestedArea)
    {
        if (nestedArea == null)
        {
            GameDebugger.Instance.LogWarning("DeregisterCharactersInNestedArea: Attempted to deregister from a NULL nested area.");
            return;
        }

        GameDebugger.Instance.LogInfo($"Deregistering all characters from Nested Area ID {nestedArea.NestedAreaID}");

        var charactersToRemove = characterTurnDataDict
            .Where(c => c.Value.NestedArea == nestedArea)
            .Select(c => c.Key)
            .ToList();

        foreach (var characterID in charactersToRemove)
        {
            if (characterTurnDataDict.TryGetValue(characterID, out var characterData))
            {
                DeregisterCharacter(characterData.Character);
            }
        }

        GameDebugger.Instance.LogInfo($"Deregistered {charactersToRemove.Count} characters from Nested Area ID {nestedArea.NestedAreaID}");
    }


    public void StartTurnCycle()
    {
        if (!GameManager.Instance.ActiveTurnManager)
        {
            GameDebugger.Instance.LogInfo("TurnManager is inactive. Turn cycle will not start.");
            return;
        }

        if (characterTurnDataDict.Count == 0)
        {
            GameDebugger.Instance.LogInfo("No characters registered. Turn cycle will not start.");
            return;
        }

        bool hasPlayerCharacter = characterTurnDataDict.Values.Any(c => c.IsPlayer);

        if (!hasPlayerCharacter)
        {
            GameDebugger.Instance.LogInfo("No active player character found. Turn cycle will not start.");
            return;
        }

     //   ValidateCharacterNestedAreas();
        SortCharactersBySpeed();
        UIController.Instance.UpdateTurnOrderUI();
        currentTurnIndex = 0;
        isPlayerTurn = false;
        ExecuteNextTurn();
    }

    private void SortCharactersBySpeed()
    {
        sortedCharacterList = sortedCharacterList
            .OrderByDescending(c => c.Speed)
            .ThenBy(c => c.PlacementID)
            .ToList();

        // Assign CurrentTurnID based on the new order
        for (int i = 0; i < sortedCharacterList.Count; i++)
        {
            sortedCharacterList[i].CurrentTurnID = i + 1;  // ID starts from 1 for the first character
        }

        // Update the totalCharacters variable
        totalCharacters = sortedCharacterList.Count;

        GameDebugger.Instance.LogInfo($"Characters sorted by speed and PlacementID, CurrentTurnID assigned. Total characters: {totalCharacters}");
    }

    private void ExecuteNextTurn()
    {
        if (!GameManager.Instance.ActiveTurnManager)
        {
            GameDebugger.Instance.LogInfo("TurnManager is inactive. ExecuteNextTurn will not proceed.");
            return;
        }

        if (currentTurnIndex >= sortedCharacterList.Count)
        {
            EndEntireTurn();
            return;
        }

        var currentCharacterData = sortedCharacterList[currentTurnIndex];
        var character = currentCharacterData.Character;
        character.InTurn = true;

        UIController.Instance.UpdateTurnOrderUI();

        PlayerStats.Instance.CurrentPlayerCharacter.CurrentNestedArea.UpdateHostileAreaStatus();
        // Check if the area is hostile using IsHostileArea
        bool hasHostiles = PlayerStats.Instance.CurrentPlayerCharacter.CurrentNestedArea?.IsHostileArea ?? false;


        // Set delay based on whether hostiles are present
        float turnDelay = hasHostiles ? 3f : 0f;

        // Log execution details
        string nextCharacterName = (currentTurnIndex + 1 < sortedCharacterList.Count)
            ? sortedCharacterList[currentTurnIndex + 1].Character.Name
            : "No one (end of turn cycle)";

        string actionDescription = character.Stance switch
        {
            NPCStance.Hostile => $"attacking {character.Target?.Name ?? "someone"}",
            NPCStance.Fleeing => "fleeing",
            NPCStance.Following => "following their target",
            NPCStance.TrueIdle => "doing nothing",
            _ => "idling"
        };

        GameDebugger.Instance.LogInfo($"Executing {character.Name}, they are {actionDescription}. Next in line is {nextCharacterName}. Hostiles present: {hasHostiles}.");

        // Start the coroutine to apply the delay before executing the next turn
        StartCoroutine(ExecuteNextTurnWithDelay(turnDelay, character, currentCharacterData.IsPlayer));
    }

    private IEnumerator ExecuteNextTurnWithDelay(float delay, Character character, bool isPlayer)
    {
        if (delay > 0)
        {
            GameDebugger.Instance.LogInfo($"Delaying turn for {character.Name} by {delay} seconds due to hostiles.");
            yield return new WaitForSeconds(delay);
        }

        // Reset ActionPoints and MovePoints after delay
        character.ActionPoints = character.MaxActionPoints;
        character.MovePoints = character.MaxMovePoints;

        if (isPlayer)
        {
            StartPlayerTurn();
        }
        else
        {
            isPlayerTurn = false;
            UpdateTurnProgress(character);
        }
    }

    private void StartPlayerTurn()
    {
        isPlayerTurn = true;
        GameDebugger.Instance.LogInfo("Player's turn started.");
        UIController.Instance.UpdateTurnOrderUI();

        // Reset the player's action and move points
        PlayerStats.Instance.ResetActionPoints();
        PlayerStats.Instance.ResetMovePoints();

        // Update the action menu or any relevant UI components for the player
        PlayerController.Instance.UpdateAdaptiveActionMenu();

        // Invoke the OnPlayerTurn event if any listeners are subscribed
        OnPlayerTurn?.Invoke();
    }

    public void PlayerTurnCompleted()
    {
        isPlayerTurn = false;
        PlayerStats.Instance.ResetActionPoints();
        PlayerStats.Instance.ResetMovePoints();
        currentTurnIndex++;
        ExecuteNextTurn();
    }

    private void AutoCompletePlayerTurn(Character playerCharacter)
    {
        // Log the auto-completion event
        GameDebugger.Instance.LogInfo($"Auto-completing turn for player {playerCharacter.Name}");

        // Optionally, simulate actions here (e.g., resting, defensive actions, etc.)
        // For now, we just end the turn

        // Perform any end-of-turn logic, such as reducing buffs/debuffs, resetting action/move points, etc.
        EndTurnForCharacter(playerCharacter);
    }

    private void UpdateTurnProgress(Character character)
    {
        int characterID = character.IInteractableID;
        string characterName = character.Name;
        GameDebugger.Instance.LogInfo($"Updating Turn Progress for Character {characterID} ({characterName})");

        // Check if the character is inactive
        if (!character.IsActive)
        {
            GameDebugger.Instance.LogInfo($"[TurnManager] Skipping {characterName} (ID: {characterID}) as they are not active.");
            EndTurnForCharacter(character);
            return;
        }

        // Check if the character's `CurrentNestedArea` is null
        if (character.CurrentNestedArea == null)
        {
            GameDebugger.Instance.LogWarning($"[TurnManager] Skipping {characterName} (ID: {characterID}) as their NestedArea is NULL.");
            EndTurnForCharacter(character);
            return;
        }

        // Ensure Player's NestedArea is also valid before comparison
        if (PlayerStats.Instance.CurrentPlayerCharacter.CurrentNestedArea == null)
        {
            GameDebugger.Instance.LogWarning($"[TurnManager] Player's NestedArea is NULL. Cannot compare locations.");
            EndTurnForCharacter(character);
            return;
        }

        // If the character is not in the active NestedArea, skip their turn
        if (character.CurrentNestedArea != PlayerStats.Instance.CurrentPlayerCharacter.CurrentNestedArea)
        {
            GameDebugger.Instance.LogInfo($"[TurnManager] Skipping {characterName} (ID: {characterID}) as they are not in the active NestedArea.");
            EndTurnForCharacter(character);
            return;
        }

        // Only execute actions if the character is in the correct area and is active
        if (characterTurnDataDict.ContainsKey(characterID))
        {
            character.ExecuteTurnActions();
            EndTurnForCharacter(character);
        }
        else
        {
            GameDebugger.Instance.LogWarning($"Character {characterID} ({characterName}) not found in turn data dictionary.");
        }
    }



    private void EndTurnForCharacter(Character character)
    {
        int characterID = character.IInteractableID;
        string characterName = character.Name;
        GameDebugger.Instance.LogInfo($"Ending Turn for Character {characterID} ({characterName})");

        if (characterTurnDataDict.ContainsKey(characterID))
        {
            // Set InTurn to false
            character.InTurn = false;

            character.OnTurnEnd();

            if (character.Health <= 0)
            {
                DeregisterCharacter(character);
            }

            currentTurnIndex++;
            ExecuteNextTurn(); // Move to the next character's turn
        }
        else
        {
            GameDebugger.Instance.LogWarning($"Character {characterID} ({characterName}) not found in turn data dictionary.");
        }
    }

    private void EndEntireTurn()
    {
        GameDebugger.Instance.LogInfo("Ending entire turn cycle.");
        currentTurnIndex = 0;

        if (GameManager.Instance.ActiveTurnManager)
        {
            if (characterTurnDataDict.Count > 0 && characterTurnDataDict.Values.Any(c => c.IsPlayer))
            {
                StartTurnCycle();
            }
            else
            {
                GameDebugger.Instance.LogInfo("Turn cycle will not restart due to missing characters or player.");
            }
        }
        else
        {
            GameDebugger.Instance.LogInfo("TurnManager is inactive. Turn cycle will not restart.");
        }
    }

    public void StartNextTurnCycle()
    {
        StartTurnCycle();
    }

    public bool IsCharacterRegistered(Character character)
    {
        return characterTurnDataDict.ContainsKey(character.IInteractableID);
    }

    public int GetRegisteredCharacterCount()
    {
        return characterTurnDataDict.Count;
    }

    public Dictionary<int, string> GetRegisteredCharacters()
    {
        Dictionary<int, string> registeredCharacters = new Dictionary<int, string>();

        foreach (var characterData in characterTurnDataDict.Values)
        {
            registeredCharacters.Add(characterData.Character.IInteractableID, characterData.Character.Name);
        }
        return registeredCharacters;
    }

    public List<Character> GetAllRegisteredCharacters()
    {
        return characterTurnDataDict.Values.Select(data => data.Character).ToList();
    }

    public void LogAllRegisteredCharacters()
    {
        var logMessage = "Current Registered Characters:\n";
        foreach (var characterData in characterTurnDataDict.Values)
        {
            logMessage += $"Character ID: {characterData.Character.IInteractableID}, Name: {characterData.Character.Name}\n";
        }

        GameDebugger.Instance.LogInfo(logMessage);
    }

    public void DeregisterAllCharacters()
    {
        GameDebugger.Instance.LogInfo("Deregistering all characters.");

        // End the current turn if a character is in turn
        if (currentTurnIndex < sortedCharacterList.Count)
        {
            var currentCharacterData = sortedCharacterList[currentTurnIndex];
            var character = currentCharacterData.Character;

            if (character != null && character.InTurn)
            {
                GameDebugger.Instance.LogInfo($"Character {character.IInteractableID} ({character.Name}) is currently in turn. Ending their turn before deregistering all characters.");
                EndTurnForCharacter(character);  // End the current character's turn
            }
        }

        // Clear all character data
        characterTurnDataDict.Clear();
        sortedCharacterList.Clear();
        currentTurnIndex = 0;

        GameDebugger.Instance.LogInfo("All characters have been deregistered and the turn cycle has ended.");
    }

    public void ValidateCharacterNestedAreas()
    {
        if (PlayerStats.Instance.CurrentNestedArea == null)
        {
            GameDebugger.Instance.LogWarning("ValidateCharacterNestedAreas: Player's CurrentNestedArea is null. Skipping validation.");
            return;
        }

        INestedArea playerNestedArea = PlayerStats.Instance.CurrentNestedArea;
        List<int> charactersToDeregister = new List<int>();

        foreach (var entry in characterTurnDataDict.Values)
        {
            if (entry.NestedArea != playerNestedArea)
            {
                GameDebugger.Instance.LogWarning($"Character {entry.Character.IInteractableID} ({entry.Character.Name}) is in the wrong NestedArea. Expected: {playerNestedArea}, Found: {entry.NestedArea}");

                // Either update the reference or remove the character
                if (entry.Character.IsInNestedArea)
                {
                    entry.UpdateNestedArea(playerNestedArea); // Update both TurnData and Character
                    GameDebugger.Instance.LogInfo($"Updated Character {entry.Character.IInteractableID} ({entry.Character.Name}) to correct NestedArea.");
                }
                else
                {
                    charactersToDeregister.Add(entry.Character.IInteractableID);
                }
            }
        }

        // Deregister characters that do not belong in the NestedArea
        foreach (int characterID in charactersToDeregister)
        {
            DeregisterCharacter(characterTurnDataDict[characterID].Character);
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
                validCharacters.Add($"[{character.IInteractableID}] {character.Name} - NestedAreaID: {character.CurrentNestedArea.NestedAreaID}");
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


    public List<string> GetTurnOrderList()
    {
        return sortedCharacterList.Select(c => $"{c.Character.Name} (Speed: {c.Speed})").ToList();
    }

    public bool AreThereHostileCharactersInArea(BaseNestedArea area)
    {
        if (area == null)
        {
            Debug.LogWarning("AreThereHostileCharactersInArea: Given area is null.");
            return false;
        }

        return area.GetAllCharactersInArea().Any(character => character.IsHostile);
    }

}

public class CharacterTurnData
{
    public Character Character { get; private set; }
    public INestedArea NestedArea { get; private set; }  // Store the character's NestedArea
    public string Name { get; private set; }
    public float Speed { get; private set; }
    public bool IsPlayer { get; private set; }
    public int PlacementID { get; private set; }
    public int CurrentTurnID { get; set; }

    public CharacterTurnData(Character character, float speed, bool isPlayer, int placementID)
    {
        Character = character;
        NestedArea = character.CurrentNestedArea; // Capture at registration
        Name = character.Name;
        Speed = speed;
        IsPlayer = isPlayer;
        PlacementID = placementID;
    }

    public void UpdateNestedArea(INestedArea newArea)
    {
        NestedArea = newArea;
        Character.CurrentNestedArea = newArea; // Ensure character is updated as well
    }
}

