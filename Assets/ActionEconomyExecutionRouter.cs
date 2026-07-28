using UnityEngine;

public static class ActionEconomyExecutionRouter
{
    public static void FinalizeInteractionProgress(object action, int legacyActionPointCost, bool isCombatContext, string source = "")
    {
        ActionCostProfile profile = ResolveProfile(action, isCombatContext);

        if (profile != null && profile.MigrationState == ActionEconomyMigrationState.TypedActionEconomy)
        {
            ApplyTypedProgress(profile, source, isCombatContext);
            return;
        }

        if (legacyActionPointCost > 0)
        {
            EndOfTurnManager.Instance?.AddTurnProgress(legacyActionPointCost);
        }
    }

    public static ActionCostProfile ResolveProfile(object action, bool isCombatContext)
    {
        switch (action)
        {
            case ITypedActionEconomyProfileProvider typedProvider:
                return typedProvider.ResolveActionCostProfile(isCombatContext);
            case IInteraction interaction:
                return ActionCostProfileResolver.BuildForInteraction(interaction, isCombatContext);
            case IEnvironmentalAction environmentalAction:
                return ActionCostProfileResolver.BuildForEnvironmentalAction(environmentalAction, isCombatContext);
            case IItemInteraction itemInteraction:
                return ActionCostProfileResolver.BuildForItemInteraction(itemInteraction);
            default:
                return null;
        }
    }

    private static void ApplyTypedProgress(ActionCostProfile profile, string source, bool isCombatContext)
    {
        if (profile == null)
        {
            return;
        }

        bool shouldAdvanceExploration = !isCombatContext &&
                                        (profile.ExplorationBehaviour == ExplorationActionBehaviour.TriggerCycle ||
                                         profile.ExplorationBehaviour == ExplorationActionBehaviour.Committed);

        if (shouldAdvanceExploration)
        {
            PlayerController.Instance?.CompleteExplorationTurnForTimeCostingAction(
                string.IsNullOrWhiteSpace(source) ? "TypedActionEconomy" : $"{source}.TypedActionEconomy",
                1f);
            return;
        }

        if (isCombatContext && profile.CombatBehaviour == CombatActionBehaviour.Committed)
        {
            PlayerController.Instance?.EndPlayerTurn(
                string.IsNullOrWhiteSpace(source) ? "TypedCombatActionEconomy" : $"{source}.TypedActionEconomy",
                true);
        }
    }
}
