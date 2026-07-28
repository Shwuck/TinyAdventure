using System;
using UnityEngine;

public sealed class ActionCostProfile
{
    public ActionEconomyMigrationState MigrationState { get; set; } = ActionEconomyMigrationState.Legacy;
    public ExplorationActionBehaviour ExplorationBehaviour { get; set; } = ExplorationActionBehaviour.Unavailable;
    public CombatActionBehaviour CombatBehaviour { get; set; } = CombatActionBehaviour.Unavailable;
    public bool IsFree { get; set; }
    public int WorldTimeCost { get; set; }
    public int LegacyActionPointCost { get; set; }
    public int LegacyMovePointCost { get; set; }
    public int StaminaCost { get; set; }
    public int? CombatExertionCost { get; set; }
    public int ConsumptionCapacityCost { get; set; }
    public bool CanOverexert { get; set; } = true;
    public bool EndsPlayerTurn { get; set; }
    public bool CandidateForFutureStamina { get; set; }
    public int PredictedStaminaCost { get; set; } = ActionCostProfileResolver.UnknownPredictedStaminaCost;
    public bool IsContextual { get; set; }
    public string CostLabel { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public bool HasPredictedStaminaCost => PredictedStaminaCost >= 0;
    public bool HasCombatExertionOverride => CombatExertionCost.HasValue;
    public bool HasConsumptionCapacityCost => ConsumptionCapacityCost > 0;

    public string GetStaminaCostText()
    {
        return FixedPointResourceMath.Format(StaminaCost);
    }

    public string GetCombatExertionCostText()
    {
        return CombatExertionCost.HasValue
            ? FixedPointResourceMath.Format(CombatExertionCost.Value)
            : "Inherited";
    }

    public string GetPredictedStaminaCostText()
    {
        return HasPredictedStaminaCost ? FixedPointResourceMath.Format(PredictedStaminaCost) : "Unknown";
    }

    public string GetMigrationStateText()
    {
        return MigrationState.ToString();
    }

    public string GetExplorationBehaviourText()
    {
        return ExplorationBehaviour.ToString();
    }

    public string GetCombatBehaviourText()
    {
        return CombatBehaviour.ToString();
    }
}

public static class ActionCostProfileResolver
{
    public const int UnknownPredictedStaminaCost = -1;

    private const string DiagnosticsTag = "CODEXLOG007_ACTION_COST_PROFILE";
    private const int DefaultMovementWorldTimeCost = 1;
    private const int DefaultMovementLegacyMovePointCost = 1;
    private const int DefaultMovementPredictedStaminaCost = 100;
    private const int DefaultPhysicalAttackPredictedStaminaCost = 400;
    private const int ModerateWorkPredictedStaminaCost = 400;
    private const int HeavyWorkPredictedStaminaCost = 600;
    private const int VeryHeavyWorkPredictedStaminaCost = 800;

    #region Resolution

    public static ActionCostResolution ResolveActionCosts(ActionCostProfile profile, ActionEffortModifierSet modifiers = null, string source = "")
    {
        ActionEffortModifierSet appliedModifiers = modifiers ?? ActionEffortModifierSet.None;

        if (profile == null)
        {
            return new ActionCostResolution
            {
                Source = source,
                AppliedModifiers = appliedModifiers,
                IsRejected = true,
                RejectionReason = "ActionCostProfile was null."
            };
        }

        int sharedResolved = ApplyModifiers(profile.StaminaCost, appliedModifiers.SharedFlatModifier, appliedModifiers.SharedMultiplier);
        int staminaResolved = ApplyModifiers(sharedResolved, appliedModifiers.StaminaFlatModifier, appliedModifiers.StaminaMultiplier);
        bool combatExertionInherited = !profile.CombatExertionCost.HasValue;
        int combatExertionBasis = combatExertionInherited ? sharedResolved : profile.CombatExertionCost.Value;
        int combatExertionResolved = ApplyModifiers(combatExertionBasis, appliedModifiers.CombatExertionFlatModifier, appliedModifiers.CombatExertionMultiplier);

        return new ActionCostResolution
        {
            SourceProfile = profile,
            StaminaCost = FixedPointResourceMath.NonNegative(staminaResolved),
            CombatExertionCost = FixedPointResourceMath.NonNegative(combatExertionResolved),
            ConsumptionCapacityCost = Mathf.Max(0, profile.ConsumptionCapacityCost),
            CombatExertionWasInherited = combatExertionInherited,
            CanOverexert = profile.CanOverexert,
            AppliedModifiers = appliedModifiers,
            Source = source
        };
    }

    public static ActionCostCommitment CreateCommitment(ActionCostProfile profile, ActionEffortModifierSet modifiers = null, string source = "")
    {
        return new ActionCostCommitment(ResolveActionCosts(profile, modifiers, source));
    }

    internal static ActionCostCommitResult CommitResolvedActionCosts(Character actor, ActionCostResolution resolution, string source, Guid commitmentId)
    {
        if (actor == null)
        {
            return ActionCostCommitResult.Rejected(commitmentId, resolution, "Actor was null.");
        }

        if (resolution == null)
        {
            return ActionCostCommitResult.Rejected(commitmentId, resolution, "Resolved action cost was null.");
        }

        if (resolution.IsRejected)
        {
            return ActionCostCommitResult.Rejected(commitmentId, resolution, resolution.RejectionReason);
        }

        if (!actor.CanSpendStamina(resolution.StaminaCost, out string staminaRejection))
        {
            return ActionCostCommitResult.Rejected(commitmentId, resolution, staminaRejection);
        }

        if (!resolution.CanOverexert && actor.CurrentStamina - resolution.StaminaCost < 0)
        {
            return ActionCostCommitResult.Rejected(commitmentId, resolution, "This action prohibits overexertion.");
        }

        if (!actor.CanSpendCombatExertion(resolution.CombatExertionCost, out string exertionRejection))
        {
            return ActionCostCommitResult.Rejected(commitmentId, resolution, exertionRejection);
        }

        if (!actor.CanSpendConsumptionCapacity(resolution.ConsumptionCapacityCost, out string capacityRejection))
        {
            return ActionCostCommitResult.Rejected(commitmentId, resolution, capacityRejection);
        }

        int staminaSpent = 0;
        int exertionSpent = 0;
        int consumptionCapacitySpent = 0;

        if (resolution.StaminaCost > 0 && !actor.SpendStamina(resolution.StaminaCost, source))
        {
            return ActionCostCommitResult.Rejected(commitmentId, resolution, "Stamina spend failed during commit.");
        }

        staminaSpent = resolution.StaminaCost;

        if (resolution.CombatExertionCost > 0 && !actor.SpendCombatExertion(resolution.CombatExertionCost, source))
        {
            if (staminaSpent > 0)
            {
                actor.RegenerateStamina(staminaSpent, $"{source}:CommitRollback");
            }

            return ActionCostCommitResult.Rejected(commitmentId, resolution, "Combat exertion spend failed during commit.");
        }

        exertionSpent = resolution.CombatExertionCost;

        if (resolution.ConsumptionCapacityCost > 0 && !actor.SpendConsumptionCapacity(resolution.ConsumptionCapacityCost, source))
        {
            if (staminaSpent > 0)
            {
                actor.RegenerateStamina(staminaSpent, $"{source}:CommitRollback");
            }

            return ActionCostCommitResult.Rejected(commitmentId, resolution, "Consumption capacity spend failed during commit.");
        }

        consumptionCapacitySpent = resolution.ConsumptionCapacityCost;

        if (staminaSpent != resolution.StaminaCost || exertionSpent != resolution.CombatExertionCost || consumptionCapacitySpent != resolution.ConsumptionCapacityCost)
        {
            return ActionCostCommitResult.Rejected(commitmentId, resolution, "Commitment validation changed after affordability check.");
        }

        return ActionCostCommitResult.Committed(commitmentId, resolution, staminaSpent, exertionSpent, consumptionCapacitySpent);
    }

    private static int ApplyModifiers(int baseCost, int flatModifier, float multiplier)
    {
        float modifiedValue = (baseCost + flatModifier) * multiplier;
        return Mathf.RoundToInt(modifiedValue);
    }

    #endregion

    #region Builders

    public static ActionCostProfile BuildForMovement(bool isCombatContext)
    {
        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
            ExplorationBehaviour = isCombatContext ? ExplorationActionBehaviour.Unavailable : ExplorationActionBehaviour.TriggerCycle,
            CombatBehaviour = isCombatContext ? CombatActionBehaviour.Flexible : CombatActionBehaviour.Unavailable,
            IsFree = false,
            WorldTimeCost = isCombatContext ? 0 : DefaultMovementWorldTimeCost,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = DefaultMovementLegacyMovePointCost,
            StaminaCost = 0,
            CombatExertionCost = isCombatContext ? FixedPointResourceMath.FromPoints(1f) : null,
            CanOverexert = true,
            EndsPlayerTurn = !isCombatContext,
            CandidateForFutureStamina = true,
            PredictedStaminaCost = DefaultMovementPredictedStaminaCost,
            IsContextual = false,
            CostLabel = string.Empty,
            Notes = isCombatContext
                ? "Combat movement currently spends MP and keeps the combat turn open. Predicted stamina is metadata only."
                : "Exploration movement currently advances world time and completes the player turn. Predicted stamina is metadata only."
        };
    }

    public static ActionCostProfile BuildForWaitOrEndTurn(bool isCombatContext)
    {
        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.Legacy,
            ExplorationBehaviour = isCombatContext ? ExplorationActionBehaviour.Unavailable : ExplorationActionBehaviour.Committed,
            CombatBehaviour = CombatActionBehaviour.Unavailable,
            IsFree = false,
            WorldTimeCost = isCombatContext ? 0 : 1,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
            StaminaCost = 0,
            CombatExertionCost = 0,
            CanOverexert = true,
            EndsPlayerTurn = true,
            CandidateForFutureStamina = false,
            PredictedStaminaCost = 0,
            IsContextual = false,
            CostLabel = "Ends turn",
            Notes = isCombatContext
                ? "Combat End Turn is a turn-completion control, not a stamina-spending action. Future versions may use it for recovery."
                : "Exploration Wait is a time-costing turn-completion action, not a stamina-spending action. Future versions may use it for recovery."
        };
    }

    public static ActionCostProfile BuildForItemInteraction(IItemInteraction interaction)
    {
        if (interaction == null)
        {
            return BuildUnknownProfile("Item interaction was null.");
        }

        if (interaction is ConsumeInteraction)
        {
            return BuildTypedConsumeProfile();
        }

        if (interaction is EquipInteraction)
        {
            return BuildTypedEquipProfile();
        }

        string normalizedActionName = NormalizeActionName(interaction.Name);

        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
            ExplorationBehaviour = ExplorationActionBehaviour.Free,
            CombatBehaviour = CombatActionBehaviour.Flexible,
            IsFree = false,
            WorldTimeCost = 0,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
            StaminaCost = 0,
            CombatExertionCost = null,
            CanOverexert = true,
            EndsPlayerTurn = false,
            CandidateForFutureStamina = false,
            PredictedStaminaCost = UnknownPredictedStaminaCost,
            IsContextual = true,
            CostLabel = string.Empty,
            Notes = GetItemInteractionNotes(normalizedActionName, false)
        };
    }

    public static ActionCostProfile BuildForInteraction(IInteraction interaction, bool isCombatContext)
    {
        if (interaction == null)
        {
            return BuildUnknownProfile("Interaction was null.");
        }

        string normalizedActionName = NormalizeActionName(interaction.Name);
        int rawCost = ClampNonNegative(interaction.ActionPointCost);

        if (interaction is PunchInteraction ||
            interaction is SlashInteraction ||
            interaction is StabInteraction ||
            interaction is BashInteraction ||
            interaction is RendInteraction)
        {
            return BuildTypedPhysicalCombatProfile(rawCost, isCombatContext);
        }

        if (interaction is TakeABreathInteraction)
        {
            return BuildTypedRecoveryProfile();
        }

        if (interaction is InspectInteraction ||
            interaction is InspectNPCInteraction ||
            interaction is TalkInteraction)
        {
            return BuildTypedFreeInteractionProfile();
        }

        if (interaction is AscendInteraction ||
            interaction is DescendInteraction ||
            interaction is EnterDungeonInteraction ||
            interaction is EnterCaveInteraction)
        {
            return BuildTypedTransitionProfile();
        }

        if (IsFreeInformationalInteraction(interaction, normalizedActionName))
        {
            return new ActionCostProfile
            {
                MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
                ExplorationBehaviour = ExplorationActionBehaviour.Free,
                CombatBehaviour = CombatActionBehaviour.Free,
                IsFree = true,
                WorldTimeCost = 0,
                LegacyActionPointCost = 0,
                LegacyMovePointCost = 0,
                StaminaCost = 0,
                CombatExertionCost = 0,
                CanOverexert = true,
                EndsPlayerTurn = false,
                CandidateForFutureStamina = false,
                PredictedStaminaCost = 0,
                IsContextual = false,
                CostLabel = string.Empty,
                Notes = "Informational inspection-style action. Free by current semantics and not a future stamina candidate."
            };
        }

        if (IsBasicPhysicalCombatAction(normalizedActionName))
        {
            return new ActionCostProfile
            {
                MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
                ExplorationBehaviour = ExplorationActionBehaviour.TriggerCycle,
                CombatBehaviour = CombatActionBehaviour.Flexible,
                IsFree = false,
                WorldTimeCost = 0,
                LegacyActionPointCost = rawCost > 0 ? rawCost : CombatResolver.DefaultPhysicalAttackActionPointCost,
                LegacyMovePointCost = 0,
                StaminaCost = DefaultPhysicalAttackPredictedStaminaCost,
                CombatExertionCost = null,
                CanOverexert = true,
                EndsPlayerTurn = false,
                CandidateForFutureStamina = true,
                PredictedStaminaCost = DefaultPhysicalAttackPredictedStaminaCost,
                IsContextual = false,
                CostLabel = FormatActionPointLabel(rawCost > 0 ? rawCost : CombatResolver.DefaultPhysicalAttackActionPointCost),
                Notes = "Shared physical combat action. Current AP semantics stay live; predicted stamina is metadata only."
            };
        }

        if (IsMagicCombatAction(normalizedActionName))
        {
            return new ActionCostProfile
            {
                MigrationState = ActionEconomyMigrationState.Legacy,
                ExplorationBehaviour = ExplorationActionBehaviour.Unavailable,
                CombatBehaviour = CombatActionBehaviour.Unavailable,
                IsFree = false,
                WorldTimeCost = rawCost,
                LegacyActionPointCost = rawCost,
                LegacyMovePointCost = 0,
                StaminaCost = 0,
                CombatExertionCost = 0,
                CanOverexert = false,
                EndsPlayerTurn = false,
                CandidateForFutureStamina = false,
                PredictedStaminaCost = UnknownPredictedStaminaCost,
                IsContextual = true,
                CostLabel = rawCost > 0 ? FormatActionPointLabel(rawCost) : string.Empty,
                Notes = "Magic action remains AP-backed today and also advances world time in its direct execution path. Future stamina use is intentionally left uncertain in this pass."
            };
        }

        if (IsContextualZeroCostInteraction(normalizedActionName, rawCost))
        {
            return new ActionCostProfile
            {
                MigrationState = ActionEconomyMigrationState.Legacy,
                ExplorationBehaviour = ExplorationActionBehaviour.Free,
                CombatBehaviour = CombatActionBehaviour.Free,
                IsFree = false,
                WorldTimeCost = 0,
                LegacyActionPointCost = rawCost,
                LegacyMovePointCost = 0,
                StaminaCost = 0,
                CombatExertionCost = 0,
                CanOverexert = true,
                EndsPlayerTurn = false,
                CandidateForFutureStamina = IsFutureExertionCandidate(normalizedActionName),
                PredictedStaminaCost = GetPredictedContextualStaminaCost(normalizedActionName),
                IsContextual = true,
                CostLabel = string.Empty,
                Notes = GetContextualInteractionNotes(normalizedActionName)
            };
        }

        if (rawCost > 0)
        {
            bool candidateForFutureStamina = IsFutureExertionCandidate(normalizedActionName);
            return new ActionCostProfile
            {
                MigrationState = ActionEconomyMigrationState.Legacy,
                ExplorationBehaviour = interaction.Type == InteractionType.Combat ? ExplorationActionBehaviour.Unavailable : ExplorationActionBehaviour.Committed,
                CombatBehaviour = CombatActionBehaviour.Unavailable,
                IsFree = false,
                WorldTimeCost = interaction.Type == InteractionType.Combat ? 0 : rawCost,
                LegacyActionPointCost = rawCost,
                LegacyMovePointCost = 0,
                StaminaCost = candidateForFutureStamina ? GetPredictedWorkStaminaCost(normalizedActionName) : 0,
                CombatExertionCost = candidateForFutureStamina ? null : 0,
                CanOverexert = true,
                EndsPlayerTurn = interaction.Type != InteractionType.Combat && !isCombatContext,
                CandidateForFutureStamina = candidateForFutureStamina,
                PredictedStaminaCost = candidateForFutureStamina ? GetPredictedWorkStaminaCost(normalizedActionName) : 0,
                IsContextual = false,
                CostLabel = interaction.Type == InteractionType.Combat && isCombatContext
                    ? FormatActionPointLabel(rawCost)
                    : "Takes time",
                Notes = interaction.Type == InteractionType.Combat
                    ? "Combat interaction remains AP-backed."
                    : "Legacy ActionPointCost is currently overloaded here as both AP-style gate and world-time progress. Predicted stamina is metadata only."
            };
        }

        return BuildUnknownProfile($"No explicit interaction classification was assigned for '{interaction.Name}'.");
    }

    public static ActionCostProfile BuildForEnvironmentalAction(IEnvironmentalAction action, bool isCombatContext)
    {
        if (action == null)
        {
            return BuildUnknownProfile("Environmental action was null.");
        }

        string normalizedActionName = NormalizeActionName(action.Name);
        int rawCost = ClampNonNegative(action.ActionPointCost);

        if (action is InspectItemsAction ||
            action is PickUpItemsAction ||
            action is PickUpALLItemsAction)
        {
            return action is InspectItemsAction
                ? BuildTypedFreeEnvironmentalProfile()
                : BuildUnknownProfile("Pickup environmental action remains legacy until its live body is migrated.");
        }

        if (action is RestAction)
        {
            return BuildTypedRestProfile();
        }

        if (IsFreeEnvironmentalInformation(normalizedActionName))
        {
            return new ActionCostProfile
            {
                MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
                ExplorationBehaviour = ExplorationActionBehaviour.Free,
                CombatBehaviour = CombatActionBehaviour.Free,
                IsFree = true,
                WorldTimeCost = 0,
                LegacyActionPointCost = 0,
                LegacyMovePointCost = 0,
                StaminaCost = 0,
                CombatExertionCost = 0,
                CanOverexert = true,
                EndsPlayerTurn = false,
                CandidateForFutureStamina = false,
                PredictedStaminaCost = 0,
                IsContextual = false,
                CostLabel = string.Empty,
                Notes = "Informational environmental action. Free by current semantics and not a future stamina candidate."
            };
        }

        if (IsContextualZeroCostEnvironmentalAction(normalizedActionName, rawCost))
        {
            return new ActionCostProfile
            {
                MigrationState = ActionEconomyMigrationState.Legacy,
                ExplorationBehaviour = ExplorationActionBehaviour.Free,
                CombatBehaviour = CombatActionBehaviour.Free,
                IsFree = false,
                WorldTimeCost = 0,
                LegacyActionPointCost = rawCost,
                LegacyMovePointCost = 0,
                StaminaCost = 0,
                CombatExertionCost = 0,
                CanOverexert = true,
                EndsPlayerTurn = false,
                CandidateForFutureStamina = false,
                PredictedStaminaCost = UnknownPredictedStaminaCost,
                IsContextual = true,
                CostLabel = string.Empty,
                Notes = "Contextual environmental action left intentionally unclassified for future action-cost refinement."
            };
        }

        bool candidateForFutureStamina = IsFutureExertionCandidate(normalizedActionName);
        int predictedCost = candidateForFutureStamina ? GetPredictedWorkStaminaCost(normalizedActionName) : 0;

        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.Legacy,
            ExplorationBehaviour = rawCost > 0 ? ExplorationActionBehaviour.Committed : ExplorationActionBehaviour.Unavailable,
            CombatBehaviour = CombatActionBehaviour.Unavailable,
            IsFree = false,
            WorldTimeCost = rawCost,
            LegacyActionPointCost = rawCost,
            LegacyMovePointCost = 0,
            StaminaCost = candidateForFutureStamina ? GetPredictedWorkStaminaCost(normalizedActionName) : 0,
            CombatExertionCost = candidateForFutureStamina ? null : 0,
            CanOverexert = true,
            EndsPlayerTurn = rawCost > 0 && !isCombatContext,
            CandidateForFutureStamina = candidateForFutureStamina,
            PredictedStaminaCost = predictedCost,
            IsContextual = false,
            CostLabel = rawCost > 0 ? "Takes time" : string.Empty,
            Notes = candidateForFutureStamina
                ? "Physical work/travel-like environmental action. Predicted stamina is metadata only."
                : "Time-costing environmental action. Current AP/turn semantics stay live; predicted stamina is metadata only."
        };
    }

    public static ActionCostProfile BuildForCombatAttackContext(AttackContext context)
    {
        if (context == null)
        {
            return BuildUnknownProfile("AttackContext was null.");
        }

        bool futureStaminaCandidate = context.Category == AttackCategory.Weapon ||
                                      context.Category == AttackCategory.Unarmed ||
                                      context.Category == AttackCategory.Natural;

        int predictedStaminaCost = futureStaminaCandidate
            ? DefaultPhysicalAttackPredictedStaminaCost
            : UnknownPredictedStaminaCost;

        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
            ExplorationBehaviour = ExplorationActionBehaviour.TriggerCycle,
            CombatBehaviour = CombatActionBehaviour.Flexible,
            IsFree = false,
            WorldTimeCost = 0,
            LegacyActionPointCost = ClampNonNegative(context.ActionPointCost),
            LegacyMovePointCost = 0,
            StaminaCost = futureStaminaCandidate ? DefaultPhysicalAttackPredictedStaminaCost : 0,
            CombatExertionCost = futureStaminaCandidate ? null : 0,
            CanOverexert = futureStaminaCandidate,
            EndsPlayerTurn = false,
            CandidateForFutureStamina = futureStaminaCandidate,
            PredictedStaminaCost = predictedStaminaCost,
            IsContextual = context.Category == AttackCategory.Magic || context.Category == AttackCategory.Ability,
            CostLabel = FormatActionPointLabel(ClampNonNegative(context.ActionPointCost)),
            Notes = futureStaminaCandidate
                ? "Shared physical attack path. Predicted stamina cost is metadata only and is not enforced."
                : "Shared attack path with non-physical category. Future stamina use remains intentionally uncertain."
        };
    }

    private static ActionCostProfile BuildTypedFreeInteractionProfile()
    {
        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
            ExplorationBehaviour = ExplorationActionBehaviour.Free,
            CombatBehaviour = CombatActionBehaviour.Free,
            IsFree = true,
            WorldTimeCost = 0,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
            StaminaCost = 0,
            CombatExertionCost = 0,
            ConsumptionCapacityCost = 0,
            CanOverexert = true,
            EndsPlayerTurn = false,
            CandidateForFutureStamina = false,
            PredictedStaminaCost = 0,
            IsContextual = true,
            CostLabel = string.Empty,
            Notes = "Typed free interaction. No resource spend and no turn progression."
        };
    }

    private static ActionCostProfile BuildTypedFreeEnvironmentalProfile()
    {
        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
            ExplorationBehaviour = ExplorationActionBehaviour.Free,
            CombatBehaviour = CombatActionBehaviour.Free,
            IsFree = true,
            WorldTimeCost = 0,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
            StaminaCost = 0,
            CombatExertionCost = 0,
            ConsumptionCapacityCost = 0,
            CanOverexert = true,
            EndsPlayerTurn = false,
            CandidateForFutureStamina = false,
            PredictedStaminaCost = 0,
            IsContextual = true,
            CostLabel = string.Empty,
            Notes = "Typed free environmental action. No resource spend and no turn progression."
        };
    }

    private static ActionCostProfile BuildTypedPhysicalCombatProfile(int legacyActionPointCost, bool isCombatContext)
    {
        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
            ExplorationBehaviour = ExplorationActionBehaviour.TriggerCycle,
            CombatBehaviour = CombatActionBehaviour.Flexible,
            IsFree = false,
            WorldTimeCost = 0,
            LegacyActionPointCost = legacyActionPointCost,
            LegacyMovePointCost = 0,
            StaminaCost = FixedPointResourceMath.FromPoints(5f),
            CombatExertionCost = isCombatContext ? null : 0,
            ConsumptionCapacityCost = 0,
            CanOverexert = true,
            EndsPlayerTurn = false,
            CandidateForFutureStamina = true,
            PredictedStaminaCost = FixedPointResourceMath.FromPoints(5f),
            IsContextual = false,
            CostLabel = legacyActionPointCost > 0 ? FormatActionPointLabel(legacyActionPointCost) : string.Empty,
            Notes = "Typed physical combat action. Stamina is authoritative and combat exertion inherits from stamina."
        };
    }

    private static ActionCostProfile BuildTypedTransitionProfile()
    {
        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
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
            IsContextual = true,
            CostLabel = string.Empty,
            Notes = "Typed area transition. Commits the action and yields the exploration opportunity."
        };
    }

    private static ActionCostProfile BuildTypedRecoveryProfile()
    {
        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
            ExplorationBehaviour = ExplorationActionBehaviour.Unavailable,
            CombatBehaviour = CombatActionBehaviour.Recovery,
            IsFree = false,
            WorldTimeCost = 0,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
            StaminaCost = 0,
            CombatExertionCost = FixedPointResourceMath.FromPoints(1f),
            ConsumptionCapacityCost = 0,
            CanOverexert = true,
            EndsPlayerTurn = false,
            CandidateForFutureStamina = false,
            PredictedStaminaCost = 0,
            IsContextual = false,
            CostLabel = string.Empty,
            Notes = "Typed combat recovery action. Costs combat exertion and queues end-of-turn stamina recovery."
        };
    }

    private static ActionCostProfile BuildTypedEquipProfile()
    {
        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
            ExplorationBehaviour = ExplorationActionBehaviour.Free,
            CombatBehaviour = CombatActionBehaviour.Flexible,
            IsFree = false,
            WorldTimeCost = 0,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
            StaminaCost = 0,
            CombatExertionCost = 0,
            ConsumptionCapacityCost = 0,
            CanOverexert = true,
            EndsPlayerTurn = false,
            CandidateForFutureStamina = false,
            PredictedStaminaCost = 0,
            IsContextual = false,
            CostLabel = string.Empty,
            Notes = "Typed tactical equipment action. Exploration is free; combat spends one combat exertion."
        };
    }

    private static ActionCostProfile BuildTypedRestProfile()
    {
        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
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
            Notes = "Typed exploration recovery action. Queues additional end-of-turn stamina regeneration."
        };
    }

    private static ActionCostProfile BuildTypedConsumeProfile()
    {
        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.TypedActionEconomy,
            ExplorationBehaviour = ExplorationActionBehaviour.TriggerCycle,
            CombatBehaviour = CombatActionBehaviour.Flexible,
            IsFree = false,
            WorldTimeCost = 0,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
            StaminaCost = 0,
            CombatExertionCost = null,
            ConsumptionCapacityCost = 1,
            CanOverexert = true,
            EndsPlayerTurn = false,
            CandidateForFutureStamina = false,
            PredictedStaminaCost = 0,
            IsContextual = false,
            CostLabel = string.Empty,
            Notes = "Typed consumable action. Consumption capacity is committed live."
        };
    }

    #endregion

    #region Presentation

    public static string BuildActionButtonLabel(string actionName, string suffix, ActionCostProfile profile)
    {
        string trimmedActionName = string.IsNullOrWhiteSpace(actionName) ? "Action" : actionName.Trim();
        string trimmedSuffix = string.IsNullOrWhiteSpace(suffix) ? string.Empty : suffix.Trim();
        string trimmedCostLabel = profile?.CostLabel?.Trim() ?? string.Empty;

        string label = trimmedActionName;
        if (!string.IsNullOrEmpty(trimmedCostLabel))
        {
            label += $" ({trimmedCostLabel})";
        }

        if (!string.IsNullOrEmpty(trimmedSuffix))
        {
            label += $" {trimmedSuffix}";
        }

        return label;
    }

    #endregion

    #region Diagnostics

    public static void LogPredictedCost(string source, string actionName, ActionCostProfile profile, Character actor = null)
    {
        if (GameDebugger.Instance == null || profile == null)
        {
            return;
        }

        string actorName = actor != null ? actor.Name : "UnknownActor";
        string safeActionName = string.IsNullOrWhiteSpace(actionName) ? "UnknownAction" : actionName;

        GameDebugger.Instance.LogInfo(
            $"{DiagnosticsTag} Source={source} Actor={actorName} Action={safeActionName} MigrationState={profile.GetMigrationStateText()} ExplorationBehaviour={profile.GetExplorationBehaviourText()} CombatBehaviour={profile.GetCombatBehaviourText()} IsFree={profile.IsFree} WorldTimeCost={profile.WorldTimeCost} LegacyActionPointCost={profile.LegacyActionPointCost} LegacyMovePointCost={profile.LegacyMovePointCost} StaminaCost={profile.GetStaminaCostText()} CombatExertionCost={profile.GetCombatExertionCostText()} ConsumptionCapacityCost={profile.ConsumptionCapacityCost} CanOverexert={profile.CanOverexert} EndsPlayerTurn={profile.EndsPlayerTurn} CandidateForFutureStamina={profile.CandidateForFutureStamina} PredictedStaminaCost={profile.GetPredictedStaminaCostText()} IsContextual={profile.IsContextual} CostLabel={profile.CostLabel ?? string.Empty} Notes={profile.Notes} Legacy classification only; live stamina/exertion resolution uses the structured fields.");
    }

    #endregion

    #region Classification Helpers

    private static ActionCostProfile BuildUnknownProfile(string notes)
    {
        return new ActionCostProfile
        {
            MigrationState = ActionEconomyMigrationState.Legacy,
            ExplorationBehaviour = ExplorationActionBehaviour.Unavailable,
            CombatBehaviour = CombatActionBehaviour.Unavailable,
            IsFree = false,
            WorldTimeCost = 0,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
            StaminaCost = 0,
            CombatExertionCost = 0,
            CanOverexert = true,
            EndsPlayerTurn = false,
            CandidateForFutureStamina = false,
            PredictedStaminaCost = UnknownPredictedStaminaCost,
            IsContextual = true,
            CostLabel = string.Empty,
            Notes = notes
        };
    }

    private static string NormalizeActionName(string actionName)
    {
        return string.IsNullOrWhiteSpace(actionName)
            ? string.Empty
            : actionName.Trim().ToLowerInvariant();
    }

    private static int ClampNonNegative(int value)
    {
        return Mathf.Max(0, value);
    }

    private static string FormatActionPointLabel(int actionPointCost)
    {
        return actionPointCost > 0 ? $"{actionPointCost} AP" : string.Empty;
    }

    private static bool IsFreeInformationalInteraction(IInteraction interaction, string normalizedActionName)
    {
        return interaction.Type == InteractionType.Inspection ||
               normalizedActionName == "inspect" ||
               normalizedActionName == "inspect items" ||
               normalizedActionName == "inspect npc" ||
               normalizedActionName == "look" ||
               normalizedActionName == "examine" ||
               normalizedActionName == "view village sign post";
    }

    private static bool IsFreeEnvironmentalInformation(string normalizedActionName)
    {
        return normalizedActionName == "inspect items";
    }

    private static bool IsBasicPhysicalCombatAction(string normalizedActionName)
    {
        return normalizedActionName == "punch" ||
               normalizedActionName == "slash" ||
               normalizedActionName == "stab" ||
               normalizedActionName == "bash" ||
               normalizedActionName == "rend";
    }

    private static bool IsMagicCombatAction(string normalizedActionName)
    {
        return normalizedActionName == "magic attack";
    }

    private static bool IsContextualZeroCostInteraction(string normalizedActionName, int rawCost)
    {
        if (rawCost > 0)
        {
            return false;
        }

        return normalizedActionName == "talk" ||
               normalizedActionName == "trade" ||
               normalizedActionName == "pickpocket" ||
               normalizedActionName == "shove" ||
               normalizedActionName == "pet" ||
               normalizedActionName == "shake" ||
               normalizedActionName == "open chest" ||
               normalizedActionName == "take ear" ||
               normalizedActionName == "donate" ||
               normalizedActionName == "smith" ||
               normalizedActionName == "craft" ||
               normalizedActionName == "cook at" ||
               normalizedActionName.StartsWith("cook at", StringComparison.Ordinal) ||
               normalizedActionName == "open container" ||
               normalizedActionName == "empty container" ||
               normalizedActionName == "ascend" ||
               normalizedActionName == "descend" ||
               normalizedActionName == "enter dungeon" ||
               normalizedActionName == "enter cave";
    }

    private static bool IsContextualZeroCostEnvironmentalAction(string normalizedActionName, int rawCost)
    {
        if (rawCost > 0)
        {
            return false;
        }

        return normalizedActionName == "pick up items" ||
               normalizedActionName == "pick up item" ||
               normalizedActionName == "pick up all items";
    }

    private static bool IsFutureExertionCandidate(string normalizedActionName)
    {
        return normalizedActionName == "shove" ||
               normalizedActionName == "chop" ||
               normalizedActionName == "gather" ||
               normalizedActionName == "mine" ||
               normalizedActionName == "pick flower" ||
               normalizedActionName == "cut" ||
               normalizedActionName == "clear with shovel" ||
               normalizedActionName == "clear with pickaxe" ||
               normalizedActionName == "feed animal" ||
               normalizedActionName == "tame animal" ||
               normalizedActionName == "mount animal" ||
               normalizedActionName == "mount" ||
               normalizedActionName == "dig" ||
               normalizedActionName == "till soil" ||
               normalizedActionName == "plant seeds" ||
               normalizedActionName == "fish" ||
               normalizedActionName == "place anvil";
    }

    private static int GetPredictedContextualStaminaCost(string normalizedActionName)
    {
        if (normalizedActionName == "shove")
        {
            return DefaultPhysicalAttackPredictedStaminaCost;
        }

        return UnknownPredictedStaminaCost;
    }

    private static int GetPredictedWorkStaminaCost(string normalizedActionName)
    {
        if (normalizedActionName == "clear with pickaxe" ||
            normalizedActionName == "place anvil")
        {
            return VeryHeavyWorkPredictedStaminaCost;
        }

        if (normalizedActionName == "chop" ||
            normalizedActionName == "mine" ||
            normalizedActionName == "dig" ||
            normalizedActionName == "clear with shovel" ||
            normalizedActionName == "tame animal")
        {
            return HeavyWorkPredictedStaminaCost;
        }

        if (normalizedActionName == "gather" ||
            normalizedActionName == "pick flower" ||
            normalizedActionName == "cut" ||
            normalizedActionName == "feed animal" ||
            normalizedActionName == "mount animal" ||
            normalizedActionName == "mount" ||
            normalizedActionName == "till soil" ||
            normalizedActionName == "plant seeds" ||
            normalizedActionName == "fish")
        {
            return ModerateWorkPredictedStaminaCost;
        }

        return DefaultPhysicalAttackPredictedStaminaCost;
    }

    private static string GetContextualInteractionNotes(string normalizedActionName)
    {
        if (normalizedActionName == "talk" || normalizedActionName == "trade")
        {
            return "Conversation/trade initiation remains contextual. Opening the panel is not yet treated as a time or stamina authority.";
        }

        if (normalizedActionName == "smith" ||
            normalizedActionName == "craft" ||
            normalizedActionName == "cook at" ||
            normalizedActionName.StartsWith("cook at", StringComparison.Ordinal))
        {
            return "Panel-opening crafting interaction left contextual. Actual production costs should be classified separately from panel access.";
        }

        if (normalizedActionName == "ascend" ||
            normalizedActionName == "descend" ||
            normalizedActionName == "enter dungeon" ||
            normalizedActionName == "enter cave")
        {
            return "Travel/transition interaction left contextual because current live execution is still zero-cost even though future semantics likely include time and possible stamina.";
        }

        if (normalizedActionName == "shove")
        {
            return "Currently free because the legacy numeric cost is zero, but it is a future exertion candidate. Predicted stamina is metadata only.";
        }

        return "Contextual interaction left intentionally unclassified in this pass because the current live numeric cost does not yet match the intended long-term semantics.";
    }

    private static bool IsInventoryAdministrationItemInteraction(string normalizedActionName)
    {
        return normalizedActionName == "equip" ||
               normalizedActionName == "unequip" ||
               normalizedActionName == "make active" ||
               normalizedActionName == "deactivate" ||
               normalizedActionName == "drop" ||
               normalizedActionName == "deseed";
    }

    private static string GetItemInteractionNotes(string normalizedActionName, bool isFreeAdministrationAction)
    {
        if (normalizedActionName == "consume")
        {
            return "Item consumption remains metadata-only here because IItemInteraction exposes no cost model. Current live execution is free by omission; future time or stamina semantics remain unresolved.";
        }

        if (isFreeAdministrationAction)
        {
            return "Inventory administration action. Free by current semantics and not a future stamina candidate.";
        }

        return "Item interaction left contextual because the current interface has no cost metadata and the live execution path does not expose a stable time or stamina contract yet.";
    }

    #endregion
}
