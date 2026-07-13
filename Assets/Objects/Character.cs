using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

public enum CharacterTurnDecisionResult
{
    None,
    Moved,
    Idled,
    PerformedAction,
    NoActionAvailable,
    FailedMovement,
    CombatAction,
    Skipped,
    TurnConsumed
}

public class Character : IInteractable
{
    #region Identification
    public int IInteractableID { get; set; }
    public virtual string Name { get; set; }
    public virtual string Description { get; set; }
    public virtual char Symbol { get; set; } = 'C';
    public virtual string Color { get; set; } = "#000000";
    #endregion

    #region Personal Information
    public int Age { get; set; }
    public string Home { get; set; }
    public int BirthdayDay { get; set; }
    public Season BirthdaySeason { get; set; }
    public int BirthdayYear { get; set; }
    #endregion

    #region Group Information
    public int GroupID { get; set; }
    public string Faction { get; set; } // Faction affiliation
    public Village HomeVillage { get; set; }
    #endregion

    #region Location
    public bool IsInMainMap { get; set; }
    public Cell CurrentCell { get; set; }
    public Vector2Int Position { get; set; }
    public Vector2Int NestedMapPosition { get; set; }
    public Vector2Int PreviousPosition { get; set; } // New property
    public Vector2Int PreviousNestedMapPosition { get; set; } // New property
    public bool IsInNestedArea { get; set; }
    public INestedArea CurrentNestedArea { get; set; }
    public int RegionNumber { get; set; }
    public int CurrentCellID { get; set; } // New property
    public int CurrentNestedAreaID { get; set; } // New property
    public int PreviousNestedAreaID { get; set; } // New property
    public int ParentNestedAreaID { get; set; } // New property
    public CoverType CoverType { get; set; }
    public bool IsInVillage { get; set; }
    public bool AvoidingPlayer { get; set; } = false;
    public bool CanLeaveArea { get; set; } = false;
    public bool IsCamped { get; set; }
    public int CampID { get; set; }
    // Legacy exploration fallback: currently stores raw cell coordinates that look situationally useful,
    // not a full role/affordance-driven intent system.
    public List<Vector2Int> CellsOfInterest { get; private set; } = new List<Vector2Int>();
    #endregion

    #region Status
    public int Level { get; set; }
    public int PowerRanking { get; set; }
    public bool IsActive { get; set; }
    public bool IsAlive { get; set; }
    public bool IsHostile { get; set; }
    public bool IsPassable { get; set; }
    public Direction DirectionFacing { get; set; } = Direction.South;
    public bool IsPlayerVisible { get; set; }
    public NPCStatus Status { get; set; } = NPCStatus.Idling;
    public NPCStance Stance { get; set; } = NPCStance.Neutral;
    public int WaitTime { get; set; }
    public bool InTurn { get; set; }
    public bool InCombat { get; set; }
    public Character Target { get; set; }
    public Character FollowTarget { get; set; }
    public float RemainingTurnTime { get; set; } = 1.0f;
    public CharacterTurnDecisionResult LastTurnDecisionResult { get; private set; } = CharacterTurnDecisionResult.None;
    public string LastTurnDecisionReason { get; private set; } = "Unresolved";
    #endregion

    #region Regarding Player
    public bool HasMetPlayer { get; set; }
    public float PlayerFavour { get; set; }
    public float AttitudeToPlayer { get; set; } // Min -100 max 100
    #endregion

    #region Attributes
    public int MaxHealth { get; set; }
    public int Health { get; set; }
    public int Awareness { get; set; }
    public int Speed { get; set; } = 1;
    public int MaxActionPoints { get; set; } = 5;
    public int ActionPoints { get; set; }
    public int MaxMovePoints = 2;
    public int MovePoints;
    public int MaxStamina { get; private set; } = 10;
    public int CurrentStamina { get; private set; } = 10;
    public int Charisma;
    public int Strength;
    public int Dexterity;
    public int Constitution;
    public int Wisdom;
    public int Luck;
    public int Intelligence;
    public int Perception;
    #endregion

    #region Misc Attributes
    public Diet Diet;
    public bool IsMountable;
    public bool IsTame;
    public Character IsTamedBy;
    public Character Mount;
    #endregion

    #region Anatomy

    public string BodyType { get; set; }
    public Anatomy Anatomy { get; set; }

    #endregion

    #region Equipment and Inventory

    public int Money;
    public int BaseMoney;
    public Loadout Loadout { get; set; }
    public CharacterInventory Inventory { get; set; } = new CharacterInventory();
    public Dictionary<EquipmentSlot, Item> EquippedItems { get; set; } = new Dictionary<EquipmentSlot, Item>();

    #endregion

    #region Technical
    private const string StaminaDiagnosticsTag = "CODEXLOG006_STAMINA_RESOURCE";
    private const int BaseStaminaValue = 10;
    private const int MinimumMaxStamina = 10;
    private const int StaminaConstitutionMultiplier = 2;
    private System.Random random = new System.Random();
#endregion

    #region Buffs & Debuffs
    public List<BuffDebuff> AffectedBy { get; set; } = new List<BuffDebuff>();

    public Dictionary<DamageType, float> Resistances = new Dictionary<DamageType, float>()
{
    { DamageType.Fire, 0 },      // Default 0% resistance
    { DamageType.Ice, 0 },
    { DamageType.Piercing, 0 },
    { DamageType.Slashing, 0 },
    { DamageType.Blunt, 0 },
    { DamageType.Magic, 0 }
};


    #endregion

    #region State Management

    public StateMachine stateMachine { get; protected set; }

    public virtual void InitializeStateMachine()
    {
        stateMachine = new StateMachine(this);
    }

    public void SetInitialState(IState initialState)
    {
        stateMachine.ChangeState(initialState);
    }

    public virtual void ChangeState(IState newState)
    {
        if (stateMachine != null)
        {
            stateMachine.ChangeState(newState);
        }
    }

    #endregion

    #region Constructor
    public Character()
    {
        IsAlive = true;
        InitializeInteractions();
        stateMachine = new StateMachine(this);
    }
    #endregion

    #region Stamina

    public int CalculateMaxStamina()
    {
        float constitutionValue = GetStatValue("Constitution");
        int calculatedMaxStamina = BaseStaminaValue + Mathf.RoundToInt(constitutionValue * StaminaConstitutionMultiplier);
        int clampedMaxStamina = Mathf.Max(MinimumMaxStamina, calculatedMaxStamina);

        if (constitutionValue <= 0f)
        {
            LogStaminaInfo($"CalculateMaxStamina used fallback-safe minimum because Constitution resolved to {constitutionValue}.");
        }
        else if (clampedMaxStamina != calculatedMaxStamina)
        {
            LogStaminaWarning($"CalculateMaxStamina clamped max stamina from {calculatedMaxStamina} to {clampedMaxStamina}.");
        }

        return clampedMaxStamina;
    }

    public void RecalculateMaxStamina(bool preservePercentage = false, string context = "Unknown")
    {
        int previousMaxStamina = MaxStamina;
        int previousCurrentStamina = CurrentStamina;
        float previousPercent = previousMaxStamina > 0 ? (float)previousCurrentStamina / previousMaxStamina : 1f;

        MaxStamina = CalculateMaxStamina();

        if (preservePercentage && previousMaxStamina > 0)
        {
            CurrentStamina = Mathf.RoundToInt(MaxStamina * previousPercent);
        }

        ClampStamina($"{context}:RecalculateMaxStamina");
        SyncPlayerStatsStaminaMirror();

        LogStaminaInfo($"RecalculateMaxStamina completed. Context={context}, PreservePercentage={preservePercentage}, Previous={previousCurrentStamina}/{previousMaxStamina}, Current={CurrentStamina}/{MaxStamina}");
    }

    public void InitializeStamina(string context = "Unknown")
    {
        MaxStamina = CalculateMaxStamina();
        CurrentStamina = MaxStamina;
        ClampStamina($"{context}:InitializeStamina");
        SyncPlayerStatsStaminaMirror();

        LogStaminaInfo($"InitializeStamina completed. Context={context}, Current={CurrentStamina}/{MaxStamina}");
    }

    public void ResetStamina(string context = "Unknown")
    {
        int previousCurrentStamina = CurrentStamina;
        CurrentStamina = MaxStamina;
        ClampStamina($"{context}:ResetStamina");
        SyncPlayerStatsStaminaMirror();

        LogStaminaInfo($"ResetStamina completed. Context={context}, PreviousCurrent={previousCurrentStamina}, Current={CurrentStamina}/{MaxStamina}");
    }

    public void ClampStamina(string context = "Unknown")
    {
        int previousMaxStamina = MaxStamina;
        int previousCurrentStamina = CurrentStamina;

        MaxStamina = Mathf.Max(MinimumMaxStamina, MaxStamina);
        CurrentStamina = Mathf.Clamp(CurrentStamina, 0, MaxStamina);

        if (previousMaxStamina != MaxStamina || previousCurrentStamina != CurrentStamina)
        {
            LogStaminaWarning($"ClampStamina adjusted values. Context={context}, Previous={previousCurrentStamina}/{previousMaxStamina}, Current={CurrentStamina}/{MaxStamina}");
        }
    }

    public bool CanSpendStamina(int amount)
    {
        if (amount < 0)
        {
            return false;
        }

        if (amount == 0)
        {
            return true;
        }

        return CurrentStamina >= amount;
    }

    public bool SpendStamina(int amount, string reason = "")
    {
        if (amount < 0)
        {
            LogStaminaWarning($"SpendStamina rejected negative spend. Amount={amount}, Reason={reason}");
            return false;
        }

        if (amount == 0)
        {
            return true;
        }

        if (!CanSpendStamina(amount))
        {
            LogStaminaWarning($"SpendStamina rejected insufficient stamina spend. Requested={amount}, Current={CurrentStamina}, Reason={reason}");
            return false;
        }

        int previousCurrentStamina = CurrentStamina;
        CurrentStamina -= amount;
        ClampStamina($"{reason}:SpendStamina");
        SyncPlayerStatsStaminaMirror();

        LogStaminaInfo($"SpendStamina completed. Reason={reason}, Spend={amount}, PreviousCurrent={previousCurrentStamina}, Current={CurrentStamina}/{MaxStamina}");
        return true;
    }

    public void RestoreStamina(int amount, string reason = "")
    {
        if (amount < 0)
        {
            LogStaminaWarning($"RestoreStamina rejected negative restore. Amount={amount}, Reason={reason}");
            return;
        }

        if (amount == 0)
        {
            return;
        }

        int previousCurrentStamina = CurrentStamina;
        CurrentStamina += amount;
        ClampStamina($"{reason}:RestoreStamina");
        SyncPlayerStatsStaminaMirror();

        LogStaminaInfo($"RestoreStamina completed. Reason={reason}, Restore={amount}, PreviousCurrent={previousCurrentStamina}, Current={CurrentStamina}/{MaxStamina}");
    }

    public float GetStaminaPercent()
    {
        if (MaxStamina <= 0)
        {
            return 0f;
        }

        return (float)CurrentStamina / MaxStamina;
    }

    public int GetStaminaRecoveryPerTurn()
    {
        return Mathf.Max(1, Mathf.RoundToInt(GetStatValue("Constitution") * 0.25f));
    }

    public int GetStaminaRecoveryOnWait()
    {
        return Mathf.Max(GetStaminaRecoveryPerTurn(), 2);
    }

    public void RecoverStaminaForTurn(string context)
    {
        RestoreStamina(GetStaminaRecoveryPerTurn(), $"{context}:RecoverStaminaForTurn");
    }

    public void RecoverStaminaOnWait(string context)
    {
        RestoreStamina(GetStaminaRecoveryOnWait(), $"{context}:RecoverStaminaOnWait");
    }

    public void RecoverStaminaOnRest(string context)
    {
        RestoreStamina(Mathf.Max(GetStaminaRecoveryOnWait(), Mathf.RoundToInt(MaxStamina * 0.25f)), $"{context}:RecoverStaminaOnRest");
    }

    public void RecoverStaminaFully(string context)
    {
        RestoreStamina(MaxStamina, $"{context}:RecoverStaminaFully");
    }

    private void SyncPlayerStatsStaminaMirror()
    {
        if (PlayerStats.Instance?.CurrentPlayerCharacter != this)
        {
            return;
        }

        PlayerStats.Instance.MaxStamina = MaxStamina;
        PlayerStats.Instance.Stamina = CurrentStamina;
    }

    private void LogStaminaInfo(string message)
    {
        if (GameDebugger.Instance == null)
        {
            return;
        }

        GameDebugger.Instance.LogInfo($"{StaminaDiagnosticsTag} {Name}: {message}");
    }

    private void LogStaminaWarning(string message)
    {
        if (GameDebugger.Instance == null)
        {
            return;
        }

        GameDebugger.Instance.LogWarning($"{StaminaDiagnosticsTag} {Name}: {message}");
    }

    #endregion

    #region Movement

    public bool Move(Direction direction) => TryMove(direction);

    public bool MoveInDirection(Direction direction) => TryMove(direction);

    public void ResetMovePointsForTurn()
    {
        // Centralized so future turn movement rules can account for status, species, injuries, encumbrance, terrain, or other modifiers.
        MovePoints = MaxMovePoints;
    }

    public void ResetTurnDecision()
    {
        LastTurnDecisionResult = CharacterTurnDecisionResult.None;
        LastTurnDecisionReason = "Unresolved";
    }

    public void RecordTurnDecision(CharacterTurnDecisionResult result, string reason)
    {
        LastTurnDecisionResult = result;
        LastTurnDecisionReason = string.IsNullOrWhiteSpace(reason) ? "Unspecified" : reason;
    }

    public void ConsumeRemainingActionPointsForTurn(string source)
    {
        if (ActionPoints <= 0)
        {
            return;
        }

        SpendActionPoints(ActionPoints, source);
    }

    private bool TryMove(Direction direction)
    {
        Vector2Int sourcePosition = NestedMapPosition;
        Vector2Int targetPosition = NestedMapPosition + DirectionToVector(direction);
        int currentCellObjectCount = -1;
        int targetCellObjectCount = -1;
        bool targetPassable = false;
        if (CurrentNestedArea != null && IsWithinMapBounds(sourcePosition, CurrentNestedArea))
        {
            Cell currentCell = CurrentNestedArea.GetCellAtPosition(sourcePosition);
            currentCellObjectCount = currentCell?.Objects?.Count ?? -1;
        }

        if (CurrentNestedArea != null && IsWithinMapBounds(targetPosition, CurrentNestedArea))
        {
            Cell targetCell = CurrentNestedArea.GetCellAtPosition(targetPosition);
            targetCellObjectCount = targetCell?.Objects?.Count ?? -1;
            targetPassable = targetCell != null && targetCell.isPassable && (targetCell.Objects == null || targetCell.Objects.All(obj => obj != null && obj.IsPassable));
        }

        // CODEXLOG002_MOVEMENT_AI: temporary movement-attempt diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[MOVEMENT]", "Character.TryMove begin",
            $"Movement attempted: True\n" +
            $"Direction: {direction}\n" +
            $"Source position: {sourcePosition}\n" +
            $"Target position: {targetPosition}\n" +
            $"Current area: {MovementAIDiagnosticsLogger.FormatArea(CurrentNestedArea)}\n" +
            $"MovePoints before: {MovePoints}\n" +
            $"Target passable precheck: {targetPassable}\n" +
            $"Old cell occupant count: {currentCellObjectCount}\n" +
            $"New cell occupant count: {targetCellObjectCount}",
            this);
        // CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic.
        NestedMapDebugger.LogSnapshotForMovement(CurrentNestedArea, this, "SNAPSHOT_BEFORE_ENTITY_MOVE");

        if (MovePoints <= 0)
        {
            GameDebugger.Instance.LogInfo($"{Name} cannot move {direction}; no MovePoints remain.");
            // CODEXLOG002_MOVEMENT_AI: temporary movement-attempt diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("Character.TryMove blocked",
                $"Movement attempted: True\nBlocked reason: no MovePoints\nPosition changed: False\nPosition after: {NestedMapPosition}",
                this);
            // CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic.
            NestedMapDebugger.LogSnapshotForMovement(CurrentNestedArea, this, "SNAPSHOT_AFTER_ENTITY_MOVE_BLOCKED_NO_MP");
            return false;
        }

        if (!IsValidMove(targetPosition))
        {
            // CODEXLOG002_MOVEMENT_AI: temporary movement-attempt diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("Character.TryMove blocked",
                $"Movement attempted: True\nBlocked reason: IsValidMove returned false\nTarget position: {targetPosition}\nPosition changed: False\nPosition after: {NestedMapPosition}",
                this);
            // CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic.
            NestedMapDebugger.LogSnapshotForMovement(CurrentNestedArea, this, "SNAPSHOT_AFTER_ENTITY_MOVE_BLOCKED_INVALID");
            return false;
        }

        ExecuteMovement(direction, targetPosition);
        MovePoints--;

        GameDebugger.Instance.LogInfo($"{Name} moved {direction} to {targetPosition}. Remaining MovePoints: {MovePoints}");
        // CODEXLOG002_MOVEMENT_AI: temporary movement-attempt diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[MOVEMENT]", "Character.TryMove end",
            $"Movement attempted: True\n" +
            $"Movement succeeded: True\n" +
            $"Position before: {sourcePosition}\n" +
            $"Position after: {NestedMapPosition}\n" +
            $"Position changed: {NestedMapPosition != sourcePosition}\n" +
            $"MovePoints after: {MovePoints}\n" +
            $"Map refresh requested: False",
            this);
        // CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic.
        NestedMapDebugger.LogSnapshotForMovement(CurrentNestedArea, this, "SNAPSHOT_AFTER_ENTITY_MOVE");
        return true;
    }

    private bool IsValidMove(Vector2Int targetPosition)
    {
        if (!IsWithinMapBounds(targetPosition, CurrentNestedArea))
        {
            // CODEXLOG002_MOVEMENT_AI: temporary movement validation diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("Character.IsValidMove out of bounds",
                $"Target position: {targetPosition}\nCurrent area: {MovementAIDiagnosticsLogger.FormatArea(CurrentNestedArea)}",
                this);
            return HandleOutOfBoundsMovement();
        }

        if (!IsCellPassable(targetPosition, CurrentNestedArea))
        {
            Debug.LogWarning($"{Name} encountered an impassable cell at {targetPosition}.");
            // CODEXLOG002_MOVEMENT_AI: temporary movement validation diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("Character.IsValidMove blocked",
                $"Target position: {targetPosition}\nBlocked reason: impassable cell or blocking object",
                this);
            return false;
        }

        return true;
    }

    private bool HandleOutOfBoundsMovement()
    {
        if (CanLeaveArea)
        {
            LeaveArea();

            Debug.LogWarning($"{Name} left the area. Movement cancelled.");
            return false;
        }

        Debug.LogWarning($"{Name} cannot move outside the area boundary.");
        return false;
    }


    private void ExecuteMovement(Direction direction, Vector2Int targetPosition)
    {
        DirectionFacing = direction;
        Vector2Int positionBefore = NestedMapPosition;
        int oldCellBefore = -1;
        int newCellBefore = -1;
        bool oldCellRemoval = false;
        bool newCellAdd = false;

        // Debugging Logs
        if (CurrentNestedArea == null)
        {
            Debug.LogError($"ExecuteMovement: CurrentNestedArea is NULL for {Name} before getting cell at {NestedMapPosition}");
            // CODEXLOG002_MOVEMENT_AI: temporary movement execution diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("Character.ExecuteMovement aborted",
                $"Movement attempted: True\nBlocked reason: CurrentNestedArea null\nTarget position: {targetPosition}",
                this);
            return;
        }

        Debug.Log($"ExecuteMovement: {Name} is at {NestedMapPosition}, moving to {targetPosition}.");

        Cell currentCell = CurrentNestedArea.GetCellAtPosition(NestedMapPosition);
        if (currentCell == null)
        {
            Debug.LogError($"ExecuteMovement: Current cell at {NestedMapPosition} is NULL!");
        }
        oldCellBefore = currentCell?.Objects?.Count ?? -1;

        Cell targetCell = CurrentNestedArea.GetCellAtPosition(targetPosition);
        if (targetCell == null)
        {
            Debug.LogError($"ExecuteMovement: Target cell at {targetPosition} is NULL!");
        }
        newCellBefore = targetCell?.Objects?.Count ?? -1;

        if (currentCell?.Objects != null)
        {
            oldCellRemoval = currentCell.Objects.Remove(this);
        }
        if (currentCell != null) currentCell.isPassable = true;

        // Update Position
        NestedMapPosition = targetPosition;

        if (targetCell?.Objects != null)
        {
            targetCell.Objects.Add(this);
            newCellAdd = targetCell.Objects.Contains(this);
        }
        if (targetCell != null) CurrentCell = targetCell;
        if (targetCell != null) targetCell.isPassable = false;
        // CODEXLOG002_MOVEMENT_AI: temporary movement execution diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[MOVEMENT]", "Character.ExecuteMovement completed",
            $"Movement attempted: True\n" +
            $"Direction: {direction}\n" +
            $"Source position: {positionBefore}\n" +
            $"Target position: {targetPosition}\n" +
            $"Position after: {NestedMapPosition}\n" +
            $"Movement succeeded: {NestedMapPosition == targetPosition}\n" +
            $"Old cell removal: {oldCellRemoval}\n" +
            $"New cell add: {newCellAdd}\n" +
            $"Old cell occupant count before: {oldCellBefore}\n" +
            $"Old cell occupant count after: {currentCell?.Objects?.Count.ToString() ?? "NULL"}\n" +
            $"New cell occupant count before: {newCellBefore}\n" +
            $"New cell occupant count after: {targetCell?.Objects?.Count.ToString() ?? "NULL"}\n" +
            $"Map refresh requested: False",
            this);
    }


    public bool MoveTowards(Vector2Int targetPos)
    {
        if (MovePoints <= 0)
        {
            Debug.LogWarning($"{Name} has no MovePoints left to move.");
            // CODEXLOG002_MOVEMENT_AI: temporary movement-attempt diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("Character.MoveTowards blocked",
                $"Target position: {targetPos}\nBlocked reason: no MovePoints",
                this);
            return false;
        }

        // Determine the best direction to move towards target position
        Direction direction = GetDirection(NestedMapPosition, targetPos, true);
        // CODEXLOG002_MOVEMENT_AI: temporary movement-attempt diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[MOVEMENT]", "Character.MoveTowards selected direction",
            $"Selected movement target: {targetPos}\nSelected direction: {direction}",
            this);

        // Try to move in the calculated direction
        return TryMove(direction);
    }


    public void MoveRelativeToCharacter(Character target, bool moveTowards)
    {
        if (MovePoints <= 0)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary movement-attempt diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("Character.MoveRelativeToCharacter blocked",
                $"Target: {target?.Name ?? "NULL"}\nMoveTowards: {moveTowards}\nBlocked reason: no MovePoints",
                this);
            return;
        }

        Direction direction = GetDirection(NestedMapPosition, target.NestedMapPosition, moveTowards);
        // CODEXLOG002_MOVEMENT_AI: temporary movement-attempt diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[MOVEMENT]", "Character.MoveRelativeToCharacter selected direction",
            $"Target: {target?.Name ?? "NULL"}\nTarget position: {target?.NestedMapPosition.ToString() ?? "NULL"}\nMoveTowards: {moveTowards}\nSelected direction: {direction}",
            this);
        TryMove(direction);
    }

    public void MoveTowardsPlayer() => MoveRelativeToCharacter(PlayerStats.Instance.CurrentPlayerCharacter, true);
    public void MoveAwayFromPlayer() => MoveRelativeToCharacter(PlayerStats.Instance.CurrentPlayerCharacter, false);
    public void MoveTowardsCharacter(Character otherCharacter) => MoveRelativeToCharacter(otherCharacter, true);
    public void MoveAwayFromCharacter(Character otherCharacter) => MoveRelativeToCharacter(otherCharacter, false);

    public bool SimpleMovement(int cellsToMove, Direction direction)
    {
        Vector2Int positionBefore = NestedMapPosition;
        // CODEXLOG002_MOVEMENT_AI: temporary forced-movement diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[SHOVE]", "Character.SimpleMovement begin",
            $"Cells to move: {cellsToMove}\nDirection: {direction}\nTarget position first step: {NestedMapPosition + DirectionToVector(direction)}",
            this);
        for (int i = 0; i < cellsToMove; i++)
        {
            if (MovePoints <= 0 || !TryMove(direction))
            {
                // CODEXLOG002_MOVEMENT_AI: temporary forced-movement diagnostic.
                MovementAIDiagnosticsLogger.LogWarning("Character.SimpleMovement failed",
                    $"Forced movement attempted: True\nStep: {i + 1}\nPosition before: {positionBefore}\nPosition after: {NestedMapPosition}\nPosition changed: {NestedMapPosition != positionBefore}",
                    this);
                return false;
            }
        }
        // CODEXLOG002_MOVEMENT_AI: temporary forced-movement diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[SHOVE]", "Character.SimpleMovement completed",
            $"Forced movement attempted: True\nPosition before: {positionBefore}\nPosition after: {NestedMapPosition}\nPosition changed: {NestedMapPosition != positionBefore}",
            this);
        return true;
    }

    public bool MoveInRandomDirection()
    {
        if (MovePoints <= 0)
        {
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "Character.MoveInRandomDirection skipped",
                "Selected action: None\nReason for no movement: no MovePoints available before random movement probe",
                this);
            return false;
        }

        List<Direction> possibleDirections = Enum.GetValues(typeof(Direction))
            .Cast<Direction>()
            .Where(dir => IsValidMoveCandidate(NestedMapPosition + DirectionToVector(dir)))
            .ToList();
        // CODEXLOG002_MOVEMENT_AI: temporary AI movement decision diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "Character.MoveInRandomDirection candidates",
            $"Candidate count: {possibleDirections.Count}\nCandidates: {string.Join(", ", possibleDirections)}",
            this);

        if (possibleDirections.Count > 0)
        {
            Direction randomDirection = possibleDirections[UnityEngine.Random.Range(0, possibleDirections.Count)];
            // CODEXLOG002_MOVEMENT_AI: temporary AI movement decision diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "Character.MoveInRandomDirection selected",
                $"Selected action: Move\nSelected direction: {randomDirection}\nTarget cell: {NestedMapPosition + DirectionToVector(randomDirection)}",
                this);
            return TryMove(randomDirection);
        }

        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "Character.MoveInRandomDirection no valid move",
            "Selected action: None\nReason for no movement: no valid adjacent directions",
            this);
        return false;
    }

    private bool IsValidMoveCandidate(Vector2Int targetPosition)
    {
        INestedArea area = CurrentNestedArea;
        if (area == null)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI movement candidate diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("Character.MoveInRandomDirection candidate skipped",
                $"Target position: {targetPosition}\nReason: CurrentNestedArea null during candidate probe",
                this);
            return false;
        }

        if (!IsWithinMapBounds(targetPosition, area))
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI movement candidate diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "Character.MoveInRandomDirection candidate out of bounds",
                $"Target position: {targetPosition}\nCurrent area: {MovementAIDiagnosticsLogger.FormatArea(area)}\nCandidate accepted: False\nLeaveArea invoked: False",
                this);
            return false;
        }

        return IsCellPassable(targetPosition, area);
    }

    public virtual void LeaveArea()
    {
        Debug.Log($"{Name} is leaving the area.");

        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[ENTITY REMOVAL]", "Character.LeaveArea before deregistration/removal", null, this);
        TurnOrchestrator.Instance?.DeregisterCharacter(this);
        CurrentNestedArea?.RemoveObjectFromArea(this);

        IsInNestedArea = false;
        CurrentNestedArea = null;
        IsActive = false;
    }

    #endregion

    #region Direction Helpers

    private Direction GetDirection(Vector2Int fromPosition, Vector2Int toPosition, bool moveTowards)
    {
        Vector2Int directionVector = moveTowards ? toPosition - fromPosition : fromPosition - toPosition;

        if (directionVector == Vector2Int.zero)
        {
            return Direction.North;
        }

        Direction result = Mathf.Abs(directionVector.x) > Mathf.Abs(directionVector.y)
            ? (directionVector.x > 0 ? Direction.East : Direction.West)
            : (directionVector.y > 0 ? Direction.North : Direction.South);

        return result;
    }

    private Vector2Int DirectionToVector(Direction direction)
    {

        Vector2Int result = direction switch
        {
            Direction.North => new Vector2Int(0, 1),
            Direction.South => new Vector2Int(0, -1),
            Direction.East => new Vector2Int(1, 0),
            Direction.West => new Vector2Int(-1, 0),
            _ => Vector2Int.zero,
        };

        if (result == Vector2Int.zero)
        {
            GameDebugger.Instance.LogWarning($"DirectionToVector: Invalid direction {direction} received. Returning zero vector.");
        }

        return result;
    }

    private bool IsWithinMapBounds(Vector2Int position, INestedArea nestedArea)
    {
        nestedArea = GetNestedAreaOnNull(nestedArea);

        if (nestedArea == null)
        {
            Debug.LogError($"IsWithinMapBounds: Unable to determine NestedArea for position {position}");
            return false; // Prevents crash
        }

        Vector2Int mapSize = nestedArea.GetSize();
        return position.x >= 0 && position.x < mapSize.x && position.y >= 0 && position.y < mapSize.y;
    }


    private bool IsCellPassable(Vector2Int position, INestedArea nestedArea)
    {
        nestedArea = GetNestedAreaOnNull(nestedArea); // Attempt to recover if null

        if (nestedArea == null)
        {
            Debug.LogError("IsCellPassable: Could not resolve a valid nested area.");
            return false;
        }

        Cell cell = nestedArea.GetCellAtPosition(position);

        if (cell == null)
        {
            Debug.LogWarning($"IsCellPassable: Attempted to check an out-of-bounds or null cell at {position}");
            return false; // Treat null cells as impassable
        }

        if (cell.Objects == null)
        {
            Debug.LogWarning($"IsCellPassable: Cell at {position} has a null Objects list.");
            return cell.isPassable; // If objects list is null, assume no blocking objects exist
        }

        return cell.isPassable && cell.Objects.All(obj => obj != null && obj.IsPassable);
    }

    private INestedArea GetNestedAreaOnNull(INestedArea nestedArea)
    {
        if (nestedArea == null)
        {
            Debug.LogWarning("GetNestedAreaOnNull: nestedArea is NULL, attempting to retrieve from PlayerStats.");
            nestedArea = PlayerStats.Instance?.CurrentPlayerCharacter?.CurrentNestedArea;

            if (nestedArea == null)
            {
                Debug.LogError("GetNestedAreaOnNull: Could not retrieve a valid nested area!");
                return null; // Return null safely
            }
        }

        return nestedArea;
    }



    #endregion

    #region Combat

    protected List<IInteraction> combatInteractions;
    public List<OnHitEffect> OnHitEffects { get; private set; } = new List<OnHitEffect>();
    public List<OnHitEffect> OnHitTakenEffects { get; private set; } = new List<OnHitEffect>();
    private const float WeaponPrimaryStatScalingMultiplier = 0.5f;

    protected virtual void InitializeCombatInteractions()
    {
        combatInteractions = new List<IInteraction>
    {
        new SlashInteraction(),
        new BashInteraction(),
        new StabInteraction(),
        new RendInteraction(),
        new PunchInteraction()
    };
    }

    public int GetAttackDamage(string primaryStat)
    {
        float baseStatValue = GetStatValue(primaryStat);
        float weaponDamage = GetWeaponDamage().Values.Sum();
        float randomFactor = UnityEngine.Random.Range(-5.0f, 5.0f);
        float finalDamage = baseStatValue + weaponDamage + randomFactor;

        Debug.Log($"{Name} calculated attack damage: Primary Stat: {primaryStat}, Base Stat: {baseStatValue}, Weapon Damage: {weaponDamage}, Random Factor: {randomFactor}, Final Damage: {finalDamage}");
        return Mathf.RoundToInt(finalDamage);
    }

    public virtual void PerformAttack(Character target, DamageType damageType = DamageType.Bludgeoning)
    {
        GameDebugger.Instance.LogInfo("PerformAttack has been called!");
        int apBeforeAttack = ActionPoints;
        AttackContext attackContext = CombatResolver.CreatePhysicalAttackContext(this, target, damageType,
            CombatActionResolutionDiagnosticsLogger.InferActionName(this, damageType));

        if (target == null)
        {
            GameDebugger.Instance.LogError($"{Name} PerformAttack: Target is NULL! Attack aborted.");
            CombatActionResolutionDiagnosticsLogger.LogWarning("Character.PerformAttack aborted because target is null",
                $"Attacker={Name} [{IInteractableID}]\nRequestedDamageType={damageType}\nAPBefore={apBeforeAttack}\nAPAfter={ActionPoints}",
                this);
            return;
        }

        AttackResult precheckResult = CombatResolver.ValidateAttack(attackContext);
        if (!precheckResult.IsValid)
        {
            CombatActionResolutionDiagnosticsLogger.LogEvent("[ATTACK FLOW]", "Character.PerformAttack aborted due to invalid resolver precheck",
                $"ActionName={attackContext.SourceActionName}\n" +
                $"RequestedDamageType={damageType}\n" +
                $"InvalidReason={precheckResult.InvalidReason}\n" +
                $"APBefore={apBeforeAttack}\n" +
                $"APAfter={ActionPoints}",
                this, target);
            return;
        }

        GameDebugger.Instance.LogInfo($"{Name} is attacking {target.Name} with {damageType} damage. Target ID: {target.IInteractableID}");
        CombatActionResolutionDiagnosticsLogger.LogEvent("[ATTACK START]", "Character.PerformAttack begin",
            $"ActionName={CombatActionResolutionDiagnosticsLogger.InferActionName(this, damageType)}\n" +
            $"RequestedDamageType={damageType}\n" +
            $"Weapon={CombatActionResolutionDiagnosticsLogger.FormatItemSummary(GetMainHandItem())}\n" +
            $"APBefore={apBeforeAttack}\n" +
            $"TargetActive={target.IsActive}\n" +
            $"TargetAlive={target.IsAlive}",
            this, target);

        Target = target;
        target.Target = this;
        target.InCombat = true;
        RelationshipManager.SetActiveHostility(target, this, "AttackedByTarget");
        TurnOrchestrator.Instance?.TryUpdateTurnContext();

        GameDebugger.Instance.LogInfo($"{target.Name}'s target has been updated to {this.Name} (ID: {this.IInteractableID})");

        if (!target.IsPlayerVisible)
        {
            GameDebugger.Instance.LogInfo($"{target.Name} is an NPC, setting stance to Hostile.");
            target.IsHostile = true;
            target.Stance = NPCStance.Hostile;
            CurrentNestedArea?.UpdateHostileAreaStatus();
            TurnOrchestrator.Instance?.TryUpdateTurnContext();
        }

        SeeAllyAttacked(target, this);
        AttackResult attackResult = CombatResolver.ResolveAttack(attackContext);

        if (!attackResult.Hit && attackResult.IsValid)
        {
            MessageLogManager.Instance.Log("combat_miss", Name, target.Name);
            GameDebugger.Instance.LogInfo($"{Name} missed the attack on {target.Name}.");
            OnMiss(target);
        }

        CombatActionResolutionDiagnosticsLogger.LogEvent("[ATTACK FLOW]", "Character.PerformAttack compatibility wrapper completed",
            $"ActionName={attackContext.SourceActionName}\n" +
            $"RequestedDamageType={damageType}\n" +
            $"APBefore={apBeforeAttack}\n" +
            $"APAfter={ActionPoints}\n" +
            $"ResultValid={attackResult.IsValid}\n" +
            $"Hit={attackResult.Hit}\n" +
            $"Resolver={attackResult.ResolverName}\n" +
            $"ContextAfterHostilityRefresh={TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "UNKNOWN"}",
            this, target);
    }

    public virtual float CalculateAccuracyAgainst(Character target)
    {
        float baseAccuracy = 80f; // Example base accuracy
        float characterAccuracy = GetStatValue("Perception");
        float enemyEvasion = target != null ? target.GetStatValue("Dexterity") : 0f;

        if (target != null && !target.IsPlayerVisible)
        {
            enemyEvasion = 0f;
            GameDebugger.Instance.LogInfo($"{Name} Base Attack: Target {target.Name} is not visible. Evasion set to 0.");
        }

        float finalAccuracy = baseAccuracy + characterAccuracy - enemyEvasion;
        GameDebugger.Instance.LogInfo($"{Name} Base Attack: Calculated Accuracy -> Base: {baseAccuracy}, Character Accuracy: {characterAccuracy}, Enemy Evasion: {enemyEvasion}, Final Accuracy: {finalAccuracy}");
        return Mathf.Clamp(finalAccuracy, 0f, 100f);
    }

    private bool DetermineCriticalHit()
    {
        return DetermineCriticalHit(out _, out _);
    }

    public virtual bool DetermineCriticalHit(out float criticalChance, out float criticalRoll)
    {
        criticalChance = this.GetCriticalHitChance();
        criticalRoll = UnityEngine.Random.Range(0f, 100f);
        bool isCriticalHit = criticalRoll < criticalChance;
        GameDebugger.Instance.LogInfo($"{Name} Base Attack: Critical Hit determination -> Critical Chance: {criticalChance}, Roll: {criticalRoll}, Result: {isCriticalHit}");
        return isCriticalHit;
    }

    private float CalculateBaseDamage()
    {
        float baseDamage = this.GetStatValue("Strength"); // Default to Strength
        Debug.Log($"{Name} Base Attack: Calculated Base Damage: {baseDamage}");
        return baseDamage;
    }

    private DamageType GetBaseDamageType()
    {
        return this.GetDamageType(); // Simply return the DamageType enum
    }

    private float CalculateFinalDamage(float baseDamage, DamageType damageType, Character target, bool isCriticalHit)
    {
        float damageBeforeResistance = baseDamage;

        if (isCriticalHit)
        {
            float criticalMultiplier = this.GetCriticalHitMultiplier();
            baseDamage *= (criticalMultiplier / 100f);
            Debug.Log($"{Name} Base Attack: Critical Hit applied -> New Base Damage: {baseDamage}");
        }

        float resistance = target.GetResistance(damageType);
        float damageAfterResistance = baseDamage * (1f - resistance / 100f);

        float defence = target.GetDefence();
        float finalDamage = Mathf.Max(damageAfterResistance - defence, 0f);

        Debug.Log($"{Name} Base Attack: Final Damage Calculation -> Base Damage: {damageBeforeResistance}, Resistance: {resistance}, Defence: {defence}, Final Damage: {finalDamage}");

        return finalDamage;
    }

    private void OnCriticalHit(Character target, float damage)
    {
        Debug.Log($"Critical hit by {Name}! Dealt {damage} damage to {target.Name}. Target ID: {target.IInteractableID}");
    }

    private void OnHit(Character target, float damage)
    {
        Debug.Log($"Hit by {Name}! Dealt {damage} damage to {target.Name}. Target ID: {target.IInteractableID}");
    }

    private void OnMiss(Character target)
    {
        Debug.Log($"{Name} missed the attack on {target.Name}. Target ID: {target.IInteractableID}");
    }

    private DamageType GetDamageType()
    {
        Item mainHand = GetMainHandItem();

        if (mainHand != null && mainHand.ItemTypes.Contains(ItemType.Weapon))
        {
            return mainHand.DamageType;
        }
        else
        {
            return DamageType.Bludgeoning;
        }
    }

    public bool IsTargetInRange(Character target, int range = 1)
    {
        Vector2Int targetPosition = target.NestedMapPosition;
        int distance = Mathf.Abs(NestedMapPosition.x - targetPosition.x) + Mathf.Abs(NestedMapPosition.y - targetPosition.y);

        Debug.Log($"{Name} (Character ID: {IInteractableID}) current position: {NestedMapPosition}. Target {target.Name} (Character ID: {target.IInteractableID}) position: {targetPosition}. Distance: {distance}");

        return distance <= range;
    }

    public virtual bool IsCombatActorAvailable()
    {
        return IsAlive && IsActive;
    }

    public virtual bool IsValidCombatTarget(Character target)
    {
        if (target == null || target == this)
        {
            return false;
        }

        if (!target.IsAlive || !target.IsActive)
        {
            return false;
        }

        if (CurrentNestedArea != null && target.CurrentNestedArea != null && CurrentNestedArea != target.CurrentNestedArea)
        {
            return false;
        }

        return true;
    }

    public virtual void ClearCombatTarget(string reason)
    {
        Character previousTarget = Target;
        Target = null;

        CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT TARGET]", "Character.ClearCombatTarget",
            $"Reason={reason ?? "Unknown"}\n" +
            $"PreviousTarget={previousTarget?.Name ?? "NULL"} [{previousTarget?.IInteractableID.ToString() ?? "NULL"}]\n" +
            $"IsHostile={IsHostile}\n" +
            $"Stance={Stance}\n" +
            $"InCombat={InCombat}",
            this, previousTarget);
    }

    public virtual Character FindReplacementCombatTarget()
    {
        Character playerCharacter = PlayerStats.Instance?.CurrentPlayerCharacter;
        List<Character> candidates = CurrentNestedArea != null
            ? CurrentNestedArea.GetAllCharactersInArea()
            : TurnOrchestrator.Instance?.GetLivingActiveAreaCharacters() ?? new List<Character>();

        IEnumerable<Character> validCandidates = candidates
            .Where(candidate => candidate != null && IsValidCombatTarget(candidate))
            .Distinct();

        if (playerCharacter != null &&
            validCandidates.Contains(playerCharacter) &&
            (RelationshipManager.HasActiveHostility(IInteractableID, playerCharacter.IInteractableID) ||
             RelationshipManager.HasActiveHostility(playerCharacter.IInteractableID, IInteractableID) ||
             playerCharacter.Target == this))
        {
            return playerCharacter;
        }

        return validCandidates
            .Where(candidate =>
                RelationshipManager.HasActiveHostility(IInteractableID, candidate.IInteractableID) ||
                RelationshipManager.HasActiveHostility(candidate.IInteractableID, IInteractableID) ||
                candidate.Target == this)
            .OrderBy(candidate => Mathf.Abs(NestedMapPosition.x - candidate.NestedMapPosition.x) + Mathf.Abs(NestedMapPosition.y - candidate.NestedMapPosition.y))
            .FirstOrDefault();
    }

    public virtual bool TryRefreshCombatTarget(string reason, out Character replacementTarget)
    {
        if (IsValidCombatTarget(Target))
        {
            replacementTarget = Target;
            return true;
        }

        Character invalidTarget = Target;
        if (invalidTarget != null)
        {
            ClearCombatTarget(reason);
        }

        replacementTarget = FindReplacementCombatTarget();
        if (replacementTarget != null)
        {
            Target = replacementTarget;
            CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT TARGET]", "Character.TryRefreshCombatTarget reacquired target",
                $"Reason={reason ?? "Unknown"}\n" +
                $"NewTarget={replacementTarget.Name} [{replacementTarget.IInteractableID}]\n" +
                $"IsHostile={IsHostile}\n" +
                $"Stance={Stance}\n" +
                $"InCombat={InCombat}",
                this, replacementTarget);
            return true;
        }

        CombatActionResolutionDiagnosticsLogger.LogWarning("Character.TryRefreshCombatTarget found no replacement target",
            $"Reason={reason ?? "Unknown"}\n" +
            $"PreviousTarget={invalidTarget?.Name ?? "NULL"} [{invalidTarget?.IInteractableID.ToString() ?? "NULL"}]\n" +
            $"IsHostile={IsHostile}\n" +
            $"Stance={Stance}\n" +
            $"InCombat={InCombat}",
            this, invalidTarget);
        return false;
    }

    public float GetStatValue(string statName)
    {
        float baseValue = statName switch
        {
            "Strength" => Strength,
            "Dexterity" => Dexterity,
            "Constitution" => Constitution,
            "Intelligence" => Intelligence,
            "Wisdom" => Wisdom,
            "Perception" => Perception,
            "Charisma" => Charisma,
            "Luck" => Luck,
            _ => 0f
        };

        float totalFlatModifier = 0f;
        float totalMultiplier = 1.0f;

        foreach (var effect in AffectedBy)
        {
            if (effect.AffectedStat == statName)
            {
                if (effect.Type == ModifierType.Flat) totalFlatModifier += effect.EffectAmount;
                else if (effect.Type == ModifierType.Multiplier) totalMultiplier *= (1 + effect.EffectAmount / 100f);
            }
        }

        foreach (var item in EquippedItems.Values)
        {
            if (item != null && item.Modifiers.TryGetValue(statName, out BuffDebuff itemModifier))
            {
                if (itemModifier.Type == ModifierType.Flat) totalFlatModifier += itemModifier.EffectAmount;
                else if (itemModifier.Type == ModifierType.Multiplier) totalMultiplier *= (1 + itemModifier.EffectAmount / 100f);
            }
        }

        float finalValue = (baseValue + totalFlatModifier) * totalMultiplier;
        Debug.Log($"{Name} calculated {statName}: Base: {baseValue}, Flat Bonus: {totalFlatModifier}, Multiplier: {totalMultiplier}, Final: {finalValue}");
        return finalValue;
    }

    public Dictionary<DamageType, int> GetWeaponDamage()
    {
        var mainHandItem = GetMainHandItem();
        if (mainHandItem == null)
        {
            GameDebugger.Instance.LogWarning($"{Name} GetWeaponDamage: No main hand weapon equipped.");
            CombatActionResolutionDiagnosticsLogger.LogWarning("Character.GetWeaponDamage found no equipped main-hand weapon",
                $"ActionContext=WeaponDamageQuery\nCharacter={Name} [{IInteractableID}]",
                this);
            return new Dictionary<DamageType, int>();
        }

        Dictionary<DamageType, int> finalDamageByType = new Dictionary<DamageType, int>();

        int baseWeaponDamage = Mathf.RoundToInt(mainHandItem.DamageOutput);
        string primaryStat = mainHandItem.PrimaryStat ?? "Strength";
        int statBonus = GetWeaponStatBonus(mainHandItem);

        GameDebugger.Instance.LogInfo($"{Name} GetWeaponDamage: Base Weapon Damage: {baseWeaponDamage}, Primary Stat: {primaryStat}, Stat Bonus: {statBonus}");

        if (!finalDamageByType.ContainsKey(mainHandItem.DamageType))
            finalDamageByType[mainHandItem.DamageType] = baseWeaponDamage;

        finalDamageByType[mainHandItem.DamageType] += statBonus;

        foreach (var modifier in mainHandItem.Modifiers.Where(m => m.Key == "Damage"))
        {
            if (Enum.TryParse(modifier.Value.AffectedDamageType.ToString(), out DamageType elementalType))
            {
                if (!finalDamageByType.ContainsKey(elementalType))
                    finalDamageByType[elementalType] = 0;

                finalDamageByType[elementalType] += Mathf.RoundToInt(modifier.Value.EffectAmount);
            }
        }

        foreach (var damageType in finalDamageByType.Keys.ToList())
        {
            foreach (var effect in AffectedBy.Where(e => e.AffectedDamageType == damageType))
            {
                if (effect.Type == ModifierType.Flat)
                    finalDamageByType[damageType] += Mathf.RoundToInt(effect.EffectAmount);
                else if (effect.Type == ModifierType.Multiplier)
                    finalDamageByType[damageType] = Mathf.RoundToInt(finalDamageByType[damageType] * (1 + effect.EffectAmount / 100f));
            }
        }

        GameDebugger.Instance.LogInfo($"{Name} GetWeaponDamage: Final Damage Calculation -> {string.Join(", ", finalDamageByType.Select(kv => kv.Key + ": " + kv.Value))}");
        CombatActionResolutionDiagnosticsLogger.LogEvent("[WEAPON DAMAGE]", "Character.GetWeaponDamage",
            $"Weapon={CombatActionResolutionDiagnosticsLogger.FormatItemSummary(mainHandItem)}\n" +
            $"BaseWeaponDamage={baseWeaponDamage}\n" +
            $"PrimaryStat={primaryStat}\n" +
            $"WeaponStatBonusCalculated={statBonus}\n" +
            $"WeaponStatBonusApplied={true}\n" +
            $"FinalWeaponDamage={CombatActionResolutionDiagnosticsLogger.FormatDamageDictionary(finalDamageByType)}",
            this);

        return finalDamageByType;
    }

    public virtual int GetWeaponStatBonus(Item weapon)
    {
        if (weapon == null)
        {
            return 0;
        }

        string primaryStat = string.IsNullOrWhiteSpace(weapon.PrimaryStat) ? "Strength" : weapon.PrimaryStat;
        return Mathf.RoundToInt(GetStatValue(primaryStat) * WeaponPrimaryStatScalingMultiplier);
    }

    public virtual int GetUnarmedAttackDamage()
    {
        float strengthDamage = GetStatValue("Strength");
        float dexterityDamage = GetStatValue("Dexterity");
        int unarmedDamage = Mathf.RoundToInt(Mathf.Max(strengthDamage, dexterityDamage));
        GameDebugger.Instance.LogInfo($"{Name} Unarmed Attack: Using {unarmedDamage} as base damage from strongest stat (Strength: {Mathf.RoundToInt(strengthDamage)}, Dexterity: {Mathf.RoundToInt(dexterityDamage)}).");
        return unarmedDamage;
    }

    public virtual DamagePacket BuildDamagePacket(AttackContext context)
    {
        DamagePacket packet = new DamagePacket();
        Item attackWeapon = context?.Weapon ?? GetMainHandItem();
        DamageType requestedDamageType = context != null ? context.RequestedDamageType : DamageType.None;

        if (attackWeapon == null)
        {
            DamageType unarmedDamageType = requestedDamageType != DamageType.None ? requestedDamageType : DamageType.Bludgeoning;
            int unarmedDamage = GetUnarmedAttackDamage();

            packet.UsesWeapon = false;
            packet.IsUnarmedOrNatural = true;
            packet.ScalingStat = GetStatValue("Strength") >= GetStatValue("Dexterity") ? "Strength" : "Dexterity";
            packet.ScalingBonusCalculated = 0f;
            packet.ScalingBonusApplied = true;
            packet.OriginalDamageByType[unarmedDamageType] = unarmedDamage;
            packet.FinalDamageByType[unarmedDamageType] = unarmedDamage;
            return packet;
        }

        packet.UsesWeapon = true;
        packet.IsUnarmedOrNatural = false;
        packet.ScalingStat = attackWeapon.PrimaryStat ?? "Strength";
        packet.ScalingBonusCalculated = GetWeaponStatBonus(attackWeapon);
        packet.ScalingBonusApplied = true;
        packet.OriginalDamageByType = new Dictionary<DamageType, int>(GetWeaponDamage());
        packet.FinalDamageByType = new Dictionary<DamageType, int>(packet.OriginalDamageByType);

        DamageType sourceDamageType = attackWeapon.DamageType;
        if ((sourceDamageType == DamageType.None || (requestedDamageType != DamageType.None && sourceDamageType != requestedDamageType)) &&
            requestedDamageType != DamageType.None)
        {
            DamageType conversionSource = sourceDamageType;
            if (!packet.FinalDamageByType.ContainsKey(conversionSource))
            {
                if (packet.FinalDamageByType.ContainsKey(DamageType.None))
                {
                    conversionSource = DamageType.None;
                }
                else if (packet.FinalDamageByType.Count == 1)
                {
                    conversionSource = packet.FinalDamageByType.Keys.First();
                }
            }

            if (packet.FinalDamageByType.TryGetValue(conversionSource, out int convertibleDamage))
            {
                packet.FinalDamageByType.Remove(conversionSource);
                if (!packet.FinalDamageByType.ContainsKey(requestedDamageType))
                {
                    packet.FinalDamageByType[requestedDamageType] = 0;
                }

                packet.FinalDamageByType[requestedDamageType] += convertibleDamage;
                packet.DamageTypeConverted = true;
                packet.ConvertedFromType = conversionSource;
                packet.ConvertedToType = requestedDamageType;
            }
        }

        return packet;
    }

    public int GetCriticalHitChance()
    {
        float baseCriticalChance = Mathf.RoundToInt(GetStatValue("Luck"));
        float totalFlatBonus = 0f;
        float totalMultiplier = 1.0f;

        foreach (var effect in AffectedBy)
        {
            if (effect.AffectedStat == "CriticalChance")
            {
                if (effect.Type == ModifierType.Flat) totalFlatBonus += effect.EffectAmount;
                else if (effect.Type == ModifierType.Multiplier) totalMultiplier *= (1 + effect.EffectAmount / 100f);
            }
        }

        foreach (var item in EquippedItems.Values)
        {
            if (item != null && item.Modifiers.TryGetValue("CriticalChance", out BuffDebuff itemModifier))
            {
                if (itemModifier.Type == ModifierType.Flat) totalFlatBonus += itemModifier.EffectAmount;
                else if (itemModifier.Type == ModifierType.Multiplier) totalMultiplier *= (1 + itemModifier.EffectAmount / 100f);
            }
        }

        int finalCriticalChance = Mathf.RoundToInt((baseCriticalChance + totalFlatBonus) * totalMultiplier);
        Debug.Log($"{Name} calculated critical hit chance -> Base: {baseCriticalChance}, Flat Bonus: {totalFlatBonus}, Multiplier: {totalMultiplier}, Final: {finalCriticalChance}");
        return finalCriticalChance;
    }

    public int GetCriticalHitMultiplier()
    {
        float baseMultiplier = 150; // Default multiplier
        float totalFlatBonus = 0f;
        float totalMultiplier = 1.0f;

        foreach (var effect in AffectedBy)
        {
            if (effect.AffectedStat == "CriticalMultiplier")
            {
                if (effect.Type == ModifierType.Flat) totalFlatBonus += effect.EffectAmount;
                else if (effect.Type == ModifierType.Multiplier) totalMultiplier *= (1 + effect.EffectAmount / 100f);
            }
        }

        foreach (var item in EquippedItems.Values)
        {
            if (item != null && item.Modifiers.TryGetValue("CriticalMultiplier", out BuffDebuff itemModifier))
            {
                if (itemModifier.Type == ModifierType.Flat) totalFlatBonus += itemModifier.EffectAmount;
                else if (itemModifier.Type == ModifierType.Multiplier) totalMultiplier *= (1 + itemModifier.EffectAmount / 100f);
            }
        }

        int finalMultiplier = Mathf.RoundToInt((baseMultiplier + totalFlatBonus) * totalMultiplier);
        Debug.Log($"{Name} calculated critical hit multiplier -> Base: {baseMultiplier}, Flat Bonus: {totalFlatBonus}, Multiplier: {totalMultiplier}, Final: {finalMultiplier}");
        return finalMultiplier;
    }

    public int GetDefence()
    {
        float baseDefence = GetStatValue("Constitution");
        float armourValue = GetTotalArmourValue();
        int totalDefence = Mathf.RoundToInt(baseDefence + armourValue);

        GameDebugger.Instance.LogInfo($"{Name} GetDefence: Base Defence: {baseDefence}, Armour Value: {armourValue}, Total Defence: {totalDefence}");
        CombatActionResolutionDiagnosticsLogger.LogEvent("[DEFENCE]", "Character.GetDefence",
            $"BaseDefence={baseDefence}\nArmourValue={armourValue}\nTotalDefence={totalDefence}\nLiveDamagePathUsesDefence={false}",
            this);

        return totalDefence;
    }

    private int GetTotalArmourValue()
    {
        int totalArmour = 0;

        if (EquippedItems == null || EquippedItems.Count == 0)
        {
            GameDebugger.Instance.LogWarning($"{Name} has no equipped items.");
            return 0;
        }

        foreach (var item in EquippedItems.Values)
        {
            if (item == null)
            {
                GameDebugger.Instance.LogWarning($"{Name} has a null item in EquippedItems.");
                continue;
            }

            totalArmour += item.ArmourValue;
        }

        GameDebugger.Instance.LogInfo($"{Name} GetTotalArmourValue: Total Armour Value: {totalArmour}");
        CombatActionResolutionDiagnosticsLogger.LogEvent("[ARMOUR]", "Character.GetTotalArmourValue",
            $"TotalArmourValue={totalArmour}\nEquippedArmour={CombatActionResolutionDiagnosticsLogger.FormatEquipmentSummary(this)}\nLiveDamagePathUsesArmourValue={false}",
            this);
        return totalArmour;
    }

    public virtual List<Item> GetEquippedArmourForBodyPart(BodyPart bodyPart)
    {
        if (bodyPart == null || EquippedItems == null || EquippedItems.Count == 0)
        {
            return new List<Item>();
        }

        List<EquipmentSlot> slots = GetEquipmentSlotsForBodyPart(bodyPart);
        return slots
            .Where(slot => EquippedItems.ContainsKey(slot) && EquippedItems[slot] != null && EquippedItems[slot].ArmourValue > 0)
            .Select(slot => EquippedItems[slot])
            .Distinct()
            .ToList();
    }

    public virtual int GetArmourValueForBodyPart(BodyPart bodyPart)
    {
        return GetEquippedArmourForBodyPart(bodyPart).Sum(item => item.ArmourValue);
    }

    public virtual int GetArmourMitigationForHit(BodyPart bodyPart, DamageType damageType)
    {
        if (!IsPhysicalDamageType(damageType))
        {
            return 0;
        }

        return GetArmourValueForBodyPart(bodyPart);
    }

    public virtual bool IsPhysicalDamageType(DamageType damageType)
    {
        return damageType == DamageType.Piercing ||
               damageType == DamageType.Slashing ||
               damageType == DamageType.Bludgeoning ||
               damageType == DamageType.Crushing ||
               damageType == DamageType.Rending ||
               damageType == DamageType.Blunt ||
               damageType == DamageType.Unarmed;
    }

    public virtual int GetResistance(DamageType damageType)
    {
        float resistance = 0;

        if (Resistances == null)
        {
            GameDebugger.Instance.LogError($"{Name} GetResistance: Resistances dictionary is NULL.");
            return 0;
        }

        if (Resistances.TryGetValue(damageType, out float baseResistance))
        {
            resistance += baseResistance;
        }
        else
        {
            GameDebugger.Instance.LogWarning($"{Name} GetResistance: Resistance for {damageType} not found. Defaulting to 0.");
        }

        if (EquippedItems != null)
        {
            foreach (var item in EquippedItems.Values)
            {
                if (item != null && item.Resistances.TryGetValue(damageType.ToString(), out float value))
                {
                    resistance += value;
                }
            }
        }
        else
        {
            GameDebugger.Instance.LogWarning($"{Name} GetResistance: EquippedItems is NULL.");
        }

        foreach (var effect in AffectedBy)
        {
            if (effect.AffectedResistance == damageType)
            {
                resistance += effect.EffectAmount;
            }
        }

        int finalResistance = Mathf.RoundToInt(resistance);
        GameDebugger.Instance.LogInfo($"{Name} GetResistance: Resistance against {damageType} -> {finalResistance}");
        CombatActionResolutionDiagnosticsLogger.LogEvent("[RESISTANCE]", "Character.GetResistance",
            $"DamageType={damageType}\nResistanceSources={CombatActionResolutionDiagnosticsLogger.FormatResistanceSources(this, damageType)}\nFinalResistance={finalResistance}",
            this);

        return finalResistance;
    }

public void TakeDamage(Dictionary<DamageType, int> incomingDamage, Character attacker, bool isCriticalHit = false, AttackResult attackResult = null)
{
    if (incomingDamage == null || incomingDamage.Count == 0)
    {
        GameDebugger.Instance.LogWarning($"{Name} TakeDamage called with no incoming damage.");
        CombatActionResolutionDiagnosticsLogger.LogWarning("Character.TakeDamage called with no incoming damage",
            $"Attacker={attacker?.Name ?? "NULL"}\nIncomingDamage={CombatActionResolutionDiagnosticsLogger.FormatDamageDictionary(incomingDamage)}",
            attacker, this);
        return;
    }

    float totalDamage = 0;
    int defenderHealthBefore = Health;
    bool wasAliveBefore = IsAlive;
    bool wasActiveBefore = IsActive;
    if (attackResult != null)
    {
        attackResult.DefenderHealthBefore = defenderHealthBefore;
        attackResult.DefenderWasAliveBefore = wasAliveBefore;
        attackResult.DefenderWasActiveBefore = wasActiveBefore;
    }

    // Pick one actual body-part instance for this incoming hit.
    // This uses Anatomy's recursive lookup, so subparts can be hit too.
    BodyPart targetPart = Anatomy?.GetRandomBodyPart();

    if (targetPart == null)
    {
        GameDebugger.Instance.LogWarning($"{Name} has no valid body part to take damage.");
        CombatActionResolutionDiagnosticsLogger.LogWarning("Character.TakeDamage aborted because no valid body part was available",
            $"IncomingDamage={CombatActionResolutionDiagnosticsLogger.FormatDamageDictionary(incomingDamage)}\nDefenderHealthBefore={defenderHealthBefore}",
            attacker, this);
        return;
    }

    string attackerName = attacker != null ? attacker.Name : "Unknown attacker";
    int bodyPartHealthBefore = targetPart.Health;
    List<EquipmentSlot> coveredEquipmentSlots = GetEquipmentSlotsForBodyPart(targetPart);
    string bodyPartEquipmentSlots = coveredEquipmentSlots.Count > 0
        ? string.Join(", ", coveredEquipmentSlots)
        : "None";
    List<Item> armourCoveringPart = GetEquippedArmourForBodyPart(targetPart);
    string coveredArmour = armourCoveringPart.Count > 0
        ? string.Join("; ", armourCoveringPart.Select(item => item.ItemInGameName))
        : "None";
    int bodyPartArmourValuePresent = GetArmourValueForBodyPart(targetPart);
    bool onHitTakenEffectsPresent = EquippedItems.Values.Any(item => item?.OnHitTakenEffects != null && item.OnHitTakenEffects.Count > 0);
    int remainingArmourMitigation = bodyPartArmourValuePresent;
    if (attackResult != null)
    {
        attackResult.SelectedBodyPartName = targetPart.Name;
        attackResult.BodyPartEquipmentSlots = bodyPartEquipmentSlots;
        attackResult.CoveredArmour = coveredArmour;
        attackResult.ArmourValuePresent = bodyPartArmourValuePresent;
        attackResult.BodyPartCoverageUsed = bodyPartArmourValuePresent > 0;
        attackResult.BodyPartHealthBefore = bodyPartHealthBefore;
        attackResult.OnHitTakenEffectsPresent = onHitTakenEffectsPresent;
    }

    foreach (var damageEntry in incomingDamage)
    {
        DamageType damageType = damageEntry.Key;
        int rawDamage = damageEntry.Value;

        // Apply resistance
        float resistance = GetResistance(damageType);
        int damageAfterResistance = Mathf.RoundToInt(rawDamage * (1 - resistance / 100f));
        int availableArmourMitigation = Mathf.Min(GetArmourMitigationForHit(targetPart, damageType), remainingArmourMitigation);
        int armourReduction = Mathf.Clamp(availableArmourMitigation, 0, damageAfterResistance);
        remainingArmourMitigation -= armourReduction;
        int mitigatedDamage = Mathf.Max(damageAfterResistance - armourReduction, 0);
        if (attackResult != null)
        {
            DamageLine damageLine = attackResult.GetOrCreateDamageLine(damageType);
            damageLine.RawAmount = rawDamage;
            damageLine.ResistancePercent = Mathf.RoundToInt(resistance);
            damageLine.AmountAfterResistance = damageAfterResistance;
            damageLine.ArmourReduction = armourReduction;
            damageLine.FinalAmount = mitigatedDamage;
            attackResult.ArmourValueUsed += armourReduction;
        }

        if (mitigatedDamage > 0)
        {
            targetPart.TakeDamage(mitigatedDamage, this);
            totalDamage += mitigatedDamage;
            ModifyHealth(-mitigatedDamage);

            if (isCriticalHit)
            {
                MessageLogManager.Instance.Log("combat_critical", attackerName, Name, mitigatedDamage, targetPart.Name, damageType);
            }
            else
            {
                MessageLogManager.Instance.Log("combat_hit", attackerName, Name, mitigatedDamage, targetPart.Name, damageType);
            }
        }
        else
        {
            MessageLogManager.Instance.Log("combat_armor_block", attackerName, Name, rawDamage);
        }

        if (targetPart.IsLost)
        {
            MessageLogManager.Instance.Log("combat_status", Name, $"Lost {targetPart.Name}", "Permanent");
            HandleLosingLimb(targetPart);
        }

        if (targetPart.IsVital && targetPart.IsLost)
        {
            MessageLogManager.Instance.Log("combat_status", Name, "Fatally Injured", "Instant Death");
            Die();
            CombatActionResolutionDiagnosticsLogger.LogEvent("[DAMAGE APPLIED]", "Character.TakeDamage fatal body-part loss resolved",
                $"IncomingDamage={CombatActionResolutionDiagnosticsLogger.FormatDamageDictionary(incomingDamage)}\n" +
                $"SelectedBodyPart={targetPart.Name}\n" +
                $"BodyPartEquipmentSlots={bodyPartEquipmentSlots}\n" +
                $"CoveredArmour={coveredArmour}\n" +
                $"ArmourValuePresent={bodyPartArmourValuePresent}\n" +
                $"ArmourValueUsed={attackResult?.ArmourValueUsed ?? 0}\n" +
                $"BodyPartCoverageUsed={bodyPartArmourValuePresent > 0}\n" +
                $"ResistanceSources={CombatActionResolutionDiagnosticsLogger.FormatResistanceSources(this, damageType)}\n" +
                $"BodyPartHealthBefore={bodyPartHealthBefore}\n" +
                $"BodyPartHealthAfter={targetPart.Health}\n" +
                $"DefenderHealthBefore={defenderHealthBefore}\n" +
                $"DefenderHealthAfter={Health}\n" +
                $"IsAliveBefore={wasAliveBefore}\n" +
                $"IsAliveAfter={IsAlive}\n" +
                $"IsActiveBefore={wasActiveBefore}\n" +
                $"IsActiveAfter={IsActive}\n" +
                $"OnHitTakenEffectsPresent={onHitTakenEffectsPresent}\n" +
                $"OnHitTakenEffectsApplied={false}",
                attacker, this);
            if (attackResult != null)
            {
                attackResult.BodyPartHealthAfter = targetPart.Health;
                attackResult.DefenderHealthAfter = Health;
                attackResult.DefenderIsAliveAfter = IsAlive;
                attackResult.DefenderIsActiveAfter = IsActive;
                attackResult.DeathOccurred = true;
            }
            return;
        }
    }

    if (attackResult != null)
    {
        attackResult.BodyPartHealthAfter = targetPart.Health;
        attackResult.DefenderHealthAfter = Health;
        attackResult.DefenderIsAliveAfter = IsAlive;
        attackResult.DefenderIsActiveAfter = IsActive;
        attackResult.DeathOccurred = wasAliveBefore && !IsAlive;
    }

    CombatActionResolutionDiagnosticsLogger.LogEvent("[DAMAGE APPLIED]", "Character.TakeDamage resolved",
        $"IncomingDamage={CombatActionResolutionDiagnosticsLogger.FormatDamageDictionary(incomingDamage)}\n" +
        $"SelectedBodyPart={targetPart.Name}\n" +
        $"BodyPartEquipmentSlots={bodyPartEquipmentSlots}\n" +
        $"CoveredArmour={coveredArmour}\n" +
        $"EquippedArmour={CombatActionResolutionDiagnosticsLogger.FormatEquipmentSummary(this)}\n" +
        $"ArmourValuePresent={bodyPartArmourValuePresent}\n" +
        $"ArmourValueUsed={attackResult?.ArmourValueUsed ?? 0}\n" +
        $"BodyPartCoverageUsed={bodyPartArmourValuePresent > 0}\n" +
        $"BodyPartHealthBefore={bodyPartHealthBefore}\n" +
        $"BodyPartHealthAfter={targetPart.Health}\n" +
        $"DefenderHealthBefore={defenderHealthBefore}\n" +
        $"DefenderHealthAfter={Health}\n" +
        $"FinalMitigatedDamage={totalDamage}\n" +
        $"IsAliveBefore={wasAliveBefore}\n" +
        $"IsAliveAfter={IsAlive}\n" +
        $"IsActiveBefore={wasActiveBefore}\n" +
        $"IsActiveAfter={IsActive}\n" +
        $"OnHitTakenEffectsPresent={onHitTakenEffectsPresent}\n" +
        $"OnHitTakenEffectsApplied={false}",
        attacker, this);

    // Trigger UI Shake when the PlayerCharacter takes actual damage
    if (totalDamage > 0 && this == PlayerStats.Instance.CurrentPlayerCharacter)
    {
        float shakeStrength = Mathf.Clamp(totalDamage * 0.5f, 5f, 20f);
        float shakeDuration = 0.3f;
        UIController uiController = UIController.Instance;
        UIEffects uiEffects = UIEffects.Instance;
        GameObject panelToShake = uiController != null ? uiController.panelToShake : null;
        GameObject combatPanel = uiController != null ? uiController.uiCombatPanel : null;

        Debug.Log($"[TakeDamage] Screen shake triggered! Damage: {totalDamage}, Shake Strength: {shakeStrength}, Duration: {shakeDuration}");
        // CODEXLOG003_ACTIONS_AAM: temporary player damage feedback diagnostic.
        ActionAAMDiagnosticsLogger.LogEvent("[PLAYER DAMAGE FEEDBACK]", "Player damage screen shake requested",
            $"Target: {FormatCombatReactionCharacter(this)}\n" +
            $"Attacker: {FormatCombatReactionCharacter(attacker)}\n" +
            $"TotalDamage: {totalDamage}\n" +
            $"ShakeStrength: {shakeStrength}\n" +
            $"ShakeDuration: {shakeDuration}\n" +
            $"UIControllerExists: {uiController != null}\n" +
            $"UIEffectsExists: {uiEffects != null}\n" +
            $"PanelToShakeExists: {panelToShake != null}\n" +
            $"PanelToShakeActive: {panelToShake?.activeInHierarchy.ToString() ?? "NULL"}\n" +
            $"CombatPanelExists: {combatPanel != null}\n" +
            $"CombatPanelActive: {combatPanel?.activeInHierarchy.ToString() ?? "NULL"}");

        if (uiController != null && panelToShake != null && uiEffects != null)
        {
            uiController.ApplyPanelShakeOnDamage(shakeStrength, shakeDuration);
        }
        else if (uiEffects != null && combatPanel != null)
        {
            uiEffects.ShakeUI(combatPanel.GetComponent<RectTransform>(), shakeDuration, shakeStrength);
        }
    }

    if (!IsActive || CurrentNestedArea == null)
    {
        ActionAAMDiagnosticsLogger.LogEvent("[COMBAT REACTION]", "TakeDamage skipping hostility/ally alert for inactive or area-less target",
            $"Target: {FormatCombatReactionCharacter(this)}\n" +
            $"Attacker: {FormatCombatReactionCharacter(attacker)}\n" +
            $"TargetIsActive: {IsActive}\n" +
            $"TargetNestedArea: {FormatCombatReactionArea(CurrentNestedArea)}\n" +
            $"AttackerNestedArea: {FormatCombatReactionArea(attacker?.CurrentNestedArea)}");
        return;
    }

    // If the character was docile before, they should now become hostile
    if (Stance != NPCStance.Hostile)
    {
        GameDebugger.Instance.LogInfo($"{Name} was attacked by {attackerName}. Becoming hostile.");
        RelationshipManager.SetActiveHostility(this, attacker, "DamagedByTarget");
        Stance = NPCStance.Hostile;
        IsHostile = true;
        Target = attacker;
        stateMachine.ChangeState(new HostileState());
        TurnOrchestrator.Instance?.TryUpdateTurnContext();
        CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT CONTEXT]", "Character.TakeDamage refreshed hostility/combat context",
            $"StanceChangedToHostile=True\nTargetAfterDamage={Target?.Name ?? "NULL"}\nContextAfterRefresh={TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "UNKNOWN"}",
            attacker, this);
    }

    // If the attacked character has allies, notify them
    AlertNearbyAllies(this);
}

    public void ApplyScarToBodyPart(string bodyPartName)
    {
        BodyPart part = Anatomy.BodyParts.Values
            .SelectMany(parts => parts)
            .FirstOrDefault(p => p.Name == bodyPartName);

        if (part != null)
        {
            part.IncreaseScar();
        }
        else
        {
            GameDebugger.Instance.LogWarning($"{Name} attempted to apply a scar to {bodyPartName}, but it was not found.");
        }
    }

    public void HealScarOnBodyPart(string bodyPartName)
    {
        BodyPart part = Anatomy.BodyParts.Values
            .SelectMany(parts => parts)
            .FirstOrDefault(p => p.Name == bodyPartName);

        if (part != null)
        {
            part.ReduceScar();
        }
        else
        {
            GameDebugger.Instance.LogWarning($"{Name} attempted to heal a scar on {bodyPartName}, but it was not found.");
        }
    }

    // Ensure losing a body part removes equipped items
public void HandleLosingLimb(BodyPart lostPart)
{
    List<EquipmentSlot> affectedSlots = GetEquipmentSlotsForBodyPart(lostPart);

    foreach (var slot in affectedSlots)
    {
        if (EquippedItems.TryGetValue(slot, out Item lostItem))
        {
            UnEquipItem(slot);
            GameDebugger.Instance.LogInfo($"{Name} lost their {lostPart.Name}, removing {lostItem.ItemInGameName} from {slot}.");
        }
    }
}

    public void ApplyOnHitEffects(Character target, AttackResult attackResult = null)
    {
        foreach (var effect in OnHitEffects)
        {
            effect.ApplyEffect(this, target);
        }

        Item mainHandItem = GetMainHandItem();
        if (mainHandItem?.OnHitEffects != null)
        {
            foreach (var effect in mainHandItem.OnHitEffects)
            {
                effect.ApplyEffect(this, target);
            }
        }

        if (attackResult != null)
        {
            attackResult.OnHitEffectsApplied = OnHitEffects.Count > 0;
            attackResult.WeaponOnHitEffectsApplied = mainHandItem?.OnHitEffects.Count > 0;
            attackResult.OnHitTakenEffectsPresent = target?.EquippedItems?.Values.Any(item => item?.OnHitTakenEffects != null && item.OnHitTakenEffects.Count > 0) ?? false;
            attackResult.OnHitTakenEffectsApplied = false;
        }

        CombatActionResolutionDiagnosticsLogger.LogEvent("[ON HIT EFFECTS]", "Character.ApplyOnHitEffects",
            $"AttackerOnHitEffects={OnHitEffects.Count}\n" +
            $"MainHandItemOnHitEffects={mainHandItem?.OnHitEffects.Count.ToString() ?? "0"}\n" +
            $"MainHandItemOnHitEffectsAppliedViaCharacterList={mainHandItem?.OnHitEffects.Count > 0}\n" +
            $"DefenderOnHitTakenEffectsPresent={target?.EquippedItems?.Values.Any(item => item?.OnHitTakenEffects != null && item.OnHitTakenEffects.Count > 0).ToString() ?? "False"}\n" +
            $"DefenderOnHitTakenEffectsApplied={false}",
            this, target);
    }

    public void Die()
    {
        if (!IsAlive && !IsActive)
        {
            CombatActionResolutionDiagnosticsLogger.LogWarning("Character.Die ignored because character already appears dead",
                $"Name={Name}\nID={IInteractableID}\nIsAlive={IsAlive}\nIsActive={IsActive}",
                this);
            return;
        }

        INestedArea deathArea = CurrentNestedArea;
        bool wasAliveBefore = IsAlive;
        bool wasActiveBefore = IsActive;
        bool wasInTurnBefore = InTurn;
        bool wasInCombatBefore = InCombat;
        int healthBefore = Health;
        int actionPointsBefore = ActionPoints;
        IsAlive = false;
        IsActive = false;
        InTurn = false;
        InCombat = false;
        ActionPoints = 0;
        Debug.Log($"{Name} has died!");
        ClearThisCharacterAsTargetForOtherCombatants();
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[ENTITY DEATH]", "Character.Die", null, this);
        CombatActionResolutionDiagnosticsLogger.LogEvent("[DEATH]", "Character.Die state transition",
            $"HealthBefore={healthBefore}\n" +
            $"HealthAfter={Health}\n" +
            $"ActionPointsBefore={actionPointsBefore}\n" +
            $"ActionPointsAfter={ActionPoints}\n" +
            $"IsAliveBefore={wasAliveBefore}\n" +
            $"IsAliveAfter={IsAlive}\n" +
            $"IsActiveBefore={wasActiveBefore}\n" +
            $"IsActiveAfter={IsActive}\n" +
            $"InTurnBefore={wasInTurnBefore}\n" +
            $"InTurnAfter={InTurn}\n" +
            $"InCombatBefore={wasInCombatBefore}\n" +
            $"InCombatAfter={InCombat}",
            this);
        OnDeath();
        deathArea?.UpdateHostileAreaStatus();
        TurnOrchestrator.Instance?.TryUpdateTurnContext();
        CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT CONTEXT]", "Character.Die refreshed hostility/combat context",
            $"DeathArea={CombatActionResolutionDiagnosticsLogger.FormatArea(deathArea)}\nContextAfterRefresh={TurnOrchestrator.Instance?.CurrentContext.ToString() ?? "UNKNOWN"}",
            this);
    }

    protected virtual void OnDeath()
    {
        Debug.Log($"{Name} has no special death behavior.");
    }

private BodyPart GetRandomBodyPart()
{
    return Anatomy?.GetRandomBodyPart();
}

    private void DisplayCombatMessages(List<string> messages)
    {
        foreach (string message in messages)
        {
            Debug.Log(message);
            MessageLogManager.Instance.Log("combat_result", message);
        }
    }

    public void Heal(int healingAmount)
    {
        if (healingAmount <= 0)
        {
            GameDebugger.Instance.LogWarning($"{Name} attempted to heal with a non-positive value.");
            return;
        }

        // Apply healing to the character's overall health
        ModifyHealth(healingAmount);

        // Spread the same healing amount to body parts
        List<BodyPart> damagedParts = Anatomy.BodyParts.Values
            .SelectMany(parts => parts)
            .Where(part => !part.IsLost && part.Health < part.MaxHealth)
            .ToList();

        if (damagedParts.Count > 0)
        {
            foreach (var part in damagedParts)
            {
                int healAmount = Mathf.Min(healingAmount, part.MaxHealth - part.Health);
                part.Health += healAmount;
                GameDebugger.Instance.LogInfo($"{Name} healed {part.Name} for {healAmount} HP. New health: {part.Health}/{part.MaxHealth}");
            }
        }
        else
        {
            GameDebugger.Instance.LogInfo($"{Name} had no damaged body parts to heal.");
        }
    }

    public void HealBodyPart(BodyPart part, int healingAmount)
    {
        if (part == null)
        {
            GameDebugger.Instance.LogError("HealBodyPart was called with a null part.");
            return;
        }

        if (part.IsLost)
        {
            GameDebugger.Instance.LogInfo($"{Name} cannot heal {part.Name} because it is lost.");
            return;
        }

        if (healingAmount <= 0)
        {
            GameDebugger.Instance.LogWarning($"{Name} attempted to heal {part.Name} with a non-positive value.");
            return;
        }

        int healAmount = Mathf.Min(healingAmount, part.MaxHealth - part.Health);
        part.Health += healAmount;

        GameDebugger.Instance.LogInfo($"{Name} healed {part.Name} for {healAmount} HP. New health: {part.Health}/{part.MaxHealth}");
    }

    #endregion

    #region Turn Actions and Action Management

    public void ExecuteTurnActions()
    {
        if (!IsCombatActorAvailable())
        {
            CombatActionResolutionDiagnosticsLogger.LogWarning("Character.ExecuteTurnActions skipped unavailable actor",
                $"Actor={Name} [{IInteractableID}]\nIsAlive={IsAlive}\nIsActive={IsActive}\nInCombat={InCombat}",
                this);
            return;
        }

        Vector2Int positionBefore = NestedMapPosition;
        int apBefore = ActionPoints;
        int mpBefore = MovePoints;
        // CODEXLOG002_MOVEMENT_AI: temporary ExecuteTurnActions diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[EXECUTE TURN ACTIONS]", "Character.ExecuteTurnActions begin",
            $"Position before: {positionBefore}\n" +
            $"AP before: {apBefore}\n" +
            $"MP before: {mpBefore}\n" +
            $"Stance: {Stance}\n" +
            $"Status: {Status}\n" +
            $"State machine type: {MovementAIDiagnosticsLogger.FormatStateMachine(this)}\n" +
            $"Current state: {MovementAIDiagnosticsLogger.FormatCurrentState(this)}",
            this);
        GameDebugger.Instance.LogInfo($"[Character ID: {IInteractableID}] [{Name}] - Starting turn. Stance: {Stance}, State: {stateMachine.CurrentState?.GetType().Name ?? "NULL"}, Max AP: {MaxActionPoints}, Current AP: {ActionPoints}");
        ResetTurnDecision();

        // Reset Action Points
        ActionPoints = MaxActionPoints;
        GameDebugger.Instance.LogInfo($"[Character ID: {IInteractableID}] [{Name}] - AP reset to Max AP: {MaxActionPoints}");
        if (InCombat || IsHostile || Stance == NPCStance.Hostile || this is Monster || this is Animal)
        {
            CombatActionResolutionDiagnosticsLogger.LogEvent("[AP RESET]", "Character.ExecuteTurnActions reset action points",
                $"APBefore={apBefore}\nAPAfter={ActionPoints}\nResetSource=Character.ExecuteTurnActions\nCurrentState={stateMachine?.CurrentState?.GetType().Name ?? "NULL"}",
                this);
        }

        // Ensure State Machine is Initialized
        if (stateMachine == null)
        {
            stateMachine = new StateMachine(this);
            GameDebugger.Instance.LogInfo($"[Character ID: {IInteractableID}] [{Name}] - State machine was NULL. Initialized a new one.");
            // CODEXLOG002_MOVEMENT_AI: temporary ExecuteTurnActions diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[EXECUTE TURN ACTIONS]", "Character.ExecuteTurnActions initialized state machine",
                "Early exit reason: None\nState machine was NULL and has been initialized.",
                this);
        }

        // If NPC has no active state, default to IdleState
        if (stateMachine.CurrentState == null)
        {
            stateMachine.ChangeState(new IdleState());
            GameDebugger.Instance.LogInfo($"[Character ID: {IInteractableID}] [{Name}] - No current state. Defaulting to IdleState.");
            // CODEXLOG002_MOVEMENT_AI: temporary ExecuteTurnActions diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[EXECUTE TURN ACTIONS]", "Character.ExecuteTurnActions defaulted state",
                "Early exit reason: None\nPrevious current state was NULL\nNew current state: IdleState",
                this);
        }

        // Combat-time membership is a shared clock, not hostile intent.
        // Only actors already marked hostile should be forced into HostileState.
        if (InCombat && (IsHostile || Stance == NPCStance.Hostile))
        {
            if (Stance != NPCStance.Hostile)
            {
                Stance = NPCStance.Hostile;
                GameDebugger.Instance.LogInfo($"[Character ID: {IInteractableID}] [{Name}] - In combat. Switching to HostileState.");
                stateMachine.HandleStanceChange(NPCStance.Hostile);
            }
        }
        else if (InCombat)
        {
            // CODEXLOG001_TURNLIFECYCLE: temporary combat participant role diagnostic.
            TurnDiagnosticsLogger.LogEvent("[COMBAT ROLE]", "Non-hostile combat-time participant kept in current state",
                $"Role: {BaseTurnManager.GetCombatParticipantRole(this)}\n" +
                $"Stance: {Stance}\n" +
                $"IsHostile: {IsHostile}\n" +
                $"CurrentState: {stateMachine.CurrentState?.GetType().Name ?? "NULL"}",
                this);
        }

        // **Force Hostile NPCs into HostileState if they aren't there already**
        if (Stance == NPCStance.Hostile && !(stateMachine.CurrentState is HostileState))
        {
            GameDebugger.Instance.LogInfo($"[Character ID: {IInteractableID}] [{Name}] - Hostile but not in HostileState! Forcing transition.");
            stateMachine.ChangeState(new HostileState());
        }

        // **Update State Machine**
        GameDebugger.Instance.LogInfo($"[Character ID: {IInteractableID}] [{Name}] - Updating state machine: {stateMachine.CurrentState?.GetType().Name ?? "No Current State"}");
        // CODEXLOG002_MOVEMENT_AI: temporary AI decision diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "Character.ExecuteTurnActions updating state machine",
            $"State before update: {MovementAIDiagnosticsLogger.FormatCurrentState(this)}\nStance: {Stance}\nTarget: {Target?.Name ?? "NULL"}",
            this);
        stateMachine.Update();

        // Movement Logic
        if (!InCombat)
        {
            if (LastTurnDecisionResult == CharacterTurnDecisionResult.None)
            {
                // CODEXLOG002_MOVEMENT_AI: temporary AI movement decision diagnostic.
                MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "Character.ExecuteTurnActions calling MoveToCellsOfInterest",
                    $"CellsOfInterest count: {CellsOfInterest?.Count ?? -1}\nPosition before call: {NestedMapPosition}",
                    this);
                MoveToCellsOfInterest();
            }
            else
            {
                MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "Character.ExecuteTurnActions skipped CellsOfInterest movement",
                    $"Reason for no movement: state machine already resolved NPC turn\nTurnDecisionResult: {LastTurnDecisionResult}\nTurnDecisionReason: {LastTurnDecisionReason}",
                    this);
            }
        }
        else
        {
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "Character.ExecuteTurnActions skipped CellsOfInterest movement during combat",
                $"Reason for no movement: combat turn actor uses combat state only\nTarget: {Target?.Name ?? "NULL"}",
                this);
        }

        // Final Log for Turn Execution
        GameDebugger.Instance.LogInfo($"[Character ID: {IInteractableID}] [{Name}] - Turn actions executed. Remaining AP: {ActionPoints}");
        // CODEXLOG002_MOVEMENT_AI: temporary ExecuteTurnActions diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[EXECUTE TURN ACTIONS]", "Character.ExecuteTurnActions end",
            $"Position before: {positionBefore}\n" +
            $"Position after: {NestedMapPosition}\n" +
            $"Position changed: {NestedMapPosition != positionBefore}\n" +
            $"AP before: {apBefore}\n" +
            $"AP after: {ActionPoints}\n" +
            $"MP before: {mpBefore}\n" +
            $"MP after: {MovePoints}\n" +
            $"State after: {MovementAIDiagnosticsLogger.FormatCurrentState(this)}\n" +
            $"Turn decision result: {LastTurnDecisionResult}\n" +
            $"Turn decision reason: {LastTurnDecisionReason}",
            this);
    }

    public void OnTurnEnd()
    {
        GameDebugger.Instance.LogInfo($"[Character ID: {IInteractableID}] [{Name}] - Turn ended. Stance: {Stance}, Final State: {stateMachine.CurrentState?.GetType().Name ?? "NULL"}, Remaining AP: {ActionPoints}");
        int actionPointsBeforeEnd = ActionPoints;

        ApplyBuffsAndDebuffsAtTurnEnd();
        ActionPoints = 0;
        if (InCombat || this == PlayerStats.Instance?.CurrentPlayerCharacter)
        {
            CombatActionResolutionDiagnosticsLogger.LogEvent("[TURN END]", "Character.OnTurnEnd zeroed action points",
                $"APBefore={actionPointsBeforeEnd}\nAPAfter={ActionPoints}\nTurnEndSource=Character.OnTurnEnd",
                this);
        }
    }


    public void ApplyBuffsAndDebuffsAtTurnEnd()
    {
        List<BuffDebuff> expiredBuffs = new List<BuffDebuff>();

        foreach (var effect in AffectedBy)
        {
            if (effect.AffectedStat == "Health")
            {
                ModifyHealth(Mathf.RoundToInt(effect.EffectAmount), effect.Name);
            }

            if (effect.AffectedStat != null)
            {
                GameDebugger.Instance.LogInfo($"{Name} - Buff/Debuff {effect.Name} still active: {effect.EffectAmount} to {effect.AffectedStat}");
            }

            effect.ReduceDuration();
            if (effect.IsExpired())
            {
                expiredBuffs.Add(effect);
            }
        }

        foreach (var expired in expiredBuffs)
        {
            AffectedBy.Remove(expired);
            GameDebugger.Instance.LogInfo($"{Name} - Buff/Debuff {expired.Name} expired.");
        }
    }


    public int GetActionCost(string actionType)
    {
        return actionType switch
        {
            "Attack" => 3,
            "Move" => 2,
            _ => 1,
        };
    }

    public void SpendActionPoints(int points, string source = "Unknown")
    {
        int actionPointsBeforeSpend = ActionPoints;
        if (points > ActionPoints)
        {
            GameDebugger.Instance.LogWarning($"{Name} does not have enough Action Points. Required: {points}, Available: {ActionPoints}");
            CombatActionResolutionDiagnosticsLogger.LogWarning("Character.SpendActionPoints rejected due to insufficient AP",
                $"RequestedSpend={points}\nAPBefore={actionPointsBeforeSpend}\nAPAfter={ActionPoints}\nSpendSource={source}",
                this);
            return;
        }
        ActionPoints -= points;
        GameDebugger.Instance.LogInfo($"{Name} spent {points} Action Points. Remaining AP: {ActionPoints}");
        CombatActionResolutionDiagnosticsLogger.LogEvent("[AP SPEND]", "Character.SpendActionPoints",
            $"RequestedSpend={points}\nAPBefore={actionPointsBeforeSpend}\nAPAfter={ActionPoints}\nSpendSource={source}",
            this);
    }

    // Current legacy heuristic: collect candidate shelter-style cells based on simple environmental conditions.
    // This is intentionally narrower than a future affordance/interest discovery system.
    public void EvaluateCellsOfInterest(Cell[,] map)
    {
        CellsOfInterest.Clear();

        foreach (var cell in map)
        {
            if (IsValidCellOfInterest(cell) && !CharacterActionManager.Instance.IsActionTaken($"MovingTo_{cell.Coordinates}"))
            {
                CellsOfInterest.Add(cell.Coordinates);
            }
        }
    }


    // Current live meaning of "interest": mostly passable indoor shelter during rain.
    private bool IsValidCellOfInterest(Cell cell)
    {
        if (!cell.isPassable) return false;

        int parentCellId = CurrentCell?.ParentAreaID ?? -1;
        WeatherType currentWeather = WeatherManager.Instance.GetWeatherOfCell(parentCellId);

     //   if (cell.HasCampfire && TimeManager.Instance.IsNightTime)
     //       return true;

        if (cell.isIndoors && currentWeather == WeatherType.Rainy)
            return true;

        return false;
    }


    // Legacy exploration fallback movement using cell coordinates, not role-aware affordance selection.
    public bool MoveToCellsOfInterest()
    {
        // If an action is already logged, stick to it.
        if (CharacterActionManager.Instance.GetCharacterAction(this) != null)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI movement decision diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "Character.MoveToCellsOfInterest no action",
                $"Selected action: None\nReason for no movement: existing logged action {CharacterActionManager.Instance.GetCharacterAction(this)}",
                this);
            RecordTurnDecision(CharacterTurnDecisionResult.NoActionAvailable, "CellsOfInterest movement skipped because an existing action is already logged.");
            return false;
        }

        // Ensure CurrentNestedArea is valid
        if (CurrentNestedArea == null)
        {
            GameDebugger.Instance.LogWarning($"{Name} has no CurrentNestedArea. Cannot move.");
            // CODEXLOG002_MOVEMENT_AI: temporary AI movement decision diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("Character.MoveToCellsOfInterest no area",
                "Selected action: None\nReason for no movement: CurrentNestedArea null",
                this);
            RecordTurnDecision(CharacterTurnDecisionResult.NoActionAvailable, "CellsOfInterest movement skipped because CurrentNestedArea is null.");
            return false;
        }

        if (MovePoints <= 0)
        {
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "Character.MoveToCellsOfInterest no movement",
                "Selected action: None\nReason for no movement: no MovePoints available before CellsOfInterest movement",
                this);
            RecordTurnDecision(CharacterTurnDecisionResult.FailedMovement, "CellsOfInterest movement skipped because no MovePoints remain.");
            return false;
        }

        var availableCells = CellsOfInterest
            .Where(pos => CurrentNestedArea.IsPassable(pos)
                && !CharacterActionManager.Instance.IsActionTaken($"MovingTo_{IInteractableID}_{pos}")) 
            .ToList();

        if (!availableCells.Any())
        {
            GameDebugger.Instance.LogInfo($"{Name} has no valid CellsOfInterest. Trying one bounded random fallback.");
            // CODEXLOG002_MOVEMENT_AI: temporary AI movement decision diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "Character.MoveToCellsOfInterest fallback random",
                $"Selected action: MoveRandom\nReason: no valid CellsOfInterest\nCellsOfInterest count: {CellsOfInterest?.Count ?? -1}",
                this);
            bool movedRandomly = MoveInRandomDirection();
            RecordTurnDecision(
                movedRandomly ? CharacterTurnDecisionResult.Moved : CharacterTurnDecisionResult.NoActionAvailable,
                movedRandomly
                    ? "CellsOfInterest movement fell back to one successful random move."
                    : "No valid CellsOfInterest and no valid random fallback move were available.");
            return movedRandomly;
        }

        Vector2Int targetPos = availableCells[UnityEngine.Random.Range(0, availableCells.Count)];
        // CODEXLOG002_MOVEMENT_AI: temporary AI movement decision diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "Character.MoveToCellsOfInterest selected target",
            $"Selected action: MoveToCellOfInterest\nTarget cell: {targetPos}\nAvailable target count: {availableCells.Count}",
            this);

        // Log action before moving to prevent conflicts
        CharacterActionManager.Instance.LogAction(this, $"MovingTo_{IInteractableID}_{targetPos}");

        if (!MoveTowards(targetPos))
        {
            GameDebugger.Instance.LogInfo($"{Name} could not move directly to {targetPos}. Trying one bounded random fallback.");
            bool movedRandomly = MoveInRandomDirection();
            RecordTurnDecision(
                movedRandomly ? CharacterTurnDecisionResult.Moved : CharacterTurnDecisionResult.FailedMovement,
                movedRandomly
                    ? $"CellsOfInterest movement fell back to random movement after direct move to {targetPos} failed."
                    : $"CellsOfInterest movement failed; direct move to {targetPos} was blocked and random fallback also failed.");
            return movedRandomly;
        }

        RecordTurnDecision(CharacterTurnDecisionResult.Moved, $"Moved toward CellsOfInterest target {targetPos}.");
        return true;
    }

    // Ensure character removes their action once completed
    public void OnActionComplete()
    {
        CharacterActionManager.Instance.RemoveAction(this);
    }

    private void SeeAllyAttacked(Character attackedAlly, Character attacker)
    {
        if (attackedAlly == null || attacker == null)
        {
            GameDebugger.Instance.LogError("SeeAllyAttacked: Invalid parameters! Cannot process ally reaction.");
            return;
        }

        if (attackedAlly.CurrentNestedArea == null)
        {
            // CODEXLOG003_ACTIONS_AAM: temporary combat reaction diagnostic.
            ActionAAMDiagnosticsLogger.LogEvent("[COMBAT REACTION]", "SeeAllyAttacked skipped missing attacked ally area",
                $"AttackedAlly: {FormatCombatReactionCharacter(attackedAlly)}\n" +
                $"Attacker: {FormatCombatReactionCharacter(attacker)}\n" +
                $"AttackedAllyNestedArea: {FormatCombatReactionArea(attackedAlly.CurrentNestedArea)}\n" +
                $"AttackerNestedArea: {FormatCombatReactionArea(attacker.CurrentNestedArea)}");
            GameDebugger.Instance.LogWarning($"SeeAllyAttacked: {attackedAlly.Name} has no nested area. Reaction skipped.");
            return;
        }

        GameDebugger.Instance.LogInfo($"[SEE ATTACK] {attackedAlly.Name} was attacked by {attacker.Name}. Checking for nearby allies with vision.");

        List<Character> potentialWitnesses = TurnOrchestrator.Instance?.GetLivingActiveAreaCharacters(attackedAlly.CurrentNestedArea);

        foreach (var ally in potentialWitnesses)
        {
            if (ally == attackedAlly || ally == attacker) continue; // Ignore the attacked ally & attacker

            // Only allies who can SEE the attack react
            if (IsSameFaction(ally, attackedAlly) && attackedAlly.CanSeeEvent(attackedAlly, ally))
            {
                GameDebugger.Instance.LogInfo($"[SEE ATTACK] {ally.Name} sees {attackedAlly.Name} being attacked and reacts!");
                ally.ReactToAllyBeingAttacked(attackedAlly, attacker);
            }
        }
    }





    #endregion

    #region Interactions
    protected List<IInteraction> interactions;

    public virtual IEnumerable<IInteraction> GetAvailableInteractions(PlayerInventory inventory)
    {
        List<IInteraction> availableInteractions = interactions
            .Where(interaction => interaction.IsAvailable(this, inventory))
            .ToList();

        List<IInteraction> uniqueInteractions = availableInteractions
            .GroupBy(GetInteractionDeduplicationKey)
            .Select(group => group.First())
            .ToList();

        if (uniqueInteractions.Count != availableInteractions.Count)
        {
            string duplicateNames = string.Join(", ", availableInteractions
                .GroupBy(interaction => interaction.Name)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key} x{group.Count()}"));

            ActionAAMDiagnosticsLogger.LogEvent("[PROVIDER DEDUPE]", "Duplicate character interactions suppressed",
                $"Provider: {Name} [{IInteractableID}] ({GetType().Name})\n" +
                $"RawAvailableInteractions: {availableInteractions.Count}\n" +
                $"UniqueAvailableInteractions: {uniqueInteractions.Count}\n" +
                $"DuplicateActions: {duplicateNames}");
        }

        return uniqueInteractions;
    }

    private static string GetInteractionDeduplicationKey(IInteraction interaction)
    {
        if (interaction == null)
        {
            return "NULL";
        }

        return $"{interaction.GetType().FullName}|{interaction.Name}|{interaction.Type}|{interaction.ActionPointCost}";
    }

    protected virtual void InitializeInteractions()
    {
        interactions = new List<IInteraction>
    {
        new InspectInteraction(),
        new SlashInteraction(),
        new BashInteraction(),
        new StabInteraction(),
        new RendInteraction(),
        new PunchInteraction()
    };
    }

    public void ExecuteAction(IInteraction interaction, PlayerInventory inventory)
    {
        interaction.ExecuteInteraction(this, inventory);
    }
    #endregion

    #region Modifiers

    public void ModifyHealth(int amount, string source = "Unknown")
    {
        if (amount == 0) return; // No need to process zero changes

        int previousHealth = Health;
        Health = Mathf.Clamp(Health + amount, 0, MaxHealth); // Prevents over-healing or negative HP

        GameDebugger.Instance.LogInfo($"{Name} - Health modified by {amount} from {source}. Previous: {previousHealth}, New: {Health}");
        if (InCombat || source != "Unknown" || amount < 0)
        {
            CombatActionResolutionDiagnosticsLogger.LogEvent("[HEALTH]", "Character.ModifyHealth",
                $"Amount={amount}\nSource={source}\nHealthBefore={previousHealth}\nHealthAfter={Health}",
                this);
        }

        if (Health <= 0)
        {
            Die();
        }
    }

    private void ClearThisCharacterAsTargetForOtherCombatants()
    {
        List<Character> potentialCombatants = new List<Character>();
        if (CurrentNestedArea != null)
        {
            potentialCombatants.AddRange(CurrentNestedArea.GetAllCharactersInArea());
        }

        if (TurnOrchestrator.Instance != null)
        {
            potentialCombatants.AddRange(TurnOrchestrator.Instance.GetLivingActiveAreaCharacters(CurrentNestedArea));
        }

        foreach (Character combatant in potentialCombatants.Where(character => character != null && character != this).Distinct())
        {
            if (combatant.Target != this)
            {
                continue;
            }

            combatant.ClearCombatTarget($"Target {Name} [{IInteractableID}] died or was removed.");

            if (!combatant.TryRefreshCombatTarget($"Target {Name} [{IInteractableID}] died.", out Character replacementTarget))
            {
                combatant.IsHostile = false;
                combatant.InCombat = false;
                if (combatant.Stance == NPCStance.Hostile)
                {
                    combatant.Stance = NPCStance.Default;
                    combatant.stateMachine?.HandleStanceChange(combatant.Stance);
                }

                CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT TARGET]", "Character.Die cleared stale combat target with no replacement",
                    $"Actor={combatant.Name} [{combatant.IInteractableID}]\n" +
                    $"ReplacementTarget={replacementTarget?.Name ?? "NULL"}\n" +
                    $"IsHostileAfter={combatant.IsHostile}\n" +
                    $"StanceAfter={combatant.Stance}\n" +
                    $"InCombatAfter={combatant.InCombat}",
                    combatant, this);
            }
        }
    }

    #endregion

    #region Inventory Management

    public void AddItem(Item item)
    {
        Inventory.AddItem(item);
    }

    public void RemoveItem(Item item)
    {
        Inventory.RemoveItem(item.ItemInGameName, 1);
    }


    public List<InventoryContainer> GetInventoryContainers()
    {
        return Inventory.GetInventoryContainers();
    }

    public void AddMoney(int amount)
    {
        Money += amount;
    }

    public void RemoveMoney(int amount)
    {
        Money -= amount;
    }

    public Item GetMainHandItem()
    {
        return EquippedItems.TryGetValue(EquipmentSlot.MainHand, out Item mainHandItem) ? mainHandItem : null;
    }

    public void EquipItem(Item item, EquipmentSlot slot)
    {
        if (item == null)
        {
            Debug.LogWarning($"{Name} attempted to equip a null item.");
            return;
        }

        if (!CanEquipItem(slot))
        {
            Debug.LogWarning($"{Name} cannot equip {item.ItemInGameName} in {slot} due to missing or non-functional anatomy.");
            return;
        }

        // Ensure anatomy still allows this slot
        if (Anatomy == null || !Anatomy.CanEquipSlot(slot))
        {
            Debug.LogWarning($"{Name} cannot equip {item.ItemInGameName} in {slot} due to lost or missing body parts.");
            return;
        }

        // If an item is already equipped in this slot, unequip it first
        if (EquippedItems.ContainsKey(slot))
        {
            UnEquipItem(slot);
        }

        EquippedItems[slot] = item;

        if (Inventory.HasItem(item.ItemInGameName))
        {
            RemoveItem(item);
        }

        Debug.Log($"{Name} equipped {item.ItemInGameName} in {slot}.");
    }

    public bool CanEquipItem(EquipmentSlot slot)
    {
        if (Anatomy == null) return false;

        // Check if the slot is currently available due to anatomy changes
        return Anatomy.CanEquipSlot(slot);
    }

    public void UnEquipItem(EquipmentSlot slot)
    {
        if (EquippedItems.ContainsKey(slot))
        {
            Item item = EquippedItems[slot];

            // Ensure space in inventory before unequipping
            if (Inventory.CanAddItem(item))
            {
                AddItem(item);
            }
            else
            {
                Debug.LogWarning($"{Name} attempted to unequip {item.ItemInGameName}, but has no inventory space.");
                return;
            }

            EquippedItems.Remove(slot);
            Debug.Log($"{Name} unequipped {item.ItemInGameName} from {slot}.");
        }
    }

    public void DropItem(Item item, EquipmentSlot? slot = null)
    {
        if (item == null) return;

        if (slot.HasValue && EquippedItems.ContainsKey(slot.Value))
        {
            EquippedItems.Remove(slot.Value);
        }

        // Only remove from inventory if it exists there
        if (Inventory.HasItem(item.ItemInGameName))
        {
            RemoveItem(item);
        }

        Cell currentCell = CurrentNestedArea?.GetCellAtPosition(NestedMapPosition);
        if (currentCell != null)
        {
            currentCell.Items.Add(item);
            Debug.Log($"{Name} dropped {item.ItemInGameName} at {currentCell.Coordinates}.");
        }
        else
        {
            Debug.LogError($"Failed to drop {item.ItemInGameName}. No valid cell found at {NestedMapPosition}.");
        }
    }

    // Retrieve equipment slots associated with a body part
private List<EquipmentSlot> GetEquipmentSlotsForBodyPart(BodyPart bodyPart)
{
    List<EquipmentSlot> foundSlots = new List<EquipmentSlot>();

    void CollectSlots(BodyPart part)
    {
        if (part == null)
        {
            return;
        }

        if (part.EquipmentSlots != null && part.EquipmentSlots.Count > 0)
        {
            foundSlots.AddRange(part.EquipmentSlots);
        }

        foreach (var subPart in part.SubParts)
        {
            CollectSlots(subPart);
        }
    }

    CollectSlots(bodyPart);

    return foundSlots.Distinct().ToList();
}

    #endregion

    #region Line of Sight
    public List<Cell> visibleCells = new List<Cell>();

    public bool CanSeePlayer()
    {
        return IsPlayerVisible;
    }

    public bool CanSeeTarget(Character target)
    {
        if (target == null) return false;

        return visibleCells.Contains(target.CurrentCell); // Uses Character's vision system
    }

    public void ClearLineOfSight()
    {
        foreach (var cell in visibleCells)
        {
            cell.canBeSeenByNPC = false;
        }
        visibleCells.Clear();
    }

    public void UpdateLineOfSight()
    {
        ClearLineOfSight();
        this.IsPlayerVisible = false;

        int viewDistance = 5;
        Vector2Int facingDirection = DirectionToVector(DirectionFacing);

        Vector2Int leftSideDirection = Vector2Int.zero;
        Vector2Int rightSideDirection = Vector2Int.zero;

        if (facingDirection == DirectionToVector(Direction.North) || facingDirection == DirectionToVector(Direction.South))
        {
            leftSideDirection = DirectionToVector(Direction.West);
            rightSideDirection = DirectionToVector(Direction.East);
        }
        else if (facingDirection == DirectionToVector(Direction.East) || facingDirection == DirectionToVector(Direction.West))
        {
            leftSideDirection = DirectionToVector(Direction.North);
            rightSideDirection = DirectionToVector(Direction.South);
        }

        bool skipNextCell = false;

        for (int i = 0; i <= viewDistance; i++)
        {
            Vector2Int forwardPosition = this.NestedMapPosition + (facingDirection * i);

            if (i == 0)
            {
                CheckAndAddVisibleCell(this.NestedMapPosition + leftSideDirection);
                CheckAndAddVisibleCell(this.NestedMapPosition + rightSideDirection);
            }
            else if (i == 1 || !skipNextCell)
            {
                skipNextCell = !CheckAndAddVisibleCell(forwardPosition);
            }
            else
            {
                skipNextCell = false;
            }

            if (i > 1 && i <= 2)
            {
                CheckAndAddVisibleCell(this.NestedMapPosition + (facingDirection * (i - 1)) + leftSideDirection);
                CheckAndAddVisibleCell(this.NestedMapPosition + (facingDirection * (i - 1)) + rightSideDirection);
            }
        }
    }

    private bool CheckAndAddVisibleCell(Vector2Int position)
    {
        if (position == null)
        {
            GameDebugger.Instance.LogError("CheckAndAddVisibleCell: position is NULL.");
            return false;
        }

        if (CurrentNestedArea == null)
        {
            GameDebugger.Instance.LogError("CheckAndAddVisibleCell: CurrentNestedArea is NULL. Cannot retrieve cell.");
            return false;
        }

        Cell cell = CurrentNestedArea.GetCellAtPosition(position);

        if (cell == null)
        {
            GameDebugger.Instance.LogWarning($"CheckAndAddVisibleCell: No cell found at position {position}. Returning true.");
            return true;
        }

        // Log successful cell retrieval
        GameDebugger.Instance.LogInfo($"CheckAndAddVisibleCell: Found cell at position {position}. Processing visibility.");

        cell.canBeSeenByNPC = true;
        visibleCells.Add(cell);

        foreach (var obj in cell.Objects)
        {
            if (obj.CoverType == CoverType.Full)
            {
                return false;
            }
            else if (obj.CoverType == CoverType.Partial)
            {
                return false;
            }
        }

        if (cell.CellID == PlayerStats.Instance.CurrentCellID)
        {
            IsPlayerVisible = true;
        }

        return true;
    }

    private bool CanSeeEvent(Character observer, Character target)
    {
        if (target.CurrentNestedArea == null) return false;

        Cell targetCell = target.CurrentNestedArea.GetCellAtPosition(target.NestedMapPosition);
        return observer.visibleCells.Contains(targetCell);
    }

    #endregion

    #region Nested Area Management
    public void PlaceInNestedArea(INestedArea nestedArea, Vector2Int position)
    {
        CurrentNestedArea = nestedArea;
        NestedMapPosition = position;
        IsInNestedArea = true;

        Cell cell = nestedArea.GetCellAtPosition(position);
        if (cell != null)
        {
            cell.isNPCPresent = true;
            cell.Objects.Add(this);
        }

		TurnOrchestrator.Instance?.RegisterCharacter(this);
		GameDebugger.Instance.LogInfo($"[Character] {Name} placed in NestedArea {nestedArea.NestedAreaID} at {position}");
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[AREA ENTRY]", "Character.PlaceInNestedArea completed", $"NestedArea: {nestedArea?.Name} ({nestedArea?.NestedAreaID})", this);

    }

    public void RemoveFromNestedArea()
    {
        if (IsInNestedArea && CurrentNestedArea != null)
        {
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogEvent("[ENTITY REMOVAL]", "Character.RemoveFromNestedArea begin", null, this);
            Cell cell = CurrentNestedArea.GetCellAtPosition(NestedMapPosition);
            if (cell != null)
            {
                cell.isNPCPresent = false;
                cell.Objects.Remove(this);
                cell.isPassable = true;
            }

            IsInNestedArea = false;
            CurrentNestedArea = null;

            TurnOrchestrator.Instance?.DeregisterCharacter(this);
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogTurnSummary("Character.RemoveFromNestedArea completed", $"Entity: {Name} [{IInteractableID}]");

        }
    }
    #endregion

    #region Attitudes
    public void FirstMetPlayerCharacter()
    {
        if (HasMetPlayer)
        {
            Debug.LogWarning($"{Name} has already met the player.");
            return;
        }

        // Set HasMetPlayer to true
        HasMetPlayer = true;

        // Create a seed using GameManager.Instance.GameSeed and Character's Name
        int combinedSeed = GenerateSeedFromName(GameManager.Instance.GameSeed, Name);

        // Create a new random instance with the combined seed
        System.Random seededRandom = new System.Random(combinedSeed);

        // Roll attitude (range -100 to 100)
        AttitudeToPlayer = seededRandom.Next(-100, 101);

        Debug.Log($"{Name} met the player for the first time. Attitude towards player: {AttitudeToPlayer}");
    }

    private int GenerateSeedFromName(int gameSeed, string characterName)
    {
        // Combine the game seed and the hash of the character's name
        int nameHash = characterName.GetHashCode();
        return gameSeed + nameHash;
    }
    #endregion

    #region Relationships

    public Character Spouse { get; set; }
    public List<Character> Ancestors { get; set; } = new List<Character>();

    // Stores relationships using IDs to improve lookup efficiency
    public Dictionary<int, float> Relationships { get; set; } = new Dictionary<int, float>();

    public void AddRelationship(Character otherCharacter, float initialValue = 0)
    {
        if (!Relationships.ContainsKey(otherCharacter.IInteractableID))
        {
            Relationships[otherCharacter.IInteractableID] = initialValue;
        }
    }

    public void AdjustRelationship(Character otherCharacter, float amount)
    {
        if (Relationships.ContainsKey(otherCharacter.IInteractableID))
        {
            Relationships[otherCharacter.IInteractableID] = Mathf.Clamp(Relationships[otherCharacter.IInteractableID] + amount, -100, 100);
        }
    }

    public float GetRelationshipValue(Character otherCharacter)
    {
        return Relationships.TryGetValue(otherCharacter.IInteractableID, out float value) ? value : 0;
    }

    #endregion

    #region Allies

    public List<Character> Allies { get; private set; } = new List<Character>();

    // Adds an ally to the character's ally list.
    public void AddAlly(Character newAlly)
    {
        if (newAlly == null)
        {
            GameDebugger.Instance.LogError($"{Name} tried to add a null ally.");
            return;
        }

        if (!Allies.Contains(newAlly))
        {
            Allies.Add(newAlly);
            GameDebugger.Instance.LogInfo($"{Name} has added {newAlly.Name} as an ally.");
        }
    }

    // Removes an ally from the character's ally list.
    public void RemoveAlly(Character formerAlly)
    {
        if (formerAlly == null)
        {
            GameDebugger.Instance.LogError($"{Name} tried to remove a null ally.");
            return;
        }

        if (Allies.Contains(formerAlly))
        {
            Allies.Remove(formerAlly);
            GameDebugger.Instance.LogInfo($"{Name} has removed {formerAlly.Name} as an ally.");
        }
    }

    // Checks if a character is an ally.
    public bool IsAlly(Character potentialAlly)
    {
        return Allies.Contains(potentialAlly);
    }

    private void AlertNearbyAllies(Character target)
    {
        if (target == null)
        {
            GameDebugger.Instance.LogError("AlertNearbyAllies: Target is NULL. Cannot alert allies.");
            return;
        }

        if (target.CurrentNestedArea == null)
        {
            // CODEXLOG003_ACTIONS_AAM: temporary combat reaction diagnostic.
            ActionAAMDiagnosticsLogger.LogEvent("[COMBAT REACTION]", "AlertNearbyAllies skipped missing target area",
                $"Target: {FormatCombatReactionCharacter(target)}\n" +
                $"Caller: {FormatCombatReactionCharacter(this)}\n" +
                $"TargetNestedArea: {FormatCombatReactionArea(target.CurrentNestedArea)}\n" +
                $"CallerNestedArea: {FormatCombatReactionArea(CurrentNestedArea)}");
            GameDebugger.Instance.LogWarning($"AlertNearbyAllies: Target {target.Name} has no nested area. Ally alert skipped.");
            return;
        }

        GameDebugger.Instance.LogInfo($"[ALERT] {target.Name} is shouting for help in {target.CurrentNestedArea.NestedAreaID}! Looking for allies in {target.Faction}.");

        List<Character> potentialAllies = TurnOrchestrator.Instance?.GetLivingActiveAreaCharacters(target.CurrentNestedArea);


        foreach (var ally in potentialAllies)
        {
            if (ally == target) continue; // Don't alert yourself

            // *Only alert allies in the same nested area & faction**
            if (ally.CurrentNestedArea == target.CurrentNestedArea && IsSameFaction(ally, target))
            {
                GameDebugger.Instance.LogInfo($"[ALERT] {target.Name} has alerted {ally.Name} (same faction).");

                // Make the ally react to the alert (e.g., become hostile, assist, etc.)
                ally.ReactToAllyBeingAttacked(target, this);
            }
        }
    }

    public void ReactToAllyBeingAttacked(Character attackedAlly, Character attacker)
    {
        Debug.Log($"{Name} has witnessed an attack on {attackedAlly.Name} by {attacker.Name}!");
        RelationshipManager.SetActiveHostility(this, attacker, "WitnessedAllyAttacked");
        IsHostile = true;

        if (Stance != NPCStance.Hostile)
        {
            Stance = NPCStance.Hostile;
            Target = attacker;
            InCombat = true;
            Debug.Log($"{Name} is now hostile towards {attacker.Name}!");
        }

        stateMachine?.HandleStanceChange(Stance);
    }

    // CODEXLOG003_ACTIONS_AAM: temporary combat reaction diagnostic helper.
    private static string FormatCombatReactionCharacter(Character character)
    {
        if (character == null) return "NULL";
        return $"{character.Name} [{character.IInteractableID}] ({character.GetType().Name})";
    }

    // CODEXLOG003_ACTIONS_AAM: temporary combat reaction diagnostic helper.
    private static string FormatCombatReactionArea(INestedArea area)
    {
        if (area == null) return "NULL";
        return $"{area.Name} (ID={area.NestedAreaID}, Level={area.NestedAreaLevel})";
    }

    private bool IsSameFaction(Character ally, Character target)
    {
        return ally.Faction == target.Faction;
    }

    #endregion


}
