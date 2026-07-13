using System;
using UnityEngine;

public sealed class ActionCostProfile
{
    public bool IsFree { get; set; }
    public int WorldTimeCost { get; set; }
    public int LegacyActionPointCost { get; set; }
    public int LegacyMovePointCost { get; set; }
    public bool EndsPlayerTurn { get; set; }
    public bool CandidateForFutureStamina { get; set; }
    public int PredictedStaminaCost { get; set; } = ActionCostProfileResolver.UnknownPredictedStaminaCost;
    public bool IsContextual { get; set; }
    public string CostLabel { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public bool HasPredictedStaminaCost => PredictedStaminaCost >= 0;

    public string GetPredictedStaminaCostText()
    {
        return HasPredictedStaminaCost ? PredictedStaminaCost.ToString() : "Unknown";
    }
}

public static class ActionCostProfileResolver
{
    public const int UnknownPredictedStaminaCost = -1;

    private const string DiagnosticsTag = "CODEXLOG007_ACTION_COST_PROFILE";
    private const int DefaultMovementWorldTimeCost = 1;
    private const int DefaultMovementLegacyMovePointCost = 1;
    private const int DefaultMovementPredictedStaminaCost = 1;
    private const int DefaultPhysicalAttackPredictedStaminaCost = 4;
    private const int ModerateWorkPredictedStaminaCost = 4;
    private const int HeavyWorkPredictedStaminaCost = 6;
    private const int VeryHeavyWorkPredictedStaminaCost = 8;

    #region Builders

    public static ActionCostProfile BuildForMovement(bool isCombatContext)
    {
        return new ActionCostProfile
        {
            IsFree = false,
            WorldTimeCost = isCombatContext ? 0 : DefaultMovementWorldTimeCost,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = DefaultMovementLegacyMovePointCost,
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
            IsFree = false,
            WorldTimeCost = isCombatContext ? 0 : 1,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
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

    public static ActionCostProfile BuildForInteraction(IInteraction interaction, bool isCombatContext)
    {
        if (interaction == null)
        {
            return BuildUnknownProfile("Interaction was null.");
        }

        string normalizedActionName = NormalizeActionName(interaction.Name);
        int rawCost = ClampNonNegative(interaction.ActionPointCost);

        if (IsFreeInformationalInteraction(interaction, normalizedActionName))
        {
            return new ActionCostProfile
            {
                IsFree = true,
                WorldTimeCost = 0,
                LegacyActionPointCost = 0,
                LegacyMovePointCost = 0,
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
                IsFree = false,
                WorldTimeCost = 0,
                LegacyActionPointCost = rawCost > 0 ? rawCost : CombatResolver.DefaultPhysicalAttackActionPointCost,
                LegacyMovePointCost = 0,
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
                IsFree = false,
                WorldTimeCost = 0,
                LegacyActionPointCost = rawCost,
                LegacyMovePointCost = 0,
                EndsPlayerTurn = false,
                CandidateForFutureStamina = false,
                PredictedStaminaCost = UnknownPredictedStaminaCost,
                IsContextual = true,
                CostLabel = rawCost > 0 ? FormatActionPointLabel(rawCost) : string.Empty,
                Notes = "Magic action remains AP-backed today. Future stamina use is intentionally left uncertain in this pass."
            };
        }

        if (IsContextualZeroCostInteraction(normalizedActionName, rawCost))
        {
            return new ActionCostProfile
            {
                IsFree = false,
                WorldTimeCost = 0,
                LegacyActionPointCost = rawCost,
                LegacyMovePointCost = 0,
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
                IsFree = false,
                WorldTimeCost = interaction.Type == InteractionType.Combat ? 0 : rawCost,
                LegacyActionPointCost = rawCost,
                LegacyMovePointCost = 0,
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

        if (IsFreeEnvironmentalInformation(normalizedActionName))
        {
            return new ActionCostProfile
            {
                IsFree = true,
                WorldTimeCost = 0,
                LegacyActionPointCost = 0,
                LegacyMovePointCost = 0,
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
                IsFree = false,
                WorldTimeCost = 0,
                LegacyActionPointCost = rawCost,
                LegacyMovePointCost = 0,
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
            IsFree = false,
            WorldTimeCost = rawCost,
            LegacyActionPointCost = rawCost,
            LegacyMovePointCost = 0,
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
            IsFree = false,
            WorldTimeCost = 0,
            LegacyActionPointCost = ClampNonNegative(context.ActionPointCost),
            LegacyMovePointCost = 0,
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
            $"{DiagnosticsTag} Source={source} Actor={actorName} Action={safeActionName} IsFree={profile.IsFree} WorldTimeCost={profile.WorldTimeCost} LegacyActionPointCost={profile.LegacyActionPointCost} LegacyMovePointCost={profile.LegacyMovePointCost} EndsPlayerTurn={profile.EndsPlayerTurn} CandidateForFutureStamina={profile.CandidateForFutureStamina} PredictedStaminaCost={profile.GetPredictedStaminaCostText()} IsContextual={profile.IsContextual} CostLabel={profile.CostLabel ?? string.Empty} Notes={profile.Notes} PredictedStaminaCost only; not enforced.");
    }

    #endregion

    #region Classification Helpers

    private static ActionCostProfile BuildUnknownProfile(string notes)
    {
        return new ActionCostProfile
        {
            IsFree = false,
            WorldTimeCost = 0,
            LegacyActionPointCost = 0,
            LegacyMovePointCost = 0,
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

    #endregion
}
