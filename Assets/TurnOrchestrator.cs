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
			// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
			ResolveTurnManagersForDiagnostics();
			// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
			TurnDiagnosticsLogger.LogEvent("[BOOT]", "TurnOrchestrator.Awake instance set", $"combatManager.Assigned: {combatManager != null}\nexplorationTurnManager.Assigned: {explorationTurnManager != null}");
		}
		else
		{
			Trace("Awake: duplicate destroyed");
			// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
			TurnDiagnosticsLogger.LogWarning("TurnOrchestrator duplicate destroyed", "A duplicate TurnOrchestrator was destroyed during Awake.");
			Destroy(gameObject);
		}
	}

	private void OnApplicationQuit()
	{
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogShutdown("TurnOrchestrator.OnApplicationQuit");
	}

	// CODEXLOG001_TURNLIFECYCLE: temporary runtime manager-reference resolution and diagnostics.
	private void ResolveTurnManagersForDiagnostics()
	{
		bool combatOriginallyAssigned = combatManager != null;
		bool explorationOriginallyAssigned = explorationTurnManager != null;
		bool combatLookupAttempted = false;
		bool explorationLookupAttempted = false;
		bool combatLookupSucceeded = false;
		bool explorationLookupSucceeded = false;
		string combatObjectName = combatManager != null ? combatManager.gameObject.name : "NULL";
		string explorationObjectName = explorationTurnManager != null ? explorationTurnManager.gameObject.name : "NULL";

		if (combatManager == null)
		{
			combatLookupAttempted = true;
			var foundCombatManager = FindObjectOfType<CombatTurnManager>();
			if (foundCombatManager != null)
			{
				combatManager = foundCombatManager;
				combatLookupSucceeded = true;
				combatObjectName = foundCombatManager.gameObject.name;
				Debug.LogWarning($"{TurnDiagnosticsLogger.DiagnosticId} [BOOT] TurnOrchestrator fallback resolved CombatTurnManager from scene object '{combatObjectName}'.");
			}
			else
			{
				Debug.LogError($"{TurnDiagnosticsLogger.DiagnosticId} [BOOT] TurnOrchestrator could not resolve CombatTurnManager. combatManager remains null.");
			}
		}

		if (explorationTurnManager == null)
		{
			explorationLookupAttempted = true;
			var foundExplorationTurnManager = FindObjectOfType<ExplorationTurnManager>();
			if (foundExplorationTurnManager != null)
			{
				explorationTurnManager = foundExplorationTurnManager;
				explorationLookupSucceeded = true;
				explorationObjectName = foundExplorationTurnManager.gameObject.name;
				Debug.LogWarning($"{TurnDiagnosticsLogger.DiagnosticId} [BOOT] TurnOrchestrator fallback resolved ExplorationTurnManager from scene object '{explorationObjectName}'.");
			}
			else
			{
				Debug.LogError($"{TurnDiagnosticsLogger.DiagnosticId} [BOOT] TurnOrchestrator could not resolve ExplorationTurnManager. explorationTurnManager remains null.");
			}
		}

		string details =
			$"combatManager.OriginallyAssigned: {combatOriginallyAssigned}\n" +
			$"explorationTurnManager.OriginallyAssigned: {explorationOriginallyAssigned}\n" +
			$"combatManager.FallbackLookupAttempted: {combatLookupAttempted}\n" +
			$"explorationTurnManager.FallbackLookupAttempted: {explorationLookupAttempted}\n" +
			$"combatManager.FallbackLookupSucceeded: {combatLookupSucceeded}\n" +
			$"explorationTurnManager.FallbackLookupSucceeded: {explorationLookupSucceeded}\n" +
			$"combatManager.FinalAssigned: {combatManager != null}\n" +
			$"explorationTurnManager.FinalAssigned: {explorationTurnManager != null}\n" +
			$"combatManager.SceneObjectName: {combatObjectName}\n" +
			$"explorationTurnManager.SceneObjectName: {explorationObjectName}";

		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[BOOT]", "TurnOrchestrator manager reference resolution", details);
	}


    #region Area Entry

	public void EnterMainMap()
	{
		Trace("EnterMainMap: begin");
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[AREA EXIT]", "TurnOrchestrator.EnterMainMap begin");
		CurrentContext = TurnContext.MainMap;
		explorationTurnManager.ClearCharacters();
		combatManager.DeregisterAllCharacters();
		allCharacters.Clear();
		Trace("EnterMainMap: cleared turn managers and registry");
		GameDebugger.Instance.LogInfo("Entered Main Map. Turn logic disabled.");
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("TurnOrchestrator.EnterMainMap completed");
	}

	public void EnterNestedArea(INestedArea area)
	{
		EnterExplorationArea(area, PlayerStats.Instance?.CurrentPlayerCharacter);
	}

	public void EnterExplorationArea(INestedArea area, Character playerCharacter)
	{
		Trace($"EnterExplorationArea: area={area?.NestedAreaID}");
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[AREA ENTRY]", "TurnOrchestrator.EnterExplorationArea begin",
			$"Area: {area?.Name} ({area?.NestedAreaID})\n" +
			$"Context before exploration entry: {CurrentContext}\n" +
			$"combatManager.Assigned: {combatManager != null}\n" +
			$"explorationTurnManager.Assigned: {explorationTurnManager != null}");

		if (area == null)
		{
			GameDebugger.Instance.LogError("TurnOrchestrator.EnterExplorationArea: area is null.");
			// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
			TurnDiagnosticsLogger.LogWarning("TurnOrchestrator.EnterExplorationArea null area", "Cannot enter exploration without a nested area.");
			return;
		}

		if (playerCharacter == null)
		{
			GameDebugger.Instance.LogError("TurnOrchestrator.EnterExplorationArea: playerCharacter is null.");
			// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
			TurnDiagnosticsLogger.LogWarning("TurnOrchestrator.EnterExplorationArea null player", "Cannot enter exploration without a player character.");
			return;
		}

		if (combatManager == null || explorationTurnManager == null)
		{
			ResolveTurnManagersForDiagnostics();
		}

		if (combatManager == null || explorationTurnManager == null)
		{
			GameDebugger.Instance.LogError("TurnOrchestrator.EnterExplorationArea: required turn managers are missing.");
			// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
			TurnDiagnosticsLogger.LogWarning("TurnOrchestrator.EnterExplorationArea missing manager",
				$"combatManager.Assigned: {combatManager != null}\nexplorationTurnManager.Assigned: {explorationTurnManager != null}");
			return;
		}

		CurrentContext = TurnContext.Exploration;
		explorationTurnManager.Resume();
		explorationTurnManager.ClearCharacters();
		combatManager.DeregisterAllCharacters();
		allCharacters.Clear();
		Trace("EnterExplorationArea: managers cleared; registering player and occupants");

		RegisterCharacter(playerCharacter);
		bool playerRegisteredInActiveManager = DiagnosticIsCharacterRegisteredInActiveManager(playerCharacter);

		int occupantsConsidered = 0;
		int occupantsRegistered = 0;
		int duplicatePlayerSkipped = 0;
		int duplicateOccupantsSkipped = 0;
		int wrongAreaOccupantsSkipped = 0;
		int inactiveOccupantsSkipped = 0;
		int deadButRegistered = 0;
		HashSet<int> registeredOccupantIds = new HashSet<int> { playerCharacter.IInteractableID };
		var occupants = area.GetAllCharactersInArea();

		foreach (var character in occupants)
		{
			occupantsConsidered++;

			if (character == null)
			{
				// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
				TurnDiagnosticsLogger.LogWarning("TurnOrchestrator.EnterExplorationArea null occupant", $"Area: {area.Name} ({area.NestedAreaID})");
				continue;
			}

			if (character == playerCharacter)
			{
				duplicatePlayerSkipped++;
				continue;
			}

			if (!registeredOccupantIds.Add(character.IInteractableID))
			{
				duplicateOccupantsSkipped++;
				// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
				TurnDiagnosticsLogger.LogWarning("TurnOrchestrator.EnterExplorationArea duplicate occupant skipped",
					$"Area: {area.Name} ({area.NestedAreaID})\nDuplicateID: {character.IInteractableID}", character);
				continue;
			}

			if (!character.IsActive)
			{
				inactiveOccupantsSkipped++;
				// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
				TurnDiagnosticsLogger.LogWarning("TurnOrchestrator.EnterExplorationArea inactive occupant skipped",
					$"Area: {area.Name} ({area.NestedAreaID})", character);
				continue;
			}

			if (!character.IsInNestedArea || character.CurrentNestedArea != area)
			{
				wrongAreaOccupantsSkipped++;
				// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
				TurnDiagnosticsLogger.LogWarning("TurnOrchestrator.EnterExplorationArea wrong-area occupant skipped",
					$"Area: {area.Name} ({area.NestedAreaID})", character);
				continue;
			}

			if (!character.IsAlive)
			{
				deadButRegistered++;
				// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
				TurnDiagnosticsLogger.LogWarning("TurnOrchestrator.EnterExplorationArea registering IsAlive false occupant",
					"IsAlive is currently not reliable enough to filter here; registering for diagnostics continuity.", character);
			}

			RegisterCharacter(character);
			occupantsRegistered++;
		}

		GameDebugger.Instance.LogInfo(
			$"Entered NestedArea {area.NestedAreaID} in Exploration mode. Player registered={playerRegisteredInActiveManager}, occupants registered={occupantsRegistered}/{occupantsConsidered}.");

		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("TurnOrchestrator.EnterExplorationArea setup completed",
			$"Context after exploration setup: {CurrentContext}\n" +
			$"Player registered in active manager: {playerRegisteredInActiveManager}\n" +
			$"Occupants considered: {occupantsConsidered}\n" +
			$"Occupants registered: {occupantsRegistered}\n" +
			$"Duplicate player registration skipped: {duplicatePlayerSkipped}\n" +
			$"Duplicate occupant IDs skipped: {duplicateOccupantsSkipped}\n" +
			$"Wrong-area occupants skipped: {wrongAreaOccupantsSkipped}\n" +
			$"Inactive occupants skipped: {inactiveOccupantsSkipped}\n" +
			$"Dead-but-registered occupants: {deadButRegistered}\n" +
			$"allCharacters.Count: {allCharacters.Count}\n" +
			$"Exploration.Count: {DiagnosticExplorationRegisteredCount}\n" +
			$"Combat.Count: {DiagnosticCombatRegisteredCount}");

		area.UpdateHostileAreaStatus();
		bool hasHostiles = area.IsHostileArea;
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[CONTEXT UPDATE]", "TurnOrchestrator.EnterExplorationArea hostility check",
			$"HasHostiles: {hasHostiles}\nContext before hostility update: {CurrentContext}");
		TryUpdateTurnContext();

		if (CurrentContext == TurnContext.Exploration)
		{
			Trace("EnterExplorationArea -> StartTurnCycle");
			StartTurnCycle();
		}

		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("TurnOrchestrator.EnterExplorationArea completed",
			$"Final context: {CurrentContext}\n" +
			$"HasHostiles: {hasHostiles}\n" +
			$"Player registered in active manager: {DiagnosticIsCharacterRegisteredInActiveManager(playerCharacter)}\n" +
			$"allCharacters.Count: {allCharacters.Count}\n" +
			$"Exploration.Count: {DiagnosticExplorationRegisteredCount}\n" +
			$"Combat.Count: {DiagnosticCombatRegisteredCount}");
		// CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic.
		NestedMapDebugger.LogSnapshot(area, "SNAPSHOT_ENTER_AREA EnterExplorationArea completed");
	}


    #endregion

    #region Registration

	public void RegisterCharacter(Character character)
	{
		Trace($"RegisterCharacter: {character?.Name ?? "NULL"}");
		if (character == null)
		{
			GameDebugger.Instance.LogError("Attempted to register NULL character.");
			// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
			TurnDiagnosticsLogger.LogWarning("TurnOrchestrator.RegisterCharacter null character", "Attempted to register a null character.");
			return;
		}

		bool added = false;
		if (!allCharacters.Contains(character))
		{
			allCharacters.Add(character);
			added = true;
		}
		Trace($"RegisterCharacter: {(added ? "added" : "already present")} total={allCharacters.Count}");
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogRegistration("TurnOrchestrator.allCharacters", character, character == PlayerStats.Instance?.CurrentPlayerCharacter);

		bool isPlayer = character == PlayerStats.Instance?.CurrentPlayerCharacter;

		switch (CurrentContext)
		{
			case TurnContext.Exploration:
				Trace("RegisterCharacter→ExplorationTurnManager.RegisterCharacter");
				explorationTurnManager.RegisterCharacter(character, isPlayer);
				break;
			case TurnContext.Combat:
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
			// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
			TurnDiagnosticsLogger.LogWarning("TurnOrchestrator.DeregisterCharacter null character", "Attempted to deregister a null character.");
			return;
		}

		allCharacters.Remove(character);
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogDeregistration("TurnOrchestrator.allCharacters", character);

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
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[CONTEXT UPDATE]", "TurnOrchestrator.TryUpdateTurnContext begin", $"Area: {area?.Name} ({area?.NestedAreaID})\nHasHostiles: {hasHostiles}");

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
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("TurnOrchestrator.TryUpdateTurnContext completed", $"HasHostiles: {hasHostiles}");
	}

	private void SwitchToCombatMode()
	{
		Trace("SwitchToCombatMode: begin");
		if (CurrentContext == TurnContext.Combat) { Trace("SwitchToCombatMode: already in Combat"); return; }
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[COMBAT START]", "TurnOrchestrator.SwitchToCombatMode begin");

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
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("TurnOrchestrator.SwitchToCombatMode completed");
	}

	private void SwitchToExplorationMode()
	{
		Trace("SwitchToExplorationMode: begin");
		if (CurrentContext == TurnContext.Exploration) { Trace("SwitchToExplorationMode: already in Exploration"); return; }
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[COMBAT END]", "TurnOrchestrator.SwitchToExplorationMode begin");

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
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("TurnOrchestrator.SwitchToExplorationMode completed");
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

		#region CODEXLOG001_TURNLIFECYCLE Diagnostics

		// CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
		public bool DiagnosticCombatManagerAssigned => combatManager != null;

		// CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
		public bool DiagnosticExplorationTurnManagerAssigned => explorationTurnManager != null;

		// CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
		public int DiagnosticAllCharactersCount => allCharacters.Count;

		// CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
		public int DiagnosticExplorationRegisteredCount => explorationTurnManager != null ? explorationTurnManager.DiagnosticRegisteredCount : -1;

		// CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
		public int DiagnosticCombatRegisteredCount => combatManager != null ? combatManager.DiagnosticRegisteredCount : -1;

		// CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
		public List<Character> DiagnosticGetAllCharactersSnapshot()
		{
			return allCharacters.Where(character => character != null).ToList();
		}

		// CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
		public List<Character> DiagnosticGetExplorationCharactersSnapshot()
		{
			return explorationTurnManager != null
				? explorationTurnManager.DiagnosticGetRegisteredCharactersSnapshot()
				: new List<Character>();
		}

		// CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
		public List<Character> DiagnosticGetCombatCharactersSnapshot()
		{
			return combatManager != null
				? combatManager.DiagnosticGetRegisteredCharactersSnapshot()
				: new List<Character>();
		}

		// CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
		public bool DiagnosticIsCharacterRegisteredInActiveManager(Character character)
		{
			if (character == null) return false;

			if (CurrentContext == TurnContext.Combat)
				return combatManager != null && combatManager.DiagnosticContainsCharacter(character);

			if (CurrentContext == TurnContext.Exploration)
				return explorationTurnManager != null && explorationTurnManager.DiagnosticContainsCharacter(character);

			return false;
		}

		#endregion
	
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
