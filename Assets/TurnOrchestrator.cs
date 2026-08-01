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
    public bool IsActionResolutionActive => actionResolutionDepth > 0;
    public bool HasPendingContextTransition => pendingContextTransition.HasValue;
    public int CurrentActionResolutionId => currentActionResolutionId;

    // Persistent roster for the currently loaded area/nested area.
    private readonly List<Character> currentAreaRoster = new List<Character>();

    private struct PendingContextTransition
    {
        public TurnContext TargetContext;
        public string Reason;
        public string Source;
        public int RequestId;
    }

    [SerializeField] private CombatTurnManager combatManager;
    [SerializeField] private ExplorationTurnManager explorationTurnManager;

    private int actionResolutionDepth = 0;
    private int turnSequenceDepth = 0;
    private int currentActionResolutionId = 0;
    private bool isApplyingContextTransition = false;
    private PendingContextTransition? pendingContextTransition;

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

	private void Update()
	{
		TryApplyPendingContextTransitionIfSafe("TurnOrchestrator.Update");
	}

    public void BeginActionResolution(string source)
    {
        actionResolutionDepth++;
        currentActionResolutionId++;

        TurnDiagnosticsLogger.LogEvent("[ACTION RESOLUTION]", "TurnOrchestrator.BeginActionResolution",
            $"Source: {source}\n" +
            $"ActionResolutionId: {currentActionResolutionId}\n" +
            $"ActionResolutionDepth: {actionResolutionDepth}\n" +
            $"PendingTransition: {DescribePendingContextTransition()}");
    }

    public void EndActionResolution(string source)
    {
        if (actionResolutionDepth > 0)
        {
            actionResolutionDepth--;
        }

        TurnDiagnosticsLogger.LogEvent("[ACTION RESOLUTION]", "TurnOrchestrator.EndActionResolution",
            $"Source: {source}\n" +
            $"ActionResolutionDepth: {actionResolutionDepth}\n" +
            $"PendingTransition: {DescribePendingContextTransition()}");

        TryApplyPendingContextTransitionIfSafe(source);
    }

    public void BeginTurnSequence(string source)
    {
        turnSequenceDepth++;
        TurnDiagnosticsLogger.LogEvent("[TURN SEQUENCE]", "TurnOrchestrator.BeginTurnSequence",
            $"Source: {source}\n" +
            $"TurnSequenceDepth: {turnSequenceDepth}\n" +
            $"Context: {CurrentContext}");
    }

    public void EndTurnSequence(string source)
    {
        if (turnSequenceDepth > 0)
        {
            turnSequenceDepth--;
        }

        TurnDiagnosticsLogger.LogEvent("[TURN SEQUENCE]", "TurnOrchestrator.EndTurnSequence",
            $"Source: {source}\n" +
            $"TurnSequenceDepth: {turnSequenceDepth}\n" +
            $"Context: {CurrentContext}");

        TryApplyPendingContextTransitionIfSafe(source);
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

    private string DescribePendingContextTransition()
    {
        if (!pendingContextTransition.HasValue)
        {
            return "None";
        }

        PendingContextTransition transition = pendingContextTransition.Value;
        return $"{transition.TargetContext} (RequestId={transition.RequestId}, Source={transition.Source}, Reason={transition.Reason})";
    }

    private static void AddActiveCharactersToRoster(IEnumerable<Character> source, INestedArea area, HashSet<int> seen, List<Character> roster)
    {
        if (source == null)
        {
            return;
        }

        foreach (Character character in source)
        {
            if (character == null || !character.IsActive || !character.IsAlive)
            {
                continue;
            }

            if (area != null && (!character.IsInNestedArea || character.CurrentNestedArea != area))
            {
                continue;
            }

            if (!seen.Add(character.IInteractableID))
            {
                continue;
            }

            roster.Add(character);
        }
    }

    private List<Character> BuildActiveAreaRosterSnapshot(INestedArea area)
    {
        HashSet<int> seen = new HashSet<int>();
        List<Character> roster = new List<Character>();

        AddActiveCharactersToRoster(currentAreaRoster, area, seen, roster);
        if (area != null)
        {
            AddActiveCharactersToRoster(area.GetAllCharactersInArea(), area, seen, roster);
        }

        return roster;
    }

    private bool TryApplyPendingContextTransitionIfSafe(string source)
    {
        if (isApplyingContextTransition || !pendingContextTransition.HasValue)
        {
            return false;
        }

        if (IsActionResolutionActive || turnSequenceDepth > 0)
        {
            Trace($"TryApplyPendingContextTransitionIfSafe deferred: source={source} depth={actionResolutionDepth}/{turnSequenceDepth}");
            return false;
        }

        PendingContextTransition transition = pendingContextTransition.Value;
        pendingContextTransition = null;
        isApplyingContextTransition = true;

        try
        {
            TurnDiagnosticsLogger.LogEvent("[CONTEXT TRANSITION]", "TurnOrchestrator.ApplyPendingContextTransition",
                $"Source: {source}\n" +
                $"RequestId: {transition.RequestId}\n" +
                $"From: {CurrentContext}\n" +
                $"To: {transition.TargetContext}\n" +
                $"Reason: {transition.Reason}\n" +
                $"ActionResolutionDepth: {actionResolutionDepth}\n" +
                $"TurnSequenceDepth: {turnSequenceDepth}");

            if (transition.TargetContext == TurnContext.Combat)
            {
                SwitchToCombatMode();
            }
            else if (transition.TargetContext == TurnContext.Exploration)
            {
                SwitchToExplorationMode();
            }

            return true;
        }
        finally
        {
            isApplyingContextTransition = false;
        }
    }

    private void RequestContextTransition(TurnContext targetContext, string reason, string source)
    {
        PendingContextTransition nextTransition = new PendingContextTransition
        {
            TargetContext = targetContext,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Unspecified" : reason,
            Source = string.IsNullOrWhiteSpace(source) ? "Unknown" : source,
            RequestId = currentActionResolutionId
        };

        if (pendingContextTransition.HasValue && pendingContextTransition.Value.TargetContext == targetContext)
        {
            pendingContextTransition = nextTransition;
            TurnDiagnosticsLogger.LogEvent("[CONTEXT TRANSITION]", "TurnOrchestrator.RequestContextTransition coalesced",
                $"Source: {source}\n" +
                $"Target: {targetContext}\n" +
                $"Reason: {nextTransition.Reason}\n" +
                $"RequestId: {nextTransition.RequestId}\n" +
                $"PendingTransition: {DescribePendingContextTransition()}");
            return;
        }

        pendingContextTransition = nextTransition;
        TurnDiagnosticsLogger.LogEvent("[CONTEXT TRANSITION]", "TurnOrchestrator.RequestContextTransition queued",
            $"Source: {source}\n" +
            $"Target: {targetContext}\n" +
            $"Reason: {nextTransition.Reason}\n" +
            $"RequestId: {nextTransition.RequestId}\n" +
            $"ActionResolutionDepth: {actionResolutionDepth}\n" +
            $"TurnSequenceDepth: {turnSequenceDepth}");
    }

    private void ClearPendingContextTransition(string source)
    {
        if (!pendingContextTransition.HasValue)
        {
            return;
        }

        TurnDiagnosticsLogger.LogEvent("[CONTEXT TRANSITION]", "TurnOrchestrator.PendingContextTransition cleared",
            $"Source: {source}\nPendingTransition: {DescribePendingContextTransition()}");
        pendingContextTransition = null;
    }


    #region Area Entry

	public void EnterMainMap()
	{
		Trace("EnterMainMap: begin");
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[AREA EXIT]", "TurnOrchestrator.EnterMainMap begin");
		ClearPendingContextTransition("TurnOrchestrator.EnterMainMap");
		CurrentContext = TurnContext.MainMap;
		PlayerStats.Instance.UpdateCurrentNestedArea(null);
		PlayerStats.Instance.UpdateCurrentNestedAreaID(0);
		PlayerStats.Instance.UpdateParentNestedAreaID(0);
		PlayerStats.Instance.UpdateIsInAreas(false, true);
		PlayerStats.Instance.RegisteredInTurnManager = false;
		PlayerStats.Instance.InCombat = false;
		if (PlayerStats.Instance.CurrentPlayerCharacter != null)
		{
			PlayerStats.Instance.CurrentPlayerCharacter.InCombat = false;
			PlayerStats.Instance.CurrentPlayerCharacter.SetCombatParticipationState(CombatParticipationState.Uninvolved, "TurnOrchestrator.EnterMainMap");
			PlayerStats.Instance.CurrentPlayerCharacter.InTurn = false;
		}
		foreach (var character in currentAreaRoster)
		{
			if (character == null) continue;
			character.InCombat = false;
			character.SetCombatParticipationState(CombatParticipationState.Uninvolved, "TurnOrchestrator.EnterMainMap");
			character.InTurn = false;
		}
		explorationTurnManager.ClearCharacters();
		combatManager.DeregisterAllCharacters();
		currentAreaRoster.Clear();
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
		currentAreaRoster.Clear();
		Trace("EnterExplorationArea: managers cleared; registering player and occupants");

		playerCharacter.InCombat = false;
		playerCharacter.SetCombatParticipationState(CombatParticipationState.Uninvolved, "TurnOrchestrator.EnterExplorationArea");
		RegisterCharacter(playerCharacter, true);
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

			character.InCombat = false;
			character.SetCombatParticipationState(CombatParticipationState.Uninvolved, "TurnOrchestrator.EnterExplorationArea");
			RegisterCharacter(character, character == playerCharacter);
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
			$"CurrentAreaRoster.Count: {currentAreaRoster.Count}\n" +
			$"Exploration.Count: {DiagnosticExplorationRegisteredCount}\n" +
			$"Combat.Count: {DiagnosticCombatRegisteredCount}");

		area.UpdateHostileAreaStatus();
		bool hasHostiles = area.IsHostileArea;
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[CONTEXT UPDATE]", "TurnOrchestrator.EnterExplorationArea hostility check",
			$"HasHostiles: {hasHostiles}\nContext before hostility update: {CurrentContext}");
		TryUpdateTurnContext();
		TryApplyPendingContextTransitionIfSafe("TurnOrchestrator.EnterExplorationArea");

		if (CurrentContext == TurnContext.Exploration && !HasPendingContextTransition)
		{
			Trace("EnterExplorationArea -> StartTurnCycle");
			StartTurnCycle();
		}

		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("TurnOrchestrator.EnterExplorationArea completed",
			$"Final context: {CurrentContext}\n" +
			$"HasHostiles: {hasHostiles}\n" +
			$"Player registered in active manager: {DiagnosticIsCharacterRegisteredInActiveManager(playerCharacter)}\n" +
			$"CurrentAreaRoster.Count: {currentAreaRoster.Count}\n" +
			$"Exploration.Count: {DiagnosticExplorationRegisteredCount}\n" +
			$"Combat.Count: {DiagnosticCombatRegisteredCount}");
		// CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic.
		NestedMapDebugger.LogSnapshot(area, "SNAPSHOT_ENTER_AREA EnterExplorationArea completed");
	}


    #endregion

    #region Registration

	public void RegisterCharacter(Character character)
	{
		RegisterCharacter(character, character == PlayerStats.Instance?.CurrentPlayerCharacter);
	}

	public void RegisterCharacter(Character character, bool isPlayer)
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
		if (!currentAreaRoster.Contains(character))
		{
			currentAreaRoster.Add(character);
			added = true;
		}
		Trace($"RegisterCharacter: {(added ? "added" : "already present")} total={currentAreaRoster.Count}");
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogRegistration("TurnOrchestrator.CurrentAreaRoster", character, isPlayer);

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

    // Compatibility helper: this now means the persistent current-area roster, not active turn participants.
    public List<Character> GetAllRegisteredCharacters() => GetCurrentAreaRoster();

    public List<Character> GetCurrentAreaRoster()
    {
        return currentAreaRoster
            .Where(character => character != null)
            .Distinct()
            .ToList();
    }

    public List<Character> GetExplorationParticipants()
    {
        return explorationTurnManager != null
            ? explorationTurnManager.GetAllRegisteredCharacters()
            : new List<Character>();
    }

	public List<Character> GetCombatParticipants()
	{
		return combatManager != null
			? combatManager.GetAllRegisteredCharacters()
			: new List<Character>();
	}

    public List<Character> GetActiveTurnParticipants()
    {
        return CurrentContext switch
        {
            TurnContext.Exploration => GetExplorationParticipants(),
            TurnContext.Combat => GetCombatParticipants(),
            _ => new List<Character>()
        };
    }

    public List<Character> GetLivingActiveAreaCharacters(INestedArea area = null)
    {
        INestedArea targetArea = area ?? PlayerStats.Instance?.CurrentNestedArea;

        return GetCurrentAreaRoster()
            .Where(character => character.IsAlive &&
                                character.IsActive &&
                                character.IsInNestedArea &&
                                (targetArea == null || character.CurrentNestedArea == targetArea))
            .ToList();
    }

    #endregion

    #region Context Transitions

	public void TryUpdateTurnContext()
	{
		Trace("TryUpdateTurnContext: begin");
		var area = PlayerStats.Instance.CurrentPlayerCharacter?.CurrentNestedArea;
		area?.UpdateHostileAreaStatus();
		Character playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
		List<Character> localCharacters = area != null
			? area.GetAllCharactersInArea().Where(character => character != null).ToList()
			: currentAreaRoster.Where(character => character != null).ToList();
		List<RelationshipHostility> relationshipHostilities = RelationshipManager.ScanLocalActiveHostilities(localCharacters, area);
		PruneStaleCombatFlags(localCharacters, relationshipHostilities);
		RelationshipManager.ApplyLocalHostilitiesToActorState(relationshipHostilities);
		List<Character> activeHostiles = localCharacters
			.Where(character => HasCombatMaintenanceSignal(character, relationshipHostilities))
			.ToList();
		int activeHostileCount = activeHostiles.Count;
		int activeRelationshipHostilityCount = relationshipHostilities.Count;
		int neutralParticipantCount = localCharacters.Count(character => character.IsActive) - activeHostileCount;
		bool hasHostiles = activeHostileCount > 0 || activeRelationshipHostilityCount > 0;
		bool playerInvolved = activeHostiles.Any(character =>
			character == playerCharacter ||
			character.Target == playerCharacter ||
			playerCharacter?.Target == character) ||
			relationshipHostilities.Any(hostility =>
				hostility.Source == playerCharacter ||
				hostility.Target == playerCharacter);
		Trace($"TryUpdateTurnContext: hasHostiles={hasHostiles}");
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[CONTEXT UPDATE]", "TurnOrchestrator.TryUpdateTurnContext begin",
			$"Area: {area?.Name} ({area?.NestedAreaID})\n" +
			$"HasHostiles: {hasHostiles}\n" +
			$"ActiveHostileCount: {activeHostileCount}\n" +
			$"ActiveRelationshipHostilities: {activeRelationshipHostilityCount}");
		// CODEXLOG001_TURNLIFECYCLE: temporary combat trigger diagnostic.
		TurnDiagnosticsLogger.LogEvent("[COMBAT TRIGGER CHECK]", "TurnOrchestrator.TryUpdateTurnContext",
			$"TriggerSource: {GetCombatTriggerSource(hasHostiles, playerInvolved, activeRelationshipHostilityCount)}\n" +
			$"PlayerInvolved: {playerInvolved}\n" +
			$"ActiveHostileCount: {activeHostileCount}\n" +
			$"ActiveRelationshipHostilities: {activeRelationshipHostilityCount}\n" +
			$"Hostiles: {(activeHostiles.Count > 0 ? string.Join(", ", activeHostiles.Select(character => $"{character.Name} [{character.IInteractableID}]")) : "NONE")}\n" +
			$"RelationshipHostilities: {(relationshipHostilities.Count > 0 ? string.Join(", ", relationshipHostilities.Select(RelationshipManager.FormatHostility)) : "NONE")}\n" +
			$"ShouldStartCombat: {CurrentContext == TurnContext.Exploration && hasHostiles}");
		// CODEXLOG001_TURNLIFECYCLE: temporary combat end diagnostic.
		TurnDiagnosticsLogger.LogEvent("[COMBAT END CHECK]", "TurnOrchestrator.TryUpdateTurnContext",
			$"ActiveHostileCount: {activeHostileCount}\n" +
			$"ActiveRelationshipHostilities: {activeRelationshipHostilityCount}\n" +
			$"ActiveHostilityPairs: {(relationshipHostilities.Count > 0 ? string.Join(", ", relationshipHostilities.Select(RelationshipManager.FormatHostility)) : "NONE")}\n" +
			$"NeutralParticipantCount: {neutralParticipantCount}\n" +
			$"InactiveOrRemovedCount: {localCharacters.Count(character => !character.IsActive)}\n" +
			$"ShouldReturnToExploration: {CurrentContext == TurnContext.Combat && !hasHostiles}");

		TurnContext desiredContext = CurrentContext;
		if (CurrentContext == TurnContext.Exploration && hasHostiles)
		{
			desiredContext = TurnContext.Combat;
		}
		else if (CurrentContext == TurnContext.Combat && !hasHostiles)
		{
			desiredContext = TurnContext.Exploration;
		}

		if (desiredContext != CurrentContext)
		{
			Trace($"TryUpdateTurnContext→Request {desiredContext}");
			RequestContextTransition(desiredContext, $"HasHostiles={hasHostiles}; PlayerInvolved={playerInvolved}; ActiveHostiles={activeHostileCount}; RelationshipHostilities={activeRelationshipHostilityCount}", "TurnOrchestrator.TryUpdateTurnContext");
		}
		else
		{
			Trace("TryUpdateTurnContext: no change");
			if (pendingContextTransition.HasValue && pendingContextTransition.Value.TargetContext != desiredContext)
			{
				ClearPendingContextTransition("TurnOrchestrator.TryUpdateTurnContext no change");
			}
		}
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("TurnOrchestrator.TryUpdateTurnContext completed",
			$"HasHostiles: {hasHostiles}\nActiveHostileCount: {activeHostileCount}\nActiveRelationshipHostilities: {activeRelationshipHostilityCount}");
	}

	private string GetCombatTriggerSource(bool hasHostiles, bool playerInvolved, int activeRelationshipHostilityCount)
	{
		if (!hasHostiles) return "None";
		if (activeRelationshipHostilityCount > 0) return playerInvolved ? "RelationshipHostilityPlayerInvolved" : "RelationshipHostility";
		return playerInvolved ? "PlayerActionOrPlayerHostility" : "AreaHostility";
	}

    private static bool IsCombatConflictParticipationState(CombatParticipationState participationState)
    {
        return participationState == CombatParticipationState.Engaged ||
		       participationState == CombatParticipationState.Assisting ||
		       participationState == CombatParticipationState.Fleeing ||
		       participationState == CombatParticipationState.Searching;
	}

    private CombatParticipationState DetermineCombatParticipationState(Character character, List<RelationshipHostility> relationshipHostilities)
    {
        if (character == null || !character.IsActive || !character.IsAlive)
        {
            return CombatParticipationState.Uninvolved;
        }

        if (character.CombatParticipation == CombatParticipationState.Fleeing)
        {
            return CombatParticipationState.Fleeing;
        }

        if (character.IsHostile ||
            character.Stance == NPCStance.Hostile ||
            character.Target != null ||
            (relationshipHostilities != null && relationshipHostilities.Any(hostility => hostility.Source == character || hostility.Target == character)))
        {
            return CombatParticipationState.Engaged;
        }

        if (character.FollowTarget != null || character.Status == NPCStatus.Chasing)
        {
            return CombatParticipationState.Aware;
        }

        return CombatParticipationState.Unaware;
    }

    private bool HasCombatMaintenanceSignal(Character character, List<RelationshipHostility> relationshipHostilities)
    {
        if (character == null || !character.IsActive || !character.IsAlive)
        {
            return false;
        }

        if (IsCombatConflictParticipationState(character.CombatParticipation))
        {
            return true;
        }

        if (character.IsHostile || character.Stance == NPCStance.Hostile || character.Target != null)
        {
            return true;
        }

        return relationshipHostilities != null &&
               relationshipHostilities.Any(hostility => hostility.Source == character || hostility.Target == character);
    }

	private void PruneStaleCombatFlags(List<Character> localCharacters, List<RelationshipHostility> relationshipHostilities)
	{
		HashSet<int> relationshipCombatantIds = relationshipHostilities != null
			? relationshipHostilities
				.SelectMany(hostility => new[] { hostility.Source?.IInteractableID ?? int.MinValue, hostility.Target?.IInteractableID ?? int.MinValue })
				.Where(id => id != int.MinValue)
				.ToHashSet()
			: new HashSet<int>();

		foreach (Character character in localCharacters.Where(candidate => candidate != null && candidate.IsActive && candidate.IsAlive))
		{
			if (!(character.IsHostile || character.Stance == NPCStance.Hostile || IsCombatConflictParticipationState(character.CombatParticipation)))
			{
				continue;
			}

			if (character.IsValidCombatTarget(character.Target))
			{
				continue;
			}

			if (relationshipCombatantIds.Contains(character.IInteractableID))
			{
				character.SetCombatParticipationState(CombatParticipationState.Searching, "Combat maintenance preserved active relationship hostility without an immediate target.");
				continue;
			}

			character.ClearCombatTarget("TurnOrchestrator.TryUpdateTurnContext pruned stale hostile target.");
			character.SetCombatParticipationState(CombatParticipationState.Searching, "Combat maintenance preserved hostility after losing target.");

			CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT CONTEXT]", "TurnOrchestrator.TryUpdateTurnContext pruned stale hostile state",
				$"Actor={character.Name} [{character.IInteractableID}]\n" +
				$"RelationshipCombatant={relationshipCombatantIds.Contains(character.IInteractableID)}\n" +
				$"IsHostileAfter={character.IsHostile}\n" +
				$"StanceAfter={character.Stance}\n" +
				$"CombatParticipationAfter={character.CombatParticipation}",
				character);
		}
	}

	private void SwitchToCombatMode()
	{
		Trace("SwitchToCombatMode: begin");
		if (CurrentContext == TurnContext.Combat) { Trace("SwitchToCombatMode: already in Combat"); return; }
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[COMBAT START]", "TurnOrchestrator.SwitchToCombatMode begin");

		CurrentContext = TurnContext.Combat;
		PlayerStats.Instance.InCombat = true;
		if (PlayerStats.Instance.CurrentPlayerCharacter != null)
		{
			PlayerStats.Instance.CurrentPlayerCharacter.InCombat = true;
		}
		explorationTurnManager.Suspend();
		combatManager.DeregisterAllCharacters();

		INestedArea activeArea = PlayerStats.Instance.CurrentNestedArea;
		List<Character> localCombatParticipants = BuildActiveAreaRosterSnapshot(activeArea);
		List<RelationshipHostility> localRelationshipHostilities = RelationshipManager.ScanLocalActiveHostilities(localCombatParticipants, activeArea);
		Character playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
		if (playerCharacter != null && !localCombatParticipants.Contains(playerCharacter))
		{
			localCombatParticipants.Insert(0, playerCharacter);
		}

		HashSet<int> registeredIds = new HashSet<int>();
		int inactiveSkipped = 0;
		int wrongAreaSkipped = 0;
		int duplicateSkipped = 0;
		int areaStateRepaired = 0;
		List<string> participantLines = new List<string>();

		foreach (var character in localCombatParticipants)
		{
			if (character == null) continue;

			if (!registeredIds.Add(character.IInteractableID))
			{
				duplicateSkipped++;
				continue;
			}

			if (!character.IsActive)
			{
				inactiveSkipped++;
				continue;
			}

			if (!character.IsAlive)
			{
				inactiveSkipped++;
				continue;
			}

			if (activeArea != null && character.CurrentNestedArea == null)
			{
				character.CurrentNestedArea = activeArea;
				character.IsInNestedArea = true;
				areaStateRepaired++;
			}

			if (activeArea != null && (!character.IsInNestedArea || character.CurrentNestedArea != activeArea))
			{
				wrongAreaSkipped++;
				continue;
			}

			bool isPlayer = character == PlayerStats.Instance.CurrentPlayerCharacter;
			character.InCombat = true;
			character.SetCombatParticipationState(DetermineCombatParticipationState(character, localRelationshipHostilities), "TurnOrchestrator.SwitchToCombatMode roster assignment");
			if (!currentAreaRoster.Contains(character))
			{
				currentAreaRoster.Add(character);
			}
			Trace($"SwitchToCombatMode: register {character.Name} isPlayer={isPlayer}");
			combatManager.RegisterCharacter(character, isPlayer);
			participantLines.Add($"{character.Name} [{character.IInteractableID}] Type={character.GetType().Name} Role={BaseTurnManager.GetCombatParticipantRole(character, isPlayer)} IsActive={character.IsActive} IsAlive={character.IsAlive} IsHostile={character.IsHostile} Stance={character.Stance} Area={character.CurrentNestedArea?.Name ?? "NULL"}");
		}

		int playerCombatRegistrationCount = combatManager.GetRegisteredCharacters()
			.Count(entry => PlayerStats.Instance.CurrentPlayerCharacter != null &&
							entry.Key == PlayerStats.Instance.CurrentPlayerCharacter.IInteractableID);
		// CODEXLOG001_TURNLIFECYCLE: temporary combat player registration diagnostic.
		TurnDiagnosticsLogger.LogEvent("[PLAYER REGISTRATION]", "TurnOrchestrator.SwitchToCombatMode player combat registration",
			$"Player: {PlayerStats.Instance.CurrentPlayerCharacter?.Name ?? "NULL"} [{PlayerStats.Instance.CurrentPlayerCharacter?.IInteractableID.ToString() ?? "NULL"}]\n" +
			$"Player registered in CombatTurnManager: {playerCombatRegistrationCount == 1}\n" +
			$"PlayerRegistrationCount: {playerCombatRegistrationCount}\n" +
			$"PlayerDuplicate: {playerCombatRegistrationCount > 1}\n" +
			$"Combat.Count: {combatManager.DiagnosticRegisteredCount}");

		// CODEXLOG001_TURNLIFECYCLE: temporary combat participant role diagnostic.
		TurnDiagnosticsLogger.LogEvent("[COMBAT PARTICIPANTS]", "TurnOrchestrator.SwitchToCombatMode registered scene-wide combat participants",
			$"Area: {activeArea?.Name ?? "NULL"} ({activeArea?.NestedAreaID.ToString() ?? "NULL"})\n" +
			$"Count: {participantLines.Count}\n" +
			$"Inactive skipped: {inactiveSkipped}\n" +
			$"Wrong-area skipped: {wrongAreaSkipped}\n" +
			$"Duplicate skipped: {duplicateSkipped}\n" +
			$"Area state repaired: {areaStateRepaired}\n" +
			$"Participants:\n{(participantLines.Count > 0 ? string.Join("\n", participantLines) : "NONE")}");
		TurnDiagnosticsLogger.LogEvent("[SIMULATION CONTEXT]", "TurnOrchestrator.SwitchToCombatMode participant split",
			$"CurrentAreaRosterCount: {currentAreaRoster.Count}\n" +
			$"CombatParticipantCount: {participantLines.Count}\n" +
			$"NonCombatantExcludedCount: {Mathf.Max(0, currentAreaRoster.Count - participantLines.Count)}\n" +
			$"ActiveTurnManager: CombatTurnManager\n" +
			"SemanticNote: All living active occupants in the current area are registered for combat-mode simulation; awareness and engagement remain actor-specific.");

		Trace("SwitchToCombatMode→CombatTurnManager.StartTurnCycle");
		CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT CONTEXT]", "TurnOrchestrator.SwitchToCombatMode",
			$"Area={activeArea?.Name ?? "NULL"} ({activeArea?.NestedAreaID.ToString() ?? "NULL"})\n" +
			$"ParticipantCount={participantLines.Count}\n" +
			$"InactiveSkipped={inactiveSkipped}\n" +
			$"WrongAreaSkipped={wrongAreaSkipped}\n" +
			$"DuplicateSkipped={duplicateSkipped}\n" +
			$"AreaStateRepaired={areaStateRepaired}\n" +
			$"Participants={(participantLines.Count > 0 ? string.Join(" | ", participantLines) : "NONE")}",
			PlayerStats.Instance.CurrentPlayerCharacter);
		combatManager.StartTurnCycle();
		GameDebugger.Instance.LogInfo("Switched to Combat mode.");
		MessageLogManager.Instance?.Log("combat_start", PlayerStats.Instance.CurrentNestedArea?.Name ?? "Hostility");
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
		PlayerStats.Instance.InCombat = false;
		if (PlayerStats.Instance.CurrentPlayerCharacter != null)
		{
			PlayerStats.Instance.CurrentPlayerCharacter.InCombat = false;
			PlayerStats.Instance.CurrentPlayerCharacter.SetCombatParticipationState(CombatParticipationState.Uninvolved, "TurnOrchestrator.SwitchToExplorationMode");
		}
		foreach (var character in currentAreaRoster)
		{
			if (character == null) continue;
			character.InCombat = false;
			character.SetCombatParticipationState(CombatParticipationState.Uninvolved, "TurnOrchestrator.SwitchToExplorationMode");
			character.InTurn = false;
		}
		combatManager.DeregisterAllCharacters();
		explorationTurnManager.ClearCharacters();

		INestedArea activeArea = PlayerStats.Instance.CurrentNestedArea;
		List<Character> localExplorationParticipants = BuildActiveAreaRosterSnapshot(activeArea);
		int explorationRestored = 0;
		int explorationSkippedInactiveOrDead = 0;
		foreach (var character in localExplorationParticipants)
		{
			if (character == null)
			{
				continue;
			}

			if (!character.IsActive || !character.IsAlive)
			{
				explorationSkippedInactiveOrDead++;
				continue;
			}

			if (character.IsInNestedArea && character.CurrentNestedArea == PlayerStats.Instance.CurrentNestedArea)
			{
				Trace($"SwitchToExplorationMode: register {character.Name}");
				character.SetCombatParticipationState(CombatParticipationState.Uninvolved, "TurnOrchestrator.SwitchToExplorationMode participant rebuild");
				explorationTurnManager.RegisterCharacter(character, character == PlayerStats.Instance.CurrentPlayerCharacter);
				explorationRestored++;
			}
		}

		Trace("SwitchToExplorationMode→ExplorationTurnManager.Resume");
		CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT CONTEXT]", "TurnOrchestrator.SwitchToExplorationMode",
			$"RegisteredExplorationCount={DiagnosticExplorationRegisteredCount}\n" +
			$"CombatRegisteredCount={DiagnosticCombatRegisteredCount}\n" +
			$"PlayerInCombat={PlayerStats.Instance.InCombat}\n" +
			$"ExplorationRestored={explorationRestored}\n" +
			$"ExplorationSkippedInactiveOrDead={explorationSkippedInactiveOrDead}",
			PlayerStats.Instance.CurrentPlayerCharacter);
		TurnDiagnosticsLogger.LogEvent("[SIMULATION CONTEXT]", "TurnOrchestrator.SwitchToExplorationMode participant rebuild",
			$"CurrentAreaRosterCount: {currentAreaRoster.Count}\n" +
			$"ExplorationParticipantCount: {DiagnosticExplorationRegisteredCount}\n" +
			$"CombatParticipantCount: {DiagnosticCombatRegisteredCount}\n" +
			$"ExplorationRestored: {explorationRestored}\n" +
			$"ExplorationSkippedInactiveOrDead: {explorationSkippedInactiveOrDead}\n" +
			"SemanticNote: Exploration participants are rebuilt from the persistent CurrentAreaRoster.");
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

        currentAreaRoster.Clear();
        currentAreaRoster.AddRange(BuildActiveAreaRosterSnapshot(activeArea));
        GameDebugger.Instance.LogInfo($"Reevaluated characters in scene. Total: {currentAreaRoster.Count}");

        switch (CurrentContext)
        {
            case TurnContext.Exploration:
                explorationTurnManager.ClearCharacters();
                foreach (var character in currentAreaRoster)
                {
                    character.InCombat = false;
                    character.SetCombatParticipationState(CombatParticipationState.Uninvolved, "TurnOrchestrator.ReevaluateCharactersInScene Exploration");
                    explorationTurnManager.RegisterCharacter(character, character == PlayerStats.Instance.CurrentPlayerCharacter);
                }
                break;

            case TurnContext.Combat:
                combatManager.DeregisterAllCharacters();
                foreach (var character in currentAreaRoster)
                {
                    character.InCombat = true;
                    character.SetCombatParticipationState(DetermineCombatParticipationState(character, RelationshipManager.ScanLocalActiveHostilities(currentAreaRoster, activeArea)), "TurnOrchestrator.ReevaluateCharactersInScene Combat");
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
		public int DiagnosticAllCharactersCount => currentAreaRoster.Count;

		// CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
		public int DiagnosticExplorationRegisteredCount => explorationTurnManager != null ? explorationTurnManager.DiagnosticRegisteredCount : -1;

		// CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
		public int DiagnosticCombatRegisteredCount => combatManager != null ? combatManager.DiagnosticRegisteredCount : -1;

		// CODEXLOG001_TURNLIFECYCLE: temporary read-only turn lifecycle diagnostic accessor.
		public List<Character> DiagnosticGetAllCharactersSnapshot()
		{
			return currentAreaRoster.Where(character => character != null).ToList();
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
			return currentAreaRoster.Contains(c);
		}

		public void StartTurnCycle() {
			if (CurrentContext == TurnContext.Combat)
				combatManager.StartTurnCycle();
			else if (CurrentContext == TurnContext.Exploration)
				explorationTurnManager.StartTurnCycle();
		}

		public void DeregisterAllCharacters()
		{
			// Scene-reset helper only. Mode switches rebuild participants from the currentAreaRoster.
			if (CurrentContext == TurnContext.Combat)
				combatManager.DeregisterAllCharacters();
			else if (CurrentContext == TurnContext.Exploration)
				explorationTurnManager.DeregisterAllCharacters();

			currentAreaRoster.Clear();
			Trace("DeregisterAllCharacters: global list cleared");
		}

		public void PlayerTurnCompleted() {
			TurnDiagnosticsLogger.LogEvent("[PLAYER TURN]", "TurnOrchestrator.PlayerTurnCompleted",
				$"CurrentContext: {CurrentContext}\n" +
				$"CurrentAreaRoster.Count: {currentAreaRoster.Count}\n" +
				$"Exploration.Count: {DiagnosticExplorationRegisteredCount}\n" +
				$"Combat.Count: {DiagnosticCombatRegisteredCount}\n" +
				$"Player: {PlayerStats.Instance?.CurrentPlayerCharacter?.Name ?? "NULL"}\n" +
				$"Player.InTurn: {PlayerStats.Instance?.CurrentPlayerCharacter?.InTurn.ToString() ?? "NULL"}\n" +
				$"PlayerStats.Stamina: {FixedPointResourceMath.Format(PlayerStats.Instance?.Stamina ?? 0)}\n" +
				$"PlayerStats.CombatExertion: {FixedPointResourceMath.Format(PlayerStats.Instance?.CombatExertion ?? 0)}\n" +
				$"PlayerStats.ConsumptionCapacity: {PlayerStats.Instance?.CurrentConsumptionCapacity.ToString() ?? "NULL"}",
				PlayerStats.Instance?.CurrentPlayerCharacter);

			if (HasPendingContextTransition || IsActionResolutionActive)
			{
				TurnDiagnosticsLogger.LogWarning("TurnOrchestrator.PlayerTurnCompleted deferred because a context transition is pending",
					$"CurrentContext: {CurrentContext}\n" +
					$"PendingTransition: {DescribePendingContextTransition()}\n" +
					$"ActionResolutionDepth: {actionResolutionDepth}\n" +
					$"TurnSequenceDepth: {turnSequenceDepth}");
				return;
			}

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
