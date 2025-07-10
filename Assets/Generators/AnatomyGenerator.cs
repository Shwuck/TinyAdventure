using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AnatomyGenerator : MonoBehaviour
{
    public static AnatomyGenerator Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// Generates an Anatomy instance strictly following the given BodyType structure.
    public Anatomy GenerateAnatomy(string bodyType)
    {
        Debug.Log($"[AnatomyGenerator] Generating anatomy for BodyType: {bodyType}");

        AnatomyData anatomyData = PermaLists.Instance.AnatomyData.FirstOrDefault(a => a.BodyType == bodyType);
        if (anatomyData == null)
        {
            Debug.LogError($"[AnatomyGenerator] ERROR: Anatomy type '{bodyType}' not found in PermaLists.");
            return null;
        }

        Anatomy anatomy = new Anatomy(bodyType);
        Dictionary<string, List<BodyPart>> createdParts = new Dictionary<string, List<BodyPart>>();
        Dictionary<string, List<BodyPart>> spares = new Dictionary<string, List<BodyPart>>();
        Dictionary<string, int> expectedParts = new Dictionary<string, int>(anatomyData.DefaultParts);

        if (expectedParts.Count == 0)
        {
            Debug.LogError($"[AnatomyGenerator] ERROR: No default parts found for BodyType: {bodyType}");
            return null;
        }

        // Step 1: Get sorted body parts order
        List<string> sortedBodyParts = GetSortedBodyParts(expectedParts);

        // Step 2: Generate all parts in strict order
        GeneratePartsInOrder(sortedBodyParts, expectedParts, createdParts, anatomy, spares);

        // Step 3: Retry for deferred subparts
        int maxAttempts = 3;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (spares.Count == 0) break;
            AssignSparesToParents(createdParts, spares);
        }

        // Step 4: Final validation and reassignment
        ValidateAndRedistribute(createdParts);

        // Step 5: Debug spares and assign
        DebugSpares("BEFORE final assignment", spares);
        AssignSparesToParents(createdParts, spares);
        DebugSpares("AFTER final assignment", spares);

        AssignEquipmentSlots(anatomy);
        ValidateEquipmentSlots(anatomy);

        Debug.Log($"[AnatomyGenerator] Successfully generated anatomy for {bodyType} with {anatomy.BodyParts.Count} categories.");
        return anatomy;
    }

    /// Determines the order in which parts should be created based on dependencies.
    private List<string> GetSortedBodyParts(Dictionary<string, int> expectedParts)
    {
        List<(string uniqueId, string partName, int priority)> sortedParts = new List<(string, string, int)>();

        foreach (var partEntry in expectedParts)
        {
            if (!PermaLists.Instance.BodyPartData.TryGetValue(partEntry.Key, out BodyPartData partData))
            {
                Debug.LogError($"[AnatomyGenerator] ERROR: BodyPart '{partEntry.Key}' is missing from BodyPartData.");
                continue;
            }

            int priority = 0;

            if (partData.BasePart && !partData.HasSubs) priority = 1;  // Torso, Head, Arm, Leg, Hand, Foot
            else if (partData.BasePart && partData.HasSubs) priority = 2;  // Base Parts with subs (Head -> Eyes, Arm -> Hand)
            else if (partData.SubPart && partData.HasSubs) priority = 3;  // Subparts with subs (Hand -> Fingers)
            else if (partData.SubPart && !partData.HasSubs) priority = 4;  // Leaf nodes (Fingers, Eyes)

            for (int i = 0; i < partEntry.Value; i++)  // Create unique IDs for each part
            {
                sortedParts.Add(($"{partEntry.Key}_{i + 1}", partEntry.Key, priority));
            }
        }

        return sortedParts.OrderBy(p => p.priority).Select(p => p.uniqueId).ToList();
    }

    /// Generates all body parts exactly as defined in the sorted order.
    private void GeneratePartsInOrder(List<string> sortedParts, Dictionary<string, int> expectedParts,
        Dictionary<string, List<BodyPart>> createdParts, Anatomy anatomy, Dictionary<string, List<BodyPart>> spares)
    {
        foreach (string uniquePartId in sortedParts)
        {
            string partName = uniquePartId.Split('_')[0];

            if (!expectedParts.TryGetValue(partName, out int count) ||
                !PermaLists.Instance.BodyPartData.TryGetValue(partName, out BodyPartData partData))
            {
                Debug.LogError($"[AnatomyGenerator] ERROR: Issue with '{partName}'");
                continue;
            }

            // Sub-Part Check: Ensure Parent Exists Before Making This
            if (partData.SubPart)
            {
                bool hasValidParent = PermaLists.Instance.BodyPartData.Values.Any(parent =>
                    parent.SubParts?.Any(sp => sp.Name == partName) == true &&
                    createdParts.ContainsKey(parent.Name) &&
                    createdParts[parent.Name].Count > 0
                );

                if (!hasValidParent)
                {
                    Debug.LogWarning($"[AnatomyGenerator] WARNING: Skipping '{partName}' due to missing parent.");
                    continue;
                }
            }

            if (!createdParts.ContainsKey(partName))
            {
                createdParts[partName] = new List<BodyPart>();
            }

            Debug.Log($"[AnatomyGenerator] Creating '{uniquePartId}'");

            BodyPart newPart = ConvertToBodyPart(partData, null, anatomy);
            createdParts[partName].Add(newPart);

            if (partData.BasePart)
            {
                anatomy.AddBodyPart(newPart);
            }
            else
            {
                if (!spares.ContainsKey(partName))
                {
                    spares[partName] = new List<BodyPart>();
                }
                spares[partName].Add(newPart);
            }
        }
    }


    /// Ensures subparts are evenly distributed across valid parents.
    private void ValidateAndRedistribute(Dictionary<string, List<BodyPart>> createdParts)
    {
        foreach (var parentEntry in createdParts)
        {
            string parentType = parentEntry.Key;
            List<BodyPart> parents = parentEntry.Value;

            foreach (BodyPart parent in parents)
            {
                if (!PermaLists.Instance.BodyPartData.TryGetValue(parentType, out BodyPartData parentData) ||
                    parentData.SubParts == null || parentData.SubParts.Count == 0)
                {
                    continue;
                }

                foreach (var subPartData in parentData.SubParts)
                {
                    string subPartType = subPartData.Name;

                    if (!createdParts.ContainsKey(subPartType) || createdParts[subPartType].Count == 0)
                    {
                        continue;
                    }

                    List<BodyPart> availableSubParts = createdParts[subPartType];

                    int parentCount = parents.Count;
                    int subPartCount = availableSubParts.Count;
                    int subPartsPerParent = Math.Max(1, subPartCount / parentCount);

                    foreach (BodyPart subPart in availableSubParts.Take(subPartsPerParent).ToList())
                    {
                        parent.AddSubPart(subPart);
                        subPart.SetParent(parent);
                        availableSubParts.Remove(subPart);
                    }
                }
            }
        }
    }

    /// Converts BodyPartData into a BodyPart instance.
    private BodyPart ConvertToBodyPart(BodyPartData partData, BodyPart parent, Anatomy anatomy)
    {
        if (!Enum.TryParse(partData.Position, out BodyPosition position))
        {
            Debug.LogError($"[AnatomyGenerator] ERROR: Invalid BodyPosition: {partData.Position} for body part {partData.Name}");
            return null;
        }

        // **Handle multiple equipment slots**
        List<EquipmentSlot> equipmentSlots = new List<EquipmentSlot>();
        if (partData.EquipmentSlots != null)
        {
            foreach (var slotName in partData.EquipmentSlots)
            {
                if (Enum.TryParse(slotName, out EquipmentSlot parsedSlot))
                {
                    equipmentSlots.Add(parsedSlot);
                }
            }
        }

        return new BodyPart(partData, parent, equipmentSlots, anatomy);
    }

    /// Logs all spare parts before and after assignment.
    private void DebugSpares(string stage, Dictionary<string, List<BodyPart>> spares)
    {
        if (spares.Count == 0)
        {
            Debug.Log($"[AnatomyGenerator] No spare parts remaining {stage}.");
            return;
        }

        Debug.Log($"[AnatomyGenerator] Spare parts {stage}:");
        foreach (var spare in spares)
        {
            Debug.Log($"  - {spare.Key}: {spare.Value.Count} unassigned");
        }
    }

    /// Attempts to assign spare parts to available parents in a balanced manner.
    private void AssignSparesToParents(Dictionary<string, List<BodyPart>> createdParts, Dictionary<string, List<BodyPart>> spares)
    {
        foreach (var spareEntry in spares.ToList())
        {
            string spareType = spareEntry.Key;

            foreach (var sparePart in spareEntry.Value.ToList())
            {
                bool assigned = false;

                // Find potential parents for this spare part
                var potentialParents = createdParts.Values
                    .SelectMany(list => list)
                    .Where(parent => PermaLists.Instance.BodyPartData.TryGetValue(parent.Name, out BodyPartData parentData) &&
                                     parentData.SubParts?.Any(sp => sp.Name == spareType) == true)
                    .OrderBy(parent => parent.SubParts.Count) // Prioritize parents with fewer sub-parts
                    .ToList();

                if (potentialParents.Count > 0)
                {
                    var selectedParent = potentialParents.First(); // Choose the one with the fewest sub-parts
                    selectedParent.AddSubPart(sparePart);
                    sparePart.SetParent(selectedParent);
                    spares[spareType].Remove(sparePart);
                    assigned = true;

                    Debug.Log($"[AnatomyGenerator] Assigned spare '{spareType}' to '{selectedParent.Name}'");
                }

                if (!assigned)
                {
                    Debug.LogWarning($"[AnatomyGenerator] WARNING: '{spareType}' could not be assigned to any parent.");
                }
            }

            // Remove empty spare lists
            if (spares[spareType].Count == 0)
            {
                spares.Remove(spareType);
            }
        }
    }

    private void AssignEquipmentSlots(Anatomy anatomy)
    {
        List<BodyPart> hands = new List<BodyPart>();
        List<string> checkedParts = new List<string>();

        foreach (var bodyPartList in anatomy.BodyParts.Values)
        {
            foreach (var part in bodyPartList)
            {
                CheckBodyPartAndSubparts(part, hands, checkedParts);
            }
        }

        Debug.Log($"[AnatomyGenerator] Body parts checked: {checkedParts.Count}\n - {string.Join("\n - ", checkedParts)}");
        Debug.Log($"[AnatomyGenerator] Total hands found: {hands.Count}");

        AssignHandEquipmentSlots(hands);
    }

    private void CheckBodyPartAndSubparts(BodyPart part, List<BodyPart> hands, List<string> checkedParts)
    {
        checkedParts.Add($"{part.Name} ({part.BodyPartType})");
        if (string.Equals(part.BodyPartType, "Hand", StringComparison.OrdinalIgnoreCase))
        {
            hands.Add(part);
        }

        foreach (var subPart in part.SubParts)
        {
            CheckBodyPartAndSubparts(subPart, hands, checkedParts);
        }
    }


    private void AssignHandEquipmentSlots(List<BodyPart> hands)
    {
        if (hands == null || hands.Count == 0) return;

        hands = hands.OrderBy(h => h.ParentPart?.Name).ToList();
        bool hasMainHand = false;

        foreach (var hand in hands)
        {
            hand.EquipmentSlots.Clear();

            if (!hasMainHand)
            {
                hand.EquipmentSlots.Add(EquipmentSlot.MainHand);
                hasMainHand = true;
                Debug.Log($"[AnatomyGenerator] Assigned MainHand to '{hand.Name}'");
            }
            else
            {
                hand.EquipmentSlots.Add(EquipmentSlot.OffHand);
                Debug.Log($"[AnatomyGenerator] Assigned OffHand to '{hand.Name}'");
            }
        }
    }

    private void ValidateEquipmentSlots(Anatomy anatomy)
    {
        bool hasDuplicates = false;
        bool hasInvalidHandSlots = false;

        foreach (var bodyPartList in anatomy.BodyParts.Values)
        {
            foreach (var part in bodyPartList)
            {
                var duplicateSlots = part.EquipmentSlots
                    .GroupBy(slot => slot)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToList();

                if (duplicateSlots.Count > 0)
                {
                    hasDuplicates = true;
                    Debug.LogError($"[AnatomyGenerator] ERROR: Duplicate equipment slots found on '{part.Name}': {string.Join(", ", duplicateSlots)}");
                }

                if (part.BodyPartType == "Hand")
                {
                    bool hasMainHand = part.EquipmentSlots.Contains(EquipmentSlot.MainHand);
                    bool hasOffHand = part.EquipmentSlots.Contains(EquipmentSlot.OffHand);

                    if (hasMainHand && hasOffHand)
                    {
                        hasInvalidHandSlots = true;
                        Debug.LogError($"[AnatomyGenerator] ERROR: Hand '{part.Name}' has both MainHand and OffHand slots! A hand should have only one.");
                    }
                }
            }
        }

        if (!hasDuplicates && !hasInvalidHandSlots)
        {
            Debug.Log("[AnatomyGenerator] Equipment slot validation passed: No duplicates or invalid hand slots found.");
        }
    }

}
