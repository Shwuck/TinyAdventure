# Character Decision Foundation Pass

## 1. Purpose

This pass begins a small Character-level world decision foundation for TinyAdventure.

It is intentionally not an NPC-only AI framework.

The goal is to give actor-like entities a shared direction for bounded world decisions:

1. Can I act?
2. What profile applies to me?
3. What interests do I currently bring?
4. What nearby world affordances exist?
5. Can I resolve one bounded decision?
6. End the turn cleanly.

This pass does not implement schedules, full needs, full personality, full perception, full social AI, or combat awareness.

## 2. Rules And Docs Read

The following project guidance was read before changes:

- `Docs/Coding Rules.txt`
- `Docs/TinyAdventure Core Simulation Semantics.txt`
- `Docs/Player Input And Simulation Turn Policy.md`
- `Docs/NPC Idle Turn Budget Pass.md`
- `Docs/NPC World Thought Process And Affordances.md`
- `Docs/Turn Determinism Pass.md`
- `Docs/Turn Roster Lifecycle Audit.md`
- `Docs/Combat Resolver Pass.md`
- `Docs/Combat Exploration Resume Repair.md`
- `Docs/Combat Runtime Repair Pass.md`

No separate repo-local prompt/rules directory with clearer current gameplay constraints was found beyond `Docs/`.

## 3. Current Role / Job Findings

Roles exist in the live project.

- `Assets/Objects/NPC.cs`
  - `NPC.Role` stores the live role as `NPCRole`
- `Assets/DataLoaders/NPCDataLoader.cs`
  - `NPCRoleData` defines JSON-loaded role metadata
- `Assets/StreamingAssets/NPCCreationData.json`
  - `RoleData` contains live role definitions
- `Assets/Generators/NPCGenerator.cs`
  - role data currently influences titles, loadouts, money, crafting flags, and stat modifiers
- `Assets/NPCManager.cs`
  - `FrequentNeeds` currently influences NPC need generation

Role data is JSON-backed and safely extendable later.

Current role fields already include:

- `Role`
- `Titles`
- `LoadoutNames`
- `FrequentNeeds`
- `NewsType`
- `IsCraftsman`
- `CraftingType`
- `StatModifiers`

Current live gap:

- roles affect flavour/loadout/stats/needs
- roles do not yet directly drive world movement or world decision selection

## 4. Current Animal / Monster Findings

Animals and monsters inherit from `Character`.

- `Assets/Objects/Animal.cs`
- `Assets/Objects/Monster.cs`

Shared path findings:

- exploration actors already converge on `Character.ExecuteTurnActions()`
- animals use `IdleState` and therefore now benefit from the new Character-level decision foundation
- monsters currently still enter `MonsterIdleState`, `MonsterAggroState`, and related monster-specific states

This means:

- the foundation is Character-level
- animals are already on the shared live idle path
- monsters are future-compatible with the same model
- monsters were not fully migrated in this pass to avoid breaking current hostile/combat behaviour

## 5. Why This Is Character-Level, Not NPC-Level

The new types use Character terminology on purpose.

Added concepts:

- `CharacterDecisionProfile`
- `CharacterInterestTag`
- `WorldAffordance`
- `InterestCandidate`
- `CharacterDecisionResult`
- `CharacterDecisionResolver`
- `WorldAffordanceProvider`

This keeps the foundation usable for:

- villagers
- normal NPCs
- animals
- monsters
- future companions
- future summons
- future modded actor types

## 6. Definitions

### CharacterDecisionProfile

A lightweight profile describing what a character tends to care about right now.

In this pass it is built from:

- actor type
- NPC role
- existing role metadata such as `IsCraftsman` and `CraftingType`
- simple species/type defaults for animals and monsters

### CharacterInterestTag

A small shared tag vocabulary representing what a character might seek.

Prototype tags added:

- `Smithing`
- `Metalwork`
- `Workstation`
- `GuardPost`
- `PatrolRoute`
- `Entrance`
- `Rest`
- `Sleep`
- `Sit`
- `Drink`
- `Socialise`
- `Trade`
- `Food`
- `FoodSmell`
- `Owner`
- `Prey`
- `Lair`
- `Intruder`
- `Shelter`
- `Warmth`
- `CrimeReport`
- `Inspect`
- `Loot`
- `Wander`
- `Noise`

### Affordance

Something the world offers to a character.

In this pass affordances are not yet data-driven.

They are discovered from a very small set of existing live object/cell types.

### InterestCandidate

A concrete candidate built from:

- a matching interest tag
- a source object/cell
- a target position
- a simple distance-based score

### CharacterDecisionResult

The outcome of one bounded world decision.

Decision types used in this pass:

- `MoveTowardsCandidate`
- `UseAffordanceInPlace`
- `IntentionalIdle`
- `WanderFallback`
- `FailedMovement`
- `SkippedCannotAct`

### WorldAffordanceProvider

A tiny discovery helper that converts existing world state into candidate affordances.

## 7. What Was Implemented

### New shared foundation

Added:

- `Assets/CharacterDecisionFoundation.cs`

This file contains the minimal shared decision types and resolver.

### Minimal profile building

`CharacterDecisionResolver` now builds lightweight profiles for:

- NPCs
- animals
- monsters
- generic characters

Prototype role interest examples:

- `Blacksmith` -> `Smithing`, `Metalwork`, `Workstation`
- `Guard` -> `GuardPost`, `Entrance`, `PatrolRoute`
- `Merchant` / `Trader` -> `Trade`, `Socialise`
- `Explorer` / `Hunter` / `Scout` / `Adventurer` -> `Wander`, `Inspect`

This is role -> interest mapping only.

It is not role -> specific object hardcoding.

### Minimal affordance discovery

The prototype now discovers a very small set of existing live affordance sources:

- `Anvil` -> `Smithing`, `Metalwork`, `Workstation`
- `Campfire` -> `Warmth`, `Rest`, `Socialise`
- `Door` -> `Entrance`, `GuardPost`
- `Corpse` -> `Inspect`, `Loot`, `CrimeReport`
- `Carcass` / `MonsterRemains` -> `Inspect`, `Loot`
- indoor cells -> `Shelter`

### Minimal IdleState integration

`Assets/StateMachine.cs`

`IdleState.UpdateState(...)` now:

1. checks the existing player-interaction special case
2. asks `CharacterDecisionResolver.ResolveWorldDecision(owner)`
3. records one bounded result
4. consumes the rest of the NPC world-turn AP budget
5. ends cleanly

This replaces the previous idle branch that randomly chose between movement / idle action / default idle without looking at role or world affordances.

## 8. What Was Deliberately Not Implemented

Not implemented in this pass:

- full affordance JSON schema
- full role-interest JSON schema
- full schedule/job system
- full needs-driven world AI
- full personality weighting
- full relationship weighting
- full social AI
- full perception / hearing / line-of-sight world awareness
- full combat awareness integration
- full object reservation / occupancy system
- full monster migration to the shared idle decision resolver
- full bystander reaction system
- full speed/readiness scheduling

## 9. How Roles Map To Interests In The Prototype

Current prototype rule:

- role metadata and role enum shape the profile
- world discovery stays object/cell driven

Examples:

- Blacksmith wants `Smithing`, `Metalwork`, `Workstation`
- Guard wants `GuardPost`, `Entrance`, `PatrolRoute`
- Innkeeper/Bard lean toward `Socialise`, `Trade`, `Warmth`
- Merchant/Trader lean toward `Trade`, `Socialise`

This is intentionally safer than hardcoding:

- blacksmith -> specific anvil instance
- guard -> specific gate instance

The bridge remains:

- role/species -> interest tags
- world object/cell -> affordance tags

## 10. How Animals / Monsters Fit

### Animals

Animals already inherit `Character` and use `IdleState`, so they now participate in the shared foundation.

Prototype defaults:

- domestic/tame animals -> `Owner`, `Food`, `Shelter`
- predators -> `Prey`, `Noise`, `Wander`
- general animals -> `Food`, `Shelter`, `Rest`, `Wander`

### Monsters

Monsters also inherit `Character`, and the resolver includes monster profile defaults:

- `Lair`
- `Intruder`
- `PatrolRoute`
- `Wander`

However, monsters still primarily use `MonsterIdleState` today.

That migration was intentionally left for a later controlled pass.

## 11. How Objects / Cells Provide Affordances In The Prototype

The prototype uses existing object types and cell state only.

No JSON schema migration was required.

Current natural extension points for future data-driven affordances:

- `Assets/Objects/Objects.cs`
- `Assets/Generators/MapGenerator.cs`
- `Cell.EnvironmentalTagFlags`
- `Cell.ResourceTagFlags`
- future object metadata / data-loader fields

## 12. Idle Fallback And Random Wandering

Important behavioural change:

- no candidate found is now a normal outcome
- intentional idle is a valid result
- random wandering is no longer the default emergency failure path

Current prototype fallback:

- if no candidate exists, most actors intentionally idle
- some profiles are allowed a small bounded wander fallback chance
- wander fallback is explicit and logged
- failed movement still ends the turn cleanly

This preserves one bounded world decision per turn.

## 13. How One Bounded Decision Per Turn Is Preserved

The pass preserves the recent idle-budget rule:

- one world/exploration turn is one bounded decision
- world idle is not an AP-spending loop
- `IdleState` now resolves once through `CharacterDecisionResolver`
- after that, remaining AP is consumed and the turn ends
- `Character.ExecuteTurnActions()` sees a recorded result and does not fall through into legacy `MoveToCellsOfInterest()`

## 14. Diagnostics Added

The new resolver writes concise decision summaries through the existing diagnostics path.

Primary diagnostic call:

- `MovementAIDiagnosticsLogger.LogEvent(...)`

Resolver summaries now include:

- actor
- profile used
- profile source
- interests considered
- candidate count
- selected candidate
- selected interest
- decision type
- turn decision result
- whether movement was attempted
- whether movement succeeded
- whether random wander was explicitly selected
- reason

No-candidate idle is informational, not warning-level failure.

## 15. Files Inspected

- `Assets/Objects/Character.cs`
- `Assets/Objects/NPC.cs`
- `Assets/Objects/Animal.cs`
- `Assets/Objects/Monster.cs`
- `Assets/StateMachine.cs`
- `Assets/ExplorationTurnManager.cs`
- `Assets/BaseTurnManager.cs`
- `Assets/Generators/NPCGenerator.cs`
- `Assets/DataLoaders/NPCDataLoader.cs`
- `Assets/NPCManager.cs`
- `Assets/Generators/MapGenerator.cs`
- `Assets/Objects/Objects.cs`
- `Assets/Objects/Items.cs`
- `Assets/Objects/IInteractions.cs`
- `Assets/NestedAreas/BaseNestedArea.cs`
- `Assets/StreamingAssets/NPCCreationData.json`

## 16. Files Changed

- `Assets/CharacterDecisionFoundation.cs`
- `Assets/StateMachine.cs`
- `Assets/Objects/Character.cs`
- `Docs/Character Decision Foundation Pass.md`

`Assets/Objects/Character.cs` only received clarification comments about legacy `CellsOfInterest` semantics in this pass.

## 17. Manual Test Plan

1. Enter an area with several standard NPCs.
2. Confirm no normal NPC hits an idle max-loop.
3. Confirm NPC turns resolve quickly and once each.
4. Confirm generic villagers do not all wander every turn.
5. Confirm no-candidate turns produce intentional idle rather than warning spam.
6. Find or place a blacksmith NPC if available.
7. Confirm the blacksmith profile logs `Smithing` / `Metalwork` / `Workstation`.
8. Find or place an `Anvil`.
9. Confirm the resolver logs an anvil candidate if reachable.
10. Confirm the actor moves toward an anvil-adjacent usable tile rather than trying to stand inside the anvil.
11. Confirm a guard profile logs `GuardPost` / `Entrance` / `PatrolRoute`.
12. Confirm animals still take bounded turns.
13. Confirm monsters still use their current hostile/combat behaviour.
14. Confirm combat/hostile paths were not broken.
15. Confirm player movement and Wait/End Turn still behave as before.

## 18. Remaining Risks

- affordances are still prototype code, not data-driven content
- only a small subset of world objects currently advertise affordances
- monsters are profile-compatible but not yet migrated to the shared idle resolver
- social opportunities, current-task continuity, reservation, and scene-aware idle distribution are still future work
- some exploration scenes may still have too few live affordance sources, producing mostly intentional idles

## 19. Recommended Next Phase

The safest next phase is a small expansion pass, not a rewrite.

Suggested order:

1. Add a few more live affordance providers to existing objects/areas.
2. Add simple role/species interest defaults in one canonical place only.
3. Add current-task continuity so characters can keep moving toward a chosen candidate across turns.
4. Add a small scene-aware idle distribution rule so wandering is chosen intentionally and sparsely.
5. Migrate monster idle/world behaviour onto the same Character-level foundation only after the exploration path is proven stable.
