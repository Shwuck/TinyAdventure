using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    #region Singleton Pattern
    public static PlayerStats Instance { get; private set; }
    #endregion

    #region References
    public PlayerController playerController;
    #endregion

    #region Player Characters
    public PlayerCharacter CurrentPlayerCharacter;
    public string PlayerCharacterFirstName;
    public string PlayerCharacterName;
    public int PlayerID;
    #endregion

    #region Player Stats
    public int Health;
    public int MaxHealth;
    public int AttackPower;
    public int Speed;
    public int Defence;
    public int MaxActionPoints;
    public int ActionPoints;
    public int MaxMovePoints;
    public int MovePoints;
    public int PendingActionPointsCost;
    public bool HasPendingAction;
    public bool RegisteredInTurnManager;
    public bool Attacking;

    // Derived stats
    public int Strength => GetStatValue("Strength");
    public int Dexterity => GetStatValue("Dexterity");
    public int Constitution => GetStatValue("Constitution");
    public int Intelligence => GetStatValue("Intelligence");
    public int Wisdom => GetStatValue("Wisdom");
    public int Charisma => GetStatValue("Charisma");
    public int Luck => GetStatValue("Luck");
    public int Perception => GetStatValue("Perception");
    #endregion

    #region Ongoing Stats
    public int Satiety;
    public int MaxSatiety;
    public int MaxStamina;
    public int Stamina;
    public int TravelSpeed = 3;
    public int PartySize = 1;
    public int Money;
    public int HoursAlive;
    #endregion

    #region Visibility
    public bool CanBeSeen;
    public bool IsVisibleToNPCs { get; private set; }
    public int InteractingWithID { get; set; }
    #endregion

    #region Player Details
    public string PlayerName { get; set; }
    #endregion

    #region Facing Cell
    public Direction PlayerFacing;
    public int FacingCellID;
    public float FacingCellNoiseValue;
    public Cell FacingCell;
    public Vector2Int FacingCellCoordinates;
    public int FacingCellParentID;
    public int FacingCellCurrentID;
    public int FacingCellChildID;
    public bool FacingCellHasNestedArea;
    #endregion

    #region Map Positions
    public Vector2Int MainMapPosition;
    public Cell CurrentCell;
    public Cell PreviousCell;
    public int CurrentRegionNumber;
    public Vector2Int Position;
    public Vector2Int PreviousPosition;
    public Vector2Int NestedMapPosition;
    public Vector2Int PreviousNestedMapPosition;
    #endregion

    #region Nested Area
    public INestedArea CurrentNestedArea { get; set; }
    public int ParentNestedAreaID;
    public int CurrentNestedAreaID;
    public int PreviousNestedAreaID;
    public int CurrentCellID;
    public int NestedAreaLevel;
    #endregion

    #region Panels
    public KeyboardPanel KeyboardPanel = KeyboardPanel.Default;
    public AdapativeActionMenu AdaptiveActionMenuPanel = AdapativeActionMenu.IInteractables;
    #endregion

    #region Map Status
    public bool IsInMainMap;
    public bool IsInNestedArea;
    #endregion

    #region Player Home
    public Cell PlayerHome { get; set; }
    #endregion

    #region Player Booleans
    public bool HasEaten { get; set; }
    public bool HasDrank { get; set; }
    public bool InCombat { get; set; }
    public bool SwapOutputs = false;
    #endregion

    #region Party Info
    // public int PartySize {get; set;}
    #endregion


    #region Initialization
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Ensure the object persists between scenes
            InitializePlayerStats();
        }
        else
        {
            Destroy(gameObject); // Destroy duplicate instances
        }
    }

    private void InitializePlayerStats()
    {
        Debug.Log("Initialized Player Stats");
        IsInNestedArea = false;
        MaxHealth = 100;
        Health = MaxHealth;
        AttackPower = 50;
        MaxStamina = 0;
        Stamina = 0;
    }
    #endregion

    #region Health and Damage Methods
    public void ModifyHealth(int amount)
    {
        Health += amount;
        Health = Mathf.Clamp(Health, 0, MaxHealth);

        if (Health <= 0)
        {
            Die();
        }
    }

    public void ModifyAttackPower(int amount)
    {
        AttackPower += amount;
        AttackPower = Mathf.Max(AttackPower, 0);
    }

    public void Die()
    {
        var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;

        UIController.Instance.OnPlayerDeath(playerCharacter);
    }
    #endregion

    #region Position Updates

    public void UpdatePosition(Vector2Int newPosition)
    {
        Position = newPosition;
        UpdateCurrentPlayerCharacterPosition();
    }

    public void UpdateNestedAreaPosition(Vector2Int nestedMapPosition)
    {
        NestedMapPosition = nestedMapPosition;
        UpdateCurrentPlayerCharacterPosition();
    }

    public void UpdateMainMapPosition(Vector2Int mainMapPosition, int currentRegionNumber)
    {
        MainMapPosition = mainMapPosition;
        CurrentRegionNumber = currentRegionNumber;
        UpdateCurrentPlayerCharacterPosition();
    }

    public void UpdateCurrentCell(Cell currentCell)
    {
        CurrentCell = currentCell;
    }

    public void UpdateCurrentNestedArea(INestedArea newNestedArea)
    {
        CurrentNestedArea = newNestedArea;

        if (CurrentPlayerCharacter != null)
        {
            CurrentPlayerCharacter.CurrentNestedArea = newNestedArea;
        }
    }

    public void UpdatePreviousPosition(Vector2Int previousPosition)
    {
        PreviousPosition = previousPosition;

        if (CurrentPlayerCharacter != null)
        {
            CurrentPlayerCharacter.PreviousPosition = previousPosition;
        }
    }

    public void UpdatePreviousNestedMapPosition(Vector2Int previousNestedMapPosition)
    {
        PreviousNestedMapPosition = previousNestedMapPosition;

        if (CurrentPlayerCharacter != null)
        {
            CurrentPlayerCharacter.PreviousNestedMapPosition = previousNestedMapPosition;
        }
    }

    public void UpdateCurrentCellID(int currentCellID)
    {
        CurrentCellID = currentCellID;

        if (CurrentPlayerCharacter != null)
        {
            CurrentPlayerCharacter.CurrentCellID = currentCellID;
        }
    }

    public void UpdateCurrentNestedAreaID(int currentNestedAreaID)
    {
        CurrentNestedAreaID = currentNestedAreaID;

        if (CurrentPlayerCharacter != null)
        {
            CurrentPlayerCharacter.CurrentNestedAreaID = currentNestedAreaID;
        }
    }

    public void UpdatePreviousNestedAreaID(int previousNestedAreaID)
    {
        PreviousNestedAreaID = previousNestedAreaID;

        if (CurrentPlayerCharacter != null)
        {
            CurrentPlayerCharacter.PreviousNestedAreaID = previousNestedAreaID;
        }
    }

    public void UpdateParentNestedAreaID(int parentNestedAreaID)
    {
        ParentNestedAreaID = parentNestedAreaID;

        if (CurrentPlayerCharacter != null)
        {
            CurrentPlayerCharacter.ParentNestedAreaID = parentNestedAreaID;
        }
    }

    public void ResetNestedArea()
    {
        NestedMapPosition = Position;
        CurrentNestedArea = null;
        ParentNestedAreaID = 0;
        CurrentNestedAreaID = 0;

        if (CurrentPlayerCharacter != null)
        {
            CurrentPlayerCharacter.NestedMapPosition = Position;
            CurrentPlayerCharacter.CurrentNestedArea = null;
            CurrentPlayerCharacter.ParentNestedAreaID = 0;
            CurrentPlayerCharacter.CurrentNestedAreaID = 0;
        }
    }

    public void UpdateIsInAreas(bool isInNestedArea, bool isInMainMap)
    {
        IsInNestedArea = isInNestedArea;
        IsInMainMap = isInMainMap;

        if (CurrentPlayerCharacter != null)
        {
            CurrentPlayerCharacter.IsInNestedArea = isInNestedArea;
            CurrentPlayerCharacter.IsInMainMap = isInMainMap;
        }
    }

    private void UpdateCurrentPlayerCharacterPosition()
    {
        if (CurrentPlayerCharacter != null)
        {
            CurrentPlayerCharacter.Position = Position;
            CurrentPlayerCharacter.NestedMapPosition = NestedMapPosition;
        }
    }

    #endregion



    #region Money Management
    public void AddMoney(int amount)
    {
        Money += amount;
    }

    public void RemoveMoney(int amount)
    {
        Money -= amount;
    }
    #endregion

    #region Visibility Management
    public void SetVisibilityToNPCs(bool isVisible)
    {
        IsVisibleToNPCs = isVisible;
    }

    public void UpdateVisibility()
    {
        if (CurrentNestedArea != null)
        {
            CanBeSeen = false;
            var npcs = CurrentNestedArea.GetAllNPCsInArea();

            foreach (var npc in npcs)
            {
                if (npc.IsPlayerVisible)
                {
                    CanBeSeen = true;
                    break;
                }
            }
        }
        else
        {
            CanBeSeen = false;
        }
    }
    #endregion

    #region Direction and Facing
    public void UpdatePlayerFacing(Direction currentDirection)
    {
        PlayerFacing = currentDirection;
    }

    public Direction GetPlayerShoveDirection()
    {
        return PlayerFacing switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            Direction.West => Direction.East,
            _ => Direction.North
        };
    }

    public void UpdateFacingCell(Cell facingCell)
    {
        FacingCell = facingCell;
        FacingCellNoiseValue = facingCell.NoiseValue;
        FacingCellID = facingCell.CellID;
        FacingCellCoordinates = facingCell.Coordinates;
        FacingCellHasNestedArea = facingCell.hasNestedArea;
        FacingCellParentID = facingCell.ParentAreaID;
        FacingCellCurrentID = facingCell.CurrentAreaID;
        FacingCellChildID = facingCell.ChildAreaID;
    }
    #endregion

    #region Nested Area Management
    public void DescendIntoNestedArea()
    {
        if (FacingCellHasNestedArea)
        {
            Cell cellToDescendTo = CurrentNestedArea.GetCellAtPosition(FacingCellCoordinates);
            playerController.EnterNestedAreaWithinNestedArea(cellToDescendTo);
            Debug.Log("Descend called from PlayerStats");
        }
        else if (!FacingCellHasNestedArea)
        {
            playerController.TryEnterOrGenerateNestedArea();
        }
    }

    public void AscendOutOfNestedArea()
    {
        playerController.LeaveNestedAreaToParent();
    }
    #endregion

    #region Dungeon Management

    public void EnterDungeon()
    {
        if (FacingCellHasNestedArea)
        {
            Cell cellToEnter = CurrentNestedArea.GetCellAtPosition(FacingCellCoordinates);
            playerController.EnterNestedAreaWithinNestedArea(cellToEnter);
            Debug.Log($"Entering Dungeon at Cell Coordinates: {FacingCellCoordinates.x}, {FacingCellCoordinates.y}");
        }
        else if (!FacingCellHasNestedArea)
        {
            playerController.TryEnterOrGenerateNestedArea();
        }
    }

    public void ExitDungeon()
    {
        if (IsInNestedArea && CurrentNestedArea is DungeonNestedArea)
        {
            CurrentNestedArea = null;
            UpdateCurrentNestedAreaID(0);
            IsInNestedArea = false;
            IsInMainMap = true;
            Debug.Log("Exited dungeon.");
        }
    }

    public void EnterCave()
    {
        if (FacingCellHasNestedArea)
        {
            Cell cellToEnter = CurrentNestedArea.GetCellAtPosition(FacingCellCoordinates);
            playerController.EnterNestedAreaWithinNestedArea(cellToEnter);
            Debug.Log($"Entering Dungeon at Cell Coordinates: {FacingCellCoordinates.x}, {FacingCellCoordinates.y}");
        }
        else if (!FacingCellHasNestedArea)
        {
            playerController.TryEnterOrGenerateNestedArea();
        }
    }


    #endregion

    #region Player Character Management
    public void AddPlayerCharacter(PlayerCharacter playerCharacter)
    {
        PermaLists.Instance.PlayerCharacters ??= new List<PlayerCharacter>();
        PermaLists.Instance.PlayerCharacters.Add(playerCharacter);
        Debug.Log($"Added {playerCharacter.FirstName} to PlayerCharacters.");
    }

    public void UpdateCurrentPlayerCharacter()
    {
        Debug.Log("UpdateCurrentPlayerCharacter Called");
        PlayerCharacter selectedCharacter = null;
        int highestCharacterID = -1;

        foreach (var character in PermaLists.Instance.PlayerCharacters)
        {
            if (character.IsActive && (selectedCharacter == null || character.PlayerCharacterID > highestCharacterID))
            {
                selectedCharacter = character;
                highestCharacterID = character.PlayerCharacterID;
            }
        }

        if (selectedCharacter != null)
        {
            CurrentPlayerCharacter = selectedCharacter;
            PlayerCharacterFirstName = selectedCharacter.FirstName;
            PlayerCharacterName = selectedCharacter.FullName;
            MaxActionPoints = selectedCharacter.MaxActionPoints;
            ActionPoints = selectedCharacter.ActionPoints;
            MaxMovePoints = selectedCharacter.MaxMovePoints;
            MaxSatiety = 100;
            Satiety = 100;
            MovePoints = selectedCharacter.MovePoints;
            SyncStaminaFromCurrentPlayerCharacter();
            Debug.Log($"Selected PlayerCharacter: {selectedCharacter.FirstName} with ID: {selectedCharacter.PlayerCharacterID}");

            // Switch to the selected character's inventory
            PlayerInventory.Instance.SwitchCharacterInventory(selectedCharacter);
            Debug.Log($"Switched inventory to character ID: {selectedCharacter.PlayerCharacterID}");
        }
        else
        {
            Debug.LogWarning("No active PlayerCharacter found.");
        }

        UIController.Instance.UpdatePlayerNameButtonText();
    }

    public void ValidatePlayerCharacter()
    {
        if (CurrentPlayerCharacter == null)
        {
            Debug.LogWarning("No player character found. Attempting to assign one...");

            // Try to get the last created player character from PermaLists
            if (PermaLists.Instance.PlayerCharacters.Count > 0)
            {
                CurrentPlayerCharacter = PermaLists.Instance.PlayerCharacters.Last();
                Debug.Log($"Assigned last created player character: {CurrentPlayerCharacter.FullName}");
            }
            else
            {
                Debug.LogError("No valid player characters exist. Creating a default character...");
                CurrentPlayerCharacter = PlayerCharacterFactory.GenerateDefaultCharacter();
            }
        }

        // Ensure the inventory is properly assigned to the player
        if (CurrentPlayerCharacter != null)
        {
            PlayerInventory.Instance.SwitchCharacterInventory(CurrentPlayerCharacter);
            SyncStaminaFromCurrentPlayerCharacter();
            Debug.Log($"Validated player character: {CurrentPlayerCharacter.FullName}");
        }
    }

    public void SyncStaminaFromCurrentPlayerCharacter()
    {
        if (CurrentPlayerCharacter == null)
        {
            MaxStamina = 0;
            Stamina = 0;
            return;
        }

        MaxStamina = CurrentPlayerCharacter.MaxStamina;
        Stamina = CurrentPlayerCharacter.CurrentStamina;
    }

    #endregion

    #region Misc Methods
    public void AssignMount(Character characterToMount)
    {
        if (CurrentPlayerCharacter != null)
        {
            CurrentPlayerCharacter.Mount = characterToMount;
            Debug.Log($"{CurrentPlayerCharacter.Name} has been assigned a new mount: {characterToMount.Name}");
        }
        else
        {
            Debug.LogWarning("No current player character to assign a mount to.");
        }
    }
    #endregion

    #region Stat Calculation

    public int GetStatValue(string statName)
    {
        if (CurrentPlayerCharacter == null) return 0;

        int baseValue = statName switch
        {
            "Strength" => CurrentPlayerCharacter.Strength,
            "Dexterity" => CurrentPlayerCharacter.Dexterity,
            "Constitution" => CurrentPlayerCharacter.Constitution,
            "Intelligence" => CurrentPlayerCharacter.Intelligence,
            "Wisdom" => CurrentPlayerCharacter.Wisdom,
            "Charisma" => CurrentPlayerCharacter.Charisma,
            "Luck" => CurrentPlayerCharacter.Luck,
            "Perception" => GetPerception(), // Calls separate function
            _ => 0
        };

        int modifiers = GetEquippedItemModifiers(statName);

        return baseValue + modifiers;
    }

    public int GetEquippedItemModifiers(string statName)
    {
        if (CurrentPlayerCharacter == null) return 0;

        int modifiers = 0;
        var equippedItems = CurrentPlayerCharacter.EquippedItems; // FIXED: Pull from Character, not Inventory

        foreach (var item in equippedItems.Values)
        {
            if (item != null && item.StatModifiers.TryGetValue(statName, out int value))
            {
                modifiers += value;
            }
        }

        return modifiers;
    }

    private int GetPerception()
    {
        if (CurrentPlayerCharacter == null) return 0;

        int baseWisdom = CurrentPlayerCharacter.Wisdom; // FIXED: Avoids infinite loop
        int modifiers = GetEquippedItemModifiers("Perception");

        return baseWisdom + modifiers;
    }

    #endregion


    #region Hunger Management
    public void IncreaseHunger(int hunger)
    {
        if (!HasEaten)
        {
            Satiety -= hunger;
            Satiety = Mathf.Clamp(Satiety, -100, 100);  // Ensure Satiety stays between -100 and 100
        }

        HasEaten = false;
    }

    public void DecreaseHunger(int hunger)
    {
        Satiety += hunger;
        Satiety = Mathf.Clamp(Satiety, -100, 100);  // Ensure Satiety stays between -100 and 100

        HasEaten = true;
    }

    public void AddHoursAlive()
    {
        HoursAlive++;
    }
    #endregion

    #region Combat Methods

    public int GetAttackDamage(string primaryStat)
    {
        return PlayerStats.Instance.CurrentPlayerCharacter.GetAttackDamage(primaryStat);
    }

    private Dictionary<DamageType, int> GetWeaponDamage()
    {
        return PlayerStats.Instance.CurrentPlayerCharacter.GetWeaponDamage();
    }

    public int GetDefence()
    {
        CombatActionResolutionDiagnosticsLogger.LogEvent("[WRAPPER]", "PlayerStats.GetDefence delegated to CurrentPlayerCharacter.GetDefence",
            $"CurrentPlayerCharacter={CurrentPlayerCharacter?.Name ?? "NULL"}",
            CurrentPlayerCharacter);
        return PlayerStats.Instance.CurrentPlayerCharacter.GetDefence();
    }

    private int GetTotalArmourValue()
    {
        return PlayerStats.Instance.CurrentPlayerCharacter.GetDefence();
    }

    public float GetCriticalHitChance()
    {
        return PlayerStats.Instance.CurrentPlayerCharacter.GetCriticalHitChance();
    }

    public float GetCriticalHitMultiplier()
    {
        return PlayerStats.Instance.CurrentPlayerCharacter.GetCriticalHitMultiplier();
    }

    #endregion


    #region Damage and Resistance

    public float GetResistance(string damageType)
    {
        if (CurrentPlayerCharacter == null) return 0;

        float resistance = 0;
        var equippedItems = CurrentPlayerCharacter.EquippedItems; // FIXED: Use Character's EquippedItems

        foreach (var item in equippedItems.Values)
        {
            if (item?.Resistances != null && item.Resistances.TryGetValue(damageType, out float value)) // Null-check added
            {
                resistance += value;
            }
        }

        return resistance;
    }

    public void ApplyDamage(int damage, string damageType)
    {
        if (CurrentPlayerCharacter == null) return;

        float resistance = GetResistance(damageType);
        int finalDamage = Mathf.RoundToInt(damage * (1 - resistance / 100f));
        CombatActionResolutionDiagnosticsLogger.LogEvent("[WRAPPER]", "PlayerStats.ApplyDamage direct health mutation path",
            $"IncomingDamage={damage}\n" +
            $"DamageType={damageType}\n" +
            $"ResistanceUsed={resistance}\n" +
            $"FinalDamage={finalDamage}\n" +
            $"UsesCharacterTakeDamage={false}\n" +
            $"UsesBodyParts={false}\n" +
            $"AuthorityNote=This is not the main live combat damage path",
            null, CurrentPlayerCharacter);

        CurrentPlayerCharacter.ModifyHealth(-finalDamage); // Ensure it properly modifies health
    }

    #endregion


    public void ResetActionPoints()
    {
        Debug.Log($"Resetting Action Points - Current points = {ActionPoints}, resetting to = {MaxActionPoints}");

        ActionPoints = MaxActionPoints;
        if (HasPendingAction)
        {
            if (PendingActionPointsCost <= ActionPoints)
            {
                ActionPoints -= PendingActionPointsCost;
                PendingActionPointsCost = 0;
                HasPendingAction = false;
            }
            else
            {
                PendingActionPointsCost -= ActionPoints;
                ActionPoints = 0;
            }
        }
    }

    public void ResetMovePoints()
    {
        Debug.Log($"Resetting Move Points - Current points = {MovePoints}, resetting to = {MaxMovePoints}");

        MovePoints = MaxMovePoints;
    }



    public void ToggleOutput()
    {
        SwapOutputs = !SwapOutputs;
    }
}

public enum KeyboardPanel
{
    Default,
    MainMap,
    NestedArea,
    Trade,
    Donation,
    Container,
    VillageInfo,
    Dialogue,
    Death,
    Popup,
    Smithing,
    Crafting,
    Cooking,
    Hint,
    Inventory
}

public enum AdapativeActionMenu
{
    IInteractables,
    Combat,
    Special
}
