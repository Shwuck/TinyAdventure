# Combat Stabilisation Pass

## 1. Scope

This pass was limited to combat stabilisation and observability.

- Verify the current live combat path before changing behaviour.
- Add combat-specific runtime diagnostics using the existing `CODEXLOG###` pattern.
- Create a combat-only diagnostic extract under `DiagnosticLogs/`.
- Fix only high-confidence combat bugs.
- Preserve the current combat architecture until a dedicated resolver can be introduced safely.

This pass does **not** rebalance combat, replace the whole combat pipeline, or remove legacy helpers.

## 2. Previous Audit Summary

The previous audit was confirmed on the current codebase:

- `Character.PerformAttack()` is the main live physical attack path.
- `Character.TakeDamage()` is the main live damage application path.
- `PerformAttack()` flattens outgoing weapon damage into the caller-requested damage type.
- `GetWeaponDamage()` calculates a stat bonus but does not apply it.
- `TakeDamage()` uses resistance but does not use `GetDefence()` or `Item.ArmourValue`.
- Body parts are real and can be damaged or lost, but hit location is random and not coverage-aware.
- `HostileState.UpdateState()` was double-spending AP after `PerformAttack()`.
- `Character.Die()` did not set `IsAlive = false`.
- `PlayerStats` still overlaps with `Character` for combat state and some wrapper methods.
- `PlayerStats.ApplyDamage()`, `BaseCombatInteraction`, and `Monster.PerformAbility()` are not the main physical combat authority.

## 3. Files Inspected

- `Assets/Objects/Character.cs`
- `Assets/Objects/IInteractions.cs`
- `Assets/PlayerStats.cs`
- `Assets/PlayerController.cs`
- `Assets/CombatTurnManager.cs`
- `Assets/BaseTurnManager.cs`
- `Assets/TurnOrchestrator.cs`
- `Assets/StateMachine.cs`
- `Assets/Objects/NPC.cs`
- `Assets/Objects/Monster.cs`
- `Assets/Objects/Animal.cs`
- `Assets/Objects/Items.cs`
- `Assets/Objects/Anatomy.cs`
- `Assets/BuffDebuff.cs`
- `Assets/TurnDiagnosticsLogger.cs`
- `Assets/ActionAAMDiagnosticsLogger.cs`
- `Assets/MovementAIDiagnosticsLogger.cs`
- `Assets/RelationshipDiagnosticsLogger.cs`
- `DiagnosticLogs/TinyAdventure_TurnDiagnostics_Latest.txt`

## 4. Runtime Diagnostic Namespace Added

Added:

- `CODEXLOG005_COMBAT_ACTION_RESOLUTION`

Implementation file:

- `Assets/CombatActionResolutionDiagnosticsLogger.cs`

Purpose:

- combat attack entry
- attack resolution
- weapon damage inspection
- defence / armour / resistance inspection
- body-part resolution
- AP spend / reset
- death state transitions
- combat context transitions
- wrapper / bypass path detection

## 5. Combat Extract Log Files

Created by the new logger at runtime:

- `DiagnosticLogs/TinyAdventure_CombatActionResolution_<timestamp>.txt`
- `DiagnosticLogs/TinyAdventure_CombatActionResolution_Latest.txt`

These files are combat-only extracts intended for post-playtest review and sharing.

## 6. Files Changed

- `Assets/CombatActionResolutionDiagnosticsLogger.cs`
- `Assets/Objects/Character.cs`
- `Assets/Objects/IInteractions.cs`
- `Assets/PlayerStats.cs`
- `Assets/PlayerController.cs`
- `Assets/CombatTurnManager.cs`
- `Assets/StateMachine.cs`
- `Assets/Objects/Monster.cs`
- `Assets/TurnOrchestrator.cs`
- `.gitignore`

## 7. Behaviour Changed

Confirmed behaviour changes in this pass:

1. `Character.Die()` now makes death state canonical by setting `IsAlive = false` as well as `IsActive = false`.
2. `Character.Die()` now clears `InTurn`, `InCombat`, and zeroes `ActionPoints`.
3. `HostileState.UpdateState()` no longer spends AP a second time after `Character.PerformAttack()`.
4. `Character` now defaults to `IsAlive = true` on construction, which makes alive/dead state safer for generated combatants that were previously not explicitly initialized.

## 8. Behaviour Logged But Not Changed

These were intentionally proven with diagnostics and left unchanged:

1. `Character.PerformAttack()` still flattens outgoing damage into the caller-requested damage type.
2. `Character.GetWeaponDamage()` still calculates a stat bonus but does not apply it.
3. `Character.TakeDamage()` still uses resistances but does not use `GetDefence()` or `Item.ArmourValue`.
4. Body-part selection is still random and not matched against armour coverage.
5. `OnHitTakenEffects` are still not applied.
6. `Monster.PerformAbility()` was instrumented, but not wired into monster AI.
7. `PlayerStats.ApplyDamage()` remains a direct-health wrapper path and is still not the main live combat path.

## 9. Confirmed Bugs

Fixed:

- Hostile AI AP double-spend after `PerformAttack()`.
- Base death state inconsistency where `Die()` did not set `IsAlive = false`.

Confirmed and logged, but not fixed in this pass:

- weapon damage flattening in `Character.PerformAttack()`
- weapon stat bonus calculated but not applied in `Character.GetWeaponDamage()`
- armour value / `GetDefence()` bypassed by `Character.TakeDamage()`
- body-part coverage not used for mitigation
- `OnHitTakenEffects` loaded/generated but not applied
- `Monster.PerformAbility()` not used by live monster AI

## 10. Suspected Bugs / Design Debt

- `BaseCombatInteraction` remains misleading because most live physical combat paths do not rely on it.
- `PlayerStats` still duplicates combat-related state with `Character`.
- Item `OnHitEffects` on the weapon are likely not merged into `Character.OnHitEffects`, so item-based on-hit behaviour may still be misleading unless another system populates the character list.
- `GetActionCost("Attack")` still reports a legacy cost that does not match the shared `PerformAttack()` AP spend authority.

## 11. Remaining Combat Design Gaps

- No single `CombatResolver`.
- No authoritative `AttackContext` or `AttackResult`.
- No armour-coverage-aware mitigation.
- No slot/body-part-aware armour lookup in live damage resolution.
- No unified physical vs magic vs ability attack path.
- No single source of truth between `PlayerStats` and `Character`.

## 12. Player vs NPC Symmetry Findings

### Shared

- Physical combat still converges on `Character.PerformAttack()` and `Character.TakeDamage()` for player, hostile NPCs, monsters, and hostile animals.
- Shared damage application still uses resistances and body-part damage.

### Not Shared

- Player combat enters through `PlayerController` and interaction classes first.
- Player magic still bypasses `PerformAttack()` and calls `TakeDamage()` directly.
- Hostile NPCs and animals use `HostileState`.
- Monsters attack through `MonsterAggroState`.
- `PlayerStats` remains a second player combat state layer.

### Future unification still needed

- player magic
- monster abilities
- legacy wrapper damage paths
- AP cost ownership

## 13. Armour / Resistance / Body-Part Recommendation

### What the code does today

- `Item.ArmourValue` exists and is summed by `Character.GetTotalArmourValue()`.
- `Character.GetDefence()` exists and combines Constitution plus armour value.
- Live damage in `Character.TakeDamage()` does **not** use that defence value.
- Damage mitigation currently comes from `Character.GetResistance()`, which can include character resistances, equipped item resistances, and buff/debuff resistance modifiers.
- Body parts are chosen randomly and receive damage, but armour is not looked up by hit body part.

### Recommended model for TinyAdventure

Use **Option D: coverage plus resistance**, in a staged implementation:

1. Armour piece determines which equipment slot / body part coverage it protects.
2. Covered armour can contribute:
   - flat armour value reduction for physical hits
   - typed resistance modifiers for elemental / special damage
3. Uncovered body parts receive no armour-value protection.
4. Resistances remain visible in diagnostics and JSON, which keeps modding practical.

Why:

- fits the existing anatomy and equipment-slot model
- keeps diagnostics readable
- supports old-school RPG mitigation plus more systemic body-part behaviour
- avoids overengineering a simulation-heavy armour model too early

## 14. Manual Test Instructions

Open play mode, reproduce each scenario, then inspect:

- `DiagnosticLogs/TinyAdventure_CombatActionResolution_Latest.txt`

### 1. Unarmed player attacks unarmoured NPC

- Setup: remove main-hand weapon, attack neutral NPC.
- Expected: attack logs `UnarmedOrNaturalAttack=True`; outgoing damage is based on Strength/Dexterity branch.
- Bug sign: weapon damage appears anyway.

### 2. Armed player attacks unarmoured NPC

- Setup: equip a weapon, attack NPC.
- Expected: `OriginalWeaponDamage` is logged; `WeaponDamageFlattened=True` is visible if physical action path is used.
- Bug sign: empty weapon damage or wrong attack path.

### 3. Armed player attacks armoured NPC

- Setup: target wearing armour with resistance entries.
- Expected: resistance use is logged; `ArmourValueUsed=False` remains explicit.
- Bug sign: armour value appears to mitigate damage without a code change.

### 4. Slash vs Stab vs Bash against same target

- Setup: use matching weapon types and actions.
- Expected: requested damage type differs in the logs.
- Bug sign: all actions collapse to the same requested type or same availability path.

### 5. Player attacks target with helmet / chest armour / gloves / boots

- Setup: equip target in multiple slots.
- Expected: covered armour is listed; `BodyPartCoverageUsed=False` remains explicit for now.
- Bug sign: logs claim slot-aware mitigation when it is not implemented.

### 6. NPC attacks player

- Setup: hostile NPC in range.
- Expected: `HostileState.UpdateState` attack entry followed by shared `PerformAttack` logs.
- Bug sign: no shared attack log or duplicated AP spend.

### 7. Hostile NPC AP after attacking

- Setup: hostile NPC begins turn with full AP and attacks once.
- Expected: one AP spend block from `Character.PerformAttack`; no second hostile-state spend block.
- Bug sign: AP drops twice for a single attack.

### 8. Monster or animal attacks player

- Setup: provoke monster or hostile animal.
- Expected: `MonsterAggroState` or `HostileState` entry, then shared physical attack logs.
- Bug sign: missing shared path or invalid `IsAlive` blocking.

### 9. Player attacks with insufficient AP

- Setup: reduce player AP below attack cost.
- Expected: action rejection diagnostics and no combat resolution block.
- Bug sign: attack still resolves.

### 10. Fatal damage kills target

- Setup: deal lethal damage or remove a vital body part.
- Expected: `Character.Die` block shows `IsAliveBefore=True`, `IsAliveAfter=False`.
- Bug sign: dead target remains alive in logs.

### 11. Dead target does not keep taking turns

- Setup: kill an active combat participant during combat.
- Expected: no later entity-turn blocks for that dead combatant.
- Bug sign: dead actor still gets a turn.

### 12. Final hostile death exits combat

- Setup: kill the last hostile in the area.
- Expected: `TurnOrchestrator.SwitchToExplorationMode` combat-context block appears.
- Bug sign: context stays `Combat`.

### 13. Buffed / debuffed attacker

- Setup: apply a status that changes stats or resistance.
- Expected: stat/resistance change is visible in combat diagnostics.
- Bug sign: effect exists but never appears in combat calculations.

### 14. Resistant target

- Setup: target with innate or equipment resistance.
- Expected: `Character.GetResistance` block shows sources and final resistance.
- Bug sign: resistance source exists but mitigation stays unchanged.

### 15. Body-part loss or body-part damage event

- Setup: force enough damage to lose a limb.
- Expected: `SelectedBodyPart`, body-part HP before/after, and limb-loss behaviour are logged.
- Bug sign: no body-part info or no death/loss handling.

### 16. Magic attack path

- Setup: equip a magic weapon and use `Magic Attack`.
- Expected: `MagicInteraction.ExecuteInteraction` logs `Resolver=MagicInteraction.DirectTakeDamage`.
- Bug sign: no dedicated magic-path diagnostic.

## 15. Recommended Next Phase

Phase 2 should introduce a small central combat resolver without deleting the current helpers yet.

Recommended minimal architecture:

- `CombatResolver`
- `AttackContext`
- `AttackResult`
- `DamagePacket`
- `DefenseResult`

Initial resolver goals:

1. make physical attack cost ownership explicit
2. preserve real weapon damage dictionaries
3. apply armour coverage and armour value in one place
4. unify player, NPC, monster, animal, and magic damage application
5. retire wrapper paths only after tests prove parity
