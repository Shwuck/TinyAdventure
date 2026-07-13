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

    public void ChangeState(IState newState)
    {
        string characterName = Owner.Name;
        GameDebugger.Instance.LogInfo($"Changing state for Character {Owner.IInteractableID} ({characterName})");
        string previousState = CurrentState != null ? CurrentState.GetType().Name : "NULL";

        CurrentState?.ExitState(Owner);
        CurrentState = newState;
        CurrentState.EnterState(Owner);

        GameDebugger.Instance.LogInfo($"State changed for Character {Owner.IInteractableID} ({characterName}) to {CurrentState.GetType().Name}");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "StateMachine.ChangeState",
            $"Previous state: {previousState}\nNew state: {CurrentState.GetType().Name}\nStance: {Owner.Stance}",
            Owner);
    }

    public void Update()
    {
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "StateMachine.Update",
            $"Current state: {CurrentState?.GetType().Name ?? "NULL"}\nStance: {(Owner != null ? Owner.Stance.ToString() : "NULL")}\nTarget: {Owner?.Target?.Name ?? "NULL"}",
            Owner);
        CurrentState?.UpdateState(Owner);
    }

    public void UpdateState(Character owner)
    {
        if (owner == null)
        {
            GameDebugger.Instance.LogError("[StateMachine] UpdateState was called with a NULL owner!");
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("StateMachine.UpdateState null owner", "Reason for no action: owner null");
            return;
        }

        GameDebugger.Instance.LogInfo($"[StateMachine] {owner.Name} updating state. Stance: {owner.Stance}, Target: {owner.Target?.Name ?? "None"}, AP: {owner.ActionPoints}, MP: {owner.MovePoints}");

        // Check if the character has a valid target
        if (owner.Target == null || !owner.Target.IsAlive)
        {
            GameDebugger.Instance.LogInfo($"[StateMachine] {owner.Name} has no valid target. Returning to Idle.");
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "StateMachine.UpdateState no valid target",
                $"Selected action: ChangeState\nReason for no movement: target null or not alive\nTarget: {owner.Target?.Name ?? "NULL"}",
                owner);
            owner.stateMachine.ChangeState(new IdleState());
            return;
        }

        // Ensure the character is actually hostile before attacking
        if (owner.Stance == NPCStance.Hostile)
        {
            Vector2Int ownerPosition = owner.NestedMapPosition;
            Vector2Int targetPosition = owner.Target.NestedMapPosition;
            int distanceToTarget = Mathf.Abs(ownerPosition.x - targetPosition.x) + Mathf.Abs(ownerPosition.y - targetPosition.y);

            GameDebugger.Instance.LogInfo($"[StateMachine] {owner.Name} is at {ownerPosition}, Target {owner.Target.Name} is at {targetPosition}. Distance: {distanceToTarget}");

            if (owner.IsTargetInRange(owner.Target))
            {
                GameDebugger.Instance.LogInfo($"[StateMachine] {owner.Name} is ATTACKING {owner.Target.Name} at {targetPosition}!");
                // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
                MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "StateMachine.UpdateState selected attack",
                    $"Selected action: Attack\nTarget: {owner.Target.Name}\nTarget cell: {targetPosition}",
                    owner);
                owner.PerformAttack(owner.Target);

                if (owner.ActionPoints <= 0)
                {
                    GameDebugger.Instance.LogInfo($"[StateMachine] {owner.Name} has no more AP after attacking. Ending turn.");
                    return;
                }
            }
            else
            {
                if (owner.MovePoints > 0)
                {
                    GameDebugger.Instance.LogInfo($"[StateMachine] {owner.Name} is MOVING toward {owner.Target.Name} from {ownerPosition} to {targetPosition}.");
                    // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
                    MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "StateMachine.UpdateState selected movement",
                        $"Selected action: MoveTowardsTarget\nTarget: {owner.Target.Name}\nTarget cell: {targetPosition}",
                        owner);
                    owner.MoveTowardsCharacter(owner.Target);
                    owner.MovePoints--;

                    Vector2Int newPosition = owner.NestedMapPosition;
                    int newDistanceToTarget = Mathf.Abs(newPosition.x - targetPosition.x) + Mathf.Abs(newPosition.y - targetPosition.y);

                    GameDebugger.Instance.LogInfo($"[StateMachine] {owner.Name} moved to {newPosition}. New Distance to {owner.Target.Name}: {newDistanceToTarget}");

                    if (owner.IsTargetInRange(owner.Target))
                    {
                        GameDebugger.Instance.LogInfo($"[StateMachine] {owner.Name} has reached attack range of {owner.Target.Name} at {targetPosition}. Attacking now.");
                        owner.PerformAttack(owner.Target);
                    }
                }
                else
                {
                    GameDebugger.Instance.LogInfo($"[StateMachine] {owner.Name} has no MovePoints left. Ending turn.");
                    // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
                    MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "StateMachine.UpdateState no movement",
                        "Selected action: None\nReason for no movement: no MovePoints",
                        owner);
                }
            }
        }
        else
        {
            GameDebugger.Instance.LogInfo($"[StateMachine] {owner.Name} is NOT hostile. Defaulting to IdleState.");
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "StateMachine.UpdateState non-hostile",
                "Selected action: ChangeState\nReason: owner not hostile\nNew state: IdleState",
                owner);
            owner.stateMachine.ChangeState(new IdleState());
        }
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
    void UpdateState(Character owner);
    void ExitState(Character owner);
}

public class IdleState : IState
{
    private const int MaxIterations = 1;

    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Idling;
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered Idle state.");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "IdleState.EnterState",
            "State: Idle\nSelected action: None",
            owner);
    }

    public void UpdateState(Character owner)
    {
        if (owner.IInteractableID == PlayerStats.Instance.InteractingWithID && owner.IsPlayerVisible)
        {
            GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) is interacting with the player and will idle.");
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "IdleState.UpdateState no movement",
                "Selected action: None\nReason for no movement: interacting with player and player visible",
                owner);
            owner.RecordTurnDecision(CharacterTurnDecisionResult.Idled, "IdleState ended the turn because the NPC is busy interacting with the player.");
            owner.ConsumeRemainingActionPointsForTurn("IdleState.UpdateState interacting with visible player");
            return;
        }

        int iterationCount = 1;
        CharacterDecisionResult decision = CharacterDecisionResolver.ResolveWorldDecision(owner);

        owner.RecordTurnDecision(
            decision?.TurnDecisionResult ?? CharacterTurnDecisionResult.Idled,
            decision?.Reason ?? "IdleState defaulted to idle because CharacterDecisionResolver returned null.");

        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "IdleState.UpdateState resolved via CharacterDecisionResolver",
            $"Iteration: {iterationCount}\n" +
            $"DecisionType: {decision?.DecisionType.ToString() ?? "NULL"}\n" +
            $"TurnDecisionResult: {decision?.TurnDecisionResult.ToString() ?? "NULL"}\n" +
            $"CandidateCount: {decision?.CandidateCount.ToString() ?? "NULL"}\n" +
            $"MovementAttempted: {decision?.MovementAttempted.ToString() ?? "NULL"}\n" +
            $"MovementSucceeded: {decision?.MovementSucceeded.ToString() ?? "NULL"}\n" +
            $"RandomWanderSelected: {decision?.RandomWanderSelected.ToString() ?? "NULL"}\n" +
            $"Reason: {decision?.Reason ?? "NULL"}",
            owner);

        owner.ConsumeRemainingActionPointsForTurn("IdleState.UpdateState turn resolved");
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "IdleState.UpdateState resolved",
            $"IterationCount: {iterationCount}/{MaxIterations}\nDecisionResult: {owner.LastTurnDecisionResult}\nDecisionReason: {owner.LastTurnDecisionReason}\nAP after resolve: {owner.ActionPoints}\nMP after resolve: {owner.MovePoints}",
            owner);
    }

    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has exited Idle state.");
    }
}


public class HostileState : IState
{
    private const int MaxIterations = 10;

    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Hostile;
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered Hostile state. Their target is: {owner.Target?.Name ?? "None"}");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "HostileState.EnterState",
            $"State: Hostile\nTarget: {owner.Target?.Name ?? "NULL"}",
            owner);
    }

    public void UpdateState(Character owner)
    {
        if (owner == null || !owner.IsCombatActorAvailable())
        {
            return;
        }

        int iterationCount = 0;

        while (owner.ActionPoints > 0 && iterationCount < MaxIterations)
        {
            iterationCount++;
            GameDebugger.Instance.LogInfo($"HostileState Update: Character {owner.IInteractableID} ({owner.Name}) with Target {owner.Target?.Name ?? "None"}.");

            if (!EnsureValidHostileTarget(owner, $"HostileState.UpdateState iteration {iterationCount}"))
            {
                return;
            }

            if (owner.Target != null && owner.IsTargetInRange(owner.Target))
            {
                int configuredActionCost = Math.Max(owner.GetActionCost("Attack"), 1);
                const int effectiveAttackCost = 2;
                if (owner.ActionPoints >= effectiveAttackCost)
                {
                    // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
                    MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "HostileState.UpdateState selected attack",
                        $"Selected action: Attack\nTarget: {owner.Target.Name}\nConfigured action cost: {configuredActionCost}\nEffective shared action cost: {effectiveAttackCost}",
                        owner);
                    CombatActionResolutionDiagnosticsLogger.LogEvent("[ATTACK ENTRY]", "HostileState.UpdateState initiating shared attack",
                        $"ActionName={CombatActionResolutionDiagnosticsLogger.InferActionName(owner, owner.GetMainHandItem() == null ? DamageType.Bludgeoning : owner.GetMainHandItem().DamageType)}\n" +
                        $"Target={owner.Target.Name} [{owner.Target.IInteractableID}]\n" +
                        $"ConfiguredActionCost={configuredActionCost}\n" +
                        $"EffectiveSharedActionCost={effectiveAttackCost}\n" +
                        $"APSpendAuthority=Character.PerformAttack\n" +
                        $"ExtraAPSpendRemoved={true}",
                        owner, owner.Target);
                    owner.PerformAttack(owner.Target);
                    GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) attacked {owner.Target.Name}. Effective cost: {effectiveAttackCost}. Remaining AP: {owner.ActionPoints}");
                }
                else
                {
                    GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has insufficient AP to attack. Breaking out of loop.");
                    break;
                }
            }
            else if (owner.Target != null && !owner.IsTargetInRange(owner.Target))
            {
                if (owner.MovePoints > 0)
                {
                    // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
                    MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "HostileState.UpdateState selected movement",
                        $"Selected action: MoveTowardsTarget\nTarget: {owner.Target.Name}\nTarget cell: {owner.Target.NestedMapPosition}",
                        owner);
                    owner.MoveTowardsCharacter(owner.Target);
                    owner.MovePoints--;
                    GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) moved towards {owner.Target.Name}. Remaining MovePoints: {owner.MovePoints}");
                }
                else
                {
                    GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has insufficient MovePoints to move. Breaking out of loop.");
                    break;
                }
            }
            else
            {
                if (!ExitHostileStateWithoutTarget(owner, "HostileState.UpdateState reached no-target branch"))
                {
                    return;
                }
            }

            if (iterationCount >= MaxIterations)
            {
                GameDebugger.Instance.LogWarning($"Character {owner.IInteractableID} ({owner.Name}) exceeded max iterations in Hostile state loop.");
                break;
            }
        }
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
            CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT TARGET]", "HostileState.UpdateState reacquired valid hostile target",
                $"Source={source}\n" +
                $"NewTarget={replacementTarget?.Name ?? "NULL"} [{replacementTarget?.IInteractableID.ToString() ?? "NULL"}]",
                owner, replacementTarget);
            return true;
        }

        ExitHostileStateWithoutTarget(owner, $"{source}: no valid hostile target");
        return false;
    }

    private bool ExitHostileStateWithoutTarget(Character owner, string source)
    {
        owner.ClearCombatTarget(source);
        owner.IsHostile = false;
        owner.InCombat = false;
        owner.Stance = NPCStance.Default;
        owner.ActionPoints = 0;
        owner.stateMachine.ChangeState(new IdleState());
        TurnOrchestrator.Instance?.TryUpdateTurnContext();
        CombatActionResolutionDiagnosticsLogger.LogEvent("[COMBAT TARGET]", "HostileState.UpdateState exited hostile state due to invalid or missing target",
            $"Source={source}\n" +
            $"Actor={owner.Name} [{owner.IInteractableID}]\n" +
            $"IsHostileAfter={owner.IsHostile}\n" +
            $"StanceAfter={owner.Stance}\n" +
            $"InCombatAfter={owner.InCombat}\n" +
            $"APAfter={owner.ActionPoints}",
            owner);
        return false;
    }
}

public class FriendlyState : IState
{
    private const int MaxIterations = 10;

    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Idling; // Friendly but idle
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered Friendly state.");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "FriendlyState.EnterState",
            "State: Friendly",
            owner);
    }

    public void UpdateState(Character owner)
    {
        int iterationCount = 0;

        while (owner.ActionPoints > 0 && iterationCount < MaxIterations)
        {
            iterationCount++;

            int actionCost = Math.Max(owner.GetActionCost("FriendlyAction"), 1);
            if (owner.ActionPoints >= actionCost)
            {
                // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
                MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "FriendlyState.UpdateState selected friendly action",
                    $"Selected action: FriendlyAction\nReason for no movement: friendly state action consumes AP\nAction cost: {actionCost}",
                    owner);
                owner.SpendActionPoints(actionCost);
                GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) performed Friendly action. Cost: {actionCost}. Remaining AP: {owner.ActionPoints}");
            }
            else
            {
                GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has insufficient AP for friendly action. Breaking out of loop.");
                break;
            }

            if (iterationCount >= MaxIterations)
            {
                GameDebugger.Instance.LogWarning($"Character {owner.IInteractableID} ({owner.Name}) exceeded max iterations in Friendly state loop.");
                break;
            }
        }
    }

    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has exited Friendly state.");
    }
}

public class FleeingState : IState
{
    private const int MaxIterations = 10;

    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Fleeing;
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered Fleeing state.");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "FleeingState.EnterState",
            "State: Fleeing",
            owner);
    }

    public void UpdateState(Character owner)
    {
        int iterationCount = 0;

        while (owner.ActionPoints > 0 && iterationCount < MaxIterations)
        {
            iterationCount++;

            if (owner.MovePoints > 0)
            {
                // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
                MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "FleeingState.UpdateState selected movement",
                    "Selected action: MoveAwayFromPlayer",
                    owner);
                owner.MoveAwayFromPlayer();
                owner.MovePoints--;
                GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) moved away from player. Remaining MovePoints: {owner.MovePoints}");
            }
            else
            {
                GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has insufficient MovePoints to move away. Breaking out of loop.");
                break;
            }

            if (iterationCount >= MaxIterations)
            {
                GameDebugger.Instance.LogWarning($"Character {owner.IInteractableID} ({owner.Name}) exceeded max iterations in Fleeing state loop.");
                break;
            }
        }
    }

    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has exited Fleeing state.");
    }
}

public class FollowingState : IState
{
    private const int MaxIterations = 10;

    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Following;
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered Following state.");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "FollowingState.EnterState",
            "State: Following",
            owner);
    }

    public void UpdateState(Character owner)
    {
        int iterationCount = 0;

        while (owner.ActionPoints > 0 && iterationCount < MaxIterations)
        {
            iterationCount++;

            if (owner.MovePoints > 0)
            {
                // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
                MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "FollowingState.UpdateState selected movement",
                    "Selected action: MoveTowardsPlayer",
                    owner);
                owner.MoveTowardsPlayer();
                owner.MovePoints--;
                GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) moved towards player. Remaining MovePoints: {owner.MovePoints}");
            }
            else
            {
                GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has insufficient MovePoints to follow player. Breaking out of loop.");
                break;
            }

            if (iterationCount >= MaxIterations)
            {
                GameDebugger.Instance.LogWarning($"Character {owner.IInteractableID} ({owner.Name}) exceeded max iterations in Following state loop.");
                break;
            }
        }
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
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered TrueIdle state.");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "TrueIdleState.EnterState",
            "State: TrueIdle",
            owner);
    }

    public void UpdateState(Character owner)
    {
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "TrueIdleState.UpdateState no movement",
            "Selected action: None\nReason for no movement: TrueIdle consumes AP and MP",
            owner);
        owner.RecordTurnDecision(CharacterTurnDecisionResult.Idled, "TrueIdle state consumed the entire NPC turn.");
        owner.SpendActionPoints(owner.ActionPoints);  // Consume all AP
        owner.MovePoints = 0;  // Ensure they don't move
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) remains idle. All AP and MovePoints consumed.");
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
        GameDebugger.Instance.LogInfo($"Monster {owner.IInteractableID} ({owner.Name}) is now aggressive!");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterAggroState.EnterState",
            $"State: MonsterAggro\nTarget: {owner.Target?.Name ?? "NULL"}",
            owner);
    }

    public void UpdateState(Character owner)
    {
        Monster monster = owner as Monster;
        if (monster == null || monster.Target == null)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("MonsterAggroState.UpdateState no action",
                $"Selected action: None\nReason for no movement: monster null or target null\nTarget: {owner?.Target?.Name ?? "NULL"}",
                owner);
            return;
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
                $"APSpendAuthority=Character.PerformAttack",
                monster, monster.Target);
            monster.PerformAttack(monster.Target);
        }
        else
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterAggroState.UpdateState selected movement",
                $"Selected action: MoveTowardsTarget\nTarget: {monster.Target.Name}\nTarget cell: {monster.Target.NestedMapPosition}",
                monster);
            monster.MoveTowardsCharacter(monster.Target);
        }
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
        GameDebugger.Instance.LogInfo($"Monster {owner.IInteractableID} ({owner.Name}) is chasing its target!");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterChaseState.EnterState",
            $"State: MonsterChase\nTarget: {owner.Target?.Name ?? "NULL"}",
            owner);
    }

    public void UpdateState(Character owner)
    {
        Monster monster = owner as Monster;
        if (monster == null || monster.Target == null)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("MonsterChaseState.UpdateState no action",
                $"Selected action: None\nReason for no movement: monster null or target null\nTarget: {owner?.Target?.Name ?? "NULL"}",
                owner);
            return;
        }

        if (monster.Target == null || !monster.Target.IsAlive || !monster.CanSeeTarget(monster.Target))
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterChaseState.UpdateState lost target",
                $"Selected action: ChangeState\nReason for no movement: target invalid, dead, or not visible\nTarget: {monster.Target?.Name ?? "NULL"}",
                monster);
            monster.stateMachine.ChangeState(new MonsterIdleState());
            return;
        }

        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterChaseState.UpdateState selected movement",
            $"Selected action: MoveTowardsTarget\nTarget: {monster.Target.Name}\nTarget cell: {monster.Target.NestedMapPosition}",
            monster);
        monster.MoveTowardsCharacter(monster.Target);
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
        GameDebugger.Instance.LogInfo($"Monster {owner.IInteractableID} ({owner.Name}) is fleeing!");
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterFleeState.EnterState",
            $"State: MonsterFlee\nTarget: {owner.Target?.Name ?? "NULL"}",
            owner);
    }

    public void UpdateState(Character owner)
    {
        Monster monster = owner as Monster;
        if (monster == null)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("MonsterFleeState.UpdateState no action",
                "Selected action: None\nReason for no movement: owner is not Monster",
                owner);
            return;
        }

        if (monster.Health > (monster.MaxHealth * 0.3)) // Stop fleeing if health recovers
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterFleeState.UpdateState recovered",
                "Selected action: ChangeState\nReason for no movement: health recovered\nNew state: MonsterIdleState",
                monster);
            monster.stateMachine.ChangeState(new MonsterIdleState());
            return;
        }

        if (monster.Target != null)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterFleeState.UpdateState selected movement",
                $"Selected action: MoveAwayFromTarget\nTarget: {monster.Target.Name}",
                monster);
            monster.MoveAwayFromCharacter(monster.Target);
        }
        else
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterFleeState.UpdateState no target",
                "Selected action: None\nReason for no movement: target null",
                monster);
        }
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
        // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterIdleState.EnterState",
            "State: MonsterIdle",
            owner);
    }

    public void UpdateState(Character owner)
    {
        Monster monster = owner as Monster;
        if (monster == null)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogWarning("MonsterIdleState.UpdateState no action",
                "Selected action: None\nReason for no movement: owner is not Monster",
                owner);
            return;
        }

        turnsSinceLastPatrol++;

        if (turnsSinceLastPatrol >= PatrolIntervalTurns)
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterIdleState.UpdateState selected patrol movement",
                $"Selected action: MoveRandom\nPatrolIntervalTurns: {PatrolIntervalTurns}\nTurnsSinceLastPatrol: {turnsSinceLastPatrol}",
                monster);
            monster.MoveInRandomDirection();
            turnsSinceLastPatrol = 0;
            GameDebugger.Instance.LogInfo($"{monster.Name} is patrolling.");
        }
        else
        {
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterIdleState.UpdateState waiting",
                $"Selected action: None\nReason for no movement: patrol interval not yet reached\nPatrolIntervalTurns: {PatrolIntervalTurns}\nTurnsSinceLastPatrol: {turnsSinceLastPatrol}",
                monster);
        }

        if (monster.IsPlayerVisible)
        {
            monster.Target = monster.FindClosestEnemy();
            // CODEXLOG002_MOVEMENT_AI: temporary AI state diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "MonsterIdleState.UpdateState player visible",
                $"Selected action: ChangeState\nNew state: MonsterAggroState\nTarget: {monster.Target?.Name ?? "NULL"}",
                monster);
            monster.stateMachine.ChangeState(new MonsterAggroState());
        }
    }


    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"{owner.Name} is leaving Monster Idle state.");
    }
}

#endregion
