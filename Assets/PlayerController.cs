using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.Events;
using System.Runtime.CompilerServices;

public class PlayerController : MonoBehaviour
{
    // Singleton instance
    public static PlayerController Instance { get; private set; }

	[SerializeField] private AreaEntryCoordinator areaEntryCoordinator;


    #region References
    public MapGenerator mapGenerator; // Reference to your MapGenerator
    public NPCManager npcManager;
    public ActionManager actionManager;
    public EndOfTurnManager endOfTurnManager;
    public TMP_Text descriptionText;
	private TurnOrchestrator turnOrchestrator;


    public MapDisplayUI mapDisplayUI;
    public GameObject playerPanel;
    public Button northButton;
    public Button southButton;
    public Button westButton;
    public Button eastButton;
    public GameObject nestedAreaPanel;
    public Button mapPlusButton;
    public Button mapMinusButton;
    public NestedAreaGenerator nestedAreaGenerator;
    public Button interactButton;
    public PlayerInventory playerInventory;
    public EventLogger eventLogger;

    public GameObject adaptiveActionMenu;
    public GameObject actionButtonPrefab;

    public Button toggleNestedAreaButton;
    private TMP_Text toggleNestedAreaButtonText;
    #endregion

    #region Player State
    public Vector2Int mainMapPosition;
    public int currentRegion;
    public int currentCellID;
    public Cell currentPlayerCell;
    public Cell previousPlayerCell;
    public Cell enteringCell;
    public int previousCellID;
    public Vector2Int playerPosition; // Current player position
    public Vector2Int previousPosition; // Previous player position
    public Vector2Int nestedMapPosition; // Nested map player position
    public Vector2Int previousNestedMapPosition; // Previous nested map position
    public Direction currentDirection;

    public bool isInNestedArea = false;
    private INestedArea currentNestedArea = null;
    public int parentNestedAreaID;
    public int currentNestedAreaID;
    public int previousNestedAreaID;

    public bool isInMainMap = true;
    public Cell facingCell;

    private bool keyboardMovementActivated = true;
    private Vector2Int facingCellPosition;
    private float turnProgress = 0f; // Add this to track turn progress
    #endregion

    #region Keyboard and Controller Inputs
    public bool isHoldingKey = false;
    private float holdKeyTimer = 0f;
    private float holdKeyInterval = 0.3f; // Interval between movements when holding a key (in seconds)
    private KeyCode currentHeldKey;
    private int lastCombatInputBlockedFrame = -1;
    private int lastCombatInputBlockedActorId = int.MinValue;
    private int lastNoMPFeedbackFrame = -1;
    private int lastNoAPFeedbackFrame = -1;
    private bool autoEndCombatTurnWhenNoAPMP = true;
    private bool autoEndingCombatTurn = false;
    private bool notifiedManualEndRequiredForNoResources = false;
    private bool? lastEndTurnActionVisible;
    private string lastEndTurnActionVisibilityReason = string.Empty;
    public bool IsShiftHeld
    {
        get
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }

    #endregion

    #region Events
    public static event EventHandler<string> OnDescriptionUpdate;
    #endregion

    #region Unity Methods
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject); // Destroy any duplicate instances
        }
    }

    void Start()
    {
        if (playerPanel != null) playerPanel.SetActive(false);

        SetupButtonListeners();
        OnDescriptionUpdate += UpdateDescriptionAfterInteraction;
        toggleNestedAreaButtonText = toggleNestedAreaButton.GetComponentInChildren<TMP_Text>();
        UpdateToggleButtonState();
        toggleNestedAreaButton.onClick.AddListener(ToggleNestedArea);
		turnOrchestrator = TurnOrchestrator.Instance;

        if (isInNestedArea)
        {
            turnProgress = 0f; // Reset turn progress when entering nested area
        }
    }

    void OnDestroy()
    {
        OnDescriptionUpdate -= UpdateDescriptionAfterInteraction;
    }

    void Update()
    {
        if (keyboardMovementActivated) HandleKeyboardInput();
        UpdatePlayerStatsInstance();
    }
    #endregion

    #region Game Start
    public void StartGame()
    {
        TimedEvents.Instance.OnGameStart();
        UIController.Instance.ToggleGreyOutPanel();

        PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;

        // Ensure PlayerInventory is initialized before doing anything else
        Debug.Log("StartGame: Ensuring PlayerInventory is initialized...");
        var _ = PlayerInventory.Instance;  // This triggers singleton instantiation

        // Assign the player to the start cell
        AssignPlayerToStartCell();

        UpdatePlayerStatsInstance();
        mapDisplayUI.UpdateBothMaps();
        UpdateDescription();

        if (playerPanel != null) playerPanel.SetActive(true);
        else Debug.LogError("Player panel reference not set.");

        UIController.Instance.DisablePlayButton();

        mapGenerator.DisplayCellIDRange();

        Debug.Log("StartGame: Completed initialization.");
    }
    #endregion


    #region Button Setup
    private void SetupButtonListeners()
    {
        northButton?.onClick.AddListener(() => RequestPlayerMove(Vector2Int.up, Direction.North, "UIButton:North"));
        southButton?.onClick.AddListener(() => RequestPlayerMove(Vector2Int.down, Direction.South, "UIButton:South"));
        westButton?.onClick.AddListener(() => RequestPlayerMove(Vector2Int.left, Direction.West, "UIButton:West"));
        eastButton?.onClick.AddListener(() => RequestPlayerMove(Vector2Int.right, Direction.East, "UIButton:East"));
    }
    #endregion

    #region Input Handling
    private void HandleKeyboardInput()
    {
        // Check if the game has started
        if (!GameManager.Instance.GameStarted)
        {
            return;
        }

        if (PlayerStats.Instance.KeyboardPanel != KeyboardPanel.Default &&
            PlayerStats.Instance.KeyboardPanel != KeyboardPanel.MainMap &&
            PlayerStats.Instance.KeyboardPanel != KeyboardPanel.NestedArea)
        {
            return;
        }

        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.Period))
        {
            ToggleAutoEndCombatTurn();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            HandleManualEndTurnInput();
            return;
        }

        if (!CanAcceptPlayerTurnInput("HandleKeyboardInput"))
        {
            isHoldingKey = false;
            holdKeyTimer = 0f;
            return;
        }

        // Movement keys handling
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            HandleKeyHold(KeyCode.W, Vector2Int.up, Direction.North, shiftHeld);
        }
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            HandleKeyHold(KeyCode.S, Vector2Int.down, Direction.South, shiftHeld);
        }
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            HandleKeyHold(KeyCode.A, Vector2Int.left, Direction.West, shiftHeld);
        }
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            HandleKeyHold(KeyCode.D, Vector2Int.right, Direction.East, shiftHeld);
        }
        else
        {
            isHoldingKey = false;
            holdKeyTimer = 0f;
        }

        // Check if the "E" key is pressed to toggle nested area
        if (Input.GetKeyDown(KeyCode.E))
        {
            ToggleNestedArea();
        }

        // Check if the adaptive action menu should be updated
        if (Input.GetKeyDown(KeyCode.T))
        {
            UpdateAdaptiveActionMenu();
        }
    }


    private void HandleKeyHold(KeyCode key, Vector2Int direction, Direction newDirection, bool shiftHeld)
    {
        if (!shiftHeld)
        {
            if (!isHoldingKey || currentHeldKey != key)
            {
                RequestPlayerMove(direction, newDirection, $"Keyboard:{key}");
                isHoldingKey = true;
                currentHeldKey = key;
                holdKeyTimer = holdKeyInterval; // Start the timer for holding the key
            }
            else
            {
                holdKeyTimer -= Time.deltaTime;
                if (holdKeyTimer <= 0f)
                {
                    RequestPlayerMove(direction, newDirection, $"KeyboardHold:{key}");
                    holdKeyTimer = holdKeyInterval; // Reset the timer for the next movement
                }
            }
        }
        else
        {
            UpdateFacingDirection(newDirection);
            isHoldingKey = false;
        }
    }

    private void UpdateFacingDirection(Direction newDirection)
    {
        currentDirection = newDirection;
        UpdateAdaptiveActionMenu();
        UpdateDescription();
    }
    #endregion

    #region Movement

    private void RequestPlayerMove(Vector2Int direction, Direction newDirection, string inputSource)
    {
        if (!CanAcceptPlayerTurnInput("RequestPlayerMove"))
        {
            return;
        }

        bool turnManagedContext = PlayerStats.Instance.RegisteredInTurnManager;
        bool combatContext = IsCombatTurnContext();
        Character playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        ActionCostProfile movementCostProfile = ActionCostProfileResolver.BuildForMovement(combatContext);
        int apBefore = PlayerStats.Instance.ActionPoints;
        int mpBefore = PlayerStats.Instance.MovePoints;
        Vector2Int positionBefore = playerPosition;
        string activeTurnManager = TurnOrchestrator.Instance?.CurrentContext switch
        {
            TurnContext.Combat => "CombatTurnManager",
            TurnContext.Exploration => "ExplorationTurnManager",
            _ => "None"
        };

        if (turnManagedContext && combatContext && PlayerStats.Instance.MovePoints < 1)
        {
            GameDebugger.Instance.LogInfo("PlayerController.RequestPlayerMove rejected due to zero combat MovePoints.");
            TurnDiagnosticsLogger.LogEvent("[PLAYER MOVEMENT]", "PlayerController.RequestPlayerMove",
                $"InputSource: {inputSource}\n" +
                $"Mode: Combat\n" +
                $"MovementRequestMethod: PlayerController.RequestPlayerMove\n" +
                $"MovementSuccess: False\n" +
                $"BlockedReason: NoMovePoints\n" +
                $"Direction: {newDirection}\n" +
                $"PositionBefore: {positionBefore}\n" +
                $"PositionAfter: {playerPosition}\n" +
                $"MovementCost: 1\n" +
                $"CostCategory: MovementBudget\n" +
                $"APBefore: {apBefore}\n" +
                $"APAfter: {PlayerStats.Instance.ActionPoints}\n" +
                $"MPBefore: {mpBefore}\n" +
                $"MPAfter: {PlayerStats.Instance.MovePoints}\n" +
                $"ExplorationTurnCompletionRequested: False\n" +
                $"CombatTurnRemainsOpen: {IsPlayerCombatTurnActive()}\n" +
                $"ActiveTurnManager: {activeTurnManager}",
                playerCharacter);
            ActionCostProfileResolver.LogPredictedCost("PlayerController.RequestPlayerMove blocked", "PlayerMove", movementCostProfile, playerCharacter);
            ShowNotEnoughMPFeedback("move");
            return;
        }

        bool moved = Move(direction);
        if (!moved)
        {
            TurnDiagnosticsLogger.LogEvent("[PLAYER MOVEMENT]", "PlayerController.RequestPlayerMove",
                $"InputSource: {inputSource}\n" +
                $"Mode: {(combatContext ? "Combat" : IsExplorationTurnContext() ? "Exploration" : "Other")}\n" +
                $"MovementRequestMethod: PlayerController.RequestPlayerMove\n" +
                $"MovementSuccess: False\n" +
                $"BlockedReason: ValidationFailed\n" +
                $"Direction: {newDirection}\n" +
                $"PositionBefore: {positionBefore}\n" +
                $"PositionAfter: {playerPosition}\n" +
                $"MovementCost: 1\n" +
                $"CostCategory: {(combatContext ? "MovementBudget" : "TimeCostingMovement")}\n" +
                $"APBefore: {apBefore}\n" +
                $"APAfter: {PlayerStats.Instance.ActionPoints}\n" +
                $"MPBefore: {mpBefore}\n" +
                $"MPAfter: {PlayerStats.Instance.MovePoints}\n" +
                $"ExplorationTurnCompletionRequested: False\n" +
                $"CombatTurnRemainsOpen: {IsPlayerCombatTurnActive()}\n" +
                $"ActiveTurnManager: {activeTurnManager}",
                playerCharacter);
            return;
        }

        currentDirection = newDirection;
        UpdateAllPlayerStats();
        UpdateDescription();
        UpdateAdaptiveActionMenu();
        UIController.Instance.UpdateMapsAfterAction();

        bool explorationTurnCompletionRequested = false;
        bool worldTimeAdvanced = false;
        if (turnManagedContext)
        {
            DeductMovePoints(1);

            if (!combatContext && IsExplorationTurnContext())
            {
                explorationTurnCompletionRequested = CompleteExplorationTurnForTimeCostingAction($"PlayerMove:{inputSource}", 1f);
                worldTimeAdvanced = explorationTurnCompletionRequested;
            }
        }
        else
        {
            AddTurnProgress(1f);
            worldTimeAdvanced = true;
        }

        TurnDiagnosticsLogger.LogEvent("[PLAYER MOVEMENT]", "PlayerController.RequestPlayerMove",
            $"InputSource: {inputSource}\n" +
            $"Mode: {(combatContext ? "Combat" : IsExplorationTurnContext() ? "Exploration" : "Other")}\n" +
            $"MovementRequestMethod: PlayerController.RequestPlayerMove\n" +
            $"MovementSuccess: True\n" +
            $"Direction: {newDirection}\n" +
            $"PositionBefore: {positionBefore}\n" +
            $"PositionAfter: {playerPosition}\n" +
            $"MovementCost: 1\n" +
            $"CostCategory: {(combatContext ? "MovementBudget" : "TimeCostingMovement")}\n" +
            $"APBefore: {apBefore}\n" +
            $"APAfter: {PlayerStats.Instance.ActionPoints}\n" +
            $"MPBefore: {mpBefore}\n" +
            $"MPAfter: {PlayerStats.Instance.MovePoints}\n" +
            $"WorldTimeAdvanced: {worldTimeAdvanced}\n" +
            $"ExplorationTurnCompletionRequested: {explorationTurnCompletionRequested}\n" +
            $"CombatTurnRemainsOpen: {combatContext && IsPlayerCombatTurnActive()}\n" +
            $"ActiveTurnManager: {activeTurnManager}",
            playerCharacter);
        ActionCostProfileResolver.LogPredictedCost("PlayerController.RequestPlayerMove executed", "PlayerMove", movementCostProfile, playerCharacter);
    }

    private bool Move(Vector2Int direction)
    {
        if (!CanAcceptPlayerTurnInput("Move"))
        {
            return false;
        }

        previousPosition = playerPosition;
        Vector2Int newPosition = playerPosition + direction;

        previousCellID = currentCellID;
        previousPlayerCell = currentPlayerCell;

        if (isInNestedArea)
        {
            if (currentNestedArea == null) return false;

            if (currentNestedArea.IsValidPosition(newPosition) && IsCellPassable(newPosition))
            {
                var currentCell = currentNestedArea.GetCellAtPosition(playerPosition);
                currentCell.LastVisited = TimeManager.Instance.currentDay;
                UpdatePlayerPosition(newPosition, true);
                currentCellID = currentNestedArea.GetCellAtPosition(newPosition).CellID;
            }
            else
            {
                return false;
            }
        }
        else
        {
            if (IsValidPosition(newPosition, false) && IsCellPassable(newPosition))
            {
                var currentCell = mapGenerator.GetCell(playerPosition);
                currentCell.LastVisited = TimeManager.Instance.currentDay;
                mapGenerator.UpdateFogOfWar(currentCell);
                UpdatePlayerPosition(newPosition, false);
                currentCellID = mapGenerator.GetCell(newPosition).CellID;
                currentRegion = mapGenerator.GetCell(newPosition).RegionNumber;
            }
            else
            {
                return false;
            }
        }


        UpdateAllPlayerStats();

        AudioController.Instance.PlayMovementSound();

        currentPlayerCell = mapGenerator.GetCellByID(currentCellID);
        mapDisplayUI.UpdateBothMaps();


        UpdateDescription();
        UpdateAdaptiveActionMenu();
        return true;
    }


    private void AssignPlayerToStartCell()
    {
        Vector2Int startPosition = GameManager.Instance.PlayerStartCell.Coordinates;
        previousPosition = new Vector2Int(-1, -1);

        if (isInNestedArea && currentNestedArea != null)
        {
            nestedMapPosition = startPosition;
            playerPosition = startPosition;
            var nestedMap = currentNestedArea.GetNestedMap();
            nestedMap[playerPosition.x, playerPosition.y].isPlayerPresent = true;
            nestedMap[playerPosition.x, playerPosition.y].isPassable = false;
            currentCellID = currentNestedArea.GetCellAtPosition(startPosition).CellID;
            currentPlayerCell = currentNestedArea.GetCellAtPosition(startPosition);
            currentNestedArea.UpdatePlayerPosition(startPosition);
        }
        else
        {
            playerPosition = startPosition;
            mapGenerator.map[playerPosition.x, playerPosition.y].isPlayerPresent = true;
            mapGenerator.map[playerPosition.x, playerPosition.y].nestedAreaCanBeSeen = true;
            currentCellID = mapGenerator.GetCell(startPosition).CellID;
            currentRegion = mapGenerator.GetCell(startPosition).RegionNumber;
            currentPlayerCell = mapGenerator.GetCell(startPosition);

        }

        currentPlayerCell = mapGenerator.GetCellByID(currentCellID);
        previousPlayerCell = mapGenerator.GetCellByID(currentCellID);
        mapGenerator.UpdateFogOfWar(currentPlayerCell);

        UpdatePreviousCellAfterMovement();
        UpdateAllPlayerStats();
        mapDisplayUI.UpdateBothMaps();
        UpdateDescription();
    }

    private void UpdatePlayerPosition(Vector2Int newPosition, bool isNested)
    {
        if (isNested)
        {
            var nestedMap = currentNestedArea.GetNestedMap();
            nestedMap[playerPosition.x, playerPosition.y].isPlayerPresent = false;
            nestedMap[playerPosition.x, playerPosition.y].isPassable = true;
            playerPosition = newPosition;
            nestedMap[playerPosition.x, playerPosition.y].isPlayerPresent = true;
            nestedMap[playerPosition.x, playerPosition.y].isPassable = false;
            nestedMapPosition = newPosition;
            currentNestedArea.UpdatePlayerPosition(newPosition);
        }
        else
        {
            mapGenerator.map[playerPosition.x, playerPosition.y].isPlayerPresent = false;
            mapGenerator.map[playerPosition.x, playerPosition.y].nestedAreaCanBeSeen = false;
            playerPosition = newPosition;
            mapGenerator.map[newPosition.x, newPosition.y].isPlayerPresent = true;
            mapGenerator.map[newPosition.x, newPosition.y].nestedAreaCanBeSeen = true;
        }

        UpdateAllPlayerStats();
    }

    private void CheckEndOfTurn()
    {
        if (turnProgress >= 1f)
        {
            turnProgress = 0f;
            endOfTurnManager.EndNestedTurn();
        }
    }

    private void UpdatePreviousCellAfterMovement()
    {
        int passedThroughToAdd = PlayerStats.Instance.PartySize;
        previousPlayerCell.PassedThroughCount += passedThroughToAdd;
    }

    #endregion

	#region Nested Area Management

	public void TryEnterOrGenerateNestedArea()
	{
		CallTrace.Mark(this);

		if (isInNestedArea)
		{
			HandleNestedAreaTransition();
		}
		else
		{
			HandleMainMapTransition();
		}

		UpdateDescription();
		UpdateAdaptiveActionMenu();
	}

	private void HandleNestedAreaTransition()
	{
		var currentCell = currentNestedArea.GetCellAtPosition(facingCellPosition);
		if (currentCell == null || !currentCell.hasNestedArea)
		{
			nestedAreaGenerator.GenerateNestedAreaWithinNestedArea(currentNestedArea, facingCellPosition);
		}

		EnterNestedAreaWithinNestedArea(currentCell);
	}

	private void HandleMainMapTransition()
	{
		var currentCell = mapGenerator.GetCell(playerPosition);
		if (currentCell == null) return;

		enteringCell = currentCell;
		currentCellID = currentCell.CellID;

		if (!currentCell.hasNestedArea)
		{
			nestedAreaGenerator.GenerateNestedArea(currentCell);
		}

		CallTrace.Mark(this);
		EnterNestedArea(currentCell);
	}

	private void EnterNestedArea(Cell cellWithNestedArea)
	{
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[AREA ENTRY]", "PlayerController.EnterNestedArea begin", $"CellID: {cellWithNestedArea?.CellID}\nHasNestedArea: {cellWithNestedArea?.hasNestedArea}");
		if (cellWithNestedArea == null || !cellWithNestedArea.hasNestedArea) return;

		isInNestedArea = true;
		var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;

		previousNestedAreaID = currentNestedArea != null ? currentNestedArea.NestedAreaID : -1;
		int mainMapCellID = currentCellID;

		parentNestedAreaID = cellWithNestedArea.CurrentAreaID;
		PlayerStats.Instance.ParentNestedAreaID = cellWithNestedArea.CurrentAreaID;

		mainMapPosition = playerPosition;
		isInMainMap = false;

		currentNestedArea = cellWithNestedArea.NestedArea;
		playerPosition = currentNestedArea.EntrancePosition;
		nestedAreaPanel.SetActive(true);

		currentNestedArea.GetNestedMap()[playerPosition.x, playerPosition.y].isPlayerPresent = true;

		CallTrace.Mark(this);

		// Re-entry hook (existing behaviour)
		if (currentNestedArea.HasVisited)
			currentNestedArea.HandlePlayerReentry();

		// Delegate environment, NPCs, animals to coordinator
		areaEntryCoordinator.HandleOnEnterFromMainMap(
			parentCell: cellWithNestedArea,
			nestedArea: currentNestedArea,
			mainMapCellID: mainMapCellID,
			wasPlayerStart: cellWithNestedArea.WasPlayerStart
		);

		// IDs, stats, logs
		previousCellID = currentCellID;
		currentCellID = currentNestedArea.GetCellAtPosition(playerPosition).CellID;
		currentNestedAreaID = currentNestedArea.NestedAreaID;

		if (MessageLogManager.Instance != null)
		{
			string areaType = cellWithNestedArea.Terrain.ToString();
			string nestedAreaName = currentNestedArea.Name;
			MessageLogManager.Instance.Log("exploration", "Entered", $"{areaType} - {nestedAreaName}");
		}

		UpdateNestedAreaStats();
		UpdateDescription();

		// Sync PlayerStats pointer
		PlayerStats.Instance.CurrentPlayerCharacter.CurrentNestedArea = currentNestedArea;

		// Turn Orchestrator (use singleton)
		var orchestrator = TurnOrchestrator.Instance;
		GameManager.Instance.ActiveTurnManager = true;
		PlayerStats.Instance.RegisteredInTurnManager = true;
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[AREA ENTRY]", "PlayerController.EnterNestedArea before TurnOrchestrator.EnterExplorationArea",
			$"NestedArea: {currentNestedArea?.Name} ({currentNestedArea?.NestedAreaID})", playerCharacter);
		orchestrator.EnterExplorationArea(currentNestedArea, playerCharacter);

		UIController.Instance.UpdateMapsAfterAction();
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("PlayerController.EnterNestedArea completed", $"NestedArea: {currentNestedArea?.Name} ({currentNestedArea?.NestedAreaID})");
	}

	public void EnterNestedAreaWithinNestedArea(Cell cellWithNestedArea)
	{
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[AREA ENTRY]", "PlayerController.EnterNestedAreaWithinNestedArea begin", $"CellID: {cellWithNestedArea?.CellID}\nHasNestedArea: {cellWithNestedArea?.hasNestedArea}");
		if (TurnOrchestrator.Instance != null && TurnOrchestrator.Instance.CurrentContext == TurnContext.Combat)
		{
			// CODEXLOG001_TURNLIFECYCLE: temporary combat exit block diagnostic.
			TurnDiagnosticsLogger.LogEvent("[COMBAT EXIT BLOCKED]", "PlayerController.EnterNestedAreaWithinNestedArea blocked during combat",
				$"Reason: Cannot enter another nested area during combat\n" +
				$"TargetCellID: {cellWithNestedArea?.CellID.ToString() ?? "NULL"}\n" +
				$"CurrentNestedArea: {currentNestedArea?.Name ?? "NULL"} ({currentNestedArea?.NestedAreaID.ToString() ?? "NULL"})",
				PlayerStats.Instance.CurrentPlayerCharacter);
			MessageLogManager.Instance?.Log("combat_exit_blocked");
			return;
		}
		if (cellWithNestedArea == null || !cellWithNestedArea.hasNestedArea || cellWithNestedArea.NestedArea == null)
		{
			TurnDiagnosticsLogger.LogWarning("PlayerController.EnterNestedAreaWithinNestedArea invalid target",
				$"CellID: {cellWithNestedArea?.CellID}\nHasNestedArea: {cellWithNestedArea?.hasNestedArea}");
			return;
		}

		int previousExplorationCount = TurnOrchestrator.Instance != null ? TurnOrchestrator.Instance.DiagnosticExplorationRegisteredCount : -1;
		int previousAllCharactersCount = TurnOrchestrator.Instance != null ? TurnOrchestrator.Instance.DiagnosticAllCharactersCount : -1;

		previousNestedAreaID = currentNestedArea?.NestedAreaID ?? -1;
		parentNestedAreaID = cellWithNestedArea.ParentAreaID;
		PlayerStats.Instance.ParentNestedAreaID = cellWithNestedArea.ParentAreaID;

		isInMainMap = false;
		isInNestedArea = true;

		currentNestedArea = cellWithNestedArea.NestedArea;
		playerPosition = currentNestedArea.EntrancePosition;
		nestedAreaPanel.SetActive(true);
		currentNestedArea.GetNestedMap()[playerPosition.x, playerPosition.y].isPlayerPresent = true;

		// Delegate NPCs (and, later if desired, animals/env) to coordinator
		areaEntryCoordinator.HandleOnEnterFromNestedArea(
			parentCell: cellWithNestedArea,
			nestedArea: currentNestedArea
		);

		currentNestedAreaID = currentNestedArea.NestedAreaID;
		UpdateNestedAreaStats();
		UpdateDescription();

		var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
		var orchestrator = TurnOrchestrator.Instance;
		if (orchestrator == null)
		{
			TurnDiagnosticsLogger.LogWarning("PlayerController.EnterNestedAreaWithinNestedArea missing orchestrator",
				$"NestedArea: {currentNestedArea?.Name} ({currentNestedArea?.NestedAreaID})", playerCharacter);
			return;
		}

		if (playerCharacter != null)
		{
			playerCharacter.CurrentNestedArea = currentNestedArea;
			playerCharacter.IsInNestedArea = true;
			playerCharacter.NestedMapPosition = playerPosition;
		}

		GameManager.Instance.ActiveTurnManager = true;
		PlayerStats.Instance.RegisteredInTurnManager = true;

		TurnDiagnosticsLogger.LogEvent("[AREA ENTRY]", "Nested-to-nested entry using orchestrated exploration entry",
			$"PreviousNestedAreaID: {previousNestedAreaID}\n" +
			$"NewNestedArea: {currentNestedArea?.Name} ({currentNestedArea?.NestedAreaID})\n" +
			$"Previous Exploration.Count: {previousExplorationCount}\n" +
			$"Previous CurrentAreaRoster.Count: {previousAllCharactersCount}", playerCharacter);
		orchestrator.EnterExplorationArea(currentNestedArea, playerCharacter);
		orchestrator.ValidateCharacterNestedAreas();
		orchestrator.LogAllRegisteredCharacters();
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("PlayerController.EnterNestedAreaWithinNestedArea completed",
			$"NestedArea: {currentNestedArea?.Name} ({currentNestedArea?.NestedAreaID})\n" +
			$"Previous active participants cleared by EnterExplorationArea: True\n" +
			$"Player registered in active manager: {orchestrator.DiagnosticIsCharacterRegisteredInActiveManager(playerCharacter)}\n" +
			$"Exploration.Count: {orchestrator.DiagnosticExplorationRegisteredCount}\n" +
			$"CurrentAreaRoster.Count: {orchestrator.DiagnosticAllCharactersCount}");
		// CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic.
		NestedMapDebugger.LogSnapshot(currentNestedArea, "SNAPSHOT_ENTER_NESTED_FROM_NESTED PlayerController.EnterNestedAreaWithinNestedArea completed");
	}

	private void ExitNestedArea()
	{
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[AREA EXIT]", "PlayerController.ExitNestedArea begin", $"NestedArea: {currentNestedArea?.Name} ({currentNestedArea?.NestedAreaID})");
		if (!isInNestedArea || currentNestedArea == null)
		{
			Debug.Log("Not currently in a nested area, cannot exit.");
			return;
		}
		if (TurnOrchestrator.Instance != null && TurnOrchestrator.Instance.CurrentContext == TurnContext.Combat)
		{
			// CODEXLOG001_TURNLIFECYCLE: temporary combat exit block diagnostic.
			TurnDiagnosticsLogger.LogEvent("[COMBAT EXIT BLOCKED]", "PlayerController.ExitNestedArea blocked during combat",
				$"Reason: Cannot exit nested area during combat\n" +
				$"NestedArea: {currentNestedArea?.Name ?? "NULL"} ({currentNestedArea?.NestedAreaID.ToString() ?? "NULL"})\n" +
				$"Player: {FormatAAMCharacter(PlayerStats.Instance.CurrentPlayerCharacter)}\n" +
				$"AP: {PlayerStats.Instance.ActionPoints}\n" +
				$"MP: {PlayerStats.Instance.MovePoints}",
				PlayerStats.Instance.CurrentPlayerCharacter);
			MessageLogManager.Instance?.Log("combat_exit_blocked");
			return;
		}
		// CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic.
		NestedMapDebugger.LogSnapshot(currentNestedArea, "SNAPSHOT_EXIT_AREA PlayerController.ExitNestedArea before exit handling");

		if (MessageLogManager.Instance != null)
		{
			string nestedAreaName = currentNestedArea?.Name ?? "unknown area";
			MessageLogManager.Instance.Log("exploration", "Exited", nestedAreaName);
		}

		HandleExitNestedArea();
		EndOfTurnManager.Instance.ConvertNestedTurnsToTime();

		CallTrace.Mark(this);

		TurnOrchestrator.Instance.EnterMainMap();
		PlayerStats.Instance.RegisteredInTurnManager = false;
		GameManager.Instance.ActiveTurnManager = false;
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("PlayerController.ExitNestedArea completed");
	}

	private void HandleExitNestedArea()
	{
		if (currentNestedArea.NestedAreaLevel == 0 || currentNestedArea.ParentCellID == currentNestedArea.MainMapCellID)
		{
			int exitedNestedAreaID = currentNestedArea.NestedAreaID;
			foreach (var npcGroup in currentNestedArea.GetNPCGroups())
				npcManager.UpdateNPCGroupStatus(npcGroup);

			var currentCell = currentNestedArea.GetCellAtPosition(nestedMapPosition);
			currentCell.LastVisited = TimeManager.Instance.currentDay;

			isInNestedArea = false;
			isInMainMap = true;

			playerPosition = mapGenerator.GetCellCoordinatesContainingNestedArea(currentNestedArea);
			var mainMapCell = mapGenerator.GetCell(playerPosition);
			currentRegion = mainMapCell.RegionNumber;
			previousCellID = currentCellID;
			previousPlayerCell = currentPlayerCell;
			currentPlayerCell = mainMapCell;
			currentCellID = mainMapCell.CellID;
			previousNestedAreaID = exitedNestedAreaID;
			currentNestedAreaID = 0;
			parentNestedAreaID = 0;

			currentNestedArea.HandlePlayerExit(mapGenerator);
			currentNestedArea = null;
			PlayerStats.Instance.UpdateCurrentNestedArea(null);
			PlayerStats.Instance.UpdateCurrentNestedAreaID(0);
			PlayerStats.Instance.UpdateParentNestedAreaID(0);
			PlayerStats.Instance.UpdateIsInAreas(false, true);

			nestedMapPosition = Vector2Int.zero;
			previousNestedMapPosition = Vector2Int.zero;

			UpdatePlayerStatsInstance();
			UpdateDescription();

			CallTrace.Mark(this);
			Debug.Log($"Exited nested area to position {playerPosition}.");
		}
		else
		{
			Debug.Log("Cannot exit nested area. You are not at the top level.");
		}
	}

	private float CalculateTurnDuration(float speed)
	{
		return Mathf.Max(0.1f, 1.0f / speed);
	}

	public void LeaveNestedAreaToParent()
	{
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[AREA EXIT]", "PlayerController.LeaveNestedAreaToParent begin", $"CurrentNestedArea: {currentNestedArea?.Name} ({currentNestedArea?.NestedAreaID})");
		if (!isInNestedArea || currentNestedArea == null)
		{
			Debug.LogWarning("Player is not currently in a nested area.");
			return;
		}
		if (TurnOrchestrator.Instance != null && TurnOrchestrator.Instance.CurrentContext == TurnContext.Combat)
		{
			// CODEXLOG001_TURNLIFECYCLE: temporary combat exit block diagnostic.
			TurnDiagnosticsLogger.LogEvent("[COMBAT EXIT BLOCKED]", "PlayerController.LeaveNestedAreaToParent blocked during combat",
				$"Reason: Cannot leave nested area to parent during combat\n" +
				$"CurrentNestedArea: {currentNestedArea?.Name ?? "NULL"} ({currentNestedArea?.NestedAreaID.ToString() ?? "NULL"})\n" +
				$"Player: {FormatAAMCharacter(PlayerStats.Instance.CurrentPlayerCharacter)}",
				PlayerStats.Instance.CurrentPlayerCharacter);
			MessageLogManager.Instance?.Log("combat_exit_blocked");
			return;
		}

		int parentNestedAreaID = PlayerStats.Instance.FacingCellParentID;
		var parentNestedArea = mapGenerator.FindNestedAreaBasedOnNestedAreaID(parentNestedAreaID);
		if (parentNestedArea == null)
		{
			Debug.LogWarning($"Parent nested area with ID {parentNestedAreaID} not found.");
			return;
		}

		var entrancePosition = PlayerStats.Instance.FacingCellCoordinates;
		MoveToNestedAreaPosition(parentNestedAreaID, entrancePosition);

		currentNestedArea = parentNestedArea;
		nestedMapPosition = entrancePosition;

		mapDisplayUI.UpdateNestedMapDisplay(parentNestedArea);
		UpdateNestedAreaStats();
		UpdateDescription();
		UpdateAdaptiveActionMenu();

		CallTrace.Mark(this);
		Debug.Log("Player returned to the parent nested area.");
		endOfTurnManager.ConvertNestedTurnsToTime();
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("PlayerController.LeaveNestedAreaToParent completed", $"ParentNestedArea: {currentNestedArea?.Name} ({currentNestedArea?.NestedAreaID})");
	}

	private void MoveToNestedAreaPosition(int nestedAreaID, Vector2Int position)
	{
		var nestedArea = mapGenerator.FindNestedAreaBasedOnNestedAreaID(nestedAreaID);
		if (nestedArea == null || !nestedArea.IsValidPosition(position))
		{
			Debug.LogError($"Invalid nested area ID {nestedAreaID} or position {position}.");
			return;
		}

		if (isInNestedArea)
		{
			previousCellID = currentCellID;
			previousPlayerCell = currentPlayerCell;
			nestedMapPosition = position;
			nestedArea.UpdatePlayerPosition(position);
			currentPlayerCell = nestedArea.GetCellAtPosition(position);
			currentCellID = currentPlayerCell != null ? currentPlayerCell.CellID : 0;
			npcManager.UpdateNPCsInNestedArea(nestedArea);
		}
		else
		{
			MovePlayerToMainMap(position);
		}

		UpdateDescription();
		UpdateAdaptiveActionMenu();
	}

	private void MovePlayerToMainMap(Vector2Int position)
	{
		previousCellID = currentCellID;
		previousPlayerCell = currentPlayerCell;
		playerPosition = position;
		var cell = mapGenerator.map[position.x, position.y];
		cell.isPlayerPresent = true;
		cell.nestedAreaCanBeSeen = true;

		currentPlayerCell = cell;
		currentCellID = cell.CellID;
		currentRegion = cell.RegionNumber;
		endOfTurnManager.EndTurn();

		if (cell?.hasNestedArea == true) cell.nestedAreaCanBeSeen = true;

		if (cell?.isNPCGroupPresent == true)
		{
			var npcGroup = npcManager.FindNPCGroupAtPosition(cell.Coordinates);
			if (npcGroup != null) npcManager.PlaceNPCs(cell.NestedArea, npcGroup);
		}

		Debug.Log($"Moved to position {position} within main map.");
	}

	private void AddTurnProgress(float progress)
	{
		endOfTurnManager.AddTurnProgress(progress);
	}

	private bool CompleteExplorationTurnForTimeCostingAction(string source, float actionDuration)
	{
        if (!IsExplorationTurnContext())
        {
            return false;
        }

        if (!TryGetEndTurnAvailability(out bool canCompleteTurn, out string reason) || !canCompleteTurn)
        {
            TurnDiagnosticsLogger.LogWarning("PlayerController.CompleteExplorationTurnForTimeCostingAction rejected",
                $"Source: {source}\n" +
                $"Reason: {reason}\n" +
                $"CurrentContext: {TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "NULL"}\n" +
                $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}\n" +
                $"PlayerStats.MovePoints: {PlayerStats.Instance.MovePoints}",
                PlayerStats.Instance.CurrentPlayerCharacter);
            return false;
        }

        TurnDiagnosticsLogger.LogEvent("[ACTION COST]", "PlayerController.CompleteExplorationTurnForTimeCostingAction",
            $"Source: {source}\n" +
            $"CostCategory: TimeCost\n" +
            $"ActionDuration: {actionDuration}\n" +
            $"CurrentContext: {TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "NULL"}\n" +
            $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}\n" +
            $"PlayerStats.MovePoints: {PlayerStats.Instance.MovePoints}",
            PlayerStats.Instance.CurrentPlayerCharacter);

		endOfTurnManager.AddTurnProgress(actionDuration);
		return TryCompletePlayerTurnFromPlayerController(source);
	}

	#endregion

	#region Nested Area Helpers
	
		private void ApplyOverallFertilityAdjustment(Cell parentCell)
		{
			if (parentCell == null)
			{
				GameDebugger.Instance.LogError("ApplyOverallFertilityAdjustment: parentCell is NULL. Aborting.");
				return;
			}

			int overallAdjustment = parentCell.OverallFertilityAdjustment;

			if (currentNestedArea == null)
			{
				GameDebugger.Instance.LogError("ApplyOverallFertilityAdjustment: currentNestedArea is NULL. Aborting.");
				return;
			}

			Cell[,] nestedMap = currentNestedArea.GetNestedMap();

			if (nestedMap == null)
			{
				GameDebugger.Instance.LogError("ApplyOverallFertilityAdjustment: nestedMap is NULL. Aborting.");
				return;
			}

			GameDebugger.Instance.LogInfo($"Applying overall fertility adjustment of {overallAdjustment} to NestedArea ID {currentNestedArea.NestedAreaID}");

			int width = nestedMap.GetLength(0);
			int height = nestedMap.GetLength(1);

			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					Cell nestedCell = nestedMap[x, y];
					if (nestedCell == null)
					{
						GameDebugger.Instance.LogWarning($"ApplyOverallFertilityAdjustment: Null cell at [{x},{y}] skipped.");
						continue;
					}

					int oldFertility = nestedCell.FertilityValue;
					int newFertility = Mathf.Clamp(oldFertility + overallAdjustment, 0, 100);

					nestedCell.FertilityValue = newFertility;
					nestedCell.OverallFertilityAdjustment += overallAdjustment;

					if (newFertility == 0)
					{
						nestedCell.isFertile = false;

						if (nestedCell.Terrain == TerrainType.Land)
						{
							nestedCell.Terrain = TerrainType.Dirt;
							GameDebugger.Instance.LogInfo($"Cell {nestedCell.CellID} turned to Dirt due to fertility drop.");
						}
					}

					GameDebugger.Instance.LogInfo(
						$"Adjusted fertility for Cell {nestedCell.CellID}: {oldFertility} → {newFertility} (Δ {overallAdjustment})");
				}
			}
		}

	#endregion


    #region Player Stats Update
    private void UpdatePlayerStatsInstance()
    {
        UpdateAllPlayerStats();
    }

    private void UpdateNestedAreaStats()
    {
        UpdateAllPlayerStats();
    }

    private void UpdateAllPlayerStats()
    {
        // Update player positions
        PlayerStats.Instance.UpdatePosition(playerPosition);
        PlayerStats.Instance.UpdatePreviousPosition(previousPosition);

        // Update nested area position if the player is in a nested area
        if (isInNestedArea)
        {
            PlayerStats.Instance.UpdateNestedAreaPosition(nestedMapPosition);
            PlayerStats.Instance.UpdateCurrentNestedArea(currentNestedArea);
        }

        // Update main map and region if the player is on the main map
        if (isInMainMap)
        {
            PlayerStats.Instance.UpdateMainMapPosition(mainMapPosition, currentRegion);
        }

        // Update current cell and cell IDs
        PlayerStats.Instance.UpdateCurrentCell(currentPlayerCell);
        PlayerStats.Instance.UpdateCurrentCellID(currentCellID);

        // Update area state flags
        PlayerStats.Instance.UpdateIsInAreas(isInNestedArea, isInMainMap);

        // Update player facing direction
        PlayerStats.Instance.UpdatePlayerFacing(currentDirection);

        // Update nested area IDs
        PlayerStats.Instance.UpdatePreviousNestedAreaID(previousNestedAreaID);
        PlayerStats.Instance.UpdateCurrentNestedAreaID(currentNestedAreaID);
        PlayerStats.Instance.UpdateParentNestedAreaID(parentNestedAreaID);
    }

    #endregion


    #region Visibility
    private void UpdateFacingDirection()
    {
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) currentDirection = Direction.North;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) currentDirection = Direction.South;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) currentDirection = Direction.West;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) currentDirection = Direction.East;

        UpdateAdaptiveActionMenu();
    }

    private Vector2Int GetDirectionVector(Direction direction)
    {
        switch (direction)
        {
            case Direction.North: return new Vector2Int(0, 1);
            case Direction.East: return new Vector2Int(1, 0);
            case Direction.South: return new Vector2Int(0, -1);
            case Direction.West: return new Vector2Int(-1, 0);
            default: return Vector2Int.zero;
        }
    }
    #endregion

    #region Description Updates
    private void UpdateDescriptionText(string description)
    {
        if (descriptionText != null) descriptionText.text = description;
    }

    public void UpdateDescriptionAfterInteraction(object sender, string description)
    {
        description = $"You interacted with something.";
        UpdateDescriptionText(description);
        OnDescriptionUpdate?.Invoke(this, description);
    }

    void UpdateDescription()
    {
        if (!IsValidPosition(playerPosition, isInNestedArea)) return;

        if (isInMainMap)
        {
            UpdateMainMapDescription();
        }
        else if (isInNestedArea)
        {
            UpdateNestedAreaDescription();
        }
    }

    private void UpdateMainMapDescription()
    {
        // Get the current cell
        var currentCell = mapGenerator.map[playerPosition.x, playerPosition.y];

        // Get the weather description from the WeatherType enum
        string weatherDescription = GetWeatherDescription(currentCell.CurrentWeather);

        // Construct the description with both terrain and weather
        string description = $"You are in a {currentCell.Terrain}. It is {weatherDescription}.";

        // Check if the current cell has a dungeon, landmark, or cave
        if (currentCell.HasDungeon || currentCell.HasLandmark || currentCell.HasCave)
        {
            description += " There is something of interest here.";
        }

        // Add terrain descriptions in cardinal directions
        description += $" To the North, there is {GetTerrainDescription(playerPosition + Vector2Int.up)}.";
        description += $" To the South, there is {GetTerrainDescription(playerPosition + Vector2Int.down)}.";
        description += $" To the East, there is {GetTerrainDescription(playerPosition + Vector2Int.right)}.";
        description += $" To the West, there is {GetTerrainDescription(playerPosition + Vector2Int.left)}.";

        // Update the description text
        UpdateDescriptionText(description);
    }


    private void UpdateNestedAreaDescription()
    {
        // Get the current cell the player is in
        var currentCell = currentNestedArea.GetCellAtPosition(playerPosition);

        // Get the weather description for the current cell
        string weatherDescription = GetWeatherDescription(currentCell.CurrentWeather);

        // Construct the description of the current position
        string description = $"You are in a {currentCell.Terrain}. It is {weatherDescription}.";

        // Get the position the player is facing
        Vector2Int facingPosition = playerPosition + GetDirectionVector(currentDirection);

        // Check if the facing position is valid in the nested area
        if (currentNestedArea.IsValidPosition(facingPosition))
        {
            // Get the cell the player is facing
            Cell facingCell = currentNestedArea.GetCellAtPosition(facingPosition);

            // Get the description of the terrain in front
            string terrainInFrontDescription = facingCell.Terrain.ToString();

            // Check if there are items on the ground
            if (facingCell.Items.Any())
            {
                // If there are items, mention them in the description
                description += $" In front of you is {terrainInFrontDescription}, there is something on the ground.";
            }
            else
            {
                // No items, just describe the terrain and any objects/creatures
                string objectInFrontDescription = GetObjectInFrontDescription(facingCell);
                description += $" In front of you, there is a {objectInFrontDescription}.";
            }
        }

        // Update the description text
        UpdateDescriptionText(description);
    }

    private string GetObjectInFrontDescription(Cell facingCell)
    {
        if (facingCell.Objects.Any())
        {
            var firstObject = facingCell.Objects.First();
            return firstObject.Name;
        }
        else if (facingCell.Animals.Any())
        {
            var firstAnimal = facingCell.Animals.First();
            return firstAnimal.Name;
        }
        else if (facingCell.NPCs.Any())
        {
            var firstNPC = facingCell.NPCs.First();
            return firstNPC.Name;
        }
        else
        {
            return facingCell.Terrain.ToString();
        }
    }

    string GetTerrainDescription(Vector2Int position)
    {
        if (isInNestedArea)
        {
            if (currentNestedArea.IsValidPosition(position))
            {
                return currentNestedArea.GetNestedMap()[position.x, position.y].Terrain.ToString();
            }
        }
        else
        {
            if (IsValidPosition(position, isInNestedArea))
            {
                return mapGenerator.map[position.x, position.y].Terrain.ToString();
            }
        }
        return "Nothing";
    }

    private string GetWeatherDescription(WeatherType weatherType)
    {
        // Convert the WeatherType enum to a readable string
        switch (weatherType)
        {
            case WeatherType.Sunny:
                return "sunny";
            case WeatherType.Cloudy:
                return "cloudy";
            case WeatherType.Rainy:
                return "rainy";
            case WeatherType.Snowy:
                return "snowy";
            // Add more cases as necessary based on your enum
            default:
                return "clear";
        }
    }
    #endregion



    #region Nested Area Toggle
    void ToggleNestedArea()
    {
        if (isInNestedArea) ExitNestedArea();
        else TryEnterOrGenerateNestedArea();

        UpdateToggleButtonState();
    }

    private void UpdateToggleButtonState()
    {
        toggleNestedAreaButtonText.text = isInNestedArea ? "Exit" : "Enter";
    }
    #endregion

    #region Passability and Validity
    bool IsCellPassable(Vector2Int position)
    {
        if (isInNestedArea && currentNestedArea != null)
        {
            return IsPassableInNestedArea(position);
        }
        else
        {
            return IsPassableInMainMap(position);
        }
    }

    private bool IsPassableInNestedArea(Vector2Int position)
    {
        if (!currentNestedArea.IsValidPosition(position))
        {
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogEvent("[WARNING]", "Nested-area out-of-bounds movement treated as blocked",
                $"Position: {position}\nNestedArea: {currentNestedArea?.Name} ({currentNestedArea?.NestedAreaID})");
            return false;
        }

        bool terrainPassable = currentNestedArea.IsPassable(position);
        bool objectsPassable = currentNestedArea.GetObjectsAtPosition(position).All(obj => obj.IsPassable);

        foreach (var obj in currentNestedArea.GetObjectsAtPosition(position))
        {
            Debug.Log($"Object {obj.Name} at {position} is passable: {obj.IsPassable}");
        }

        return terrainPassable && objectsPassable;
    }

    private bool IsPassableInMainMap(Vector2Int position)
    {
        bool terrainPassable = mapGenerator.map[position.x, position.y].isPassable;
        bool objectsPassable = mapGenerator.map[position.x, position.y].Objects.All(obj => obj.IsPassable);

        foreach (var obj in mapGenerator.map[position.x, position.y].Objects)
        {
            Debug.Log($"Object {obj.Name} at {position} is passable: {obj.IsPassable}");
        }

        return terrainPassable && objectsPassable;
    }

    bool IsValidPosition(Vector2Int position, bool isInNestedArea)
    {
        if (isInNestedArea && currentNestedArea != null)
        {
            return currentNestedArea.IsValidPosition(position);
        }
        else
        {
            return position.x >= 0 && position.x < mapGenerator.width &&
                   position.y >= 0 && position.y < mapGenerator.height;
        }
    }
    #endregion

    #region Adaptive Action Menu
    public void UpdateAdaptiveActionMenu()
    {
        EndOfTurnManager.Instance?.RefreshWaitButtonPresentation();

        // If we're not in a nested area, no actions should be generated
        if (!isInNestedArea)
        {
            ClearAdaptiveActionMenu();
            // CODEXLOG003_ACTIONS_AAM: temporary AAM refresh diagnostic.
            ActionAAMDiagnosticsLogger.LogEvent("[AAM REFRESH]", "UpdateAdaptiveActionMenu skipped outside nested area",
                $"CurrentTab: {PlayerStats.Instance.AdaptiveActionMenuPanel}\n" +
                $"PlayerPosition: {playerPosition}\n" +
                $"CurrentNestedAreaExists: {currentNestedArea != null}");
            return;
        }

        ClearAdaptiveActionMenu();

        Cell[,] currentMap = isInNestedArea ? currentNestedArea.GetNestedMap() : mapGenerator.map;
        Vector2Int facingDirection = GetDirectionVector(currentDirection);
        Vector2Int facingPosition = playerPosition + facingDirection;
        bool hasCurrentNestedArea = currentNestedArea != null;
        bool facingPositionValid = IsValidPosition(facingPosition, isInNestedArea);

        // CODEXLOG003_ACTIONS_AAM: temporary AAM refresh diagnostic.
        ActionAAMDiagnosticsLogger.LogEvent("[AAM REFRESH]", "UpdateAdaptiveActionMenu",
            $"CurrentTab: {PlayerStats.Instance.AdaptiveActionMenuPanel}\n" +
            $"PlayerPosition: {playerPosition}\n" +
            $"FacingDirection: {currentDirection}\n" +
            $"FacingDirectionVector: {facingDirection}\n" +
            $"FacingPosition: {facingPosition}\n" +
            $"CurrentNestedAreaExists: {hasCurrentNestedArea}\n" +
            $"FacingCellFound: {facingPositionValid}");

        if (facingPositionValid)
        {
            Cell facingCell = currentMap[facingPosition.x, facingPosition.y];
            PlayerStats.Instance.UpdateFacingCell(facingCell);
            facingCellPosition = facingCell.Coordinates;

            HandleInteractableObjectsInFacingCell(facingCell);
            AddEnvironmentalActions(facingCell);
        }

        AddTurnControlActions();
		
		CallTrace.Mark(this);
    }


    private void HandleInteractableObjectsInFacingCell(Cell facingCell)
    {
        // CODEXLOG003_ACTIONS_AAM: temporary faced-cell scan diagnostic.
        ActionAAMDiagnosticsLogger.LogEvent("[FACED CELL]", "Facing cell contents",
            $"CellID: {facingCell?.CellID.ToString() ?? "NULL"}\n" +
            $"CellPosition: {facingCell?.Coordinates.ToString() ?? "NULL"}\n" +
            $"Objects count: {facingCell?.Objects?.Count.ToString() ?? "NULL"}\n" +
            $"Items count: {facingCell?.Items?.Count.ToString() ?? "NULL"}\n" +
            $"Animals count: {facingCell?.Animals?.Count.ToString() ?? "NULL"}\n" +
            $"NPCs count: {facingCell?.NPCs?.Count.ToString() ?? "NULL"}");

        var processedProviders = new HashSet<string>();
        ProcessInteractableObjects((facingCell.Objects ?? Enumerable.Empty<IInteractable>()).OfType<IInteractable>(), "Objects", processedProviders);
        ProcessInteractableObjects((facingCell.Items ?? Enumerable.Empty<Item>()).OfType<IInteractable>(), "Items", processedProviders);
        ProcessInteractableObjects((facingCell.Animals ?? Enumerable.Empty<Animal>()).OfType<IInteractable>(), "Animals", processedProviders);
        ProcessInteractableObjects((facingCell.NPCs ?? Enumerable.Empty<NPC>()).OfType<IInteractable>(), "NPCs", processedProviders);
    }

    private void ProcessInteractableObjects(IEnumerable<IInteractable> interactables, string sourceCollection, HashSet<string> processedProviders)
    {
        var playerInventory = PlayerInventory.Instance;
        var addedInteractions = new HashSet<string>(); // Set to keep track of added interactions

        foreach (var interactable in interactables)
        {
            if (interactable == null) continue;

            RepairAAMProviderAreaStateIfSafe(interactable, sourceCollection);

            string providerKey = GetAAMProviderKey(interactable);
            if (!processedProviders.Add(providerKey))
            {
                // CODEXLOG003_ACTIONS_AAM: temporary duplicate-provider diagnostic.
                ActionAAMDiagnosticsLogger.LogEvent("[PROVIDER SKIPPED]", "Duplicate AAM provider skipped",
                    $"Provider: {FormatAAMProvider(interactable)}\n" +
                    $"SourceCollection: {sourceCollection}\n" +
                    $"ProviderKey: {providerKey}");
                continue;
            }

            var availableInteractions = interactable.GetAvailableInteractions(playerInventory).ToList();
            Debug.Log($"Found {availableInteractions.Count} available interactions for {interactable.Name}");

            // CODEXLOG003_ACTIONS_AAM: temporary action provider diagnostic.
            ActionAAMDiagnosticsLogger.LogEvent("[PROVIDER FOUND]", "AAM action provider found",
                $"Provider: {FormatAAMProvider(interactable)}\n" +
                $"SourceCollection: {sourceCollection}\n" +
                $"AvailableInteractionsReturned: {availableInteractions.Count}");

            foreach (var interaction in availableInteractions)
            {
                if (!IsInteractionVisibleInCurrentPanel(interaction))
                {
                    // CODEXLOG003_ACTIONS_AAM: temporary tab filtering diagnostic.
                    ActionAAMDiagnosticsLogger.LogEvent("[ACTION HIDDEN]", "Action hidden by AAM tab filter",
                        $"ActionName: {interaction.Name}\n" +
                        $"InteractionType: {interaction.Type}\n" +
                        $"CurrentTab: {PlayerStats.Instance.AdaptiveActionMenuPanel}\n" +
                        $"Provider: {FormatAAMProvider(interactable)}\n" +
                        $"SourceCollection: {sourceCollection}");
                    continue;
                }

                ActionCostProfile costProfile = ActionCostProfileResolver.BuildForInteraction(interaction, IsCombatTurnContext());
                string buttonName = ActionCostProfileResolver.BuildActionButtonLabel(interaction.Name, interactable.Name, costProfile);

                // Check if this interaction has already been added
                if (!addedInteractions.Contains(buttonName))
                {
                    // Add the button name to the set
                    addedInteractions.Add(buttonName);

                    // Create the button
                    // CODEXLOG003_ACTIONS_AAM: temporary button creation diagnostic.
                    ActionAAMDiagnosticsLogger.LogEvent("[BUTTON CREATED]", "AAM interaction button created",
                        $"ButtonLabel: {buttonName}\n" +
                        $"ActionName: {interaction.Name}\n" +
                        $"ActionPointCost: {interaction.ActionPointCost}\n" +
                        $"ResolvedCostLabel: {costProfile.CostLabel}\n" +
                        $"InteractionType: {interaction.Type}\n" +
                        $"CurrentTab: {PlayerStats.Instance.AdaptiveActionMenuPanel}\n" +
                        $"Provider: {FormatAAMProvider(interactable)}\n" +
                        $"SourceCollection: {sourceCollection}");
                    CreateActionButton(buttonName, interaction.ActionPointCost, () => ExecutePlayerAction(interaction, interactable));
                }
            }
        }
    }

    private void AddEnvironmentalActions(Cell facingCell)
    {
        var playerInventory = PlayerInventory.Instance;
        var addedActions = new HashSet<string>(); // Set to keep track of added actions

        var availableEnvironmentalActions = actionManager.GetAvailableEnvironmentalActions(facingCell, playerInventory);
        foreach (var action in availableEnvironmentalActions)
        {
            if (!IsInteractionTypeVisibleInCurrentPanel(action.Type))
            {
                ActionAAMDiagnosticsLogger.LogEvent("[ACTION HIDDEN]", "Environmental action hidden by AAM tab filter",
                    $"ActionName: {action.Name}\n" +
                    $"InteractionType: {action.Type}\n" +
                    $"CurrentTab: {PlayerStats.Instance.AdaptiveActionMenuPanel}\n" +
                    $"TargetCell: {facingCell?.Coordinates.ToString() ?? "NULL"}");
                continue;
            }

            ActionCostProfile costProfile = ActionCostProfileResolver.BuildForEnvironmentalAction(action, IsCombatTurnContext());
            string buttonName = ActionCostProfileResolver.BuildActionButtonLabel(action.Name, string.Empty, costProfile);

            // Check if this action has already been added
            if (!addedActions.Contains(buttonName))
            {
                // Add the action name to the set
                addedActions.Add(buttonName);

                // Create the button
                // CODEXLOG003_ACTIONS_AAM: temporary environmental button diagnostic.
                ActionAAMDiagnosticsLogger.LogEvent("[BUTTON CREATED]", "AAM environmental button created",
                    $"ButtonLabel: {buttonName}\n" +
                    $"ActionName: {action.Name}\n" +
                    $"ActionPointCost: {action.ActionPointCost}\n" +
                    $"ResolvedCostLabel: {costProfile.CostLabel}\n" +
                    $"InteractionType: {action.Type}\n" +
                    $"CurrentTab: {PlayerStats.Instance.AdaptiveActionMenuPanel}\n" +
                    $"TargetCell: {facingCell?.Coordinates.ToString() ?? "NULL"}");
                CreateActionButton(buttonName, action.ActionPointCost, () => ExecuteEnvironmentalAction(action, facingCell));
            }
        }
    }

    private void AddTurnControlActions()
    {
        LogEndTurnActionVisibility(false, "WaitButtonOwnsTurnControl");
    }


    public void CreateActionButton(string buttonText, int actionPointCost, UnityAction baseOnClickAction)
    {
        GameObject buttonGO = Instantiate(actionButtonPrefab, adaptiveActionMenu.transform);
        Button button = buttonGO.GetComponent<Button>();
        TextMeshProUGUI buttonTextComponent = button.GetComponentInChildren<TextMeshProUGUI>();

        if (buttonTextComponent != null)
        {
            buttonTextComponent.text = buttonText;
        }
        else
        {
            Debug.LogWarning("TextMeshProUGUI component not found on button or its children.");
        }

        UnityAction extendedOnClickAction = () =>
		{
			UpdatePlayerStatsInstance();

			/*
			TEMP DISABLED:
			AP spending, pending actions, and turn completion should not happen here.

			Reason:
			CreateActionButton() is only the UI wrapper. The actual action execution methods
			already handle AP checks/deductions. Keeping this block active can cause actions
			to be charged twice, or fail after AP is spent before the action runs.

			if (PlayerStats.Instance.ActionPoints >= actionPointCost)
			{
				PlayerStats.Instance.ActionPoints -= actionPointCost;
				baseOnClickAction.Invoke();

				if (PlayerStats.Instance.ActionPoints == 0)
				{
					turnOrchestrator.PlayerTurnCompleted();
				}
			}
			else
			{
				PlayerStats.Instance.PendingActionPointsCost = actionPointCost - PlayerStats.Instance.ActionPoints;
				PlayerStats.Instance.ActionPoints = 0;
				PlayerStats.Instance.HasPendingAction = true;

				turnOrchestrator.PlayerTurnCompleted();
			}
			*/

			baseOnClickAction.Invoke();

			UpdateAdaptiveActionMenu();
		};

        button.onClick.AddListener(extendedOnClickAction);
    }

    private void ClearAdaptiveActionMenu()
    {
        foreach (Transform child in adaptiveActionMenu.transform)
        {
            Destroy(child.gameObject);
        }

        if (!ShouldPreserveInteractionTargetOnCurrentPanel())
        {
            ClearInteractingWithTarget("ClearAdaptiveActionMenu");
        }
    }

    private IEnumerable<IInteraction> FilterInteractionsByPanel(IEnumerable<IInteraction> interactions)
    {
        return interactions.Where(IsInteractionVisibleInCurrentPanel);
    }

    private bool IsInteractionVisibleInCurrentPanel(IInteraction interaction)
    {
        return interaction != null && IsInteractionTypeVisibleInCurrentPanel(interaction.Type);
    }

    private bool IsInteractionTypeVisibleInCurrentPanel(InteractionType interactionType)
    {
        var panelType = PlayerStats.Instance.AdaptiveActionMenuPanel;

        switch (panelType)
        {
            case AdapativeActionMenu.Combat:
                return interactionType == InteractionType.Combat;
            case AdapativeActionMenu.Special:
                return interactionType == InteractionType.Special;
            default:
                // For all other panels, return everything except Combat and Special
                return interactionType != InteractionType.Combat &&
                       interactionType != InteractionType.Special;
        }
    }

    private void SetInteractingWithTargetFromSelection(IInteraction interaction, IInteractable entity)
    {
        if (!(entity is Character character))
        {
            return;
        }

        PlayerStats.Instance.InteractingWithID = character.IInteractableID;

        ActionAAMDiagnosticsLogger.LogEvent("[INTERACTION TARGET]", "Interaction target set from selected AAM action",
            $"ActionName: {interaction?.Name ?? "NULL"}\n" +
            $"Target: {FormatAAMCharacter(character)}\n" +
            $"KeyboardPanel: {PlayerStats.Instance.KeyboardPanel}\n" +
            $"CurrentTab: {PlayerStats.Instance.AdaptiveActionMenuPanel}");
    }

    private bool ShouldPreserveInteractionTargetOnCurrentPanel()
    {
        return PlayerStats.Instance.KeyboardPanel == KeyboardPanel.Dialogue ||
               PlayerStats.Instance.KeyboardPanel == KeyboardPanel.Trade;
    }

    public void ClearInteractingWithTarget(string source)
    {
        if (PlayerStats.Instance.InteractingWithID == 0)
        {
            return;
        }

        ActionAAMDiagnosticsLogger.LogEvent("[INTERACTION TARGET]", "Interaction target cleared",
            $"Source: {source}\n" +
            $"PreviousTargetID: {PlayerStats.Instance.InteractingWithID}\n" +
            $"KeyboardPanel: {PlayerStats.Instance.KeyboardPanel}");

        PlayerStats.Instance.InteractingWithID = 0;
    }

    // CODEXLOG003_ACTIONS_AAM: temporary AAM provider diagnostic helper.
    private string GetAAMProviderKey(IInteractable interactable)
    {
        if (interactable == null) return "null";
        if (interactable.IInteractableID != 0)
        {
            return $"{interactable.GetType().FullName}:{interactable.IInteractableID}";
        }

        return $"ref:{RuntimeHelpers.GetHashCode(interactable)}";
    }

    // CODEXLOG003_ACTIONS_AAM: temporary AAM provider diagnostic helper.
    private string FormatAAMProvider(IInteractable interactable)
    {
        if (interactable == null) return "NULL";
        return $"{interactable.Name} [{interactable.IInteractableID}] ({interactable.GetType().Name})";
    }
    #endregion

    #region Player Action Handling

    public void DeductActionPoints(int amount)
    {
        PlayerStats.Instance.ActionPoints -= amount;
        if (IsCombatTurnContext() && PlayerStats.Instance.CurrentPlayerCharacter != null)
        {
            PlayerStats.Instance.CurrentPlayerCharacter.ActionPoints = PlayerStats.Instance.ActionPoints;
        }
        LogCombatResourceState("[AP SPEND]", "PlayerController.DeductActionPoints",
            $"Amount: {amount}\nPlayerStats.ActionPoints after spend: {PlayerStats.Instance.ActionPoints}");
        RefreshCombatStatusUI();

        if (IsCombatTurnContext())
        {
            PostPlayerActionTurnMaintenance("DeductActionPoints", "AP spend");
            return;
        }

        // Check if ActionPoints are depleted
        if (PlayerStats.Instance.ActionPoints <= 0)
        {
            // If the player has any pending action, handle it
            if (PlayerStats.Instance.HasPendingAction)
            {
                // Handle the pending action (if any)
                HandlePendingAction();
            }
            else
            {
                // If no pending action, complete the player's turn
                TryCompletePlayerTurnFromPlayerController("DeductActionPoints");
            }
        }
    }

    public void DeductMovePoints(int amount)
    {
        PlayerStats.Instance.MovePoints -= amount;
        if (IsCombatTurnContext() && PlayerStats.Instance.CurrentPlayerCharacter != null)
        {
            PlayerStats.Instance.CurrentPlayerCharacter.MovePoints = PlayerStats.Instance.MovePoints;
        }
        LogCombatResourceState("[MP SPEND]", "PlayerController.DeductMovePoints",
            $"Amount: {amount}\nPlayerStats.MovePoints after spend: {PlayerStats.Instance.MovePoints}");
        RefreshCombatStatusUI();

        if (IsCombatTurnContext())
        {
            if (PlayerStats.Instance.MovePoints <= 0)
            {
                ShowNotEnoughMPFeedback("move farther this turn");
            }
            PostPlayerActionTurnMaintenance("DeductMovePoints", "movement");
        }
    }

    private void HandlePendingAction()
    {
        if (IsCombatTurnContext())
        {
            // CODEXLOG003_ACTIONS_AAM: temporary combat pending-action diagnostic.
            ActionAAMDiagnosticsLogger.LogEvent("[PENDING ACTION]", "Pending action ignored during combat",
                $"PendingActionPointsCost: {PlayerStats.Instance.PendingActionPointsCost}\n" +
                $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}\n" +
                $"CurrentPlayerCharacter.ActionPoints: {PlayerStats.Instance.CurrentPlayerCharacter?.ActionPoints.ToString() ?? "NULL"}\n" +
                $"CurrentPlayerCharacter.InTurn: {PlayerStats.Instance.CurrentPlayerCharacter?.InTurn.ToString() ?? "NULL"}");

            PlayerStats.Instance.PendingActionPointsCost = 0;
            PlayerStats.Instance.HasPendingAction = false;
            return;
        }

        if (PlayerStats.Instance.PendingActionPointsCost <= PlayerStats.Instance.ActionPoints)
        {
            PlayerStats.Instance.ActionPoints -= PlayerStats.Instance.PendingActionPointsCost;
            PlayerStats.Instance.PendingActionPointsCost = 0;
            PlayerStats.Instance.HasPendingAction = false;

            // If the player still has action points, they can continue
			if (PlayerStats.Instance.ActionPoints > 0)
			{
				UpdateAdaptiveActionMenu();
			}
			else
			{
				TryCompletePlayerTurnFromPlayerController("HandlePendingAction completed");
			}
        }
        else
        {
            // If the player still doesn't have enough AP to complete the pending action
            PlayerStats.Instance.PendingActionPointsCost -= PlayerStats.Instance.ActionPoints;
            PlayerStats.Instance.ActionPoints = 0;
			TryCompletePlayerTurnFromPlayerController("HandlePendingAction deferred");
        }
    }

    private void ExecutePlayerAction(IInteraction interaction, IInteractable entity)
    {
        if (!CanAcceptPlayerTurnInput("ExecutePlayerAction"))
        {
            return;
        }

        int actionPointCost = interaction.ActionPointCost;
        bool characterOwnedCombatAction = CombatActionUsesCharacterActionPoints(interaction);
        ActionCostProfile actionCostProfile = ActionCostProfileResolver.BuildForInteraction(interaction, IsCombatTurnContext());

        if (PlayerStats.Instance.ActionPoints >= actionPointCost)
        {
            Character playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
            if (interaction.Type == InteractionType.Combat &&
                (playerCharacter == null || !playerCharacter.IsCombatActorAvailable()))
            {
                GameDebugger.Instance.LogWarning("PlayerController.ExecutePlayerAction rejected combat action because player character is unavailable.");
                CombatActionResolutionDiagnosticsLogger.LogWarning("PlayerController.ExecutePlayerAction rejected combat action because player attacker is unavailable",
                    $"ActionName={interaction?.Name ?? "NULL"}\n" +
                    $"Target={(entity as Character)?.Name ?? entity?.Name ?? "NULL"}\n" +
                    $"PlayerIsAlive={playerCharacter?.IsAlive.ToString() ?? "NULL"}\n" +
                    $"PlayerIsActive={playerCharacter?.IsActive.ToString() ?? "NULL"}\n" +
                    $"PlayerInCombat={playerCharacter?.InCombat.ToString() ?? "NULL"}\n" +
                    $"PlayerInTurn={playerCharacter?.InTurn.ToString() ?? "NULL"}\n" +
                    $"PlayerStatsInCombat={PlayerStats.Instance.InCombat}\n" +
                    $"PlayerStatsActionPoints={PlayerStats.Instance.ActionPoints}",
                    playerCharacter, entity as Character);
                return;
            }

            if (characterOwnedCombatAction && !PrepareCharacterActionPointsForCombatAction(interaction, playerCharacter, actionPointCost))
            {
                return;
            }

            if (interaction.Type != InteractionType.Combat || !characterOwnedCombatAction)
            {
                ActionCostProfileResolver.LogPredictedCost("PlayerController.ExecutePlayerAction", interaction.Name, actionCostProfile, playerCharacter);
            }

            // Execute the interaction 
            PlayerStats.Instance.Attacking = true;
            SetInteractingWithTargetFromSelection(interaction, entity);
            if (interaction.Type == InteractionType.Combat)
            {
                Character attacker = PlayerStats.Instance.CurrentPlayerCharacter;
                Character target = entity as Character;
                // CODEXLOG003_ACTIONS_AAM: temporary combat execution diagnostic.
                ActionAAMDiagnosticsLogger.LogEvent("[COMBAT EXECUTE]", "Player combat interaction executing",
                    $"ActionName: {interaction.Name}\n" +
                    $"ActionPointCost: {actionPointCost}\n" +
                    $"Attacker: {FormatAAMCharacter(attacker)}\n" +
                    $"Target: {FormatAAMCharacter(target)}\n" +
                    $"AttackerNestedArea: {FormatAAMArea(attacker?.CurrentNestedArea)}\n" +
                    $"TargetNestedArea: {FormatAAMArea(target?.CurrentNestedArea)}\n" +
                    $"TargetIsActive: {target?.IsActive.ToString() ?? "NULL"}\n" +
                    $"TargetIsAlive: {target?.IsAlive.ToString() ?? "NULL"}");
                CombatActionResolutionDiagnosticsLogger.LogEvent("[ATTACK ENTRY]", "PlayerController.ExecutePlayerAction combat action requested",
                    $"ActionName={interaction.Name}\n" +
                    $"ActionPointCost={actionPointCost}\n" +
                    $"CharacterOwnedCombatAction={characterOwnedCombatAction}\n" +
                    $"APSource={(characterOwnedCombatAction ? "Character.PerformAttack" : "PlayerController.DeductActionPoints")}\n" +
                    $"TargetIsActive={target?.IsActive.ToString() ?? "NULL"}\n" +
                    $"TargetIsAlive={target?.IsAlive.ToString() ?? "NULL"}",
                    attacker, target);
            }
            interaction.ExecuteInteraction(entity, PlayerInventory.Instance);
            if (interaction.Type == InteractionType.Combat)
            {
                Character attacker = PlayerStats.Instance.CurrentPlayerCharacter;
                Character target = entity as Character;
                INestedArea area = attacker?.CurrentNestedArea ?? target?.CurrentNestedArea ?? PlayerStats.Instance.CurrentNestedArea;

                area?.UpdateHostileAreaStatus();
                // CODEXLOG001_TURNLIFECYCLE: temporary player-initiated combat transition diagnostic.
                TurnDiagnosticsLogger.LogEvent("[CONTEXT UPDATE]", "PlayerController.ExecutePlayerAction after combat action",
                    $"ActionName: {interaction.Name}\n" +
                    $"Attacker: {FormatAAMCharacter(attacker)}\n" +
                    $"Target: {FormatAAMCharacter(target)}\n" +
                    $"AttackerNestedArea: {FormatAAMArea(attacker?.CurrentNestedArea)}\n" +
                    $"TargetNestedArea: {FormatAAMArea(target?.CurrentNestedArea)}\n" +
                    $"AreaUpdated: {FormatAAMArea(area)}\n" +
                    $"AreaHasHostiles: {area?.IsHostileArea.ToString() ?? "NULL"}");
                TurnOrchestrator.Instance?.TryUpdateTurnContext();
            }
            if (characterOwnedCombatAction)
            {
                SyncPlayerStatsActionPointsFromCharacter(playerCharacter, interaction.Name);
                PostPlayerActionTurnMaintenance("ExecutePlayerAction character-owned combat action", $"AAM action: {interaction.Name}");
            }
            else
            {
                DeductActionPoints(actionPointCost); // Deduct AP and check if the turn should end
            }
            PlayerStats.Instance.Attacking = false;
        }
        else
        {
            if (IsCombatTurnContext())
            {
                // CODEXLOG003_ACTIONS_AAM: temporary combat AP/pending diagnostic.
                ActionAAMDiagnosticsLogger.LogEvent("[AP CHECK]", "Combat action rejected without pending action",
                    $"ActionName: {interaction.Name}\n" +
                    $"ActionPointCost: {actionPointCost}\n" +
                    $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}\n" +
                    $"CurrentPlayerCharacter.ActionPoints: {PlayerStats.Instance.CurrentPlayerCharacter?.ActionPoints.ToString() ?? "NULL"}\n" +
                    $"CurrentPlayerCharacter.InTurn: {PlayerStats.Instance.CurrentPlayerCharacter?.InTurn.ToString() ?? "NULL"}");
                ShowNotEnoughAPFeedback(interaction.Name);
                return;
            }

            // Store the remaining AP cost for the next turn
            PlayerStats.Instance.PendingActionPointsCost = actionPointCost - PlayerStats.Instance.ActionPoints;
            PlayerStats.Instance.HasPendingAction = true;
            DeductActionPoints(PlayerStats.Instance.ActionPoints); // Set AP to 0 and end the turn
        }
    }

    // CODEXLOG003_ACTIONS_AAM: temporary combat action diagnostic helper.
    private string FormatAAMCharacter(Character character)
    {
        if (character == null) return "NULL";
        return $"{character.Name} [{character.IInteractableID}] ({character.GetType().Name})";
    }

    // CODEXLOG003_ACTIONS_AAM: temporary AAM area-state repair diagnostic helper.
    private void RepairAAMProviderAreaStateIfSafe(IInteractable interactable, string sourceCollection)
    {
        if (!(interactable is Character character)) return;
        if (currentNestedArea == null) return;
        if (character.CurrentNestedArea != null && character.CurrentNestedArea != currentNestedArea) return;

        bool repaired = false;
        string areaBefore = FormatAAMArea(character.CurrentNestedArea);
        bool isInNestedAreaBefore = character.IsInNestedArea;

        if (character.CurrentNestedArea == null)
        {
            character.CurrentNestedArea = currentNestedArea;
            repaired = true;
        }

        if (!character.IsInNestedArea)
        {
            character.IsInNestedArea = true;
            repaired = true;
        }

        if (repaired)
        {
            // CODEXLOG003_ACTIONS_AAM: temporary AAM area-state repair diagnostic.
            ActionAAMDiagnosticsLogger.LogEvent("[PROVIDER AREA REPAIR]", "AAM provider area state repaired from faced cell",
                $"Provider: {FormatAAMProvider(interactable)}\n" +
                $"SourceCollection: {sourceCollection}\n" +
                $"CurrentNestedArea before: {areaBefore}\n" +
                $"CurrentNestedArea after: {FormatAAMArea(character.CurrentNestedArea)}\n" +
                $"IsInNestedArea before: {isInNestedAreaBefore}\n" +
                $"IsInNestedArea after: {character.IsInNestedArea}");
        }
    }

    // CODEXLOG003_ACTIONS_AAM: temporary AAM/combat diagnostic helper.
    private string FormatAAMArea(INestedArea area)
    {
        if (area == null) return "NULL";
        return $"{area.Name} (ID={area.NestedAreaID}, Level={area.NestedAreaLevel})";
    }

    private bool IsCombatTurnContext()
    {
        return TurnOrchestrator.Instance != null &&
               TurnOrchestrator.Instance.CurrentContext == TurnContext.Combat;
    }

    private bool IsExplorationTurnContext()
    {
        return TurnOrchestrator.Instance != null &&
               TurnOrchestrator.Instance.CurrentContext == TurnContext.Exploration;
    }

    private bool IsPlayerCombatTurnActive()
    {
        Character playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        return playerCharacter != null && playerCharacter.InTurn;
    }

    private TurnOrchestrator ResolveTurnOrchestrator()
    {
        if (TurnOrchestrator.Instance != null && turnOrchestrator != TurnOrchestrator.Instance)
        {
            turnOrchestrator = TurnOrchestrator.Instance;
            GameDebugger.Instance.LogInfo("PlayerController.ResolveTurnOrchestrator refreshed cached TurnOrchestrator reference from singleton.");
        }

        return turnOrchestrator;
    }

    private bool CanAcceptPlayerTurnInput(string source)
    {
        if (!IsCombatTurnContext())
        {
            return true;
        }

        if (IsPlayerCombatTurnActive())
        {
            lastCombatInputBlockedActorId = int.MinValue;
            return true;
        }

        Character currentActor = GetCurrentCombatTurnActor();
        int currentActorId = currentActor != null ? currentActor.IInteractableID : int.MinValue;

        if (lastCombatInputBlockedActorId != currentActorId)
        {
            lastCombatInputBlockedFrame = Time.frameCount;
            lastCombatInputBlockedActorId = currentActorId;
            Character playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
            // CODEXLOG001_TURNLIFECYCLE: temporary combat player-input ownership diagnostic.
            TurnDiagnosticsLogger.LogEvent("[PLAYER INPUT]", "Player input ignored because not player combat turn",
                $"Source: {source}\n" +
                $"CurrentContext: {TurnOrchestrator.Instance.CurrentContext}\n" +
                $"CurrentActor: {FormatAAMCharacter(currentActor)}\n" +
                $"Player: {FormatAAMCharacter(playerCharacter)}\n" +
                $"Player.InTurn: {playerCharacter?.InTurn.ToString() ?? "NULL"}\n" +
                $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}\n" +
                $"PlayerStats.MovePoints: {PlayerStats.Instance.MovePoints}\n" +
                $"PlayerStats.InCombat: {PlayerStats.Instance.InCombat}");
            MessageLogManager.Instance?.Log("combat_wait_turn", currentActor != null ? currentActor.Name : "Someone else");
        }

        return false;
    }

    private bool TryGetEndTurnAvailability(out bool canEndTurn, out string reason)
    {
        canEndTurn = false;
        reason = "Unavailable";
        TurnOrchestrator orchestrator = ResolveTurnOrchestrator();

        if (!isInNestedArea)
        {
            reason = "NotInNestedArea";
            return false;
        }

        if (orchestrator == null)
        {
            reason = "MissingTurnOrchestrator";
            return false;
        }

        if (!GameManager.Instance.ActiveTurnManager || !PlayerStats.Instance.RegisteredInTurnManager)
        {
            reason = "TurnManagerInactive";
            return false;
        }

        if (IsCombatTurnContext())
        {
            canEndTurn = IsPlayerCombatTurnActive();
            reason = canEndTurn ? "CombatPlayerTurnActive" : "CombatNotPlayerTurn";
            return canEndTurn;
        }

        if (IsExplorationTurnContext())
        {
            canEndTurn = true;
            reason = "ExplorationTurnActive";
            return true;
        }

        reason = $"UnsupportedContext:{orchestrator.CurrentContext}";
        return false;
    }

    public void GetWaitOrEndTurnPresentation(out string label, out bool canUse, out string reason)
    {
        label = IsCombatTurnContext() ? "End Turn" : "Wait";

        if (!isInNestedArea)
        {
            canUse = true;
            reason = "MainMapWait";
            return;
        }

        canUse = TryGetEndTurnAvailability(out _, out reason);
    }

    private void LogEndTurnActionVisibility(bool isVisible, string reason)
    {
        if (lastEndTurnActionVisible == isVisible &&
            string.Equals(lastEndTurnActionVisibilityReason, reason, StringComparison.Ordinal))
        {
            return;
        }

        lastEndTurnActionVisible = isVisible;
        lastEndTurnActionVisibilityReason = reason ?? string.Empty;

        string context = TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "NULL";
        string managerName = context switch
        {
            "Combat" => "CombatTurnManager",
            "Exploration" => "ExplorationTurnManager",
            _ => "None"
        };

        GameDebugger.Instance.LogInfo(
            $"PlayerController.EndTurnAction {(isVisible ? "shown" : "hidden")}. Context={context} Reason={reason} AP={PlayerStats.Instance.ActionPoints} MP={PlayerStats.Instance.MovePoints} ActiveTurnManager={managerName}");
    }

    private bool TryCompletePlayerTurnFromPlayerController(string source)
    {
        TurnOrchestrator orchestrator = ResolveTurnOrchestrator();

        if (orchestrator == null)
        {
            TurnDiagnosticsLogger.LogWarning("PlayerController could not complete player turn because TurnOrchestrator was missing",
                $"Source: {source}\n" +
                $"Context: {TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "NULL"}\n" +
                $"PlayerInTurn: {PlayerStats.Instance.CurrentPlayerCharacter?.InTurn.ToString() ?? "NULL"}\n" +
                $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}\n" +
                $"PlayerStats.MovePoints: {PlayerStats.Instance.MovePoints}");
            return false;
        }

        if (IsCombatTurnContext() && !IsPlayerCombatTurnActive())
        {
            Character playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
            // CODEXLOG001_TURNLIFECYCLE: temporary combat player-turn completion diagnostic.
            TurnDiagnosticsLogger.LogEvent("[PLAYER TURN]", "PlayerController skipped PlayerTurnCompleted because combat player turn is not active",
                $"Source: {source}\n" +
                $"CurrentContext: {TurnOrchestrator.Instance.CurrentContext}\n" +
                $"Player: {FormatAAMCharacter(playerCharacter)}\n" +
                $"Player.InTurn: {playerCharacter?.InTurn.ToString() ?? "NULL"}\n" +
                $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}\n" +
                $"PlayerStats.MovePoints: {PlayerStats.Instance.MovePoints}\n" +
                $"PlayerStats.InCombat: {PlayerStats.Instance.InCombat}");
            return false;
        }

        TurnDiagnosticsLogger.LogEvent("[PLAYER TURN]", "PlayerController.TryCompletePlayerTurnFromPlayerController",
            $"Source: {source}\n" +
            $"CurrentContext: {orchestrator.CurrentContext}\n" +
            $"Player: {FormatAAMCharacter(PlayerStats.Instance.CurrentPlayerCharacter)}\n" +
            $"Player.InTurn: {PlayerStats.Instance.CurrentPlayerCharacter?.InTurn.ToString() ?? "NULL"}\n" +
            $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}\n" +
            $"PlayerStats.MovePoints: {PlayerStats.Instance.MovePoints}\n" +
            $"RegisteredInTurnManager: {PlayerStats.Instance.RegisteredInTurnManager}");
        GameDebugger.Instance.LogInfo(
            $"PlayerController.PlayerTurnCompleted requested. Source={source} Context={orchestrator.CurrentContext} AP={PlayerStats.Instance.ActionPoints} MP={PlayerStats.Instance.MovePoints}");
        orchestrator.PlayerTurnCompleted();
        notifiedManualEndRequiredForNoResources = false;
        return true;
    }

    public bool EndPlayerTurn(string source, bool playerFacingMessage = true)
    {
        if (!TryGetEndTurnAvailability(out _, out string reason))
        {
            GameDebugger.Instance.LogWarning(
                $"PlayerController.EndPlayerTurn rejected. Source={source} Reason={reason} Context={TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "NULL"} AP={PlayerStats.Instance.ActionPoints} MP={PlayerStats.Instance.MovePoints}");

            if (IsCombatTurnContext())
            {
                Character currentActor = GetCurrentCombatTurnActor();
                MessageLogManager.Instance?.Log("combat_wait_turn", currentActor != null ? currentActor.Name : "Someone else");
            }

            return false;
        }

        string currentContext = TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "NULL";
        string activeTurnManager = IsCombatTurnContext() ? "CombatTurnManager" : "ExplorationTurnManager";
        bool wasCombatContext = IsCombatTurnContext();

        GameDebugger.Instance.LogInfo(
            $"PlayerController.EndPlayerTurn clicked. Source={source} Context={currentContext} AP={PlayerStats.Instance.ActionPoints} MP={PlayerStats.Instance.MovePoints} ActiveTurnManager={activeTurnManager}");

        bool completed = TryCompletePlayerTurnFromPlayerController(source);
        if (!completed)
        {
            return false;
        }

        if (playerFacingMessage)
        {
            MessageLogManager.Instance?.Log(wasCombatContext ? "combat_manual_end_turn" : "exploration_manual_end_turn");
        }

        return true;
    }

    public bool HandleWaitOrEndTurn(string source, bool playerFacingMessage = true)
    {
        ActionCostProfile waitOrEndTurnProfile = ActionCostProfileResolver.BuildForWaitOrEndTurn(IsCombatTurnContext());
        ActionCostProfileResolver.LogPredictedCost("PlayerController.HandleWaitOrEndTurn", waitOrEndTurnProfile.CostLabel, waitOrEndTurnProfile, PlayerStats.Instance.CurrentPlayerCharacter);

        if (IsExplorationTurnContext())
        {
            bool completed = CompleteExplorationTurnForTimeCostingAction(source, 1f);
            if (completed && playerFacingMessage)
            {
                MessageLogManager.Instance?.Log("exploration_manual_end_turn");
            }

            return completed;
        }

        return EndPlayerTurn(source, playerFacingMessage);
    }

    private void HandleManualEndTurnInput()
    {
        HandleWaitOrEndTurn("ManualWaitOrEndTurnHotkey", true);
    }

    private void ToggleAutoEndCombatTurn()
    {
        autoEndCombatTurnWhenNoAPMP = !autoEndCombatTurnWhenNoAPMP;
        notifiedManualEndRequiredForNoResources = false;

        // CODEXLOG001_TURNLIFECYCLE: temporary auto-end toggle diagnostic.
        TurnDiagnosticsLogger.LogEvent("[AUTO END TOGGLE]", "PlayerController.ToggleAutoEndCombatTurn",
            $"AutoEndCombatTurnWhenNoAPMP: {autoEndCombatTurnWhenNoAPMP}\n" +
            $"Context: {TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "NULL"}\n" +
            $"IsPlayerTurn: {IsPlayerCombatTurnActive()}\n" +
            $"AP: {PlayerStats.Instance.ActionPoints}\n" +
            $"MP: {PlayerStats.Instance.MovePoints}",
            PlayerStats.Instance.CurrentPlayerCharacter);

        MessageLogManager.Instance?.Log(autoEndCombatTurnWhenNoAPMP ? "combat_auto_end_on" : "combat_auto_end_off");
        PostPlayerActionTurnMaintenance("ToggleAutoEndCombatTurn", "auto-end toggle");
    }

    private void CompletePlayerTurnIfActionPointsDepleted(string source)
    {
        if (PlayerStats.Instance.ActionPoints <= 0)
        {
            if (IsCombatTurnContext())
            {
                ShowNotEnoughAPFeedback("continue acting");
                PostPlayerActionTurnMaintenance(source, "legacy AP depletion check");
                return;
            }
            TryCompletePlayerTurnFromPlayerController(source);
        }
    }

    private void PostPlayerActionTurnMaintenance(string source, string pipelineSource)
    {
        if (!IsCombatTurnContext())
        {
            return;
        }

        Character playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        int characterAPBeforeSync = playerCharacter != null ? playerCharacter.ActionPoints : -1;
        int characterMPBeforeSync = playerCharacter != null ? playerCharacter.MovePoints : -1;

        if (playerCharacter != null)
        {
            playerCharacter.ActionPoints = PlayerStats.Instance.ActionPoints;
            playerCharacter.MovePoints = PlayerStats.Instance.MovePoints;
        }

        RefreshCombatStatusUI();
        UpdateAdaptiveActionMenu();

        bool playerTurnActive = IsPlayerCombatTurnActive();
        bool noUsableAP = PlayerStats.Instance.ActionPoints <= 0;
        bool noUsableMP = PlayerStats.Instance.MovePoints <= 0;
        bool waitingForManualEnd = playerTurnActive &&
                                   noUsableAP &&
                                   noUsableMP &&
                                   !autoEndCombatTurnWhenNoAPMP;
        bool shouldAutoEnd = autoEndCombatTurnWhenNoAPMP &&
                             playerTurnActive &&
                             noUsableAP &&
                             noUsableMP &&
                             !autoEndingCombatTurn;

        // CODEXLOG003_ACTIONS_AAM: temporary central player action pipeline diagnostic.
        ActionAAMDiagnosticsLogger.LogEvent("[PLAYER ACTION PIPELINE]", "Post player action combat maintenance",
            $"Source: {source}\n" +
            $"PipelineSource: {pipelineSource}\n" +
            $"PostActionSync: True\n" +
            $"AutoEndChecked: True\n" +
            $"PlayerTurnActive: {playerTurnActive}\n" +
            $"PlayerStatsAP: {PlayerStats.Instance.ActionPoints}\n" +
            $"CharacterAPBeforeSync: {characterAPBeforeSync}\n" +
            $"CharacterAP: {playerCharacter?.ActionPoints.ToString() ?? "NULL"}\n" +
            $"PlayerStatsMP: {PlayerStats.Instance.MovePoints}\n" +
            $"CharacterMPBeforeSync: {characterMPBeforeSync}\n" +
            $"CharacterMP: {playerCharacter?.MovePoints.ToString() ?? "NULL"}\n" +
            $"AutoEnd: {shouldAutoEnd}");

        // CODEXLOG001_TURNLIFECYCLE: temporary combat auto-end diagnostic.
        TurnDiagnosticsLogger.LogEvent("[AUTO END CHECK]", "PlayerController.PostPlayerActionTurnMaintenance",
            $"Source: {source}\n" +
            $"PipelineSource: {pipelineSource}\n" +
            $"CurrentContext: {TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "NULL"}\n" +
            $"PlayerTurnActive: {playerTurnActive}\n" +
            $"AP: {PlayerStats.Instance.ActionPoints}\n" +
            $"MP: {PlayerStats.Instance.MovePoints}\n" +
            $"AutoEndEnabled: {autoEndCombatTurnWhenNoAPMP}\n" +
            $"AutoEndInProgress: {autoEndingCombatTurn}\n" +
            $"Action: {(shouldAutoEnd ? "EndPlayerTurn" : waitingForManualEnd ? "WaitingForManualEndTurn" : "ContinuePlayerTurn")}",
            playerCharacter);

        if (waitingForManualEnd)
        {
            if (!notifiedManualEndRequiredForNoResources)
            {
                notifiedManualEndRequiredForNoResources = true;
                MessageLogManager.Instance?.Log("combat_no_resources_manual_end");
            }
            return;
        }

        if (!noUsableAP || !noUsableMP)
        {
            notifiedManualEndRequiredForNoResources = false;
        }

        if (!shouldAutoEnd)
        {
            return;
        }

        autoEndingCombatTurn = true;
        try
        {
            MessageLogManager.Instance?.Log("combat_auto_end_no_resources");
            TryCompletePlayerTurnFromPlayerController($"AutoEndNoAPMP:{source}");
        }
        finally
        {
            autoEndingCombatTurn = false;
        }
    }

    private bool CombatActionUsesCharacterActionPoints(IInteraction interaction)
    {
        if (interaction == null || interaction.Type != InteractionType.Combat)
        {
            return false;
        }

        return interaction.Name == "Punch" ||
               interaction.Name == "Slash" ||
               interaction.Name == "Stab" ||
               interaction.Name == "Bash" ||
               interaction.Name == "Rend";
    }

    private bool PrepareCharacterActionPointsForCombatAction(IInteraction interaction, Character playerCharacter, int actionPointCost)
    {
        if (playerCharacter == null)
        {
            // CODEXLOG003_ACTIONS_AAM: temporary combat AP ownership diagnostic.
            ActionAAMDiagnosticsLogger.LogEvent("[AP CHECK]", "Combat action rejected because player character is null",
                $"ActionName: {interaction?.Name ?? "NULL"}\n" +
                $"ActionPointCost: {actionPointCost}\n" +
                $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}");
            return false;
        }

        if (!playerCharacter.IsCombatActorAvailable())
        {
            ActionAAMDiagnosticsLogger.LogEvent("[AP CHECK]", "Combat action rejected because player character is inactive or dead",
                $"ActionName: {interaction?.Name ?? "NULL"}\n" +
                $"ActionPointCost: {actionPointCost}\n" +
                $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}\n" +
                $"PlayerCharacter.IsAlive: {playerCharacter.IsAlive}\n" +
                $"PlayerCharacter.IsActive: {playerCharacter.IsActive}");
            return false;
        }

        int characterAPBefore = playerCharacter.ActionPoints;
        if (playerCharacter.ActionPoints != PlayerStats.Instance.ActionPoints)
        {
            playerCharacter.ActionPoints = PlayerStats.Instance.ActionPoints;
        }

        bool canAfford = playerCharacter.ActionPoints >= actionPointCost;
        // CODEXLOG003_ACTIONS_AAM: temporary combat AP ownership diagnostic.
        ActionAAMDiagnosticsLogger.LogEvent("[AP CHECK]", "Combat action character AP prepared before execution",
            $"ActionName: {interaction.Name}\n" +
            $"ActionPointCost: {actionPointCost}\n" +
            $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}\n" +
            $"Character.ActionPoints before sync: {characterAPBefore}\n" +
            $"Character.ActionPoints after sync: {playerCharacter.ActionPoints}\n" +
            $"CanAffordBeforeDamage: {canAfford}\n" +
            $"AP owner for this action: Character.PerformAttack");

        return canAfford;
    }

    private void SyncPlayerStatsActionPointsFromCharacter(Character playerCharacter, string actionName)
    {
        if (playerCharacter == null)
        {
            return;
        }

        int playerStatsAPBefore = PlayerStats.Instance.ActionPoints;
        PlayerStats.Instance.ActionPoints = playerCharacter.ActionPoints;

        // CODEXLOG003_ACTIONS_AAM: temporary combat AP ownership diagnostic.
        ActionAAMDiagnosticsLogger.LogEvent("[AP SPEND]", "PlayerStats AP synced from character after combat action",
            $"ActionName: {actionName}\n" +
            $"PlayerStats.ActionPoints before sync: {playerStatsAPBefore}\n" +
            $"PlayerStats.ActionPoints after sync: {PlayerStats.Instance.ActionPoints}\n" +
            $"Character.ActionPoints: {playerCharacter.ActionPoints}\n" +
            $"AP spend source: Character.PerformAttack");
        RefreshCombatStatusUI();
    }

    private Character GetCurrentCombatTurnActor()
    {
        if (TurnOrchestrator.Instance == null ||
            TurnOrchestrator.Instance.CurrentContext != TurnContext.Combat)
        {
            return null;
        }

        return TurnOrchestrator.Instance.DiagnosticGetCombatCharactersSnapshot()
            .FirstOrDefault(character => character != null && character.InTurn);
    }

    private void ShowNotEnoughAPFeedback(string actionName)
    {
        if (lastNoAPFeedbackFrame == Time.frameCount)
        {
            return;
        }

        lastNoAPFeedbackFrame = Time.frameCount;
        ActionAAMDiagnosticsLogger.LogEvent("[AP CHECK]", "Not enough AP feedback shown",
            $"ActionName: {actionName}\n" +
            $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}\n" +
            $"CurrentPlayerCharacter.ActionPoints: {PlayerStats.Instance.CurrentPlayerCharacter?.ActionPoints.ToString() ?? "NULL"}\n" +
            $"Context: {TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "NULL"}");
        MessageLogManager.Instance?.Log("combat_no_ap", actionName);
    }

    private void ShowNotEnoughMPFeedback(string actionName)
    {
        if (lastNoMPFeedbackFrame == Time.frameCount)
        {
            return;
        }

        lastNoMPFeedbackFrame = Time.frameCount;
        ActionAAMDiagnosticsLogger.LogEvent("[MP CHECK]", "Not enough MP feedback shown",
            $"ActionName: {actionName}\n" +
            $"PlayerStats.MovePoints: {PlayerStats.Instance.MovePoints}\n" +
            $"CurrentPlayerCharacter.MovePoints: {PlayerStats.Instance.CurrentPlayerCharacter?.MovePoints.ToString() ?? "NULL"}\n" +
            $"Context: {TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "NULL"}");
        MessageLogManager.Instance?.Log("combat_no_mp", actionName);
    }

    private void LogCombatResourceState(string category, string eventName, string details)
    {
        if (!IsCombatTurnContext())
        {
            return;
        }

        ActionAAMDiagnosticsLogger.LogEvent(category, eventName,
            $"{details}\n" +
            $"PlayerStats.ActionPoints: {PlayerStats.Instance.ActionPoints}\n" +
            $"PlayerStats.MovePoints: {PlayerStats.Instance.MovePoints}\n" +
            $"CurrentPlayerCharacter.ActionPoints: {PlayerStats.Instance.CurrentPlayerCharacter?.ActionPoints.ToString() ?? "NULL"}\n" +
            $"CurrentPlayerCharacter.MovePoints: {PlayerStats.Instance.CurrentPlayerCharacter?.MovePoints.ToString() ?? "NULL"}");
    }

    private void RefreshCombatStatusUI()
    {
        if (!IsCombatTurnContext())
        {
            return;
        }

        UIController.Instance?.UpdateTurnOrderUI();
    }

    private void ExecuteEnvironmentalAction(IEnvironmentalAction action, Cell cell)
    {
        if (!CanAcceptPlayerTurnInput("ExecuteEnvironmentalAction"))
        {
            return;
        }

        int actionPointCost = action.ActionPointCost;
        ActionCostProfile actionCostProfile = ActionCostProfileResolver.BuildForEnvironmentalAction(action, IsCombatTurnContext());
        ActionCostProfileResolver.LogPredictedCost("PlayerController.ExecuteEnvironmentalAction", action.Name, actionCostProfile, PlayerStats.Instance.CurrentPlayerCharacter);

        if (PlayerStats.Instance.ActionPoints >= actionPointCost)
        {
            // Execute the environmental action
            action.ExecuteAction(cell, PlayerInventory.Instance);
            DeductActionPoints(actionPointCost); // Deduct AP and check if the turn should end
        }
        else
        {
            // Store the remaining AP cost for the next turn
            PlayerStats.Instance.PendingActionPointsCost = actionPointCost - PlayerStats.Instance.ActionPoints;
            PlayerStats.Instance.HasPendingAction = true;
            DeductActionPoints(PlayerStats.Instance.ActionPoints); // Set AP to 0 and end the turn
        }
    }
    #endregion

    #region Object Manipulation

    public void PlaceObject()
    {
        // The name of the object to place (e.g., "Wall" or "Door")
        string objectToPlace = "Wall";

        // Retrieve the player's facing cell and the coordinates
        Cell facingCell = PlayerStats.Instance.FacingCell;
        Vector2Int facingCellCoordinates = facingCell.Coordinates;

        // Get the current nested area
        INestedArea currentNestedArea = PlayerStats.Instance.CurrentNestedArea;

        // Use the factory to place the object at the player's facing cell coordinates
        bool success = ObjectPlacementFactory.Instance.PlaceObjectAt(objectToPlace, facingCellCoordinates, currentNestedArea);

        // Optionally, you can check if the placement was successful
        if (success)
        {
            Debug.Log($"Successfully placed {objectToPlace} at {facingCellCoordinates}");
        }
        else
        {
            Debug.LogError($"Failed to place {objectToPlace} at {facingCellCoordinates}");
        }
    }


    #endregion

    #region Nested Area Animal Generation
    private void GenerateAnimalsForNestedArea()
    {
        var cell = currentNestedArea.GetCellAtPosition(playerPosition);

        if (cell == null)
        {
            Debug.LogWarning("Player is not in a valid cell position.");
            return;
        }

        var cellID = cell.CellID;

        if (PermaLists.Instance.AnimalsToGenerate.ContainsKey(cellID))
        {
            var animalNames = PermaLists.Instance.AnimalsToGenerate[cellID];
            Debug.Log($"CellID {cellID} has {animalNames.Count} animals to make.");

            foreach (var animalName in animalNames)
            {
                var animalData = AnimalGenerator.Instance.GetAnimalDataByName(animalName);
                if (animalData != null)
                {
                    var animal = AnimalFactory.CreateAnimal(animalData);
                    currentNestedArea.AddAnimal(animal, playerPosition);
                    Debug.Log($"Added animal {animal.Name} to cell {cellID} at position {playerPosition} via a naughty PlayerController");
                }
                else
                {
                    Debug.LogWarning($"Animal data for {animalName} not found.");
                }
            }

            PermaLists.Instance.AnimalsToGenerate.Remove(cellID);
            Debug.Log($"Cleared animals to generate for cell {cellID}.");
        }
        else
        {
            Debug.Log($"No animals to generate for CellID {cellID}.");
        }
    }
    #endregion
}
