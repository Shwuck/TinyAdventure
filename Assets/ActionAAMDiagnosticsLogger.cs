// CODEXLOG003_ACTIONS_AAM: temporary Adaptive Action Menu and action diagnostics helper.
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ActionAAMDiagnosticsLogger
{
    public const string DiagnosticId = "CODEXLOG003_ACTIONS_AAM";
    private static readonly object FileLock = new object();
    private static bool loggingFailed;

    // CODEXLOG003_ACTIONS_AAM: temporary AAM/action diagnostic method.
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
            Debug.LogWarning($"{DiagnosticId} [WARNING] Failed to write AAM/action diagnostics. Gameplay will continue. {ex.Message}");
        }
    }

    // CODEXLOG003_ACTIONS_AAM: temporary AAM/action diagnostic method.
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

    // CODEXLOG003_ACTIONS_AAM: temporary AAM/action diagnostic method.
    private static void AppendLine(StringBuilder sb, string text)
    {
        sb.AppendLine($"{DiagnosticId} {text}");
    }

    // CODEXLOG003_ACTIONS_AAM: temporary AAM/action diagnostic method.
    private static string GetSceneName()
    {
        var scene = SceneManager.GetActiveScene();
        return scene.IsValid() ? scene.name : "UNKNOWN";
    }
}
