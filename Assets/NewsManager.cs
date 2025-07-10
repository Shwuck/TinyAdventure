using System.Collections.Generic;
using UnityEngine;

public class NewsManager : MonoBehaviour
{
    public static NewsManager Instance { get; private set; }

    private Dictionary<Village, List<NewsEntry>> villageNewsDictionary = new Dictionary<Village, List<NewsEntry>>();

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

    // Method to generate news for a specific village
    public void GenerateNewsForVillage(Village village)
    {
        if (!villageNewsDictionary.ContainsKey(village))
        {
            villageNewsDictionary[village] = new List<NewsEntry>();
        }

        int dayGenerated = TimeManager.Instance.currentDay;
        var newsFeed = villageNewsDictionary[village];
        newsFeed.Clear(); // Clear old news for this village

        // Generate Local News
        GenerateLocalNews(village, newsFeed, dayGenerated);

        // Generate Regional News
        GenerateRegionalNews(village, newsFeed, dayGenerated);

        // Generate Worldwide News
        GenerateWorldwideNews(village, newsFeed, dayGenerated);

        DisplayNews(newsFeed); // Display or store the news as needed
    }

    private void GenerateLocalNews(Village village, List<NewsEntry> newsFeed, int dayGenerated)
    {
        if (village == null)
        {
            Debug.LogError("Village is null in GenerateLocalNews.");
            return;
        }

        if (village.Stats == null)
        {
            Debug.LogError($"Village Stats is null for village {village.VillageName}.");
            return;
        }

        if (village.VillageNPCs == null)
        {
            Debug.LogError($"VillageNPCs is null for village {village.VillageName}.");
            return;
        }

        // Example: Local news for population growth
        if (village.Stats.Population > 10000)
        {
            newsFeed.Add(new NewsEntry("Village Grows", $"The population of {village.VillageName} has exceeded 10000.", dayGenerated, NewsType.Local));
        }

        foreach (var npc in village.VillageNPCs)
        {
            // Safely skip if the CurrentNeed is null or HasNeed is false
            if (npc.CurrentNeed == null)
            {
                continue; // Skip this NPC if CurrentNeed is null
            }

            if (!npc.CurrentNeed.HasNeed)
            {
                continue; // Skip this NPC if they don't have an active need
            }

            // If the NPC has a need, add it to the newsFeed
            string needDescription = $"{npc.Name} needs {npc.CurrentNeed.NumberRequired} {npc.CurrentNeed.ItemName}";
            newsFeed.Add(new NewsEntry("Local Need", needDescription, dayGenerated, NewsType.Local));
        }
    }



    private void GenerateRegionalNews(Village village, List<NewsEntry> newsFeed, int dayGenerated)
    {
        foreach (var cell in MapGenerator.Instance.allCells)
        {
            // Check for dungeons in the region
            if (cell.HasDungeon && !cell.HasVisited && IsCellInRegion(cell, village.Location))
            {
                newsFeed.Add(new NewsEntry("Nearby Dungeon", $"A dungeon has been discovered near {village.VillageName}.", dayGenerated, NewsType.Regional));
            }

            // Check for landmarks in the region
            if (cell.HasLandmark && !cell.HasVisited && IsCellInRegion(cell, village.Location))
            {
                newsFeed.Add(new NewsEntry("Nearby Landmark", $"A landmark has been discovered near {village.VillageName}.", dayGenerated, NewsType.Regional));
            }

            // Check for landmarks in the region
            if (cell.HasCave && !cell.HasVisited && IsCellInRegion(cell, village.Location))
            {
                newsFeed.Add(new NewsEntry("Nearby Cave", $"A cave has been discovered near {village.VillageName}.", dayGenerated, NewsType.Regional));
            }
        }
    }


    private void GenerateWorldwideNews(Village village, List<NewsEntry> newsFeed, int dayGenerated)
    {
        int currentRegionNumber = village.Location.RegionNumber;
        RegionInfo currentRegionInfo = RegionManager.Instance.GetRegionInfo(currentRegionNumber);

        foreach (var regionInfo in PermaLists.Instance.RegionInfoDictionary.Values)
        {
            if (regionInfo.RegionNumber != currentRegionNumber)
            {
                // Add news about dungeons in other regions
                if (regionInfo.DungeonCount > 0)
                {
                    string compassDirection = GetCompassDirectionDescription(currentRegionInfo.CompassDirection, regionInfo.CompassDirection);
                    newsFeed.Add(new NewsEntry("Dungeon Rumor", $"Rumor has it there is a dungeon in the {compassDirection}.", dayGenerated, NewsType.Worldwide));
                }

                // Add news about landmarks in other regions
                if (regionInfo.LandmarkCount > 0)
                {
                    string compassDirection = GetCompassDirectionDescription(currentRegionInfo.CompassDirection, regionInfo.CompassDirection);
                    newsFeed.Add(new NewsEntry("Landmark Rumor", $"Word has spread about a landmark in the {compassDirection}.", dayGenerated, NewsType.Worldwide));
                }
            }
        }
    }

    private string GetCompassDirectionDescription(CompassDirection currentDirection, CompassDirection targetDirection)
    {
        // If the target region is in the center or no specific direction, adjust accordingly
        if (targetDirection == CompassDirection.None || targetDirection == CompassDirection.Centre)
        {
            return "Centre";
        }

        // Return the compass direction of the target region relative to the current region
        return targetDirection.ToString();
    }


    private bool IsCellInRegion(Cell cell, Cell villageLocation)
    {
        // Example logic to determine if a cell is in the same region as the village
        return Vector2Int.Distance(cell.Coordinates, villageLocation.Coordinates) <= 10; // Adjust distance threshold as needed
    }

    public void DisplayNews(List<NewsEntry> newsFeed)
    {
        if (newsFeed.Count > 0)
        {
            foreach (var news in newsFeed)
            {
                Debug.Log(news.ToString()); // Replace with your method of displaying news to the player
            }
        }
        else
        {
            Debug.Log("No new news at this time.");
        }
    }

    public List<NewsEntry> GetVillageNews(Village village)
    {
        if (villageNewsDictionary.ContainsKey(village))
        {
            return new List<NewsEntry>(villageNewsDictionary[village]);
        }
        return new List<NewsEntry>();
    }


}

public enum NewsType
{
    Local,
    Regional,
    Worldwide
}

public class NewsEntry
{
    public string Title { get; set; }
    public string Content { get; set; }
    public int DayGenerated { get; set; }
    public NewsType NewsType { get; set; }  // Add this property

    public NewsEntry(string title, string content, int dayGenerated, NewsType newsType)
    {
        Title = title;
        Content = content;
        DayGenerated = dayGenerated;
        NewsType = newsType;  // Initialize the NewsType property
    }

    public override string ToString()
    {
        return $"{Title} ({DayGenerated}): {Content}";
    }
}
