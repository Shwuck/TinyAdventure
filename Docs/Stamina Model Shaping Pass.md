# Stamina Model Shaping Pass

## 1. Purpose

This pass defines the intended long-term stamina model for TinyAdventure without migrating live gameplay to stamina yet.

The goal is to shape a future replacement path for the current AP/MP model while preserving:

- current movement behaviour
- current combat behaviour
- current turn flow
- current free-action behaviour
- current exploration smoothness

This is an architecture and semantics pass first, not a stamina gameplay pass.

## 2. Rules And Docs Read

Read before this pass:

- `Docs/Coding Rules.txt`
- `Docs/TinyAdventure Core Simulation Semantics.txt`
- `Docs/Stamina Direction And Foundation Pass.md`
- `Docs/Action Cost Semantics Audit.md`
- `Docs/Player Input And Simulation Turn Policy.md`
- `Docs/Character Decision Foundation Pass.md`
- `Docs/NPC World Thought Process And Affordances.md`
- `Docs/Combat Resolver Pass.md`
- `Docs/Player End Turn Action.md`
- `Docs/Turn Determinism Pass.md`

Prompt / rules folders checked:

- `Prompts/` not present
- `Docs/Prompts/` not present
- `In/` not present
- `Input/` not present
- `CodexPrompts/` not present

Hard constraints followed in this pass:

- one authoritative area roster
- deterministic turn ownership
- one bounded world decision per actor turn
- combat and exploration remain context policies over shared characters
- stamina remains distinct from time, readiness, speed, and health
- AP/MP stay live until a deliberate migration pass replaces them

## 3. Files Inspected

- `Assets/Objects/Character.cs`
- `Assets/PlayerStats.cs`
- `Assets/PlayerController.cs`
- `Assets/CombatResolver.cs`
- `Assets/Objects/IInteractions.cs`
- `Assets/Objects/Item Actions.cs`
- `Assets/Actions/ActionManager.cs`
- `Assets/EndOfTurnManager.cs`
- `Assets/ExplorationTurnManager.cs`
- `Assets/CombatTurnManager.cs`
- `Assets/BaseTurnManager.cs`
- `Assets/TurnOrchestrator.cs`
- `Assets/StateMachine.cs`
- `Assets/CharacterDecisionFoundation.cs`
- `Assets/PlayerPanelUI.cs`
- `Assets/PlayerCharacter.cs`
- `Assets/Generators/NPCGenerator.cs`
- `Assets/Generators/AnimalGenerator.cs`
- `Assets/Objects/Monster.cs`
- `Assets/SaveSystem.cs`

## 4. Current State

### 4.1 Stamina

Current stamina authority already exists on `Character`.

Current canonical fields:

- `Character.MaxStamina`
- `Character.CurrentStamina`

Current max formula:

`MaxStamina = max(10, 10 + round(GetStatValue("Constitution") * 2))`

Current initialization:

- player: `PlayerCharacterFactory.CreatePlayerCharacter()`
- NPCs: `NPCGenerator.GenerateNPC()` and related generation paths
- animals: `AnimalFactory.CreateAnimal()`
- monsters: `Monster` constructor

Current helper methods on `Character`:

- `CalculateMaxStamina()`
- `RecalculateMaxStamina(...)`
- `InitializeStamina(...)`
- `ResetStamina(...)`
- `ClampStamina(...)`
- `CanSpendStamina(...)`
- `SpendStamina(...)`
- `RestoreStamina(...)`
- `GetStaminaPercent()`
- `GetStaminaRecoveryPerTurn()`
- `GetStaminaRecoveryOnWait()`
- `RecoverStaminaForTurn(...)`
- `RecoverStaminaOnWait(...)`
- `RecoverStaminaOnRest(...)`
- `RecoverStaminaFully(...)`

Current live effect:

- none

Stamina currently does not affect:

- movement
- combat
- turn order
- turn completion
- NPC decisions
- action availability

### 4.2 AP / MP

Current live tactical resources remain:

- `Character.ActionPoints`
- `Character.MovePoints`
- mirrored in `PlayerStats`

Current reset behaviour:

- exploration player turn start resets player MP
- combat player turn start resets player AP and MP
- NPC exploration/combat turn execution resets NPC MP before `ExecuteTurnActions()`
- `Character.ExecuteTurnActions()` resets AP to `MaxActionPoints`

Current use:

- combat attacks spend AP through `CombatResolver.ResolveAttack()` -> `Character.SpendActionPoints(...)`
- combat movement spends MP through `PlayerController.DeductMovePoints(...)`
- exploration movement also spends MP, but exploration turn completion is now semantic time ownership, not MP depletion

### 4.3 Exploration / Combat Turn Semantics

Current important behaviour:

- exploration movement advances world time through `PlayerController.CompleteExplorationTurnForTimeCostingAction(...)`
- combat movement spends movement budget but does not automatically end the turn
- Wait in exploration routes to a time-costing turn completion
- End Turn in combat routes to manual player-turn completion

### 4.4 Action Cost Semantic Risk

`IInteraction.ActionPointCost` is still overloaded.

Today it is used to mean different things in different places:

- combat AP cost
- exploration/world-time progression
- free-action classification when cost is `0`
- UI label text `"(X AP)"`
- sometimes a rough "full turn" or "longer action" signal

This is the main semantic risk that stamina must not inherit.

## 5. Current AP / MP Role

The live model is still:

- combat uses AP plus MP as tactical per-turn budgets
- exploration uses explicit player turn completion for time-costing actions
- NPC exploration uses one bounded world decision, not an AP loop

This means AP/MP are already partly legacy semantics:

- AP is still authoritative in combat
- MP is still authoritative for movement limits
- exploration time is already conceptually separate from MP

That separation is useful groundwork for stamina, because it shows that:

- effort is not time
- movement budget is not automatically world-time
- combat budget and exploration time already need different presentation

## 6. End-State Stamina Model

### 6.1 MaxStamina

`MaxStamina` is the character's total endurance reserve.

It answers:

> How much total physical effort can this character sustain before exhaustion?

Future influences may include:

- Constitution
- species / race
- long-term fatigue
- hunger / thirst / sleep
- disease / poison
- anatomy condition
- traits
- buffs / debuffs
- injury state

### 6.2 CurrentStamina

`CurrentStamina` is the character's currently remaining physical effort reserve.

It answers:

> How exhausted is this character right now?

This is the resource that future exertive actions will reduce and recovery will restore.

### 6.3 Turn-Limited Exertion

The model needs a per-turn exertion cap in addition to total stamina.

This is not a second stamina pool.

Recommended internal name:

- `TurnExertionLimit`

Recommended computed availability name:

- `AvailableTurnStamina`

Recommended player-facing combat label:

- `Stamina This Turn`

Reasoning:

- `TurnExertionLimit` is semantically clear in code: it is a limit, not a pool
- `AvailableTurnStamina` is clear for calculation: `min(CurrentStamina, TurnExertionLimit)`
- `Stamina This Turn` is easier to read in UI than `Turn Exertion Limit`

Definition:

`AvailableTurnStamina = min(CurrentStamina, TurnExertionLimit)`

This answers:

> How much physical effort can this character exert in this turn window?

This is the natural tactical successor to the role AP/MP currently fill.

### 6.4 Recovery

Recovery is separate from maximum capacity and separate from the turn exertion cap.

Future recovery layers:

- passive recovery per turn / tick
- catch-breath recovery on Wait / End Turn
- stronger recovery while resting
- full or near-full recovery while sleeping
- modified by fatigue, injury, food, thirst, disease, traits, species, and buffs

### 6.5 Action Stamina Cost

Future physical actions should have stamina costs separate from time/readiness.

Examples:

- inspect: `0`
- normal step: low
- sprint: moderate or high
- slash: moderate
- heavy swing: high
- force door: high
- climb: high
- wait: recovery, not cost

## 7. Time / Readiness Distinction

Stamina is not:

- health
- time
- readiness
- speed

The intended split is:

- stamina answers "can this actor afford the effort?"
- time/readiness answers "when does this actor act again?"
- action-cost metadata answers "what does this action consume?"

The code should not collapse those into one number.

## 8. Exploration Experience

Exploration should remain soft and low-friction.

Desired player experience:

- inspect/look/status/inventory-style inputs remain free
- normal walking may eventually have low stamina cost, but should not feel like AP micromanagement
- exertive actions should matter more than routine movement
- wait/rest should become the main recovery affordances
- injury, encumbrance, armour, terrain, and chase situations should matter more than ordinary walking

Exploration should feel like:

- a simulation of exertion

not:

- a visible tactical AP puzzle every step

## 9. Combat Experience

Combat should expose stamina much more directly.

Desired combat presentation:

- show `CurrentStamina`
- show `Stamina This Turn`
- show stamina costs on strenuous actions
- limit turn exertion by `AvailableTurnStamina`
- deplete total stamina over repeated exertion
- use Wait / End Turn / catch breath as meaningful recovery choices later

Combat should feel like:

- "what can I physically afford this turn?"

not:

- "move twice, attack once"

## 10. What Stamina Should Not Do

Stamina should not:

- replace health
- replace readiness / speed / turn frequency
- make inspect/look/menu actions costly
- make exploration tedious
- hard-lock the player every time it reaches `0`
- become AP with a different label
- be calculated separately in movement, combat, and interaction code
- be owned by `PlayerStats` instead of `Character`

At low stamina, later behaviour should degrade options before it fully removes agency.

Better future patterns:

- heavy actions disappear first
- sprinting disappears first
- movement softens or slows
- weak fallback attacks remain
- recovery becomes important

## 11. Suggested Starter Formulas

### 11.1 MaxStamina

Recommendation:

- keep the current Constitution-derived formula for now

Current formula is good enough for the foundation phase because it is:

- simple
- stat-derived
- already implemented
- already canonical on `Character`
- easy to rebalance later

Recommended current formula to keep:

`MaxStamina = max(10, 10 + round(GetStatValue("Constitution") * 2))`

### 11.2 TurnExertionLimit

Recommendation:

- start flat, not deeply derived

Recommended starter formula:

`TurnExertionLimit = 10`

Why:

- easy to reason about
- easy to tune in combat
- avoids prematurely entangling stamina with speed, Dexterity, or readiness
- lets the team migrate semantics before balancing stats

Future modifiers can later adjust it by:

- anatomy
- injuries
- encumbrance
- armour
- traits
- buffs / debuffs
- stance
- fatigue

### 11.3 Recovery

Recommended conceptual starter model:

- passive turn recovery: very small
- Wait / End Turn catch-breath recovery: moderate
- rest recovery: large
- sleep recovery: full or near-full

Recommended direction for later tuning:

- passive combat turn recovery: `1`
- catch-breath / wait recovery: `2` to `4`
- rest recovery: percentage-based with a safe floor
- sleep recovery: full restore

The existing `Character` recovery helpers are acceptable placeholders, but they should be treated as provisional until recovery is integrated intentionally.

## 12. Where Stamina Should Plug In Later

The natural future integration points are:

- a central action-cost metadata/resolution layer
- `PlayerController.RequestPlayerMove(...)` for predicted exploration and combat movement costs
- `PlayerController.ExecutePlayerAction(...)` / `ExecuteEnvironmentalAction(...)`
- `Character.PerformAttack(...)` / `CombatResolver`
- Wait / End Turn recovery entry points
- future climb, sprint, forced movement, heavy-object, and terrain systems

It should not first plug in through:

- `PlayerStats` ownership
- UI-only numbers
- per-interaction ad hoc formulas
- direct stamina checks scattered across `IInteraction` implementations

## 13. Future ActionCostProfile Relationship

This pass adopts the Action Cost Semantics Audit direction.

Future action-cost metadata should centrally express at least:

- `IsFree`
- `WorldTimeCost`
- `StaminaCost`
- `LegacyActionPointCost`
- `LegacyMovePointCost`
- `EndsPlayerTurn`
- `RecoveryAmount`
- `IsContextual`
- `ActionTimeCost`

Key rule:

- stamina cost should be predicted and logged before it is enforced

This avoids repeating the current `ActionPointCost` overload problem.

## 14. Recommended Migration Strategy

### Stage 1

- keep current stamina foundation as-is
- no gameplay effect

### Stage 2

- adopt this shaping model
- keep AP/MP live
- do not enforce stamina yet

### Stage 3

- add `ActionCostProfile` metadata or equivalent central action-cost description
- stop using `ActionPointCost` as the only semantic field

### Stage 4

- add predicted stamina diagnostics for movement, attacks, and key strenuous actions
- no enforcement

### Stage 5

- add recovery previews for Wait / End Turn / rest
- still no enforcement

### Stage 6

- soft integration for a small set of strenuous actions:
  - sprint
  - climb
  - force door
  - heavy attack

### Stage 7

- combat UI exposes `CurrentStamina` and `Stamina This Turn`
- selected combat actions and combat movement start spending stamina
- AP/MP remain as compatibility scaffolding

### Stage 8

- AP/MP become derived compatibility presentation or are retired
- stamina plus turn exertion semantics own physical action costs

### Stage 9

- readiness / speed scheduling evolves separately
- stamina remains effort, not initiative timing

### Stage 10

- anatomy, equipment, terrain, statuses, traits, hunger, fatigue, and species shape stamina cost and recovery

## 15. Risks

- `PlayerStats` still mirrors stamina, AP, and MP, so it remains a desync risk if future stamina spending is wired in two places
- `ActionPointCost` overload is still unresolved in live code
- `ExecutePlayerAction(...)` and `ExecuteEnvironmentalAction(...)` still use AP-style gating semantics outside pure combat meaning
- the current AAM still labels many actions with `AP` text even when the real semantics are world-time or mixed legacy behaviour
- some interactions with `ActionPointCost = 0` are commented as half-turn or fractional-turn actions, so current metadata is semantically unreliable
- `SaveSystem` only persists map data; there is no clear full character save/load contract to migrate stamina semantics through yet

## 16. Helper Methods Added

No new code helpers were added in this pass.

Reason:

- the semantic shape was still the missing piece
- adding turn-budget helpers before cost metadata is clarified would encourage premature integration
- the safest outcome for this pass is documentation-first alignment

## 17. Recommended Next Phase

Recommended next Codex pass:

- introduce a small, non-invasive `ActionCostProfile` or equivalent central action-cost description type
- keep it metadata-only at first
- classify movement, wait, inspect, talk, loot, open, search, attack, and heavy actions
- add predicted stamina cost logging without enforcement

That pass should still avoid:

- replacing AP
- replacing MP
- changing turn flow
- changing combat balance
- making stamina block actions

