using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

public class CivilisationManager : MonoBehaviour
{
    public static CivilisationManager Instance { get; private set; }

    public List<Village> Villages { get; private set; } = new List<Village>();

    public int desiredNPCs;
    public CivilisationGenerator civilisationGenerator;
    public NPCGenerator npcGenerator; // Reference to the NPCGenerator
    public bool IsNew = true;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void GenerateCivilisationsAtMapStart()
    {
        civilisationGenerator.GenerateCivilisations();
    }

    public void UpdateAllVillages()
    {
        var villagesCopy = new List<Village>(Villages);

        foreach (var village in villagesCopy)
        {
            UpdateVillage(village);
        }

        UpdateAllNPCs();
        RandomizeVillageNPCs();
    }

    public void UpdateVillage(Village village)
    {
        var villageData = GetVillageCreationData(village.VillageType);

        if (villageData == null)
        {
            Debug.LogError($"No village data found for village type {village.VillageType}");
            return;
        }

        village.RefreshFulfilledRoles(villageData.NPCRoles);

        foreach (var role in villageData.NPCRoles)
        {
            if (!village.FulfilledRoles.TryGetValue(role, out bool isFulfilled))
            {
                Debug.LogError($"Role {role} not found in FulfilledRoles for village {village.VillageName}");
                continue;
            }

            if (!isFulfilled)
            {
                if (Enum.TryParse(role, true, out NPCRole missingRole))
                {
                    Debug.Log($"Generating NPC with role {missingRole} for village {village.VillageName}");
                    var newNPC = npcGenerator.GenerateNPCWithRole(village, missingRole);
                    if (newNPC != null)
                    {
                        village.AddNPC(newNPC);
                        village.FulfilledRoles[role] = true;
                    }
                }
                else
                {
                    Debug.LogError($"Invalid NPCRole: {role}");
                }
            }
        }

        if (ShouldGenerateNewNPCs(village))
        {
            int npcsToGenerate = CalculateNPCsToGenerate(village);
            if (npcsToGenerate > 0)
            {
                Debug.Log($"Generating {npcsToGenerate} Villager NPCs for village {village.VillageName}");
                var newNPCs = npcGenerator.GenerateNPCs(npcsToGenerate, village, NPCRole.Villager);
                if (newNPCs != null)
                {
                    foreach (var npc in newNPCs)
                    {
                        village.AddNPC(npc);
                    }
                }
            }
        }

        UpdateVillageNeededResources(village);
        NewsManager.Instance.GenerateNewsForVillage(village);
    }

    public void CreateAndAddVillage(VillageType villageType)
    {
        Village newVillage = new Village
        {
            VillageType = villageType,
            VillageName = "Village" + (Villages.Count + 1)
        };
        AddVillage(newVillage);
    }

    public VillageCreationData GetVillageCreationData(VillageType villageType)
    {
        return PermaLists.Instance.VillageCreationData.FirstOrDefault(v => v.VillageType == villageType);
    }

    public void UpdateVillageNeededResources(Village village)
    {
        UnityEngine.Random.InitState(GameManager.Instance.GameSeed);

        var stats = village.Stats;

        Dictionary<string, int> statValues = new Dictionary<string, int>
        {
            { "Food", stats.StoredFood },
            { "Water", stats.StoredWater },
            { "Wood", stats.StoredWood },
            { "Stone", stats.StoredStone }
        };

        var lowestStat = statValues.OrderBy(stat => stat.Value).First().Key;

        switch (lowestStat)
        {
            case "Food":
                ItemType[] foodOptions = { ItemType.Fruit, ItemType.Vegetable, ItemType.Meat };
                stats.NeededResource = foodOptions[UnityEngine.Random.Range(0, foodOptions.Length)];
                break;
            case "Water":
                stats.NeededResource = ItemType.Water;
                break;
            case "Wood":
            case "Stone":
                stats.NeededResource = ItemType.CraftingMaterial;
                break;
            default:
                throw new System.Exception("Unhandled stat type");
        }
    }

    public void AddVillage(Village village)
    {
        if (!Villages.Contains(village))
        {
            Villages.Add(village);
            Debug.Log($"Added new village: {village.VillageName}");
        }
    }

    public void RemoveVillage(Village village)
    {
        if (Villages.Contains(village))
        {
            Villages.Remove(village);
            Debug.Log($"Removed village: {village.VillageName}");
        }
    }

    public void RandomizeVillageNPCs()
    {
        foreach (var village in Villages)
        {
            village.AvailableVillageNPCs.Clear();

            int numberOfNPCs = UnityEngine.Random.Range(6, 10);

            // Include currentDay in the random seed to ensure variability
            int seed = GameManager.Instance.GameSeed + TimeManager.Instance.currentDay;
            UnityEngine.Random.InitState(seed);

            List<NPC> shuffledNPCs = village.VillageNPCs.OrderBy(npc => UnityEngine.Random.value).ToList();

            int totalNPCs = village.VillageNPCs.Count;
            int roleNPCs = village.VillageNPCs.Count(npc => npc.Role != NPCRole.Villager);

            Debug.Log($"{village.VillageName} has {totalNPCs} NPCs, {roleNPCs} of which have roles.");

            // Randomly select NPCs
            village.AvailableVillageNPCs.AddRange(shuffledNPCs.Take(numberOfNPCs));

            // Ensure at least one NPC with a role other than Villager
            bool hasNonVillager = village.AvailableVillageNPCs.Any(npc => npc.Role != NPCRole.Villager);

            if (!hasNonVillager)
            {
                var nonVillagerNPC = shuffledNPCs.FirstOrDefault(npc => npc.Role != NPCRole.Villager);
                if (nonVillagerNPC != null)
                {
                    village.AvailableVillageNPCs.Add(nonVillagerNPC);
                    Debug.Log($"Included NPC with role: {nonVillagerNPC.Role}");
                }
            }

            // Ensure the Mayor is always included if present
            var mayor = village.VillageNPCs.FirstOrDefault(npc => npc.Role == NPCRole.Mayor);
            if (mayor != null && !village.AvailableVillageNPCs.Contains(mayor))
            {
                village.AvailableVillageNPCs.Add(mayor);
                Debug.Log($"Included Mayor: {mayor.Name}");
            }

            // Update Status for each selected NPC based on provided conditions
            foreach (var npc in village.AvailableVillageNPCs)
            {
                if (npc.Stance != NPCStance.Hostile)
                {
                    if (npc.Role != NPCRole.Villager)
                    {
                        npc.Status = NPCStatus.TrueIdle;
                        npc.Stance = NPCStance.TrueIdle;
                    }
                    else
                    {
                        npc.Status = NPCStatus.Idling;
                    }
                }
                else
                {
                    npc.Status = NPCStatus.Hostile;
                }
            }

            Debug.Log($"Selected {village.AvailableVillageNPCs.Count} NPCs to be AvailableNPCs in {village.VillageName}.");
        }
    }

    private bool ShouldGenerateNewNPCs(Village village)
    {
        int minimumNPCs = Mathf.Max(20, Mathf.RoundToInt(village.Stats.Population * 0.1f));

        if (village.VillageNPCs.Count < minimumNPCs)
        {
            Debug.Log($"Village {village.VillageName} needs more NPCs. Current: {village.VillageNPCs.Count}, Minimum: {minimumNPCs}");
            return true;
        }

        return false;
    }

    private int CalculateNPCsToGenerate(Village village)
    {
        int minimumNPCs = Mathf.Max(20, Mathf.RoundToInt(village.Stats.Population * 0.1f));
        int npcsToGenerate = minimumNPCs - village.VillageNPCs.Count;

        return Mathf.Max(0, npcsToGenerate);
    }

    public void UpdateNewsForAllVillages()
    {
        foreach (var village in Villages)
        {
            NewsManager.Instance.GenerateNewsForVillage(village);
            Debug.Log($"News updated for village: {village.VillageName}");
        }
    }

    // This is the new method to update all NPCs
    public void UpdateAllNPCs()
    {
        foreach (var village in Villages)
        {
            // Loop through all NPCs in the current village
            foreach (var npc in village.VillageNPCs)
            {
                // Set NPC's RegionNumber to the village's RegionNumber
                npc.RegionNumber = village.RegionNumber;
            }
        }
    }

}
