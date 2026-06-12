using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.Events;

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
        northButton?.onClick.AddListener(() => Move(Vector2Int.up));
        southButton?.onClick.AddListener(() => Move(Vector2Int.down));
        westButton?.onClick.AddListener(() => Move(Vector2Int.left));
        eastButton?.onClick.AddListener(() => Move(Vector2Int.right));
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
                MoveAndUpdate(direction, newDirection);
                isHoldingKey = true;
                currentHeldKey = key;
                holdKeyTimer = holdKeyInterval; // Start the timer for holding the key
            }
            else
            {
                holdKeyTimer -= Time.deltaTime;
                if (holdKeyTimer <= 0f)
                {
                    MoveAndUpdate(direction, newDirection);
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

    private void MoveAndUpdate(Vector2Int direction, Direction newDirection)
    {
        if (IsCellPassable(playerPosition + direction))
        {
            if (PlayerStats.Instance.RegisteredInTurnManager)
            {
                if (PlayerStats.Instance.InCombat && PlayerStats.Instance.MovePoints < 1)
                {
                    Debug.Log("Cannot move, out of MovePoints.");
                    return;
                }

                Move(direction);
                currentDirection = newDirection;
                DeductMovePoints(1);
            }
            else
            {
                Move(direction);
                currentDirection = newDirection;
            }

            UpdateAllPlayerStats();
            UpdateAdaptiveActionMenu();
            UIController.Instance.UpdateMapsAfterAction();
        }
    }

    private void Move(Vector2Int direction)
    {
        previousPosition = playerPosition;
        Vector2Int newPosition = playerPosition + direction;

        previousCellID = currentCellID;
        previousPlayerCell = currentPlayerCell;

        if (isInNestedArea)
        {
            if (currentNestedArea == null) return;

            if (currentNestedArea.IsValidPosition(newPosition) && IsCellPassable(newPosition))
            {
                var currentCell = currentNestedArea.GetCellAtPosition(playerPosition);
                currentCell.LastVisited = TimeManager.Instance.currentDay;
                UpdatePlayerPosition(newPosition, true);
                currentCellID = currentNestedArea.GetCellAtPosition(newPosition).CellID;
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
        }


        UpdateAllPlayerStats();
        // Add turn progress after successfully moving the player, regardless of the map type.
        AddTurnProgress(1f);

        AudioController.Instance.PlayMovementSound();

        currentPlayerCell = mapGenerator.GetCellByID(currentCellID);
        mapDisplayUI.UpdateBothMaps();


        UpdateDescription();
        UpdateAdaptiveActionMenu();
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
			$"Previous allCharacters.Count: {previousAllCharactersCount}", playerCharacter);
		orchestrator.EnterExplorationArea(currentNestedArea, playerCharacter);
		orchestrator.ValidateCharacterNestedAreas();
		orchestrator.LogAllRegisteredCharacters();
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("PlayerController.EnterNestedAreaWithinNestedArea completed",
			$"NestedArea: {currentNestedArea?.Name} ({currentNestedArea?.NestedAreaID})\n" +
			$"Previous active participants cleared by EnterExplorationArea: True\n" +
			$"Player registered in active manager: {orchestrator.DiagnosticIsCharacterRegisteredInActiveManager(playerCharacter)}\n" +
			$"Exploration.Count: {orchestrator.DiagnosticExplorationRegisteredCount}\n" +
			$"allCharacters.Count: {orchestrator.DiagnosticAllCharactersCount}");
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

		TurnOrchestrator.Instance.DeregisterAllCharacters();
		PlayerStats.Instance.RegisteredInTurnManager = false;
		GameManager.Instance.ActiveTurnManager = false;
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogTurnSummary("PlayerController.ExitNestedArea completed");
	}

	private void HandleExitNestedArea()
	{
		if (currentNestedArea.NestedAreaLevel == 0 || currentNestedArea.ParentCellID == currentNestedArea.MainMapCellID)
		{
			foreach (var npcGroup in currentNestedArea.GetNPCGroups())
				npcManager.UpdateNPCGroupStatus(npcGroup);

			var currentCell = currentNestedArea.GetCellAtPosition(nestedMapPosition);
			currentCell.LastVisited = TimeManager.Instance.currentDay;

			isInNestedArea = false;
			isInMainMap = true;

			playerPosition = mapGenerator.GetCellCoordinatesContainingNestedArea(currentNestedArea);
			currentRegion = mapGenerator.GetCell(playerPosition).RegionNumber;

			currentNestedArea.HandlePlayerExit(mapGenerator);
			currentNestedArea = null;

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
			nestedMapPosition = position;
			currentNestedArea.UpdatePlayerPosition(position);
			npcManager.UpdateNPCsInNestedArea(currentNestedArea);
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
		playerPosition = position;
		var cell = mapGenerator.map[position.x, position.y];
		cell.isPlayerPresent = true;
		cell.nestedAreaCanBeSeen = true;

		currentRegion = mapGenerator.GetCell(position).RegionNumber;
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

	private void HandleAction(float actionDuration)
	{
		endOfTurnManager.AddTurnProgress(actionDuration);
		TurnOrchestrator.Instance.PlayerTurnCompleted();
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
        // If we're not in a nested area, no actions should be generated
        if (!isInNestedArea)
        {
            ClearAdaptiveActionMenu();
            return;
        }

        ClearAdaptiveActionMenu();

        Cell[,] currentMap = isInNestedArea ? currentNestedArea.GetNestedMap() : mapGenerator.map;
        Vector2Int facingDirection = GetDirectionVector(currentDirection);
        Vector2Int facingPosition = playerPosition + facingDirection;

        if (IsValidPosition(facingPosition, isInNestedArea))
        {
            Cell facingCell = currentMap[facingPosition.x, facingPosition.y];
            PlayerStats.Instance.UpdateFacingCell(facingCell);
            facingCellPosition = facingCell.Coordinates;

            HandleInteractableObjectsInFacingCell(facingCell);
            AddEnvironmentalActions(facingCell);
        }
		
		CallTrace.Mark(this);
    }


    private void HandleInteractableObjectsInFacingCell(Cell facingCell)
    {
        ProcessInteractableObjects(facingCell.Objects.OfType<IInteractable>());
        ProcessInteractableObjects(facingCell.Items.OfType<IInteractable>());
        ProcessInteractableObjects(facingCell.Animals.OfType<IInteractable>());
    }

    private void ProcessInteractableObjects(IEnumerable<IInteractable> interactables)
    {
        var playerInventory = PlayerInventory.Instance;
        var addedInteractions = new HashSet<string>(); // Set to keep track of added interactions

        foreach (var interactable in interactables)
        {
            var availableInteractions = interactable.GetAvailableInteractions(playerInventory);
            Debug.Log($"Found {availableInteractions.Count()} available interactions for {interactable.Name}");

            // Filter interactions based on the current AdaptiveActionMenuPanel
            var filteredInteractions = FilterInteractionsByPanel(availableInteractions);

            foreach (var interaction in filteredInteractions)
            {
                // Generate the button name
                string buttonName = interaction.ActionPointCost > 0
                    ? $"{interaction.Name} ({interaction.ActionPointCost} AP) {interactable.Name}"
                    : $"{interaction.Name} {interactable.Name}";

                // Check if this interaction has already been added
                if (!addedInteractions.Contains(buttonName))
                {
                    // Add the button name to the set
                    addedInteractions.Add(buttonName);

                    // Create the button
                    CreateActionButton(buttonName, interaction.ActionPointCost, () => ExecutePlayerAction(interaction, interactable));
                    PlayerStats.Instance.InteractingWithID = interactable.IInteractableID;
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
            // Generate the button name
            string buttonName = action.ActionPointCost > 0
                ? $"{action.Name} ({action.ActionPointCost} AP)"
                : $"{action.Name}";

            // Check if this action has already been added
            if (!addedActions.Contains(buttonName))
            {
                // Add the action name to the set
                addedActions.Add(buttonName);

                // Create the button
                CreateActionButton(buttonName, action.ActionPointCost, () => ExecuteEnvironmentalAction(action, facingCell));
            }
        }
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

        PlayerStats.Instance.InteractingWithID = 0;
    }

    private IEnumerable<IInteraction> FilterInteractionsByPanel(IEnumerable<IInteraction> interactions)
    {
        var panelType = PlayerStats.Instance.AdaptiveActionMenuPanel;

        return interactions.Where(interaction =>
        {
            switch (panelType)
            {
                case AdapativeActionMenu.Combat:
                    return interaction.Type == InteractionType.Combat;
                case AdapativeActionMenu.Special:
                    return interaction.Type == InteractionType.Special;
                default:
                    // For all other panels, return everything except Combat and Special
                    return interaction.Type != InteractionType.Combat &&
                           interaction.Type != InteractionType.Special;
            }
        });
    }
    #endregion

    #region Player Action Handling

    public void DeductActionPoints(int amount)
    {
        PlayerStats.Instance.ActionPoints -= amount;

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
                turnOrchestrator.PlayerTurnCompleted();
            }
        }
    }

    public void DeductMovePoints(int amount)
    {
        PlayerStats.Instance.MovePoints -= amount;

        // Check if MovePoints are depleted
		if (PlayerStats.Instance.MovePoints <= 0)
		{
			if (!PlayerStats.Instance.InCombat)
			{
				turnOrchestrator.PlayerTurnCompleted();
			}
		}
    }

    private void HandlePendingAction()
    {
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
				turnOrchestrator.PlayerTurnCompleted();
			}
        }
        else
        {
            // If the player still doesn't have enough AP to complete the pending action
            PlayerStats.Instance.PendingActionPointsCost -= PlayerStats.Instance.ActionPoints;
            PlayerStats.Instance.ActionPoints = 0;
			turnOrchestrator.PlayerTurnCompleted();
        }
    }

    private void ExecutePlayerAction(IInteraction interaction, IInteractable entity)
    {
        int actionPointCost = interaction.ActionPointCost;

        if (PlayerStats.Instance.ActionPoints >= actionPointCost)
        {
            // Execute the interaction 
            PlayerStats.Instance.Attacking = true;
            interaction.ExecuteInteraction(entity, PlayerInventory.Instance);
            DeductActionPoints(actionPointCost); // Deduct AP and check if the turn should end
            PlayerStats.Instance.Attacking = false;
        }
        else
        {
            // Store the remaining AP cost for the next turn
            PlayerStats.Instance.PendingActionPointsCost = actionPointCost - PlayerStats.Instance.ActionPoints;
            PlayerStats.Instance.HasPendingAction = true;
            DeductActionPoints(PlayerStats.Instance.ActionPoints); // Set AP to 0 and end the turn
        }
    }

    private void ExecuteEnvironmentalAction(IEnvironmentalAction action, Cell cell)
    {
        int actionPointCost = action.ActionPointCost;

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
