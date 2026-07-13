# ActionCostProfile Metadata Pass

## 1. Purpose

This pass adds a narrow metadata bridge between the legacy AP/MP action model and the future stamina/time model.

It does not migrate gameplay to stamina.

It does not:

- replace AP
- replace MP
- spend stamina
- block actions with stamina
- change movement execution
- change combat execution
- change turn flow
- change action availability

The goal is to describe action cost meaning without changing the current action authority.

## 2. Docs / Rules Read

Hard constraints read first:

- `Docs/Coding Rules.txt`
- `Docs/TinyAdventure Core Simulation Semantics.txt`
- `Docs/Stamina Direction And Foundation Pass.md`
- `Docs/Stamina Model Shaping Pass.md`
- `Docs/Action Cost Semantics Audit.md`
- `Docs/Player Input And Simulation Turn Policy.md`
- `Docs/Character Decision Foundation Pass.md`
- `Docs/NPC World Thought Process And Affordances.md`

Recent related docs also read:

- `Docs/Combat Resolver Pass.md`
- `Docs/Combat Runtime Repair Pass.md`
- `Docs/Combat Exploration Resume Repair.md`
- `Docs/Player End Turn Action.md`
- `Docs/Turn Determinism Pass.md`

Prompt / rules folder check:

- `Prompts/` not present
- `Docs/Prompts/` not present
- `In/` not present
- `Input/` not present
- `CodexPrompts/` not present

## 3. Files Inspected

- `Assets/Objects/IInteractions.cs`
- `Assets/Objects/Item Actions.cs`
- `Assets/Actions/ActionManager.cs`
- `Assets/PlayerController.cs`
- `Assets/EndOfTurnManager.cs`
- `Assets/CombatResolver.cs`
- `Assets/Objects/Character.cs`
- `Assets/StateMachine.cs`
- `Assets/CharacterDecisionFoundation.cs`
- `Assets/ExplorationTurnManager.cs`
- `Assets/CombatTurnManager.cs`
- `Assets/UIController.cs`
- `Assets/PlayerPanelUI.cs`

## 4. Current `ActionPointCost` Usage

### 4.1 Declaration

`ActionPointCost` is declared on:

- `IInteraction`
- `IEnvironmentalAction`
- `ICombatAction`

all in `Assets/Objects/IInteractions.cs`.

### 4.2 Read Sites

Current important read paths:

- `PlayerController` AAM button labels
- `PlayerController.ExecutePlayerAction(...)`
- `PlayerController.ExecuteEnvironmentalAction(...)`
- `CombatResolver.AttackContext.ActionPointCost`
- `CombatResolver.ResolveAttack(...)`
- many interaction/environmental implementations that call `EndOfTurnManager.AddTurnProgress(ActionPointCost)`
- `Character.GetInteractionCacheKey(...)`

### 4.3 Why It Is Overloaded

The same numeric field currently means multiple things:

- combat AP cost
- exploration/world-time progress
- free-action classification when `0`
- UI label text shown as `AP`

That is the semantic overload this pass is preparing to replace.

## 5. Why A Multi-Axis Profile Is Needed

A single enum is not enough, because a live action can currently be multiple things at once.

Examples:

- exploration movement:
  - time-costing
  - legacy MP-backed
  - future stamina candidate
  - ends exploration turn

- combat attack:
  - legacy AP-backed
  - future stamina candidate
  - does not automatically end the combat turn

- Wait:
  - ends turn
  - costs world time in exploration
  - future recovery candidate
  - not a stamina-spending action

The profile must describe multiple axes without becoming the live execution authority.

## 6. `ActionCostProfile` Added

Added in:

- `Assets/Actions/ActionCostProfileResolver.cs`

Added model:

- `ActionCostProfile`

Fields:

- `IsFree`
- `WorldTimeCost`
- `LegacyActionPointCost`
- `LegacyMovePointCost`
- `EndsPlayerTurn`
- `CandidateForFutureStamina`
- `PredictedStaminaCost`
- `IsContextual`
- `CostLabel`
- `Notes`

Important rule:

- this metadata does not spend AP
- this metadata does not spend MP
- this metadata does not spend stamina
- this metadata does not decide if an action is allowed

## 7. Profile Builder / Helper Location

Added helper:

- `ActionCostProfileResolver`

Location:

- `Assets/Actions/ActionCostProfileResolver.cs`

Builder entry points added:

- `BuildForMovement(bool isCombatContext)`
- `BuildForWaitOrEndTurn(bool isCombatContext)`
- `BuildForInteraction(IInteraction interaction, bool isCombatContext)`
- `BuildForEnvironmentalAction(IEnvironmentalAction action, bool isCombatContext)`
- `BuildForCombatAttackContext(AttackContext context)`

Utility methods added:

- `BuildActionButtonLabel(...)`
- `LogPredictedCost(...)`

## 8. Actions Classified

### 8.1 Free / Informational

Classified as free:

- `Inspect`
- `Inspect Items`
- `Inspect NPC`
- `Look`
- `Examine`
- `View Village Sign Post`

Metadata pattern:

- `IsFree = true`
- `WorldTimeCost = 0`
- `LegacyActionPointCost = 0`
- `LegacyMovePointCost = 0`
- `EndsPlayerTurn = false`
- `CandidateForFutureStamina = false`
- `PredictedStaminaCost = 0`

### 8.2 Movement

Movement is now profiled centrally for diagnostics through:

- `BuildForMovement(...)`

Exploration movement metadata:

- world-time costing
- legacy move-point backed
- ends exploration turn
- future stamina candidate
- predicted stamina cost `1`

Combat movement metadata:

- legacy move-point backed
- does not end combat turn
- future stamina candidate
- predicted stamina cost `1`

### 8.3 Wait / End Turn

Wait / End Turn is now profiled centrally for diagnostics through:

- `BuildForWaitOrEndTurn(...)`

Metadata:

- not free
- ends current turn
- exploration Wait has `WorldTimeCost = 1`
- combat End Turn has `WorldTimeCost = 0`
- not a future stamina-spending action
- predicted stamina cost `0`
- future recovery candidate only

### 8.4 Basic Physical Combat Actions

Classified:

- `Punch`
- `Slash`
- `Stab`
- `Bash`
- `Rend`

Metadata:

- legacy AP-backed
- future stamina candidate
- predicted stamina cost `4`
- no turn-flow change

Shared physical attack diagnostics are emitted from:

- `CombatResolver.ResolveAttack(...)`

### 8.5 Time-Costing Environmental / Work Actions

Classified as time-costing via `BuildForEnvironmentalAction(...)` when they have positive current cost.

Examples:

- `Dig`
- `Till Soil`
- `Plant Seeds`
- `Fish`
- `Drink`
- `Claim Land`
- `Place Wooden Wall`
- `Place Wooden Door`
- `Place Anvil`
- `Place Bed`

Examples from interaction path with positive cost:

- `Chop`
- `Gather`
- `Mine`
- `Pick Flower`
- `Cut`
- `Open Door`
- `Close Door`
- `Extinguish`
- `Light Campfire`
- `Clear with Shovel`
- `Clear with Pickaxe`
- `Feed Animal`
- `Tame Animal`
- `Mount`

Metadata pattern:

- `WorldTimeCost = ActionPointCost`
- `LegacyActionPointCost = ActionPointCost`
- `CostLabel = "Takes time"`
- future stamina candidate only for clearly exertive work

## 9. Actions Left Contextual / Unknown

Left intentionally contextual:

- `Talk`
- `Trade`
- `Pickpocket`
- `Shove`
- `Pet`
- `Shake`
- `Open Chest`
- `Take Ear`
- `Donate`
- `Smith`
- `Craft`
- `Cook at ...`
- `Open Container`
- `Empty Container`
- `Ascend`
- `Descend`
- `Enter Dungeon`
- `Enter Cave`
- `Pick Up Items`
- `Pick Up All Items`
- all `IItemInteraction` item actions

Reason:

- many of these still use `ActionPointCost = 0` even when comments imply time
- some are panel-opening/initiation actions rather than true work resolution
- travel transitions likely need future time/stamina semantics, but current live execution is still zero-cost
- item actions still have no declared cost interface at all

Special note:

- `Shove` is left contextual, but marked as a future exertion candidate with a predicted stamina cost for diagnostics only

## 10. Predicted Stamina Diagnostics

Diagnostics added under:

- `CODEXLOG007_ACTION_COST_PROFILE`

Added execute-time / request-time diagnostics for:

- player movement
- Wait / End Turn
- player interaction execution
- environmental action execution
- shared physical combat attack resolution

Diagnostic wording explicitly includes:

- `PredictedStaminaCost only; not enforced.`

Starter predicted values used:

- movement: `1`
- basic physical attack: `4`
- moderate work: `4`
- heavy work: `6`
- very heavy work: `8`
- free / Wait / End Turn: `0`
- contextual unknowns: `Unknown`

These values are not balancing decisions. They are pipeline diagnostics only.

## 11. AAM / Action Label Changes

Presentation changed in a narrow way.

Updated AAM label building now uses `ActionCostProfile.CostLabel` instead of blindly appending `AP`.

Current presentation result:

- free / informational actions: no cost label
- non-combat time-costing actions: `Takes time`
- combat AP actions: still show `X AP`
- contextual / unknown actions: no cost label

This changed only label semantics.

It did not change:

- availability
- execution
- AP spend
- MP spend
- turn completion

## 12. What Gameplay Did Not Change

This pass did not change:

- `PlayerController` action authority
- `CombatResolver` AP spending
- exploration movement execution
- combat movement execution
- Wait / End Turn flow
- action availability
- stamina spending
- stamina gating
- turn flow

AP and MP remain the live resource authorities.

Stamina still affects nothing live.

## 13. Future Migration Path

Recommended next path:

1. Expand metadata coverage to more actions and item actions.
2. Add explicit movement / wait / attack profile surfaces to more UI or debug tools if needed.
3. Add predicted stamina diagnostics for more NPC/world actions if useful.
4. Introduce recovery preview semantics.
5. Only then consider soft stamina integration or warning-only behaviours.

Do not skip directly from this pass to AP/MP removal.

## 14. Risks

- `ActionPointCost` is still the live overloaded numeric field
- contextual zero-cost actions still do not have reliable world-time semantics
- `PlayerStats` still mirrors AP/MP/stamina and remains a desync risk
- `IItemInteraction` still has no declared cost metadata interface
- `Magic Attack` remains semantically messy because its path still combines combat AP semantics with direct time-progress code
- some actions now display `Takes time` while current execution still uses the same overloaded numeric field for both AP gating and world-time progression underneath

## 15. Manual Test Plan

1. Start the game.
2. Open the exploration AAM.
3. Confirm free actions like `Inspect` do not show fake `AP` costs.
4. Confirm positive-cost exploration actions like `Dig` show `Takes time`, not `AP`.
5. Confirm movement buttons still behave exactly as before.
6. Execute `Inspect`.
7. Confirm no turn advancement if that is the current intended behaviour.
8. Execute exploration movement.
9. Confirm movement and world advancement behave exactly as before.
10. Press `Wait`.
11. Confirm Wait/End Turn behaviour is unchanged.
12. Enter combat.
13. Confirm `Punch` / `Slash` / other combat attacks still show `AP` as before.
14. Execute combat movement.
15. Confirm MP behaviour is unchanged.
16. Execute a basic combat attack.
17. Confirm AP spending is unchanged.
18. Confirm stamina is not spent.
19. Check `GameDebugger` output for `CODEXLOG007_ACTION_COST_PROFILE` entries.
20. Confirm no action availability changed.

## 16. Recommended Next Phase

Recommended next Codex pass:

- expand metadata coverage for item actions and more contextual interactions
- decide which zero-cost contextual actions should become true free actions vs true time-costing actions
- isolate magic action semantics from the current AP + `AddTurnProgress(...)` overlap
- optionally add a debug view for resolved `ActionCostProfile` output

