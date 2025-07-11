using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum TurnContext
{
    MainMap,
    Exploration,
    Combat
}

public class TurnOrchestrator : MonoBehaviour
{
    public static TurnOrchestrator Instance { get; private set; }

    public TurnContext CurrentContext { get; private set; } = TurnContext.MainMap;

    private readonly List<Character> allCharacters = new List<Character>();

    [SerializeField] private CombatTurnManager combatManager;
    [SerializeField] private ExplorationTurnManager explorationTurnManager;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            GameDebugger.Instance.LogInfo("TurnOrchestrator initialized.");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #region Area Entry

    public void EnterMainMap()
    {
        CurrentContext = TurnContext.MainMap;
        explorationTurnManager.ClearCharacters();
        combatManager.DeregisterAllCharacters();
        allCharacters.Clear();
        GameDebugger.Instance.LogInfo("Entered Main Map. Turn logic disabled.");
    }

    public void EnterNestedArea(INestedArea area)
    {
        CurrentContext = TurnContext.Exploration;
        explorationTurnManager.ClearCharacters();
        combatManager.DeregisterAllCharacters();
        allCharacters.Clear();

        foreach (var character in area.GetAllCharactersInArea())
        {
            RegisterCharacter(character);
        }

        GameDebugger.Instance.LogInfo($"Entered NestedArea {area.NestedAreaID} in Exploration mode.");
    }

    #endregion

    #region Registration

    public void RegisterCharacter(Character character)
    {
        if (character == null)
        {
            GameDebugger.Instance.LogError("Attempted to register NULL character.");
            return;
        }

        if (!allCharacters.Contains(character))
        {
            allCharacters.Add(character);
        }

        switch (CurrentContext)
        {
            case TurnContext.Exploration:
                explorationTurnManager.RegisterCharacter(character);
                break;
            case TurnContext.Combat:
                combatManager.RegisterCharacter(character, character == PlayerStats.Instance.CurrentPlayerCharacter);
                break;
        }
    }

    public void DeregisterCharacter(Character character)
    {
        if (character == null)
        {
            GameDebugger.Instance.LogError("Attempted to deregister NULL character.");
            return;
        }

        allCharacters.Remove(character);

        switch (CurrentContext)
        {
            case TurnContext.Exploration:
                explorationTurnManager.DeregisterCharacter(character);
                break;
            case TurnContext.Combat:
                combatManager.DeregisterCharacter(character);
                break;
        }
    }

    public List<Character> GetAllRegisteredCharacters() => allCharacters;

    #endregion

    #region Context Transitions

    public void TryUpdateTurnContext()
    {
        var area = PlayerStats.Instance.CurrentPlayerCharacter?.CurrentNestedArea;
        bool hasHostiles = area?.IsHostileArea ?? false;

        if (CurrentContext == TurnContext.Exploration && hasHostiles)
        {
            SwitchToCombatMode();
        }
        else if (CurrentContext == TurnContext.Combat && !hasHostiles)
        {
            SwitchToExplorationMode();
        }
    }

    private void SwitchToCombatMode()
    {
        if (CurrentContext == TurnContext.Combat) return;

        CurrentContext = TurnContext.Combat;
        explorationTurnManager.Suspend();
        combatManager.DeregisterAllCharacters();

        foreach (var character in allCharacters)
        {
            if (character.IsInNestedArea && character.CurrentNestedArea == PlayerStats.Instance.CurrentNestedArea)
            {
                combatManager.RegisterCharacter(character, character == PlayerStats.Instance.CurrentPlayerCharacter);
            }
        }

        combatManager.StartTurnCycle();
        GameDebugger.Instance.LogInfo("Switched to Combat mode.");
    }

    private void SwitchToExplorationMode()
    {
        if (CurrentContext == TurnContext.Exploration) return;

        CurrentContext = TurnContext.Exploration;
        combatManager.DeregisterAllCharacters();
        explorationTurnManager.ClearCharacters();

        foreach (var character in allCharacters)
        {
            if (character.IsInNestedArea && character.CurrentNestedArea == PlayerStats.Instance.CurrentNestedArea)
            {
                explorationTurnManager.RegisterCharacter(character);
            }
        }

        explorationTurnManager.Resume();
        GameDebugger.Instance.LogInfo("Switched to Exploration mode.");
    }

    #endregion

    #region Scene Management Utilities

    public void ReevaluateCharactersInScene()
    {
        var activeArea = PlayerStats.Instance.CurrentNestedArea;
        if (activeArea == null) return;

        allCharacters.Clear();
        allCharacters.AddRange(activeArea.GetAllCharactersInArea());
        GameDebugger.Instance.LogInfo($"Reevaluated characters in scene. Total: {allCharacters.Count}");

        switch (CurrentContext)
        {
            case TurnContext.Exploration:
                explorationTurnManager.ClearCharacters();
                foreach (var character in allCharacters)
                {
                    explorationTurnManager.RegisterCharacter(character);
                }
                break;

            case TurnContext.Combat:
                combatManager.DeregisterAllCharacters();
                foreach (var character in allCharacters)
                {
                    combatManager.RegisterCharacter(character, character == PlayerStats.Instance.CurrentPlayerCharacter);
                }
                break;
        }
    }

    #endregion
}
