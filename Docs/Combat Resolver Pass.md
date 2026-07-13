# Combat Resolver Pass

## 1. Scope

This pass introduced a small central combat resolver for shared physical attacks without rewriting the entire combat system.

Goals of this pass:

- centralise live physical attack resolution
- preserve existing player/NPC/monster/animal physical combat entry points
- stop flattening multi-type outgoing damage into a single caller-selected type
- apply intended weapon stat scaling once
- make body-part armour mitigation explicit and logged
- keep AP spending in one shared place for physical attacks
- preserve `Character.TakeDamage()` as the authoritative damage application path

This pass did **not** fully migrate magic or monster abilities into the resolver.

## 2. Coding Rules Consulted

Consulted file:

- `Coding Rules.txt`

Rules followed directly in this pass:

- avoid duplicating combat knowledge across multiple methods
- keep methods focused
- prefer boring, readable solutions
- use existing systems rather than parallel systems
- use `GameDebugger` for new diagnostic code paths
- use `MessageLogManager` for player-facing combat output

## 3. Files Inspected

Primary combat files inspected:

- `Assets/Objects/Character.cs`
- `Assets/Objects/IInteractions.cs`
- `Assets/PlayerController.cs`
- `Assets/PlayerStats.cs`
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
- `Assets/CombatActionResolutionDiagnosticsLogger.cs`
- `Assets/Inventories/Inventory.cs`
- `Assets/Objects/Item Actions.cs`
- `Assets/Generators/ItemGenerator.cs`
- `Docs/Combat Stabilisation Pass.md`

Checked for runtime outputs:

- `DiagnosticLogs/TinyAdventure_TurnDiagnostics_Latest.txt`
- `DiagnosticLogs/TinyAdventure_CombatActionResolution_Latest.txt` was not present in the workspace at the time of this pass

## 4. Files Changed

- `Assets/CombatResolver.cs`
- `Assets/Objects/Character.cs`
- `Docs/Combat Resolver Pass.md`

## 5. New Classes / Structs Added

Added in `Assets/CombatResolver.cs`:

- `AttackCategory`
- `AttackContext`
- `DamageLine`
- `DamagePacket`
- `AttackResult`
- `CombatResolver`

These are intentionally small data carriers plus one orchestration class.

## 6. Existing Methods Preserved

Preserved as live compatibility methods:

- `Character.PerformAttack(...)`
- `Character.TakeDamage(...)`
- `Character.ApplyOnHitEffects(...)`
- `Character.GetWeaponDamage()`
- `Character.GetResistance(...)`
- `Character.SpendActionPoints(...)`

## 7. Existing Methods Changed

### `Assets/Objects/Character.cs`

- `PerformAttack(...)`
  - now acts as a compatibility wrapper
  - still performs pre-existing hostility/target setup
  - now delegates shared physical resolution into `CombatResolver.ResolveAttack(...)`

- `GetWeaponDamage()`
  - now applies the computed primary-stat weapon bonus instead of only logging it
  - still returns per-damage-type output

- `CalculateAccuracyAgainst(...)`
  - extracted from the old internal attack path so the resolver can call the shared accuracy rule

- `DetermineCriticalHit(out ..., out ...)`
  - exposed for resolver use so crit rolling stays on the character side

- `BuildDamagePacket(...)`
  - new canonical outgoing physical damage builder for `Character`
  - uses existing character/item values instead of duplicating them in the resolver

- `TakeDamage(...)`
  - now accepts optional `AttackResult`
  - still remains the authoritative live damage application path
  - now applies body-part armour value as flat mitigation for the struck part
  - now records per-damage-type resistance and armour outcomes into the resolver result

- `ApplyOnHitEffects(...)`
  - now applies both character on-hit effects and main-hand weapon on-hit effects
  - still does not apply defender `OnHitTakenEffects`

## 8. Compatibility Wrappers

Compatibility wrapper kept:

- `Character.PerformAttack(...)`

Current flow:

`Interaction / AI state -> Character.PerformAttack -> CombatResolver.ResolveAttack -> Character.TakeDamage -> Character.ApplyOnHitEffects -> Character.SpendActionPoints`

This keeps old call sites alive while moving shared physical maths into one place.

## 9. Combat Flow Before

Before this pass:

- `Character.PerformAttack(...)` handled hit roll, crit roll, unarmed/weapon damage build, damage flattening, on-hit effects, and AP spend directly
- outgoing weapon damage was flattened into the caller-requested damage type
- `GetWeaponDamage()` calculated stat bonus but did not apply it
- `TakeDamage(...)` used resistances and body parts but ignored armour value for mitigation

## 10. Combat Flow After

Physical flow after this pass:

1. Action or AI requests an attack.
2. `Character.PerformAttack(...)` performs compatibility setup and builds an `AttackContext`.
3. `CombatResolver.ResolveAttack(...)` handles hit, crit, damage packet resolution, AP ownership, and result logging.
4. `Character.BuildDamagePacket(...)` builds outgoing damage from existing character/item data.
5. `Character.TakeDamage(...)` applies per-type resistance, body-part armour mitigation, body-part damage, health damage, death, and hostility reactions.
6. `Character.ApplyOnHitEffects(...)` applies character and weapon on-hit effects.

## 11. How Derived Stats Are Resolved

Canonical combat stat path in this pass:

- `Character.GetStatValue(string statName)`

Why this path was used:

- it includes base character stats
- it includes active `AffectedBy` modifiers
- it includes equipped item `Modifiers`
- it is shared by all `Character`-derived combatants

Important remaining risk:

- `PlayerStats.GetStatValue(...)` is still a duplicate path and does **not** match `Character.GetStatValue(...)`
- this pass did not rewrite `PlayerStats` into a full wrapper-only class

## 12. How Weapon Damage Is Resolved

Canonical outgoing physical damage builder:

- `Character.BuildDamagePacket(AttackContext context)`

Supporting methods:

- `Character.GetWeaponDamage()`
- `Character.GetWeaponStatBonus(Item weapon)`
- `Character.GetUnarmedAttackDamage()`

Behaviour now:

- weapon damage remains a dictionary keyed by `DamageType`
- primary-stat scaling is now applied once inside `GetWeaponDamage()`
- unarmed/natural attacks still use the strongest of Strength/Dexterity as base damage
- requested action damage type can convert the main physical line when needed
- extra damage lines are preserved

## 13. How Multiple Damage Types Are Preserved

Previous problem:

- weapon damage could be flattened into a single requested type inside `Character.PerformAttack(...)`

Current behaviour:

- `DamagePacket.OriginalDamageByType` stores the raw outgoing breakdown
- `DamagePacket.FinalDamageByType` stores the context-adjusted outgoing breakdown
- damage type conversion is recorded explicitly:
  - `DamageTypeConverted`
  - `ConvertedFromType`
  - `ConvertedToType`

Important note:

- if a weapon has no explicit runtime `DamageType`, the resolver converts the main line to the requested action type rather than leaving it as `None`

## 14. How Armour / Resistance / Body-Part Mitigation Works

Current live mitigation order for physical attacks:

1. select random body part via anatomy
2. apply `GetResistance(damageType)` per damage line
3. apply flat armour reduction from equipment covering the struck body part
4. apply body-part damage and health damage

New armour helper methods:

- `Character.GetEquippedArmourForBodyPart(...)`
- `Character.GetArmourValueForBodyPart(...)`
- `Character.GetArmourMitigationForHit(...)`
- `Character.IsPhysicalDamageType(...)`

Important design choice in this pass:

- armour value is now body-part-aware flat mitigation
- the armour budget is consumed once per hit, not re-used infinitely across multiple physical lines
- resistances still come from `Character.GetResistance(...)`

Still true after this pass:

- armour item resistances are still aggregated through `GetResistance(...)`
- `GetDefence()` is still not the live mitigation authority
- armour `OnHitTakenEffects` are still not applied

## 15. How AP Is Spent

Shared physical AP authority in this pass:

- `CombatResolver.ResolveAttack(...)` calls `Character.SpendActionPoints(...)`

Compatibility:

- callers still use `Character.PerformAttack(...)`
- `PlayerController` still syncs player AP from `Character.ActionPoints` after character-owned combat actions
- hostile AI still treats `Character.PerformAttack(...)` as the AP-owning attack path

Target result:

- one physical attack -> one shared AP spend

## 16. How Player / NPC / Monster / Animal Paths Converge

Converged shared physical paths:

- player physical interactions: `Slash`, `Stab`, `Bash`, `Rend`, `Punch`
- hostile NPC attacks through `HostileState`
- hostile monster attacks through `MonsterAggroState`
- unarmed and natural physical attacks through `Character.PerformAttack(...)`

Shared resolver used:

- `CombatResolver.ResolveAttack(...)`

Different high-level entry points remain, but the live physical resolution now converges.

## 17. What Still Bypasses the Resolver

Still separate after this pass:

- `MagicInteraction.ExecuteInteraction(...)`
- `Monster.PerformAbility(...)`
- legacy `BaseCombatInteraction.ExecuteInteraction(...)` direct damage path
- `PlayerStats.ApplyDamage(...)` direct health mutation wrapper

These should be considered future migration targets.

## 18. Known Risks

- `PlayerStats` still duplicates combat-relevant state and stat logic.
- `GetResistance(...)` still aggregates equipped-item resistances globally rather than by struck body part.
- `OnHitTakenEffects` are still logged but not applied.
- `MagicInteraction` and `Monster.PerformAbility()` still bypass the physical resolver.
- `Character.cs` still contains legacy combat helpers such as `CalculateFinalDamage(...)` that are not the authoritative live path.

## 19. Manual Test Plan

### 1. Player punches unarmoured NPC

- Setup: no main-hand weapon on player, target NPC with no armour.
- Expected: `AttackCategory=Unarmed`, one bludgeoning line, AP reduced once.
- Check log: `DamageTypeConverted=false`, `Weapon=None`, `ArmourValueUsed=0`.
- Bug signs: flattened/empty damage, no AP spend, or target not taking body-part damage.

### 2. Player uses sword against unarmoured NPC

- Setup: equip a sharp weapon, attack an unarmoured NPC with `Slash`.
- Expected: resolver shows weapon packet and stat bonus applied.
- Check log: `WeaponStatBonusApplied=true`, `WeaponDamageFlattened=false`.
- Bug signs: outgoing damage only shows requested type with no preserved packet, or stat bonus still logged but absent from final damage.

### 3. Player uses sword against slashing-resistant NPC

- Setup: target with slashing resistance.
- Expected: resistance reduces the slashing line before final damage.
- Check log: `DamageBreakdown=Slashing:Raw=...,Resistance=...`.
- Bug signs: resistance missing from breakdown or final damage equal to raw damage.

### 4. Player uses mixed-damage weapon

- Setup: use a weapon with base damage plus an extra elemental modifier.
- Expected: both damage lines remain visible through the resolver.
- Check log: `OriginalOutgoingDamage` and `FinalOutgoingDamage` both contain multiple damage types.
- Bug signs: only one outgoing damage type remains.

### 5. Player attacks armoured NPC

- Setup: target wears armour on the struck body part.
- Expected: body-part armour value reduces physical damage.
- Check log: `SelectedBodyPart`, `CoveredArmour`, `ArmourValuePresent`, `ArmourValueUsed`.
- Bug signs: armour present but armour used always zero on physical hits.

### 6. Player attacks relevant armour coverage

- Setup: helmet or glove equipped on target; attack several times until that part is selected.
- Expected: only hits to covered parts show that armour in `CoveredArmour`.
- Check log: body-part-specific armour summary changes by hit location.
- Bug signs: same armour always applies regardless of selected body part.

### 7. Player attacks uncovered part

- Setup: partial armour loadout on target.
- Expected: uncovered body parts show `CoveredArmour=None` and `ArmourValueUsed=0`.
- Check log: body part and armour fields.
- Bug signs: global armour reduces all hits regardless of coverage.

### 8. Player attacks while Strength-buffed

- Setup: apply a Strength buff through existing effect systems, then attack with a Strength-scaling weapon.
- Expected: `GetStatValue("Strength")` changes outgoing damage.
- Check log: higher `WeaponStatBonusCalculated` or stronger unarmed damage.
- Bug signs: buff visible on character but absent from outgoing damage.

### 9. Player attacks while debuffed

- Setup: apply a combat stat debuff to attacker.
- Expected: outgoing damage or hit chance drops through shared stat access.
- Check log: lower accuracy or damage values.
- Bug signs: debuff exists in `AffectedBy` but combat ignores it.

### 10. NPC attacks player

- Setup: let a hostile NPC act in melee range.
- Expected: attack resolves through `Character.PerformAttack -> CombatResolver`.
- Check log: `Resolver=CombatResolver`, attacker type is NPC.
- Bug signs: no resolver entry or AP double-spend.

### 11. Monster attacks player

- Setup: hostile monster reaches melee range.
- Expected: same physical resolver path as other combatants.
- Check log: `AttackCategory=Natural` or `Weapon`, depending on equipment.
- Bug signs: monster melee still using a separate flat-damage path.

### 12. Animal attacks player

- Setup: hostile animal in melee range.
- Expected: same physical resolver path, natural/unarmed packet.
- Check log: no weapon, AP spend once, body part hit recorded.
- Bug signs: attack bypasses resolver or deals no meaningful damage.

### 13. Attack with insufficient AP

- Setup: reduce attacker AP below 2 and force a physical attack attempt.
- Expected: resolver returns invalid and no hit/damage is applied.
- Check log: invalid attack warning with `InvalidReason=Insufficient AP`.
- Bug signs: free attack or negative AP.

### 14. Kill target and confirm death state

- Setup: land fatal damage.
- Expected: target ends with `IsAlive=false`, `IsActive=false`, `ActionPoints=0`.
- Check log: death transition and post-hit defender state.
- Bug signs: dead target still active or still taking turns.

### 15. Confirm final hostile death exits combat

- Setup: kill the last hostile in the area.
- Expected: combat context refreshes back toward exploration.
- Check log: `Character.Die refreshed hostility/combat context` and `TurnOrchestrator` context changes.
- Bug signs: no combat exit despite no remaining hostiles.

### 16. Confirm combat extract is generated and readable

- Setup: play through several attacks in Play Mode.
- Expected: `DiagnosticLogs/TinyAdventure_CombatActionResolution_Latest.txt` contains resolver attack entries and damage breakdowns.
- Check log: `CODEXLOG005_COMBAT_ACTION_RESOLUTION`.
- Bug signs: no combat-only extract or missing resolver details.

## 20. Recommended Next Phase

Recommended next steps after this pass:

1. migrate `MagicInteraction` and `Monster.PerformAbility()` into the same resolver family
2. remove or retire dead/legacy direct combat paths once verified
3. make `PlayerStats` a true wrapper around canonical `Character` combat values
4. move armour resistances from global equipped-item aggregation toward struck-body-part-aware aggregation where appropriate
5. add explicit resolver hooks for `OnHitTakenEffects`
