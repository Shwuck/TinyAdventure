using System.Collections.Generic;
using System.Linq;

public static class RelationshipManager
{
    private static readonly Dictionary<string, CharacterRelationship> relationships = new Dictionary<string, CharacterRelationship>();

    public static int RelationshipCount => relationships.Count;

    public static CharacterRelationship GetRelationship(int sourceCharacterId, int targetCharacterId)
    {
        relationships.TryGetValue(GetKey(sourceCharacterId, targetCharacterId), out CharacterRelationship relationship);
        return relationship;
    }

    public static bool HasActiveHostility(int sourceCharacterId, int targetCharacterId)
    {
        CharacterRelationship relationship = GetRelationship(sourceCharacterId, targetCharacterId);
        return relationship != null && relationship.ActiveHostility;
    }

    public static CharacterRelationship SetActiveHostility(Character source, Character target, string reason)
    {
        if (source == null || target == null || source == target)
        {
            // CODEXLOG004_RELATIONSHIPS: temporary relationship diagnostic call.
            RelationshipDiagnosticsLogger.LogEvent("[RELATIONSHIP UPDATED]", "SetActiveHostility skipped invalid source/target",
                $"Source: {FormatCharacter(source)}\n" +
                $"Target: {FormatCharacter(target)}\n" +
                $"Reason: {reason ?? "NULL"}");
            return null;
        }

        string key = GetKey(source.IInteractableID, target.IInteractableID);
        int currentDay = GetCurrentDay();

        if (!relationships.TryGetValue(key, out CharacterRelationship relationship))
        {
            relationship = new CharacterRelationship
            {
                SourceCharacterId = source.IInteractableID,
                TargetCharacterId = target.IInteractableID,
                EstablishedDay = currentDay
            };
            relationships[key] = relationship;
        }

        relationship.ActiveHostility = true;
        relationship.Reason = reason;
        relationship.LastUpdatedDay = currentDay;
        AddTagIfMissing(relationship, "ActiveHostility");

        // CODEXLOG004_RELATIONSHIPS: temporary relationship diagnostic call.
        RelationshipDiagnosticsLogger.LogEvent("[RELATIONSHIP UPDATED]", "RelationshipManager.SetActiveHostility",
            $"Source: {FormatCharacter(source)}\n" +
            $"Target: {FormatCharacter(target)}\n" +
            $"ActiveHostility: {relationship.ActiveHostility}\n" +
            $"Reason: {relationship.Reason ?? "NULL"}\n" +
            $"EstablishedDay: {relationship.EstablishedDay}\n" +
            $"LastUpdatedDay: {relationship.LastUpdatedDay}\n" +
            $"RelationshipCount: {relationships.Count}");

        return relationship;
    }

    public static List<RelationshipHostility> ScanLocalActiveHostilities(IEnumerable<Character> localActors, INestedArea area)
    {
        List<Character> actors = localActors?
            .Where(actor => actor != null && actor.IsActive)
            .GroupBy(actor => actor.IInteractableID)
            .Select(group => group.First())
            .ToList() ?? new List<Character>();

        Dictionary<int, Character> actorById = actors.ToDictionary(actor => actor.IInteractableID, actor => actor);
        List<RelationshipHostility> hostilities = new List<RelationshipHostility>();
        int relationshipsChecked = 0;

        foreach (Character source in actors)
        {
            foreach (Character target in actors)
            {
                if (source == target) continue;

                relationshipsChecked++;
                CharacterRelationship relationship = GetRelationship(source.IInteractableID, target.IInteractableID);
                if (relationship == null || !relationship.ActiveHostility)
                {
                    continue;
                }

                if (!actorById.ContainsKey(relationship.SourceCharacterId) ||
                    !actorById.ContainsKey(relationship.TargetCharacterId))
                {
                    continue;
                }

                hostilities.Add(new RelationshipHostility(source, target, relationship));
            }
        }

        // CODEXLOG004_RELATIONSHIPS: temporary local scene relationship scan diagnostic.
        RelationshipDiagnosticsLogger.LogEvent("[SCENE RELATIONSHIP SCAN]", "RelationshipManager.ScanLocalActiveHostilities",
            $"Area: {FormatArea(area)}\n" +
            $"LocalActors: {actors.Count}\n" +
            $"RelationshipsChecked: {relationshipsChecked}\n" +
            $"StoredRelationships: {relationships.Count}\n" +
            $"ActiveHostilitiesFound: {hostilities.Count}\n" +
            $"Hostilities: {(hostilities.Count > 0 ? string.Join(", ", hostilities.Select(FormatHostility)) : "NONE")}");

        return hostilities;
    }

    public static void ApplyLocalHostilitiesToActorState(IEnumerable<RelationshipHostility> hostilities)
    {
        if (hostilities == null) return;

        foreach (RelationshipHostility hostility in hostilities)
        {
            if (hostility.Source == null || hostility.Target == null) continue;

            hostility.Source.IsHostile = true;
            hostility.Source.Stance = NPCStance.Hostile;
            hostility.Source.Target = hostility.Target;
            hostility.Source.InCombat = true;

            // CODEXLOG004_RELATIONSHIPS: temporary relationship-to-combat-state diagnostic.
            RelationshipDiagnosticsLogger.LogEvent("[RELATIONSHIP HOSTILITY APPLIED]", "RelationshipManager.ApplyLocalHostilitiesToActorState",
                $"Source: {FormatCharacter(hostility.Source)}\n" +
                $"Target: {FormatCharacter(hostility.Target)}\n" +
                $"Reason: {hostility.Relationship?.Reason ?? "NULL"}\n" +
                $"SourceIsHostile: {hostility.Source.IsHostile}\n" +
                $"SourceStance: {hostility.Source.Stance}");
        }
    }

    public static string FormatHostility(RelationshipHostility hostility)
    {
        if (hostility == null) return "NULL";
        return $"{FormatCharacter(hostility.Source)} -> {FormatCharacter(hostility.Target)} Reason={hostility.Relationship?.Reason ?? "NULL"}";
    }

    private static string GetKey(int sourceCharacterId, int targetCharacterId)
    {
        return $"{sourceCharacterId}:{targetCharacterId}";
    }

    private static void AddTagIfMissing(CharacterRelationship relationship, string tag)
    {
        if (relationship == null || string.IsNullOrEmpty(tag)) return;
        if (!relationship.Tags.Contains(tag))
        {
            relationship.Tags.Add(tag);
        }
    }

    private static int GetCurrentDay()
    {
        return TimeManager.Instance != null ? TimeManager.Instance.TotalDaysPassed : -1;
    }

    private static string FormatCharacter(Character character)
    {
        if (character == null) return "NULL";
        return $"{character.Name} [{character.IInteractableID}] ({character.GetType().Name})";
    }

    private static string FormatArea(INestedArea area)
    {
        if (area == null) return "NULL";
        return $"{area.Name} (ID={area.NestedAreaID}, Level={area.NestedAreaLevel})";
    }
}

public class RelationshipHostility
{
    public Character Source { get; }
    public Character Target { get; }
    public CharacterRelationship Relationship { get; }

    public RelationshipHostility(Character source, Character target, CharacterRelationship relationship)
    {
        Source = source;
        Target = target;
        Relationship = relationship;
    }
}
