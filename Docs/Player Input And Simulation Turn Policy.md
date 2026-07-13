# Player Input And Simulation Turn Policy

## 1. Purpose Of This Pass

Align live player input, movement, waiting, and turn-completion behaviour with the core simulation semantics without adding new combat mechanics or rewriting the turn system.

## 2. Documents Read

- `Docs/Coding Rules.txt`
- `Docs/TinyAdventure Core Simulation Semantics.txt`
- `Docs/Player End Turn Action.md`
- `Docs/Turn Roster Lifecycle Audit.md`
- `Docs/Turn Determinism Pass.md`
- `Docs/Combat Resolver Pass.md`
- `Docs/Combat Exploration Resume Repair.md`

## 3. Runtime Observations

- Keyboard movement was already advancing exploration NPC turns.
- Physical direction buttons were not using the same gameplay path.
- The existing scene `WaitButton` already existed and was a better place for manual turn completion than a duplicate AAM button.
- Combat currently runs only `CombatParticipants`, so uninvolved area characters remain in `CurrentAreaRoster` but do not currently receive turns during combat.

## 4. Keyboard Movement Path

Before this pass:

- keyboard input flowed through `PlayerController.HandleKeyboardInput()`
- then `PlayerController.HandleKeyHold(...)`
- then `PlayerController.MoveAndUpdate(...)`
- then movement cost / turn completion logic

After this pass:

- keyboard input flows through `PlayerController.HandleKeyboardInput()`
- then `PlayerController.HandleKeyHold(...)`
- then `PlayerController.RequestPlayerMove(...)`

## 5. UI Movement Button Path

Before this pass:

- `northButton/southButton/eastButton/westButton` in `PlayerController.SetupButtonListeners()` called `Move(...)` directly
- that bypassed the shared movement-cost / exploration-turn-completion path

After this pass:

- those buttons now call `PlayerController.RequestPlayerMove(...)`

## 6. Authoritative Movement Method

The authoritative player movement entry point is now:

- `PlayerController.RequestPlayerMove(Vector2Int direction, Direction newDirection, string inputSource)`

It owns:

- movement input source diagnostics
- movement validation outcome
- combat MP rejection
- post-move facing update
- movement-cost spending
- exploration turn-completion policy
- combat keep-turn-open policy

`Move(...)` is now an internal movement-application helper rather than a UI/input authority.

## 7. Exploration Movement Behaviour

Current aligned behaviour:

- successful exploration movement is treated as a time-costing action
- it now completes the exploration player turn explicitly through:
  - `PlayerController.CompleteExplorationTurnForTimeCostingAction(...)`
  - `PlayerController.TryCompletePlayerTurnFromPlayerController(...)`
  - `TurnOrchestrator.PlayerTurnCompleted()`
  - `ExplorationTurnManager.PlayerTurnCompleted()`

This no longer relies on exploration move-point depletion as the semantic reason the turn ends.

## 8. Combat Movement Behaviour

Current aligned behaviour:

- combat movement spends movement budget through `DeductMovePoints(...)`
- combat movement does not automatically end the player turn
- if MP reaches zero in combat, the player receives feedback and the turn remains open unless normal combat end-turn rules say otherwise

This matches the semantics document more closely.

## 9. Free vs Time-Costing Action Semantics

Confirmed current reality:

- the codebase still overloads `ActionPointCost` for both:
  - combat AP-like costs
  - world-time progression via `EndOfTurnManager.AddTurnProgress(...)`

This is semantically muddy and not yet fully aligned.

Important live findings:

- many `IInteraction` / `IEnvironmentalAction` implementations in `Assets/Objects/IInteractions.cs` call `EndOfTurnManager.AddTurnProgress(ActionPointCost)`
- many “free” interactions use `ActionPointCost = 0`, so they do not advance time
- several comments describe “half turn” or “small fraction of a turn” while the actual value is still `0`

So:

- inspect/look-style actions are mostly free in practice
- movement is now explicitly time-costing in exploration
- combat actions still use combat AP rules
- the broader action-cost model still needs future cleanup

## 10. Wait / End Turn Decision

Decision in this pass:

- keep the existing scene `WaitButton`
- make it context-sensitive

Behaviour:

- exploration label: `Wait`
- combat label: `End Turn`
- main-map label: `Wait`

Runtime routing:

- `EndOfTurnManager.PlayerWaits()`
- `PlayerController.HandleWaitOrEndTurn(...)`
- exploration:
  - `CompleteExplorationTurnForTimeCostingAction(...)`
- combat:
  - `EndPlayerTurn(...)`

## 11. AAM End Turn Decision

Decision:

- disable the duplicate AAM `End Turn` action

Current implementation:

- `PlayerController.AddTurnControlActions()` no longer creates the AAM button
- it logs that the dedicated wait button owns turn control

This keeps the AAM cleaner and avoids two controls with the same purpose.

## 12. Combat-As-Context Model

Current code is only partially aligned with the semantics document.

What is aligned:

- `CurrentAreaRoster` is the persistent runtime roster
- combat/exploration participants are filtered views over the same character instances
- combat does not remove uninvolved characters from the roster

What is still misaligned:

- combat currently switches active turn processing to `CombatTurnManager`
- `CombatTurnManager` only processes `CombatParticipants`
- uninvolved area characters therefore do not currently continue idle/exploration turns during combat

That means current combat still behaves like a partial world freeze as a side effect of manager ownership, even though the roster semantics are now correct.

## 13. Non-Combatant Behaviour During Combat

Current live behaviour:

- non-combatants remain in `CurrentAreaRoster`
- non-combatants are excluded from the active turn list during combat
- non-combatants are therefore effectively frozen during combat in the current implementation

This is not the intended long-term design.

Recommended direction:

- keep one persistent area roster
- stop treating `CombatParticipants` as “everyone who acts while combat exists”
- move toward:
  - combat-engaged actors using combat behaviour
  - aware bystanders using reaction behaviour later
  - unaware characters continuing normal idle/exploration behaviour

## 14. Speed / Movement / AP Future Design Note

Current state:

- `Speed` exists on `Character` and `PlayerStats`
- turn order sorting already uses `Speed` in `BaseTurnManager.SortCharacters()`
- `MovePoints` and `ActionPoints` also exist separately
- combat currently uses visible AP/MP budgets

Current limitation:

- `Speed` affects turn order sorting, not readiness frequency
- movement budget and turn frequency are still conceptually tangled
- anatomy-driven movement penalties are not yet the authoritative live movement-budget path

Recommended next design step:

- preserve the current fields
- do not implement full readiness scheduling yet
- later introduce clearer derived helpers such as:
  - `GetSpeed()`
  - `GetMovementBudget()`
  - `GetActionBudget()`

## 15. Files Inspected

- `Assets/PlayerController.cs`
- `Assets/EndOfTurnManager.cs`
- `Assets/TurnOrchestrator.cs`
- `Assets/BaseTurnManager.cs`
- `Assets/ExplorationTurnManager.cs`
- `Assets/CombatTurnManager.cs`
- `Assets/PlayerStats.cs`
- `Assets/Objects/IInteractions.cs`
- `Assets/Objects/Character.cs`
- `Assets/UIController.cs`
- latest turn and combat diagnostic logs in `DiagnosticLogs/`

## 16. Files Changed

- `Assets/PlayerController.cs`
- `Assets/EndOfTurnManager.cs`
- `Assets/TurnOrchestrator.cs`
- `Assets/MessageLogManager.cs`
- `Docs/Player Input And Simulation Turn Policy.md`

## 17. Diagnostics Added / Clarified

Added or improved:

- `PlayerController.RequestPlayerMove` diagnostics
  - input source
  - mode
  - success/failure
  - blocked reason
  - AP/MP before/after
  - whether world time advanced
  - whether exploration turn completion was requested
  - whether combat turn remained open
- `PlayerController.CompleteExplorationTurnForTimeCostingAction`
  - explicit action-cost category logging
- `TurnOrchestrator.SwitchToCombatMode`
  - roster vs combat-participant split summary
- `TurnOrchestrator.SwitchToExplorationMode`
  - participant rebuild summary
- `EndOfTurnManager.RefreshWaitButtonPresentation`
  - label/interactable changes for the context-sensitive wait button

## 18. Manual Tests

1. In a nested area, move with keyboard once in exploration.
2. Confirm the move succeeds and NPCs/world actors then act.
3. Move with each physical direction button in exploration.
4. Confirm those buttons now produce the same turn result as keyboard movement.
5. Confirm the latest turn log shows `PlayerController.RequestPlayerMove` for both input sources.
6. Press the existing `WaitButton` in exploration.
7. Confirm it says `Wait`, ends the exploration turn, and advances NPC/world turns.
8. Enter combat.
9. Confirm the `WaitButton` label changes to `End Turn`.
10. Move during combat.
11. Confirm MP is spent and the player turn remains open if valid.
12. Press `End Turn`.
13. Confirm combat actors advance through the deterministic turn manager.
14. Confirm no duplicate AAM `End Turn` button appears.
15. Confirm combat logs show the roster/participant split summary.

## 19. Remaining Risks

- `ActionPointCost` is still overloaded across exploration-time and combat-AP semantics.
- Many interaction comments in `Assets/Objects/IInteractions.cs` do not match the actual numeric value being used.
- Combat still freezes uninvolved area characters as a side effect of `CombatTurnManager` ownership.
- The current wait-button lookup uses the existing scene object name `WaitButton`; if that name changes, the presentation refresh will need updating.

## 20. Recommended Next Phase

1. Introduce a small action-cost semantic layer without rewriting all interactions at once.
2. Audit and classify the most important live actions:
   - inspect/look
   - talk
   - loot/use item
   - environmental actions
3. Move toward one area simulation with separate behaviour modes instead of freezing non-combatants during combat.
4. Only after that, design readiness/speed scheduling on top of the stable area simulation loop.
