// CODEXLOG004_RELATIONSHIPS: temporary sparse relationship and hostility diagnostics helper.
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RelationshipDiagnosticsLogger
{
    public const string DiagnosticId = "CODEXLOG004_RELATIONSHIPS";
    private static readonly object FileLock = new object();
    private static bool loggingFailed;

    // CODEXLOG004_RELATIONSHIPS: temporary relationship diagnostic method.
    public static void LogEvent(string category, string eventName, string details = null)
    {
        if (loggingFailed) return;

        try
        {
            var sb = new StringBuilder();
            AppendLine(sb, $"{category} {DateTime.Now:yyyy-MM-dd HH:mm:ss} | frame={Time.frameCount}");
            AppendLine(sb, $"Event: {eventName}");
            AppendLine(sb, $"Scene: {GetSceneName()}");

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
        }
        catch (Exception ex)
        {
            loggingFailed = true;
            Debug.LogWarning($"{DiagnosticId} [WARNING] Failed to write relationship diagnostics. Gameplay will continue. {ex.Message}");
        }
    }

    // CODEXLOG004_RELATIONSHIPS: temporary relationship diagnostic method.
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

    // CODEXLOG004_RELATIONSHIPS: temporary relationship diagnostic method.
    private static void AppendLine(StringBuilder sb, string text)
    {
        sb.AppendLine($"{DiagnosticId} {text}");
    }

    // CODEXLOG004_RELATIONSHIPS: temporary relationship diagnostic method.
    private static string GetSceneName()
    {
        var scene = SceneManager.GetActiveScene();
        return scene.IsValid() ? scene.name : "UNKNOWN";
    }
}
