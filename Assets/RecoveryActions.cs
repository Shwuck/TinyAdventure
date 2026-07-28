using UnityEngine;

public class TakeABreathInteraction : IInteraction, ITypedActionEconomyProfileProvider
{
    private const int RecoveryBonus = 200;
    private const int BreathCombatExertionCost = 100;

    public InteractionType Type => InteractionType.Combat;
    public string Name => "Take a Breath";
    public int ActionPointCost => 0;
    public ActionEconomyMigrationState MigrationState => ActionEconomyMigrationState.TypedActionEconomy;

    public ActionCostProfile ResolveActionCostProfile(bool isCombatContext)
    {
        return new ActionCostProfile
        {
            MigrationState = MigrationState,
            ExplorationBehaviour = ExplorationActionBehaviour.Unavailable,
            CombatBehaviour = CombatActionBehaviour.Recovery,
            IsFree = false,
            WorldTimeCost = 0,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
            StaminaCost = 0,
            CombatExertionCost = BreathCombatExertionCost,
            ConsumptionCapacityCost = 0,
            CanOverexert = true,
            EndsPlayerTurn = false,
            CandidateForFutureStamina = false,
            PredictedStaminaCost = 0,
            IsContextual = false,
            CostLabel = string.Empty,
            Notes = "Typed combat recovery action. Converts combat exertion into a queued end-of-turn stamina bonus."
        };
    }

    public void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        Character actor = PlayerStats.Instance?.CurrentPlayerCharacter;
        if (actor == null)
        {
            GameDebugger.Instance.LogWarning("TakeABreathInteraction: no active player character.");
            return;
        }

        if (TurnOrchestrator.Instance?.CurrentContext != TurnContext.Combat)
        {
            GameDebugger.Instance.LogWarning("TakeABreathInteraction rejected outside combat.");
            return;
        }

        if (!actor.CanUseTakeABreath())
        {
            GameDebugger.Instance.LogWarning("TakeABreathInteraction rejected because the actor has already used it this combat turn.");
            return;
        }

        ActionCostProfile profile = ResolveActionCostProfile(true);
        ActionCostCommitment commitment = ActionCostProfileResolver.CreateCommitment(profile, null, "TakeABreathInteraction.ExecuteInteraction");
        ActionCostCommitResult commitResult = commitment.TryCommit(actor, "TakeABreathInteraction.ExecuteInteraction");
        if (!commitResult.IsCommitted)
        {
            GameDebugger.Instance.LogWarning($"TakeABreathInteraction rejected. Reason={commitResult.RejectionReason}");
            return;
        }

        if (!actor.SpendTakeABreathUse("TakeABreathInteraction.ExecuteInteraction"))
        {
            return;
        }

        actor.QueueStaminaRegenerationBonus(RecoveryBonus, "TakeABreathInteraction.ExecuteInteraction");
        Debug.Log("You steady your breathing and prepare to recover stamina at turn end.");
    }

    public bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        Character actor = PlayerStats.Instance?.CurrentPlayerCharacter;
        return actor != null &&
               TurnOrchestrator.Instance != null &&
               TurnOrchestrator.Instance.CurrentContext == TurnContext.Combat &&
               actor.CanUseTakeABreath() &&
               actor.CurrentCombatExertion >= BreathCombatExertionCost;
    }
}

public class RestAction : IEnvironmentalAction, ITypedActionEconomyProfileProvider
{
    private const int RestRecoveryBonus = 400;

    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Rest";
    public int ActionPointCost => 0;
    public ActionEconomyMigrationState MigrationState => ActionEconomyMigrationState.TypedActionEconomy;

    public ActionCostProfile ResolveActionCostProfile(bool isCombatContext)
    {
        return new ActionCostProfile
        {
            MigrationState = MigrationState,
            ExplorationBehaviour = ExplorationActionBehaviour.Committed,
            CombatBehaviour = CombatActionBehaviour.Unavailable,
            IsFree = false,
            WorldTimeCost = 0,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
            StaminaCost = 0,
            CombatExertionCost = 0,
            ConsumptionCapacityCost = 0,
            CanOverexert = true,
            EndsPlayerTurn = true,
            CandidateForFutureStamina = false,
            PredictedStaminaCost = 0,
            IsContextual = false,
            CostLabel = string.Empty,
            Notes = "Typed exploration recovery action. Queues extra end-of-turn stamina recovery."
        };
    }

    public bool IsAvailable(Cell cell, PlayerInventory inventory)
    {
        return PlayerStats.Instance != null &&
               PlayerStats.Instance.CurrentPlayerCharacter != null &&
               TurnOrchestrator.Instance != null &&
               TurnOrchestrator.Instance.CurrentContext == TurnContext.Exploration;
    }

    public void ExecuteAction(Cell cell, PlayerInventory inventory)
    {
        Character actor = PlayerStats.Instance?.CurrentPlayerCharacter;
        if (actor == null)
        {
            GameDebugger.Instance.LogWarning("RestAction: no active player character.");
            return;
        }

        actor.QueueStaminaRegenerationBonus(RestRecoveryBonus, "RestAction.ExecuteAction");
        Debug.Log("You rest and prepare to recover stamina when the turn completes.");
    }
}
