using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CivilisationGenerator : MonoBehaviour
{
    private MapGenerator mapGenerator;
    private CivilisationManager civilisationManager;
    public int proximityRadius = 8;
    public int proximityPenalty = 5;

    public void GenerateCivilisations()
    {
        int civilisationCount = GameManager.Instance.CivilisationCount;
        int raceCount = GameManager.Instance.RaceCount;

        // Ensure RaceCount does not exceed CivilisationCount
        if (raceCount > civilisationCount)
        {
            raceCount = civilisationCount;
        }

        mapGenerator = FindObjectOfType<MapGenerator>();
        civilisationManager = CivilisationManager.Instance;

        if (mapGenerator == null || civilisationManager == null)
        {
            Debug.LogError("MapGenerator or CivilisationManager instance not found!");
            return;
        }

        // Use RaceManager to prepare race data
        RaceManager.Instance.PopulatePreferredTerrains();
        RaceManager.Instance.FilterAndSelectMainRaces(raceCount);

        var selectedRacesData = PermaLists.Instance.RaceDataDict.Values.Where(r => r.IsSelected).ToList();

        if (selectedRacesData.Count == 0)
        {
            Debug.LogError("No valid races found.");
            return;
        }

        // Output selected races to PermaLists.Instance.SelectedRaces
        PermaLists.Instance.SelectedRaces.Clear();
        foreach (var raceData in selectedRacesData)
        {
            var race = PermaLists.Instance.Races.FirstOrDefault(r => r.Name == raceData.Name);
            if (race != null)
            {
                PermaLists.Instance.SelectedRaces.Add(race);
            }
            else
            {
                Debug.LogWarning($"Race {raceData.Name} not found in PermaLists.Instance.Races");
            }
        }

        // Debug message with selected races
        string selectedRaceNames = string.Join(", ", selectedRacesData.Select(r => r.Name));
        Debug.Log($"To match RaceCount of {raceCount}, CivilisationGenerator has selected races: {selectedRaceNames}");

        GenerateVillages(civilisationCount);
    }

    private void GenerateVillages(int civilisationCount)
    {
        var selectedRaces = PermaLists.Instance.RaceDataDict.Values.Where(r => r.IsSelected).ToList();
        foreach (var raceData in selectedRaces)
        {
            var villageCreationData = GetVillageCreationDataForRace(raceData);
            if (villageCreationData == null)
            {
                Debug.LogWarning($"No VillageCreationData found for race {raceData.VillageType}");
                continue;
            }

            List<(Vector2Int, int)> scoredCells = ScoreCellsForVillageType(villageCreationData);

            if (scoredCells.Count == 0)
            {
                Debug.LogError("No valid cells available for village placement.");
                return;
            }

            var villageCell = SelectLocation(scoredCells);
            if (villageCell != Vector2Int.zero)
            {
                PlaceVillage(villageCell, raceData, villageCreationData);
                civilisationCount--;
            }
        }
    }

    private VillageCreationData GetVillageCreationDataForRace(RaceData raceData)
    {
        return CivilisationManager.Instance.GetVillageCreationData(raceData.VillageType);
    }

    private List<(Vector2Int, int)> ScoreCellsForVillageType(VillageCreationData villageCreationData)
    {
        List<(Vector2Int, int)> scoredCells = new List<(Vector2Int, int)>();
        List<Cell> villageCells = mapGenerator.GetCellsByTerrain(TerrainType.Village);

        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                Cell currentCell = mapGenerator.map[x, y];

                // Skip cells that are not suitable for village placement
                if (currentCell.HasDungeon || currentCell.HasLandmark || currentCell.HasVillage || currentCell.IsOwned)
                {
                    continue;
                }

                if (currentCell.Terrain != TerrainType.River && currentCell.Terrain != TerrainType.MountainPeak && currentCell.Terrain != TerrainType.Water && currentCell.Terrain != TerrainType.Bridge && !currentCell.hasNestedArea)
                {
                    int score = ScoreCell(x, y, villageCreationData, villageCells);
                    if (score > 0)
                    {
                        scoredCells.Add((new Vector2Int(x, y), score));
                    }
                }
            }
        }

        scoredCells.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return scoredCells;
    }


    private int ScoreCell(int x, int y, VillageCreationData villageCreationData, List<Cell> villageCells)
    {
        int score = 0;
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        // Check adjacent cells for preferred terrain types
        foreach (var dir in directions)
        {
            Vector2Int adjacentPos = new Vector2Int(x, y) + dir;
            if (IsValidPosition(adjacentPos))
            {
                Cell adjacentCell = mapGenerator.map[adjacentPos.x, adjacentPos.y];
                if (villageCreationData.PreferredTerrains.Contains(adjacentCell.Terrain))
                {
                    score += 10; // Arbitrary score for preferred terrain, adjust as needed
                }
            }
        }

        // Penalize for proximity to existing villages
        foreach (var cell in villageCells)
        {
            Vector2Int villagePos = cell.Coordinates;
            float distance = Vector2Int.Distance(new Vector2Int(x, y), villagePos);
            if (distance <= proximityRadius)
            {
                score -= proximityPenalty;
            }
        }

        return score;
    }

    private bool IsValidPosition(Vector2Int position)
    {
        return position.x >= 0 && position.x < mapGenerator.width && position.y >= 0 && position.y < mapGenerator.height;
    }

    private Vector2Int SelectLocation(List<(Vector2Int, int)> scoredCells)
    {
        if (scoredCells.Count == 0)
        {
            Debug.LogWarning("No positive-scored cells found. Placing village randomly.");
            return Vector2Int.zero;
        }

        // Calculate the total weight (sum of all scores)
        int totalWeight = scoredCells.Sum(cell => cell.Item2);

        // Generate a random number between 0 and totalWeight
        int randomWeight = Random.Range(0, totalWeight);

        // Iterate through the scored cells
        foreach (var cell in scoredCells)
        {
            randomWeight -= cell.Item2;
            if (randomWeight <= 0)
            {
                return cell.Item1;
            }
        }

        // Fallback: return the last cell's location in case something goes wrong
        return scoredCells.Last().Item1;
    }

    private void PlaceVillage(Vector2Int location, RaceData raceData, VillageCreationData villageCreationData)
    {
        // Get or create the faction for this race
        Faction faction = FactionManager.Instance.GetFactionForRace(raceData);
        if (faction == null)
        {
            faction = FactionManager.Instance.CreateFaction(raceData);
        }

        Debug.Log($"Placing village for race {raceData.Name} at {location}");

        // Set the terrain type based on the village creation data
        mapGenerator.map[location.x, location.y].PreviousTerrain = mapGenerator.map[location.x, location.y].Terrain;
        mapGenerator.map[location.x, location.y].Terrain = TerrainType.Village;

        // Set the village type terrain
        var villageTypeTerrain = GetVillageTypeTerrain(villageCreationData.VillageType);
        mapGenerator.map[location.x, location.y].VillageTypeTerrain = villageTypeTerrain;
        mapGenerator.map[location.x, location.y].TerrainToDisplay = villageTypeTerrain;

        // Calculate population based on PrestigeLevel
        int prestigeLevel = villageCreationData.PrestigeLevel;

        if (prestigeLevel <= 0)
        {
            prestigeLevel = 1;
        }
        else if (prestigeLevel >= 11)
        {
            prestigeLevel = 10;
        }

        int population = (prestigeLevel + 1) * 100;

        // Create a new village and assign it to the faction
        Village newVillage = new Village(faction, raceData.VillageType)
        {
            Location = mapGenerator.map[location.x, location.y],
            VillageName = $"Village{GameManager.Instance.GetNextVillageCounter()}"
        };

        newVillage.Name = newVillage.VillageName;
        newVillage.RegionNumber = mapGenerator.map[location.x, location.y].RegionNumber;
        // Initialize village stats with the calculated population and prestige level
        newVillage.InitializeVillageStats(newVillage.VillageName, population, prestigeLevel);

        mapGenerator.map[location.x, location.y].SetNestedArea(newVillage);
        mapGenerator.map[location.x, location.y].Terrain = TerrainType.Village;

        // Add the village to the faction using the new method
        FactionManager.Instance.AddVillageToFaction(faction, newVillage);

        mapGenerator.map[location.x, location.y].IsOwned = true;
        mapGenerator.map[location.x, location.y].OwnedBy = newVillage.VillageName;
        mapGenerator.map[location.x, location.y].OwnedByFaction = faction;
        mapGenerator.map[location.x, location.y].HasVillage = true;
        mapGenerator.map[location.x, location.y].Village = newVillage;

        civilisationManager.AddVillage(newVillage);
        Debug.Log($"Placed a {raceData.VillageType} village for {raceData.Name} under faction {faction.FactionName} at {location}");
    }


    private TerrainType GetVillageTypeTerrain(VillageType villageType)
    {
        switch (villageType)
        {
            case VillageType.HumanVillage:
                return TerrainType.Village;
            case VillageType.DwarvenHall:
                return TerrainType.Hall;
            case VillageType.ElvenGrove:
                return TerrainType.Grove;
            case VillageType.SabrenCamp:
                return TerrainType.Camp;
            default:
                Debug.LogWarning($"Unknown VillageType: {villageType}");
                return TerrainType.Village; // Default case, or handle as appropriate
        }
    }
}
