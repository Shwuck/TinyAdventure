**Scope**
Runtime lifecycle audit and targeted repair for exploration turn continuation after area entry and manual player end-turn.

**Runtime Issue**
- Unity compiled.
- Player could enter a nested area.
- Initial exploration setup succeeded.
- Manual `End Turn` was available.
- NPCs were reported as not resuming movement/turns after the player ended their turn.

**Logs Inspected**
- `DiagnosticLogs/TinyAdventure_TurnDiagnostics_Latest.txt`
- `DiagnosticLogs/TinyAdventure_CombatActionResolution_Latest.txt`
- `Docs/Coding Rules.txt`
- `Docs/Turn Determinism Pass.md`
- `Docs/Combat Runtime Repair Pass.md`
- `Docs/Combat Exploration Resume Repair.md`
- `Docs/Player End Turn Action.md`

**Timeline From Logs**
1. `PlayerController.EnterNestedArea` called `TurnOrchestrator.EnterExplorationArea`.
2. `TurnOrchestrator.EnterExplorationArea setup completed` logged `CurrentAreaRoster.Count: 8`, `Exploration.Count: 8`, `Combat.Count: 0`.
3. `ExplorationTurnManager.StartTurnCycle` ran.
4. `ExplorationTurnManager.ContinueTurnSequence` executed four NPC turns before the player turn.
5. `ExplorationTurnManager.OnNPCTurnExecute` called `Character.ExecuteTurnActions()` and movement/state-machine diagnostics ran for those NPCs.
6. `ExplorationTurnManager.OnPlayerTurnStart` then began the player turn.
7. The latest turn diagnostics did not contain a matching exploration-side `PlayerTurnCompleted` / `TurnOrchestrator.PlayerTurnCompleted` / post-player `ContinueTurnSequence` handoff.

**Findings**
- `CurrentAreaRoster` was populated correctly on area entry.
- `ExplorationParticipants` were built correctly on area entry.
- Exploration mode was active.
- NPC exploration turns and movement logic did run before the player turn.
- The missing proof point was the player-turn-complete handoff back into the active exploration turn manager.

**Root Cause**
The player end-turn path in `Assets/PlayerController.cs` still depended on a cached `turnOrchestrator` field set in `Start()`, while most context checks used `TurnOrchestrator.Instance` directly.

That left the player-turn-complete path more fragile than the rest of the turn lifecycle:
- availability checks for `End Turn` used the cached field
- completion routing used the cached field
- if the cached field was stale or missing, the player-facing interaction could appear to work while the deterministic exploration handoff had no turn-diagnostic proof and could fail to resume NPC progression

**Files Changed**
- `Assets/PlayerController.cs`
- `Assets/TurnOrchestrator.cs`
- `Assets/BaseTurnManager.cs`

**Behaviour Before**
- Area entry built the roster and exploration participant list correctly.
- Initial NPC exploration turns ran correctly.
- Player turn start was logged.
- Exploration player-turn completion did not have authoritative lifecycle diagnostics.
- End-turn routing depended on a cached orchestrator reference.

**Behaviour After**
- `PlayerController` refreshes its cached orchestrator from `TurnOrchestrator.Instance` on demand through `ResolveTurnOrchestrator()`.
- `TryGetEndTurnAvailability()` and `TryCompletePlayerTurnFromPlayerController()` now use the resolved live orchestrator.
- Missing-orchestrator failure now logs explicit turn diagnostics instead of failing silently.
- Player turn completion now logs at:
  - `PlayerController.TryCompletePlayerTurnFromPlayerController`
  - `TurnOrchestrator.PlayerTurnCompleted`
  - `BaseTurnManager.PlayerTurnCompleted`

**Why This Fix Was Chosen**
- It is narrow.
- It preserves the deterministic turn manager architecture.
- It does not change combat rules or exploration movement rules.
- It strengthens the exact lifecycle seam that the latest logs failed to prove.

**Manual Tests**
1. Enter an area with multiple NPCs.
2. Confirm `TurnOrchestrator.EnterExplorationArea setup completed` shows player plus NPCs in the roster and exploration participant list.
3. Confirm NPC exploration turns run before the player turn.
4. Press `End Turn` immediately on the player turn.
5. Confirm logs now show:
   - `PlayerController.TryCompletePlayerTurnFromPlayerController`
   - `TurnOrchestrator.PlayerTurnCompleted`
   - `ExplorationTurnManager.PlayerTurnCompleted`
   - `ExplorationTurnManager.ContinueTurnSequence`
   - subsequent `ExplorationTurnManager.OnNPCTurnExecute`
6. Confirm NPCs move/act once each and the next player turn arrives.
7. Repeat after spending some movement first, then pressing `End Turn`.
8. Enter combat and confirm the same logging seam remains valid without changing combat behaviour.

**Remaining Risks**
- This pass is source-level validated only; Unity play-mode was not rerun here.
- The latest available turn diagnostics showed the exploration cycle up to player-turn start, but not a captured manual end-turn event in that session.
- If NPCs still do not move after this patch, the next likely fault is not roster registration but a runtime ownership/continuation block inside the exploration manager after `PlayerTurnCompleted`. The new diagnostics should make that explicit.
