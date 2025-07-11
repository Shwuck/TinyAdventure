using System.Collections.Generic;
using UnityEngine;

public class ExplorationTurnManager : MonoBehaviour
{
    private readonly List<Character> activeCharacters = new();

    public void RegisterCharacter(Character character)
    {
        if (character == null) return;
        if (!activeCharacters.Contains(character))
        {
            activeCharacters.Add(character);
            GameDebugger.Instance.LogInfo($"[ExplorationTurnManager] Registered character: {character.Name}");
        }
    }

    public void DeregisterCharacter(Character character)
    {
        if (character == null) return;
        if (activeCharacters.Remove(character))
        {
            GameDebugger.Instance.LogInfo($"[ExplorationTurnManager] Deregistered character: {character.Name}");
        }
    }

    public void ClearCharacters()
    {
        activeCharacters.Clear();
        GameDebugger.Instance.LogInfo("[ExplorationTurnManager] Cleared all characters.");
    }

    public void Suspend() => enabled = false;
    public void Resume() => enabled = true;

    public void Tick()
    {
        foreach (var character in activeCharacters)
        {
            if (character.IsActive && character != PlayerStats.Instance.CurrentPlayerCharacter)
            {
                character.ExecuteTurnActions(); // AI, reactions, etc.
            }
        }
        GameDebugger.Instance.LogInfo("[ExplorationTurnManager] Tick completed.");
    }
}
