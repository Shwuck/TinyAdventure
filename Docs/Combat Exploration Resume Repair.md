# Combat Exploration Resume Repair

## 1. Runtime Observation

An instant kill during a combat action caused combat to start and end in the same logical action. After that, surviving NPCs stopped acting while the player remained active.

## 2. Timeline From Logs

- `TurnOrchestrator.SwitchToCombatMode begin`
- Combat participants were registered.
- `CombatResolver.ResolveAttack` resolved the hit and the target died.
- `Character.Die state transition` ran.
- `TurnOrchestrator.TryUpdateTurnContext` detected no valid hostilities.
- `TurnOrchestrator.SwitchToExplorationMode begin` ran.
- Exploration resumed, but only the player was restored into the exploration turn manager.

The key evidence was that `allCharacters.Count` had dropped to `1` by the time exploration resumed, even though surviving NPCs still existed in the area.

## 3. Root Cause

`TurnOrchestrator.SwitchToCombatMode()` was clearing the shared `allCharacters` roster during combat entry.

That made `allCharacters` stop functioning as the persistent scene roster. When combat ended immediately after a one-hit kill, `SwitchToExplorationMode()` rebuilt exploration participants from that truncated list, so surviving NPCs were never re-registered.

## 4. Files Changed

- [Assets/TurnOrchestrator.cs](../Assets/TurnOrchestrator.cs)

## 5. Behaviour Before

- Combat entry discarded the shared roster.
- Combat exit rebuilt exploration participants from an incomplete roster.
- Dead/inactive targets were not the only issue; surviving NPCs could be lost from turn registration.
- Exploration mode resumed, but non-player characters could remain frozen because they were no longer registered.

## 6. Behaviour After

- Combat entry preserves the shared roster.
- Combat exit skips dead or inactive characters and re-registers surviving active characters in the current nested area.
- Exploration mode can now restore the NPC roster after an instant kill ends combat immediately.
- Dead characters remain inactive and are not re-registered.

## 7. Manual Tests

1. Kill a target in one hit during combat.
2. Confirm combat begins and then exits immediately when no hostilities remain.
3. Confirm `SwitchToExplorationMode()` restores surviving NPCs.
4. Confirm surviving NPCs continue acting in exploration.
5. Confirm the dead target is not re-registered.
6. Confirm the player remains active.
7. Confirm no turn-manager recursion or coroutine behavior returns.

## 8. Remaining Risks

- Other roster-reset paths still exist for map/scene transitions and should be treated separately.
- This pass does not change combat balance or add new combat rules.
- If another system clears `allCharacters` outside the combat transition, it can still break restoration.
