// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostics helper.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TurnDiagnosticsLogger
{
    public const string DiagnosticId = "CODEXLOG001_TURNLIFECYCLE";

    private static readonly object FileLock = new object();
    private static bool initialized;
    private static string sessionLogPath;
    private static string latestLogPath;
    private static bool loggingFailed;

    public static string SessionLogPath
    {
        get
        {
            EnsureInitialized();
            return sessionLogPath;
        }
    }

    public static bool MaintainsLatestCopy => true;

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    public static void LogEvent(string category, string eventName, string details = null, Character character = null)
    {
        WriteBlock(category, eventName, details, character, false);
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    public static void LogWarning(string eventName, string details = null, Character character = null)
    {
        WriteBlock("[WARNING]", eventName, details, character, true);
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    public static void LogTurnSummary(string eventName, string details = null)
    {
        WriteBlock("[TURN SUMMARY]", eventName, details, null, false);
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    public static void LogRegistration(string source, Character character, bool isPlayer)
    {
        string details = $"Source: {source}\nIsPlayer: {isPlayer}";
        WriteBlock("[REGISTRATION]", "Character registration observed", details, character, false);
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    public static void LogDeregistration(string source, Character character)
    {
        string details = $"Source: {source}";
        WriteBlock("[DEREGISTRATION]", "Character deregistration observed", details, character, false);
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    public static void LogInvariantCheck(string eventName, string details = null)
    {
        WriteBlock("[INVARIANT CHECK]", eventName, details, null, false);
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    public static void LogShutdown(string reason)
    {
        WriteBlock("[SHUTDOWN]", reason, null, null, false);
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    private static void EnsureInitialized()
    {
        if (initialized) return;

        initialized = true;

        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                projectRoot = Application.persistentDataPath;
            }

            string logDirectory = Path.Combine(projectRoot, "DiagnosticLogs");
            Directory.CreateDirectory(logDirectory);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            sessionLogPath = Path.Combine(logDirectory, $"TinyAdventure_TurnDiagnostics_{stamp}.txt");
            latestLogPath = Path.Combine(logDirectory, "TinyAdventure_TurnDiagnostics_Latest.txt");

            if (File.Exists(latestLogPath))
            {
                File.Delete(latestLogPath);
            }

            WriteRaw(BuildHeader());
            Debug.Log($"{DiagnosticId} [BOOT] Turn diagnostics log path: {sessionLogPath}");
        }
        catch (Exception ex)
        {
            loggingFailed = true;
            Debug.LogWarning($"{DiagnosticId} [WARNING] Failed to initialize turn diagnostics logging. Gameplay will continue. {ex.Message}");
        }
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    private static string BuildHeader()
    {
        var scene = SceneManager.GetActiveScene();
        var orchestrator = TurnOrchestrator.Instance;

        var sb = new StringBuilder();
        sb.AppendLine($"{DiagnosticId} [BOOT] {DateTime.Now:yyyy-MM-dd HH:mm:ss} | frame={Time.frameCount}");
        sb.AppendLine($"DiagnosticId: {DiagnosticId}");
        sb.AppendLine($"DateTime: {DateTime.Now:O}");
        sb.AppendLine($"Scene: {(scene.IsValid() ? scene.name : "UNKNOWN")}");
        sb.AppendLine($"Runtime: {(Application.isEditor ? "Editor" : "Build")}");
        sb.AppendLine($"LogPath: {sessionLogPath}");
        sb.AppendLine($"LatestPath: {latestLogPath}");
        sb.AppendLine($"TurnOrchestrator.Exists: {orchestrator != null}");
        sb.AppendLine($"TurnOrchestrator.combatManager.Assigned: {orchestrator?.DiagnosticCombatManagerAssigned.ToString() ?? "UNKNOWN"}");
        sb.AppendLine($"TurnOrchestrator.explorationTurnManager.Assigned: {orchestrator?.DiagnosticExplorationTurnManagerAssigned.ToString() ?? "UNKNOWN"}");
        sb.AppendLine();
        return sb.ToString();
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    private static void WriteBlock(string category, string eventName, string details, Character character, bool forceWarning)
    {
        EnsureInitialized();
        if (loggingFailed) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{DiagnosticId} {category} {DateTime.Now:yyyy-MM-dd HH:mm:ss} | frame={Time.frameCount}");
            sb.AppendLine($"Event: {eventName}");
            AppendSceneAndContext(sb);

            if (character != null)
            {
                AppendCharacter(sb, "Entity", character);
            }

            if (!string.IsNullOrEmpty(details))
            {
                sb.AppendLine("Details:");
                sb.AppendLine(details);
            }

            List<string> warnings = CollectWarnings();
            foreach (string warning in warnings)
            {
                sb.AppendLine($"WARNING: {warning}");
            }

            sb.AppendLine();
            WriteRaw(sb.ToString());

            if (forceWarning)
            {
                Debug.LogWarning($"{DiagnosticId} {category} {eventName}");
            }
        }
        catch (Exception ex)
        {
            loggingFailed = true;
            Debug.LogWarning($"{DiagnosticId} [WARNING] Failed to write turn diagnostics log. Gameplay will continue. {ex.Message}");
        }
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    private static void WriteRaw(string text)
    {
        lock (FileLock)
        {
            File.AppendAllText(sessionLogPath, text);
            File.AppendAllText(latestLogPath, text);
        }
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    private static void AppendSceneAndContext(StringBuilder sb)
    {
        var scene = SceneManager.GetActiveScene();
        var orchestrator = TurnOrchestrator.Instance;
        var playerStats = PlayerStats.Instance;
        var playerCharacter = playerStats != null ? playerStats.CurrentPlayerCharacter : null;

        sb.AppendLine($"Scene: {(scene.IsValid() ? scene.name : "UNKNOWN")}");
        sb.AppendLine($"Context: {(orchestrator != null ? orchestrator.CurrentContext.ToString() : "NO_TURN_ORCHESTRATOR")}");
        sb.AppendLine($"CurrentNestedArea: {FormatArea(playerStats?.CurrentNestedArea)}");
        sb.AppendLine($"PlayerStats.CurrentNestedArea: {FormatArea(playerStats?.CurrentNestedArea)}");
        sb.AppendLine($"PlayerCharacter.CurrentNestedArea: {FormatArea(playerCharacter?.CurrentNestedArea)}");
        sb.AppendLine($"PlayerStats.IsInNestedArea: {playerStats?.IsInNestedArea.ToString() ?? "UNKNOWN"}");
        sb.AppendLine($"PlayerStats.IsInMainMap: {playerStats?.IsInMainMap.ToString() ?? "UNKNOWN"}");
        sb.AppendLine($"PlayerStats.RegisteredInTurnManager: {playerStats?.RegisteredInTurnManager.ToString() ?? "UNKNOWN"}");
        sb.AppendLine($"PlayerStats.InCombat: {playerStats?.InCombat.ToString() ?? "UNKNOWN"}");
        sb.AppendLine($"allCharacters.Count: {orchestrator?.DiagnosticAllCharactersCount.ToString() ?? "UNKNOWN"}");
        sb.AppendLine($"Exploration.Count: {orchestrator?.DiagnosticExplorationRegisteredCount.ToString() ?? "UNKNOWN"}");
        sb.AppendLine($"Combat.Count: {orchestrator?.DiagnosticCombatRegisteredCount.ToString() ?? "UNKNOWN"}");
        sb.AppendLine($"combatManager.Assigned: {orchestrator?.DiagnosticCombatManagerAssigned.ToString() ?? "UNKNOWN"}");
        sb.AppendLine($"explorationTurnManager.Assigned: {orchestrator?.DiagnosticExplorationTurnManagerAssigned.ToString() ?? "UNKNOWN"}");
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    private static void AppendCharacter(StringBuilder sb, string label, Character character)
    {
        sb.AppendLine($"{label}.Name: {character.Name}");
        sb.AppendLine($"{label}.ID: {character.IInteractableID}");
        sb.AppendLine($"{label}.Type: {character.GetType().Name}");
        sb.AppendLine($"{label}.IsActive: {character.IsActive}");
        sb.AppendLine($"{label}.IsAlive: {character.IsAlive}");
        sb.AppendLine($"{label}.IsHostile: {character.IsHostile}");
        sb.AppendLine($"{label}.IsInNestedArea: {character.IsInNestedArea}");
        sb.AppendLine($"{label}.CurrentNestedArea: {FormatArea(character.CurrentNestedArea)}");
        sb.AppendLine($"{label}.NestedMapPosition: {character.NestedMapPosition}");
        sb.AppendLine($"{label}.InTurn: {character.InTurn}");
        sb.AppendLine($"{label}.InCombat: {character.InCombat}");
        sb.AppendLine($"{label}.Stamina: {FixedPointResourceMath.Format(character.CurrentStamina)}/{FixedPointResourceMath.Format(character.MaxStamina)}");
        sb.AppendLine($"{label}.CombatExertion: {FixedPointResourceMath.Format(character.CurrentCombatExertion)}/{FixedPointResourceMath.Format(character.MaxCombatExertion)}");
        sb.AppendLine($"{label}.ConsumptionCapacity: {character.CurrentConsumptionCapacity}/{character.MaxConsumptionCapacity}");
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    private static List<string> CollectWarnings()
    {
        var warnings = new List<string>();
        var orchestrator = TurnOrchestrator.Instance;
        var playerStats = PlayerStats.Instance;
        var playerCharacter = playerStats != null ? playerStats.CurrentPlayerCharacter : null;

        if (orchestrator == null)
        {
            warnings.Add("TurnOrchestrator.Instance is null.");
            return warnings;
        }

        if (!orchestrator.DiagnosticCombatManagerAssigned)
        {
            warnings.Add("combatManager is null.");
        }

        if (!orchestrator.DiagnosticExplorationTurnManagerAssigned)
        {
            warnings.Add("explorationTurnManager is null.");
        }

        if (playerStats != null && playerStats.IsInNestedArea && orchestrator.CurrentContext == TurnContext.MainMap)
        {
            warnings.Add("Player is in a nested area but TurnOrchestrator.CurrentContext is MainMap.");
        }

        if (orchestrator.CurrentContext == TurnContext.Exploration && orchestrator.DiagnosticExplorationRegisteredCount == 0)
        {
            warnings.Add("Context is Exploration but ExplorationTurnManager has zero participants.");
        }

        if (orchestrator.CurrentContext == TurnContext.Combat && orchestrator.DiagnosticCombatRegisteredCount == 0)
        {
            warnings.Add("Context is Combat but CombatTurnManager has zero participants.");
        }

        if (playerStats != null && playerCharacter != null && playerStats.CurrentNestedArea != playerCharacter.CurrentNestedArea)
        {
            warnings.Add($"PlayerStats.CurrentNestedArea disagrees with player character CurrentNestedArea. PlayerStats={FormatArea(playerStats.CurrentNestedArea)}, PlayerCharacter={FormatArea(playerCharacter.CurrentNestedArea)}.");
        }

        if (playerStats != null && playerCharacter != null && playerStats.RegisteredInTurnManager && !orchestrator.DiagnosticIsCharacterRegisteredInActiveManager(playerCharacter))
        {
            warnings.Add("PlayerStats.RegisteredInTurnManager is true but the player character is not registered in the active turn manager.");
        }

        if (orchestrator.CurrentContext != TurnContext.Combat && orchestrator.DiagnosticCombatRegisteredCount > 0)
        {
            warnings.Add("CombatTurnManager contains participants while TurnOrchestrator context is not Combat.");
        }

        if (orchestrator.CurrentContext == TurnContext.Combat && orchestrator.DiagnosticExplorationRegisteredCount > 0)
        {
            warnings.Add("ExplorationTurnManager contains participants while TurnOrchestrator context is Combat.");
        }

        AddEntityWarnings(warnings, "allCharacters", orchestrator.DiagnosticGetAllCharactersSnapshot(), playerStats?.CurrentNestedArea, orchestrator.CurrentContext, true);
        AddEntityWarnings(warnings, "ExplorationTurnManager", orchestrator.DiagnosticGetExplorationCharactersSnapshot(), playerStats?.CurrentNestedArea, orchestrator.CurrentContext, false);
        AddEntityWarnings(warnings, "CombatTurnManager", orchestrator.DiagnosticGetCombatCharactersSnapshot(), playerStats?.CurrentNestedArea, orchestrator.CurrentContext, false);

        return warnings;
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    private static void AddEntityWarnings(List<string> warnings, string source, List<Character> characters, INestedArea activeArea, TurnContext context, bool globalList)
    {
        if (characters == null || characters.Count == 0) return;

        var duplicateIds = characters
            .Where(c => c != null)
            .GroupBy(c => c.IInteractableID)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (int id in duplicateIds)
        {
            warnings.Add($"{source} contains duplicate entity ID {id}.");
        }

        foreach (var character in characters)
        {
            if (character == null)
            {
                warnings.Add($"{source} contains a null character.");
                continue;
            }

            string prefix = $"{source} contains {character.GetType().Name} '{character.Name}' [{character.IInteractableID}]";

            if (!character.IsAlive)
            {
                warnings.Add($"{prefix} but IsAlive is false.");
            }

            if (!character.IsActive)
            {
                warnings.Add($"{prefix} but IsActive is false.");
            }

            if ((context == TurnContext.Exploration || context == TurnContext.Combat) && character.CurrentNestedArea == null)
            {
                warnings.Add($"{prefix} but CurrentNestedArea is null during nested-area context.");
            }

            if ((context == TurnContext.Exploration || context == TurnContext.Combat) && activeArea != null && character.CurrentNestedArea != null && character.CurrentNestedArea != activeArea)
            {
                warnings.Add($"{prefix} but belongs to a different nested area. Active={FormatArea(activeArea)}, Entity={FormatArea(character.CurrentNestedArea)}.");
            }

            if (globalList && (!character.IsActive || !character.IsAlive || ((context == TurnContext.Exploration || context == TurnContext.Combat) && activeArea != null && character.CurrentNestedArea != activeArea)))
            {
                warnings.Add($"TurnOrchestrator.allCharacters may contain stale entity '{character.Name}' [{character.IInteractableID}].");
            }
        }
    }

    // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic method.
    private static string FormatArea(INestedArea area)
    {
        if (area == null) return "NULL";
        return $"{area.Name} (ID={area.NestedAreaID}, Level={area.NestedAreaLevel})";
    }
}
