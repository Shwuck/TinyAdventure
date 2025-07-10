using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            IntegrityCheck();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PopulatePreferredTerrains()
    {
        foreach (var race in PermaLists.Instance.Races)
        {
            if (race.HasSubRace)
            {
                foreach (var subRace in race.SubRaces)
                {
                    foreach (var terrain in subRace.PreferredTerrains)
                    {
                        if (!race.PreferredTerrains.Contains(terrain))
                        {
                            race.PreferredTerrains.Add(terrain);
                        }
                    }
                }
            }
            Debug.Log($"RaceManager: Race {race.Name} preferred terrains: {string.Join(", ", race.PreferredTerrains)}");
        }
    }

    public void FilterAndSelectMainRaces(int raceCount)
    {
        UnityEngine.Random.InitState(GameManager.Instance.GameSeed);

        CreateRaceDataDictionary();

        List<RaceData> validRaces = GetValidRaces();
        Debug.Log($"RaceManager: Found {validRaces.Count} valid races.");

        SelectRacesBasedOnRarity(validRaces, raceCount);

        // Log the selected races in a single debug statement
        Debug.Log($"RaceManager: Selected races: {string.Join(", ", PermaLists.Instance.RaceDataDict.Values.Where(r => r.IsSelected).Select(r => r.Name))}");
    }

    private void CreateRaceDataDictionary()
    {
        IntegrityCheck();

        var raceDataDict = new Dictionary<string, RaceData>();

        foreach (var race in PermaLists.Instance.Races)
        {
            if (!raceDataDict.ContainsKey(race.Name))
            {
                raceDataDict[race.Name] = new RaceData
                {
                    Name = race.Name,
                    IsSubrace = false,
                    MainRace = race.Name,
                    VillageType = race.Village,
                    Terrains = race.PreferredTerrains,
                    Rarity = race.Rarity,
                    IsValid = false,
                    IsSelected = false,
                    CountOfVillages = 0
                };
                Debug.Log($"RaceManager: Added main race {race.Name} to dictionary.");
            }

            if (race.HasSubRace)
            {
                foreach (var subRace in race.SubRaces)
                {
                    if (!raceDataDict.ContainsKey(subRace.Name))
                    {
                        raceDataDict[subRace.Name] = new RaceData
                        {
                            Name = subRace.Name,
                            IsSubrace = true,
                            MainRace = race.Name,
                            VillageType = subRace.Village,
                            Terrains = subRace.PreferredTerrains,
                            Rarity = subRace.Rarity,
                            IsValid = false,
                            IsSelected = false,
                            CountOfVillages = 0
                        };
                        Debug.Log($"RaceManager: Added subrace {subRace.Name} of {race.Name} to dictionary.");
                    }
                }
            }
        }

        PermaLists.Instance.RaceDataDict = raceDataDict;
    }

    private List<RaceData> GetValidRaces()
    {
        foreach (var raceData in PermaLists.Instance.RaceDataDict.Values)
        {
            raceData.IsValid = raceData.Terrains.Any(IsTerrainTypePresent);
            if (raceData.IsValid)
            {
                Debug.Log($"RaceManager: Race {raceData.Name} is valid.");
            }
            else
            {
                Debug.Log($"RaceManager: Race {raceData.Name} is not valid for any preferred terrain.");
            }
        }

        return PermaLists.Instance.RaceDataDict.Values.Where(r => r.IsValid).ToList();
    }

    private void SelectRacesBasedOnRarity(List<RaceData> validRaces, int count)
    {
        Dictionary<string, float> raceAdjustments = validRaces.ToDictionary(r => r.Name, r => 1.0f);

        for (int i = 0; i < count; i++)
        {
            // Recalculate total rarity based on adjustments (inverse rarity: higher value means less common)
            int totalRarity = validRaces.Where(r => !r.IsSelected).Sum(r => Mathf.Max(1, Mathf.FloorToInt(100f / r.Rarity * raceAdjustments[r.Name])));
            Debug.Log($"RaceManager: Selecting race {i + 1} of {count} with total rarity {totalRarity}");

            int roll = UnityEngine.Random.Range(0, totalRarity);
            Debug.Log($"RaceManager: Roll value: {roll} (Total Rarity: {totalRarity})");
            int cumulative = 0;
            RaceData selectedRaceData = null;

            foreach (var raceData in validRaces.Where(r => !r.IsSelected))
            {
                int adjustedRarity = Mathf.Max(1, Mathf.FloorToInt(100f / raceData.Rarity * raceAdjustments[raceData.Name]));
                cumulative += adjustedRarity;
                Debug.Log($"RaceManager: Checking race {raceData.Name} with adjusted rarity {adjustedRarity}, cumulative {cumulative}");

                if (roll < cumulative)
                {
                    selectedRaceData = raceData;
                    selectedRaceData.IsSelected = true; // Mark race as selected
                    Debug.Log($"RaceManager: Selected race {selectedRaceData.Name}");
                    break;
                }
            }

            if (selectedRaceData == null)
            {
                Debug.LogWarning("RaceManager: No race selected, which shouldn't happen. Check for logic errors.");
                // In case no race is selected, we force select a random unselected valid race to avoid infinite loop
                var unselectedRaceData = validRaces.FirstOrDefault(r => !r.IsSelected);
                if (unselectedRaceData != null)
                {
                    unselectedRaceData.IsSelected = true;
                    Debug.Log($"RaceManager: Forced selection of race {unselectedRaceData.Name}");
                }
                else
                {
                    Debug.LogError("RaceManager: Failed to force select a race. No valid unselected races available.");
                }
                continue;
            }

            // Adjust probabilities for subraces of the same main race
            foreach (var raceData in validRaces.Where(r => !r.IsSelected))
            {
                if (selectedRaceData.IsSubrace && raceData.IsSubrace && raceData.MainRace == selectedRaceData.MainRace)
                {
                    raceAdjustments[raceData.Name] *= 0.75f; // Reduce the probability by 25% for subraces
                    Debug.Log($"RaceManager: Adjusted subrace {raceData.Name} probability to {raceAdjustments[raceData.Name]}");
                }
                else if (!selectedRaceData.IsSubrace && raceData.IsSubrace && raceData.MainRace == selectedRaceData.Name)
                {
                    raceAdjustments[raceData.Name] *= 0.75f; // Reduce the probability by 25% for subraces
                    Debug.Log($"RaceManager: Adjusted subrace {raceData.Name} probability to {raceAdjustments[raceData.Name]}");
                }
            }
        }

        if (validRaces.Count(r => r.IsSelected) < count)
        {
            Debug.LogError($"RaceManager: Only selected {validRaces.Count(r => r.IsSelected)} races out of the required {count}.");
        }
    }

    private bool IsTerrainTypePresent(TerrainType terrainType)
    {
        Debug.Log($"RaceManager: Checking if terrain type {terrainType} is present in map cells.");
        var mapGenerator = FindObjectOfType<MapGenerator>();
        bool isPresent = mapGenerator.allCells != null && mapGenerator.allCells.Any(cell => cell.Terrain == terrainType);
        Debug.Log($"RaceManager: Terrain type {terrainType} is {(isPresent ? "present" : "not present")}.");
        return isPresent;
    }

    private void IntegrityCheck()
    {
        foreach (var race in PermaLists.Instance.Races)
        {
            if (race.HasSubRace && race.SubRaces != null)
            {
                foreach (var subRace in race.SubRaces)
                {
                    if (string.IsNullOrEmpty(subRace.MainRaceName))
                    {
                        subRace.MainRaceName = race.Name;
                        Debug.LogWarning($"RaceManager: SubRace {subRace.Name} did not have a MainRaceName. It has been set to {race.Name}");
                    }
                }
            }
        }
        Debug.Log("RaceManager: Integrity check completed.");
    }
}

public class RaceData
{
    public string Name { get; set; }
    public bool IsSubrace { get; set; }
    public string MainRace { get; set; }
    public VillageType VillageType { get; set; } 
    public List<TerrainType> Terrains { get; set; }
    public int Rarity { get; set; }
    public bool IsValid { get; set; }
    public bool IsSelected { get; set; }
    public int CountOfVillages { get; set; }
    public string BodyType { get; set; }

    // Ensure ExpansionModifiers exists
    public Dictionary<TerrainType, float> ExpansionModifiers { get; set; } = new Dictionary<TerrainType, float>();

    public float GetExpansionModifier(TerrainType terrain)
    {
        return ExpansionModifiers.TryGetValue(terrain, out float modifier) ? modifier : 1.5f;
    }
}
