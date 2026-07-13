# Turn Determinism Pass

## Scope

This pass audited and corrected accidental frame-driven and coroutine-driven turn behaviour in the active turn system.

Files audited:

- `Assets/BaseTurnManager.cs`
- `Assets/CombatTurnManager.cs`
- `Assets/ExplorationTurnManager.cs`
- `Assets/TurnOrchestrator.cs`
- `Assets/Objects/Character.cs`
- `Assets/StateMachine.cs`
- `Assets/Objects/Monster.cs`

## Findings Before This Pass

- `BaseTurnManager` used coroutines as turn-control flow:
  - `StartCoroutine(ExecuteTurnWithDelay(...))`
  - queued next-turn execution through `RequestExecuteNextTurn(...)`
  - frame-yielded `ExecuteNextTurnNextFrame()`
- `ExplorationTurnManager.OnCycleEnded()` restarted the next cycle by coroutine on the next frame.
- `CombatTurnManager.OnCycleEnded()` could restart the next cycle immediately from cycle-end logic.
- skipped actors advanced through queued next-turn scheduling instead of one bounded deterministic loop.
- monster idle behaviour depended on `Time.time`.
- `Monster.UpdateMonsterAI()` exposed a non-turn-owned state machine update entry point.

These patterns made turn progression depend on frame scheduling instead of a single turn authority.

## Deterministic Model After This Pass

The active turn model is now:

1. `StartTurnCycle()` begins one sorted cycle.
2. `BaseTurnManager.ContinueTurnSequence(...)` owns advancement synchronously.
3. Each actor is processed in sorted order.
4. NPC turns are fully resolved in the manager before the next actor is considered.
5. Player turns pause the sequence and wait for `PlayerTurnCompleted()`.
6. End-of-cycle handling updates context only.
7. If another cycle should begin, the manager opens it explicitly and continues deterministically.

No coroutine or frame callback now decides when the next actor gets processed in the active turn managers.

## Key Changes

### `Assets/BaseTurnManager.cs`

- removed coroutine-driven logical advancement
- removed queued-next-turn control flow
- added synchronous `ContinueTurnSequence(...)`
- added guarded `BeginCycleInternal(...)`
- moved cycle-to-cycle continuation into deterministic manager logic

### `Assets/ExplorationTurnManager.cs`

- removed frame-scheduled cycle restart logic
- exploration now relies on `ShouldAutoStartNextCycle()`

### `Assets/CombatTurnManager.cs`

- removed direct cycle self-restart from `OnCycleEnded()`
- combat now relies on `ShouldAutoStartNextCycle()`
- player auto-complete path now stays inside the same logical turn sequence

### `Assets/StateMachine.cs`

- removed `Time.time` patrol gating from `MonsterIdleState`
- monster patrol cadence is now turn-based

### `Assets/Objects/Monster.cs`

- `UpdateMonsterAI()` now refuses to run unless the monster currently owns the turn

## Behaviour Intentionally Preserved

- actor ordering is still based on sorted speed and placement ID
- NPCs can still use their own AP/MP within their single logical turn
- player turns still pause for input
- combat and exploration still remain context-separated through `TurnOrchestrator`

## Remaining Risks

- `Assets/TurnManager.cs` still exists as a legacy file with its own coroutine-based flow, but it is not part of the active `BaseTurnManager` / `TurnOrchestrator` path audited here
- `StateMachine.UpdateState(...)` is still present as legacy alternate logic and should remain non-authoritative
- this pass was source-level validated only; Unity compile and runtime validation still need to be run
