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
		Trace("Awake: init attempt");
		if (Instance == null)
		{
			Instance = this;
			DontDestroyOnLoad(this.gameObject);
			GameDebugger.Instance.LogInfo("TurnOrchestrator initialized.");
			Trace("Awake: instance set");
		}
		else
		{
			Trace("Awake: duplicate destroyed");
			Destroy(gameObject);
		}
	}


    #region Area Entry

	public void EnterMainMap()
	{
		Trace("EnterMainMap: begin");
		CurrentContext = TurnContext.MainMap;
		explorationTurnManager.ClearCharacters();
		combatManager.DeregisterAllCharacters();
		allCharacters.Clear();
		Trace("EnterMainMap: cleared turn managers and registry");
		GameDebugger.Instance.LogInfo("Entered Main Map. Turn logic disabled.");
	}

	public void EnterNestedArea(INestedArea area)
	{
		Trace($"EnterNestedArea: area={area?.NestedAreaID}");
		CurrentContext = TurnContext.Exploration;
		explorationTurnManager.ClearCharacters();
		combatManager.DeregisterAllCharacters();
		allCharacters.Clear();
		Trace("EnterNestedArea: managers cleared; registering characters");

		foreach (var character in area.GetAllCharactersInArea())
		{
			RegisterCharacter(character);
		}

		Trace($"EnterNestedArea: registered={allCharacters.Count}");
		GameDebugger.Instance.LogInfo($"Entered NestedArea {area.NestedAreaID} in Exploration mode.");
	}


    #endregion

    #region Registration

	public void RegisterCharacter(Character character)
	{
		Trace($"RegisterCharacter: {character?.Name ?? "NULL"}");
		if (character == null)
		{
			GameDebugger.Instance.LogError("Attempted to register NULL character.");
			return;
		}

		bool added = false;
		if (!allCharacters.Contains(character))
		{
			allCharacters.Add(character);
			added = true;
		}
		Trace($"RegisterCharacter: {(added ? "added" : "already present")} total={allCharacters.Count}");

		switch (CurrentContext)
		{
			case TurnContext.Exploration:
				Trace("RegisterCharacter→ExplorationTurnManager.RegisterCharacter");
				explorationTurnManager.RegisterCharacter(character);
				break;
			case TurnContext.Combat:
				bool isPlayer = character == PlayerStats.Instance.CurrentPlayerCharacter;
				Trace($"RegisterCharacter→CombatTurnManager.RegisterCharacter isPlayer={isPlayer}");
				combatManager.RegisterCharacter(character, isPlayer);
				break;
		}
	}

	public void DeregisterCharacter(Character character)
	{
		Trace($"DeregisterCharacter: {character?.Name ?? "NULL"}");
		if (character == null)
		{
			GameDebugger.Instance.LogError("Attempted to deregister NULL character.");
			return;
		}

		allCharacters.Remove(character);

		switch (CurrentContext)
		{
			case TurnContext.Exploration:
				Trace("DeregisterCharacter→ExplorationTurnManager.DeregisterCharacter");
				explorationTurnManager.DeregisterCharacter(character);
				break;
			case TurnContext.Combat:
				Trace("DeregisterCharacter→CombatTurnManager.DeregisterCharacter");
				combatManager.DeregisterCharacter(character);
				break;
		}
	}


    public List<Character> GetAllRegisteredCharacters() => allCharacters;

    #endregion

    #region Context Transitions

	public void TryUpdateTurnContext()
	{
		Trace("TryUpdateTurnContext: begin");
		var area = PlayerStats.Instance.CurrentPlayerCharacter?.CurrentNestedArea;
		bool hasHostiles = area?.IsHostileArea ?? false;
		Trace($"TryUpdateTurnContext: hasHostiles={hasHostiles}");

		if (CurrentContext == TurnContext.Exploration && hasHostiles)
		{
			Trace("TryUpdateTurnContext→SwitchToCombatMode");
			SwitchToCombatMode();
		}
		else if (CurrentContext == TurnContext.Combat && !hasHostiles)
		{
			Trace("TryUpdateTurnContext→SwitchToExplorationMode");
			SwitchToExplorationMode();
		}
		else
		{
			Trace("TryUpdateTurnContext: no change");
		}
	}

	private void SwitchToCombatMode()
	{
		Trace("SwitchToCombatMode: begin");
		if (CurrentContext == TurnContext.Combat) { Trace("SwitchToCombatMode: already in Combat"); return; }

		CurrentContext = TurnContext.Combat;
		explorationTurnManager.Suspend();
		combatManager.DeregisterAllCharacters();

		foreach (var character in allCharacters)
		{
			if (character.IsInNestedArea && character.CurrentNestedArea == PlayerStats.Instance.CurrentNestedArea)
			{
				bool isPlayer = character == PlayerStats.Instance.CurrentPlayerCharacter;
				Trace($"SwitchToCombatMode: register {character.Name} isPlayer={isPlayer}");
				combatManager.RegisterCharacter(character, isPlayer);
			}
		}

		Trace("SwitchToCombatMode→CombatTurnManager.StartTurnCycle");
		combatManager.StartTurnCycle();
		GameDebugger.Instance.LogInfo("Switched to Combat mode.");
	}

	private void SwitchToExplorationMode()
	{
		Trace("SwitchToExplorationMode: begin");
		if (CurrentContext == TurnContext.Exploration) { Trace("SwitchToExplorationMode: already in Exploration"); return; }

		CurrentContext = TurnContext.Exploration;
		combatManager.DeregisterAllCharacters();
		explorationTurnManager.ClearCharacters();

		foreach (var character in allCharacters)
		{
			if (character.IsInNestedArea && character.CurrentNestedArea == PlayerStats.Instance.CurrentNestedArea)
			{
				Trace($"SwitchToExplorationMode: register {character.Name}");
				explorationTurnManager.RegisterCharacter(character);
			}
		}

		Trace("SwitchToExplorationMode→ExplorationTurnManager.Resume");
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
	
	#region Helper Methods
	
			public void ValidateCharacterNestedAreas() {
			if (CurrentContext == TurnContext.Combat)
				combatManager.ValidateCharacterNestedAreas();
			else if (CurrentContext == TurnContext.Exploration)
				explorationTurnManager.ValidateCharacterNestedAreas();
		}

		public void LogAllRegisteredCharacters() {
			if (CurrentContext == TurnContext.Combat)
				combatManager.LogAllRegisteredCharacters();
			else if (CurrentContext == TurnContext.Exploration)
				explorationTurnManager.LogAllRegisteredCharacters();
		}

		public Dictionary<int,string> GetRegisteredCharacters() {
			if (CurrentContext == TurnContext.Combat)
				return combatManager.GetRegisteredCharacters();
			else if (CurrentContext == TurnContext.Exploration)
				return explorationTurnManager.GetRegisteredCharacters();
			return new Dictionary<int,string>();
		}

		public void DeregisterCharactersInNestedArea(INestedArea area) {
			if (CurrentContext == TurnContext.Combat)
				combatManager.DeregisterCharactersInNestedArea(area);
			else if (CurrentContext == TurnContext.Exploration)
				explorationTurnManager.DeregisterCharactersInNestedArea(area);
		}

		public bool IsCharacterRegistered(Character c)
		{
			if (c == null) return false;

			if (CurrentContext == TurnContext.Combat)
				return combatManager.IsCharacterRegistered(c);

			if (CurrentContext == TurnContext.Exploration)
				return explorationTurnManager.IsCharacterRegistered(c);

			// In MainMap or any fallback state, at least respect the global list
			return allCharacters.Contains(c);
		}

		public void StartTurnCycle() {
			if (CurrentContext == TurnContext.Combat)
				combatManager.StartTurnCycle();
			else if (CurrentContext == TurnContext.Exploration)
				explorationTurnManager.StartTurnCycle();
		}

		public void DeregisterAllCharacters()
		{
			if (CurrentContext == TurnContext.Combat)
				combatManager.DeregisterAllCharacters();
			else if (CurrentContext == TurnContext.Exploration)
				explorationTurnManager.DeregisterAllCharacters();

			allCharacters.Clear();
			Trace("DeregisterAllCharacters: global list cleared");
		}

		public void PlayerTurnCompleted() {
			if (CurrentContext == TurnContext.Combat)
				combatManager.PlayerTurnCompleted();
			else if (CurrentContext == TurnContext.Exploration)
				explorationTurnManager.PlayerTurnCompleted();
		}

			public List<string> GetTurnOrderList()
		{
			if (CurrentContext == TurnContext.Combat)
				return combatManager.GetTurnOrderList();

			if (CurrentContext == TurnContext.Exploration)
				return explorationTurnManager.GetRegisteredCharacters()
					.Select(kv => kv.Value + " (Exploration)").ToList();

			return new List<string> { "No active turn order in MainMap." };
		}

		private void Trace(string msg)
		{
			CallTrace.Mark(this, $"[{CurrentContext}] {msg}");
		}
	
	#endregion	
	
	#region Turn Audit

// per-cycle ledger
private readonly Dictionary<int, int> auditHits = new();   // CharacterID -> times seen this cycle
private readonly List<int> auditOrder = new();             // actual order seen this cycle
private List<int> auditExpected = new();                   // expected order at start of cycle
private int auditCycleId = 0;

public void AuditBeginCycle(List<Character> ordered)
{
    auditCycleId++;
    auditHits.Clear();
    auditOrder.Clear();
    auditExpected = ordered?.Select(c => c.IInteractableID).ToList() ?? new List<int>();


    GameDebugger.Instance.LogInfo($"[TurnAudit] Cycle #{auditCycleId} started. Expected count: {auditExpected.Count}");
}

public void AuditMarkTurn(Character c, string stage = "Start")
{
    if (c == null) { GameDebugger.Instance.LogWarning("[TurnAudit] Null character reported."); return; }
	
	// Optional bridge to CallTrace for cross-linking
	CallTrace.Mark(TurnOrchestrator.Instance, $"TurnAudit {stage}: {c.Name} [{c.IInteractableID}]");

    int id = c.IInteractableID;
    if (!auditHits.ContainsKey(id)) auditHits[id] = 0;
    auditHits[id]++;
    auditOrder.Add(id);

    // Optional order check (only on first hit of this character)
    int appearance = auditHits[id];
    if (appearance == 1 && auditExpected.Count == auditOrder.Count)
    {
        int expectedId = auditExpected[auditOrder.Count - 1];
        if (expectedId != id)
        {
            GameDebugger.Instance.LogWarning(
                $"[TurnAudit] Order mismatch at position {auditOrder.Count}: expected {expectedId}, got {id} ({c.Name}).");
        }
    }

    GameDebugger.Instance.LogInfo($"[TurnAudit] {stage}: {c.Name} [{id}] (hit #{auditHits[id]}).");
}

public void AuditEndCycle()
{
    var missing = auditExpected.Except(auditHits.Keys).ToList();
    var extras  = auditHits.Keys.Except(auditExpected).ToList();
    string orderStr = string.Join(" -> ", auditOrder);

    if (missing.Count == 0 && extras.Count == 0)
    {
        GameDebugger.Instance.LogInfo($"[TurnAudit] Cycle #{auditCycleId} complete. All {auditExpected.Count} acted. Order: {orderStr}");
    }
    else
    {
        if (missing.Count > 0)
            GameDebugger.Instance.LogWarning($"[TurnAudit] Missing this cycle ({missing.Count}): {string.Join(", ", missing)}");
        if (extras.Count > 0)
            GameDebugger.Instance.LogWarning($"[TurnAudit] Unexpected actors ({extras.Count}): {string.Join(", ", extras)}");
        GameDebugger.Instance.LogInfo($"[TurnAudit] Actual order: {orderStr}");
    }
}

#endregion

}
