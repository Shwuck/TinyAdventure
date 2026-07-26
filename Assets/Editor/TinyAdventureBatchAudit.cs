using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class TinyAdventureBatchAudit
{
    private const string ScenarioGeneration = "generation";
    private const string ScenarioContentWarnings = "content-warnings";
    private const string ScenarioExplorationSmoke = "exploration-smoke";
    private const string ScenarioInteractionSmoke = "interaction-smoke";
    private const string ScenarioExplorationInteraction = "exploration-interaction";
    private const string ScenarioAll = "all";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string DefaultReportPath = @"C:\Temp\tiny_adventure_batch_audit.json";
    private const int DefaultRunCount = 50;
    private static readonly int[] BaseDeterminismSeeds = { 1001, 1002, 1003, 1004, 1005, 2001, 2002, 2003, 3001, 9999 };
    private static readonly int[] ExpandedSeedPool =
    {
        1001, 1002, 1003, 1004, 1005, 2001, 2002, 2003, 3001, 9999,
        4001, 4002, 4003, 4004, 4005, 4006, 4007, 4008, 4009, 4010,
        4011, 4012, 4013, 4014, 4015, 4016, 4017, 4018, 4019, 4020,
        4021, 4022, 4023, 4024, 4025, 4026, 4027, 4028, 4029, 4030,
        4031, 4032, 4033, 4034, 4035, 4036, 4037, 4038, 4039, 4040
    };

    private static readonly HashSet<string> KnownOptionalMissingContentMessages = new HashSet<string>
    {
        "LootCreationData.json not found in StreamingAssets!",
        "EntityLootData.json not found in StreamingAssets!",
        "MonsterCreationData.json not found in StreamingAssets!"
    };

    private static readonly string[] LoaderTypeOrder =
    {
        "AnatomyDataLoader",
        "MaterialDataLoader",
        "ItemDataLoader",
        "AnimalDataLoader",
        "NPCDataLoader",
        "PersonalityDataLoader",
        "DialogueDataLoader",
        "VillageDataLoader",
        "LandmarkDataLoader",
        "LootDataLoader",
        "CraftingRecipeDataLoader",
        "CookingRecipeDataLoader",
        "SmithingRecipeDataLoader",
        "EventDataLoader",
        "DungeonDataLoader"
    };

    private static readonly Type[] SingletonBootstrapTypes =
    {
        typeof(GameManager),
        typeof(PermaLists),
        typeof(PlayerStats),
        typeof(RaceManager),
        typeof(MapGenerator),
        typeof(NestedAreaGenerator),
        typeof(IntegrityChecker),
        typeof(DialogueManager),
        typeof(TimeManager),
        typeof(TurnOrchestrator),
        typeof(CivilisationManager),
        typeof(NewsManager),
        typeof(PlantFlowerManager),
        typeof(FactionManager),
        typeof(RegionManager),
        typeof(NPCGenerator),
        typeof(AnimalGenerator),
        typeof(AnatomyGenerator),
        typeof(ItemGenerator),
        typeof(DungeonGenerator),
        typeof(CaveGenerator),
        typeof(CampGenerator),
        typeof(WeatherManager),
        typeof(PlayerCharacterGenerator)
    };

    private static readonly Dictionary<string, int> FailureReasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> WarningReasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, KnownOptionalContentSummary> KnownOptionalMissingContentTracker = new Dictionary<string, KnownOptionalContentSummary>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, WarningCategorySummary> WarningCategoryTracker = new Dictionary<string, WarningCategorySummary>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> CollectedWarnings = new List<string>();
    private static readonly List<string> CollectedErrors = new List<string>();
    private static bool LoggedHeadlessCallTraceSkip;
    private static bool SuppressLogCapture;

    public static void RunBatchAudit()
    {
        var report = CreateReportSkeleton();
        int exitCode = 0;
        string reportPath = GetReportPath();
        string requestedScenario = GetRequestedScenario();
        int requestedRunCount = GetRequestedRunCount(requestedScenario);
        List<int> plannedSeeds = BuildPlannedSeeds(requestedScenario, requestedRunCount);

        Application.logMessageReceived += OnLogMessage;

        try
        {
            ClearLogBuckets();

            Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? Path.GetTempPath());

            Debug.Log("[TinyAdventureBatchAudit] Starting headless batch audit.");

            foreach (int seed in plannedSeeds)
            {
                var run = RunSeedAudit(seed, report, aggregateResults: true, captureLogs: true);
                report.Seeds.Add(run);
            }

            report.Determinism = RunDeterminismCheck(report, BaseDeterminismSeeds);

            RunSmokeScenariosIfRequested(report, requestedScenario);

            FinalizeReport(report);

            exitCode = DetermineExitCode(report);
            report.ExitCode = exitCode;
            report.ReportPath = reportPath;
            report.LogPath = TryGetLogPath();
            report.SelectedScenario = requestedScenario;
            report.ValidationWarnings = CollectedWarnings.Distinct().OrderBy(x => x).ToList();
            report.RuntimeErrors = report.Seeds
                .SelectMany(seed => seed.LoggedErrors)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
            report.FailureReasonCounts = FailureReasonCounts.OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            report.WarningReasonCounts = WarningReasonCounts.OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            report.KnownOptionalMissingContent = KnownOptionalMissingContentTracker.Values
                .OrderByDescending(summary => summary.Count)
                .ThenBy(summary => summary.Message)
                .Select(summary => summary.ToRecord())
                .ToList();
            report.ProjectInfo = new ProjectInfoReport
            {
                ProjectName = report.ProjectName,
                UnityVersion = report.UnityVersion,
                SceneLoaded = report.SceneLoaded
            };
            report.AuditSettings = new AuditSettingsReport
            {
                BatchMode = report.BatchMode,
                NoGraphics = report.NoGraphics,
                RequestedRunCount = requestedRunCount,
                SelectedScenario = requestedScenario,
                SeedsPlanned = plannedSeeds,
                DeterminismSubset = BaseDeterminismSeeds.ToList()
            };
            report.Summary = new SummaryReport
            {
                TotalRuns = report.TotalRuns,
                PassedRuns = report.AcceptedRuns,
                FailedRuns = report.RejectedRuns,
                HardExceptions = report.HardExceptionCount,
                CompileErrors = report.CompileErrorCount,
                DataLoadingFailures = report.DataLoadingFailureCount,
                SceneWiringFailures = report.SceneWiringFailureCount,
                GeneratedStateValidationFailures = report.GeneratedStateValidationFailureCount,
                ValidationWarnings = report.ValidationWarningCount
            };
            report.DataLoading = new DataLoadingReport
            {
                Attempted = report.Seeds.Sum(x => x.DataLoadersAttempted),
                Succeeded = report.Seeds.Sum(x => x.DataLoadersSucceeded),
                Failed = report.Seeds.Sum(x => x.DataLoadersFailed)
            };
            report.SceneWiring = new SceneWiringReport
            {
                FailureCount = report.SceneWiringFailureCount,
                Failures = report.Seeds.SelectMany(x => x.SceneWiringFailures).Distinct().OrderBy(x => x).ToList()
            };
            report.GenerationSummary = BuildGenerationSummary(report);
            report.UnityLogErrors = report.Seeds.SelectMany(x => x.LoggedErrors)
                .GroupBy(x => x)
                .Select(g => new LogIssueSummary { Message = g.Key, Count = g.Count(), CausedAuditFailure = false })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Message)
                .ToList();
            report.UnityLogWarnings = WarningReasonCounts.OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new LogIssueSummary { Message = kvp.Key, Count = kvp.Value, CausedAuditFailure = false })
                .ToList();
            report.FailureReasons = FailureReasonCounts.OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new LogIssueSummary { Message = kvp.Key, Count = kvp.Value, CausedAuditFailure = true })
                .ToList();
            report.WarningReasons = WarningReasonCounts.OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new LogIssueSummary { Message = kvp.Key, Count = kvp.Value, CausedAuditFailure = false })
                .ToList();
            report.RecommendedNextSteps = BuildRecommendedNextSteps(report);
            report.SeedResults = report.Seeds;
            report.UnexpectedUnityErrors = report.RuntimeErrors.ToList();
            report.OverallSummary = BuildOverallSummary(report);
            report.ScenarioResults = BuildScenarioResults(report, requestedScenario);

            WriteReport(reportPath, report);
            LogSummary(report);
        }
        catch (Exception ex)
        {
            exitCode = 1;
            report.ExitCode = exitCode;
            report.RuntimeErrors.Add(ex.ToString());
            Debug.LogException(ex);

            try
            {
                report.ReportPath = reportPath;
                report.LogPath = TryGetLogPath();
                WriteReport(reportPath, report);
            }
            catch (Exception writeEx)
            {
                Debug.LogException(writeEx);
            }

            LogSummary(report);
        }
        finally
        {
            Application.logMessageReceived -= OnLogMessage;
            EditorApplication.Exit(exitCode);
        }
    }

    private static BatchAuditReport CreateReportSkeleton()
    {
        return new BatchAuditReport
        {
            ProjectName = PlayerSettings.productName,
            UnityVersion = Application.unityVersion,
            AuditTimestampUtc = DateTime.UtcNow.ToString("o"),
            BatchMode = Application.isBatchMode,
            NoGraphics = IsNoGraphicsLikelyActive(),
            SeedsPlanned = ExpandedSeedPool.Take(DefaultRunCount).ToList(),
            SelectedScenario = ScenarioGeneration,
            Feasibility = new FeasibilityReport
            {
                CompileOnly = "supported",
                JsonDataLoading = "supported",
                SceneWiring = "supported with SampleScene",
                ProceduralGeneration = "supported",
                CharacterNpcAnimalItemGeneration = "supported",
                TurnSimulation = "partially blocked",
                FullDeterministicSimulation = "blocked"
            },
            CompileErrorCount = 0,
            ProjectInfo = new ProjectInfoReport(),
            AuditSettings = new AuditSettingsReport(),
            Summary = new SummaryReport(),
            DataLoading = new DataLoadingReport(),
            SceneWiring = new SceneWiringReport(),
            GenerationSummary = new GenerationSummaryReport(),
            OverallSummary = new OverallSummaryReport(),
            ScenarioResults = new ScenarioResultsReport(),
            KnownOptionalMissingContent = new List<KnownOptionalContentRecord>(),
            UnexpectedUnityErrors = new List<string>(),
            UnityLogErrors = new List<LogIssueSummary>(),
            UnityLogWarnings = new List<LogIssueSummary>(),
            FailureReasons = new List<LogIssueSummary>(),
            WarningReasons = new List<LogIssueSummary>(),
            ManualFollowUpTestsRecommended = new List<string>
            {
                "Open SampleScene in Play Mode once and confirm the UIController references are all assigned.",
                "Exercise one hostile encounter and confirm the combat turn loop advances without null UI references.",
                "Verify any nested-area entry flow that requires actual player movement or button-driven UI interactions.",
                "Confirm save/load and any scene-transition paths that depend on runtime-only state."
            }
        };
    }

    private static SeedAuditReport RunSeedAudit(int seed, BatchAuditReport aggregateReport, bool aggregateResults, bool captureLogs)
    {
        var run = new SeedAuditReport
        {
            Seed = seed
        };
        int warningStart = CollectedWarnings.Count;
        int errorStart = CollectedErrors.Count;

        try
        {
            ClearSingletonStatics();

            var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            run.ScenePath = scene.path;

            BootstrapSceneSingletons(scene, run);

            var dataLoadReport = LoadAllData(run);
            run.DataLoadersAttempted = dataLoadReport.Attempted;
            run.DataLoadersSucceeded = dataLoadReport.Succeeded;
            run.DataLoadersFailed = dataLoadReport.Failed;
            run.LoaderFailures.AddRange(dataLoadReport.Failures);

            var integrityReport = RunIntegrityCheck(run);
            if (!string.IsNullOrEmpty(integrityReport))
            {
                run.ValidationWarnings.Add(integrityReport);
            }

            ValidateLoadedData(run);

            var mapResult = RunMapGeneration(seed, run);
            run.MapGenerated = mapResult.MapGenerated;
            run.MapWidth = mapResult.MapWidth;
            run.MapHeight = mapResult.MapHeight;
            run.TotalCells = mapResult.TotalCells;
            run.StartCellId = mapResult.StartCellId;
            run.StartCellCoordinates = mapResult.StartCellCoordinates;
            run.StartCellTerrain = mapResult.StartCellTerrain;
            run.TerrainCounts = mapResult.TerrainCounts;
            run.RegionCount = mapResult.RegionCount;
            run.DungeonCount = mapResult.DungeonCount;
            run.CaveCount = mapResult.CaveCount;
            run.CampCount = mapResult.CampCount;
            run.NestedAreaCount = mapResult.NestedAreaCount;
            run.AnimalAssignmentCount = mapResult.AnimalAssignmentCount;
            run.ItemGenerationCount = mapResult.ItemGenerationCount;
            run.NpcGenerationCount = mapResult.NpcGenerationCount;
            run.PlayerCharacterCount = mapResult.PlayerCharacterCount;

            run.NestedAreaSmokeTest = TryGenerateNestedAreaSmokeTest(run);
            run.ItemSmokeTest = TryGenerateItemSmokeTest(run);
            run.CharacterSmokeTest = TryGenerateCharacterSmokeTest(run);
            run.AnimalSmokeTest = TryGenerateAnimalSmokeTest(run);

            ValidateWorldState(run);
            run.BaselineSuccess =
                run.HardExceptions.Count == 0 &&
                run.FailureReasons.Count == 0 &&
                run.LoaderFailures.Count == 0 &&
                run.SceneWiringFailures.Count == 0 &&
                run.GeneratedStateFailures.Count == 0;
            run.BaselineHardExceptionCount = run.HardExceptions.Count;
            run.BaselineLoggedErrorCount = run.LoggedErrors.Count;
            run.BaselineGeneratedStateFailureCount = run.GeneratedStateFailures.Count;
            run.BaselineValidationWarningCount = run.ValidationWarnings.Count;
            run.BaselineFailureReasons = run.FailureReasons.ToList();
            run.Success =
                run.BaselineSuccess;
        }
        catch (Exception ex)
        {
            run.Success = false;
            run.HardExceptions.Add(ex.ToString());
            run.FailureReasons.Add("Hard exception during seed audit: " + ex.GetType().Name);
            Debug.LogException(ex);
        }

        if (captureLogs)
        {
            var newWarnings = CollectedWarnings.Skip(warningStart).ToList();
            var newErrors = CollectedErrors.Skip(errorStart).ToList();
            run.ValidationWarnings.AddRange(newWarnings.Where(warning => !string.IsNullOrWhiteSpace(warning)));
            foreach (string warning in newWarnings.Where(warning => !string.IsNullOrWhiteSpace(warning)))
            {
                if (!WarningReasonCounts.TryGetValue(warning, out int count))
                {
                    WarningReasonCounts[warning] = 1;
                }
                else
                {
                    WarningReasonCounts[warning] = count + 1;
                }

                RegisterWarningCategory(seed, warning);
            }
            ClassifyLoggedErrors(seed, run, newErrors);
        }

        if (run.LoggedErrors.Count > 0)
        {
            run.FailureReasons.Add($"Unity logged {run.LoggedErrors.Count} error/exception messages during the run.");
            run.Success = false;
        }

        if (aggregateResults)
        {
            if (run.Success)
            {
                aggregateReport.AcceptedRuns++;
            }
            else
            {
                aggregateReport.RejectedRuns++;
            }

            aggregateReport.TotalRuns++;
            aggregateReport.HardExceptionCount += run.HardExceptions.Count;
            aggregateReport.DataLoadingFailureCount += run.DataLoadersFailed;
            aggregateReport.SceneWiringFailureCount += run.SceneWiringFailures.Count;
            aggregateReport.GeneratedStateValidationFailureCount += run.GeneratedStateFailures.Count;
            aggregateReport.ValidationWarningCount += run.ValidationWarnings.Count;
        }

        if (aggregateResults)
        {
            foreach (string reason in run.FailureReasons.Concat(run.LoaderFailures).Concat(run.SceneWiringFailures).Concat(run.GeneratedStateFailures))
            {
                IncrementReason(reason);
            }
        }

        return run;
    }

    private static void BootstrapSceneSingletons(UnityEngine.SceneManagement.Scene scene, SeedAuditReport run)
    {
        foreach (var type in SingletonBootstrapTypes)
        {
            var component = FindSceneComponent(type, scene);
            if (component == null)
            {
                run.SceneWiringFailures.Add($"Missing scene component: {type.Name}");
                continue;
            }

            SetSingletonInstance(component);

            if (type == typeof(MapGenerator))
            {
                ConfigureMapGenerator((MapGenerator)component, scene, run);
            }
            else if (type == typeof(PermaLists))
            {
                TryInvokeMethod(component, "InitializeLists");
            }
            else if (type == typeof(PlayerStats))
            {
                TryInvokeMethod(component, "InitializePlayerStats");
            }
            else if (type == typeof(AnimalGenerator))
            {
                TryInvokeMethod(component, "InitializePermaLists");
            }
            else if (type == typeof(CivilisationManager))
            {
                ConfigureCivilisationManager((CivilisationManager)component, scene, run);
            }
            else if (type == typeof(FactionManager))
            {
                ((FactionManager)component).mapGenerator = EnsureComponent<MapGenerator>(scene, run);
            }
            else if (type == typeof(NestedAreaGenerator))
            {
                ((NestedAreaGenerator)component).mapGenerator = EnsureComponent<MapGenerator>(scene, run);
            }
            else if (type == typeof(ItemGenerator))
            {
                // No extra wiring needed, but keep the instance warm for later smoke tests.
            }
            else if (type == typeof(GameManager) && !LoggedHeadlessCallTraceSkip)
            {
                run.ValidationWarnings.Add("Skipped GameManager.ApplyCallTraceSettings in headless audit because it depends on editor-only DontDestroyOnLoad behavior through GameDebugger.");
                LoggedHeadlessCallTraceSkip = true;
            }
        }
    }

    private static void ConfigureMapGenerator(MapGenerator mapGenerator, UnityEngine.SceneManagement.Scene scene, SeedAuditReport run)
    {
        mapGenerator.forestGenerator = EnsureSceneComponent<ForestGenerator>(scene, run);
        mapGenerator.swampGenerator = EnsureSceneComponent<SwampGenerator>(scene, run);
        mapGenerator.desertGenerator = EnsureSceneComponent<DesertGenerator>(scene, run);
        mapGenerator.riverGenerator = EnsureSceneComponent<RiverGenerator>(scene, run);
    }

    private static void ConfigureCivilisationManager(CivilisationManager civilisationManager, UnityEngine.SceneManagement.Scene scene, SeedAuditReport run)
    {
        civilisationManager.civilisationGenerator = EnsureSceneComponent<CivilisationGenerator>(scene, run);
        civilisationManager.npcGenerator = EnsureSceneComponent<NPCGenerator>(scene, run);
    }

    private static DataLoadSummary LoadAllData(SeedAuditReport run)
    {
        var summary = new DataLoadSummary();
        var scene = EditorSceneManager.GetActiveScene();
        var loaders = FindSceneMonoBehaviours(scene)
            .Where(component => component is IDataLoader)
            .OrderBy(component => LoaderSortIndex(component.GetType().Name))
            .ThenBy(component => component.GetType().Name)
            .ToList();

        // DungeonDataLoader exists as a public loader but not all scene configurations mark it with IDataLoader.
        var dungeonLoader = FindSceneComponent(typeof(DungeonDataLoader), scene);
        if (dungeonLoader != null && loaders.All(existing => existing.GetType() != dungeonLoader.GetType()))
        {
            loaders.Add((MonoBehaviour)dungeonLoader);
        }

        summary.Attempted = loaders.Count;

        foreach (var loaderComponent in loaders)
        {
            try
            {
                TryInvokeMethod(loaderComponent, "LoadData");
                summary.Succeeded++;
            }
            catch (Exception ex)
            {
                summary.Failed++;
                string failure = $"{loaderComponent.GetType().Name}: {ex.GetType().Name} - {ex.Message}";
                summary.Failures.Add(failure);
                run.LoaderFailures.Add(failure);
                Debug.LogException(ex);
            }
        }

        return summary;
    }

    private static string RunIntegrityCheck(SeedAuditReport run)
    {
        var integrityChecker = EnsureSceneComponent<IntegrityChecker>(EditorSceneManager.GetActiveScene(), run);
        if (integrityChecker == null)
        {
            return "IntegrityChecker is missing; data integrity validation skipped.";
        }

        try
        {
            integrityChecker.CheckDataIntegrity(false);
            return string.Empty;
        }
        catch (Exception ex)
        {
            run.HardExceptions.Add(ex.ToString());
            run.FailureReasons.Add("Integrity validation threw " + ex.GetType().Name);
            Debug.LogException(ex);
            return "Integrity validation threw an exception: " + ex.Message;
        }
    }

    private static void ValidateLoadedData(SeedAuditReport run)
    {
        var perma = PermaLists.Instance;
        if (perma == null)
        {
            run.FailureReasons.Add("PermaLists.Instance is null after bootstrap.");
            return;
        }

        RequireNonEmpty(perma.AnatomyData, "AnatomyData", run);
        RequireNonEmpty(perma.BodyPartData, "BodyPartData", run);
        RequireNonEmpty(perma.ItemCreationData, "ItemCreationData", run);
        RequireNonEmpty(perma.ObjectMaterials, "ObjectMaterials", run);
        RequireNonEmpty(perma.AnimalCreationData, "AnimalCreationData", run);
        RequireNonEmpty(perma.Races, "Races", run);
        RequireNonEmpty(perma.Backgrounds, "Backgrounds", run);
        RequireNonEmpty(perma.RoleData, "RoleData", run);
        RequireNonEmpty(perma.VillageCreationData, "VillageCreationData", run);
        RequireNonEmpty(perma.DialogueScripts, "DialogueScripts", run);
        RequireNonEmpty(perma.LootCreationData, "LootCreationData", run);
        RequireNonEmpty(perma.CraftingRecipeList, "CraftingRecipeList", run);
        RequireNonEmpty(perma.CookingRecipeList, "CookingRecipeList", run);
        RequireNonEmpty(perma.SmithingRecipeList, "SmithingRecipeList", run);
    }

    private static MapRunResult RunMapGeneration(int seed, SeedAuditReport run)
    {
        var result = new MapRunResult();
        var mapGenerator = EnsureSceneComponent<MapGenerator>(EditorSceneManager.GetActiveScene(), run);
        if (mapGenerator == null)
        {
            run.GeneratedStateFailures.Add("MapGenerator is missing.");
            return result;
        }

        var gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            run.GeneratedStateFailures.Add("GameManager.Instance is null.");
            return result;
        }

        try
        {
            gameManager.GameSeed = seed;
            gameManager.PlayerGivenSeed = seed;
            gameManager.ScenarioSeed = seed;
            gameManager.MapGenerated = false;
            gameManager.GoodToStart = false;
            gameManager.MapSet = false;
            gameManager.PlayerSet = false;

            mapGenerator.GenerateMap();

            result.MapGenerated = gameManager.MapGenerated;
            result.MapWidth = mapGenerator.width;
            result.MapHeight = mapGenerator.height;
            result.TotalCells = mapGenerator.allCells?.Count ?? 0;
            result.StartCellId = mapGenerator.startCell != null ? mapGenerator.startCell.CellID : -1;
            result.StartCellCoordinates = mapGenerator.startCell != null ? mapGenerator.startCell.Coordinates.ToString() : "NULL";
            result.StartCellTerrain = mapGenerator.startCell != null ? mapGenerator.startCell.Terrain.ToString() : "NULL";
            result.TerrainCounts = SnapshotTerrainCounts(mapGenerator);
            result.RegionCount = PermaLists.Instance.RegionInfoDictionary?.Count ?? 0;
            result.DungeonCount = PermaLists.Instance.DungeonCreationDataList?.Count ?? 0;
            result.CaveCount = PermaLists.Instance.CaveCreationDataList?.Count ?? 0;
            result.CampCount = PermaLists.Instance.Camps?.Count ?? 0;
            result.NestedAreaCount = PermaLists.Instance.AllNestedAreas?.Count ?? 0;
            result.AnimalAssignmentCount = PermaLists.Instance.AnimalsToGenerate?.Count ?? 0;
            result.ItemGenerationCount = PermaLists.Instance.ItemCreationData?.Count ?? 0;
            result.NpcGenerationCount = PermaLists.Instance.AllNPCs?.Count ?? 0;
            result.PlayerCharacterCount = PermaLists.Instance.PlayerCharacters?.Count ?? 0;

            ValidateMapStructure(mapGenerator, result, run);
        }
        catch (Exception ex)
        {
            run.HardExceptions.Add(ex.ToString());
            run.FailureReasons.Add("Map generation threw " + ex.GetType().Name);
            Debug.LogException(ex);
        }

        return result;
    }

    private static bool TryGenerateNestedAreaSmokeTest(SeedAuditReport run)
    {
        var nestedAreaGenerator = EnsureSceneComponent<NestedAreaGenerator>(EditorSceneManager.GetActiveScene(), run);
        var mapGenerator = EnsureSceneComponent<MapGenerator>(EditorSceneManager.GetActiveScene(), run);
        if (nestedAreaGenerator == null || mapGenerator == null || mapGenerator.startCell == null)
        {
            run.ValidationWarnings.Add("Nested-area smoke test skipped because the map or nested-area generator was unavailable.");
            return false;
        }

        nestedAreaGenerator.mapGenerator = mapGenerator;

        var cell = mapGenerator.startCell;
        try
        {
            nestedAreaGenerator.GenerateNestedArea(cell);
            bool generated = cell.hasNestedArea && cell.NestedArea != null;
            if (!generated)
            {
                run.GeneratedStateFailures.Add($"Nested-area smoke test did not attach a nested area to start cell {cell.CellID}.");
                return false;
            }

            ValidateGeneratedNestedAreaParentContract(cell.NestedArea, cell, run, "first-level nested area");

            var innerTargetCell = FindNestedAreaSmokeTargetCell(cell.NestedArea);
            if (innerTargetCell != null)
            {
                nestedAreaGenerator.GenerateNestedAreaWithinNestedArea(cell.NestedArea, innerTargetCell.Coordinates);

                if (!innerTargetCell.hasNestedArea || innerTargetCell.NestedArea == null)
                {
                    run.GeneratedStateFailures.Add($"Nested-area smoke test did not attach a child nested area to nested-area cell {innerTargetCell.CellID}.");
                }
                else
                {
                    ValidateGeneratedNestedAreaParentContract(innerTargetCell.NestedArea, innerTargetCell, run, "nested-area child");
                    if (innerTargetCell.NestedArea.MainMapCellID != cell.NestedArea.MainMapCellID)
                    {
                        run.GeneratedStateFailures.Add(
                            $"Nested-area child main-map ancestor mismatch. Expected={cell.NestedArea.MainMapCellID}, Actual={innerTargetCell.NestedArea.MainMapCellID}.");
                    }
                }
            }
            return generated;
        }
        catch (Exception ex)
        {
            run.HardExceptions.Add(ex.ToString());
            run.FailureReasons.Add("Nested-area smoke test threw " + ex.GetType().Name);
            Debug.LogException(ex);
            return false;
        }
    }

    private static void ValidateGeneratedNestedAreaParentContract(INestedArea nestedArea, Cell expectedParentCell, SeedAuditReport run, string label)
    {
        if (nestedArea == null)
        {
            run.GeneratedStateFailures.Add($"Nested-area smoke test could not validate {label} because the nested area was null.");
            return;
        }

        if (expectedParentCell == null)
        {
            run.GeneratedStateFailures.Add($"Nested-area smoke test could not validate {label} because the expected parent cell was null.");
            return;
        }

        if (nestedArea.ParentCell == null)
        {
            run.GeneratedStateFailures.Add($"Nested-area smoke test found a null ParentCell for {label} {nestedArea.Name} ({nestedArea.NestedAreaID}).");
        }
        else if (!ReferenceEquals(nestedArea.ParentCell, expectedParentCell))
        {
            run.GeneratedStateFailures.Add(
                $"Nested-area smoke test found the wrong ParentCell for {label} {nestedArea.Name} ({nestedArea.NestedAreaID}). Expected={expectedParentCell.CellID}, Actual={nestedArea.ParentCell.CellID}.");
        }

        if (nestedArea.ParentCellID != expectedParentCell.CellID)
        {
            run.GeneratedStateFailures.Add(
                $"Nested-area smoke test found a ParentCellID mismatch for {label} {nestedArea.Name} ({nestedArea.NestedAreaID}). Expected={expectedParentCell.CellID}, Actual={nestedArea.ParentCellID}.");
        }
    }

    private static Cell FindNestedAreaSmokeTargetCell(INestedArea area)
    {
        if (area == null)
        {
            return null;
        }

        foreach (var cell in area.GetNestedMap())
        {
            if (cell != null && !cell.hasNestedArea)
            {
                return cell;
            }
        }

        return null;
    }

    private static bool TryGenerateItemSmokeTest(SeedAuditReport run)
    {
        var itemGenerator = EnsureSceneComponent<ItemGenerator>(EditorSceneManager.GetActiveScene(), run);
        if (itemGenerator == null)
        {
            run.ValidationWarnings.Add("Item smoke test skipped because ItemGenerator is unavailable.");
            return false;
        }

        try
        {
            var item = itemGenerator.GenerateRandomWeapon(1);
            if (item == null)
            {
                run.GeneratedStateFailures.Add("ItemGenerator.GenerateRandomWeapon returned null.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            run.HardExceptions.Add(ex.ToString());
            run.FailureReasons.Add("Item smoke test threw " + ex.GetType().Name);
            Debug.LogException(ex);
            return false;
        }
    }

    private static bool TryGenerateCharacterSmokeTest(SeedAuditReport run)
    {
        var playerGenerator = EnsureSceneComponent<PlayerCharacterGenerator>(EditorSceneManager.GetActiveScene(), run);
        var npcGenerator = EnsureSceneComponent<NPCGenerator>(EditorSceneManager.GetActiveScene(), run);
        if (playerGenerator == null || npcGenerator == null)
        {
            run.ValidationWarnings.Add("Character smoke test skipped because PlayerCharacterGenerator or NPCGenerator is unavailable.");
            return false;
        }

        try
        {
            int playerBefore = PermaLists.Instance.PlayerCharacters?.Count ?? 0;
            int npcBefore = PermaLists.Instance.AllNPCs?.Count ?? 0;

            playerGenerator.GenerateNewPlayerCharacter();
            var npc = npcGenerator.GenerateStandaloneNPC();

            bool playerGenerated = (PermaLists.Instance.PlayerCharacters?.Count ?? 0) > playerBefore;
            bool npcGenerated = (PermaLists.Instance.AllNPCs?.Count ?? 0) > npcBefore && npc != null;

            if (!playerGenerated)
            {
                run.GeneratedStateFailures.Add("PlayerCharacterGenerator did not add a player character.");
            }

            if (!npcGenerated)
            {
                run.GeneratedStateFailures.Add("NPCGenerator.GenerateStandaloneNPC failed or returned null.");
            }

            return playerGenerated && npcGenerated;
        }
        catch (Exception ex)
        {
            run.HardExceptions.Add(ex.ToString());
            run.FailureReasons.Add("Character smoke test threw " + ex.GetType().Name);
            Debug.LogException(ex);
            return false;
        }
    }

    private static bool TryGenerateAnimalSmokeTest(SeedAuditReport run)
    {
        var animalGenerator = EnsureSceneComponent<AnimalGenerator>(EditorSceneManager.GetActiveScene(), run);
        var mapGenerator = EnsureSceneComponent<MapGenerator>(EditorSceneManager.GetActiveScene(), run);
        if (animalGenerator == null || mapGenerator == null || mapGenerator.startCell == null)
        {
            run.ValidationWarnings.Add("Animal smoke test skipped because the animal generator, map, or start cell was unavailable.");
            return false;
        }

        try
        {
            string animalName = PermaLists.Instance.AnimalCreationData != null && PermaLists.Instance.AnimalCreationData.Count > 0
                ? PermaLists.Instance.AnimalCreationData[0].AnimalName
                : null;

            if (string.IsNullOrEmpty(animalName))
            {
                run.GeneratedStateFailures.Add("No animal creation data was available for the animal smoke test.");
                return false;
            }

            int before = PermaLists.Instance.AllWildAnimals?.Count ?? 0;
            var animals = animalGenerator.GenerateAnimal(animalName, 1, mapGenerator.startCell);
            bool generated = animals != null && animals.Count > 0 && (PermaLists.Instance.AllWildAnimals?.Count ?? 0) > before;

            if (!generated)
            {
                run.GeneratedStateFailures.Add("AnimalGenerator.GenerateAnimal did not produce a wild animal.");
            }

            return generated;
        }
        catch (Exception ex)
        {
            run.HardExceptions.Add(ex.ToString());
            run.FailureReasons.Add("Animal smoke test threw " + ex.GetType().Name);
            Debug.LogException(ex);
            return false;
        }
    }

    private static void ValidateWorldState(SeedAuditReport run)
    {
        var mapGenerator = EnsureSceneComponent<MapGenerator>(EditorSceneManager.GetActiveScene(), run);
        if (mapGenerator == null)
        {
            run.GeneratedStateFailures.Add("MapGenerator missing during world validation.");
            return;
        }

        if (mapGenerator.map == null)
        {
            run.GeneratedStateFailures.Add("MapGenerator.map is null after generation.");
            return;
        }

        int expectedCells = mapGenerator.width * mapGenerator.height;
        if ((mapGenerator.allCells?.Count ?? 0) != expectedCells)
        {
            run.GeneratedStateFailures.Add($"MapGenerator.allCells count was {(mapGenerator.allCells?.Count ?? 0)} but expected {expectedCells}.");
        }

        if (mapGenerator.startCell == null)
        {
            run.GeneratedStateFailures.Add("MapGenerator.startCell is null after generation.");
        }
        else
        {
            if (!mapGenerator.IsPositionValid(mapGenerator.startCell.Coordinates))
            {
                run.GeneratedStateFailures.Add($"Start cell coordinates were invalid: {mapGenerator.startCell.Coordinates}.");
            }

            if (mapGenerator.GetCell(mapGenerator.startCell.Coordinates) == null)
            {
                run.GeneratedStateFailures.Add($"GetCell returned null for start cell coordinates {mapGenerator.startCell.Coordinates}.");
            }
        }

        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                if (mapGenerator.map[x, y] == null)
                {
                    run.GeneratedStateFailures.Add($"Null map cell found at [{x}, {y}].");
                    return;
                }
            }
        }
    }

    private static void ValidateMapStructure(MapGenerator mapGenerator, MapRunResult result, SeedAuditReport run)
    {
        if (mapGenerator.map == null)
        {
            run.GeneratedStateFailures.Add("MapGenerator.map remained null during map validation.");
            return;
        }

        if (mapGenerator.startCell == null)
        {
            run.GeneratedStateFailures.Add("No start cell was selected.");
        }

        if (PermaLists.Instance.AllMapCells == null || PermaLists.Instance.AllMapCells.Count == 0)
        {
            run.GeneratedStateFailures.Add("PermaLists.AllMapCells is empty after map generation.");
        }

        if (result.DungeonCount > 0)
        {
            var dungeons = PermaLists.Instance.DungeonCreationDataList ?? new List<DungeonCreationData>();
            foreach (var dungeon in dungeons)
            {
                var cell = mapGenerator.GetCellByID(dungeon.DungeonCellID);
                if (cell == null || !cell.HasDungeon)
                {
                    run.GeneratedStateFailures.Add($"Dungeon data {dungeon.DungeonID} was not attached to a valid dungeon cell.");
                    break;
                }
            }
        }

        if (result.CaveCount > 0)
        {
            var caves = PermaLists.Instance.CaveCreationDataList ?? new List<CaveCreationData>();
            foreach (var cave in caves)
            {
                var cell = mapGenerator.GetCellByID(cave.CaveCellID);
                if (cell == null || !cell.HasCave)
                {
                    run.GeneratedStateFailures.Add($"Cave data {cave.CaveID} was not attached to a valid cave cell.");
                    break;
                }
            }
        }

        if (result.CampCount > 0)
        {
            var camps = PermaLists.Instance.Camps ?? new List<Camp>();
            foreach (var camp in camps)
            {
                if (camp == null || camp.Location == null || !camp.Location.HasCamp)
                {
                    run.GeneratedStateFailures.Add("Camp generation produced an invalid camp record.");
                    break;
                }
            }
        }
    }

    private static Dictionary<string, int> SnapshotTerrainCounts(MapGenerator mapGenerator)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (mapGenerator.allCells == null)
        {
            return counts;
        }

        foreach (var cell in mapGenerator.allCells)
        {
            if (cell == null)
            {
                continue;
            }

            string key = cell.Terrain.ToString();
            counts.TryGetValue(key, out int value);
            counts[key] = value + 1;
        }

        return counts;
    }

    private static void RequireNonEmpty<T>(ICollection<T> collection, string name, SeedAuditReport run)
    {
        if (collection == null)
        {
            run.FailureReasons.Add($"{name} is null.");
            return;
        }

        if (collection.Count == 0)
        {
            run.ValidationWarnings.Add($"{name} is empty.");
        }
    }

    private static void RequireNonEmpty<TKey, TValue>(IDictionary<TKey, TValue> dictionary, string name, SeedAuditReport run)
    {
        if (dictionary == null)
        {
            run.FailureReasons.Add($"{name} is null.");
            return;
        }

        if (dictionary.Count == 0)
        {
            run.ValidationWarnings.Add($"{name} is empty.");
        }
    }

    private static int LoaderSortIndex(string typeName)
    {
        int index = Array.IndexOf(LoaderTypeOrder, typeName);
        return index >= 0 ? index : LoaderTypeOrder.Length + 1;
    }

    private static void FinalizeReport(BatchAuditReport report)
    {
        report.TotalRuns = report.Seeds.Count;
        report.AcceptedRuns = report.Seeds.Count(seed => seed.Success);
        report.RejectedRuns = report.TotalRuns - report.AcceptedRuns;
        report.FailedRuns = report.RejectedRuns;
        report.HardExceptionCount = report.Seeds.Sum(seed => seed.HardExceptions.Count);
        report.DataLoadingFailureCount = report.Seeds.Sum(seed => seed.DataLoadersFailed);
        report.SceneWiringFailureCount = report.Seeds.Sum(seed => seed.SceneWiringFailures.Count);
        report.GeneratedStateValidationFailureCount = report.Seeds.Sum(seed => seed.GeneratedStateFailures.Count);
        report.ValidationWarningCount = CollectedWarnings.Count;
        report.WorstAttempts = 1;
        report.AverageAttempts = 1.0;
        report.MostCommonFailureReasons = FailureReasonCounts
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .Select(kvp => $"{kvp.Key} ({kvp.Value})")
            .ToList();

        report.SceneLoaded = string.IsNullOrEmpty(report.SceneLoaded) ? SampleScenePath : report.SceneLoaded;
        report.UnityBatchMode = Application.isBatchMode;
        report.UnityGraphicsDevice = SystemInfo.graphicsDeviceType.ToString();
        report.HadHardExceptions = report.HardExceptionCount > 0 || report.RuntimeErrors.Count > 0;
        report.FailedRuns = report.Seeds.Count(run => !run.Success);
    }

    private static int DetermineExitCode(BatchAuditReport report)
    {
        if (report.HardExceptionCount > 0)
        {
            return 1;
        }

        if (report.FailedRuns > 0)
        {
            return 1;
        }

        return 0;
    }

    private static void WriteReport(string reportPath, BatchAuditReport report)
    {
        string json = JsonConvert.SerializeObject(report, Formatting.Indented);
        File.WriteAllText(reportPath, json);
    }

    private static void LogSummary(BatchAuditReport report)
    {
        Debug.Log($"[TinyAdventureBatchAudit] Project: {report.ProjectName}");
        Debug.Log($"[TinyAdventureBatchAudit] Unity: {report.UnityVersion}");
        Debug.Log($"[TinyAdventureBatchAudit] Scene: {report.SceneLoaded}");
        Debug.Log($"[TinyAdventureBatchAudit] BatchMode={report.BatchMode}, NoGraphics={report.NoGraphics}, GraphicsDevice={report.UnityGraphicsDevice}, Scenario={report.SelectedScenario}");
        Debug.Log($"[TinyAdventureBatchAudit] CompileErrors={report.CompileErrorCount}");
        Debug.Log($"[TinyAdventureBatchAudit] Runs={report.TotalRuns}, Passed={report.AcceptedRuns}, Failed={report.RejectedRuns}, HardExceptions={report.HardExceptionCount}");
        Debug.Log($"[TinyAdventureBatchAudit] DataLoaders attempted={report.Seeds.Sum(x => x.DataLoadersAttempted)}, succeeded={report.Seeds.Sum(x => x.DataLoadersSucceeded)}, failed={report.Seeds.Sum(x => x.DataLoadersFailed)}");
        Debug.Log($"[TinyAdventureBatchAudit] JSON report: {report.ReportPath}");
        if (report.ValidationWarnings.Count > 0)
        {
            Debug.LogWarning($"[TinyAdventureBatchAudit] Validation warnings: {string.Join(" | ", report.ValidationWarnings.Take(8))}");
        }

        if (report.MostCommonFailureReasons.Count > 0)
        {
            Debug.LogWarning($"[TinyAdventureBatchAudit] Most common failure reasons: {string.Join(" | ", report.MostCommonFailureReasons.Take(5))}");
        }

        if (report.ManualFollowUpTestsRecommended.Count > 0)
        {
            Debug.Log($"[TinyAdventureBatchAudit] Manual follow-up tests: {string.Join(" | ", report.ManualFollowUpTestsRecommended)}");
        }
    }

    private static void ClearSingletonStatics()
    {
        foreach (var type in SingletonBootstrapTypes)
        {
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.FieldType == type)
                {
                    field.SetValue(null, null);
                }
            }
        }
    }

    private static void SetSingletonInstance(object instance)
    {
        if (instance == null)
        {
            return;
        }

        var type = instance.GetType();
        foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType == type)
            {
                field.SetValue(null, instance);
            }
        }
    }

    private static void ClearLogBuckets()
    {
        FailureReasonCounts.Clear();
        WarningReasonCounts.Clear();
        KnownOptionalMissingContentTracker.Clear();
        WarningCategoryTracker.Clear();
        CollectedWarnings.Clear();
        CollectedErrors.Clear();
    }

    private static void ClassifyLoggedErrors(int seed, SeedAuditReport run, List<string> newErrors)
    {
        if (newErrors == null || newErrors.Count == 0)
        {
            return;
        }

        foreach (string message in newErrors.Where(error => !string.IsNullOrWhiteSpace(error)))
        {
            if (IsKnownOptionalMissingContentMessage(message))
            {
                run.KnownOptionalMissingContent.Add(message);
                RegisterKnownOptionalContent(seed, message);
                continue;
            }

            run.LoggedErrors.Add(message);
        }
    }

    private static bool IsKnownOptionalMissingContentMessage(string message)
    {
        return KnownOptionalMissingContentMessages.Contains(message);
    }

    private static void RegisterKnownOptionalContent(int seed, string message)
    {
        if (!KnownOptionalMissingContentTracker.TryGetValue(message, out var summary))
        {
            summary = new KnownOptionalContentSummary
            {
                Message = message,
                Note = "Intentionally tolerated for now by project-owner decision.",
                CausedAuditFailure = false
            };
            KnownOptionalMissingContentTracker[message] = summary;
        }

        summary.Count++;
        if (!summary.AffectedSeeds.Contains(seed))
        {
            summary.AffectedSeeds.Add(seed);
        }
    }

    private static GenerationSummaryReport BuildGenerationSummary(BatchAuditReport report)
    {
        return new GenerationSummaryReport
        {
            AverageMapWidth = report.Seeds.Count == 0 ? 0 : report.Seeds.Average(seed => seed.MapWidth),
            AverageMapHeight = report.Seeds.Count == 0 ? 0 : report.Seeds.Average(seed => seed.MapHeight),
            AverageCellCount = report.Seeds.Count == 0 ? 0 : report.Seeds.Average(seed => seed.TotalCells),
            AverageCaves = report.Seeds.Count == 0 ? 0 : report.Seeds.Average(seed => seed.CaveCount),
            AverageDungeons = report.Seeds.Count == 0 ? 0 : report.Seeds.Average(seed => seed.DungeonCount),
            AverageCamps = report.Seeds.Count == 0 ? 0 : report.Seeds.Average(seed => seed.CampCount),
            AverageNPCs = report.Seeds.Count == 0 ? 0 : report.Seeds.Average(seed => seed.NpcGenerationCount),
            AverageAnimals = report.Seeds.Count == 0 ? 0 : report.Seeds.Average(seed => seed.AnimalAssignmentCount),
            AverageItems = report.Seeds.Count == 0 ? 0 : report.Seeds.Average(seed => seed.ItemGenerationCount)
        };
    }

    private static OverallSummaryReport BuildOverallSummary(BatchAuditReport report)
    {
        return new OverallSummaryReport
        {
            TotalRuns = report.TotalRuns,
            PassedRuns = report.AcceptedRuns,
            FailedRuns = report.RejectedRuns,
            HardExceptions = report.HardExceptionCount,
            CompileErrors = report.CompileErrorCount,
            DataLoadingFailures = report.DataLoadingFailureCount,
            SceneWiringFailures = report.SceneWiringFailureCount,
            GeneratedStateValidationFailures = report.GeneratedStateValidationFailureCount,
            ValidationWarnings = report.ValidationWarningCount,
            UnexpectedUnityErrors = report.RuntimeErrors.Count
        };
    }

    private static ScenarioResultsReport BuildScenarioResults(BatchAuditReport report, string selectedScenario)
    {
        int baselineRunCount = report.Seeds.Count;
        int baselineAcceptedRuns = report.Seeds.Count(seed => seed.BaselineSuccess);
        int baselineRejectedRuns = baselineRunCount - baselineAcceptedRuns;
        int baselineHardExceptions = report.Seeds.Sum(seed => seed.BaselineHardExceptionCount);
        int baselineUnexpectedErrors = report.Seeds.Sum(seed => seed.BaselineLoggedErrorCount);
        int baselineWarnings = report.Seeds.Sum(seed => seed.BaselineValidationWarningCount);
        var baselineFailureReasonCounts = report.Seeds
            .SelectMany(seed => seed.BaselineFailureReasons)
            .GroupBy(reason => reason)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var baselineWarningReasonCounts = report.Seeds
            .SelectMany(seed => seed.ValidationWarnings)
            .GroupBy(reason => reason)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var baselineWarningCategories = BuildWarningCategories(report.Seeds.SelectMany(seed => seed.ValidationWarnings));

        var generation = BuildScenarioResult(
            "generation",
            ran: true,
            passed: baselineRejectedRuns == 0 && baselineHardExceptions == 0 && baselineUnexpectedErrors == 0,
            baselineRunCount,
            baselineRejectedRuns,
            baselineWarnings,
            baselineHardExceptions,
            baselineUnexpectedErrors,
            report.GenerationSummary,
            baselineWarningReasonCounts,
            baselineFailureReasonCounts);
        generation.WarningCategories = baselineWarningCategories;

        var contentWarnings = BuildScenarioResult(
            "content-warnings",
            ran: true,
            passed: baselineHardExceptions == 0 && baselineUnexpectedErrors == 0,
            baselineRunCount,
            baselineRejectedRuns,
            baselineWarnings,
            baselineHardExceptions,
            baselineUnexpectedErrors,
            report.GenerationSummary,
            baselineWarningReasonCounts,
            baselineFailureReasonCounts);
        contentWarnings.WarningCategories = baselineWarningCategories;

        var explorationSmoke = BuildExplorationSmokeReport(report, selectedScenario);
        var interactionSmoke = BuildInteractionSmokeReport(report, selectedScenario);
        var explorationInteraction = BuildExplorationInteractionReport(report, selectedScenario);

        return new ScenarioResultsReport
        {
            SelectedScenario = selectedScenario,
            Generation = generation,
            ContentWarnings = contentWarnings,
            ExplorationSmoke = explorationSmoke,
            InteractionSmoke = interactionSmoke,
            ExplorationInteraction = explorationInteraction
        };
    }

    private static ScenarioResultReport BuildScenarioResult(
        string scenarioName,
        bool ran,
        bool passed,
        int runCount,
        int failureCount,
        int warningCount,
        int hardExceptionCount,
        int unexpectedErrorCount,
        GenerationSummaryReport summaryMetrics,
        IReadOnlyDictionary<string, int> warningCounts,
        IReadOnlyDictionary<string, int> failureCounts)
    {
        return new ScenarioResultReport
        {
            ScenarioName = scenarioName,
            Ran = ran,
            Passed = passed,
            RunCount = runCount,
            FailureCount = failureCount,
            WarningCount = warningCount,
            HardExceptionCount = hardExceptionCount,
            UnexpectedErrorCount = unexpectedErrorCount,
            SummaryMetrics = summaryMetrics,
            TopFailureReasons = failureCounts.OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .Select(kvp => new LogIssueSummary { Message = kvp.Key, Count = kvp.Value, CausedAuditFailure = true })
                .ToList(),
            TopWarningReasons = warningCounts.OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .Select(kvp => new LogIssueSummary { Message = kvp.Key, Count = kvp.Value, CausedAuditFailure = false })
                .ToList(),
            WarningCategories = WarningCategoryTracker.Values
                .OrderByDescending(summary => summary.TotalCount)
                .ThenBy(summary => summary.Category)
                .Select(summary => summary.ToRecord())
                .ToList()
        };
    }

    private static void RegisterWarningCategory(int seed, string warning)
    {
        string category = ClassifyWarning(warning);
        string recommendedAction = GetRecommendedActionForWarningCategory(category);
        bool causesAuditFailure = category == WarningCategoryNames.PotentialGeneratorDefect;

        if (!WarningCategoryTracker.TryGetValue(category, out var summary))
        {
            summary = new WarningCategorySummary
            {
                Category = category,
                CausesAuditFailure = causesAuditFailure,
                RecommendedAction = recommendedAction
            };
            WarningCategoryTracker[category] = summary;
        }

        summary.TotalCount++;
        if (!summary.DistinctMessages.Contains(warning))
        {
            summary.DistinctMessages.Add(warning);
        }

        if (!summary.AffectedSeeds.Contains(seed))
        {
            summary.AffectedSeeds.Add(seed);
        }
    }

    private static string ClassifyWarning(string warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
        {
            return WarningCategoryNames.UnknownWarning;
        }

        if (KnownOptionalMissingContentMessages.Contains(warning))
        {
            return WarningCategoryNames.OptionalFutureContent;
        }

        if (warning.StartsWith("ItemCreationData not found for crop type:", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("No VineFruit found for the group", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("No VineFruit set for directions", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("No BushFruit found for the group", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("No BushFruit set for directions", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("No smithing recipe found for weapon", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("No native animals found for terrain type", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("No VillageCreationData found for race", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("LootCreationData is empty.", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("SmithingRecipeList is empty.", StringComparison.OrdinalIgnoreCase))
        {
            return WarningCategoryNames.ContentCompletenessGap;
        }

        if (warning.StartsWith("RaceManager:", StringComparison.OrdinalIgnoreCase) &&
            warning.IndexOf("did not have a MainRaceName", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return WarningCategoryNames.DataCanonicalization;
        }

        if (warning.StartsWith("Race ", StringComparison.OrdinalIgnoreCase) &&
            warning.IndexOf("not found in PermaLists.Instance.Races", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return WarningCategoryNames.DataCanonicalization;
        }

        if (warning.StartsWith("Count of ", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("Skipped GameManager.ApplyCallTraceSettings", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("Skipped tree placement", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("Skipped water placement", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("No valid position found for placing a rock.", StringComparison.OrdinalIgnoreCase) ||
            warning.StartsWith("PlayerInventory: Instance was accessed before initialization!", StringComparison.OrdinalIgnoreCase))
        {
            return WarningCategoryNames.GeneratorInfo;
        }

        if (warning.IndexOf("invalid EquipmentSlots", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return WarningCategoryNames.PotentialGeneratorDefect;
        }

        if (warning.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
            warning.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 ||
            warning.IndexOf("null", StringComparison.OrdinalIgnoreCase) >= 0 ||
            warning.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return WarningCategoryNames.PotentialGeneratorDefect;
        }

        return WarningCategoryNames.UnknownWarning;
    }

    private static string GetRecommendedActionForWarningCategory(string category)
    {
        if (string.Equals(category, WarningCategoryNames.OptionalFutureContent, StringComparison.OrdinalIgnoreCase))
        {
            return "Keep tolerated until the optional future content is intentionally added.";
        }

        if (string.Equals(category, WarningCategoryNames.ContentCompletenessGap, StringComparison.OrdinalIgnoreCase))
        {
            return "Decide whether the missing content is expected or should be added next.";
        }

        if (string.Equals(category, WarningCategoryNames.DataCanonicalization, StringComparison.OrdinalIgnoreCase))
        {
            return "Review data canonicalization rules and decide whether the warnings should be cleaned up in source data.";
        }

        if (string.Equals(category, WarningCategoryNames.GeneratorInfo, StringComparison.OrdinalIgnoreCase))
        {
            return "Keep as informational unless the generator behavior changes or a real defect appears.";
        }

        if (string.Equals(category, WarningCategoryNames.PotentialGeneratorDefect, StringComparison.OrdinalIgnoreCase))
        {
            return "Inspect the generator path for a real structural issue.";
        }

        return "Review manually if this warning starts to recur or changes shape.";
    }

    private static string GetRequestedScenario()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-tinyAdventureAuditScenario");
        if (index >= 0 && index + 1 < args.Length)
        {
            string scenario = args[index + 1].Trim();
            if (IsSupportedScenario(scenario))
            {
                return NormalizeScenario(scenario);
            }

            Debug.LogWarning($"[TinyAdventureBatchAudit] Unknown scenario '{scenario}', defaulting to generation.");
        }

        return ScenarioGeneration;
    }

    private static bool IsSupportedScenario(string scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario))
        {
            return false;
        }

        string normalized = NormalizeScenario(scenario);
        return normalized == ScenarioGeneration
            || normalized == ScenarioContentWarnings
            || normalized == ScenarioExplorationSmoke
            || normalized == ScenarioInteractionSmoke
            || normalized == ScenarioExplorationInteraction
            || normalized == ScenarioAll;
    }

    private static string NormalizeScenario(string scenario)
    {
        return scenario.Trim().ToLowerInvariant();
    }

    private static List<string> BuildRecommendedNextSteps(BatchAuditReport report)
    {
        var steps = new List<string>();

        if (report.KnownOptionalMissingContent.Count > 0)
        {
            steps.Add("Decide whether to add the optional loot/entity/monster JSON files or keep them tolerated as future content.");
        }

        if (report.WarningReasonCounts?.Count > 0)
        {
            steps.Add("Review the top warning reasons and decide which content gaps are expected versus worth filling next.");
        }

        if (report.Seeds.Any(seed => seed.ExplorationSmoke?.PartiallyBlocked == true))
        {
            steps.Add("Expose a tiny non-UI movement wrapper if exploration remains partially blocked in headless mode.");
        }

        if (report.Seeds.Any(seed => seed.InteractionSmoke?.PartiallyBlocked == true))
        {
            steps.Add("Expose a headless-safe action-discovery wrapper if interaction remains partially blocked by UI coupling.");
        }

        steps.Add("Run one manual Play Mode pass to confirm the gameplay UI still behaves with the current data set.");
        steps.Add("Add or flesh out optional loot, entity, and monster content if those systems become required.");
        return steps;
    }

    private static int GetRequestedRunCount(string scenario)
    {
        if (IsSmokeScenario(scenario))
        {
            return 5;
        }

        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-tinyAdventureAuditRunCount");
        if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int requested))
        {
            return Mathf.Max(10, requested);
        }

        return DefaultRunCount;
    }

    private static List<int> BuildPlannedSeeds(string scenario, int requestedRunCount)
    {
        int minimum = IsSmokeScenario(scenario) ? 5 : 10;
        return ExpandedSeedPool.Take(Mathf.Clamp(requestedRunCount, minimum, ExpandedSeedPool.Length)).ToList();
    }

    private static DeterminismReport RunDeterminismCheck(BatchAuditReport aggregateReport, IReadOnlyList<int> seeds)
    {
        if (seeds == null || seeds.Count == 0)
        {
            return new DeterminismReport
            {
                Skipped = true,
                Note = "Determinism check skipped because there were no base seeds."
            };
        }

        SuppressLogCapture = true;
        try
        {
            var firstPass = new Dictionary<int, SeedSnapshot>();
            foreach (int seed in seeds)
            {
                var run = RunSeedAudit(seed, aggregateReport, aggregateResults: false, captureLogs: false);
                firstPass[seed] = SeedSnapshot.From(run);
            }

            var secondPass = new Dictionary<int, SeedSnapshot>();
            foreach (int seed in seeds)
            {
                var run = RunSeedAudit(seed, aggregateReport, aggregateResults: false, captureLogs: false);
                secondPass[seed] = SeedSnapshot.From(run);
            }

            var mismatches = new List<string>();
            foreach (int seed in seeds)
            {
                if (!firstPass[seed].Equals(secondPass[seed]))
                {
                    mismatches.Add($"Seed {seed} produced a different stable snapshot between passes.");
                }
            }

            return new DeterminismReport
            {
                Skipped = false,
                Passed = mismatches.Count == 0,
                CheckedSeeds = seeds.ToList(),
                Mismatches = mismatches,
                Note = mismatches.Count == 0
                    ? "Stable snapshot metrics matched across repeated runs."
                    : "Stable snapshot metrics differed; investigate singleton reset or hidden state."
            };
        }
        catch (Exception ex)
        {
            return new DeterminismReport
            {
                Skipped = false,
                Passed = false,
                CheckedSeeds = seeds.ToList(),
                Mismatches = new List<string> { ex.Message },
                Note = "Determinism check could not complete safely.",
                HardException = ex.ToString()
            };
        }
        finally
        {
            SuppressLogCapture = false;
        }
    }

    private static void IncrementReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        if (!FailureReasonCounts.TryGetValue(reason, out int count))
        {
            FailureReasonCounts[reason] = 1;
        }
        else
        {
            FailureReasonCounts[reason] = count + 1;
        }
    }

    private static string GetReportPath()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-tinyAdventureAuditReport");
        if (index >= 0 && index + 1 < args.Length)
        {
            return args[index + 1];
        }

        return DefaultReportPath;
    }

    private static string TryGetLogPath()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-logFile");
        if (index >= 0 && index + 1 < args.Length)
        {
            return args[index + 1];
        }

        return string.Empty;
    }

    private static bool IsNoGraphicsLikelyActive()
    {
        return SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
    }

    private static IEnumerable<MonoBehaviour> FindSceneMonoBehaviours(UnityEngine.SceneManagement.Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<MonoBehaviour>()
            .Where(component => component != null &&
                                component.gameObject != null &&
                                component.gameObject.scene == scene);
    }

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (SuppressLogCapture)
        {
            return;
        }

        if (type == LogType.Warning)
        {
            CollectedWarnings.Add(condition);
        }
        else if (type == LogType.Error || type == LogType.Exception)
        {
            CollectedErrors.Add(condition);
        }
    }

    private static Component FindSceneComponent(Type componentType, UnityEngine.SceneManagement.Scene scene)
    {
        return Resources.FindObjectsOfTypeAll(componentType)
            .OfType<Component>()
            .FirstOrDefault(component => component != null && component.gameObject != null && component.gameObject.scene == scene);
    }

    private static T EnsureSceneComponent<T>(UnityEngine.SceneManagement.Scene scene, SeedAuditReport run) where T : Component
    {
        var component = FindSceneComponent(typeof(T), scene) as T;
        if (component != null)
        {
            SetSingletonInstance(component);
            return component;
        }

        var go = new GameObject(typeof(T).Name);
        var created = go.AddComponent<T>();
        if (created == null)
        {
            run.SceneWiringFailures.Add($"Could not create missing component {typeof(T).Name}.");
            return null;
        }

        SetSingletonInstance(created);
        return created;
    }

    private static T EnsureComponent<T>(UnityEngine.SceneManagement.Scene scene, SeedAuditReport run) where T : Component
    {
        return EnsureSceneComponent<T>(scene, run);
    }

    private static void TryInvokeMethod(object target, string methodName)
    {
        if (target == null)
        {
            return;
        }

        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            return;
        }

        method.Invoke(target, null);
    }

    private static void TryInvokeMethod(Component target, string methodName)
    {
        TryInvokeMethod((object)target, methodName);
    }

    private static object InvokeMethod(object target, string methodName, params object[] args)
    {
        if (target == null)
        {
            return null;
        }

        var methods = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name == methodName && method.GetParameters().Length == (args?.Length ?? 0))
            .ToList();

        if (methods.Count == 0)
        {
            return null;
        }

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            bool compatible = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                var arg = args[i];
                if (arg == null)
                {
                    if (parameters[i].ParameterType.IsValueType && Nullable.GetUnderlyingType(parameters[i].ParameterType) == null)
                    {
                        compatible = false;
                        break;
                    }
                    continue;
                }

                if (!parameters[i].ParameterType.IsAssignableFrom(arg.GetType()))
                {
                    compatible = false;
                    break;
                }
            }

            if (!compatible)
            {
                continue;
            }

            return method.Invoke(target, args);
        }

        return null;
    }

    private sealed class DataLoadSummary
    {
        public int Attempted;
        public int Succeeded;
        public int Failed;
        public List<string> Failures = new List<string>();
    }

    private sealed class MapRunResult
    {
        public bool MapGenerated;
        public int MapWidth;
        public int MapHeight;
        public int TotalCells;
        public int StartCellId;
        public string StartCellCoordinates;
        public string StartCellTerrain;
        public Dictionary<string, int> TerrainCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public int RegionCount;
        public int DungeonCount;
        public int CaveCount;
        public int CampCount;
        public int NestedAreaCount;
        public int AnimalAssignmentCount;
        public int ItemGenerationCount;
        public int NpcGenerationCount;
        public int PlayerCharacterCount;
    }

    [Serializable]
    private sealed class FeasibilityReport
    {
        public string CompileOnly;
        public string JsonDataLoading;
        public string SceneWiring;
        public string ProceduralGeneration;
        public string CharacterNpcAnimalItemGeneration;
        public string TurnSimulation;
        public string FullDeterministicSimulation;
    }

    [Serializable]
    private sealed class BatchAuditReport
    {
        public string ProjectName;
        public string UnityVersion;
        public string AuditTimestampUtc;
        public string SelectedScenario;
        public bool BatchMode;
        public bool NoGraphics;
        public bool UnityBatchMode;
        public string UnityGraphicsDevice;
        public string SceneLoaded;
        public string ReportPath;
        public string LogPath;
        public int ExitCode;
        public List<int> SeedsPlanned = new List<int>();
        public List<SeedAuditReport> Seeds = new List<SeedAuditReport>();
        public List<SeedAuditReport> SeedResults = new List<SeedAuditReport>();
        public int TotalRuns;
        public int AcceptedRuns;
        public int RejectedRuns;
        public int FailedRuns;
        public int HardExceptionCount;
        public int CompileErrorCount;
        public int DataLoadingFailureCount;
        public int SceneWiringFailureCount;
        public int GeneratedStateValidationFailureCount;
        public int ValidationWarningCount;
        public bool HadHardExceptions;
        public double AverageAttempts;
        public int WorstAttempts;
        public FeasibilityReport Feasibility;
        public ProjectInfoReport ProjectInfo;
        public AuditSettingsReport AuditSettings;
        public SummaryReport Summary;
        public OverallSummaryReport OverallSummary;
        public ScenarioResultsReport ScenarioResults;
        public DataLoadingReport DataLoading;
        public SceneWiringReport SceneWiring;
        public GenerationSummaryReport GenerationSummary;
        public DeterminismReport Determinism;
        public List<KnownOptionalContentRecord> KnownOptionalMissingContent = new List<KnownOptionalContentRecord>();
        public List<string> UnexpectedUnityErrors = new List<string>();
        public List<LogIssueSummary> UnityLogErrors = new List<LogIssueSummary>();
        public List<LogIssueSummary> UnityLogWarnings = new List<LogIssueSummary>();
        public List<LogIssueSummary> FailureReasons = new List<LogIssueSummary>();
        public List<LogIssueSummary> WarningReasons = new List<LogIssueSummary>();
        public Dictionary<string, int> WarningReasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public List<string> RecommendedNextSteps = new List<string>();
        public List<string> ValidationWarnings = new List<string>();
        public List<string> RuntimeErrors = new List<string>();
        public Dictionary<string, int> FailureReasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public List<string> MostCommonFailureReasons = new List<string>();
        public List<string> ManualFollowUpTestsRecommended = new List<string>();
    }

    [Serializable]
    private sealed class SeedAuditReport
    {
        public int Seed;
        public string ScenePath;
        public bool Success;
        public bool MapGenerated;
        public int DataLoadersAttempted;
        public int DataLoadersSucceeded;
        public int DataLoadersFailed;
        public int MapWidth;
        public int MapHeight;
        public int TotalCells;
        public int StartCellId;
        public string StartCellCoordinates;
        public string StartCellTerrain;
        public Dictionary<string, int> TerrainCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public int RegionCount;
        public int DungeonCount;
        public int CaveCount;
        public int CampCount;
        public int NestedAreaCount;
        public int AnimalAssignmentCount;
        public int ItemGenerationCount;
        public int NpcGenerationCount;
        public int PlayerCharacterCount;
        public bool NestedAreaSmokeTest;
        public bool ItemSmokeTest;
        public bool CharacterSmokeTest;
        public bool AnimalSmokeTest;
        public List<string> FailureReasons = new List<string>();
        public List<string> LoaderFailures = new List<string>();
        public List<string> SceneWiringFailures = new List<string>();
        public List<string> GeneratedStateFailures = new List<string>();
        public List<string> ValidationWarnings = new List<string>();
        public List<string> HardExceptions = new List<string>();
        public List<string> LoggedErrors = new List<string>();
        public List<string> KnownOptionalMissingContent = new List<string>();
        public bool BaselineSuccess;
        public int BaselineHardExceptionCount;
        public int BaselineLoggedErrorCount;
        public int BaselineGeneratedStateFailureCount;
        public int BaselineValidationWarningCount;
        public List<string> BaselineFailureReasons = new List<string>();
        public ExplorationSmokeSeedReport ExplorationSmoke;
        public InteractionSmokeSeedReport InteractionSmoke;
    }

    [Serializable]
    private sealed class ProjectInfoReport
    {
        public string ProjectName;
        public string UnityVersion;
        public string SceneLoaded;
    }

    [Serializable]
    private sealed class OverallSummaryReport
    {
        public int TotalRuns;
        public int PassedRuns;
        public int FailedRuns;
        public int HardExceptions;
        public int CompileErrors;
        public int DataLoadingFailures;
        public int SceneWiringFailures;
        public int GeneratedStateValidationFailures;
        public int ValidationWarnings;
        public int UnexpectedUnityErrors;
    }

    [Serializable]
    private sealed class ScenarioResultsReport
    {
        public string SelectedScenario;
        public ScenarioResultReport Generation = new ScenarioResultReport();
        public ScenarioResultReport ContentWarnings = new ScenarioResultReport();
        public ExplorationSmokeReport ExplorationSmoke = new ExplorationSmokeReport();
        public InteractionSmokeReport InteractionSmoke = new InteractionSmokeReport();
        public ExplorationInteractionReport ExplorationInteraction = new ExplorationInteractionReport();
    }

    [Serializable]
    private sealed class ScenarioResultReport
    {
        public string ScenarioName;
        public bool Ran;
        public bool Passed;
        public int RunCount;
        public int FailureCount;
        public int WarningCount;
        public int HardExceptionCount;
        public int UnexpectedErrorCount;
        public GenerationSummaryReport SummaryMetrics = new GenerationSummaryReport();
        public List<LogIssueSummary> TopFailureReasons = new List<LogIssueSummary>();
        public List<LogIssueSummary> TopWarningReasons = new List<LogIssueSummary>();
        public List<WarningCategoryRecord> WarningCategories = new List<WarningCategoryRecord>();
    }

    [Serializable]
    private class ScenarioSmokeReportBase
    {
        public string ScenarioName;
        public bool Ran;
        public bool Passed;
        public int SeedCount;
        public int PassedCount;
        public int FailedCount;
        public int PartiallyBlockedCount;
        public int HardExceptionCount;
        public int UnexpectedErrorCount;
        public int GeneratedStateValidationFailureCount;
        public int WarningCount;
        public List<string> BlockedReasons = new List<string>();
        public List<string> RecommendedNextSteps = new List<string>();
        public List<LogIssueSummary> TopFailureReasons = new List<LogIssueSummary>();
        public List<LogIssueSummary> TopWarningReasons = new List<LogIssueSummary>();
        public List<WarningCategoryRecord> WarningCategories = new List<WarningCategoryRecord>();
    }

    [Serializable]
    private sealed class ExplorationSmokeReport : ScenarioSmokeReportBase
    {
        public List<ExplorationSmokeSeedReport> SeedResults = new List<ExplorationSmokeSeedReport>();
        public int MovementAttemptedCount;
        public int MovementSuccessfulCount;
        public int MovementFailedCount;
    }

    [Serializable]
    private sealed class InteractionSmokeReport : ScenarioSmokeReportBase
    {
        public List<InteractionSmokeSeedReport> SeedResults = new List<InteractionSmokeSeedReport>();
        public int CandidateInteractablesFound;
        public int ActionProvidersInspected;
        public int ActionsDiscovered;
        public int NullActionCount;
        public int DuplicateActionCount;
        public int SafeActionsExecuted;
        public int UnsafeActionsSkipped;
        public int UiBoundActionsSkipped;
    }

    [Serializable]
    private sealed class ExplorationInteractionReport : ScenarioSmokeReportBase
    {
        public ExplorationSmokeReport Exploration = new ExplorationSmokeReport();
        public InteractionSmokeReport Interaction = new InteractionSmokeReport();
    }

    [Serializable]
    private sealed class ExplorationSmokeSeedReport
    {
        public int Seed;
        public bool Ran;
        public bool Passed;
        public bool Failed;
        public bool PartiallyBlocked;
        public bool PlayerResolved;
        public bool StartCellValid;
        public bool ExplorationModeEntered;
        public string StartCell;
        public string StartPosition;
        public string FinalPosition;
        public string CurrentAreaName;
        public int CurrentAreaId;
        public int AttemptedMovementCount;
        public int SuccessfulMovementCount;
        public int FailedMovementCount;
        public string BlockedReason;
        public int HardExceptionCount;
        public int UnexpectedErrorCount;
        public int GeneratedStateValidationFailureCount;
        public List<string> StateConsistencyWarnings = new List<string>();
        public List<string> HardExceptions = new List<string>();
        public List<string> UnexpectedUnityErrors = new List<string>();
        public List<string> SafeActionNames = new List<string>();
    }

    [Serializable]
    private sealed class InteractionSmokeSeedReport
    {
        public int Seed;
        public bool Ran;
        public bool Passed;
        public bool Failed;
        public bool PartiallyBlocked;
        public bool PlayerResolved;
        public string StartCell;
        public string CurrentCell;
        public int CandidateInteractablesFound;
        public int ActionProvidersInspected;
        public int ActionsDiscovered;
        public int NullActionCount;
        public int DuplicateActionCount;
        public int SafeActionsExecuted;
        public int UnsafeActionsSkipped;
        public int UiBoundActionsSkipped;
        public string BlockedReason;
        public int HardExceptionCount;
        public int UnexpectedErrorCount;
        public int GeneratedStateValidationFailureCount;
        public List<string> StateConsistencyWarnings = new List<string>();
        public List<string> HardExceptions = new List<string>();
        public List<string> UnexpectedUnityErrors = new List<string>();
        public List<string> SafeActionNames = new List<string>();
        public List<string> DiscoveredActionNames = new List<string>();
    }

    [Serializable]
    private sealed class AuditSettingsReport
    {
        public bool BatchMode;
        public bool NoGraphics;
        public int RequestedRunCount;
        public string SelectedScenario;
        public List<int> SeedsPlanned = new List<int>();
        public List<int> DeterminismSubset = new List<int>();
    }

    [Serializable]
    private sealed class SummaryReport
    {
        public int TotalRuns;
        public int PassedRuns;
        public int FailedRuns;
        public int HardExceptions;
        public int CompileErrors;
        public int DataLoadingFailures;
        public int SceneWiringFailures;
        public int GeneratedStateValidationFailures;
        public int ValidationWarnings;
    }

    [Serializable]
    private sealed class DataLoadingReport
    {
        public int Attempted;
        public int Succeeded;
        public int Failed;
    }

    [Serializable]
    private sealed class SceneWiringReport
    {
        public int FailureCount;
        public List<string> Failures = new List<string>();
    }

    [Serializable]
    private sealed class GenerationSummaryReport
    {
        public double AverageMapWidth;
        public double AverageMapHeight;
        public double AverageCellCount;
        public double AverageCaves;
        public double AverageDungeons;
        public double AverageCamps;
        public double AverageNPCs;
        public double AverageAnimals;
        public double AverageItems;
    }

    [Serializable]
    private sealed class KnownOptionalContentRecord
    {
        public string Message;
        public int Count;
        public List<int> AffectedSeeds = new List<int>();
        public bool CausedAuditFailure;
        public string Note;
    }

    [Serializable]
    private sealed class LogIssueSummary
    {
        public string Message;
        public int Count;
        public bool CausedAuditFailure;
    }

    [Serializable]
    private sealed class KnownOptionalContentSummary
    {
        public string Message;
        public int Count;
        public List<int> AffectedSeeds = new List<int>();
        public bool CausedAuditFailure;
        public string Note;

        public KnownOptionalContentRecord ToRecord()
        {
            return new KnownOptionalContentRecord
            {
                Message = Message,
                Count = Count,
                AffectedSeeds = AffectedSeeds.ToList(),
                CausedAuditFailure = CausedAuditFailure,
                Note = Note
            };
        }
    }

    private static class WarningCategoryNames
    {
        public const string OptionalFutureContent = "OptionalFutureContent";
        public const string ContentCompletenessGap = "ContentCompletenessGap";
        public const string DataCanonicalization = "DataCanonicalization";
        public const string GeneratorInfo = "GeneratorInfo";
        public const string PotentialGeneratorDefect = "PotentialGeneratorDefect";
        public const string UnknownWarning = "UnknownWarning";
    }

    [Serializable]
    private sealed class WarningCategoryRecord
    {
        public string Category;
        public List<string> DistinctMessages = new List<string>();
        public int TotalCount;
        public List<int> AffectedSeeds = new List<int>();
        public bool CausesAuditFailure;
        public string RecommendedAction;
    }

    private sealed class WarningCategorySummary
    {
        public string Category;
        public List<string> DistinctMessages = new List<string>();
        public int TotalCount;
        public List<int> AffectedSeeds = new List<int>();
        public bool CausesAuditFailure;
        public string RecommendedAction;

        public WarningCategoryRecord ToRecord()
        {
            return new WarningCategoryRecord
            {
                Category = Category,
                DistinctMessages = DistinctMessages.Distinct().OrderBy(x => x).ToList(),
                TotalCount = TotalCount,
                AffectedSeeds = AffectedSeeds.Distinct().OrderBy(x => x).ToList(),
                CausesAuditFailure = CausesAuditFailure,
                RecommendedAction = RecommendedAction
            };
        }
    }

    [Serializable]
    private sealed class DeterminismReport
    {
        public bool Skipped;
        public bool Passed;
        public string Note;
        public string HardException;
        public List<int> CheckedSeeds = new List<int>();
        public List<string> Mismatches = new List<string>();
    }

    [Serializable]
    private sealed class SeedSnapshot
    {
        public int MapWidth;
        public int MapHeight;
        public string StartCellCoordinates;
        public Dictionary<string, int> TerrainCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public int CaveCount;
        public int DungeonCount;
        public int CampCount;
        public int NpcGenerationCount;
        public int AnimalAssignmentCount;
        public int ItemGenerationCount;
        public int PlayerCharacterCount;

        public static SeedSnapshot From(SeedAuditReport run)
        {
            return new SeedSnapshot
            {
                MapWidth = run.MapWidth,
                MapHeight = run.MapHeight,
                StartCellCoordinates = run.StartCellCoordinates,
                TerrainCounts = run.TerrainCounts != null
                    ? new Dictionary<string, int>(run.TerrainCounts, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                CaveCount = run.CaveCount,
                DungeonCount = run.DungeonCount,
                CampCount = run.CampCount,
                NpcGenerationCount = run.NpcGenerationCount,
                AnimalAssignmentCount = run.AnimalAssignmentCount,
                ItemGenerationCount = run.ItemGenerationCount,
                PlayerCharacterCount = run.PlayerCharacterCount
            };
        }

        public override bool Equals(object obj)
        {
            var other = obj as SeedSnapshot;
            if (other == null)
            {
                return false;
            }

            return MapWidth == other.MapWidth
                && MapHeight == other.MapHeight
                && string.Equals(StartCellCoordinates, other.StartCellCoordinates, StringComparison.Ordinal)
                && CaveCount == other.CaveCount
                && DungeonCount == other.DungeonCount
                && CampCount == other.CampCount
                && NpcGenerationCount == other.NpcGenerationCount
                && AnimalAssignmentCount == other.AnimalAssignmentCount
                && ItemGenerationCount == other.ItemGenerationCount
                && PlayerCharacterCount == other.PlayerCharacterCount
                && TerrainCounts.Count == other.TerrainCounts.Count
                && !TerrainCounts.Except(other.TerrainCounts).Any();
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + MapWidth.GetHashCode();
                hash = hash * 23 + MapHeight.GetHashCode();
                hash = hash * 23 + (StartCellCoordinates != null ? StartCellCoordinates.GetHashCode() : 0);
                hash = hash * 23 + CaveCount.GetHashCode();
                hash = hash * 23 + DungeonCount.GetHashCode();
                hash = hash * 23 + CampCount.GetHashCode();
                hash = hash * 23 + NpcGenerationCount.GetHashCode();
                hash = hash * 23 + AnimalAssignmentCount.GetHashCode();
                hash = hash * 23 + ItemGenerationCount.GetHashCode();
                hash = hash * 23 + PlayerCharacterCount.GetHashCode();
                return hash;
            }
        }
    }
}
