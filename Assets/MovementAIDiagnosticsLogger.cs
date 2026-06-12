// CODEXLOG002_MOVEMENT_AI: temporary movement and AI diagnostics helper.
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MovementAIDiagnosticsLogger
{
    public const string DiagnosticId = "CODEXLOG002_MOVEMENT_AI";
    private static readonly object FileLock = new object();
    private static bool loggingFailed;

    // CODEXLOG002_MOVEMENT_AI: temporary movement and AI diagnostic method.
    public static void LogEvent(string category, string eventName, string details = null, Character character = null)
    {
        WriteBlock(category, eventName, details, character, false);
    }

    // CODEXLOG002_MOVEMENT_AI: temporary movement and AI diagnostic method.
    public static void LogWarning(string eventName, string details = null, Character character = null)
    {
        WriteBlock("[WARNING]", eventName, details, character, true);
    }

    // CODEXLOG002_MOVEMENT_AI: temporary movement and AI diagnostic method.
    private static void WriteBlock(string category, string eventName, string details, Character character, bool forceWarning)
    {
        if (loggingFailed) return;

        try
        {
            var sb = new StringBuilder();
            AppendLine(sb, $"{category} {DateTime.Now:yyyy-MM-dd HH:mm:ss} | frame={Time.frameCount}");
            AppendLine(sb, $"Event: {eventName}");
            AppendLine(sb, $"Scene: {GetSceneName()}");

            if (character != null)
            {
                AppendLine(sb, $"Entity.Name: {character.Name}");
                AppendLine(sb, $"Entity.ID: {character.IInteractableID}");
                AppendLine(sb, $"Entity.Type: {character.GetType().Name}");
                AppendLine(sb, $"Entity.IsActive: {character.IsActive}");
                AppendLine(sb, $"Entity.IsAlive: {character.IsAlive}");
                AppendLine(sb, $"Entity.IsHostile: {character.IsHostile}");
                AppendLine(sb, $"Entity.InCombat: {character.InCombat}");
                AppendLine(sb, $"Entity.CurrentNestedArea: {FormatArea(character.CurrentNestedArea)}");
                AppendLine(sb, $"Entity.NestedMapPosition: {character.NestedMapPosition}");
                AppendLine(sb, $"Entity.ActionPoints: {character.ActionPoints}");
                AppendLine(sb, $"Entity.MovePoints: {character.MovePoints}");
                AppendLine(sb, $"Entity.StateMachine: {FormatStateMachine(character)}");
                AppendLine(sb, $"Entity.CurrentState: {FormatCurrentState(character)}");
            }

            if (!string.IsNullOrEmpty(details))
            {
                AppendLine(sb, "Details:");
                foreach (string line in details.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                {
                    AppendLine(sb, line);
                }
            }

            AppendLine(sb, string.Empty);
            WriteRaw(sb.ToString());

            if (forceWarning)
            {
                Debug.LogWarning($"{DiagnosticId} {category} {eventName}");
            }
        }
        catch (Exception ex)
        {
            loggingFailed = true;
            Debug.LogWarning($"{DiagnosticId} [WARNING] Failed to write movement/AI diagnostics. Gameplay will continue. {ex.Message}");
        }
    }

    // CODEXLOG002_MOVEMENT_AI: temporary movement and AI diagnostic method.
    public static void WriteRawBlock(string text)
    {
        if (loggingFailed) return;

        try
        {
            WriteRaw(text);
        }
        catch (Exception ex)
        {
            loggingFailed = true;
            Debug.LogWarning($"{DiagnosticId} [WARNING] Failed to write raw movement/AI diagnostics. Gameplay will continue. {ex.Message}");
        }
    }

    // CODEXLOG002_MOVEMENT_AI: temporary movement and AI diagnostic method.
    private static void WriteRaw(string text)
    {
        string sessionPath = TurnDiagnosticsLogger.SessionLogPath;
        string directory = Path.GetDirectoryName(sessionPath);
        string latestPath = Path.Combine(directory, "TinyAdventure_TurnDiagnostics_Latest.txt");

        lock (FileLock)
        {
            File.AppendAllText(sessionPath, text);
            File.AppendAllText(latestPath, text);
        }
    }

    // CODEXLOG002_MOVEMENT_AI: temporary movement and AI diagnostic method.
    public static void AppendLine(StringBuilder sb, string text)
    {
        sb.AppendLine($"{DiagnosticId} {text}");
    }

    // CODEXLOG002_MOVEMENT_AI: temporary movement and AI diagnostic method.
    public static string FormatArea(INestedArea area)
    {
        if (area == null) return "NULL";
        return $"{area.Name} (ID={area.NestedAreaID}, Level={area.NestedAreaLevel})";
    }

    // CODEXLOG002_MOVEMENT_AI: temporary movement and AI diagnostic method.
    public static string FormatStateMachine(Character character)
    {
        return character?.stateMachine != null ? character.stateMachine.GetType().Name : "NULL";
    }

    // CODEXLOG002_MOVEMENT_AI: temporary movement and AI diagnostic method.
    public static string FormatCurrentState(Character character)
    {
        return character?.stateMachine?.CurrentState != null ? character.stateMachine.CurrentState.GetType().Name : "NULL";
    }

    // CODEXLOG002_MOVEMENT_AI: temporary movement and AI diagnostic method.
    private static string GetSceneName()
    {
        var scene = SceneManager.GetActiveScene();
        return scene.IsValid() ? scene.name : "UNKNOWN";
    }
}
