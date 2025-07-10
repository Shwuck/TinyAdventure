using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnOrchestrator : MonoBehaviour
{
    public static TurnOrchestrator Instance { get; private set; }

    public enum TurnMode { Exploration, Combat }
    public TurnMode CurrentMode { get; private set; } = TurnMode.Exploration;

    private HashSet<Character> allCharacters = new(); // Global registry

    [SerializeField] private ExplorationTurnManager explorationTurnManager;
    [SerializeField] private CombatManager combatManager;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterCharacter(Character character)
    {
        if (character == null) return;
        if (!allCharacters.Contains(character))
        {
            allCharacters.Add(character);
            GameDebugger.Instance.LogInfo($"[Orchestrator] Registered new character: {character.Name} (ID: {character.IInteractableID})");
        }

        if (CurrentMode == TurnMode.Combat)
        {
            if (!combatManager.IsCharacterRegistered(character))
                combatManager.RegisterCharacter(character);
        }
        else
        {
            if (!explorationTurnManager.IsCharacterRegistered(character))
                explorationTurnManager.RegisterCharacter(character);
        }
    }

    public void DeregisterCharacter(Character character)
    {
        if (character == null) return;

        allCharacters.Remove(character);
        explorationTurnManager.DeregisterCharacter(character);
        combatManager.DeregisterCharacter(character);

        GameDebugger.Instance.LogInfo($"[Orchestrator] Deregistered character: {character.Name} (ID: {character.IInteractableID})");
    }

    public void SwitchToCombatMode()
    {
        CurrentMode = TurnMode.Combat;

        explorationTurnManager.Suspend();
        combatManager.ClearCharacters();

        foreach (var character in allCharacters)
        {
            if (character.IsActive && character.IsInNestedArea)
                combatManager.RegisterCharacter(character);
        }

        combatManager.BeginCombat();
    }

    public void SwitchToExplorationMode()
    {
        CurrentMode = TurnMode.Exploration;

        combatManager.ClearCharacters();
        explorationTurnManager.ClearCharacters();

        foreach (var character in allCharacters)
        {
            if (character.IsActive && character.IsInNestedArea)
                explorationTurnManager.RegisterCharacter(character);
        }

        explorationTurnManager.Resume();
    }

    public List<Character> GetAllCharactersInScene() => allCharacters.ToList();
}
