# Action Cost Semantics Audit

## 1. Purpose

This pass audits TinyAdventure's current action-cost semantics so future stamina integration can happen without breaking:

* free actions
* exploration time flow
* combat AP/MP behaviour
* movement behaviour
* Wait/End Turn behaviour
* deterministic turn flow

This is not a stamina gameplay migration.

This pass does not:

* spend stamina
* replace AP
* replace MP
* change action costs
* change movement behaviour
* change combat behaviour
* change turn flow

## 2. Docs Read

Hard constraints read first:

* `Docs/Coding Rules.txt`
* `Docs/TinyAdventure Core Simulation Semantics.txt`
* `Docs/Stamina Direction And Foundation Pass.md`
* `Docs/Player Input And Simulation Turn Policy.md`
* `Docs/Character Decision Foundation Pass.md`

Recent related docs also read:

* `Docs/Combat Resolver Pass.md`
* `Docs/Player End Turn Action.md`
* `Docs/Turn Determinism Pass.md`
* `Docs/Combat Exploration Resume Repair.md`

## 3. Current Action-Cost System

TinyAdventure currently has multiple parallel action-cost systems:

### 3.1 `IInteraction.ActionPointCost`

Used by:

* `IInteraction`
* `IEnvironmentalAction`
* `ICombatAction`

Current live meanings:

* combat AP cost
* exploration/world-time progress amount
* free-action classification when `0`
* longer-than-one-turn world-time progression when `2`
* AAM button label text such as `"(1 AP)"`

### 3.2 Explicit movement handling in `PlayerController`

Player movement does not rely on `ActionPointCost`.

It uses:

* `DeductMovePoints(1)` for turn-managed movement
* `CompleteExplorationTurnForTimeCostingAction(..., 1f)` for exploration movement
* open-turn combat movement semantics in combat

This is currently the clearest live action-cost path in the project.

### 3.3 Explicit Wait / End Turn handling

Wait/End Turn also does not rely on `ActionPointCost`.

It uses:

* `PlayerController.HandleWaitOrEndTurn(...)`
* `CompleteExplorationTurnForTimeCostingAction(..., 1f)` in exploration
* `EndPlayerTurn(...)` in combat

### 3.4 Player AP wrapper path

Positive-cost `IInteraction` and `IEnvironmentalAction` actions are also gated through `PlayerStats.ActionPoints` in:

* `PlayerController.ExecutePlayerAction(...)`
* `PlayerController.ExecuteEnvironmentalAction(...)`

That means many non-combat actions still use an AP affordability check even when their actual semantic purpose is exploration time progression.

### 3.5 Item actions

`IItemInteraction` still has no live cost model in execution.

A non-breaking metadata helper now exists separately in the action-cost resolver, but the inventory UI does not yet consume it.

Inventory item actions are effectively free by omission because they:

* do not expose cost semantics
* do not spend AP
* do not spend MP
* do not advance world time

## 4. Current Uses Of `ActionPointCost`

`ActionPointCost` is currently used in four different ways:

### 4.1 As combat AP

Examples:

* `Punch`
* `Slash`
* `Stab`
* `Bash`
* `Rend`
* `Magic Attack`

### 4.2 As exploration/world-time progression

Many interactions call:

* `EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost)`

That means:

* `0` means no time advance
* `1` means one full world-turn worth of progress
* `2` means two full world-turn units of progress

### 4.3 As free-action classification

Many interactions use:

* `ActionPointCost => 0`

and are therefore free in practice because they still call `AddTurnProgress(0)`.

### 4.4 As AAM presentation text

The AAM currently formats any positive cost as `AP` in button labels even outside combat.

So an exploration-only world action can be shown as:

* `Open Door (1 AP)`
* `Dig (1 AP)`

even when the semantic meaning is actually world-time progression, not combat AP.

## 5. Direct Answers To The Audit Questions

### 5.1 Where are player actions/interactions defined?

Primary live action definitions are in:

* `Assets/Objects/IInteractions.cs`
* `Assets/Actions/ActionManager.cs`
* `Assets/Objects/Character.cs`
* `Assets/Objects/NPC.cs`
* `Assets/Objects/Animal.cs`
* `Assets/Objects/Objects.cs`
* `Assets/Objects/Item Actions.cs`

Runtime dispatch mainly happens through:

* `PlayerController.UpdateAdaptiveActionMenu()`
* `PlayerController.ExecutePlayerAction(...)`
* `PlayerController.ExecuteEnvironmentalAction(...)`
* `InventoryUI.PerformAction(...)`

### 5.2 How does `IInteraction.ActionPointCost` work today?

It is overloaded.

Current live behaviour:

* many interactions use it as world-time progress
* combat interactions use it as AP cost
* the AAM shows it as AP text
* zero-cost actions use it as free-action classification
* some longer actions use `2`, which effectively means multi-turn world-time progression

### 5.3 Which actions are free?

Current live free actions include:

* `Inspect`
* `Inspect Items`
* `Inspect NPC`
* `Talk`
* `Trade`
* `Pickpocket`
* `Shove`
* `Pet`
* `Shake`
* `Open Chest`
* `Take Ear`
* `View Village Sign Post`
* `Donate`
* `Smith`
* `Craft`
* `Cook`
* `Open Container`
* `Empty Container`
* `Ascend`
* `Descend`
* `Enter Dungeon`
* `Enter Cave`
* `Pick Up Items`
* `Pick Up All Items`
* inventory item actions such as consume/equip/unequip/drop/deseed/make active/deactivate

Important note:

many of these are only free because their numeric cost is `0`, even when comments say they should take a fraction of a turn or half a turn.

### 5.4 Which actions consume AP in combat?

Current live combat AP actions:

* `Punch`
* `Slash`
* `Stab`
* `Bash`
* `Rend`
* `Magic Attack`

Shared physical attacks currently resolve through `CombatResolver` with a default AP cost of `2`.

### 5.5 Which actions advance time in exploration?

Current live exploration time-costing actions include:

* player movement through `CompleteExplorationTurnForTimeCostingAction(..., 1f)`
* Wait through `CompleteExplorationTurnForTimeCostingAction(..., 1f)`
* any interaction/environmental action that calls `AddTurnProgress(ActionPointCost)` with cost `> 0`

Examples:

* `Chop`
* `Gather`
* `Mine`
* `Pick Flower`
* `Cut`
* `Open Door`
* `Close Door`
* `Extinguish`
* `Light Campfire`
* `Clear with Shovel`
* `Clear with Pickaxe`
* `Feed Animal`
* `Tame Animal`
* `Mount`
* `Dig`
* `Till Soil`
* `Plant Seeds`
* `Harvest`
* `Fish`
* `Drink`
* `Claim Land`
* construction actions such as placing walls/doors/anvils/beds

### 5.6 Which actions consume movement points?

Current live MP consumers:

* player movement in turn-managed exploration
* player movement in combat
* NPC/animal/monster movement through state-machine/world-decision paths

Important nuance:

* exploration movement both spends MP and completes the player exploration turn
* combat movement spends MP but does not automatically end the turn

That means MP in exploration currently exists as a compatibility detail, not the primary semantic authority.

### 5.7 Which actions are full-turn actions?

Current live full-turn actions:

* `Wait` in exploration
* any exploration action that advances world time by `1`
* exploration movement, because it explicitly completes the player turn after success

Longer-than-full-turn actions:

* actions with `ActionPointCost => 2` that call `AddTurnProgress(ActionPointCost)`
* examples include `Clear with Pickaxe`, `Tame Animal`, and `Place Anvil`

Combat `End Turn` is also a full-turn completion control, but it is explicit turn-ending UI logic rather than a generic cost field.

### 5.8 Which actions are contextual?

Strong contextual candidates:

* `Talk`
* `Trade`
* `Pickpocket`
* `Open Container`
* `Empty Container`
* `Open Chest`
* `Pick Up Items`
* `Pick Up All Items`
* `Open Door`
* `Close Door`
* `Ascend`
* `Descend`
* `Enter Dungeon`
* `Enter Cave`
* inventory item actions

These should not all be hardcoded as permanently free or permanently stamina-costing.

### 5.9 Which actions are misclassified or ambiguous?

Main misclassifications and ambiguities:

* many interactions comment that they take time, but use `ActionPointCost => 0`
* AAM labels positive-cost exploration actions as `AP`
* exploration interactions with cost `> 0` require player AP affordability even though exploration turns do not reset AP
* `Magic Attack` both uses combat AP and calls `AddTurnProgress(ActionPointCost)`
* `BaseCombatInteraction` still contains a legacy `AddTurnProgress(ActionPointCost)` path
* inventory item actions have no declared cost semantics at all

### 5.10 Which actions should eventually use stamina?

Strong future stamina candidates:

* movement
* sprint-like movement if added
* melee attacks
* heavy weapon attacks
* shoving
* chopping
* mining
* digging
* clearing rubble/stone
* some mounting/dismounting/carrying actions
* forcing heavy physical obstacles if added later

### 5.11 Which actions should never use stamina?

Actions that should never use stamina:

* inspect/read/look/status actions
* UI tab switching
* opening inventory
* dialogue UI opening itself
* trade UI opening itself
* container UI opening itself
* purely administrative ownership/claim confirmations
* equip/unequip/make-active/deactivate as inventory admin actions

If any of those later cost time, that still does not mean they should cost stamina.

### 5.12 Which actions should remain free?

Recommended free set:

* inspect-style information actions
* sign reading
* opening container UI
* opening inventory
* item equipment management
* item activation/deactivation toggles
* other non-physical UI/admin actions

### 5.13 Which actions should cost time but not stamina?

Strong time-but-not-stamina candidates:

* Wait
* meaningful dialogue
* trade
* looting / transferring many items
* claim land
* entering/exiting areas
* opening/closing ordinary doors if not physically taxing
* consuming or using non-strenuous items

### 5.14 Which actions should cost stamina but maybe not AP later?

Strong candidates:

* melee attacks
* movement
* shove
* chop
* mine
* dig
* clear rubble/stone
* heavy construction placement
* mount if treated as a physical effort action

### 5.15 Which actions should remain tactical/combat-only?

Current and future tactical/combat-only actions:

* `Punch`
* `Slash`
* `Stab`
* `Bash`
* `Rend`
* `Magic Attack`
* combat movement budget semantics
* combat `End Turn`

## 6. Major Ambiguities Found

### 6.1 `ActionPointCost` is overloaded

This is confirmed.

It currently stands for multiple different things and should not stay as the long-term semantic source of truth.

### 6.2 Positive-cost exploration actions still use player AP checks

`ExecutePlayerAction(...)` and `ExecuteEnvironmentalAction(...)` both check:

* `PlayerStats.Instance.ActionPoints >= actionPointCost`

That means exploration world actions with positive cost still depend on AP affordability.

### 6.3 Exploration does not reset player AP

`ExplorationTurnManager.OnPlayerTurnStart(...)` resets move points, not action points.

So positive-cost exploration interactions are tied to a player AP wrapper that is primarily refreshed in combat.

This is one of the strongest signs that current action-cost semantics are unsafe.

### 6.4 AAM cost labels are misleading

The AAM labels positive costs as `AP` regardless of whether the action is actually:

* a combat AP action
* an exploration time-costing action
* a longer multi-turn world action

### 6.5 Some combat code still mixes AP and turn-time semantics

`MagicInteraction` is the clearest example:

* it declares `ActionPointCost => 2`
* it is treated as a combat AP action
* it also calls `EndOfTurnManager.AddTurnProgress(ActionPointCost)`

That is semantically unsafe in combat.

### 6.6 Item actions have no live declared cost semantics

Inventory item actions currently bypass the live cost model entirely.

Metadata-only classification exists separately, but there is still no enforced item-action authority path.

That means the project does not yet have one unified place to ask:

* is this free?
* does this advance time?
* does this end the turn?
* is this physical?
* should this later use stamina?

## 7. Proposed Action Cost Model

Do not keep using one overloaded integer as the full semantic model.

Recommended direction:

* keep legacy AP and MP live for compatibility
* introduce a small documentation-first `ActionCostProfile`
* make the model multi-axis instead of single-field

### 7.1 Recommended profile shape

```csharp
public sealed class ActionCostProfile
{
    public bool IsFree { get; init; }
    public int WorldTimeCost { get; init; }
    public int LegacyActionPointCost { get; init; }
    public int LegacyMovePointCost { get; init; }
    public int FutureStaminaCost { get; init; }
    public bool EndsPlayerTurn { get; init; }
    public bool CombatOnly { get; init; }
    public bool ExplorationOnly { get; init; }
    public bool IsContextual { get; init; }
    public bool CandidateForFutureStamina { get; init; }
    public string Notes { get; init; }
}
```

### 7.2 Why this shape is safer than a single enum

A single category is not enough because some actions are simultaneously:

* time-costing and full-turn
* time-costing and legacy-movement-costing
* combat-only and legacy-AP-costing
* contextual and possible future stamina candidates

Examples:

* exploration movement is time-costing, ends the exploration turn, and currently also spends MP
* combat movement is legacy-movement-costing, combat-only, and should later become a stamina candidate
* melee attack is legacy-AP-costing now and likely future stamina-costing later

### 7.3 Semantic tags to use in docs/reviews

Useful semantic labels:

* `Free`
* `TimeCosting`
* `LegacyAPCost`
* `LegacyMovementCost`
* `FullTurn`
* `Contextual`
* `CandidateForFutureStamina`

These are best treated as semantic tags or profile traits, not mutually exclusive modes.

## 8. Audit Table Of Key Actions

| Action | Current Definition | Current Live Cost | Current Live Meaning | Ambiguity / Risk | Recommended Future Profile |
|---|---|---:|---|---|---|
| Inspect | `IInteractions.cs` | 0 | Free | Safe | Free |
| Inspect Items | `IInteractions.cs` | 0 | Free | Safe | Free |
| Talk | `IInteractions.cs` | 0 | Free | Comment says time-ish, numeric says free | Contextual, usually time-but-not-stamina |
| Trade | `IInteractions.cs` | 0 | Free | Comment says half-turn, numeric says free | Contextual, usually time-but-not-stamina |
| Pickpocket | `IInteractions.cs` | 0 | Free | Physically meaningful but free now | Contextual; could later cost time and maybe stamina |
| Shove | `IInteractions.cs` | 0 | Free | Physical effort action marked free | Candidate for future stamina |
| Open Container | `IInteractions.cs` | 0 | Free | Probably UI/admin, okay as free | Free or contextual |
| Empty Container | `IInteractions.cs` | 0 | Free | Bulk loot transfer is fully free | Contextual, likely time-costing |
| Pick Up Items | `IInteractions.cs` | 0 | Free | Loot transfer is fully free | Contextual, likely time-costing later |
| Open Door | `IInteractions.cs` | 1 | One world-turn + AP-gated wrapper path | Comment says small fraction; AAM shows AP | TimeCosting contextual action |
| Close Door | `IInteractions.cs` | 1 | One world-turn + AP-gated wrapper path | Same issue as open | TimeCosting contextual action |
| Chop | `IInteractions.cs` | 1 | One world-turn + AP-gated wrapper path | Mostly okay semantically, wrong label | TimeCosting + future stamina candidate |
| Gather | `IInteractions.cs` | 1 | One world-turn + AP-gated wrapper path | Probably okay | TimeCosting, maybe no stamina |
| Mine | `IInteractions.cs` | 1 | One world-turn + AP-gated wrapper path | Physical effort should later use stamina | TimeCosting + future stamina candidate |
| Clear with Pickaxe | `IInteractions.cs` | 2 | Two world-turn units + AP-gated wrapper path | AAM still says AP | TimeCosting multi-turn + future stamina candidate |
| Feed Animal | `IInteractions.cs` | 1 | One world-turn + AP-gated wrapper path | Likely time, not stamina | TimeCosting |
| Tame Animal | `IInteractions.cs` | 2 | Two world-turn units + AP-gated wrapper path | Could be mostly time/social, not raw stamina | Contextual time-costing |
| Mount | `IInteractions.cs` | 1 | One world-turn + AP-gated wrapper path | Could be physical but small | Contextual, maybe future stamina candidate |
| Dig | `IInteractions.cs` | 1 | One world-turn + AP-gated wrapper path | Should later be stamina candidate | TimeCosting + future stamina candidate |
| Fish | `IInteractions.cs` | 1 | One world-turn + AP-gated wrapper path | Probably time more than stamina | TimeCosting contextual |
| Drink | `IInteractions.cs` | 1 | One world-turn + AP-gated wrapper path | Drinking itself likely not stamina | TimeCosting, no stamina |
| Place Anvil | `IInteractions.cs` | 2 | Two world-turn units + AP-gated wrapper path | Heavy action shown as AP | TimeCosting + future stamina candidate |
| Punch / Slash / Stab / Bash / Rend | `IInteractions.cs`, `CombatResolver.cs` | 2 | Combat AP cost | Mostly correct current combat meaning | Combat-only, LegacyAPCost, future stamina candidate |
| Magic Attack | `IInteractions.cs` | 2 | Combat AP plus `AddTurnProgress(2)` | Semantically wrong in combat | Combat-only, LegacyAPCost; not world-time |
| Exploration Move | `PlayerController.cs` | explicit | MP spend + explicit world-time + explicit turn completion | Best current model | TimeCosting + LegacyMovementCost + FullTurn + future stamina candidate |
| Combat Move | `PlayerController.cs` | explicit | MP spend only, turn stays open | Best current model | Combat-only + LegacyMovementCost + future stamina candidate |
| Wait | `PlayerController.cs`, `EndOfTurnManager.cs` | explicit | full exploration turn | Safe | FullTurn + TimeCosting |
| End Turn | `PlayerController.cs` | explicit | manual combat turn completion | Safe | FullTurn + CombatOnly |
| Inventory item actions | `Item Actions.cs` | none | free by omission | no declared semantics | Mostly Free or Contextual; should be explicitly modeled later |

## 9. Which Actions Are Free

Current live free actions:

* inspect-style actions
* open container UI
* read/view sign
* most social UI entry actions
* most current loot transfer actions
* most area-transition interactions
* inventory item actions

Recommended durable free actions:

* inspect/read/status/look
* open inventory
* open UI panels
* equip/unequip/admin inventory actions

## 10. Which Actions Are Time-Costing

Current live time-costing actions:

* exploration movement
* Wait
* all `ActionPointCost > 0` interactions that call `AddTurnProgress(ActionPointCost)`

Recommended time-costing actions long-term:

* exploration movement
* Wait
* harvesting/gathering/work actions
* meaningful dialogue/trade/loot actions
* door and area transitions where appropriate

## 11. Which Actions Currently Use AP

Current live AP usage:

* combat turn reset through `CombatTurnManager`
* physical combat actions through `CombatResolver`
* player wrapper AP gating in `ExecutePlayerAction(...)`
* player wrapper AP gating in `ExecuteEnvironmentalAction(...)`

Important problem:

positive-cost exploration actions currently use player AP gating even though their semantic role is not combat AP.

## 12. Which Actions Currently Use MP

Current live MP usage:

* player movement in exploration
* player movement in combat
* NPC/animal/monster movement on their turns

Important note:

exploration movement still spends MP, but the semantic reason the turn ends is explicit exploration time completion, not MP depletion.

## 13. Which Actions May Eventually Use Stamina

Strong candidates:

* exploration movement
* combat movement
* melee attacks
* shove
* chop
* mine
* dig
* clear rubble/stone
* heavy construction placement
* other strenuous physical work

## 14. Which Actions Should Never Use Stamina

Should never use stamina:

* inspect/look/read
* status review
* UI navigation
* opening inventory
* opening container UI
* trade UI opening
* dialogue UI opening
* equip/unequip/admin inventory actions

## 15. Recommended Staged Migration Path

### Stage 1

Keep this as documentation and audit only.

Do not change gameplay yet.

### Stage 2

Introduce documentation-first `ActionCostProfile` semantics and start tagging key actions conceptually.

No spend logic yet.

### Stage 3

Stop using `ActionPointCost` as a universal semantic name in new work.

Prefer:

* world-time cost
* legacy AP cost
* legacy movement cost
* future stamina candidate

### Stage 4

Audit and isolate the worst mixed paths:

* `MagicInteraction`
* positive-cost exploration interactions that still require player AP
* item actions with no declared cost semantics

### Stage 5

Update AAM presentation so non-combat exploration actions do not display fake `AP` labels.

### Stage 6

Add predicted stamina-cost metadata to a small number of physical actions without enforcing it.

### Stage 7

Once the cost model is stable, selectively migrate one physical action family at a time.

## 16. Files Inspected

* `Assets/Objects/IInteractions.cs`
* `Assets/PlayerController.cs`
* `Assets/EndOfTurnManager.cs`
* `Assets/CombatResolver.cs`
* `Assets/Objects/Character.cs`
* `Assets/StateMachine.cs`
* `Assets/CharacterDecisionFoundation.cs`
* `Assets/ExplorationTurnManager.cs`
* `Assets/CombatTurnManager.cs`
* `Assets/Actions/ActionManager.cs`
* `Assets/Objects/Item Actions.cs`
* `Assets/UI/InventoryUI.cs`
* `Assets/Objects/NPC.cs`
* `Assets/Objects/Animal.cs`
* `Assets/Objects/Objects.cs`
* related docs listed above

## 17. Files Changed

* `Docs/Action Cost Semantics Audit.md`

No gameplay code was changed in this pass.

## 18. Risks

Main risks:

* `ActionPointCost` remains overloaded until a later implementation pass
* positive-cost exploration actions still rely on player AP affordability
* exploration player AP is not reset each exploration turn
* AAM cost labels remain misleading for non-combat actions
* `MagicInteraction` still mixes combat AP and world-time progression
* item actions still have no explicit cost semantics

## 19. Recommended Next Phase

Recommended next phase:

1. keep gameplay unchanged
2. add a tiny shared semantic description layer or metadata type only after agreeing on the profile shape
3. first fix naming/presentation and semantic ownership
4. only after that, begin predicted stamina-cost tagging for selected physical actions

The safest immediate follow-up is a non-gameplay implementation pass that:

* introduces `ActionCostProfile` metadata
* updates AAM presentation to stop calling all positive costs `AP`
* separates exploration world-time cost from combat AP cost

That should happen before any stamina-spending migration.
