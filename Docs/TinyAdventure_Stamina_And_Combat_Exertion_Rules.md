# Tiny Adventure — Stamina, Combat Exertion, and Turn Resolution Rules

**Status:** Approved design specification  
**Scope:** Nested-area exploration and nested-area combat  
**Out of scope:** Redesigning world time, deferred nested-area time conversion, and the main-map travel model

---

## 1. Design Goal

Tiny Adventure uses one persistent simulation with two different levels of temporal resolution:

- **Exploration mode** should feel continuous, permissive, and simulation-led.
- **Combat mode** should feel ordered, tactical, and temporally constrained.

The transition into combat must not create a second, disconnected version of the world. Character positions, stamina, injuries, equipment, relationships, awareness, environmental changes, and other simulation state persist across both modes.

The central design principle is:

> **Exploration resolves the simulation through responsive world cycles. Combat resolves the same simulation through ordered tactical turns. Stamina persists across both, while Combat Exertion limits how much tactical activity fits inside one combat turn.**

---

## 2. Core Resources

### 2.1 Stamina Reserve

**Stamina Reserve** represents a character's persistent physical energy.

It:

- persists across exploration cycles;
- persists across combat turns;
- persists when combat begins or ends;
- is spent immediately when a committed action has a Stamina Cost;
- regenerates at the end of the character's turn;
- may enter bounded negative debt;
- is affected by traits, statuses, injuries, equipment, environment, and other simulation systems.

Stamina is not a temporary combat allowance and does not refill merely because a new combat turn begins.

### 2.2 Combat Exertion

**Combat Exertion** represents how much tactical activity a character can fit into one combat turn.

It:

- exists as a limiting resource only in combat mode;
- refreshes at the beginning of each combat turn;
- never becomes negative;
- is spent immediately when a committed action has a Combat Exertion Cost;
- disappears unused at the end of the turn;
- does not automatically reduce Stamina;
- is not used to limit exploration actions.

The initial default is:

```text
Maximum Combat Exertion: 10.00
```

A character with positive Stamina receives their full modified Maximum Combat Exertion at the start of the combat turn.

A character with zero or negative Stamina receives no Combat Exertion.

```text
If Current Stamina > 0:
    Current Combat Exertion = Modified Maximum Combat Exertion

If Current Stamina <= 0:
    Current Combat Exertion = 0
```

This allows a character with low positive Stamina to overexert into permitted Stamina debt during combat.

### 2.3 Relationship Between the Two Resources

Stamina and Combat Exertion are separate resources.

An action may spend:

- neither;
- Stamina only;
- Combat Exertion only;
- both.

Combat Exertion is best understood as a temporary tactical spending allowance. Stamina is the persistent reserve that is charged only when an action explicitly has a Stamina Cost.

Spending Combat Exertion does **not** inherently spend Stamina.

---

## 3. Action Cost Contract

Every action must resolve, directly or through an action profile, at least the following properties:

```text
Stamina Cost
Combat Exertion Cost?       // nullable
Exploration Behaviour
Combat Behaviour
Can Overexert
Can Use While Spent
Consumption Capacity Cost   // where relevant
```

Additional contextual requirements may include target, tool, range, equipment, terrain, status, or mode restrictions.

### 3.1 Combat Exertion Cost Fallback

Combat Exertion Cost is optional.

```text
If Combat Exertion Cost is null:
    use the resolved Stamina Cost

If Combat Exertion Cost is explicitly 0:
    spend no Combat Exertion

If Combat Exertion Cost is positive:
    use that explicit value
```

`null` and `0` must never mean the same thing.

Examples:

| Action | Stamina Cost | Combat Exertion Cost | Combat Result |
|---|---:|---:|---|
| Sword attack | 5.00 | `null` | Spend 5.00 Stamina and 5.00 Exertion |
| Equip weapon | 0.00 | 1.00 | Spend 0 Stamina and 1.00 Exertion |
| Inspect | 0.00 | 0.00 | Spend neither |
| Exceptional stamina-only effect | 4.00 | 0.00 | Spend 4.00 Stamina and no Exertion |

### 3.2 Immediate Commitment

Applicable costs are deducted immediately when an action is validly committed.

The action lifecycle is:

```text
Validate system preconditions
→ calculate resolved costs
→ confirm affordability
→ commit costs once
→ resolve outcome
→ apply exploration/combat behaviour
```

Costs must not be delayed until the end of the turn.

### 3.3 Rejected Actions

A **rejected action** is one that cannot validly begin because the system determines that its preconditions are not met.

Examples:

- invalid or missing target;
- target out of range;
- blocked destination known before movement commits;
- required tool missing;
- action unavailable in the current mode;
- insufficient Combat Exertion;
- projected Stamina would exceed the permitted debt limit;
- actor incapacitated;
- target no longer exists.

A rejected action:

- spends no Stamina;
- spends no Combat Exertion;
- spends no Consumption Capacity;
- produces no action effect;
- does not trigger an exploration cycle;
- does not end the combat turn unless rejection itself is explicitly designed to do so.

### 3.4 Committed but Unsuccessful Actions

Once an action validly begins, its costs remain spent even when the outcome is unfavourable.

Examples:

- an attack misses;
- the target dodges;
- armour blocks the attack;
- a weapon bounces off;
- a shove fails;
- a pickpocket attempt is detected;
- gathering produces nothing;
- an attack deals zero damage.

The governing rule is:

> **Costs depend on whether the action was committed, not whether its outcome was favourable.**

An attack that misses is a resolved miss, not a rejected action.

---

## 4. Exploration Mode

### 4.1 Exploration Is Not Tactically Capped

Exploration does not use Combat Exertion as an affordability limit.

An exploration action checks:

```text
Resolved Stamina Cost
Exploration Behaviour
Other system preconditions
```

An action may be performed in exploration even when its cost exceeds the character's Maximum Combat Exertion, provided the character can afford it within the permitted Stamina debt limit.

Example:

```text
Current Stamina:          12.00
Maximum Combat Exertion:  10.00
Chop Tree Stamina Cost:   11.00
```

The action is allowed in exploration and leaves the actor at 1.00 Stamina.

The same action may be unavailable in combat because it cannot fit within the actor's tactical allowance.

### 4.2 Exploration Cycles

Exploration remains turn-based underneath, but should feel continuous to the player.

A consequential player action triggers one exploration cycle:

```text
Player commits consequential action
→ action resolves
→ active world actors receive their opportunities
→ control returns to the player
```

The interface does not need to announce each exploration turn as a formal round.

### 4.3 Exploration Cycle Triggers

Actions that generally trigger an exploration cycle include:

- moving one cell;
- attacking;
- chopping;
- mining;
- digging;
- gathering;
- fishing;
- construction;
- forcing a container;
- attempting pickpocketing;
- consuming an item;
- waiting;
- resting;
- other consequential world-changing actions.

Actions that generally do not trigger an exploration cycle include:

- inspect;
- look;
- examine;
- read;
- view descriptions;
- open informational interfaces;
- review character information;
- ordinary inventory administration where no separate rule says otherwise.

Exploration-cycle behaviour is independent of Stamina Cost. A consequential action may cost no Stamina and still trigger a cycle.

### 4.4 Exploration Movement

Ordinary exploration walking:

```text
Stamina Cost: 0.00
Exploration Behaviour: Trigger Cycle
```

Ordinary walking should not exhaust the player merely for travelling through an area.

Strenuous movement may cost Stamina, including:

- sprinting;
- climbing;
- swimming;
- difficult terrain;
- severe encumbrance;
- movement while injured;
- other explicitly demanding movement.

### 4.5 NPC and World Response

Every active actor remains simulated during exploration cycles.

Each non-player actor ordinarily receives one consequential action opportunity per exploration cycle. Internal reasoning, perception, and bookkeeping may occur without counting as an additional consequential action.

---

## 5. Combat Mode

### 5.1 Tactical Resolution

When the nested area enters combat mode, the whole active area uses one shared tactical scheduler.

Combat mode belongs to the area's clock, not to every actor's mind.

Individual actors retain separate:

- awareness states;
- suspicion states;
- engagement states;
- goals;
- relationships;
- decision logic.

An actor may therefore be in a combat-mode scene while remaining unaware and uninvolved.

### 5.2 Awareness and Engagement Are Individual

Suggested conceptual states include:

**Awareness:**

- Unaware
- Suspicious
- Alerted
- Aware of Combat

**Engagement:**

- Uninvolved
- Observing
- Investigating
- Fleeing
- Helping
- Pursuing
- Engaged

The exact enums may differ, but scene mode, awareness, and engagement must remain separate concepts.

### 5.3 Actor Behaviour During Scene-Wide Combat

All active actors continue to receive turns and retain access to actions appropriate to their goals and awareness.

An unaware blacksmith may continue smithing. A sleeping character may remain asleep. A traveller may continue moving. A civilian may hear a sound and become suspicious.

Long activities must be represented as tactical progress increments rather than completing an entire long task in one combat turn.

Examples:

- Continue Smithing: one work increment
- Build Wall: one construction increment
- Sleep: remain asleep for this tactical turn
- Travel: move an appropriate tactical amount

Player-facing narration should be filtered by perception and relevance. Distant or unseen actions may update simulation state without producing repetitive visible text.

### 5.4 Combat Movement

Ordinary combat movement per cell initially uses:

```text
Stamina Cost:          0.00
Combat Exertion Cost:  1.00
Combat Behaviour:      Flexible
```

Movement consumes tactical opportunity but does not normally drain the persistent Stamina Reserve.

Strenuous circumstances may add or increase Stamina and Exertion costs, including:

- difficult terrain;
- sprinting;
- encumbrance;
- injury;
- climbing;
- other demanding movement.

### 5.5 Opening Hostile Actions

A hostile action may resolve during exploration before combat begins.

Example:

```text
Player attacks guard during exploration
→ attack costs are committed
→ attack resolves
→ awareness, witnesses, sound, and hostility update
→ combat begins if an actor enters active combat engagement
→ initiative is rolled normally
```

The initiating actor receives:

- no automatic initiative penalty;
- no Combat Exertion carry-over penalty;
- no automatic “already acted” flag.

The initiating action occurred before the first tactical combat round. The initiator may therefore act first again if initiative places them first.

This advantage is intentional.

A missed or hidden attack does not automatically trigger combat. Combat depends on perception, awareness, hostility, and engagement.

### 5.6 Mid-Cycle Combat Transition

If combat engagement begins during an exploration cycle:

```text
Finish the currently resolving actor's action
→ stop the remaining exploration cycle
→ initialise combat mode
```

Remaining actors do not complete ordinary exploration actions before combat begins.

### 5.7 Combat Exit

Combat mode continues while at least one actor remains in an immediate combat state, such as:

- actively engaged;
- pursuing a known hostile;
- responding to an immediate threat;
- attempting to reach or assist an active fight.

Suspicion alone does not require combat mode.

When immediate engagement ends, the area may return to exploration while actors retain:

- suspicion;
- alertness;
- memory;
- search goals;
- relationship or crime consequences.

---

## 6. Stamina Debt and Overexertion

### 6.1 Debt Limit

The initial universal Stamina debt floor is:

```text
Minimum Current Stamina: -20.00
```

A physical action may overexert by default provided:

```text
Projected Stamina >= -20.00
```

An action is rejected if it would push the character below the debt floor.

Individual actions may explicitly prohibit overexertion.

### 6.2 Effects of Zero or Negative Stamina

At zero or negative Stamina:

- the actor receives no Combat Exertion at the start of the combat turn;
- regeneration repays debt before positive Stamina becomes available;
- free actions remain available;
- End Turn remains available;
- physical actions requiring Combat Exertion are unavailable;
- Take a Breath is unavailable because it requires Combat Exertion;
- Rest may remain available outside combat where context permits.

Low positive Stamina does not automatically reduce Maximum Combat Exertion. A character with any positive Stamina receives the full modified Combat Exertion allowance and may push into debt.

### 6.3 No Automatic Low-Reserve Cost Penalty

Low current Stamina does not automatically make every action cost more.

Current Stamina already matters through:

- affordability;
- debt;
- loss of Combat Exertion at zero or below;
- recovery requirements.

Additional penalties should come from explicit statuses and simulation systems such as fatigue, injury, hunger, illness, heat, poison, or encumbrance.

This avoids an uncontrolled death spiral.

---

## 7. Stamina Regeneration and Recovery

### 7.1 Baseline Regeneration

At the end of every completed actor turn:

```text
Base Stamina Regeneration: +3.00
```

This occurs regardless of activity unless a rule, trait, or status modifies it.

Regeneration:

- is applied at the end of the actor's turn;
- repays negative Stamina first;
- cannot raise Current Stamina above Maximum Stamina;
- may be increased or reduced by traits, statuses, race, injury, environment, and actions.

The value of 3.00 is an initial configurable default and may be tuned later.

### 7.2 End Turn / Wait

End Turn ends the current combat turn or yields the current opportunity.

It does not itself define a fixed recovery amount. The character receives the normal end-of-turn regeneration and any applicable modifiers based on what occurred during the turn.

### 7.3 Take a Breath

Initial rule:

```text
Stamina Cost:                 0.00
Combat Exertion Cost:         3.00
Uses Per Combat Turn:         1
Immediate Stamina Recovery:   0.00
End-of-Turn Regen Bonus:     +3.00
Combat Behaviour:             Flexible
Ends Turn:                    No
```

Take a Breath invests tactical opportunity into improved recovery.

It does not immediately restore Stamina and therefore cannot directly fund more actions during the same combat turn.

At the end of that turn:

```text
Base Regeneration:        +3.00
Take a Breath Bonus:      +3.00
Total Initial Recovery:   +6.00
```

A character with zero Combat Exertion cannot Take a Breath.

### 7.4 Rest

Rest is a stronger, generally out-of-combat recovery activity.

It:

- restores more Stamina than ordinary regeneration;
- consumes exploration cycles and existing time/turn progression;
- may operate over repeated cycles;
- may later be affected by safety, shelter, food, sleep, injury, and environment.

Exact Rest values may be tuned separately.

---

## 8. Consumption Capacity

### 8.1 Definition

Every character has a Consumption Capacity used in all relevant modes.

Initial defaults:

```text
Maximum Consumption Capacity: 3
Current Consumption Capacity: 3
```

This resource limits how many consumable items can be used during one actor opportunity.

Maximum Consumption Capacity may later be modified by:

- race;
- traits;
- perks;
- anatomy or size;
- status effects;
- illness;
- other simulation rules.

### 8.2 Consumable Costs

Every consumable defines a Consumption Capacity Cost.

Examples:

| Consumable size | Capacity Cost |
|---|---:|
| Small or ordinary consumable | 1 |
| Large consumable or meal | 2 |
| Major or unusually demanding consumable | 3 |

Default consume-action costs:

```text
Stamina Cost:               0.00
Combat Exertion Cost:       1.00
Consumption Capacity Cost:  1
```

Individual consumables may override these values.

### 8.3 Refresh Boundary

Consumption Capacity refreshes:

- at the start of the actor's next combat turn;
- when control returns to the actor after an exploration cycle.

It does not refresh because of free actions.

Consuming an item does not immediately refresh it.

### 8.4 Exploration Consumption

Consuming an item in exploration:

- spends Consumption Capacity;
- applies any Stamina Cost;
- triggers an exploration cycle;
- allows the active world to respond before capacity refreshes.

### 8.5 Lifetime Statistics

Lifetime statistics are separate from Consumption Capacity.

A committed consumption event may update statistics such as:

- total consumables used;
- total food consumed;
- total potions used;
- per-item consumption totals.

Consumption Capacity governs immediate action limits. Statistics record history.

---

## 9. Statuses, Traits, and Cost Modifiers

### 9.1 Default Linkage

For ordinary physical actions, Stamina and Combat Exertion are linked by default through the null fallback.

A status representing increased physical difficulty should normally affect both.

Example:

```text
Sword Base Stamina Cost:       5.00
Combat Exertion Cost:          null
Weakened Shared Modifier:     +25%

Resolved Stamina Cost:         6.25
Resolved Exertion Cost:        6.25
```

### 9.2 Modifier Channels

The system should support distinct modifier channels.

#### Shared Effort Modifiers

Affect both Stamina Cost and inherited Combat Exertion Cost.

Examples:

- Weakness;
- relevant injuries;
- unsuitable weapons;
- severe encumbrance;
- difficult physical conditions;
- hunger or physical inefficiency where appropriate.

#### Stamina-Only Modifiers

Affect persistent energy use or recovery without necessarily changing tactical speed.

Examples:

- endurance traits;
- illness;
- heat;
- stamina poison;
- breathing efficiency;
- regeneration bonuses or penalties.

#### Combat-Exertion-Only Modifiers

Affect tactical opportunity without necessarily changing physical drain.

Examples:

- Slow;
- Haste;
- entanglement;
- impaired coordination;
- quick-draw training;
- weapon handling;
- cramped surroundings.

### 9.3 Resolution Order

For actions whose Combat Exertion Cost inherits Stamina Cost:

```text
1. Start with Base Stamina Cost
2. Apply shared effort modifiers
3. Use that shared result as the inherited Combat Exertion basis
4. Apply Stamina-only modifiers to Stamina Cost
5. Apply Exertion-only modifiers to Combat Exertion Cost
6. Round to fixed-point precision
7. Validate affordability
8. Commit applicable costs once
```

### 9.4 Resource-Specific Character Identities

The separation permits distinct character qualities:

- efficient but not faster: reduced Stamina Cost, unchanged Exertion Cost;
- faster but not efficient: reduced Exertion Cost or increased maximum Exertion, unchanged Stamina Cost;
- high endurance: larger reserve or stronger regeneration;
- adrenaline: greater tactical output without restoring reserve;
- slow: increased Exertion Cost without necessarily increasing Stamina Cost.

Stamina must not replace every other combat or character statistic.

---

## 10. Fixed-Point Numeric Representation

Stamina and Combat Exertion should not use raw floating-point values as their authoritative representation.

Use fixed-point integer storage representing hundredths:

```text
1.00 = 100 internal units
0.85 = 85 internal units
10.00 = 1000 internal units
```

Benefits include:

- exact affordability comparisons;
- deterministic calculations;
- stable save/load values;
- no floating-point drift;
- nuanced costs and modifiers.

The UI should omit unnecessary trailing zeros where practical.

Examples:

- display `5` rather than `5.00`;
- display `4.5` rather than `4.50`;
- display `0.85` where precision matters.

---

## 11. Recommended Action Behaviours

### 11.1 Exploration Behaviour

Suggested mutually exclusive values:

```text
Free
TriggerCycle
Committed
Unavailable
```

### 11.2 Combat Behaviour

Suggested mutually exclusive values:

```text
Free
Flexible
Committed
Recovery
Unavailable
```

These behaviours should be represented through structured policies or enums rather than overlapping Boolean combinations.

### 11.3 Example Profiles

#### Inspect

```text
Stamina Cost:           0.00
Combat Exertion Cost:   0.00
Exploration Behaviour:  Free
Combat Behaviour:       Free
```

#### Normal Exploration Walk

```text
Stamina Cost:           0.00
Exploration Behaviour:  TriggerCycle
```

#### Normal Combat Step

```text
Stamina Cost:           0.00
Combat Exertion Cost:   1.00
Combat Behaviour:       Flexible
```

#### Sword Attack

```text
Stamina Cost:           5.00
Combat Exertion Cost:   null
Exploration Behaviour:  TriggerCycle
Combat Behaviour:       Flexible
Can Overexert:          Yes
```

#### Equip Ordinary Weapon

```text
Stamina Cost:           0.00
Combat Exertion Cost:   1.00
Exploration Behaviour:  Free
Combat Behaviour:       Flexible
```

#### Chop Tree

```text
Stamina Cost:           11.00
Combat Exertion Cost:   null
Exploration Behaviour:  Committed
Combat Behaviour:       Unavailable unless explicitly supported
Can Overexert:          Yes
```

#### Consume Ordinary Item

```text
Stamina Cost:               0.00
Combat Exertion Cost:       1.00
Consumption Capacity Cost:  1
Exploration Behaviour:      TriggerCycle
Combat Behaviour:           Flexible or item-defined
```

#### Take a Breath

```text
Stamina Cost:                 0.00
Combat Exertion Cost:         3.00
Exploration Behaviour:        Unavailable or separately defined
Combat Behaviour:             Recovery / Flexible
Uses Per Turn:                1
End-of-Turn Regen Bonus:     +3.00
```

---

## 12. Authority and Implementation Constraints

### 12.1 One Authoritative Cost Resolver

No action, controller, combat resolver, and UI component should independently spend the same cost.

One authoritative system must:

- resolve action rules;
- calculate final costs;
- apply shared and resource-specific modifiers;
- validate debt and affordability;
- commit costs once;
- return a clear result for diagnostics and UI.

### 12.2 Typed Rules, Not String Matching

String-based action-name matching may remain useful for temporary diagnostics and migration, but must not become the final live authority.

Live actions should expose typed or structured action-rule data.

### 12.3 AP and MP Migration

Action Points and Move Points are migration targets, not permanent parallel authorities.

The intended destination is:

- Stamina for persistent physical cost;
- Combat Exertion for tactical activity limits;
- explicit exploration behaviour for world-cycle triggering;
- explicit combat behaviour for turn commitment;
- existing time and deferred nested-turn progression remaining separate.

Legacy `ActionPointCost` or `MovePointCost` fields must not remain overloaded as hidden affordability, time, and turn-ending authorities after migration.

### 12.4 Time System Is Not Being Redesigned

The existing nested-area turn accumulation and deferred time conversion remain in place.

This specification does not require:

- a new nested-area session clock;
- a new global time model;
- changes to deferred turn conversion;
- changes to main-map travel time.

The migration must preserve existing time progression while separating it from AP and MP semantics.

### 12.5 Magic Is Not Included in the Initial Physical Migration

Magic Attack and future magical actions should not be forced into the physical Stamina model merely for consistency.

Magic may later use:

- Stamina;
- Combat Exertion;
- a separate resource;
- mixed costs;
- another designed system.

The initial migration should prioritise physical attacks, physical work, movement, equipment handling, recovery, and consumption.

---

## 13. Initial Defaults Summary

```text
Maximum Combat Exertion:          10.00
Base End-of-Turn Stamina Regen:     3.00
Minimum Stamina / Debt Floor:     -20.00
Maximum Consumption Capacity:       3

Ordinary Exploration Walk:
  Stamina Cost:                     0.00
  Triggers Exploration Cycle:       Yes

Ordinary Combat Step:
  Stamina Cost:                     0.00
  Combat Exertion Cost:             1.00

Take a Breath:
  Stamina Cost:                     0.00
  Combat Exertion Cost:             3.00
  End-of-Turn Regen Bonus:         +3.00
  Uses Per Combat Turn:             1
  Ends Turn:                        No

Ordinary Consumable:
  Stamina Cost:                     0.00
  Combat Exertion Cost:             1.00
  Consumption Capacity Cost:        1
```

These are initial tunable values. Changing them does not alter the underlying rules architecture.

---

## 14. Final Design Statement

> **Tiny Adventure uses one persistent simulation. Exploration allows the player to act freely while consequential actions trigger responsive world cycles. Combat places the entire active nested area onto one tactical scheduler, while individual awareness and engagement remain actor-specific. Stamina represents persistent physical energy. Combat Exertion represents the tactical activity available during one combat turn. Actions may cost either or both, with Combat Exertion inheriting Stamina Cost when no explicit override is supplied. Costs are committed immediately when an action validly begins, regardless of whether its outcome is favourable.**

This specification is the approved design basis for the Stamina, Combat Exertion, exploration-cycle, combat-turn, recovery, and consumption-capacity migration.
