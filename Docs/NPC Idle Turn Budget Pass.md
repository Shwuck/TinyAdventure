# NPC Idle Turn Budget Pass

## 1. Runtime Issue

Exploration NPC turns were taking too long because many idle actors were effectively processing more than one movement/no-movement decision per turn.

Observed editor/runtime warnings included:

- `Character X exceeded max iterations in Idle state loop.`
- `Character has no valid CellsOfInterest. Moving randomly.`
- `Character has no MovePoints left to move.`
- `Character.TryMove blocked.`
- `Character.MoveInRandomDirection no valid move.`

## 2. Root Cause

The idle exploration path had three separate problems interacting together:

1. `Assets/StateMachine.cs` `IdleState.UpdateState(...)` used an AP-driven loop and could make multiple idle decisions in one NPC turn.
2. Movement/no-movement branches inside `IdleState.UpdateState(...)` often did not consume the NPC turn, so AP could remain unchanged.
3. `Assets/Objects/Character.cs` `ExecuteTurnActions()` always called `MoveToCellsOfInterest()` after `stateMachine.Update()` in exploration, even when the state machine had already attempted or resolved movement.

That produced a double-processing pattern:

- state machine idle decision
- possible random movement attempt
- then unconditional `MoveToCellsOfInterest()`
- then fallback random movement again

The latest turn diagnostics showed this exact sequence repeatedly in the same NPC turn.

## 3. Files Changed

- `Assets/StateMachine.cs`
- `Assets/Objects/Character.cs`

## 4. IdleState Before / After

### Before

- `IdleState.UpdateState(...)` looped while `ActionPoints > 0`.
- a no-movement outcome could leave AP unchanged
- random movement could fail without resolving the turn
- the max-iteration guard could be hit in normal idle turns

### After

- `IdleState.UpdateState(...)` makes one bounded decision per NPC turn
- the decision is recorded as a turn result
- the remaining AP is consumed once the idle decision is resolved
- failed movement and deliberate idling both count as a completed NPC turn
- the old loop no longer drives repeated idle decisions in a single turn

## 5. ExecuteTurnActions Before / After

### Before

- `Character.ExecuteTurnActions()` always called `MoveToCellsOfInterest()` after `stateMachine.Update()` in exploration
- this duplicated movement logic already attempted by idle AI

### After

- `Character.ExecuteTurnActions()` resets a per-turn decision result before the state update
- if the state machine already resolved the NPC turn, `MoveToCellsOfInterest()` is skipped
- `MoveToCellsOfInterest()` is now only used as a fallback when the state machine genuinely left the exploration turn unresolved

## 6. Movement Fallback Before / After

### Before

- `MoveToCellsOfInterest()` always fell back to random movement when no valid cells existed
- random movement could still be attempted after a prior random movement decision
- no valid cells and no valid adjacent move were logged like warnings during normal idle behaviour

### After

- fallback movement is bounded to one attempt
- movement is skipped early if `MovePoints <= 0`
- `Character.MoveInRandomDirection()` now returns whether movement actually succeeded
- `no valid CellsOfInterest` and `no valid random move` are treated as normal AI outcomes in diagnostics rather than routine warning spam

## 7. MovePoints Findings

- `ExplorationTurnManager.OnNPCTurnExecute(...)` already resets NPC move points through `Character.ResetMovePointsForTurn()`
- the main bug was not a missing move reset in the exploration manager
- the more important issue was that idle/movement fallback logic kept attempting extra work after a turn should already have been considered resolved

## 8. Diagnostics Added / Clarified

The pass now records:

- per-turn NPC decision result
- decision reason
- whether `ExecuteTurnActions()` skipped `MoveToCellsOfInterest()` because the state machine had already resolved the turn
- idle resolution summary including AP/MP after the bounded decision
- early skip when random movement is requested with no move points

## 9. Manual Tests

1. Enter a nested area with several NPCs.
2. Move the player once with keyboard input.
3. Confirm NPC turns resolve quickly and the player regains control promptly.
4. Confirm normal idle NPCs do not hit the idle max-iteration warning.
5. Confirm an NPC with no `CellsOfInterest` ends their turn cheaply.
6. Confirm an NPC with `MovePoints == 0` does not attempt random movement repeatedly.
7. Confirm a random movement attempt happens at most once per idle turn.
8. Confirm some NPCs still visibly move during exploration.
9. Enter combat and confirm the hostile opponent still acts.
10. Review `DiagnosticLogs/TinyAdventure_TurnDiagnostics_Latest.txt` for:
   - `IdleState.UpdateState resolved`
   - `Character.ExecuteTurnActions skipped CellsOfInterest movement`
   - one bounded decision result per NPC turn

## 10. Remaining Risks

- `MoveToCellsOfInterest()` is still a legacy fallback path and not yet part of a cleaner unified exploration AI policy.
- Other non-idle states still use older AP/MP patterns and may need the same bounded-turn audit later.
- The broader codebase still contains many legacy raw `Debug.Log*` calls outside the specific methods touched in this pass.
