# Stamina Direction And Foundation Pass

## 1. Purpose Of This Pass

This was implemented as a direction/foundation pass, not a gameplay migration.

The goal was to:

* understand the current stat/resource/turn/action model
* add canonical stamina to `Character`
* derive stamina from existing character stats through the current canonical stat path
* add safe helper methods and diagnostics
* avoid changing movement, combat, turn flow, action costs, or NPC decisions

## 2. Why Stamina Is Being Introduced

TinyAdventure is moving toward a more simulation-first model with:

* one authoritative area roster
* deterministic turn ownership
* one bounded world decision per actor turn
* character-level decision ownership
* affordance/interest-driven behaviour instead of narrow NPC scripts
* exploration and combat as different turn policies inside one simulation
* future readiness/speed concepts separated from action effort

In that direction, stamina is a better future candidate for physical effort than treating AP and MP as the long-term final model.

## 3. Why This Is A Design-Direction Shift

The project already distinguishes:

* exploration movement as a time-costing world action
* combat movement as a tactical movement-budget action
* AP as a tactical action budget

The possible future shift is to treat stamina as the primary effort resource while time/readiness answers turn frequency and action semantics answer what a specific action consumes.

That is a design-direction change, so this pass only establishes safe foundation.

## 4. Why AP/MP Were Not Replaced

Current live semantics still rely on:

* AP for bounded combat actions
* MP for bounded combat movement
* exploration movement as a time-costing action that completes the player turn
* `IInteraction.ActionPointCost` as a legacy overloaded cost field

Replacing AP/MP now would mix a semantic migration with an infrastructure pass and would risk breaking movement, combat, turn flow, and player input routing.

## 5. Docs, Rules, And Prompt Sources Read

Hard-constraint docs read:

* `Docs/Coding Rules.txt`
* `Docs/TinyAdventure Core Simulation Semantics.txt`

Related design docs read:

* `Docs/Player Input And Simulation Turn Policy.md`
* `Docs/Character Decision Foundation Pass.md`
* `Docs/NPC World Thought Process And Affordances.md`
* `Docs/NPC Idle Turn Budget Pass.md`
* `Docs/Turn Determinism Pass.md`
* `Docs/Turn Roster Lifecycle Audit.md`
* `Docs/Player End Turn Action.md`
* `Docs/Combat Resolver Pass.md`
* `Docs/Combat Stabilisation Pass.md`
* `Docs/Combat Runtime Repair Pass.md`
* `Docs/Combat Exploration Resume Repair.md`
* `Docs/Review of Nested Area TurnManager and Combat.txt`
* `Docs/Further Turn Management Review.txt`

Prompt/rule folder audit:

* checked `Prompts/`, `Docs/Prompts/`, `In/`, `Input/`, and `CodexPrompts/`
* no current project prompt/rule folders were found outside `Docs/`

## 6. Current Stat And Resource Audit

### Current Stats

Current commonly used character stats are:

* `Strength`
* `Dexterity`
* `Constitution`
* `Intelligence`
* `Wisdom`
* `Charisma`
* `Luck`
* `Perception`
* `Speed`
* `Awareness` exists as a field but is not part of `Character.GetStatValue(...)`

Findings:

* Constitution exists.
* Strength exists.
* Dexterity exists.
* Agility does not exist as a live character stat.
* Endurance does not exist as a live character stat.
* Vitality does not exist as a live character stat.

### Canonical Stat Access

`Character.GetStatValue(string statName)` is the better current canonical stat method.

It currently includes:

* raw character base stat fields
* `AffectedBy` buffs/debuffs that target the named stat
* equipped item `Modifiers` entries that target the named stat

It currently does not include:

* anatomy/body-part condition as generic stat modifiers
* traits as a separate system
* status effects outside `AffectedBy`
* item `StatModifiers` used by `PlayerStats`
* `Awareness`
* speed/readiness semantics

`PlayerStats.GetStatValue(...)` is not the best canonical path because it duplicates stat logic and does not match `Character.GetStatValue(...)`.

### Current Resource Ownership

Canonical live resource fields are already on `Character`:

* Health: `Character.Health`
* MaxHealth: `Character.MaxHealth`
* ActionPoints: `Character.ActionPoints`
* MaxActionPoints: `Character.MaxActionPoints`
* MovePoints: `Character.MovePoints`
* MaxMovePoints: `Character.MaxMovePoints`

`PlayerStats` duplicates parts of this for the player wrapper/UI path.

## 7. Current AP/MP And Action-Cost Findings

### AP

* Combat attacks currently spend AP through `CombatResolver` and `Character.SpendActionPoints(...)`.
* NPC AP resets happen in `Character.ExecuteTurnActions()`.
* Player combat AP resets happen through `PlayerStats.ResetActionPoints()` and are then synced back into `Character`.

### MP

* Movement currently consumes MP through player controller flow.
* Combat movement spends MP and keeps the player combat turn open.
* Exploration movement also spends MP in the current input path but then completes the player exploration turn as a time-costing action.

### Exploration Turn Completion

* Successful exploration movement completes the player turn through the exploration/world-time path.
* Wait/End Turn in exploration is also treated as a time-costing action.

### Combat Turn Continuation

* Successful combat movement spends MP but does not auto-complete the turn.
* Combat remains a bounded tactical player-budget mode.

### `IInteraction.ActionPointCost`

`IInteraction.ActionPointCost` is still overloaded.

It currently mixes:

* combat AP costs
* exploration/world-time progress
* free actions with zero cost

That is the main future insertion point for clearer action-cost semantics. Stamina should eventually integrate through a richer action-cost model instead of reusing this field blindly.

## 8. Current PlayerStats Duplication / Desync Findings

`PlayerStats` is currently a desync risk.

It duplicates:

* health
* AP
* MP
* stat access
* some world state

Additional stamina finding:

* `PlayerStats` already had `MaxStamina` and `Stamina` fields before this pass
* `PlayerPanelUI` already displays `PlayerStats.Stamina / PlayerStats.MaxStamina`
* `PlayerCharacter` also previously had a separate player-only `MaxStamina`

That meant stamina already existed in fragmented form before this pass.

Direction chosen:

* `Character` is now the canonical stamina owner
* `PlayerStats` remains a mirror/wrapper path only
* no separate player-owned stamina logic was added

## 9. Constitution / Endurance / Vitality Findings

* Constitution exists and is widely used in player, NPC, animal, and monster generation.
* Endurance is not a live character stat.
* Vitality is not a live character stat.
* Because no endurance/vitality-style stat exists, Constitution is the correct current foundation stat.

## 10. Stamina Semantic Definition

Stamina represents a character's current physical effort capacity.

Stamina is:

* not health
* not speed
* not time
* not readiness
* not AP
* not MP

Stamina eventually may be spent by:

* movement
* sprinting
* climbing
* swimming
* attacking
* blocking
* dodging
* forced exertion
* carrying and armour burden
* fleeing
* fatigue and injury compensation

In this pass, stamina does none of that yet.

## 11. MaxStamina Formula Chosen

Chosen formula:

`MaxStamina = max(10, 10 + round(GetStatValue("Constitution") * 2))`

Reasoning:

* Constitution exists everywhere relevant.
* It better represents endurance/effort capacity than Dexterity.
* The formula goes through `Character.GetStatValue(...)`, so buffs/debuffs and equipped item `Modifiers` can matter when stamina is recalculated.
* The formula is intentionally conservative and easy to replace later.

Fallback behaviour:

* if Constitution resolves to `0` or below, stamina still clamps to a safe minimum of `10`
* a diagnostic is logged when that fallback-safe path is used

## 12. Where Stamina Was Added

Canonical stamina was added to `Assets/Objects/Character.cs` as:

* `MaxStamina`
* `CurrentStamina`

This means stamina now exists once at the shared actor level and therefore applies to:

* player characters
* NPCs
* animals
* monsters

## 13. Helper Methods Added

Added to `Character`:

* `CalculateMaxStamina()`
* `RecalculateMaxStamina(bool preservePercentage = false, string context = "Unknown")`
* `InitializeStamina(string context = "Unknown")`
* `ResetStamina(string context = "Unknown")`
* `ClampStamina(string context = "Unknown")`
* `CanSpendStamina(int amount)`
* `SpendStamina(int amount, string reason = "")`
* `RestoreStamina(int amount, string reason = "")`
* `GetStaminaPercent()`
* `GetStaminaRecoveryPerTurn()`
* `GetStaminaRecoveryOnWait()`
* `RecoverStaminaForTurn(string context)`
* `RecoverStaminaOnWait(string context)`
* `RecoverStaminaOnRest(string context)`
* `RecoverStaminaFully(string context)`

Helper guarantees:

* current stamina never goes below `0`
* current stamina never exceeds max stamina
* max stamina never goes below `10`
* negative spend is rejected and logged
* negative restore is rejected and logged
* zero spend/restore is a safe no-op
* recalculation clamps current stamina

This pass clamps on recalculation by default instead of preserving percentage automatically. That matches current safety goals and keeps behaviour explicit.

## 14. What Is Wired Now

Wired in this pass:

* player characters initialize stamina after loadout assignment in `PlayerCharacterFactory`
* generated NPCs initialize stamina after stat and role modifiers
* generated animals initialize stamina in `AnimalFactory`
* monsters initialize stamina in the `Monster` constructor
* `PlayerStats` now mirrors current player stamina from `Character` for compatibility with the existing player panel

## 15. What Is Deliberately Not Wired

Not wired in this pass:

* movement stamina costs
* attack stamina costs
* ability stamina costs
* low-stamina action blocking
* stamina influence on combat outcomes
* stamina influence on turn order
* stamina influence on NPC decisions
* stamina influence on exploration time cost
* wait/rest automatic stamina recovery
* readiness/speed integration
* AP/MP replacement
* `CombatResolver` changes
* `PlayerController` action handling changes
* `IInteraction.ActionPointCost` semantics changes

## 16. Diagnostics Added

Added a dedicated runtime diagnostic namespace:

* `CODEXLOG006_STAMINA_RESOURCE`

It logs:

* max stamina fallback usage
* initialization
* recalculation
* reset
* spend
* restore
* invalid negative spend/restore
* clamp adjustments

Logging is change/event-based only. There is no per-frame or per-turn stamina spam.

## 17. UI Findings

UI audit findings:

* the existing player panel already displays stamina from `PlayerStats`
* no new stamina UI was added
* no HP/AP/MP UI was removed or reworked

Implementation choice:

* UI was left structurally unchanged
* `PlayerStats` stamina is now a compatibility mirror sourced from `Character`

Future direction:

* if stamina becomes a live gameplay resource, the UI should ultimately read through `Character` authority rather than maintain duplicate logic

## 18. Generation / Save / Load Findings

Generation:

* player creation now initializes valid stamina
* generated NPCs now initialize valid stamina
* generated animals now initialize valid stamina
* monsters now initialize valid stamina

Serialization / save-load:

* current `SaveSystem` only serializes map data
* there is no authoritative character save/load stamina path to migrate yet
* no JSON schema changes were required
* no loader schema changes were required

Compatibility:

* missing stamina save data is not a current blocker because character save serialization is not yet implemented here

## 19. Future Migration Path

Recommended staged path:

### Stage 1

* stamina exists on `Character`
* helpers and diagnostics are in place
* no gameplay changes

### Stage 2

* introduce explicit action-cost semantics such as:
* free
* time-costing
* stamina-costing
* legacy AP-costing
* legacy movement-costing
* contextual

### Stage 3

* add optional debug-only predicted stamina costs for one isolated action or movement probe
* do not enforce them yet

### Stage 4

* exploration movement computes suggested stamina cost and logs it without blocking

### Stage 5

* wait/rest/sleep recovery semantics are wired deliberately
* add clearer stamina debug/UI presentation

### Stage 6

* selected physical actions start spending stamina while AP/MP remain as compatibility layers

### Stage 7

* combat movement and attacks migrate gradually
* AP/MP become either tactical presentation layers or retirement candidates

### Stage 8

* readiness/speed scheduler work is integrated separately from stamina

### Stage 9

* anatomy, equipment, encumbrance, traits, statuses, hunger, injury, terrain, and fatigue affect stamina costs and recovery

## 20. Manual Test Plan

1. Start the game.
2. Create or load a player character.
3. Confirm the player has valid `MaxStamina` and `CurrentStamina`.
4. Confirm player `MaxStamina` matches `10 + Constitution * 2` after any current `Character.GetStatValue("Constitution")` modifiers.
5. Enter a nested area with NPCs.
6. Confirm generated NPCs have valid stamina values.
7. Confirm generated animals have valid stamina values if present.
8. Confirm monsters have valid stamina values if present.
9. Manually call `SpendStamina`, `RestoreStamina`, and `RecalculateMaxStamina` from a safe debug path or inspector-driven debug helper if available.
10. Confirm clamping prevents values below `0` or above `MaxStamina`.
11. Confirm movement behaves exactly as before in exploration.
12. Confirm movement behaves exactly as before in combat.
13. Confirm combat AP spending behaves exactly as before.
14. Confirm Wait/End Turn behaves exactly as before.
15. Confirm NPC idle/world decisions behave exactly as before.
16. Confirm no AP/MP behaviour changed for player or NPCs.
17. Confirm player stamina shown in the player panel matches the current character's canonical stamina.
18. Confirm no stamina initialization warnings appear for normal generated actors unless a fallback-safe case is expected.

## 21. Risks

Main risks remaining:

* `PlayerStats` still duplicates other resources and stat logic beyond stamina
* `PlayerStats.GetStatValue(...)` still differs from `Character.GetStatValue(...)`
* `IInteraction.ActionPointCost` remains semantically overloaded
* equipment modifiers are split across `Item.Modifiers` and `Item.StatModifiers`, which means canonical and wrapper stat paths still diverge
* stamina recalculation is available but not yet centrally triggered by every future source of stat change
* existing scene-serialized `PlayerStats.MaxStamina/Stamina` values are legacy compatibility data only

## 22. Recommended Next Phase

Recommended next phase:

* do not migrate movement or combat costs yet
* first define a proper `ActionCostProfile` or equivalent semantic layer
* then add predicted stamina-cost diagnostics for one narrow, optional action path

That keeps the next step semantic and observable rather than disruptive.
