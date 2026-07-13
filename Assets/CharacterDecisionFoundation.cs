using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CharacterInterestTag
{
    None,
    Smithing,
    Metalwork,
    Workstation,
    GuardPost,
    PatrolRoute,
    Entrance,
    Rest,
    Sleep,
    Sit,
    Drink,
    Socialise,
    Trade,
    Food,
    FoodSmell,
    Owner,
    Prey,
    Lair,
    Intruder,
    Shelter,
    Warmth,
    CrimeReport,
    Inspect,
    Loot,
    Wander,
    Noise
}

public enum InterestCandidateType
{
    None,
    Cell,
    Object,
    Character,
    Event,
    CurrentTask
}

public enum CharacterWorldDecisionType
{
    None,
    MoveTowardsCandidate,
    UseAffordanceInPlace,
    IntentionalIdle,
    WanderFallback,
    FailedMovement,
    SkippedCannotAct
}

public sealed class CharacterDecisionProfile
{
    public string ProfileId { get; }
    public string SourceDescription { get; }
    public IReadOnlyDictionary<CharacterInterestTag, int> InterestWeights => interestWeights;
    public bool AllowsRandomWanderFallback { get; }

    private readonly Dictionary<CharacterInterestTag, int> interestWeights;

    public CharacterDecisionProfile(string profileId, string sourceDescription, Dictionary<CharacterInterestTag, int> weights, bool allowsRandomWanderFallback)
    {
        ProfileId = string.IsNullOrWhiteSpace(profileId) ? "unknown" : profileId;
        SourceDescription = string.IsNullOrWhiteSpace(sourceDescription) ? "unknown" : sourceDescription;
        interestWeights = weights ?? new Dictionary<CharacterInterestTag, int>();
        AllowsRandomWanderFallback = allowsRandomWanderFallback;
    }

    public int GetWeight(CharacterInterestTag tag)
    {
        return interestWeights.TryGetValue(tag, out int weight) ? weight : 0;
    }

    public string FormatInterests()
    {
        if (interestWeights.Count == 0)
        {
            return "None";
        }

        return string.Join(", ", interestWeights
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key.ToString())
            .Select(pair => $"{pair.Key}:{pair.Value}"));
    }
}

public sealed class WorldAffordance
{
    public CharacterInterestTag Tag { get; }
    public InterestCandidateType CandidateType { get; }
    public string SourceName { get; }
    public Vector2Int SourcePosition { get; }
    public bool RequiresAdjacentPosition { get; }
    public IInteractable Interactable { get; }

    public WorldAffordance(CharacterInterestTag tag, InterestCandidateType candidateType, string sourceName, Vector2Int sourcePosition, bool requiresAdjacentPosition, IInteractable interactable = null)
    {
        Tag = tag;
        CandidateType = candidateType;
        SourceName = string.IsNullOrWhiteSpace(sourceName) ? "Unknown" : sourceName;
        SourcePosition = sourcePosition;
        RequiresAdjacentPosition = requiresAdjacentPosition;
        Interactable = interactable;
    }
}

public sealed class InterestCandidate
{
    public InterestCandidateType CandidateType { get; set; }
    public CharacterInterestTag MatchingInterest { get; set; }
    public string SourceName { get; set; }
    public Vector2Int SourcePosition { get; set; }
    public Vector2Int TargetPosition { get; set; }
    public bool RequiresMovement { get; set; }
    public int Distance { get; set; }
    public int Score { get; set; }
    public bool IsReachable { get; set; }
    public string Reason { get; set; }
    public IInteractable Interactable { get; set; }
}

public sealed class CharacterDecisionResult
{
    public bool Resolved { get; set; }
    public CharacterWorldDecisionType DecisionType { get; set; }
    public CharacterTurnDecisionResult TurnDecisionResult { get; set; }
    public CharacterDecisionProfile Profile { get; set; }
    public CharacterInterestTag SelectedInterest { get; set; }
    public InterestCandidate SelectedCandidate { get; set; }
    public int CandidateCount { get; set; }
    public bool MovementAttempted { get; set; }
    public bool MovementSucceeded { get; set; }
    public bool RandomWanderSelected { get; set; }
    public string Reason { get; set; }
}

public static class WorldAffordanceProvider
{
    private static readonly CharacterInterestTag[] EmptyTags = Array.Empty<CharacterInterestTag>();

    public static List<InterestCandidate> DiscoverCandidates(Character actor, CharacterDecisionProfile profile)
    {
        List<InterestCandidate> candidates = new List<InterestCandidate>();
        if (actor == null || profile == null || actor.CurrentNestedArea == null)
        {
            return candidates;
        }

        AddObjectCandidates(actor, profile, candidates);
        AddCellCandidates(actor, profile, candidates);

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.SourceName)
            .ToList();
    }

    private static void AddObjectCandidates(Character actor, CharacterDecisionProfile profile, List<InterestCandidate> candidates)
    {
        foreach (IInteractable interactable in actor.CurrentNestedArea.GetAllObjectsInArea())
        {
            if (interactable == null || !interactable.IsActive)
            {
                continue;
            }

            CharacterInterestTag[] affordances = GetAffordanceTags(interactable);
            if (affordances.Length == 0)
            {
                continue;
            }

            foreach (CharacterInterestTag tag in affordances)
            {
                InterestCandidate candidate = TryBuildCandidate(
                    actor,
                    profile,
                    tag,
                    interactable.NestedMapPosition,
                    requiresAdjacentPosition: !interactable.IsPassable,
                    InterestCandidateType.Object,
                    interactable.Name,
                    interactable);

                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }
        }
    }

    private static void AddCellCandidates(Character actor, CharacterDecisionProfile profile, List<InterestCandidate> candidates)
    {
        Cell[,] map = actor.CurrentNestedArea.GetNestedMap();
        if (map == null)
        {
            return;
        }

        for (int x = 0; x < map.GetLength(0); x++)
        {
            for (int y = 0; y < map.GetLength(1); y++)
            {
                Cell cell = map[x, y];
                if (cell == null)
                {
                    continue;
                }

                if (cell.isIndoors)
                {
                    InterestCandidate shelterCandidate = TryBuildCandidate(
                        actor,
                        profile,
                        CharacterInterestTag.Shelter,
                        cell.Coordinates,
                        requiresAdjacentPosition: false,
                        InterestCandidateType.Cell,
                        $"IndoorCell[{cell.Coordinates.x},{cell.Coordinates.y}]",
                        null);

                    if (shelterCandidate != null)
                    {
                        candidates.Add(shelterCandidate);
                    }
                }
            }
        }
    }

    private static InterestCandidate TryBuildCandidate(
        Character actor,
        CharacterDecisionProfile profile,
        CharacterInterestTag tag,
        Vector2Int sourcePosition,
        bool requiresAdjacentPosition,
        InterestCandidateType candidateType,
        string sourceName,
        IInteractable interactable)
    {
        int weight = profile.GetWeight(tag);
        if (weight <= 0)
        {
            return null;
        }

        Vector2Int targetPosition = GetTargetPosition(actor, sourcePosition, requiresAdjacentPosition);
        if (targetPosition == InvalidPosition)
        {
            return null;
        }

        int distance = ManhattanDistance(actor.NestedMapPosition, targetPosition);
        int score = weight - (distance * 5);
        if (score <= 0)
        {
            return null;
        }

        return new InterestCandidate
        {
            CandidateType = candidateType,
            MatchingInterest = tag,
            SourceName = sourceName,
            SourcePosition = sourcePosition,
            TargetPosition = targetPosition,
            RequiresMovement = actor.NestedMapPosition != targetPosition,
            Distance = distance,
            Score = score,
            IsReachable = true,
            Reason = $"{tag} matched {sourceName}",
            Interactable = interactable
        };
    }

    private static CharacterInterestTag[] GetAffordanceTags(IInteractable interactable)
    {
        switch (interactable)
        {
            case Anvil:
                return new[] { CharacterInterestTag.Smithing, CharacterInterestTag.Metalwork, CharacterInterestTag.Workstation };
            case Campfire:
                return new[] { CharacterInterestTag.Warmth, CharacterInterestTag.Rest, CharacterInterestTag.Socialise };
            case Door:
                return new[] { CharacterInterestTag.Entrance, CharacterInterestTag.GuardPost };
            case Corpse:
                return new[] { CharacterInterestTag.Inspect, CharacterInterestTag.Loot, CharacterInterestTag.CrimeReport };
            case Carcass:
            case MonsterRemains:
                return new[] { CharacterInterestTag.Inspect, CharacterInterestTag.Loot };
            default:
                return EmptyTags;
        }
    }

    private static readonly Vector2Int InvalidPosition = new Vector2Int(int.MinValue, int.MinValue);

    private static Vector2Int GetTargetPosition(Character actor, Vector2Int sourcePosition, bool requiresAdjacentPosition)
    {
        if (actor == null || actor.CurrentNestedArea == null)
        {
            return InvalidPosition;
        }

        if (!requiresAdjacentPosition)
        {
            if (actor.NestedMapPosition == sourcePosition)
            {
                return sourcePosition;
            }

            return actor.CurrentNestedArea.IsPassable(sourcePosition) ? sourcePosition : InvalidPosition;
        }

        if (IsAdjacent(actor.NestedMapPosition, sourcePosition))
        {
            return actor.NestedMapPosition;
        }

        List<Vector2Int> adjacentPositions = new List<Vector2Int>
        {
            sourcePosition + Vector2Int.up,
            sourcePosition + Vector2Int.down,
            sourcePosition + Vector2Int.left,
            sourcePosition + Vector2Int.right
        };

        List<Vector2Int> validAdjacentPositions = adjacentPositions
            .Where(position => actor.CurrentNestedArea.IsValidPosition(position) && actor.CurrentNestedArea.IsPassable(position))
            .OrderBy(position => ManhattanDistance(actor.NestedMapPosition, position))
            .ToList();

        return validAdjacentPositions.Count > 0 ? validAdjacentPositions[0] : InvalidPosition;
    }

    private static bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        return ManhattanDistance(a, b) == 1;
    }

    private static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}

public static class CharacterDecisionResolver
{
    public static CharacterDecisionResult ResolveWorldDecision(Character actor)
    {
        CharacterDecisionResult result = new CharacterDecisionResult
        {
            Resolved = true,
            DecisionType = CharacterWorldDecisionType.IntentionalIdle,
            TurnDecisionResult = CharacterTurnDecisionResult.Idled,
            Reason = "No decision resolved."
        };

        if (actor == null)
        {
            result.Resolved = false;
            result.DecisionType = CharacterWorldDecisionType.SkippedCannotAct;
            result.TurnDecisionResult = CharacterTurnDecisionResult.Skipped;
            result.Reason = "CharacterDecisionResolver received a null actor.";
            return result;
        }

        CharacterDecisionProfile profile = BuildProfile(actor);
        result.Profile = profile;

        if (!CanAct(actor, out string cannotActReason))
        {
            result.DecisionType = CharacterWorldDecisionType.SkippedCannotAct;
            result.TurnDecisionResult = CharacterTurnDecisionResult.Skipped;
            result.Reason = cannotActReason;
            LogDecision(actor, result);
            return result;
        }

        List<InterestCandidate> candidates = WorldAffordanceProvider.DiscoverCandidates(actor, profile);
        result.CandidateCount = candidates.Count;
        InterestCandidate selectedCandidate = candidates.FirstOrDefault();

        if (selectedCandidate != null)
        {
            result.SelectedCandidate = selectedCandidate;
            result.SelectedInterest = selectedCandidate.MatchingInterest;

            if (!selectedCandidate.RequiresMovement)
            {
                result.DecisionType = CharacterWorldDecisionType.UseAffordanceInPlace;
                result.TurnDecisionResult = CharacterTurnDecisionResult.PerformedAction;
                result.Reason = $"Used {selectedCandidate.SourceName} for {selectedCandidate.MatchingInterest} without moving.";
                LogDecision(actor, result);
                return result;
            }

            result.MovementAttempted = true;

            if (actor.MovePoints <= 0)
            {
                result.DecisionType = CharacterWorldDecisionType.FailedMovement;
                result.TurnDecisionResult = CharacterTurnDecisionResult.FailedMovement;
                result.Reason = $"Selected {selectedCandidate.SourceName} for {selectedCandidate.MatchingInterest}, but no MovePoints remained.";
                LogDecision(actor, result);
                return result;
            }

            bool moved = actor.MoveTowards(selectedCandidate.TargetPosition);
            result.MovementSucceeded = moved;
            result.DecisionType = moved ? CharacterWorldDecisionType.MoveTowardsCandidate : CharacterWorldDecisionType.FailedMovement;
            result.TurnDecisionResult = moved ? CharacterTurnDecisionResult.Moved : CharacterTurnDecisionResult.FailedMovement;
            result.Reason = moved
                ? $"Moved toward {selectedCandidate.SourceName} for {selectedCandidate.MatchingInterest}."
                : $"Could not move toward {selectedCandidate.SourceName} for {selectedCandidate.MatchingInterest}.";
            LogDecision(actor, result);
            return result;
        }

        ResolveIdleFallback(actor, profile, result);
        LogDecision(actor, result);
        return result;
    }

    private static bool CanAct(Character actor, out string reason)
    {
        if (!actor.IsAlive)
        {
            reason = "Actor cannot resolve a world decision because IsAlive is false.";
            return false;
        }

        if (!actor.IsActive)
        {
            reason = "Actor cannot resolve a world decision because IsActive is false.";
            return false;
        }

        if (actor.CurrentNestedArea == null)
        {
            reason = "Actor cannot resolve a world decision because CurrentNestedArea is null.";
            return false;
        }

        reason = null;
        return true;
    }

    private static void ResolveIdleFallback(Character actor, CharacterDecisionProfile profile, CharacterDecisionResult result)
    {
        if (profile.AllowsRandomWanderFallback && actor.MovePoints > 0 && UnityEngine.Random.value < 0.2f)
        {
            result.MovementAttempted = true;
            result.RandomWanderSelected = true;

            bool moved = actor.MoveInRandomDirection();
            result.MovementSucceeded = moved;
            result.DecisionType = moved ? CharacterWorldDecisionType.WanderFallback : CharacterWorldDecisionType.IntentionalIdle;
            result.TurnDecisionResult = moved ? CharacterTurnDecisionResult.Moved : CharacterTurnDecisionResult.Idled;
            result.Reason = moved
                ? "No stronger world affordance was available, so the actor chose one bounded wander step."
                : "No stronger world affordance was available; wander fallback was considered but no valid move existed.";
            return;
        }

        result.DecisionType = CharacterWorldDecisionType.IntentionalIdle;
        result.TurnDecisionResult = CharacterTurnDecisionResult.Idled;
        result.Reason = "No matching world affordance was found, so the actor intentionally idled.";
    }

    private static CharacterDecisionProfile BuildProfile(Character actor)
    {
        Dictionary<CharacterInterestTag, int> weights = new Dictionary<CharacterInterestTag, int>();
        string profileId = actor.GetType().Name;
        string sourceDescription = actor.GetType().Name;
        bool allowWanderFallback = false;

        if (actor is NPC npc)
        {
            profileId = $"NPC:{npc.Role}";
            sourceDescription = $"NPC role {npc.Role}";
            allowWanderFallback =
                npc.Role == NPCRole.Explorer ||
                npc.Role == NPCRole.Hunter ||
                npc.Role == NPCRole.Scout ||
                npc.Role == NPCRole.Adventurer;
            AddWeight(weights, CharacterInterestTag.Shelter, 25);
            AddWeight(weights, CharacterInterestTag.Rest, 20);
            AddWeight(weights, CharacterInterestTag.Socialise, 15);
            AddWeight(weights, CharacterInterestTag.Wander, 10);

            ApplyRoleDataInterests(npc, weights);
        }
        else if (actor is Animal animal)
        {
            profileId = $"Animal:{animal.Name}";
            sourceDescription = animal.IsPredator ? "Predator animal" : "Animal";
            allowWanderFallback = true;
            AddWeight(weights, CharacterInterestTag.Shelter, 15);
            AddWeight(weights, CharacterInterestTag.Rest, 10);
            AddWeight(weights, CharacterInterestTag.Wander, 25);

            if (animal.IsDomestic || animal.IsTame)
            {
                AddWeight(weights, CharacterInterestTag.Owner, 35);
                AddWeight(weights, CharacterInterestTag.Food, 20);
                AddWeight(weights, CharacterInterestTag.Shelter, 25);
            }

            if (animal.IsPredator)
            {
                AddWeight(weights, CharacterInterestTag.Prey, 35);
                AddWeight(weights, CharacterInterestTag.Noise, 10);
            }
            else
            {
                AddWeight(weights, CharacterInterestTag.Food, 20);
                AddWeight(weights, CharacterInterestTag.Sleep, 10);
            }
        }
        else if (actor is Monster monster)
        {
            profileId = $"Monster:{monster.Type}";
            sourceDescription = $"Monster type {monster.Type}";
            allowWanderFallback = true;
            AddWeight(weights, CharacterInterestTag.Lair, 30);
            AddWeight(weights, CharacterInterestTag.Intruder, 25);
            AddWeight(weights, CharacterInterestTag.PatrolRoute, 20);
            AddWeight(weights, CharacterInterestTag.Wander, 10);
        }
        else
        {
            allowWanderFallback = false;
            AddWeight(weights, CharacterInterestTag.Shelter, 20);
            AddWeight(weights, CharacterInterestTag.Rest, 15);
            AddWeight(weights, CharacterInterestTag.Wander, 5);
        }

        return new CharacterDecisionProfile(profileId, sourceDescription, weights, allowWanderFallback);
    }

    private static void ApplyRoleDataInterests(NPC npc, Dictionary<CharacterInterestTag, int> weights)
    {
        NPCRoleData roleData = PermaLists.Instance?.RoleData?.FirstOrDefault(role => role.Role == npc.Role);
        if (roleData?.IsCraftsman == true)
        {
            AddWeight(weights, CharacterInterestTag.Workstation, 45);
        }

        if (roleData != null)
        {
            switch (roleData.CraftingType)
            {
                case CraftingType.Smithing:
                    AddWeight(weights, CharacterInterestTag.Smithing, 70);
                    AddWeight(weights, CharacterInterestTag.Metalwork, 60);
                    AddWeight(weights, CharacterInterestTag.Workstation, 55);
                    break;
                case CraftingType.Cooking:
                    AddWeight(weights, CharacterInterestTag.Food, 35);
                    AddWeight(weights, CharacterInterestTag.Warmth, 25);
                    AddWeight(weights, CharacterInterestTag.Workstation, 20);
                    break;
                case CraftingType.Alchemy:
                    AddWeight(weights, CharacterInterestTag.Workstation, 35);
                    AddWeight(weights, CharacterInterestTag.Inspect, 15);
                    break;
                case CraftingType.Crafting:
                    AddWeight(weights, CharacterInterestTag.Workstation, 35);
                    break;
            }
        }

        switch (npc.Role)
        {
            case NPCRole.Blacksmith:
                AddWeight(weights, CharacterInterestTag.Smithing, 80);
                AddWeight(weights, CharacterInterestTag.Metalwork, 70);
                AddWeight(weights, CharacterInterestTag.Workstation, 65);
                break;
            case NPCRole.Guard:
                AddWeight(weights, CharacterInterestTag.GuardPost, 70);
                AddWeight(weights, CharacterInterestTag.Entrance, 55);
                AddWeight(weights, CharacterInterestTag.PatrolRoute, 45);
                break;
            case NPCRole.Innkeeper:
            case NPCRole.Bard:
                AddWeight(weights, CharacterInterestTag.Socialise, 40);
                AddWeight(weights, CharacterInterestTag.Trade, 25);
                AddWeight(weights, CharacterInterestTag.Warmth, 20);
                break;
            case NPCRole.Merchant:
            case NPCRole.Trader:
                AddWeight(weights, CharacterInterestTag.Trade, 45);
                AddWeight(weights, CharacterInterestTag.Socialise, 20);
                break;
            case NPCRole.Hunter:
            case NPCRole.Scout:
            case NPCRole.Explorer:
            case NPCRole.Adventurer:
                AddWeight(weights, CharacterInterestTag.Wander, 30);
                AddWeight(weights, CharacterInterestTag.Inspect, 20);
                break;
            case NPCRole.Priest:
            case NPCRole.Scholar:
                AddWeight(weights, CharacterInterestTag.Rest, 25);
                AddWeight(weights, CharacterInterestTag.Shelter, 20);
                break;
        }
    }

    private static void AddWeight(Dictionary<CharacterInterestTag, int> weights, CharacterInterestTag tag, int amount)
    {
        if (tag == CharacterInterestTag.None || amount == 0)
        {
            return;
        }

        if (weights.ContainsKey(tag))
        {
            weights[tag] += amount;
        }
        else
        {
            weights[tag] = amount;
        }
    }

    private static void LogDecision(Character actor, CharacterDecisionResult result)
    {
        string candidateSummary = result.SelectedCandidate == null
            ? "None"
            : $"{result.SelectedCandidate.SourceName} | Interest={result.SelectedCandidate.MatchingInterest} | Target={result.SelectedCandidate.TargetPosition} | Score={result.SelectedCandidate.Score}";

        MovementAIDiagnosticsLogger.LogEvent("[AI DECISION]", "CharacterDecisionResolver.ResolveWorldDecision",
            $"Profile={result.Profile?.ProfileId ?? "NULL"}\n" +
            $"ProfileSource={result.Profile?.SourceDescription ?? "NULL"}\n" +
            $"Interests={result.Profile?.FormatInterests() ?? "None"}\n" +
            $"CandidateCount={result.CandidateCount}\n" +
            $"SelectedCandidate={candidateSummary}\n" +
            $"DecisionType={result.DecisionType}\n" +
            $"TurnDecisionResult={result.TurnDecisionResult}\n" +
            $"MovementAttempted={result.MovementAttempted}\n" +
            $"MovementSucceeded={result.MovementSucceeded}\n" +
            $"RandomWanderSelected={result.RandomWanderSelected}\n" +
            $"Reason={result.Reason}",
            actor);

        GameDebugger.Instance.LogInfo(
            $"CharacterDecisionResolver: {actor.Name} [{actor.IInteractableID}] profile={result.Profile?.ProfileId ?? "NULL"} " +
            $"decision={result.DecisionType} candidateCount={result.CandidateCount} reason={result.Reason}");
    }
}
