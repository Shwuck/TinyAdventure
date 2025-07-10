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

    #region References
    public MapGenerator mapGenerator; // Reference to your MapGenerator
    public NPCManager npcManager;
    public ActionManager actionManager;
    public EndOfTurnManager endOfTurnManager;
    public TMP_Text descriptionText;

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

        EnterNestedArea(currentCell);
    }

    private void EnterNestedArea(Cell cellWithNestedArea)
    {
        if (cellWithNestedArea == null || !cellWithNestedArea.hasNestedArea) return;

        isInNestedArea = true;
        var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        var playerCharacterName = playerCharacter.Name;
        var playerCharacterID = playerCharacter.IInteractableID;

        Debug.Log($"Entering nested area. PlayerCharacter Name: {playerCharacterName}, ID: {playerCharacterID}");

        previousNestedAreaID = currentNestedArea != null ? currentNestedArea.NestedAreaID : -1;
        int mainMapCellID = currentCellID;

        parentNestedAreaID = cellWithNestedArea.CurrentAreaID;
        PlayerStats.Instance.ParentNestedAreaID = cellWithNestedArea.CurrentAreaID;
        mainMapPosition = playerPosition;
        isInMainMap = false;
        isInNestedArea = true;
        currentNestedArea = cellWithNestedArea.NestedArea;
        playerPosition = cellWithNestedArea.NestedArea.EntrancePosition;
        nestedAreaPanel.SetActive(true);

        currentNestedArea.GetNestedMap()[playerPosition.x, playerPosition.y].isPlayerPresent = true;

        if (currentNestedArea.HasVisited)
        {
            currentNestedArea.HandlePlayerReentry();
        }

        ApplyOverallFertilityAdjustment(cellWithNestedArea);

        if (cellWithNestedArea.Terrain == TerrainType.Village)
        {
            Debug.Log("Placing village NPCs.");
            PlaceVillageNPCs(cellWithNestedArea);
        }

        if (cellWithNestedArea.isNPCGroupPresent)
        {
            Debug.Log("Placing NPC group in nested area.");
            PlaceNPCGroupInNestedArea(cellWithNestedArea);
        }

        if (!currentNestedArea.GetAllAnimalsInArea().Any())
        {
            currentNestedArea.GenerateAnimalsForCellID(mainMapCellID);
        }
        else
        {
            Debug.Log("Animals already present in nested area; skipping generation.");
        }

        if (!cellWithNestedArea.WasPlayerStart)
        {
            AnimalManager.Instance.PlaceAnimalsForNestedArea(currentNestedArea);
        }

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

        Debug.Log($"Fully Entered Nested Area! NestedAreaID {currentNestedAreaID}. PlayerCharacter Name: {playerCharacterName}, ID: {playerCharacterID}");
        PlayerStats.Instance.CurrentPlayerCharacter.CurrentNestedArea = currentNestedArea;

        float turnDuration = CalculateTurnDuration(PlayerStats.Instance.TravelSpeed);

        Debug.Log("Registering player character with TurnManager.");
        GameManager.Instance.ActiveTurnManager = true;
        TurnManager.Instance.RegisterCharacter(playerCharacter, true);
        TurnManager.Instance.ValidateCharacterNestedAreas();
        TurnManager.Instance.DebugNestedArea();
        PlayerStats.Instance.RegisteredInTurnManager = true;
        TurnManager.Instance.LogAllRegisteredCharacters();
        TurnManager.Instance.StartTurnCycle();

        UIController.Instance.UpdateMapsAfterAction();
    }


    private void ApplyOverallFertilityAdjustment(Cell parentCell)
    {
        int overallAdjustment = parentCell.OverallFertilityAdjustment;
        Cell[,] nestedMap = currentNestedArea.GetNestedMap();

        for (int x = 0; x < nestedMap.GetLength(0); x++)
        {
            for (int y = 0; y < nestedMap.GetLength(1); y++)
            {
                Cell nestedCell = nestedMap[x, y];
                nestedCell.FertilityValue += overallAdjustment;
                nestedCell.OverallFertilityAdjustment += overallAdjustment;

                // Ensure the fertility value is within the valid range
                if (nestedCell.FertilityValue > 100)
                {
                    nestedCell.FertilityValue = 100;
                }
                else if (nestedCell.FertilityValue < 0)
                {
                    nestedCell.FertilityValue = 0;
                    nestedCell.isFertile = false;
                    if (nestedCell.Terrain == TerrainType.Land)
                    {
                        nestedCell.Terrain = TerrainType.Dirt;
                        Debug.Log($"Cell {nestedCell.CellID} changed to Dirt due to fertility drop.");
                    }
                }

                Debug.Log($"Applied overall fertility adjustment of {overallAdjustment} to nested cell {nestedCell.CellID}, new fertility value: {nestedCell.FertilityValue}");
            }
        }
    }

    private void PlaceVillageNPCs(Cell cellWithNestedArea)
    {
        INestedArea nestedAreaToPass = cellWithNestedArea.NestedArea;
        Village village = cellWithNestedArea.NestedArea as Village;
        if (village != null && village.VillageNPCs.Count > 0)
        {
            foreach (NPC npc in village.AvailableVillageNPCs)
            {
                // Check if NPC is already placed in the nested area
                if (!npcManager.IsNPCInNestedArea(npc, nestedAreaToPass))
                {
                    npc.IsInVillage = true;
                    npcManager.PlaceNPC(nestedAreaToPass, npc);
                }
                else
                {
                    Debug.Log($"NPC '{npc.Name}' is already placed in the nested area.");
                }
            }

            // Get the count of registered characters and log it
            int registeredCount = TurnManager.Instance.GetRegisteredCharacterCount();
            Debug.Log($"Total registered characters: {registeredCount}");

            // Optionally, log the names of registered characters
            var registeredCharacters = TurnManager.Instance.GetRegisteredCharacters();
            foreach (var character in registeredCharacters)
            {
                Debug.Log($"Character ID: {character.Key}, Name: {character.Value}");
            }
        }
    }

    private void PlaceNPCGroupInNestedArea(Cell cellWithNestedArea)
    {
        NPCGroup npcGroup = npcManager.FindNPCGroupAtPosition(cellWithNestedArea.Coordinates);
        if (npcGroup != null)
        {
            npcManager.PlaceNPCs(currentNestedArea, npcGroup);
        }
    }

    public void EnterNestedAreaWithinNestedArea(Cell cellWithNestedArea)
    {
        if (currentNestedArea != null)
        {
            Debug.Log($"Deregistering characters from Nested Area {currentNestedAreaID} before moving deeper.");
            TurnManager.Instance.DeregisterCharactersInNestedArea(currentNestedArea);
        }

        previousNestedAreaID = currentNestedArea != null ? currentNestedArea.NestedAreaID : -1;

        parentNestedAreaID = cellWithNestedArea.ParentAreaID;
        PlayerStats.Instance.ParentNestedAreaID = cellWithNestedArea.ParentAreaID;
        isInMainMap = false;
        isInNestedArea = true;
        currentNestedArea = cellWithNestedArea.NestedArea;
        playerPosition = cellWithNestedArea.NestedArea.EntrancePosition;
        nestedAreaPanel.SetActive(true);

        currentNestedArea.GetNestedMap()[playerPosition.x, playerPosition.y].isPlayerPresent = true;

        if (cellWithNestedArea.isNPCGroupPresent)
        {
            PlaceNPCGroupInNestedArea(cellWithNestedArea);
        }

        currentNestedAreaID = currentNestedArea.NestedAreaID;
        UpdateNestedAreaStats();
        UpdateDescription();

        // **Re-register the player character in the new nested area**
        var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        if (!TurnManager.Instance.IsCharacterRegistered(playerCharacter))
        {
            Debug.Log($"Re-registering player character in Nested Area {currentNestedAreaID}.");
            TurnManager.Instance.RegisterCharacter(playerCharacter, true);
        }
        else
        {
            Debug.Log($"Player character was already registered in TurnManager for Nested Area {currentNestedAreaID}.");
        }

        TurnManager.Instance.ValidateCharacterNestedAreas();
        TurnManager.Instance.LogAllRegisteredCharacters();

        // **Restart the turn cycle in the new nested area**
        TurnManager.Instance.StartTurnCycle();
    }


    private void ExitNestedArea()
    {
        if (!isInNestedArea || currentNestedArea == null)
        {
            Debug.Log("Not currently in a nested area, cannot exit.");
            return;
        }

        // Log exiting the nested area
        if (MessageLogManager.Instance != null)
        {
            string nestedAreaName = currentNestedArea?.Name ?? "unknown area";
            MessageLogManager.Instance.Log("exploration", "Exited", nestedAreaName);
        }

        HandleExitNestedArea();
        EndOfTurnManager.Instance.ConvertNestedTurnsToTime();

        // Deregister all characters, including the player and NPCs
        TurnManager.Instance.DeregisterAllCharacters();
        PlayerStats.Instance.RegisteredInTurnManager = false;
        GameManager.Instance.ActiveTurnManager = false;
    }

    private void HandleExitNestedArea()
    {
        if (currentNestedArea.NestedAreaLevel == 0 || currentNestedArea.ParentCellID == currentNestedArea.MainMapCellID)
        {
            foreach (var npcGroup in currentNestedArea.GetNPCGroups())
            {
                npcManager.UpdateNPCGroupStatus(npcGroup);
            }

            var currentCell = currentNestedArea.GetCellAtPosition(nestedMapPosition);
            currentCell.LastVisited = TimeManager.Instance.currentDay;

            isInNestedArea = false;
            isInMainMap = true;
            playerPosition = mapGenerator.GetCellCoordinatesContainingNestedArea(currentNestedArea);
            currentRegion = mapGenerator.GetCell(playerPosition).RegionNumber;
            Debug.Log($"Exiting nested area to {playerPosition}.");

            currentNestedArea.HandlePlayerExit(mapGenerator);
            currentNestedArea = null;
            nestedMapPosition = Vector2Int.zero;
            previousNestedMapPosition = Vector2Int.zero;
            UpdatePlayerStatsInstance();
            UpdateDescription();
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
        if (isInNestedArea && currentNestedArea != null)
        {
            int parentNestedAreaID = PlayerStats.Instance.FacingCellParentID;
            INestedArea parentNestedArea = mapGenerator.FindNestedAreaBasedOnNestedAreaID(parentNestedAreaID);

            if (parentNestedArea != null)
            {
                Vector2Int entrancePosition = PlayerStats.Instance.FacingCellCoordinates;
                MoveToNestedAreaPosition(parentNestedAreaID, entrancePosition);

                currentNestedArea = parentNestedArea;
                nestedMapPosition = entrancePosition;

                mapDisplayUI.UpdateNestedMapDisplay(parentNestedArea);
                UpdateNestedAreaStats();
                UpdateDescription();
                UpdateAdaptiveActionMenu();

                Debug.Log("Player left the current nested area and returned to the parent nested area.");
            }
            else
            {
                Debug.LogWarning($"Parent nested area with ID {parentNestedAreaID} not found.");
            }
        }
        else
        {
            Debug.LogWarning("Player is not currently in a nested area.");
        }
        endOfTurnManager.ConvertNestedTurnsToTime();
    }

    private void MoveToNestedAreaPosition(int nestedAreaID, Vector2Int position)
    {
        INestedArea nestedArea = mapGenerator.FindNestedAreaBasedOnNestedAreaID(nestedAreaID);
        if (nestedArea == null || !nestedArea.IsValidPosition(position))
        {
            Debug.LogError($"Invalid nested area ID {nestedAreaID} or position {position}.");
            return;
        }

        if (isInNestedArea)
        {
            nestedMapPosition = position;
            currentNestedArea.UpdatePlayerPosition(position);
            Debug.Log($"Moved to position {position} within NestedArea {nestedAreaID}.");
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
        mapGenerator.map[position.x, position.y].isPlayerPresent = true;
        mapGenerator.map[position.x, position.y].nestedAreaCanBeSeen = true;
        currentRegion = mapGenerator.GetCell(position).RegionNumber;
        endOfTurnManager.EndTurn();

        Cell currentCell = mapGenerator.GetCell(position);
        if (currentCell?.hasNestedArea == true) currentCell.nestedAreaCanBeSeen = true;

        if (currentCell?.isNPCGroupPresent == true)
        {
            NPCGroup npcGroup = npcManager.FindNPCGroupAtPosition(currentCell.Coordinates);
            if (npcGroup != null) npcManager.PlaceNPCs(currentCell.NestedArea, npcGroup);
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
        // Call PlayerTurnCompleted after the player performs an action
        TurnManager.Instance.PlayerTurnCompleted();
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

    /*
    private void UpdatePlayerStatsInstance()
    {
        PlayerStats.Instance.UpdatePosition(playerPosition);
        PlayerStats.Instance.UpdatePreviousPosition(previousPosition);
        PlayerStats.Instance.UpdateIsInAreas(isInNestedArea, isInMainMap);
        PlayerStats.Instance.UpdateMainMapPosition(mainMapPosition, currentRegion);
        PlayerStats.Instance.UpdatePlayerFacing(currentDirection);
        PlayerStats.Instance.UpdateCurrentCell(currentPlayerCell);

        if (isInNestedArea) UpdateNestedAreaStats();
        else PlayerStats.Instance.ResetNestedArea();
    }

    private void UpdateNestedAreaStats()
    {
        PlayerStats.Instance.UpdateNestedMapPosition(nestedMapPosition);
        PlayerStats.Instance.UpdatePreviousNestedMapPosition(previousNestedMapPosition);
        PlayerStats.Instance.UpdateCurrentNestedArea(currentNestedArea);
        PlayerStats.Instance.UpdateParentNestedAreaID(parentNestedAreaID);
        PlayerStats.Instance.UpdateCurrentNestedAreaID(currentNestedAreaID);
        PlayerStats.Instance.UpdatePreviousNestedAreaID(previousNestedAreaID);
    } 
    
     */



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

            if (PlayerStats.Instance.ActionPoints >= actionPointCost)
            {
                // Deduct AP
                PlayerStats.Instance.ActionPoints -= actionPointCost;
                baseOnClickAction.Invoke();

                // End the turn only if all AP has been used
                if (PlayerStats.Instance.ActionPoints == 0)
                {
                    TurnManager.Instance.PlayerTurnCompleted();
                }
            }
            else
            {
                // Store the remaining AP cost for the next turn
                PlayerStats.Instance.PendingActionPointsCost = actionPointCost - PlayerStats.Instance.ActionPoints;
                PlayerStats.Instance.ActionPoints = 0;
                PlayerStats.Instance.HasPendingAction = true;

                // End the turn since AP is exhausted
                TurnManager.Instance.PlayerTurnCompleted();
            }

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
                TurnManager.Instance.PlayerTurnCompleted();
            }
        }
    }

    public void DeductMovePoints(int amount)
    {
        PlayerStats.Instance.MovePoints -= amount;

        // Check if MovePoints are depleted
        if (PlayerStats.Instance.MovePoints <= 0)
        {
            // Check if the player is NOT in combat
            if (!PlayerStats.Instance.InCombat)
            {
                // If the player is out of MovePoints and not in combat, complete the player's turn
                TurnManager.Instance.PlayerTurnCompleted();
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
                UpdateAdaptiveActionMenu(); // Update the action menu
            }
            else
            {
                // If AP is depleted after handling the pending action, complete the player's turn
                TurnManager.Instance.PlayerTurnCompleted();
            }
        }
        else
        {
            // If the player still doesn't have enough AP to complete the pending action
            PlayerStats.Instance.PendingActionPointsCost -= PlayerStats.Instance.ActionPoints;
            PlayerStats.Instance.ActionPoints = 0;
            TurnManager.Instance.PlayerTurnCompleted();
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
