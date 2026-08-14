using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class StateMachine
{
    public Character Owner { get; private set; }
    public IState CurrentState { get; private set; }

    public StateMachine(Character owner)
    {
        Owner = owner;
        CurrentState = null;
        string characterName = Owner.Name;
        GameDebugger.Instance.LogInfo($"StateMachine initialized for owner {Owner.IInteractableID} ({characterName})");
    }

    public void ChangeState(IState newState, string reason = null)
    {
        string characterName = Owner.Name;
        if (newState == null)
        {
            GameDebugger.Instance.LogWarning($"Changing state for Character {Owner.IInteractableID} ({characterName}) was skipped because the new state was null.");
            return;
        }

        string previousState = CurrentState != null ? CurrentState.GetType().Name : "NULL";
        string nextState = newState.GetType().Name;
        if (CurrentState != null && CurrentState.GetType() == newState.GetType())
        {
            GameDebugger.Instance.LogInfo($"Changing state for Character {Owner.IInteractableID} ({characterName}) was skipped because the state is already {nextState}.");
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "StateMachine.ChangeState no-op",
                $"Previous state: {previousState}\nNew state: {nextState}\nReason: {reason ?? "Same state type"}",
                Owner);
            return;
        }

        GameDebugger.Instance.LogInfo($"Changing state for Character {Owner.IInteractableID} ({characterName})");

        CurrentState?.ExitState(Owner);
        CurrentState = newState;
        CurrentState.EnterState(Owner);

        GameDebugger.Instance.LogInfo($"State changed for Character {Owner.IInteractableID} ({characterName}) to {CurrentState.GetType().Name}");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "StateMachine.ChangeState",
            $"Previous state: {previousState}\nNew state: {CurrentState.GetType().Name}\nReason: {reason ?? "Unspecified"}\nStance: {Owner.Stance}",
            Owner);
    }

    public CharacterDecisionResult Update()
    {
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "StateMachine.Update",
            $"Current state: {CurrentState?.GetType().Name ?? "NULL"}\nStance: {(Owner != null ? Owner.Stance.ToString() : "NULL")}\nTarget: {Owner?.Target?.Name ?? "NULL"}",
            Owner);
        if (Owner == null)
        {
            GameDebugger.Instance.LogError("[StateMachine] UpdateState was called with a NULL owner!");
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("StateMachine.UpdateState null owner", "Reason for no action: owner null");
            return null;
        }

        if (CurrentState == null)
        {
            MovementAIDiagnosticsLogger.LogWarning("StateMachine.Update missing current state",
                $"Owner={Owner.Name} [{Owner.IInteractableID}]\nReason=CurrentState was null");
            return null;
        }

        CharacterDecisionResult result = CurrentState.UpdateState(Owner);
        if (result == null)
        {
            MovementAIDiagnosticsLogger.LogWarning("StateMachine.Update missing state result",
                $"Owner={Owner.Name} [{Owner.IInteractableID}]\nState={CurrentState.GetType().Name}\nReason=State returned null");
            return null;
        }

        if (!result.Resolved)
        {
            MovementAIDiagnosticsLogger.LogWarning("StateMachine.Update unresolved state result",
                $"Owner={Owner.Name} [{Owner.IInteractableID}]\nState={CurrentState.GetType().Name}\nDecisionType={result.DecisionType}\nReason={result.Reason}",
                Owner);
        }

        if (Owner.LastTurnDecisionResult == CharacterTurnDecisionResult.None &&
            result.TurnDecisionResult != CharacterTurnDecisionResult.None)
        {
            Owner.RecordTurnDecision(result.TurnDecisionResult, result.Reason ?? "State returned an explicit decision.");
        }

        return result;
    }

    public void HandleStanceChange(NPCStance stance)
    {
        string characterName = Owner.Name;
        GameDebugger.Instance.LogInfo($"Handling stance change for Character {Owner.IInteractableID} ({characterName}). New Stance: {stance}");

        switch (stance)
        {
            case NPCStance.Hostile:
                ChangeState(new HostileState());
                break;
            case NPCStance.Friendly:
                ChangeState(new FriendlyState());
                break;
            case NPCStance.Following:
                ChangeState(new FollowingState());
                break;
            case NPCStance.Fleeing:
                ChangeState(new FleeingState());
                break;
            case NPCStance.TrueIdle:
                ChangeState(new TrueIdleState());
                break;
            default:
                ChangeState(new IdleState());
                break;
        }
    }
}

public interface IState
{
    void EnterState(Character owner);
    CharacterDecisionResult UpdateState(Character owner);
    void ExitState(Character owner);
}

public class IdleState : IState
{
    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Idling;
        owner.SetCombatParticipationState(CombatParticipationState.Unaware, "Entered Idle state.");
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered Idle state.");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "IdleState.EnterState",
            "State: Idle\nSelected action: None",
            owner);
    }

    public CharacterDecisionResult UpdateState(Character owner)
    {
        if (owner.IInteractableID == PlayerStats.Instance.InteractingWithID && owner.IsPlayerVisible)
        {
            GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) is interacting with the player and will idle.");
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "IdleState.UpdateState no movement",
                "Selected action: None\nReason for no movement: interacting with player and player visible",
                owner);
            CharacterDecisionResult result = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.IntentionalIdle,
                TurnDecisionResult = CharacterTurnDecisionResult.Idled,
                WasAttempted = true,
                WasCommitted = true,
                EndsOpportunity = true,
                Reason = "IdleState ended the turn because the NPC is busy interacting with the player.",
                ActionName = "IntentionalIdle"
            };
            owner.RecordTurnDecision(result.TurnDecisionResult, result.Reason);
            return result;
        }

        CharacterDecisionResult decision = CharacterDecisionResolver.ResolveWorldDecision(owner);

        owner.RecordTurnDecision(
            decision?.TurnDecisionResult ?? CharacterTurnDecisionResult.Idled,
            decision?.Reason ?? "IdleState defaulted to idle because CharacterDecisionResolver returned null.");

        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "IdleState.UpdateState resolved via CharacterDecisionResolver",
            $"DecisionType: {decision?.DecisionType.ToString() ?? "NULL"}\n" +
            $"TurnDecisionResult: {decision?.TurnDecisionResult.ToString() ?? "NULL"}\n" +
            $"CandidateCount: {decision?.CandidateCount.ToString() ?? "NULL"}\n" +
            $"MovementAttempted: {decision?.MovementAttempted.ToString() ?? "NULL"}\n" +
            $"MovementSucceeded: {decision?.MovementSucceeded.ToString() ?? "NULL"}\n" +
            $"RandomWanderSelected: {decision?.RandomWanderSelected.ToString() ?? "NULL"}\n" +
            $"Reason: {decision?.Reason ?? "NULL"}",
            owner);

        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "IdleState.UpdateState resolved",
            $"DecisionResult: {owner.LastTurnDecisionResult}\nDecisionReason: {owner.LastTurnDecisionReason}\nStamina after resolve: {FixedPointResourceMath.Format(owner.CurrentStamina)}\nCombatExertion after resolve: {FixedPointResourceMath.Format(owner.CurrentCombatExertion)}",
            owner);

        return decision;
    }

    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has exited Idle state.");
    }
}


public class HostileState : IState
{
    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Hostile;
        owner.SetCombatParticipationState(CombatParticipationState.Engaged, "Entered Hostile state.");
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered Hostile state. Their target is: {owner.Target?.Name ?? "None"}");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "HostileState.EnterState",
            $"State: Hostile\nTarget: {owner.Target?.Name ?? "NULL"}",
            owner);
    }

    public CharacterDecisionResult UpdateState(Character owner)
    {
        if (owner == null || !owner.IsCombatActorAvailable())
        {
            return new CharacterDecisionResult
            {
                Resolved = false,
                DecisionType = CharacterWorldDecisionType.SkippedCannotAct,
                TurnDecisionResult = CharacterTurnDecisionResult.Skipped,
                Reason = "HostileState skipped because the actor was unavailable.",
                EndsOpportunity = true
            };
        }

        if (!EnsureValidHostileTarget(owner, "HostileState.UpdateState"))
        {
            owner.TryContinueSearchAfterLostTarget("HostileState.UpdateState", out CharacterDecisionResult searchResult);
            if (owner.CombatParticipation != CombatParticipationState.Searching)
            {
                owner.stateMachine.ChangeState(new IdleState(), "Hostile search expired or could not continue.");
            }

            return searchResult;
        }

        if (owner.Target != null && owner.IsTargetInRange(owner.Target))
        {
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "HostileState.UpdateState selected attack",
                $"Selected action: Attack\nTarget: {owner.Target.Name}\nCombatExertion: {FixedPointResourceMath.Format(owner.CurrentCombatExertion)}",
                owner);
            CombatActionResolutionDiagnosticsLogger.LogEvent("[ATTACK ENTRY]", "HostileState.UpdateState initiating shared attack",
                $"ActionName={CombatActionResolutionDiagnosticsLogger.InferActionName(owner, owner.GetMainHandItem() == null ? DamageType.Bludgeoning : owner.GetMainHandItem().DamageType)}\n" +
                $"Target={owner.Target.Name} [{owner.Target.IInteractableID}]\n" +
                $"CombatExertionBefore={FixedPointResourceMath.Format(owner.CurrentCombatExertion)}\n" +
                $"StaminaBefore={FixedPointResourceMath.Format(owner.CurrentStamina)}",
                owner, owner.Target);
            Character targetBeforeAttack = owner.Target;
            bool attackResolved = owner.PerformAttack(targetBeforeAttack);
            if (!attackResolved)
            {
                MovementAIDiagnosticsLogger.LogWarning("HostileState.UpdateState attack failed",
                    $"Selected action: Attack\nTarget: {targetBeforeAttack?.Name ?? "NULL"}\nFailure reason: attack rejected or aborted\nCombatExertion: {FixedPointResourceMath.Format(owner.CurrentCombatExertion)}\nStamina: {FixedPointResourceMath.Format(owner.CurrentStamina)}",
                    owner);
                return new CharacterDecisionResult
                {
                    Resolved = true,
                    DecisionType = CharacterWorldDecisionType.NoActionAvailable,
                    TurnDecisionResult = CharacterTurnDecisionResult.NoActionAvailable,
                    Reason = "Hostile attack was rejected or aborted.",
                    EndsOpportunity = true
                };
            }

            GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) attacked {targetBeforeAttack.Name}. Remaining CombatExertion: {FixedPointResourceMath.Format(owner.CurrentCombatExertion)}");
            CharacterDecisionResult result = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.PerformedAction,
                TurnDecisionResult = CharacterTurnDecisionResult.CombatAction,
                WasAttempted = true,
                WasCommitted = true,
                ChangedWorldState = true,
                MayContinueCombatTurn = owner.IsCombatActorAvailable() && owner.CurrentCombatExertion > 0 && !(TurnOrchestrator.Instance?.HasPendingContextTransition ?? false),
                EndsOpportunity = true,
                ModeTransitionPending = TurnOrchestrator.Instance?.HasPendingContextTransition ?? false,
                Reason = $"HostileState committed attack against {targetBeforeAttack.Name}.",
                ActionName = "Attack"
            };
            owner.RecordTurnDecision(result.TurnDecisionResult, result.Reason);
            return result;
        }

        if (owner.CurrentCombatExertion <= 0)
        {
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "HostileState.UpdateState no movement",
                "Selected action: None\nReason for no movement: no CombatExertion",
                owner);
            CharacterDecisionResult result = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.NoActionAvailable,
                TurnDecisionResult = CharacterTurnDecisionResult.NoActionAvailable,
                Reason = "Hostile actor had no Combat Exertion left.",
                EndsOpportunity = true
            };
            owner.RecordTurnDecision(result.TurnDecisionResult, result.Reason);
            return result;
        }

        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "HostileState.UpdateState selected movement",
            $"Selected action: MoveTowardsTarget\nTarget: {owner.Target.Name}\nTarget cell: {owner.Target.NestedMapPosition}",
            owner);
        Character targetBeforeMove = owner.Target;
        bool moved = owner.MoveTowardsCharacter(targetBeforeMove);
        if (!moved)
        {
            MovementAIDiagnosticsLogger.LogWarning("HostileState.UpdateState movement failed",
                $"Selected action: MoveTowardsTarget\nTarget: {targetBeforeMove.Name}\nFailure reason: blocked or unaffordable movement\nCombatExertion: {FixedPointResourceMath.Format(owner.CurrentCombatExertion)}\nStamina: {FixedPointResourceMath.Format(owner.CurrentStamina)}",
                owner);
            CharacterDecisionResult result = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.FailedMovement,
                TurnDecisionResult = CharacterTurnDecisionResult.FailedMovement,
                Reason = "Hostile movement was blocked or unaffordable.",
                EndsOpportunity = true
            };
            owner.RecordTurnDecision(result.TurnDecisionResult, result.Reason);
            return result;
        }

        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) moved towards {targetBeforeMove.Name}. Remaining CombatExertion: {FixedPointResourceMath.Format(owner.CurrentCombatExertion)}");
        CharacterDecisionResult movedResult = new CharacterDecisionResult
        {
            Resolved = true,
            DecisionType = CharacterWorldDecisionType.MoveTowardsCandidate,
            TurnDecisionResult = CharacterTurnDecisionResult.Moved,
            WasAttempted = true,
            WasCommitted = true,
            PositionChanged = true,
            ChangedWorldState = true,
            MayContinueCombatTurn = owner.IsCombatActorAvailable() && owner.CurrentCombatExertion > 0 && !(TurnOrchestrator.Instance?.HasPendingContextTransition ?? false),
            EndsOpportunity = true,
            ModeTransitionPending = TurnOrchestrator.Instance?.HasPendingContextTransition ?? false,
            Reason = $"HostileState moved toward {targetBeforeMove.Name}.",
            ActionName = "MoveTowardsTarget"
        };
        owner.RecordTurnDecision(movedResult.TurnDecisionResult, movedResult.Reason);
        return movedResult;
    }

    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has exited Hostile state.");
    }

    private bool EnsureValidHostileTarget(Character owner, string source)
    {
        if (owner.IsValidCombatTarget(owner.Target))
        {
            return true;
        }

        if (owner.TryRefreshCombatTarget($"{source}: current target invalid", out Character replacementTarget))
        {
            owner.SetCombatParticipationState(CombatParticipationState.Engaged, $"{source}: reacquired hostile target.");
            CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT TARGET]", "HostileState.UpdateState reacquired valid hostile target",
                $"Source={source}\n" +
                $"NewTarget={replacementTarget?.Name ?? "NULL"} [{replacementTarget?.IInteractableID.ToString() ?? "NULL"}]",
                owner, replacementTarget);
            return true;
        }

        owner.ClearCombatTarget($"{source}: no valid hostile target");
        if (owner.CombatParticipation != CombatParticipationState.Searching)
        {
            owner.BeginCombatSearch(owner.LastKnownCombatOpponent, $"{source}: no valid hostile target was available.");
        }
        else
        {
            owner.RememberCombatOpponent(owner.LastKnownCombatOpponent ?? owner.Target, $"{source}: preserved ongoing search.");
        }
        CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT TARGET]", "HostileState.UpdateState retained hostility without target",
            $"Source={source}\n" +
            $"Actor={owner.Name} [{owner.IInteractableID}]\n" +
            $"IsHostile={owner.IsHostile}\n" +
            $"Stance={owner.Stance}\n" +
            $"InCombat={owner.InCombat}",
            owner);
        return false;
    }
}

public class FriendlyState : IState
{
    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Idling; // Friendly but idle
        owner.SetCombatParticipationState(CombatParticipationState.Uninvolved, "Entered Friendly state.");
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered Friendly state.");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "FriendlyState.EnterState",
            "State: Friendly",
            owner);
    }

    public CharacterDecisionResult UpdateState(Character owner)
    {
        CharacterDecisionResult result = CharacterDecisionResolver.ResolveWorldDecision(owner);
        if (result == null)
        {
            result = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.IntentionalIdle,
                TurnDecisionResult = CharacterTurnDecisionResult.Idled,
                Reason = "Friendly state had no decision result; intentionally idled.",
                EndsOpportunity = true,
                WasAttempted = true,
                WasCommitted = true,
                ActionName = "IntentionalIdle"
            };
        }

        owner.RecordTurnDecision(result.TurnDecisionResult, result.Reason);
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "FriendlyState.UpdateState resolved",
            $"Selected action: {result.ActionName ?? result.DecisionType.ToString()}\nReason: {result.Reason}",
            owner);
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) friendly state resolved with {result.DecisionType}.");
        return result;
    }

    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has exited Friendly state.");
    }
}

public class FleeingState : IState
{
    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Fleeing;
        owner.SetCombatParticipationState(CombatParticipationState.Fleeing, "Entered Fleeing state.");
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered Fleeing state.");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "FleeingState.EnterState",
            "State: Fleeing",
            owner);
    }

    public CharacterDecisionResult UpdateState(Character owner)
    {
        Character threatSource = owner.Target ?? owner.FollowTarget ?? PlayerStats.Instance?.CurrentPlayerCharacter;
        if (threatSource == null)
        {
            CharacterDecisionResult noThreatResult = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.IntentionalIdle,
                TurnDecisionResult = CharacterTurnDecisionResult.Idled,
                Reason = "Fleeing state had no valid threat source.",
                EndsOpportunity = true,
                WasAttempted = true,
                WasCommitted = true,
                ActionName = "IntentionalIdle"
            };
            owner.RecordTurnDecision(noThreatResult.TurnDecisionResult, noThreatResult.Reason);
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "FleeingState.UpdateState no threat",
                "Selected action: None\nReason for no movement: no valid threat source",
                owner);
            if (owner.CombatParticipation != CombatParticipationState.Fleeing)
            {
                owner.stateMachine.ChangeState(new IdleState(), "Fleeing had no threat source and exited.");
            }
            return noThreatResult;
        }

        if (owner.TryContinueFleeAfterDanger("FleeingState.UpdateState", out CharacterDecisionResult fleeResult))
        {
            owner.RecordTurnDecision(fleeResult.TurnDecisionResult, fleeResult.Reason);
            return fleeResult;
        }

        if (owner.CombatParticipation != CombatParticipationState.Fleeing)
        {
            owner.stateMachine.ChangeState(new IdleState(), "Fleeing completed or expired.");
        }

        if (fleeResult == null)
        {
            fleeResult = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.NoActionAvailable,
                TurnDecisionResult = CharacterTurnDecisionResult.NoActionAvailable,
                Reason = "Fleeing state ended without a valid flee result.",
                EndsOpportunity = true
            };
        }

        owner.RecordTurnDecision(fleeResult.TurnDecisionResult, fleeResult.Reason);
        return fleeResult;
    }

    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has exited Fleeing state.");
    }
}

public class FollowingState : IState
{
    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Following;
        owner.SetCombatParticipationState(CombatParticipationState.Aware, "Entered Following state.");
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered Following state.");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "FollowingState.EnterState",
            "State: Following",
            owner);
    }

    public CharacterDecisionResult UpdateState(Character owner)
    {
        Character followTarget = owner.FollowTarget ?? PlayerStats.Instance?.CurrentPlayerCharacter;
        if (followTarget == null)
        {
            CharacterDecisionResult noTargetResult = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.IntentionalIdle,
                TurnDecisionResult = CharacterTurnDecisionResult.Idled,
                Reason = "Following state had no valid follow target.",
                EndsOpportunity = true,
                WasAttempted = true,
                WasCommitted = true,
                ActionName = "IntentionalIdle"
            };
            owner.RecordTurnDecision(noTargetResult.TurnDecisionResult, noTargetResult.Reason);
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "FollowingState.UpdateState no follow target",
                "Selected action: None\nReason for no movement: no valid follow target",
                owner);
            return noTargetResult;
        }

        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "FollowingState.UpdateState selected movement",
            $"Selected action: MoveTowardsCharacter\nFollowTarget: {followTarget.Name}",
            owner);
        bool moved = owner.MoveTowardsCharacter(followTarget);
        CharacterDecisionResult result = new CharacterDecisionResult
        {
            Resolved = true,
            DecisionType = moved ? CharacterWorldDecisionType.MoveTowardsCandidate : CharacterWorldDecisionType.FailedMovement,
            TurnDecisionResult = moved ? CharacterTurnDecisionResult.Moved : CharacterTurnDecisionResult.FailedMovement,
            WasAttempted = true,
            WasCommitted = moved,
            PositionChanged = moved,
            ChangedWorldState = moved,
            Reason = moved
                ? $"Moved towards {followTarget.Name}."
                : $"Could not move towards {followTarget.Name}.",
            EndsOpportunity = true,
            ActionName = "MoveTowardsCharacter"
        };

        if (!moved)
        {
            MovementAIDiagnosticsLogger.LogWarning("FollowingState.UpdateState movement failed",
                $"Selected action: MoveTowardsCharacter\nFollowTarget: {followTarget.Name}\nFailure reason: blocked or unaffordable movement",
                owner);
        }
        else
        {
            GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) moved towards {followTarget.Name}. Remaining CombatExertion: {FixedPointResourceMath.Format(owner.CurrentCombatExertion)}");
        }

        owner.RecordTurnDecision(result.TurnDecisionResult, result.Reason);
        return result;
    }

    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has exited Following state.");
    }
}

public class TrueIdleState : IState
{
    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.TrueIdle;
        owner.SetCombatParticipationState(CombatParticipationState.Uninvolved, "Entered TrueIdle state.");
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered TrueIdle state.");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "TrueIdleState.EnterState",
            "State: TrueIdle",
            owner);
    }

    public CharacterDecisionResult UpdateState(Character owner)
    {
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "TrueIdleState.UpdateState no movement",
            "Selected action: None\nReason for no movement: TrueIdle intentionally idles without resource spend.",
            owner);
        CharacterDecisionResult result = new CharacterDecisionResult
        {
            Resolved = true,
            DecisionType = CharacterWorldDecisionType.IntentionalIdle,
            TurnDecisionResult = CharacterTurnDecisionResult.Idled,
            WasAttempted = true,
            WasCommitted = true,
            EndsOpportunity = true,
            Reason = "TrueIdle state consumed the entire NPC turn.",
            ActionName = "IntentionalIdle"
        };
        owner.RecordTurnDecision(result.TurnDecisionResult, result.Reason);
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) remains idle without resource spend.");
        return result;
    }

    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has exited TrueIdle state.");
    }
}

#region Monster States

public class MonsterAggroState : IState
{
    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Hostile;
        owner.SetCombatParticipationState(CombatParticipationState.Engaged, "Entered MonsterAggro state.");
        GameDebugger.Instance.LogInfo($"Monster {owner.IInteractableID} ({owner.Name}) is now aggressive!");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterAggroState.EnterState",
            $"State: MonsterAggro\nTarget: {owner.Target?.Name ?? "NULL"}",
            owner);
    }

    public CharacterDecisionResult UpdateState(Character owner)
    {
        Monster monster = owner as Monster;
        if (monster == null || monster.Target == null)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("MonsterAggroState.UpdateState no action",
                $"Selected action: None\nReason for no movement: monster null or target null\nTarget: {owner?.Target?.Name ?? "NULL"}",
                owner);
            CharacterDecisionResult noAction = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.NoActionAvailable,
                TurnDecisionResult = CharacterTurnDecisionResult.NoActionAvailable,
                Reason = "MonsterAggroState had no target.",
                EndsOpportunity = true
            };
            owner.TryContinueSearchAfterLostTarget("MonsterAggroState had no target", out CharacterDecisionResult searchResult);
            if (owner.CombatParticipation != CombatParticipationState.Searching)
            {
                owner.stateMachine.ChangeState(new MonsterIdleState(), "Monster aggro search expired or could not continue.");
            }
            else
            {
                noAction = searchResult;
            }
            owner.RecordTurnDecision(noAction.TurnDecisionResult, noAction.Reason);
            return noAction;
        }

        if (monster.IsTargetInRange(monster.Target))
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterAggroState.UpdateState selected attack",
                $"Selected action: Attack\nTarget: {monster.Target.Name}",
                monster);
            CombatActionResolutionDiagnosticsLogger.LogEvent("[ATTACK ENTRY]", "MonsterAggroState.UpdateState initiating shared attack",
                $"ActionName={CombatActionResolutionDiagnosticsLogger.InferActionName(monster, monster.GetMainHandItem() == null ? DamageType.Bludgeoning : monster.GetMainHandItem().DamageType)}\n" +
                $"Target={monster.Target.Name} [{monster.Target.IInteractableID}]\n" +
                $"TypedCombatAuthority=Character.PerformAttack",
                monster, monster.Target);
            bool attackResolved = monster.PerformAttack(monster.Target);
            if (!attackResolved)
            {
                MovementAIDiagnosticsLogger.LogWarning("MonsterAggroState.UpdateState attack failed",
                    $"Selected action: Attack\nTarget: {monster.Target.Name}\nFailure reason: attack rejected or aborted",
                    monster);
                CharacterDecisionResult failed = new CharacterDecisionResult
                {
                    Resolved = true,
                    DecisionType = CharacterWorldDecisionType.NoActionAvailable,
                    TurnDecisionResult = CharacterTurnDecisionResult.NoActionAvailable,
                    Reason = "MonsterAggroState attack was rejected or aborted.",
                    EndsOpportunity = true
                };
                owner.RecordTurnDecision(failed.TurnDecisionResult, failed.Reason);
                return failed;
            }

            CharacterDecisionResult result = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.PerformedAction,
                TurnDecisionResult = CharacterTurnDecisionResult.CombatAction,
                WasAttempted = true,
                WasCommitted = true,
                ChangedWorldState = true,
                MayContinueCombatTurn = monster.IsCombatActorAvailable() && monster.CurrentCombatExertion > 0 && !(TurnOrchestrator.Instance?.HasPendingContextTransition ?? false),
                EndsOpportunity = true,
                ModeTransitionPending = TurnOrchestrator.Instance?.HasPendingContextTransition ?? false,
                Reason = $"Monster attacked {monster.Target.Name}.",
                ActionName = "Attack"
            };
            owner.RecordTurnDecision(result.TurnDecisionResult, result.Reason);
            return result;
        }

        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterAggroState.UpdateState selected movement",
            $"Selected action: MoveTowardsTarget\nTarget: {monster.Target.Name}\nTarget cell: {monster.Target.NestedMapPosition}",
            monster);
        bool moved = monster.MoveTowardsCharacter(monster.Target);
        if (!moved)
        {
            MovementAIDiagnosticsLogger.LogWarning("MonsterAggroState.UpdateState movement failed",
                $"Selected action: MoveTowardsTarget\nTarget: {monster.Target.Name}\nFailure reason: blocked or unaffordable movement",
                monster);
            CharacterDecisionResult failedMove = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.FailedMovement,
                TurnDecisionResult = CharacterTurnDecisionResult.FailedMovement,
                Reason = "MonsterAggroState movement was blocked or unaffordable.",
                EndsOpportunity = true
            };
            owner.RecordTurnDecision(failedMove.TurnDecisionResult, failedMove.Reason);
            return failedMove;
        }

        CharacterDecisionResult movedResult = new CharacterDecisionResult
        {
            Resolved = true,
            DecisionType = CharacterWorldDecisionType.MoveTowardsCandidate,
            TurnDecisionResult = CharacterTurnDecisionResult.Moved,
            WasAttempted = true,
            WasCommitted = true,
            PositionChanged = true,
            ChangedWorldState = true,
            MayContinueCombatTurn = monster.IsCombatActorAvailable() && monster.CurrentCombatExertion > 0 && !(TurnOrchestrator.Instance?.HasPendingContextTransition ?? false),
            EndsOpportunity = true,
            ModeTransitionPending = TurnOrchestrator.Instance?.HasPendingContextTransition ?? false,
            Reason = $"Monster moved towards {monster.Target.Name}.",
            ActionName = "MoveTowardsTarget"
        };
        owner.RecordTurnDecision(movedResult.TurnDecisionResult, movedResult.Reason);
        return movedResult;
    }

    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"Monster {owner.IInteractableID} ({owner.Name}) exited Aggro state.");
    }
}

public class MonsterChaseState : IState
{
    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Chasing;
        owner.SetCombatParticipationState(CombatParticipationState.Engaged, "Entered MonsterChase state.");
        GameDebugger.Instance.LogInfo($"Monster {owner.IInteractableID} ({owner.Name}) is chasing its target!");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterChaseState.EnterState",
            $"State: MonsterChase\nTarget: {owner.Target?.Name ?? "NULL"}",
            owner);
    }

    public CharacterDecisionResult UpdateState(Character owner)
    {
        Monster monster = owner as Monster;
        if (monster == null || monster.Target == null)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("MonsterChaseState.UpdateState no action",
                $"Selected action: None\nReason for no movement: monster null or target null\nTarget: {owner?.Target?.Name ?? "NULL"}",
                owner);
            CharacterDecisionResult noAction = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.NoActionAvailable,
                TurnDecisionResult = CharacterTurnDecisionResult.NoActionAvailable,
                Reason = "MonsterChaseState had no target.",
                EndsOpportunity = true
            };
            owner.TryContinueSearchAfterLostTarget("MonsterChaseState had no target", out CharacterDecisionResult searchResult);
            if (owner.CombatParticipation != CombatParticipationState.Searching)
            {
                owner.stateMachine.ChangeState(new MonsterIdleState(), "Monster chase search expired or could not continue.");
            }
            else
            {
                noAction = searchResult;
            }
            owner.RecordTurnDecision(noAction.TurnDecisionResult, noAction.Reason);
            return noAction;
        }

        if (monster.Target == null || !monster.Target.IsAlive || !monster.CanSeeTarget(monster.Target))
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterChaseState.UpdateState lost target",
                $"Selected action: ChangeState\nReason for no movement: target invalid, dead, or not visible\nTarget: {monster.Target?.Name ?? "NULL"}",
                monster);
            monster.stateMachine.ChangeState(new MonsterIdleState());
            CharacterDecisionResult lostTarget = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.NoActionAvailable,
                TurnDecisionResult = CharacterTurnDecisionResult.NoActionAvailable,
                Reason = "MonsterChaseState lost visibility on the target and returned to idle.",
                EndsOpportunity = true
            };
            owner.TryContinueSearchAfterLostTarget("MonsterChaseState lost target visibility", out CharacterDecisionResult searchResult);
            if (owner.CombatParticipation != CombatParticipationState.Searching)
            {
                monster.stateMachine.ChangeState(new MonsterIdleState(), "Monster chase search expired or could not continue.");
            }
            else
            {
                lostTarget = searchResult;
            }
            owner.RecordTurnDecision(lostTarget.TurnDecisionResult, lostTarget.Reason);
            return lostTarget;
        }

        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterChaseState.UpdateState selected movement",
            $"Selected action: MoveTowardsTarget\nTarget: {monster.Target.Name}\nTarget cell: {monster.Target.NestedMapPosition}",
            monster);
        bool moved = monster.MoveTowardsCharacter(monster.Target);
        if (!moved)
        {
            MovementAIDiagnosticsLogger.LogWarning("MonsterChaseState.UpdateState movement failed",
                $"Selected action: MoveTowardsTarget\nTarget: {monster.Target.Name}\nFailure reason: blocked or unaffordable movement",
                monster);
            CharacterDecisionResult failedMove = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.FailedMovement,
                TurnDecisionResult = CharacterTurnDecisionResult.FailedMovement,
                Reason = "MonsterChaseState movement was blocked or unaffordable.",
                EndsOpportunity = true
            };
            owner.RecordTurnDecision(failedMove.TurnDecisionResult, failedMove.Reason);
            return failedMove;
        }

        CharacterDecisionResult result = new CharacterDecisionResult
        {
            Resolved = true,
            DecisionType = CharacterWorldDecisionType.MoveTowardsCandidate,
            TurnDecisionResult = CharacterTurnDecisionResult.Moved,
            WasAttempted = true,
            WasCommitted = true,
            PositionChanged = true,
            ChangedWorldState = true,
            Reason = $"Monster moved towards {monster.Target.Name}.",
            EndsOpportunity = true,
            ActionName = "MoveTowardsTarget"
        };
        owner.RecordTurnDecision(result.TurnDecisionResult, result.Reason);
        return result;
    }

    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"Monster {owner.IInteractableID} ({owner.Name}) exited Chase state.");
    }
}

public class MonsterFleeState : IState
{
    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Fleeing;
        owner.SetCombatParticipationState(CombatParticipationState.Fleeing, "Entered MonsterFlee state.");
        GameDebugger.Instance.LogInfo($"Monster {owner.IInteractableID} ({owner.Name}) is fleeing!");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterFleeState.EnterState",
            $"State: MonsterFlee\nTarget: {owner.Target?.Name ?? "NULL"}",
            owner);
    }

    public CharacterDecisionResult UpdateState(Character owner)
    {
        Monster monster = owner as Monster;
        if (monster == null)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("MonsterFleeState.UpdateState no action",
                "Selected action: None\nReason for no movement: owner is not Monster",
                owner);
            CharacterDecisionResult noAction = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.NoActionAvailable,
                TurnDecisionResult = CharacterTurnDecisionResult.NoActionAvailable,
                Reason = "MonsterFleeState owner was not a Monster.",
                EndsOpportunity = true
            };
            owner.SetCombatParticipationState(CombatParticipationState.Uninvolved, "MonsterFleeState owner was not a Monster.");
            owner.RecordTurnDecision(noAction.TurnDecisionResult, noAction.Reason);
            return noAction;
        }

        if (monster.Health > (monster.MaxHealth * 0.3)) // Stop fleeing if health recovers
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterFleeState.UpdateState recovered",
                "Selected action: ChangeState\nReason for no movement: health recovered\nNew state: MonsterIdleState",
                monster);
            monster.stateMachine.ChangeState(new MonsterIdleState(), "Monster recovered while fleeing.");
            CharacterDecisionResult recovered = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.NoActionAvailable,
                TurnDecisionResult = CharacterTurnDecisionResult.NoActionAvailable,
                Reason = "Monster recovered and changed back to idle.",
                EndsOpportunity = true
            };
            owner.SetCombatParticipationState(CombatParticipationState.Uninvolved, "MonsterFleeState recovered.");
            owner.RecordTurnDecision(recovered.TurnDecisionResult, recovered.Reason);
            return recovered;
        }

        if (monster.Target != null)
        {
            if (owner.TryContinueFleeAfterDanger("MonsterFleeState.UpdateState", out CharacterDecisionResult fleeResult))
            {
                owner.RecordTurnDecision(fleeResult.TurnDecisionResult, fleeResult.Reason);
                return fleeResult;
            }

            if (owner.CombatParticipation != CombatParticipationState.Fleeing)
            {
                monster.stateMachine.ChangeState(new MonsterIdleState(), "Monster flee completed or expired.");
            }

            owner.RecordTurnDecision(fleeResult.TurnDecisionResult, fleeResult.Reason);
            return fleeResult;
        }
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterFleeState.UpdateState no target",
            "Selected action: None\nReason for no movement: target null",
            monster);
        CharacterDecisionResult noTarget = new CharacterDecisionResult
        {
            Resolved = true,
            DecisionType = CharacterWorldDecisionType.IntentionalIdle,
            TurnDecisionResult = CharacterTurnDecisionResult.Idled,
            Reason = "MonsterFleeState had no target.",
            EndsOpportunity = true
        };
        owner.SetCombatParticipationState(CombatParticipationState.Uninvolved, "MonsterFleeState had no target.");
        monster.stateMachine.ChangeState(new MonsterIdleState(), "Monster flee ended without a target.");
        owner.RecordTurnDecision(noTarget.TurnDecisionResult, noTarget.Reason);
        return noTarget;
    }

    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"Monster {owner.IInteractableID} ({owner.Name}) stopped fleeing.");
    }
}

public class MonsterIdleState : IState
{
    private const int PatrolIntervalTurns = 1;
    private int turnsSinceLastPatrol = PatrolIntervalTurns;

    public void EnterState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"{owner.Name} has entered Monster Idle state.");
        owner.SetCombatParticipationState(CombatParticipationState.Unaware, "Entered MonsterIdle state.");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterIdleState.EnterState",
            "State: MonsterIdle",
            owner);
    }

    public CharacterDecisionResult UpdateState(Character owner)
    {
        Monster monster = owner as Monster;
        if (monster == null)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("MonsterIdleState.UpdateState no action",
                "Selected action: None\nReason for no movement: owner is not Monster",
                owner);
            CharacterDecisionResult noAction = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.NoActionAvailable,
                TurnDecisionResult = CharacterTurnDecisionResult.NoActionAvailable,
                Reason = "MonsterIdleState owner was not a Monster.",
                EndsOpportunity = true
            };
            owner.SetCombatParticipationState(CombatParticipationState.Uninvolved, "MonsterIdleState owner was not a Monster.");
            owner.RecordTurnDecision(noAction.TurnDecisionResult, noAction.Reason);
            return noAction;
        }

        turnsSinceLastPatrol++;

        if (turnsSinceLastPatrol >= PatrolIntervalTurns)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterIdleState.UpdateState selected patrol movement",
                $"Selected action: MoveRandom\nPatrolIntervalTurns: {PatrolIntervalTurns}\nTurnsSinceLastPatrol: {turnsSinceLastPatrol}",
                monster);
            bool moved = monster.MoveInRandomDirection();
            if (!moved)
            {
                MovementAIDiagnosticsLogger.LogWarning("MonsterIdleState.UpdateState patrol movement failed",
                    "Selected action: MoveRandom\nFailure reason: blocked or unavailable movement",
                    monster);
            }
            else
            {
                GameDebugger.Instance.LogInfo($"{monster.Name} is patrolling.");
            }
            turnsSinceLastPatrol = 0;

            CharacterDecisionResult patrolResult = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = moved ? CharacterWorldDecisionType.WanderFallback : CharacterWorldDecisionType.IntentionalIdle,
                TurnDecisionResult = moved ? CharacterTurnDecisionResult.Moved : CharacterTurnDecisionResult.Idled,
                WasAttempted = true,
                WasCommitted = moved,
                PositionChanged = moved,
                ChangedWorldState = moved,
                Reason = moved ? "Monster patrolled one step." : "Monster attempted to patrol but no valid move existed.",
                EndsOpportunity = true,
                ActionName = moved ? "RandomPatrol" : "IntentionalIdle"
            };
            owner.SetCombatParticipationState(CombatParticipationState.Unaware, "MonsterIdleState patrol result.");
            owner.RecordTurnDecision(patrolResult.TurnDecisionResult, patrolResult.Reason);
            if (monster.IsPlayerVisible)
            {
                monster.Target = monster.FindClosestEnemy();
                // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
                MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterIdleState.UpdateState player visible",
                    $"Selected action: ChangeState\nNew state: MonsterAggroState\nTarget: {monster.Target?.Name ?? "NULL"}",
                    monster);
                monster.stateMachine.ChangeState(new MonsterAggroState(), "Player became visible to monster.");
            }
            return patrolResult;
        }
        else
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterIdleState.UpdateState waiting",
                $"Selected action: None\nReason for no movement: patrol interval not yet reached\nPatrolIntervalTurns: {PatrolIntervalTurns}\nTurnsSinceLastPatrol: {turnsSinceLastPatrol}",
                monster);
            CharacterDecisionResult waitResult = new CharacterDecisionResult
            {
                Resolved = true,
                DecisionType = CharacterWorldDecisionType.IntentionalIdle,
                TurnDecisionResult = CharacterTurnDecisionResult.Idled,
                Reason = "Monster idle turn waited for patrol interval.",
                EndsOpportunity = true
            };
            owner.SetCombatParticipationState(CombatParticipationState.Unaware, "MonsterIdleState waiting.");
            owner.RecordTurnDecision(waitResult.TurnDecisionResult, waitResult.Reason);
            if (monster.IsPlayerVisible)
            {
                monster.Target = monster.FindClosestEnemy();
                // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
                MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterIdleState.UpdateState player visible",
                    $"Selected action: ChangeState\nNew state: MonsterAggroState\nTarget: {monster.Target?.Name ?? "NULL"}",
                    monster);
                monster.stateMachine.ChangeState(new MonsterAggroState(), "Player became visible to monster.");
            }
            return waitResult;
        }
    }


    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"{owner.Name} is leaving Monster Idle state.");
    }
}

#endregion
