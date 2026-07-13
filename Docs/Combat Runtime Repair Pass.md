# Combat Runtime Repair Pass

## 1. Runtime Issue Summary

This pass repaired runtime failures introduced around the shared `CombatResolver` integration.

Reported symptoms:

- repeated `CODEXLOG005_COMBAT_ACTION_RESOLUTION` warnings for inactive attackers and defenders
- hostile actors repeatedly trying to attack stale targets
- hostile state loops reaching max iteration guards
- non-combat villagers being processed by the combat turn manager
- immediate combat turn-cycle restart chains ending in `StackOverflowException`

This pass did not add new combat features or deepen combat mechanics.

## 2. Root Cause Found

The runtime failures were caused by several interacting state/flow issues rather than one single bug.

Primary causes confirmed:

- `CombatResolver` correctly rejected dead/inactive actors, but stale `Target` references were still left on combatants after death or removal.
- `HostileState.UpdateState(...)` could continue operating with invalid targets instead of exiting hostility cleanly.
- `TurnOrchestrator.SwitchToCombatMode()` was registering broad local area occupants into combat instead of only actual combat participants.
- `CombatTurnManager` was willing to keep cycling actors that were no longer alive, active, or meaningfully in combat.
- `BaseTurnManager` advanced turns synchronously in a tight chain, and the first recursion guard pass accidentally still allowed same-frame restart pressure.
- monsters were not explicitly initialised active/alive in their constructor, which made resolver rejections more likely for generated monsters.
- the player action path could still request a combat action after the player character had already become inactive/dead.

## 3. Files Changed

- `Assets/BaseTurnManager.cs`
- `Assets/CombatResolver.cs`
- `Assets/CombatTurnManager.cs`
- `Assets/Objects/Animal.cs`
- `Assets/Objects/Character.cs`
- `Assets/Objects/Monster.cs`
- `Assets/Objects/NPC.cs`
- `Assets/PlayerController.cs`
- `Assets/StateMachine.cs`
- `Assets/TurnOrchestrator.cs`
- `Docs/Combat Runtime Repair Pass.md`

## 4. Living Character Initialisation

Findings:

- `PlayerCharacterFactory.CreatePlayerCharacter(...)` already initialised the player as alive and active.
- NPC generation paths were already mostly correct.
- `Character` base construction already set `IsAlive = true`.
- `Monster(MonsterCreationData data)` did not explicitly set both `IsAlive` and `IsActive`.

Repair:

- made `Monster`, `NPC`, and `Animal` constructors explicitly set living runtime state consistently
- left resolver validation strict instead of weakening it

## 5. Invalid Target Retention

Confirmed:

- dead or inactive characters could remain assigned as `Target`
- other combatants could continue trying to attack them

Repair:

- `Character.PerformAttack(...)` now performs a resolver validation precheck before mutating hostility/target state
- `Character.Die()` now clears this character as a target from other combatants
- `Character` gained focused target-maintenance helpers:
  - `IsCombatActorAvailable()`
  - `IsValidCombatTarget(...)`
  - `ClearCombatTarget(...)`
  - `FindReplacementCombatTarget()`
  - `TryRefreshCombatTarget(...)`

## 6. HostileState Changes

`Assets/StateMachine.cs`

Changes:

- `HostileState.UpdateState(...)` now bails out immediately for unavailable actors
- each hostile loop iteration validates or refreshes the current target
- if no valid replacement target exists, the actor:
  - clears target
  - clears hostile/combat intent
  - returns to `IdleState`
  - zeroes AP for the turn
  - requests `TurnOrchestrator.TryUpdateTurnContext()`

Result:

- hostile loops should stop treating max-iteration guards as normal control flow
- stale hostility should unwind instead of spamming invalid attacks

## 7. CombatTurnManager / BaseTurnManager Changes

`Assets/CombatTurnManager.cs`

Changes:

- prunes dead/inactive/non-combat registrants before cycle start and after cycle end
- skips dead, inactive, or no-longer-combat actors
- deregisters invalid combatants before and after NPC turns
- removes hostile actors that no longer have a valid target
- only restarts combat cycles if there is still a living active player combatant and at least one valid opposing combatant

`Assets/BaseTurnManager.cs`

Changes:

- replaced direct same-frame turn advancement chains with queued next-frame advancement
- added `RequestExecuteNextTurn(...)`
- added `nextTurnAdvanceQueued` guard
- added context ownership checks around execution methods
- changed `StartTurnCycle()` to queue the first turn instead of entering `ExecuteNextTurn()` immediately
- fixed the queued-next-turn coroutine so it now executes the next turn instead of re-queuing indefinitely

Result:

- the combat cycle should no longer recurse through `EndTurnForCharacter -> ExecuteNextTurn -> EndCycle -> OnCycleEnded -> StartTurnCycle` in one stack

## 8. Turn Context / Participant Registration Changes

`Assets/TurnOrchestrator.cs`

Changes:

- `TryUpdateTurnContext()` now scans local active relationship hostilities and prunes stale hostile flags
- `SwitchToCombatMode()` now registers only actual combat participants, not every local bystander
- stale hostile actors with no target and no supporting hostility pair are reset out of combat

Result:

- idle villagers should no longer be pulled into combat turn processing unless they are genuinely involved

## 9. Player Action Guard

`Assets/PlayerController.cs`

Changes:

- `ExecutePlayerAction(...)` now rejects combat actions when the player character is dead or inactive
- `PrepareCharacterActionPointsForCombatAction(...)` now also rejects dead/inactive player attackers

Result:

- player-side dead-attacker combat requests should stop reaching `Character.PerformAttack(...)`

## 10. Behaviour Changed

- invalid shared attacks are rejected earlier and with clearer reason logging
- stale hostile targets are cleared and optionally reacquired
- actors with no valid hostile target exit hostile state instead of spinning
- combat cycles now advance through a queued frame break instead of immediate recursive chaining
- only live combat participants should stay registered in `CombatTurnManager`

## 11. Behaviour Intentionally Preserved

- `CombatResolver` remains the shared physical attack resolver
- invalid attackers/defenders are still rejected, not silently accepted
- no new armour, status, magic, or monster-ability mechanics were added
- no combat rebalance was attempted

## 12. Manual Tests To Run

1. Start the game and enter combat.
   Expected: combat starts normally and `CombatTurnManager` registers only actual participants.

2. Player punches a valid hostile target.
   Expected: normal attack resolution, no inactive-attacker warning.

3. Player attacks with a weapon.
   Expected: normal shared physical resolution and readable combat extract entry.

4. NPC attacks player.
   Expected: NPC uses hostile path once, spends AP once, and does not loop on an invalid target.

5. Kill an NPC target.
   Expected: target dies, becomes inactive, is cleared as a stale target from other combatants, and is not attacked again.

6. Kill the player or make the player inactive, then try to trigger a combat action.
   Expected: `PlayerController.ExecutePlayerAction` rejects the action before `PerformAttack`.

7. End combat by removing the final hostile.
   Expected: combat context exits cleanly, cycle restart is aborted, and exploration resumes.

8. Observe unrelated idle villagers during combat.
   Expected: they are not processed as combat actors unless actually hostile/involved.

9. Watch the diagnostic logs.
   Check:
   - `DiagnosticLogs/TinyAdventure_CombatActionResolution_Latest.txt`
   - `DiagnosticLogs/TinyAdventure_TurnDiagnostics_Latest.txt`

10. Confirm absence of prior failure patterns.
   Expected:
   - no repeated `CombatResolver.ResolveAttack rejected inactive defender`
   - no repeated `CombatResolver.ResolveAttack rejected inactive attacker`
   - no routine `exceeded max iterations in Hostile state loop`
   - no `StackOverflowException`

## 13. Remaining Risks

- this pass was source-level validated only; Unity compile/play mode was not run here
- magic and monster ability paths are still not unified under the same runtime validity wrappers
- some legacy interactions still contain older direct debug logging outside the touched repair path
- player death/game-over flow may still need a dedicated top-level rule outside combat-state cleanup
