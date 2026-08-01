using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Monster : Character
{
    #region Monster Properties
    public int MonsterID { get; set; }
    public string MonsterName { get; set; }
    public int MonsterLevel { get; set; }
    public MonsterType Type { get; set; }
    public List<MonsterAbility> Abilities { get; set; } = new List<MonsterAbility>();
    public bool IsBoss { get; set; }
    public RarityType Rarity { get; set; }
    public override char Symbol { get; set; } = 'M';
    public override string Color { get; set; } = "#FF0000"; // Default monster color
    #endregion

    #region Constructor
    public Monster(MonsterCreationData data)
    {
        Name = data.MonsterName;
        if (GameManager.Instance != null)
        {
            MonsterID = GameManager.Instance.GetMonsterID(); // Secondary monster-definition/runtime ID
            IInteractableID = GameManager.Instance.GetInteractableID(); // Authoritative runtime identity
        }
        else
        {
            GameDebugger.Instance.LogError("Monster constructor could not resolve GameManager. IDs defaulted to 0.");
            MonsterID = 0;
            IInteractableID = 0;
        }
        Type = data.Type;
        IsBoss = data.IsBoss;
        Rarity = data.Rarity;
        Abilities = data.Abilities ?? new List<MonsterAbility>();

        MaxHealth = data.MaxHealth;
        Health = MaxHealth;
        Strength = data.Strength;
        Dexterity = data.Dexterity;
        Constitution = data.Constitution;
        Intelligence = data.Intelligence;
        Wisdom = data.Wisdom;
        Luck = data.Luck;
        Awareness = data.Awareness;
        Speed = data.Speed;
        IsAlive = true;
        IsActive = true;
        IsHostile = true;
        CanLeaveArea = false;

        foreach (var resistance in data.DamageResistances)
        {
            if (Resistances.ContainsKey(resistance.Key))
            {
                Resistances[resistance.Key] = resistance.Value;
            }
        }

        Anatomy = AnatomyGenerator.Instance.GenerateAnatomy(data.BodyType);
        InitializeStamina("Monster.Constructor");

        // Register the monster in the turn manager
        stateMachine = new StateMachine(this);
        stateMachine.ChangeState(new MonsterIdleState()); // Start in monster idle state
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[REGISTRATION]", "Monster constructor before TurnOrchestrator.RegisterCharacter", "This observes current constructor-time registration behavior only.", this);
        TurnOrchestrator.Instance.RegisterCharacter(this);
    }
    #endregion

    #region Combat
    public override int GetResistance(DamageType damageType)
    {
        if (Resistances.TryGetValue(damageType, out float resistance))
        {
            return Mathf.RoundToInt(resistance);
        }
        return base.GetResistance(damageType);
    }

    protected override void OnDeath()
    {
        GameDebugger.Instance.LogInfo($"Monster {Name} has been slain!");
        MessageLogManager.Instance.Log("combat_result", Name, "has been slain!");

        // Generate remains with stored loot
        MonsterRemains remains = MonsterRemains.GenerateRemains(this);

        // Remove from Turn Manager
        // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
        TurnDiagnosticsLogger.LogEvent("[ENTITY DEATH]", "Monster.OnDeath before TurnOrchestrator.DeregisterCharacter", null, this);
        TurnOrchestrator.Instance.DeregisterCharacter(this);
        IsActive = false; // Mark as inactive

        // Ensure remains are properly placed and visible
        if (remains != null)
        {
            GameDebugger.Instance.LogInfo($"Remains of {Name} (Level {MonsterLevel}) have been placed at {remains.Position}");
        }
    }

    public void PerformAbility(Character target)
    {
        TurnOrchestrator orchestrator = TurnOrchestrator.Instance;
        orchestrator?.BeginActionResolution($"{Name}.PerformAbility");
        try
        {
        if (Abilities == null || Abilities.Count == 0)
        {
            GameDebugger.Instance.LogWarning($"{Name} has no abilities, cannot perform ability attack!");
            CombatActionResolutionDiagnosticsLogger.LogWarning("Monster.PerformAbility rejected because monster has no abilities",
                $"Target={target?.Name ?? "NULL"}",
                this, target);
            return;
        }

        MonsterAbility chosenAbility = Abilities[Random.Range(0, Abilities.Count)];

        if (!IsTargetInRange(target, chosenAbility.Range))
        {
            GameDebugger.Instance.LogWarning($"{Name} tried to use {chosenAbility.Name}, but {target.Name} is out of range.");
            CombatActionResolutionDiagnosticsLogger.LogWarning("Monster.PerformAbility rejected because target is out of range",
                $"Ability={chosenAbility.Name}\nTarget={target?.Name ?? "NULL"}\nRange={chosenAbility.Range}",
                this, target);
            return;
        }

        ActionCostProfile abilityCostProfile = new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
            ExplorationBehaviour = ExplorationActionBehaviour.Unavailable,
            CombatBehaviour = CombatActionBehaviour.Flexible,
            IsFree = false,
            WorldTimeCost = 0,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
            StaminaCost = 0,
            CombatExertionCost = FixedPointResourceMath.FromPoints(3f),
            ConsumptionCapacityCost = 0,
            CanOverexert = true,
            EndsPlayerTurn = false,
            CandidateForFutureStamina = false,
            PredictedStaminaCost = ActionCostProfileResolver.UnknownPredictedStaminaCost,
            IsContextual = false,
            CostLabel = string.Empty,
            Notes = "Typed monster ability action. Uses combat exertion only."
        };

        ActionCostCommitResult commitResult = ActionCostProfileResolver.CreateCommitment(abilityCostProfile, null, $"Monster.PerformAbility:{chosenAbility.Name}").TryCommit(this, $"Monster.PerformAbility:{chosenAbility.Name}");
        if (!commitResult.IsCommitted)
        {
            GameDebugger.Instance.LogWarning($"{Name} could not commit typed combat exertion for {chosenAbility.Name}. Reason={commitResult.RejectionReason}");
            CombatActionResolutionDiagnosticsLogger.LogWarning("Monster.PerformAbility rejected typed commitment",
                $"Target={target?.Name ?? "NULL"}\nAbility={chosenAbility.Name}\nReason={commitResult.RejectionReason}",
                this, target);
            return;
        }

        Dictionary<DamageType, int> damageByType = new Dictionary<DamageType, int>
    {
        { chosenAbility.Type, chosenAbility.Damage }
    };

        MessageLogManager.Instance.Log("combat", Name, "uses", chosenAbility.Name, "on", target.Name);
        GameDebugger.Instance.LogInfo($"{Name} uses {chosenAbility.Name} on {target.Name}!");

        target.TakeDamage(damageByType, this);
        CombatActionResolutionDiagnosticsLogger.LogEvent("[ATTACK RESOLVED]", "Monster.PerformAbility resolved",
            $"ActionName={chosenAbility.Name}\n" +
            $"RequestedDamageType={chosenAbility.Type}\n" +
            $"FinalOutgoingDamage={CombatActionResolutionDiagnosticsLogger.FormatDamageDictionary(damageByType)}\n" +
            $"Resolver=Monster.PerformAbility\n" +
            $"StaminaAfter={FixedPointResourceMath.Format(CurrentStamina)}\n" +
            $"CombatExertionAfter={FixedPointResourceMath.Format(CurrentCombatExertion)}",
            this, target);
        }
        finally
        {
            orchestrator?.EndActionResolution($"{Name}.PerformAbility");
        }
    }

    #endregion

    #region AI Behavior
    public void UpdateMonsterAI()
    {
        if (!InTurn)
        {
            GameDebugger.Instance.LogWarning($"Monster.UpdateMonsterAI ignored for {Name} because the monster is not currently in turn.");
            return;
        }

        stateMachine.Update(); // Let the state machine decide what to do
    }

    private void Patrol()
    {
        MoveInRandomDirection();
        GameDebugger.Instance.LogInfo($"{Name} is patrolling.");
    }

    public Character FindClosestEnemy()
    {
        Character player = PlayerStats.Instance.CurrentPlayerCharacter;

        // Prioritize Player if visible
        if (player != null && CanSeeTarget(player)) return player;

        // Otherwise, target the closest NPC
        return TurnOrchestrator.Instance.GetLivingActiveAreaCharacters(CurrentNestedArea)
            .Where(c => c is NPC && CanSeeTarget(c)) // Ensure they are visible
            .OrderBy(c => Vector2.Distance(this.Position, c.Position))
            .FirstOrDefault();
    }


    #endregion
}

public class MonsterAbility
{
    public string Name { get; set; }
    public int Damage { get; set; }
    public DamageType Type { get; set; }
    public int Range { get; set; } = 1;
}
