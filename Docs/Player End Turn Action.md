# Player End Turn Action

## 1. Runtime Issue

The deterministic turn flow could leave the player stuck when no useful actions remained, because the turn manager was waiting for player completion but the UI did not expose a direct way to end the turn.

## 2. Design Decision

The authoritative turn-ending path remains the existing player-controller-to-turn-manager flow:

- `PlayerController.EndPlayerTurn(...)`
- `PlayerController.TryCompletePlayerTurnFromPlayerController(...)`
- `TurnOrchestrator.PlayerTurnCompleted()`
- active turn manager `PlayerTurnCompleted()`

The new UI hook is an Adaptive Action Menu system action, not a new combat mechanic.

## 3. Files Changed

- [Assets/TurnOrchestrator.cs](../Assets/TurnOrchestrator.cs)
- [Assets/Objects/Character.cs](../Assets/Objects/Character.cs)
- [Assets/Objects/Monster.cs](../Assets/Objects/Monster.cs)
- [Assets/PlayerController.cs](../Assets/PlayerController.cs)
- [Assets/MessageLogManager.cs](../Assets/MessageLogManager.cs)

## 4. How The End Turn Action Appears

`PlayerController.UpdateAdaptiveActionMenu()` now adds an `End Turn` button through `AddTurnControlActions()`.

It appears when:

- the player is in a nested area
- the turn manager is active
- the player is registered in the turn system
- exploration is active, or combat is active and it is currently the player turn

It is hidden outside those conditions.

## 5. How It Routes Through The Turn Manager

The button and the spacebar/manual path both call `PlayerController.EndPlayerTurn(...)`.

That method does not force NPC turns directly. It delegates to:

- `TurnOrchestrator.PlayerTurnCompleted()`
- then the active turn manager continues the deterministic sequence

## 6. Exploration Behaviour

In exploration, `End Turn` is available while the exploration turn system is active. Pressing it ends the current player turn and allows the exploration turn manager to process the next actors normally.

## 7. Combat Behaviour

In combat, `End Turn` is available only during the player combat turn. If clicked outside the player combat turn, it is safely rejected and the existing wait feedback is used.

## 8. AP / MP Handling

This pass does not invent a new AP/MP rule.

Manual `End Turn` preserves the existing behavior:

- it does not manually force AP/MP consumption
- it does not manually run NPCs
- it hands control back through the turn manager
- next-turn resets still happen where the current turn rules already define them

## 9. Manual Tests

1. Spend all movement in exploration and click `End Turn`.
2. Confirm NPCs act and the player later receives the next turn.
3. Enter combat and use an attack, then click `End Turn`.
4. Confirm enemies act once each in deterministic order.
5. Confirm the player receives the next combat turn.
6. Confirm `End Turn` is hidden or rejected when it is not the player turn.
7. Confirm the spacebar/manual end-turn path still works.

## 10. Remaining Risks

- `GetAllRegisteredCharacters()` still exists for compatibility and now explicitly means current-area roster, not active participants.
- Existing callers outside this pass that implicitly assume active participants should be audited before broader AI or perception work.
- This pass does not rebalance AP/MP or add automatic end-turn behavior beyond what already existed.
