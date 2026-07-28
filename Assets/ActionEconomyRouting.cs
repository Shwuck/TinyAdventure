using System;

public enum ActionEconomyMigrationState
{
    Legacy,
    TypedActionEconomy
}

public enum ExplorationActionBehaviour
{
    Free,
    TriggerCycle,
    Committed,
    Unavailable
}

public enum CombatActionBehaviour
{
    Free,
    Flexible,
    Committed,
    Recovery,
    Unavailable
}

public interface ITypedActionEconomyProfileProvider
{
    ActionEconomyMigrationState MigrationState { get; }
    ActionCostProfile ResolveActionCostProfile(bool isCombatContext);
}
