// CODEXLOG005_COMBAT_ACTION_RESOLUTION: combat action resolution diagnostics helper.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CombatActionResolutionDiagnosticsLogger
{
    public const string DiagnosticId = "CODEXLOG005_COMBAT_ACTION_RESOLUTION";

    private static readonly object FileLock = new object();
    private static bool initialized;
    private static bool loggingFailed;
    private static string sessionLogPath;
    private static string latestLogPath;
    private static string sessionStamp;

    public static string SessionLogPath
    {
        get
        {
            EnsureInitialized();
            return sessionLogPath;
        }
    }

    public static string LatestLogPath
    {
        get
        {
            EnsureInitialized();
            return latestLogPath;
        }
    }

    public static void LogEvent(string category, string eventName, string details = null, Character attacker = null, Character defender = null)
    {
        WriteBlock(category, eventName, details, attacker, defender, false);
    }

    public static void LogWarning(string eventName, string details = null, Character attacker = null, Character defender = null)
    {
        WriteBlock("[WARNING]", eventName, details, attacker, defender, true);
    }

    public static string FormatArea(INestedArea area)
    {
        if (area == null) return "NULL";
        return $"{area.Name} (ID={area.NestedAreaID}, Level={area.NestedAreaLevel})";
    }

    public static string FormatDamageDictionary(IDictionary<DamageType, int> damageByType)
    {
        if (damageByType == null || damageByType.Count == 0)
        {
            return "{}";
        }

        return "{" + string.Join(", ", damageByType.Select(kv => $"{kv.Key}:{kv.Value}")) + "}";
    }

    public static string FormatItemSummary(Item item)
    {
        if (item == null) return "NULL";

        string slotSummary = item.EquipmentSlots != null && item.EquipmentSlots.Count > 0
            ? string.Join("/", item.EquipmentSlots)
            : "NoSlots";
        string resistanceSummary = item.Resistances != null && item.Resistances.Count > 0
            ? string.Join(", ", item.Resistances.Select(kv => $"{kv.Key}:{kv.Value}"))
            : "None";

        return $"{item.ItemInGameName} [Type={item.WeaponType}, DamageType={item.DamageType}, Damage={item.DamageOutput}, Armour={item.ArmourValue}, Slots={slotSummary}, Resistances={resistanceSummary}]";
    }

    public static string FormatEquipmentSummary(Character character)
    {
        if (character?.EquippedItems == null || character.EquippedItems.Count == 0)
        {
            return "NONE";
        }

        return string.Join("; ",
            character.EquippedItems
                .Where(kv => kv.Value != null)
                .OrderBy(kv => kv.Key.ToString())
                .Select(kv => $"{kv.Key}={FormatItemSummary(kv.Value)}"));
    }

    public static string FormatResistanceSources(Character character, DamageType damageType)
    {
        if (character == null)
        {
            return "Character=NULL";
        }

        var parts = new List<string>();

        float baseResistance = 0f;
        if (character.Resistances != null && character.Resistances.TryGetValue(damageType, out float storedBaseResistance))
        {
            baseResistance = storedBaseResistance;
        }
        parts.Add($"Base={baseResistance}");

        if (character.EquippedItems != null)
        {
            foreach (var kv in character.EquippedItems.OrderBy(kv => kv.Key.ToString()))
            {
                Item item = kv.Value;
                if (item?.Resistances != null && item.Resistances.TryGetValue(damageType.ToString(), out float itemResistance))
                {
                    parts.Add($"{kv.Key}:{item.ItemInGameName}={itemResistance}");
                }
            }
        }

        if (character.AffectedBy != null)
        {
            foreach (var effect in character.AffectedBy.Where(effect => effect.AffectedResistance == damageType))
            {
                parts.Add($"Effect:{effect.Name}={effect.EffectAmount}");
            }
        }

        return string.Join(" | ", parts);
    }

    public static string InferActionName(Character attacker, DamageType requestedDamageType)
    {
        if (attacker == null)
        {
            return "UnknownAttack";
        }

        Item mainHand = attacker.GetMainHandItem();
        if (mainHand == null)
        {
            if (attacker is Monster || attacker is Animal)
            {
                return "NaturalAttack";
            }

            return requestedDamageType == DamageType.Bludgeoning ? "Punch" : $"Unarmed{requestedDamageType}";
        }

        return requestedDamageType switch
        {
            DamageType.Slashing => "Slash",
            DamageType.Piercing => "Stab",
            DamageType.Bludgeoning => "Bash",
            DamageType.Rending => "Rend",
            DamageType.Magic => "Magic",
            _ => $"{mainHand.WeaponType}Attack"
        };
    }

    private static void EnsureInitialized()
    {
        if (initialized) return;

        initialized = true;

        try
        {
            string sharedSessionPath = TurnDiagnosticsLogger.SessionLogPath;
            string directory = Path.GetDirectoryName(sharedSessionPath);
            sessionStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            sessionLogPath = Path.Combine(directory, $"TinyAdventure_CombatActionResolution_{sessionStamp}.txt");
            latestLogPath = Path.Combine(directory, "TinyAdventure_CombatActionResolution_Latest.txt");

            if (File.Exists(latestLogPath))
            {
                File.Delete(latestLogPath);
            }

            WriteCombatOnly(BuildHeader());
            Debug.Log($"{DiagnosticId} [BOOT] Combat diagnostics log path: {sessionLogPath}");
        }
        catch (Exception ex)
        {
            loggingFailed = true;
            Debug.LogWarning($"{DiagnosticId} [WARNING] Failed to initialize combat diagnostics logging. Gameplay will continue. {ex.Message}");
        }
    }

    private static string BuildHeader()
    {
        var scene = SceneManager.GetActiveScene();
        var playerStats = PlayerStats.Instance;
        var playerCharacter = playerStats != null ? playerStats.CurrentPlayerCharacter : null;
        var orchestrator = TurnOrchestrator.Instance;
        var gameManager = GameManager.Instance;

        var sb = new StringBuilder();
        AppendLine(sb, $"[BOOT] {DateTime.Now:yyyy-MM-dd HH:mm:ss} | frame={Time.frameCount}");
        AppendLine(sb, $"DiagnosticId: {DiagnosticId}");
        AppendLine(sb, "LogType: Combat-only filtered extract");
        AppendLine(sb, "Note: This file contains combat action resolution diagnostics only.");
        AppendLine(sb, $"DateTime: {DateTime.Now:O}");
        AppendLine(sb, $"SessionStamp: {sessionStamp}");
        AppendLine(sb, $"Scene: {(scene.IsValid() ? scene.name : "UNKNOWN")}");
        AppendLine(sb, $"Runtime: {(Application.isEditor ? "Editor" : "Build")}");
        AppendLine(sb, $"GameSeed: {gameManager?.GameSeed.ToString() ?? "UNKNOWN"}");
        AppendLine(sb, $"BuildOrSessionIdentifier: {sessionStamp}");
        AppendLine(sb, $"LogPath: {sessionLogPath}");
        AppendLine(sb, $"LatestPath: {latestLogPath}");
        AppendLine(sb, $"Context: {orchestrator?.CurrentContext.ToString() ?? "NO_TURN_ORCHESTRATOR"}");
        AppendLine(sb, $"Player.Name: {playerCharacter?.Name ?? "NULL"}");
        AppendLine(sb, $"Player.ID: {playerCharacter?.IInteractableID.ToString() ?? "NULL"}");
        AppendLine(sb, $"Player.CurrentNestedArea: {FormatArea(playerCharacter?.CurrentNestedArea)}");
        AppendLine(sb, $"Player.NestedMapPosition: {playerCharacter?.NestedMapPosition.ToString() ?? "NULL"}");
        AppendLine(sb, $"PlayerStats.CurrentNestedArea: {FormatArea(playerStats?.CurrentNestedArea)}");
        AppendLine(sb, string.Empty);
        return sb.ToString();
    }

    private static void WriteBlock(string category, string eventName, string details, Character attacker, Character defender, bool forceWarning)
    {
        EnsureInitialized();
        if (loggingFailed) return;

        try
        {
            var sb = new StringBuilder();
            AppendLine(sb, $"{category} {DateTime.Now:yyyy-MM-dd HH:mm:ss} | frame={Time.frameCount}");
            AppendLine(sb, $"Event: {eventName}");
            AppendSceneAndContext(sb);

            if (attacker != null)
            {
                AppendCharacter(sb, "Attacker", attacker);
            }

            if (defender != null)
            {
                AppendCharacter(sb, "Defender", defender);
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

            string block = sb.ToString();
            WriteCombatOnly(block);
            WriteShared(block);

            if (forceWarning)
            {
                Debug.LogWarning($"{DiagnosticId} {category} {eventName}");
            }
        }
        catch (Exception ex)
        {
            loggingFailed = true;
            Debug.LogWarning($"{DiagnosticId} [WARNING] Failed to write combat diagnostics log. Gameplay will continue. {ex.Message}");
        }
    }

    private static void AppendSceneAndContext(StringBuilder sb)
    {
        var scene = SceneManager.GetActiveScene();
        var orchestrator = TurnOrchestrator.Instance;
        var playerStats = PlayerStats.Instance;
        var playerCharacter = playerStats != null ? playerStats.CurrentPlayerCharacter : null;

        AppendLine(sb, $"Scene: {(scene.IsValid() ? scene.name : "UNKNOWN")}");
        AppendLine(sb, $"Context: {orchestrator?.CurrentContext.ToString() ?? "NO_TURN_ORCHESTRATOR"}");
        AppendLine(sb, $"GameSeed: {GameManager.Instance?.GameSeed.ToString() ?? "UNKNOWN"}");
        AppendLine(sb, $"Player.Name: {playerCharacter?.Name ?? "NULL"}");
        AppendLine(sb, $"Player.ID: {playerCharacter?.IInteractableID.ToString() ?? "NULL"}");
        AppendLine(sb, $"CurrentNestedArea: {FormatArea(playerStats?.CurrentNestedArea ?? playerCharacter?.CurrentNestedArea)}");
    }

    private static void AppendCharacter(StringBuilder sb, string label, Character character)
    {
        AppendLine(sb, $"{label}.Name: {character.Name}");
        AppendLine(sb, $"{label}.ID: {character.IInteractableID}");
        AppendLine(sb, $"{label}.Type: {character.GetType().Name}");
        AppendLine(sb, $"{label}.Role: {BaseTurnManager.GetCombatParticipantRole(character)}");
        AppendLine(sb, $"{label}.IsActive: {character.IsActive}");
        AppendLine(sb, $"{label}.IsAlive: {character.IsAlive}");
        AppendLine(sb, $"{label}.IsHostile: {character.IsHostile}");
        AppendLine(sb, $"{label}.InCombat: {character.InCombat}");
        AppendLine(sb, $"{label}.InTurn: {character.InTurn}");
        AppendLine(sb, $"{label}.Health: {character.Health}/{character.MaxHealth}");
        AppendLine(sb, $"{label}.ActionPoints: {character.ActionPoints}/{character.MaxActionPoints}");
        AppendLine(sb, $"{label}.MovePoints: {character.MovePoints}/{character.MaxMovePoints}");
        AppendLine(sb, $"{label}.CurrentNestedArea: {FormatArea(character.CurrentNestedArea)}");
        AppendLine(sb, $"{label}.NestedMapPosition: {character.NestedMapPosition}");
        AppendLine(sb, $"{label}.Target: {(character.Target != null ? $"{character.Target.Name} [{character.Target.IInteractableID}]" : "NULL")}");
        AppendLine(sb, $"{label}.MainHand: {FormatItemSummary(character.GetMainHandItem())}");
        AppendLine(sb, $"{label}.EquippedItems: {FormatEquipmentSummary(character)}");
    }

    private static void WriteCombatOnly(string text)
    {
        lock (FileLock)
        {
            File.AppendAllText(sessionLogPath, text);
            File.AppendAllText(latestLogPath, text);
        }
    }

    private static void WriteShared(string text)
    {
        string sharedSessionPath = TurnDiagnosticsLogger.SessionLogPath;
        string directory = Path.GetDirectoryName(sharedSessionPath);
        string sharedLatestPath = Path.Combine(directory, "TinyAdventure_TurnDiagnostics_Latest.txt");

        lock (FileLock)
        {
            File.AppendAllText(sharedSessionPath, text);
            File.AppendAllText(sharedLatestPath, text);
        }
    }

    private static void AppendLine(StringBuilder sb, string text)
    {
        sb.AppendLine($"{DiagnosticId} {text}");
    }
}
