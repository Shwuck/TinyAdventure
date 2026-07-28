using System;
using UnityEngine;

public static class FixedPointResourceMath
{
    public const int UnitsPerPoint = 100;
    public const int DefaultMaximumStamina = 1000;
    public const int DefaultMaximumCombatExertion = 1000;
    public const int DefaultMaximumConsumptionCapacity = 3;
    public const int DefaultMinimumCurrentStamina = -2000;
    public const int BaseStaminaRegeneration = 300;

    public static int FromPoints(float points)
    {
        return Mathf.RoundToInt(points * UnitsPerPoint);
    }

    public static float ToPoints(int fixedPointValue)
    {
        return fixedPointValue / (float)UnitsPerPoint;
    }

    public static int Clamp(int value, int minimum, int maximum)
    {
        return Mathf.Clamp(value, minimum, maximum);
    }

    public static int NonNegative(int value)
    {
        return Mathf.Max(0, value);
    }

    public static string Format(int fixedPointValue)
    {
        bool isNegative = fixedPointValue < 0;
        int absoluteValue = Mathf.Abs(fixedPointValue);
        int whole = absoluteValue / UnitsPerPoint;
        int fraction = absoluteValue % UnitsPerPoint;

        if (fraction == 0)
        {
            return isNegative ? $"-{whole}" : whole.ToString();
        }

        string fractionText = fraction.ToString("D2").TrimEnd('0');
        if (string.IsNullOrEmpty(fractionText))
        {
            fractionText = "0";
        }

        return isNegative
            ? $"-{whole}.{fractionText}"
            : $"{whole}.{fractionText}";
    }
}

public sealed class ActionEffortModifierSet
{
    public int SharedFlatModifier { get; set; }
    public float SharedMultiplier { get; set; } = 1f;
    public int StaminaFlatModifier { get; set; }
    public float StaminaMultiplier { get; set; } = 1f;
    public int CombatExertionFlatModifier { get; set; }
    public float CombatExertionMultiplier { get; set; } = 1f;

    public static ActionEffortModifierSet None => new ActionEffortModifierSet();
}

public enum ActionCostCommitState
{
    Rejected,
    Committed
}

public sealed class ActionCostResolution
{
    public ActionCostProfile SourceProfile { get; set; }
    public int StaminaCost { get; set; }
    public int CombatExertionCost { get; set; }
    public int ConsumptionCapacityCost { get; set; }
    public bool CombatExertionWasInherited { get; set; }
    public bool CanOverexert { get; set; }
    public ActionEffortModifierSet AppliedModifiers { get; set; } = ActionEffortModifierSet.None;
    public string Source { get; set; } = string.Empty;
    public bool IsRejected { get; set; }
    public string RejectionReason { get; set; } = string.Empty;
}

public sealed class ActionCostCommitResult
{
    public Guid CommitmentId { get; set; }
    public ActionCostCommitState State { get; set; }
    public ActionCostResolution Resolution { get; set; }
    public int StaminaSpent { get; set; }
    public int CombatExertionSpent { get; set; }
    public int ConsumptionCapacitySpent { get; set; }
    public string RejectionReason { get; set; } = string.Empty;

    public bool IsCommitted => State == ActionCostCommitState.Committed;
    public bool IsRejected => State == ActionCostCommitState.Rejected;

    public static ActionCostCommitResult Rejected(Guid commitmentId, ActionCostResolution resolution, string rejectionReason)
    {
        return new ActionCostCommitResult
        {
            CommitmentId = commitmentId,
            State = ActionCostCommitState.Rejected,
            Resolution = resolution,
            RejectionReason = rejectionReason ?? string.Empty
        };
    }

    public static ActionCostCommitResult Committed(Guid commitmentId, ActionCostResolution resolution, int staminaSpent, int combatExertionSpent, int consumptionCapacitySpent = 0)
    {
        return new ActionCostCommitResult
        {
            CommitmentId = commitmentId,
            State = ActionCostCommitState.Committed,
            Resolution = resolution,
            StaminaSpent = staminaSpent,
            CombatExertionSpent = combatExertionSpent,
            ConsumptionCapacitySpent = consumptionCapacitySpent
        };
    }
}

public sealed class ActionCostCommitment
{
    private bool committed;

    public ActionCostCommitment(ActionCostResolution resolution)
    {
        Resolution = resolution;
        CommitmentId = Guid.NewGuid();
    }

    public Guid CommitmentId { get; }
    public ActionCostResolution Resolution { get; }

    public ActionCostCommitResult TryCommit(Character actor, string source)
    {
        if (committed)
        {
            return ActionCostCommitResult.Rejected(CommitmentId, Resolution, "This cost commitment has already been consumed.");
        }

        committed = true;
        return ActionCostProfileResolver.CommitResolvedActionCosts(actor, Resolution, source, CommitmentId);
    }
}
