// CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostics helper.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class NestedMapDebugger
{
    private const int MaxCellsToPrint = 400;

    // CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic method.
    public static void LogSnapshot(INestedArea area, string reason)
    {
        try
        {
            var sb = new StringBuilder();
            MovementAIDiagnosticsLogger.AppendLine(sb, $"[NESTED MAP SNAPSHOT] {DateTime.Now:yyyy-MM-dd HH:mm:ss} | frame={Time.frameCount}");
            MovementAIDiagnosticsLogger.AppendLine(sb, $"Reason: {reason}");

            if (area == null)
            {
                MovementAIDiagnosticsLogger.AppendLine(sb, "Area: NULL");
                MovementAIDiagnosticsLogger.AppendLine(sb, "Snapshot skipped: area is null.");
                MovementAIDiagnosticsLogger.AppendLine(sb, string.Empty);
                MovementAIDiagnosticsLogger.WriteRawBlock(sb.ToString());
                return;
            }

            Cell[,] map = area.GetNestedMap();
            if (map == null)
            {
                MovementAIDiagnosticsLogger.AppendLine(sb, $"Area: {MovementAIDiagnosticsLogger.FormatArea(area)}");
                MovementAIDiagnosticsLogger.AppendLine(sb, "Snapshot skipped: nested map is null.");
                MovementAIDiagnosticsLogger.AppendLine(sb, string.Empty);
                MovementAIDiagnosticsLogger.WriteRawBlock(sb.ToString());
                return;
            }

            int width = map.GetLength(0);
            int height = map.GetLength(1);
            var player = PlayerStats.Instance?.CurrentPlayerCharacter;
            Vector2Int playerPosition = player != null ? player.NestedMapPosition : Vector2Int.zero;
            var roster = BuildRoster(area, map);

            MovementAIDiagnosticsLogger.AppendLine(sb, $"Area: {MovementAIDiagnosticsLogger.FormatArea(area)}");
            MovementAIDiagnosticsLogger.AppendLine(sb, $"Size: {width}x{height}");
            MovementAIDiagnosticsLogger.AppendLine(sb, $"Player: {(player != null ? player.Name : "NULL")} at {playerPosition}");
            MovementAIDiagnosticsLogger.AppendLine(sb, $"Visible character count: {roster.Count}");
            MovementAIDiagnosticsLogger.AppendLine(sb, $"NPC count: {roster.Count(r => r.Character is NPC)}");
            MovementAIDiagnosticsLogger.AppendLine(sb, $"Animal count: {roster.Count(r => r.Character is Animal)}");
            MovementAIDiagnosticsLogger.AppendLine(sb, $"Monster count: {roster.Count(r => r.Character is Monster)}");
            MovementAIDiagnosticsLogger.AppendLine(sb, $"Object-character count: {roster.Count(r => r.Source.Contains("Objects"))}");
            MovementAIDiagnosticsLogger.AppendLine(sb, "Legend: @=Player, N=NPC, A=Animal, M=Monster, X=Multiple, O=Object, #=Blocked/Wall, .=Empty, ?=Unknown");

            if (width * height <= MaxCellsToPrint)
            {
                for (int y = height - 1; y >= 0; y--)
                {
                    var row = new StringBuilder();
                    row.Append($"y={y} ");
                    for (int x = 0; x < width; x++)
                    {
                        row.Append(GetCellSymbol(area, map[x, y], new Vector2Int(x, y), player, playerPosition));
                        if (x < width - 1) row.Append(' ');
                    }

                    MovementAIDiagnosticsLogger.AppendLine(sb, row.ToString());
                }
            }
            else
            {
                MovementAIDiagnosticsLogger.AppendLine(sb, $"Grid skipped: size {width * height} exceeds cap {MaxCellsToPrint}.");
            }

            MovementAIDiagnosticsLogger.AppendLine(sb, "[NESTED MAP ROSTER]");
            foreach (var entry in roster)
            {
                Character c = entry.Character;
                MovementAIDiagnosticsLogger.AppendLine(sb,
                    $"{c.GetType().Name} {c.Name} [{c.IInteractableID}] pos={c.NestedMapPosition} active={c.IsActive} alive={c.IsAlive} area={MovementAIDiagnosticsLogger.FormatArea(c.CurrentNestedArea)} cellSource={entry.Source}");
            }

            MovementAIDiagnosticsLogger.AppendLine(sb, string.Empty);
            MovementAIDiagnosticsLogger.WriteRawBlock(sb.ToString());
        }
        catch (Exception ex)
        {
            MovementAIDiagnosticsLogger.LogWarning("NestedMapDebugger.LogSnapshot failed", ex.Message);
        }
    }

    // CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic method.
    public static void LogSnapshotForMovement(INestedArea area, Character mover, string reason)
    {
        LogSnapshot(area ?? mover?.CurrentNestedArea, reason);
    }

    // CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic method.
    private static char GetCellSymbol(INestedArea area, Cell cell, Vector2Int position, Character player, Vector2Int playerPosition)
    {
        if (cell == null) return '?';
        if (player != null && playerPosition == position && player.CurrentNestedArea == area) return '@';

        int characterCount = 0;
        bool hasNpc = false;
        bool hasAnimal = false;
        bool hasMonster = false;

        if (cell.Objects != null)
        {
            foreach (var character in cell.Objects.OfType<Character>())
            {
                if (character == null || !character.IsActive) continue;
                characterCount++;
                hasNpc |= character is NPC;
                hasAnimal |= character is Animal;
                hasMonster |= character is Monster;
            }
        }

        if (cell.Animals != null)
        {
            foreach (var animal in cell.Animals)
            {
                if (animal == null || !animal.IsActive) continue;
                characterCount++;
                hasAnimal = true;
            }
        }

        if (characterCount > 1) return 'X';
        if (hasMonster) return 'M';
        if (hasNpc) return 'N';
        if (hasAnimal) return 'A';
        if (cell.Objects != null && cell.Objects.Any(obj => obj != null && obj.IsActive)) return 'O';
        if (!cell.isPassable) return '#';
        return '.';
    }

    // CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic method.
    private static List<RosterEntry> BuildRoster(INestedArea area, Cell[,] map)
    {
        var roster = new List<RosterEntry>();
        var seen = new HashSet<int>();
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Cell cell = map[x, y];
                if (cell == null) continue;

                if (cell.Objects != null)
                {
                    foreach (var character in cell.Objects.OfType<Character>())
                    {
                        AddRosterEntry(roster, seen, character, "Objects");
                    }
                }

                if (cell.Animals != null)
                {
                    foreach (var animal in cell.Animals)
                    {
                        AddRosterEntry(roster, seen, animal, "Animals");
                    }
                }
            }
        }

        foreach (var character in area.GetAllCharactersInArea())
        {
            AddRosterEntry(roster, seen, character, "GetAllCharactersInArea");
        }

        return roster;
    }

    // CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic method.
    private static void AddRosterEntry(List<RosterEntry> roster, HashSet<int> seen, Character character, string source)
    {
        if (character == null || !seen.Add(character.IInteractableID)) return;
        roster.Add(new RosterEntry { Character = character, Source = source });
    }

    private class RosterEntry
    {
        public Character Character;
        public string Source;
    }
}
