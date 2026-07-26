using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class TinyAdventureBatchAudit
{
    private static readonly HashSet<int> SmokeSeedSubset = new HashSet<int>(new[] { 1001, 1002, 1003, 1004, 1005 });

    private static bool IsSmokeScenario(string scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario))
        {
            return false;
        }

        string normalized = NormalizeScenario(scenario);
        return normalized == ScenarioExplorationSmoke
            || normalized == ScenarioInteractionSmoke
            || normalized == ScenarioExplorationInteraction;
    }

    private static bool ShouldRunExplorationSmoke(string scenario)
    {
        string normalized = NormalizeScenario(scenario ?? string.Empty);
        return normalized == ScenarioExplorationSmoke
            || normalized == ScenarioExplorationInteraction
            || normalized == ScenarioAll;
    }

    private static bool ShouldRunInteractionSmoke(string scenario)
    {
        string normalized = NormalizeScenario(scenario ?? string.Empty);
        return normalized == ScenarioInteractionSmoke
            || normalized == ScenarioExplorationInteraction
            || normalized == ScenarioAll;
    }

    private static void RunSmokeScenariosIfRequested(BatchAuditReport report, string requestedScenario)
    {
        if (report == null || report.Seeds == null || report.Seeds.Count == 0)
        {
            return;
        }

        bool runExploration = ShouldRunExplorationSmoke(requestedScenario);
        bool runInteraction = ShouldRunInteractionSmoke(requestedScenario);
        if (!runExploration && !runInteraction)
        {
            return;
        }

        foreach (var seed in report.Seeds.Where(seed => SmokeSeedSubset.Contains(seed.Seed)))
        {
            if (!seed.Success)
            {
                continue;
            }

            bool smokeFailed = false;
            if (runExploration)
            {
                seed.ExplorationSmoke = RunExplorationSmokeForSeed(seed);
                smokeFailed |= seed.ExplorationSmoke != null && seed.ExplorationSmoke.Failed;
            }

            if (runInteraction)
            {
                seed.InteractionSmoke = RunInteractionSmokeForSeed(seed);
                smokeFailed |= seed.InteractionSmoke != null && seed.InteractionSmoke.Failed;
            }

            if (smokeFailed)
            {
                seed.Success = false;
            }
        }
    }

    private static ExplorationSmokeSeedReport RunExplorationSmokeForSeed(SeedAuditReport run)
    {
        var result = new ExplorationSmokeSeedReport
        {
            Seed = run.Seed,
            Ran = true
        };

        int warningStart = CollectedWarnings.Count;
        int errorStart = CollectedErrors.Count;
        int loggedErrorStart = run.LoggedErrors.Count;
        int hardExceptionStart = run.HardExceptions.Count;
        int generatedStateStart = run.GeneratedStateFailures.Count;
        int failureStart = run.FailureReasons.Count;

        try
        {
            var scene = EditorSceneManager.GetActiveScene();
            BootstrapSmokeSingletons(scene);

            var playerController = FindSceneComponent(typeof(PlayerController), scene) as PlayerController;
            var playerStats = PlayerStats.Instance;
            var mapGenerator = FindSceneComponent(typeof(MapGenerator), scene) as MapGenerator;
            var playerInventory = EnsurePlayerInventory();

            if (playerController != null)
            {
                mapGenerator ??= playerController.mapGenerator;
            }

            if (playerController == null)
            {
                result.PartiallyBlocked = true;
                result.BlockedReason = "PlayerController scene component was not available.";
            }

            if (playerStats == null)
            {
                result.PartiallyBlocked = true;
                result.BlockedReason = AppendBlockReason(result.BlockedReason, "PlayerStats.Instance was unavailable.");
            }

            if (mapGenerator == null)
            {
                result.PartiallyBlocked = true;
                result.BlockedReason = AppendBlockReason(result.BlockedReason, "MapGenerator scene component was not available.");
            }

            if (playerController == null || playerStats == null || mapGenerator == null)
            {
                return FinalizeExplorationSmokeResult(run, result, warningStart, errorStart, loggedErrorStart, hardExceptionStart, generatedStateStart, failureStart);
            }

            if (playerStats.CurrentPlayerCharacter == null)
            {
                playerStats.UpdateCurrentPlayerCharacter();
            }

            var playerCharacter = playerStats.CurrentPlayerCharacter;
            if (playerCharacter == null)
            {
                result.PartiallyBlocked = true;
                result.BlockedReason = AppendBlockReason(result.BlockedReason, "No current player character could be resolved.");
                return FinalizeExplorationSmokeResult(run, result, warningStart, errorStart, loggedErrorStart, hardExceptionStart, generatedStateStart, failureStart);
            }

            result.PlayerResolved = true;

            InvokeMethod(playerController, "AssignPlayerToStartCell");
            playerController.TryEnterOrGenerateNestedArea();

            var area = playerCharacter.CurrentNestedArea ?? playerStats.CurrentNestedArea;
            if (area == null)
            {
                result.PartiallyBlocked = true;
                result.BlockedReason = AppendBlockReason(result.BlockedReason, "Exploration mode could not be entered or resolved headlessly.");
                return FinalizeExplorationSmokeResult(run, result, warningStart, errorStart, loggedErrorStart, hardExceptionStart, generatedStateStart, failureStart);
            }

            result.ExplorationModeEntered = true;
            result.CurrentAreaName = area.Name;
            result.CurrentAreaId = area.NestedAreaID;

            Vector2Int startPosition = playerCharacter.NestedMapPosition;
            Cell startCell = area.GetCellAtPosition(startPosition);
            result.StartPosition = startPosition.ToString();
            result.StartCell = startCell != null ? $"{startCell.CellID} @ {startCell.Coordinates}" : "NULL";
            result.StartCellValid = startCell != null && area.IsValidPosition(startPosition);

            if (!result.StartCellValid)
            {
                result.Failed = true;
                result.BlockedReason = AppendBlockReason(result.BlockedReason, "Player start cell in exploration area was invalid.");
                run.GeneratedStateFailures.Add("Player start cell in exploration area was invalid.");
            }

            playerCharacter.ResetMovePointsForTurn();

            var attemptedDirections = GetPassableDirections(area, startPosition).Take(2).ToList();
            result.AttemptedMovementCount = attemptedDirections.Count;

            if (attemptedDirections.Count == 0)
            {
                result.PartiallyBlocked = true;
                result.BlockedReason = AppendBlockReason(result.BlockedReason, "No passable adjacent cells were available for movement.");
            }
            else
            {
                foreach (var direction in attemptedDirections)
                {
                    Vector2Int before = playerCharacter.NestedMapPosition;
                    bool moved = TryMoveViaPlayerController(playerController, direction);
                    if (moved)
                    {
                        result.SuccessfulMovementCount++;
                        result.SafeActionNames.Add($"Move:{direction}");
                    }
                    else
                    {
                        result.FailedMovementCount++;
                    }

                    if (playerCharacter.NestedMapPosition != before && moved)
                    {
                        string syncWarning = ValidateExplorationMovementState(playerStats, playerCharacter, area);
                        if (!string.IsNullOrEmpty(syncWarning))
                        {
                            result.StateConsistencyWarnings.Add(syncWarning);
                            run.GeneratedStateFailures.Add(syncWarning);
                        }
                    }
                    else if (!moved)
                    {
                        result.PartiallyBlocked = true;
                        result.BlockedReason = AppendBlockReason(result.BlockedReason, $"Movement toward {direction} was not possible.");
                    }
                }
            }

            result.FinalPosition = playerCharacter.NestedMapPosition.ToString();
            string finalSyncWarning = ValidateExplorationMovementState(playerStats, playerCharacter, area);
            if (!string.IsNullOrEmpty(finalSyncWarning))
            {
                result.StateConsistencyWarnings.Add(finalSyncWarning);
                run.GeneratedStateFailures.Add(finalSyncWarning);
                result.Failed = true;
            }

            string transitionWarning = ValidateNestedAreaRoundTripState(playerController, playerStats, playerCharacter, area);
            if (!string.IsNullOrEmpty(transitionWarning))
            {
                result.StateConsistencyWarnings.Add(transitionWarning);
                run.GeneratedStateFailures.Add(transitionWarning);
                result.Failed = true;
            }
        }
        catch (Exception ex)
        {
            result.HardExceptions.Add(ex.ToString());
            result.HardExceptionCount++;
            result.Failed = true;
            run.HardExceptions.Add(ex.ToString());
            run.FailureReasons.Add("Exploration smoke threw " + ex.GetType().Name);
            IncrementReason("Exploration smoke threw " + ex.GetType().Name);
            Debug.LogException(ex);
        }

        return FinalizeExplorationSmokeResult(run, result, warningStart, errorStart, loggedErrorStart, hardExceptionStart, generatedStateStart, failureStart);
    }

    private static InteractionSmokeSeedReport RunInteractionSmokeForSeed(SeedAuditReport run)
    {
        var result = new InteractionSmokeSeedReport
        {
            Seed = run.Seed,
            Ran = true
        };

        int warningStart = CollectedWarnings.Count;
        int errorStart = CollectedErrors.Count;
        int loggedErrorStart = run.LoggedErrors.Count;
        int hardExceptionStart = run.HardExceptions.Count;
        int generatedStateStart = run.GeneratedStateFailures.Count;
        int failureStart = run.FailureReasons.Count;

        try
        {
            var scene = EditorSceneManager.GetActiveScene();
            BootstrapSmokeSingletons(scene);

            var playerController = FindSceneComponent(typeof(PlayerController), scene) as PlayerController;
            var playerStats = PlayerStats.Instance;
            var mapGenerator = FindSceneComponent(typeof(MapGenerator), scene) as MapGenerator;
            var actionManager = FindSceneComponent(typeof(ActionManager), scene) as ActionManager;
            var playerInventory = EnsurePlayerInventory();

            if (playerController != null)
            {
                mapGenerator ??= playerController.mapGenerator;
                actionManager ??= playerController.actionManager;
            }

            if (actionManager != null)
            {
                actionManager.InitializeEnvironmentalActions();
            }

            if (playerController == null)
            {
                result.PartiallyBlocked = true;
                result.BlockedReason = "PlayerController scene component was not available.";
            }

            if (playerStats == null)
            {
                result.PartiallyBlocked = true;
                result.BlockedReason = AppendBlockReason(result.BlockedReason, "PlayerStats.Instance was unavailable.");
            }

            if (mapGenerator == null)
            {
                result.PartiallyBlocked = true;
                result.BlockedReason = AppendBlockReason(result.BlockedReason, "MapGenerator scene component was not available.");
            }

            if (playerController == null || playerStats == null || mapGenerator == null)
            {
                return FinalizeInteractionSmokeResult(run, result, warningStart, errorStart, loggedErrorStart, hardExceptionStart, generatedStateStart, failureStart);
            }

            if (playerStats.CurrentPlayerCharacter == null)
            {
                playerStats.UpdateCurrentPlayerCharacter();
            }

            var playerCharacter = playerStats.CurrentPlayerCharacter;
            if (playerCharacter == null)
            {
                result.PartiallyBlocked = true;
                result.BlockedReason = AppendBlockReason(result.BlockedReason, "No current player character could be resolved.");
                return FinalizeInteractionSmokeResult(run, result, warningStart, errorStart, loggedErrorStart, hardExceptionStart, generatedStateStart, failureStart);
            }

            result.PlayerResolved = true;

            InvokeMethod(playerController, "AssignPlayerToStartCell");
            playerController.TryEnterOrGenerateNestedArea();

            var area = playerCharacter.CurrentNestedArea ?? playerStats.CurrentNestedArea;
            if (area == null)
            {
                result.PartiallyBlocked = true;
                result.BlockedReason = AppendBlockReason(result.BlockedReason, "Exploration mode could not be entered or resolved headlessly.");
                return FinalizeInteractionSmokeResult(run, result, warningStart, errorStart, loggedErrorStart, hardExceptionStart, generatedStateStart, failureStart);
            }

            Cell currentCell = area.GetCellAtPosition(playerCharacter.NestedMapPosition);
            result.StartCell = currentCell != null ? $"{currentCell.CellID} @ {currentCell.Coordinates}" : "NULL";
            result.CurrentCell = currentCell != null ? currentCell.Coordinates.ToString() : "NULL";
            if (currentCell == null)
            {
                result.Failed = true;
                result.BlockedReason = AppendBlockReason(result.BlockedReason, "Current cell in exploration area was invalid.");
                run.GeneratedStateFailures.Add("Current cell in exploration area was invalid.");
            }

            var candidateCells = GetNearbyCells(area, playerCharacter.NestedMapPosition);
            var interactables = CollectInteractables(candidateCells).ToList();
            result.CandidateInteractablesFound = interactables.Count;
            result.ActionProvidersInspected = interactables.Count + (actionManager != null && currentCell != null ? 1 : 0);

            var discoveredInteractions = new List<(IInteractable Provider, IInteraction Interaction)>();
            var discoveredEnvironmentalActions = new List<IEnvironmentalAction>();
            foreach (var interactable in interactables)
            {
                if (interactable == null)
                {
                    result.NullActionCount++;
                    continue;
                }

                IEnumerable<IInteraction> available;
                try
                {
                    available = interactable.GetAvailableInteractions(playerInventory) ?? Enumerable.Empty<IInteraction>();
                }
                catch (Exception ex)
                {
                    result.HardExceptions.Add(ex.ToString());
                    result.HardExceptionCount++;
                    result.Failed = true;
                    run.HardExceptions.Add(ex.ToString());
                    run.FailureReasons.Add("Interaction discovery threw " + ex.GetType().Name);
                    Debug.LogException(ex);
                    continue;
                }

                foreach (var interaction in available)
                {
                    if (interaction == null)
                    {
                        result.NullActionCount++;
                        continue;
                    }

                    result.ActionsDiscovered++;
                    string actionName = interaction.Name;
                    if (string.IsNullOrWhiteSpace(actionName))
                    {
                        result.Failed = true;
                        string actionWarning = $"Null or empty action name discovered from {FormatInteractable(interactable)}.";
                        result.StateConsistencyWarnings.Add(actionWarning);
                        run.GeneratedStateFailures.Add(actionWarning);
                        result.NullActionCount++;
                        continue;
                    }

                    result.DiscoveredActionNames.Add($"{actionName} [{interaction.Type}]");
                    if (IsDuplicateAction(discoveredInteractions, interaction))
                    {
                        result.DuplicateActionCount++;
                    }
                    if (IsUiBoundInteraction(interaction))
                    {
                        result.UiBoundActionsSkipped++;
                    }
                    else if (!IsSafeInteraction(interaction))
                    {
                        result.UnsafeActionsSkipped++;
                    }
                    discoveredInteractions.Add((interactable, interaction));
                }
            }

            if (actionManager != null && currentCell != null)
            {
                IEnumerable<IEnvironmentalAction> environmentalActions;
                try
                {
                    environmentalActions = actionManager.GetAvailableEnvironmentalActions(currentCell, playerInventory) ?? Enumerable.Empty<IEnvironmentalAction>();
                }
                catch (Exception ex)
                {
                    result.HardExceptions.Add(ex.ToString());
                    result.HardExceptionCount++;
                    result.Failed = true;
                    run.HardExceptions.Add(ex.ToString());
                    run.FailureReasons.Add("Environmental action discovery threw " + ex.GetType().Name);
                    Debug.LogException(ex);
                    environmentalActions = Enumerable.Empty<IEnvironmentalAction>();
                }

                foreach (var action in environmentalActions)
                {
                    if (action == null)
                    {
                        result.NullActionCount++;
                        continue;
                    }

                    result.ActionsDiscovered++;
                    result.DiscoveredActionNames.Add($"{action.Name} [{action.Type}]");
                    if (IsSafeEnvironmentalAction(action))
                    {
                        discoveredEnvironmentalActions.Add(action);
                    }
                    else
                    {
                        result.UnsafeActionsSkipped++;
                    }
                    if (string.IsNullOrWhiteSpace(action.Name))
                    {
                        result.Failed = true;
                        string envActionWarning = "Environmental action with null or empty name discovered.";
                        result.StateConsistencyWarnings.Add(envActionWarning);
                        run.GeneratedStateFailures.Add(envActionWarning);
                        result.NullActionCount++;
                    }
                }
            }

            var safeTarget = discoveredInteractions
                .FirstOrDefault(entry => IsSafeInteraction(entry.Interaction) && !IsUiBoundInteraction(entry.Interaction));

            if (safeTarget.Interaction == null)
            {
                var safeEnvironmentalAction = discoveredEnvironmentalActions.FirstOrDefault(IsSafeEnvironmentalAction);
                if (safeEnvironmentalAction == null)
                {
                    result.PartiallyBlocked = true;
                    result.BlockedReason = AppendBlockReason(result.BlockedReason, "No safe non-destructive interaction was discovered.");
                }
                else
                {
                    result.SafeActionsExecuted++;
                    result.SafeActionNames.Add(safeEnvironmentalAction.Name);
                    var beforeState = SnapshotInteractionState(playerStats, playerCharacter, currentCell, playerInventory);
                    safeEnvironmentalAction.ExecuteAction(currentCell, playerInventory);
                    var afterState = SnapshotInteractionState(playerStats, playerCharacter, currentCell, playerInventory);

                    var forbiddenMutationWarning = CompareInteractionSnapshots(beforeState, afterState);
                    if (!string.IsNullOrEmpty(forbiddenMutationWarning))
                    {
                        result.StateConsistencyWarnings.Add(forbiddenMutationWarning);
                        run.GeneratedStateFailures.Add(forbiddenMutationWarning);
                        result.Failed = true;
                    }
                }
            }
            else
            {
                result.SafeActionsExecuted++;
                result.SafeActionNames.Add(safeTarget.Interaction.Name);

                var beforeState = SnapshotInteractionState(playerStats, playerCharacter, currentCell, playerInventory);
                ExecuteSafeInteraction(safeTarget.Provider, safeTarget.Interaction, playerInventory);
                var afterState = SnapshotInteractionState(playerStats, playerCharacter, currentCell, playerInventory);

                var forbiddenMutationWarning = CompareInteractionSnapshots(beforeState, afterState);
                if (!string.IsNullOrEmpty(forbiddenMutationWarning))
                {
                    result.StateConsistencyWarnings.Add(forbiddenMutationWarning);
                    run.GeneratedStateFailures.Add(forbiddenMutationWarning);
                    result.Failed = true;
                }
            }

            string syncWarning = ValidateInteractionState(playerStats, playerCharacter, area, currentCell);
            if (!string.IsNullOrEmpty(syncWarning))
            {
                result.StateConsistencyWarnings.Add(syncWarning);
                run.GeneratedStateFailures.Add(syncWarning);
                result.Failed = true;
            }
        }
        catch (Exception ex)
        {
            result.HardExceptions.Add(ex.ToString());
            result.HardExceptionCount++;
            result.Failed = true;
            run.HardExceptions.Add(ex.ToString());
            run.FailureReasons.Add("Interaction smoke threw " + ex.GetType().Name);
            IncrementReason("Interaction smoke threw " + ex.GetType().Name);
            Debug.LogException(ex);
        }

        return FinalizeInteractionSmokeResult(run, result, warningStart, errorStart, loggedErrorStart, hardExceptionStart, generatedStateStart, failureStart);
    }

    private static void BootstrapSmokeSingletons(UnityEngine.SceneManagement.Scene scene)
    {
        BootstrapOptionalSingleton<UIController>(scene);
        BootstrapOptionalSingleton<InspectionManager>(scene);
        BootstrapOptionalSingleton<EndOfTurnManager>(scene);
        BootstrapOptionalSingleton<MessageLogManager>(scene);
        BootstrapOptionalSingleton<AudioController>(scene);
        BootstrapOptionalSingleton<PlayerController>(scene);
    }

    private static void BootstrapOptionalSingleton<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
    {
        var component = FindSceneComponent(typeof(T), scene) as T;
        if (component != null)
        {
            SetSingletonInstance(component);
        }
    }

    private static PlayerInventory EnsurePlayerInventory()
    {
        var field = typeof(PlayerInventory).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        if (field != null && field.GetValue(null) is PlayerInventory existing)
        {
            return existing;
        }

        PlayerInventory.Initialize();
        return PlayerInventory.Instance;
    }

    private static bool TryMoveViaPlayerController(PlayerController playerController, Direction direction)
    {
        var vector = DirectionToVector(direction);
        object result = InvokeMethod(playerController, "Move", vector);
        return result is bool b && b;
    }

    private static Vector2Int DirectionToVector(Direction direction)
    {
        return direction switch
        {
            Direction.North => Vector2Int.up,
            Direction.South => Vector2Int.down,
            Direction.West => Vector2Int.left,
            Direction.East => Vector2Int.right,
            _ => Vector2Int.zero
        };
    }

    private static IEnumerable<Direction> GetPassableDirections(INestedArea area, Vector2Int origin)
    {
        foreach (Direction direction in new[] { Direction.North, Direction.South, Direction.West, Direction.East })
        {
            Vector2Int target = origin + DirectionToVector(direction);
            if (area.IsValidPosition(target) && area.IsPassable(target))
            {
                yield return direction;
            }
        }
    }

    private static IEnumerable<IInteractable> CollectInteractables(IEnumerable<Cell> cells)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in cells)
        {
            if (cell == null)
            {
                continue;
            }

            foreach (var interactable in (cell.Objects ?? Enumerable.Empty<IInteractable>()).OfType<IInteractable>())
            {
                if (interactable == null)
                {
                    continue;
                }

                string key = GetInteractableKey(interactable);
                if (seen.Add(key))
                {
                    yield return interactable;
                }
            }

            foreach (var interactable in (cell.Items ?? Enumerable.Empty<Item>()).OfType<IInteractable>())
            {
                if (interactable == null)
                {
                    continue;
                }

                string key = GetInteractableKey(interactable);
                if (seen.Add(key))
                {
                    yield return interactable;
                }
            }

            foreach (var interactable in (cell.Animals ?? Enumerable.Empty<Animal>()).OfType<IInteractable>())
            {
                if (interactable == null)
                {
                    continue;
                }

                string key = GetInteractableKey(interactable);
                if (seen.Add(key))
                {
                    yield return interactable;
                }
            }

            foreach (var interactable in (cell.NPCs ?? Enumerable.Empty<NPC>()).OfType<IInteractable>())
            {
                if (interactable == null)
                {
                    continue;
                }

                string key = GetInteractableKey(interactable);
                if (seen.Add(key))
                {
                    yield return interactable;
                }
            }
        }
    }

    private static IEnumerable<Cell> GetNearbyCells(INestedArea area, Vector2Int origin)
    {
        yield return area.GetCellAtPosition(origin);
        yield return area.GetCellAtPosition(origin + Vector2Int.up);
        yield return area.GetCellAtPosition(origin + Vector2Int.down);
        yield return area.GetCellAtPosition(origin + Vector2Int.left);
        yield return area.GetCellAtPosition(origin + Vector2Int.right);
    }

    private static string GetInteractableKey(IInteractable interactable)
    {
        return $"{interactable.GetType().FullName}:{interactable.IInteractableID}";
    }

    private static string FormatInteractable(IInteractable interactable)
    {
        return interactable == null ? "NULL" : $"{interactable.Name} [{interactable.IInteractableID}] ({interactable.GetType().Name})";
    }

    private static bool IsDuplicateAction(List<(IInteractable Provider, IInteraction Interaction)> discoveredInteractions, IInteraction interaction)
    {
        if (discoveredInteractions == null || interaction == null)
        {
            return false;
        }

        return discoveredInteractions.Any(entry =>
            entry.Interaction != null &&
            string.Equals(entry.Interaction.GetType().FullName, interaction.GetType().FullName, StringComparison.Ordinal) &&
            string.Equals(entry.Interaction.Name, interaction.Name, StringComparison.Ordinal) &&
            entry.Interaction.Type == interaction.Type &&
            entry.Interaction.ActionPointCost == interaction.ActionPointCost);
    }

    private static bool IsSafeInteraction(IInteraction interaction)
    {
        if (interaction == null)
        {
            return false;
        }

        if (interaction is InspectInteraction || interaction is InspectNPCInteraction)
        {
            return true;
        }

        return string.Equals(interaction.Name, "Inspect", StringComparison.OrdinalIgnoreCase)
            || string.Equals(interaction.Name, "Inspect NPC", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUiBoundInteraction(IInteraction interaction)
    {
        if (interaction == null)
        {
            return false;
        }

        return interaction is TalkInteraction || interaction is TradeInteraction;
    }

    private static bool IsSafeEnvironmentalAction(IEnvironmentalAction action)
    {
        if (action == null)
        {
            return false;
        }

        return action is InspectItemsAction
            || string.Equals(action.Name, "Inspect Items", StringComparison.OrdinalIgnoreCase);
    }

    private static void ExecuteSafeInteraction(IInteractable provider, IInteraction interaction, PlayerInventory inventory)
    {
        if (interaction is IEnvironmentalAction environmentalAction && provider is Cell cell)
        {
            environmentalAction.ExecuteAction(cell, inventory);
            return;
        }

        interaction.ExecuteInteraction(provider, inventory);
    }

    private static (int InventoryItemCount, int CellObjectCount, int CellItemCount, int CellAnimalCount, int CellNpcCount, Vector2Int PlayerPosition) SnapshotInteractionState(
        PlayerStats playerStats,
        Character playerCharacter,
        Cell currentCell,
        PlayerInventory inventory)
    {
        int inventoryItemCount = inventory?.GetInventoryContainers()?.Sum(container => container?.Items?.Count ?? 0) ?? 0;
        return (
            InventoryItemCount: inventoryItemCount,
            CellObjectCount: currentCell?.Objects?.Count ?? -1,
            CellItemCount: currentCell?.Items?.Count ?? -1,
            CellAnimalCount: currentCell?.Animals?.Count ?? -1,
            CellNpcCount: currentCell?.NPCs?.Count ?? -1,
            PlayerPosition: playerCharacter?.NestedMapPosition ?? Vector2Int.zero
        );
    }

    private static string CompareInteractionSnapshots(
        (int InventoryItemCount, int CellObjectCount, int CellItemCount, int CellAnimalCount, int CellNpcCount, Vector2Int PlayerPosition) before,
        (int InventoryItemCount, int CellObjectCount, int CellItemCount, int CellAnimalCount, int CellNpcCount, Vector2Int PlayerPosition) after)
    {
        if (before.InventoryItemCount != after.InventoryItemCount)
        {
            return $"Inventory item count changed during a supposedly safe interaction: before={before.InventoryItemCount}, after={after.InventoryItemCount}.";
        }

        if (before.CellObjectCount != after.CellObjectCount ||
            before.CellItemCount != after.CellItemCount ||
            before.CellAnimalCount != after.CellAnimalCount ||
            before.CellNpcCount != after.CellNpcCount)
        {
            return "Cell contents changed during a supposedly safe interaction.";
        }

        if (before.PlayerPosition != after.PlayerPosition)
        {
            return "Player position changed during a supposedly safe interaction.";
        }

        return string.Empty;
    }

    private static string ValidateNestedAreaRoundTripState(
        PlayerController playerController,
        PlayerStats playerStats,
        Character playerCharacter,
        INestedArea originalArea)
    {
        if (playerController == null || playerStats == null || playerCharacter == null || originalArea == null)
        {
            return "Nested-area round-trip validation could not complete because one or more references were null.";
        }

        object exitResult = InvokeMethod(playerController, "ExitNestedArea");
        _ = exitResult;

        if (playerStats.CurrentNestedArea != null || playerCharacter.CurrentNestedArea != null)
        {
            return $"Nested-area exit did not clear the active area mirrors. PlayerStats={FormatArea(playerStats.CurrentNestedArea)}, PlayerCharacter={FormatArea(playerCharacter.CurrentNestedArea)}.";
        }

        if (playerStats.IsInNestedArea || !playerStats.IsInMainMap)
        {
            return $"Nested-area exit did not restore main-map flags. IsInNestedArea={playerStats.IsInNestedArea}, IsInMainMap={playerStats.IsInMainMap}.";
        }

        if (TurnOrchestrator.Instance != null && TurnOrchestrator.Instance.CurrentContext != TurnContext.MainMap)
        {
            return $"Nested-area exit did not return TurnOrchestrator to MainMap. Actual={TurnOrchestrator.Instance.CurrentContext}.";
        }

        playerController.TryEnterOrGenerateNestedArea();

        if (playerStats.CurrentNestedArea != originalArea || playerCharacter.CurrentNestedArea != originalArea)
        {
            return $"Nested-area re-entry did not recover the same area instance. Expected={FormatArea(originalArea)}, PlayerStats={FormatArea(playerStats.CurrentNestedArea)}, PlayerCharacter={FormatArea(playerCharacter.CurrentNestedArea)}.";
        }

        if (playerStats.CurrentNestedAreaID != originalArea.NestedAreaID || playerCharacter.CurrentNestedAreaID != originalArea.NestedAreaID)
        {
            return $"Nested-area re-entry did not restore the expected nested area ID. Expected={originalArea.NestedAreaID}, PlayerStats={playerStats.CurrentNestedAreaID}, PlayerCharacter={playerCharacter.CurrentNestedAreaID}.";
        }

        if (TurnOrchestrator.Instance != null && TurnOrchestrator.Instance.CurrentContext != TurnContext.Exploration)
        {
            return $"Nested-area re-entry did not restore Exploration context. Actual={TurnOrchestrator.Instance.CurrentContext}.";
        }

        return string.Empty;
    }

    private static string ValidateExplorationMovementState(PlayerStats playerStats, Character playerCharacter, INestedArea area)
    {
        if (playerStats == null || playerCharacter == null || area == null)
        {
            return "Exploration state validation could not complete because one or more references were null.";
        }

        if (playerCharacter.CurrentNestedArea != area)
        {
            return $"Player character nested area drifted away from the active area. Expected={FormatArea(area)}, Actual={FormatArea(playerCharacter.CurrentNestedArea)}.";
        }

        if (playerStats.CurrentNestedArea != area)
        {
            return $"PlayerStats nested area drifted away from the active area. Expected={FormatArea(area)}, Actual={FormatArea(playerStats.CurrentNestedArea)}.";
        }

        if (playerStats.NestedMapPosition != playerCharacter.NestedMapPosition)
        {
            return $"PlayerStats nested position drifted away from the player character. Expected={playerCharacter.NestedMapPosition}, Actual={playerStats.NestedMapPosition}.";
        }

        if (area.GetCellAtPosition(playerCharacter.NestedMapPosition) == null)
        {
            return $"Player character position {playerCharacter.NestedMapPosition} does not resolve to a valid cell.";
        }

        if (playerStats.CurrentCell != area.GetCellAtPosition(playerCharacter.NestedMapPosition))
        {
            return "PlayerStats current cell drifted away from the player's actual cell.";
        }

        return string.Empty;
    }

    private static string ValidateInteractionState(PlayerStats playerStats, Character playerCharacter, INestedArea area, Cell currentCell)
    {
        if (playerStats == null || playerCharacter == null || area == null)
        {
            return "Interaction state validation could not complete because one or more references were null.";
        }

        if (playerCharacter.CurrentNestedArea != area)
        {
            return $"Player character nested area drifted away from the active area. Expected={FormatArea(area)}, Actual={FormatArea(playerCharacter.CurrentNestedArea)}.";
        }

        if (playerStats.CurrentNestedArea != area)
        {
            return $"PlayerStats nested area drifted away from the active area. Expected={FormatArea(area)}, Actual={FormatArea(playerStats.CurrentNestedArea)}.";
        }

        if (playerStats.NestedMapPosition != playerCharacter.NestedMapPosition)
        {
            return $"PlayerStats nested position drifted away from the player character. Expected={playerCharacter.NestedMapPosition}, Actual={playerStats.NestedMapPosition}.";
        }

        if (currentCell != null && area.GetCellAtPosition(playerCharacter.NestedMapPosition) != currentCell)
        {
            return "Current cell reference drifted after interaction discovery or execution.";
        }

        if (playerStats.CurrentCell != area.GetCellAtPosition(playerCharacter.NestedMapPosition))
        {
            return "PlayerStats current cell drifted away from the player's actual cell.";
        }

        return string.Empty;
    }

    private static string AppendBlockReason(string existing, string addition)
    {
        if (string.IsNullOrWhiteSpace(existing))
        {
            return addition;
        }

        if (string.IsNullOrWhiteSpace(addition))
        {
            return existing;
        }

        return existing + " | " + addition;
    }

    private static ExplorationSmokeSeedReport FinalizeExplorationSmokeResult(
        SeedAuditReport run,
        ExplorationSmokeSeedReport result,
        int warningStart,
        int errorStart,
        int loggedErrorStart,
        int hardExceptionStart,
        int generatedStateStart,
        int failureStart)
    {
        CaptureSmokeLogDelta(run, result.StateConsistencyWarnings, result.UnexpectedUnityErrors, warningStart, errorStart, loggedErrorStart);

        result.UnexpectedErrorCount = result.UnexpectedUnityErrors.Count;
        result.GeneratedStateValidationFailureCount = run.GeneratedStateFailures.Count - generatedStateStart;
        result.HardExceptionCount = run.HardExceptions.Count - hardExceptionStart;
        result.Failed = result.Failed || result.HardExceptionCount > 0 || result.UnexpectedErrorCount > 0 || result.GeneratedStateValidationFailureCount > 0;
        result.Passed = !result.Failed;
        if (result.PartiallyBlocked && result.Failed)
        {
            result.PartiallyBlocked = false;
        }

        if (result.Failed)
        {
            result.BlockedReason = AppendBlockReason(result.BlockedReason, "Exploration smoke validation failed.");
            run.Success = false;
            run.FailureReasons.Add("Exploration smoke validation failed.");
            IncrementReason("Exploration smoke validation failed.");
        }

        if (!result.Failed && result.PartiallyBlocked && string.IsNullOrWhiteSpace(result.BlockedReason))
        {
            result.BlockedReason = "Exploration smoke was partially blocked by current architecture.";
        }

        if (run.FailureReasons.Count > failureStart && result.Failed)
        {
            // Preserve the baseline failure reasons while still surfacing the new smoke failure.
        }

        result.StateConsistencyWarnings = result.StateConsistencyWarnings.Distinct().OrderBy(x => x).ToList();
        return result;
    }

    private static InteractionSmokeSeedReport FinalizeInteractionSmokeResult(
        SeedAuditReport run,
        InteractionSmokeSeedReport result,
        int warningStart,
        int errorStart,
        int loggedErrorStart,
        int hardExceptionStart,
        int generatedStateStart,
        int failureStart)
    {
        CaptureSmokeLogDelta(run, result.StateConsistencyWarnings, result.UnexpectedUnityErrors, warningStart, errorStart, loggedErrorStart);

        result.UnexpectedErrorCount = result.UnexpectedUnityErrors.Count;
        result.GeneratedStateValidationFailureCount = run.GeneratedStateFailures.Count - generatedStateStart;
        result.HardExceptionCount = run.HardExceptions.Count - hardExceptionStart;
        result.Failed = result.Failed || result.HardExceptionCount > 0 || result.UnexpectedErrorCount > 0 || result.GeneratedStateValidationFailureCount > 0;
        result.Passed = !result.Failed;
        if (result.PartiallyBlocked && result.Failed)
        {
            result.PartiallyBlocked = false;
        }

        if (result.Failed)
        {
            result.BlockedReason = AppendBlockReason(result.BlockedReason, "Interaction smoke validation failed.");
            run.Success = false;
            run.FailureReasons.Add("Interaction smoke validation failed.");
            IncrementReason("Interaction smoke validation failed.");
        }

        if (!result.Failed && result.PartiallyBlocked && string.IsNullOrWhiteSpace(result.BlockedReason))
        {
            result.BlockedReason = "Interaction smoke was partially blocked by current architecture.";
        }

        if (run.FailureReasons.Count > failureStart && result.Failed)
        {
            // Preserve the baseline failure reasons while still surfacing the new smoke failure.
        }

        result.StateConsistencyWarnings = result.StateConsistencyWarnings.Distinct().OrderBy(x => x).ToList();
        result.SafeActionNames = result.SafeActionNames.Distinct().OrderBy(x => x).ToList();
        result.DiscoveredActionNames = result.DiscoveredActionNames.Distinct().OrderBy(x => x).ToList();
        return result;
    }

    private static void CaptureSmokeLogDelta(
        SeedAuditReport run,
        List<string> warnings,
        List<string> unexpectedErrors,
        int warningStart,
        int errorStart,
        int loggedErrorStart)
    {
        var newWarnings = CollectedWarnings.Skip(warningStart).Where(entry => !string.IsNullOrWhiteSpace(entry)).ToList();
        foreach (var warning in newWarnings)
        {
            warnings.Add(warning);
            if (!WarningReasonCounts.TryGetValue(warning, out int count))
            {
                WarningReasonCounts[warning] = 1;
            }
            else
            {
                WarningReasonCounts[warning] = count + 1;
            }

            RegisterWarningCategory(run.Seed, warning);
        }

        var newErrors = CollectedErrors.Skip(errorStart).Where(entry => !string.IsNullOrWhiteSpace(entry)).ToList();
        ClassifyLoggedErrors(run.Seed, run, newErrors);
        unexpectedErrors.AddRange(run.LoggedErrors.Skip(loggedErrorStart));
    }

    private static string FormatArea(INestedArea area)
    {
        return area == null ? "NULL" : $"{area.Name} [{area.NestedAreaID}]";
    }

    private static ExplorationSmokeReport BuildExplorationSmokeReport(BatchAuditReport report, string selectedScenario)
    {
        var seedResults = report.Seeds
            .Select(seed => seed.ExplorationSmoke)
            .Where(result => result != null && result.Ran)
            .ToList();

        var recommendations = seedResults.Count > 0
            ? BuildExplorationSmokeRecommendations(seedResults)
            : new List<string>();

        return new ExplorationSmokeReport
        {
            ScenarioName = ScenarioExplorationSmoke,
            Ran = seedResults.Count > 0,
            Passed = seedResults.Count > 0 && seedResults.All(result => result.Passed),
            SeedCount = seedResults.Count,
            PassedCount = seedResults.Count(result => result.Passed),
            FailedCount = seedResults.Count(result => result.Failed),
            PartiallyBlockedCount = seedResults.Count(result => result.PartiallyBlocked && !result.Failed),
            HardExceptionCount = seedResults.Sum(result => result.HardExceptionCount),
            UnexpectedErrorCount = seedResults.Sum(result => result.UnexpectedErrorCount),
            GeneratedStateValidationFailureCount = seedResults.Sum(result => result.GeneratedStateValidationFailureCount),
            WarningCount = seedResults.Sum(result => result.StateConsistencyWarnings.Count),
            SeedResults = seedResults,
            BlockedReasons = seedResults.Select(result => result.BlockedReason)
                .Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Distinct()
                .OrderBy(reason => reason)
                .ToList(),
            MovementAttemptedCount = seedResults.Sum(result => result.AttemptedMovementCount),
            MovementSuccessfulCount = seedResults.Sum(result => result.SuccessfulMovementCount),
            MovementFailedCount = seedResults.Sum(result => result.FailedMovementCount),
            TopFailureReasons = seedResults
                .SelectMany(result => result.HardExceptions
                    .Concat(result.StateConsistencyWarnings)
                    .Concat(string.IsNullOrWhiteSpace(result.BlockedReason) ? Enumerable.Empty<string>() : new[] { result.BlockedReason }))
                .GroupBy(reason => reason)
                .Select(group => new LogIssueSummary { Message = group.Key, Count = group.Count(), CausedAuditFailure = true })
                .OrderByDescending(summary => summary.Count)
                .ThenBy(summary => summary.Message)
                .Take(10)
                .ToList(),
            TopWarningReasons = seedResults
                .SelectMany(result => result.StateConsistencyWarnings)
                .GroupBy(reason => reason)
                .Select(group => new LogIssueSummary { Message = group.Key, Count = group.Count(), CausedAuditFailure = false })
                .OrderByDescending(summary => summary.Count)
                .ThenBy(summary => summary.Message)
                .Take(10)
                .ToList(),
            WarningCategories = BuildWarningCategories(seedResults.SelectMany(result => result.StateConsistencyWarnings)),
            RecommendedNextSteps = recommendations
        };
    }

    private static InteractionSmokeReport BuildInteractionSmokeReport(BatchAuditReport report, string selectedScenario)
    {
        var seedResults = report.Seeds
            .Select(seed => seed.InteractionSmoke)
            .Where(result => result != null && result.Ran)
            .ToList();

        var recommendations = seedResults.Count > 0
            ? BuildInteractionSmokeRecommendations(seedResults)
            : new List<string>();

        return new InteractionSmokeReport
        {
            ScenarioName = ScenarioInteractionSmoke,
            Ran = seedResults.Count > 0,
            Passed = seedResults.Count > 0 && seedResults.All(result => result.Passed),
            SeedCount = seedResults.Count,
            PassedCount = seedResults.Count(result => result.Passed),
            FailedCount = seedResults.Count(result => result.Failed),
            PartiallyBlockedCount = seedResults.Count(result => result.PartiallyBlocked && !result.Failed),
            HardExceptionCount = seedResults.Sum(result => result.HardExceptionCount),
            UnexpectedErrorCount = seedResults.Sum(result => result.UnexpectedErrorCount),
            GeneratedStateValidationFailureCount = seedResults.Sum(result => result.GeneratedStateValidationFailureCount),
            WarningCount = seedResults.Sum(result => result.StateConsistencyWarnings.Count),
            SeedResults = seedResults,
            BlockedReasons = seedResults.Select(result => result.BlockedReason)
                .Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Distinct()
                .OrderBy(reason => reason)
                .ToList(),
            CandidateInteractablesFound = seedResults.Sum(result => result.CandidateInteractablesFound),
            ActionProvidersInspected = seedResults.Sum(result => result.ActionProvidersInspected),
            ActionsDiscovered = seedResults.Sum(result => result.ActionsDiscovered),
            NullActionCount = seedResults.Sum(result => result.NullActionCount),
            DuplicateActionCount = seedResults.Sum(result => result.DuplicateActionCount),
            SafeActionsExecuted = seedResults.Sum(result => result.SafeActionsExecuted),
            UnsafeActionsSkipped = seedResults.Sum(result => result.UnsafeActionsSkipped),
            UiBoundActionsSkipped = seedResults.Sum(result => result.UiBoundActionsSkipped),
            TopFailureReasons = seedResults
                .SelectMany(result => result.HardExceptions
                    .Concat(result.StateConsistencyWarnings)
                    .Concat(string.IsNullOrWhiteSpace(result.BlockedReason) ? Enumerable.Empty<string>() : new[] { result.BlockedReason }))
                .GroupBy(reason => reason)
                .Select(group => new LogIssueSummary { Message = group.Key, Count = group.Count(), CausedAuditFailure = true })
                .OrderByDescending(summary => summary.Count)
                .ThenBy(summary => summary.Message)
                .Take(10)
                .ToList(),
            TopWarningReasons = seedResults
                .SelectMany(result => result.StateConsistencyWarnings)
                .GroupBy(reason => reason)
                .Select(group => new LogIssueSummary { Message = group.Key, Count = group.Count(), CausedAuditFailure = false })
                .OrderByDescending(summary => summary.Count)
                .ThenBy(summary => summary.Message)
                .Take(10)
                .ToList(),
            WarningCategories = BuildWarningCategories(seedResults.SelectMany(result => result.StateConsistencyWarnings)),
            RecommendedNextSteps = recommendations
        };
    }

    private static ExplorationInteractionReport BuildExplorationInteractionReport(BatchAuditReport report, string selectedScenario)
    {
        var exploration = BuildExplorationSmokeReport(report, selectedScenario);
        var interaction = BuildInteractionSmokeReport(report, selectedScenario);
        bool ran = exploration.Ran && interaction.Ran;
        var combinedSeeds = ran
            ? report.Seeds.Where(seed => seed.ExplorationSmoke?.Ran == true && seed.InteractionSmoke?.Ran == true).ToList()
            : new List<SeedAuditReport>();

        var recommendations = combinedSeeds.Count > 0
            ? BuildCombinedSmokeRecommendations(exploration, interaction)
            : new List<string>();

        return new ExplorationInteractionReport
        {
            ScenarioName = ScenarioExplorationInteraction,
            Ran = ran,
            Passed = combinedSeeds.Count > 0 && combinedSeeds.All(seed =>
                (seed.ExplorationSmoke == null || seed.ExplorationSmoke.Passed || seed.ExplorationSmoke.PartiallyBlocked) &&
                (seed.InteractionSmoke == null || seed.InteractionSmoke.Passed || seed.InteractionSmoke.PartiallyBlocked)),
            SeedCount = combinedSeeds.Count,
            PassedCount = combinedSeeds.Count(seed =>
                (seed.ExplorationSmoke == null || seed.ExplorationSmoke.Passed || seed.ExplorationSmoke.PartiallyBlocked) &&
                (seed.InteractionSmoke == null || seed.InteractionSmoke.Passed || seed.InteractionSmoke.PartiallyBlocked)),
            FailedCount = combinedSeeds.Count(seed =>
                (seed.ExplorationSmoke != null && seed.ExplorationSmoke.Failed) ||
                (seed.InteractionSmoke != null && seed.InteractionSmoke.Failed)),
            PartiallyBlockedCount = combinedSeeds.Count(seed =>
                (seed.ExplorationSmoke != null && seed.ExplorationSmoke.PartiallyBlocked && !seed.ExplorationSmoke.Failed) ||
                (seed.InteractionSmoke != null && seed.InteractionSmoke.PartiallyBlocked && !seed.InteractionSmoke.Failed)),
            HardExceptionCount = combinedSeeds.Sum(seed => (seed.ExplorationSmoke?.HardExceptionCount ?? 0) + (seed.InteractionSmoke?.HardExceptionCount ?? 0)),
            UnexpectedErrorCount = combinedSeeds.Sum(seed => (seed.ExplorationSmoke?.UnexpectedErrorCount ?? 0) + (seed.InteractionSmoke?.UnexpectedErrorCount ?? 0)),
            GeneratedStateValidationFailureCount = combinedSeeds.Sum(seed => (seed.ExplorationSmoke?.GeneratedStateValidationFailureCount ?? 0) + (seed.InteractionSmoke?.GeneratedStateValidationFailureCount ?? 0)),
            WarningCount = combinedSeeds.Sum(seed => (seed.ExplorationSmoke?.StateConsistencyWarnings.Count ?? 0) + (seed.InteractionSmoke?.StateConsistencyWarnings.Count ?? 0)),
            Exploration = exploration,
            Interaction = interaction,
            BlockedReasons = combinedSeeds.SelectMany(seed => new[]
                {
                    seed.ExplorationSmoke?.BlockedReason,
                    seed.InteractionSmoke?.BlockedReason
                })
                .Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Distinct()
                .OrderBy(reason => reason)
                .ToList(),
            RecommendedNextSteps = recommendations
        };
    }

    private static List<WarningCategoryRecord> BuildWarningCategories(IEnumerable<string> warnings)
    {
        var tracker = new Dictionary<string, WarningCategorySummary>(StringComparer.OrdinalIgnoreCase);
        foreach (string warning in warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)))
        {
            string category = ClassifyWarning(warning);
            string recommendedAction = GetRecommendedActionForWarningCategory(category);
            bool causesAuditFailure = category == WarningCategoryNames.PotentialGeneratorDefect;

            if (!tracker.TryGetValue(category, out var summary))
            {
                summary = new WarningCategorySummary
                {
                    Category = category,
                    CausesAuditFailure = causesAuditFailure,
                    RecommendedAction = recommendedAction
                };
                tracker[category] = summary;
            }

            summary.TotalCount++;
            if (!summary.DistinctMessages.Contains(warning))
            {
                summary.DistinctMessages.Add(warning);
            }
        }

        return tracker.Values
            .OrderByDescending(summary => summary.TotalCount)
            .ThenBy(summary => summary.Category)
            .Select(summary => summary.ToRecord())
            .ToList();
    }

    private static List<string> BuildExplorationSmokeRecommendations(IEnumerable<ExplorationSmokeSeedReport> seeds)
    {
        var recommendations = new List<string>();
        if (seeds.Any(seed => seed.PartiallyBlocked))
        {
            recommendations.Add("Expose a tiny non-UI movement wrapper if the controller path remains partially blocked.");
        }

        if (seeds.Any(seed => seed.Failed))
        {
            recommendations.Add("Inspect the controller movement path for state-sync bugs before broadening exploration coverage.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Exploration smoke is currently healthy; broaden seed coverage only if movement remains stable.");
        }

        return recommendations;
    }

    private static List<string> BuildInteractionSmokeRecommendations(IEnumerable<InteractionSmokeSeedReport> seeds)
    {
        var recommendations = new List<string>();
        if (seeds.Any(seed => seed.PartiallyBlocked))
        {
            recommendations.Add("Expose a headless-safe action discovery wrapper if the current interaction path remains UI-bound.");
        }

        if (seeds.Any(seed => seed.Failed))
        {
            recommendations.Add("Inspect safe interaction execution and scene singleton initialization for hidden coupling.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Interaction smoke is currently healthy; broaden only after more non-destructive actions are exposed headlessly.");
        }

        return recommendations;
    }

    private static List<string> BuildCombinedSmokeRecommendations(ExplorationSmokeReport exploration, InteractionSmokeReport interaction)
    {
        var recommendations = new List<string>();
        recommendations.AddRange(exploration.RecommendedNextSteps);
        recommendations.AddRange(interaction.RecommendedNextSteps);
        return recommendations.Distinct().OrderBy(text => text).ToList();
    }
}
