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

        CurrentState?.ExitState(Owner);
        CurrentState = newState;
        CurrentState.EnterState(Owner);

        GameDebugger.Instance.LogInfo($"State changed for Character {Owner.IInteractableID} ({characterName}) to {CurrentState.GetType().Name}");
    }

    public void Update()
    {
        CurrentState?.UpdateState(Owner);
    }

    public void UpdateState(Character owner)
    {
        if (owner == null)
        {
            GameDebugger.Instance.LogError("[StateMachine] UpdateState was called with a NULL owner!");
            return;
        }

        GameDebugger.Instance.LogInfo($"[StateMachine] {owner.Name} updating state. Stance: {owner.Stance}, Target: {owner.Target?.Name ?? "None"}, AP: {owner.ActionPoints}, MP: {owner.MovePoints}");

        // Check if the character has a valid target
        if (owner.Target == null || !owner.Target.IsAlive)
        {
            GameDebugger.Instance.LogInfo($"[StateMachine] {owner.Name} has no valid target. Returning to Idle.");
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
                }
            }
        }
        else
        {
            GameDebugger.Instance.LogInfo($"[StateMachine] {owner.Name} is NOT hostile. Defaulting to IdleState.");
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
    private System.Random random = new System.Random();
    private const int MaxIterations = 2;
    private const float MovementProbability = 0.8f; // 80% chance to move

    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Idling;
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered Idle state.");
    }

    public void UpdateState(Character owner)
    {
        if (owner.IInteractableID == PlayerStats.Instance.InteractingWithID && owner.IsPlayerVisible)
        {
            GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) is interacting with the player and will idle.");
            return;
        }

        int iterationCount = 0;

        while (owner.ActionPoints > 0 && iterationCount < MaxIterations)
        {
            iterationCount++;

            int actionChoice = random.Next(0, 3); // Randomly choose an idle action
            int actionCost = 1; // Default action cost

            switch (actionChoice)
            {
                case 0: // Consider movement
                    if (random.NextDouble() <= MovementProbability) // Only move if probability check passes
                    {
                        owner.MoveInRandomDirection(); // Delegate the movement logic to the Character
                    }
                    else
                    {
                        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) chose not to move this turn.");
                    }
                    break;

                case 1: // Perform another idle action
                    owner.SpendActionPoints(actionCost);
                    GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) performed another idle action. Cost: {actionCost}. Remaining AP: {owner.ActionPoints}");
                    break;

                default: // Default idle action
                    owner.SpendActionPoints(actionCost);
                    GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) performed default idle action. Cost: {actionCost}. Remaining AP: {owner.ActionPoints}");
                    break;
            }

            if (iterationCount >= MaxIterations)
            {
                GameDebugger.Instance.LogWarning($"Character {owner.IInteractableID} ({owner.Name}) exceeded max iterations in Idle state loop.");
                break;
            }
        }
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
    }

    public void UpdateState(Character owner)
    {
        int iterationCount = 0;

        while (owner.ActionPoints > 0 && iterationCount < MaxIterations)
        {
            iterationCount++;
            GameDebugger.Instance.LogInfo($"HostileState Update: Character {owner.IInteractableID} ({owner.Name}) with Target {owner.Target?.Name ?? "None"}.");

            if (owner.Target != null && owner.IsTargetInRange(owner.Target))
            {
                int actionCost = Math.Max(owner.GetActionCost("Attack"), 1);
                if (owner.ActionPoints >= actionCost)
                {
                    owner.PerformAttack(owner.Target);
                    owner.SpendActionPoints(actionCost);
                    GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) attacked {owner.Target.Name}. Cost: {actionCost}. Remaining AP: {owner.ActionPoints}");
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
                int actionCost = Math.Max(owner.GetActionCost("Idle"), 1);
                if (owner.ActionPoints >= actionCost)
                {
                    owner.SpendActionPoints(actionCost);
                    GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) performed an idle action while in Hostile state. Cost: {actionCost}. Remaining AP: {owner.ActionPoints}");
                }
                else
                {
                    GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has insufficient AP to idle. Breaking out of loop.");
                    break;
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
}

public class FriendlyState : IState
{
    private const int MaxIterations = 10;

    public void EnterState(Character owner)
    {
        owner.Status = NPCStatus.Idling; // Friendly but idle
        GameDebugger.Instance.LogInfo($"Character {owner.IInteractableID} ({owner.Name}) has entered Friendly state.");
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
    }

    public void UpdateState(Character owner)
    {
        int iterationCount = 0;

        while (owner.ActionPoints > 0 && iterationCount < MaxIterations)
        {
            iterationCount++;

            if (owner.MovePoints > 0)
            {
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
    }

    public void UpdateState(Character owner)
    {
        int iterationCount = 0;

        while (owner.ActionPoints > 0 && iterationCount < MaxIterations)
        {
            iterationCount++;

            if (owner.MovePoints > 0)
            {
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
    }

    public void UpdateState(Character owner)
    {
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
    }

    public void UpdateState(Character owner)
    {
        Monster monster = owner as Monster;
        if (monster == null || monster.Target == null) return;

        if (monster.IsTargetInRange(monster.Target))
        {
            monster.PerformAttack(monster.Target);
        }
        else
        {
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
    }

    public void UpdateState(Character owner)
    {
        Monster monster = owner as Monster;
        if (monster == null || monster.Target == null) return;

        if (monster.Target == null || !monster.Target.IsAlive || !monster.CanSeeTarget(monster.Target))
        {
            monster.stateMachine.ChangeState(new MonsterIdleState());
            return;
        }


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
    }

    public void UpdateState(Character owner)
    {
        Monster monster = owner as Monster;
        if (monster == null) return;

        if (monster.Health > (monster.MaxHealth * 0.3)) // Stop fleeing if health recovers
        {
            monster.stateMachine.ChangeState(new MonsterIdleState());
            return;
        }

        if (monster.Target != null)
        {
            monster.MoveAwayFromCharacter(monster.Target);
        }
    }

    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"Monster {owner.IInteractableID} ({owner.Name}) stopped fleeing.");
    }
}

public class MonsterIdleState : IState
{
    public void EnterState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"{owner.Name} has entered Monster Idle state.");
    }

    private float patrolCooldown = 2.0f; // Move every 2 seconds
    private float lastPatrolTime = 0f;

    public void UpdateState(Character owner)
    {
        Monster monster = owner as Monster;
        if (monster == null) return;

        if (Time.time - lastPatrolTime > patrolCooldown)
        {
            monster.MoveInRandomDirection();
            lastPatrolTime = Time.time;
            GameDebugger.Instance.LogInfo($"{monster.Name} is patrolling.");
        }

        if (monster.IsPlayerVisible)
        {
            monster.Target = monster.FindClosestEnemy();
            monster.stateMachine.ChangeState(new MonsterAggroState());
        }
    }


    public void ExitState(Character owner)
    {
        GameDebugger.Instance.LogInfo($"{owner.Name} is leaving Monster Idle state.");
    }
}

#endregion
