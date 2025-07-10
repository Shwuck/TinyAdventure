using System.Collections.Generic;
using UnityEngine;

public class CharacterActionManager : MonoBehaviour
{
    public static CharacterActionManager Instance { get; private set; }

    private Dictionary<int, string> actionLog = new Dictionary<int, string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Check if a given action has already been taken by another character.
    public bool IsActionTaken(string action)
    {
        return actionLog.ContainsValue(action);
    }

    // Log an action for a character.
    public void LogAction(Character character, string action)
    {
        actionLog[character.IInteractableID] = action;
    }

    // Remove a character's action after it is completed or canceled.
    public void RemoveAction(Character character)
    {
        actionLog.Remove(character.IInteractableID);
    }

    // Get the currently planned action of a character.
    public string GetCharacterAction(Character character)
    {
        return actionLog.TryGetValue(character.IInteractableID, out string action) ? action : null;
    }

    // Clear all actions at the end of a turn cycle.
    public void ClearAllActions()
    {
        actionLog.Clear();
    }
}
