# Contextual Action Cost Classification Pass

## 1. Purpose And Scope

This pass refines the existing `ActionCostProfile` metadata so ambiguous actions are described more accurately without changing gameplay execution.

It keeps the live authority model unchanged:

- AP remains the current combat action authority
- MP remains the current movement budget authority
- world time remains the turn-progress authority
- stamina remains metadata only

No action execution was intentionally changed by this pass.

## 2. Documents Read

Read before editing:

- `Docs/Coding Rules.txt`
- `Docs/TinyAdventure Core Simulation Semantics.txt`
- `Docs/Stamina Direction And Foundation Pass.md`
- `Docs/Stamina Model Shaping Pass.md`
- `Docs/Action Cost Semantics Audit.md`
- `Docs/ActionCostProfile Metadata Pass.md`
- `Docs/Player Input And Simulation Turn Policy.md`

Recent related documents also checked:

- `Docs/Combat Resolver Pass.md`
- `Docs/Combat Runtime Repair Pass.md`
- `Docs/Combat Exploration Resume Repair.md`
- `Docs/Player End Turn Action.md`
- `Docs/Turn Determinism Pass.md`
- `Docs/Character Decision Foundation Pass.md`
- `Docs/NPC World Thought Process And Affordances.md`

## 3. Current Live Authority

### AP

Combat AP is still live authority in combat.

Relevant live paths:

- `CombatResolver.ResolveAttack(...)`
- `PlayerController.ExecutePlayerAction(...)`
- combat interaction classes in `Assets/Objects/IInteractions.cs`

### MP

Movement points are still live authority for movement budgets.

Relevant live paths:

- `PlayerController.RequestPlayerMove(...)`
- `DeductMovePoints(...)`
- combat and exploration movement handling

### World Time

World time remains live authority for turn progression.

Relevant live paths:

- `EndOfTurnManager.AddTurnProgress(...)`
- exploration movement completion
- Wait / End Turn handling
- many interaction and environmental action implementations in `Assets/Objects/IInteractions.cs`

### Turns

Turn completion is still managed by the current exploration/combat turn managers and `TurnOrchestrator`.

## 4. Actions Audited

Audited action families:

- social actions
- container and loot actions
- work and gathering actions
- travel and transition actions
- item interactions
- magic attacks and related combat actions

## 5. Free Actions

Current free metadata:

- `Inspect`
- `Inspect Items`
- `Inspect NPC`
- `Look`
- `Examine`
- `View Village Sign Post`

Item administration actions are also treated as free metadata in the new item helper:

- `Equip`
- `Unequip`
- `Make Active`
- `Deactivate`
- `Drop`
- `Deseed`

These actions remain free by current semantics and are not stamina candidates.

## 6. AP-Backed Actions

Current AP-backed metadata:

- `Punch`
- `Slash`
- `Stab`
- `Bash`
- `Rend`
- `Magic Attack`

Magic Attack remains AP-backed in presentation, but its execution also advances world time. That overlap is recorded as design debt and was not repaired.

## 7. MP-Backed Actions

Current MP-backed metadata:

- exploration movement
- combat movement

The movement profile remains metadata-only and does not change how movement spends points.

## 8. Time-Costing Actions

Current time-costing metadata:

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

These continue to use the current overloaded live numeric cost paths where the source code already does so.

## 9. Contextual Actions

Current contextual metadata:

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
- `Pick Up Item`
- `Pick Up All Items`
- `Consume`
- any unclassified `IItemInteraction` item actions

The resolver now keeps these actions contextual rather than pretending they are all free or all stamina-backed.

## 10. Future Stamina Candidates

Marked as future stamina candidates in metadata:

- movement
- `Punch`
- `Slash`
- `Stab`
- `Bash`
- `Rend`
- `Shove`
- `Chop`
- `Gather`
- `Mine`
- `Pick Flower`
- `Cut`
- `Clear with Shovel`
- `Clear with Pickaxe`
- `Feed Animal`
- `Tame Animal`
- `Mount`
- `Dig`
- `Till Soil`
- `Plant Seeds`
- `Fish`
- `Place Anvil`

These are prediction-only markers. They are not enforced costs.

## 11. Explicitly Not Stamina-Backed

Explicitly not stamina-backed:

- inspect / look / read style actions
- UI entry actions
- trade panel opening
- dialogue panel opening
- item admin actions such as equip and unequip
- wait / end turn itself
- current combat turn completion controls

## 12. Item-Action Findings

Item interactions still do not have a live cost authority path in the inventory UI.

What changed:

- a non-breaking resolver helper now exists: `BuildForItemInteraction(IItemInteraction interaction)`
- inventory administration actions are classified as free metadata there
- `Consume` remains contextual because the interface still has no live cost model

What remains unresolved:

- item UI does not yet consume the metadata helper
- `IItemInteraction` still does not expose an explicit cost contract
- read / throw / use variants are not present in the current codebase and were not added

## 13. Magic Attack Findings

Magic Attack remains execution-compatible and was not rewritten.

Current behavior:

- combat AP is still spent as before
- direct turn progress is still added as before

Metadata now records the overlap:

- AP-backed
- also advances world time in its direct execution path
- future stamina use remains intentionally uncertain

## 14. Resolver Changes

Changed in `Assets/Actions/ActionCostProfileResolver.cs`:

- added `BuildForItemInteraction(IItemInteraction interaction)`
- corrected the environmental pickup name match for `Pick Up Item`
- updated Magic Attack metadata to record the AP plus world-time overlap
- preserved all existing live execution paths

No new live cost system was introduced.

## 15. Presentation Changes

Current presentation remains narrow:

- free actions show no cost label
- time-costing non-combat actions can show `Takes time`
- combat AP actions can show `X AP`
- contextual actions can show no label

No new UI panels or action flows were added.

## 16. Diagnostic Changes

Diagnostics remain under `CODEXLOG007_ACTION_COST_PROFILE`.

Current diagnostic wording includes:

- `PredictedStaminaCost only; not enforced.`

The diagnostics are metadata-only and do not enforce stamina.

## 17. Behaviour Confirmation

Gameplay behavior was intentionally left unchanged for:

- action execution
- action availability
- AP spending
- MP spending
- stamina spending
- movement
- combat
- turn flow
- nested-area transitions

## 18. Remaining Ambiguities

Remaining ambiguous areas:

- `Consume` could still later become time-costing, but it is not yet tied to a live cost authority
- the inventory item path still lacks a UI consumer for the new metadata helper
- `Magic Attack` still mixes AP and world-time semantics in live execution
- some contextual travel and container actions may still deserve later subdivision between free, time-costing, and UI-only categories

## 19. Static Validation

Performed without Unity:

- inspected the final diff
- ran `git diff --check`
- searched for resolver call sites
- verified no stamina deduction was added
- verified no AP or MP deduction was removed
- verified no action invocation was replaced
- verified the new item helper is non-breaking and unused by live UI paths

## 20. Manual Test Plan

1. Open the exploration AAM.
2. Confirm `Inspect` shows no fake `AP` cost.
3. Confirm `Talk`, `Trade`, and `Pickpocket` have no misleading AP label.
4. Confirm `Dig`, `Chop`, `Mine`, and similar work actions show `Takes time`.
5. Confirm `Pick Up Item` and `Pick Up All Items` do not show AP labels.
6. Open the inventory item UI.
7. Confirm equip / unequip / activate / deactivate / drop labels remain unchanged.
8. Execute a social action.
9. Execute a container or pickup action.
10. Execute a work action.
11. Execute a Magic Attack.
12. Confirm AP, MP, movement, and turn progression behave as before.
13. Confirm stamina is not deducted.
14. Inspect diagnostics for repeated or misleading `CODEXLOG007_ACTION_COST_PROFILE` messages.

## 21. Recommended Next Pass

Smallest logical follow-up:

1. wire the new item metadata helper into the inventory UI only if the team wants item labels surfaced there
2. otherwise, leave item metadata as documentation and diagnostics only
3. defer any `Consume` cost enforcement until the item interaction contract is expanded

