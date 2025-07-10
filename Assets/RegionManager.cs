using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class RegionManager : MonoBehaviour
{
    public static RegionManager Instance { get; private set; }


    private int TimesCalled = 0;

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

    // New method to clear and repopulate region info at the start of the game
    public void PopulateRegionInfoAtGameStart()
    {
        // Clear the current region info to prevent duplicates
        if (PermaLists.Instance.RegionInfoDictionary != null)
        {
            PermaLists.Instance.RegionInfoDictionary.Clear();
        }
        else
        {
            // Initialize the dictionary if it's null (first time use)
            PermaLists.Instance.RegionInfoDictionary = new Dictionary<int, RegionInfo>();
        }

        // Populate the dictionary with the latest region info
        PopulateRegionInfo();
    }

    public void UpdateRegionInfo()
    {
        // Increment the call counter
        TimesCalled++;
        Debug.Log($"UpdateRegionInfo called in RegionManager. TimesCalled: {TimesCalled}");

        // Reset the RegionInfoDictionary in PermaLists
        PermaLists.Instance.RegionInfoDictionary = new Dictionary<int, RegionInfo>();

        // Populate the dictionary with the latest region info
        PopulateRegionInfo();
    }

    private void PopulateRegionInfo()
    {
        var regionInfoDictionary = PermaLists.Instance.RegionInfoDictionary;
        int regionSize = 25; // Assuming each region is 25x25 cells

        int totalRegionsX = MapGenerator.Instance.width / regionSize;
        int totalRegionsY = MapGenerator.Instance.height / regionSize;

        foreach (Cell cell in MapGenerator.Instance.allCells)
        {
            if (!regionInfoDictionary.ContainsKey(cell.RegionNumber))
            {
                regionInfoDictionary[cell.RegionNumber] = new RegionInfo(cell.RegionNumber);
            }

            var regionInfo = regionInfoDictionary[cell.RegionNumber];

            // Increment the cell count for the region
            regionInfo.IncrementCellCount();

            // Count terrain types
            regionInfo.IncrementTerrainTypeCount(cell.Terrain);

            // Count dungeons
            if (cell.HasDungeon)
            {
                regionInfo.IncrementDungeonCount();
            }

            // Count landmarks
            if (cell.HasLandmark)
            {
                regionInfo.IncrementLandmarkCount();
            }

            // Count villages
            if (cell.HasVillage)
            {
                regionInfo.IncrementVillageCount();
            }

            // Count owned cells
            if (cell.IsOwned)
            {
                regionInfo.IncrementOwnedCellCount();
            }
        }

        // Assign compass directions and set native fruits and vegetables
        foreach (var regionInfo in regionInfoDictionary.Values)
        {
            AssignCompassDirection(regionInfo, totalRegionsX, totalRegionsY);
        }
    }

    private void AssignCompassDirection(RegionInfo regionInfo, int totalRegionsX, int totalRegionsY)
    {
        int regionX = regionInfo.RegionNumber % totalRegionsX; // Horizontal index of the region
        int regionY = regionInfo.RegionNumber / totalRegionsX; // Vertical index of the region

        // Determine compass direction based on regionX and regionY
        if (regionY == 0) // Top row
        {
            if (regionX == 0)
                regionInfo.CompassDirection = CompassDirection.NorthWest;
            else if (regionX == totalRegionsX - 1)
                regionInfo.CompassDirection = CompassDirection.NorthEast;
            else
                regionInfo.CompassDirection = CompassDirection.North;
        }
        else if (regionY == totalRegionsY - 1) // Bottom row
        {
            if (regionX == 0)
                regionInfo.CompassDirection = CompassDirection.SouthWest;
            else if (regionX == totalRegionsX - 1)
                regionInfo.CompassDirection = CompassDirection.SouthEast;
            else
                regionInfo.CompassDirection = CompassDirection.South;
        }
        else // Middle rows
        {
            if (regionX == 0)
                regionInfo.CompassDirection = CompassDirection.West;
            else if (regionX == totalRegionsX - 1)
                regionInfo.CompassDirection = CompassDirection.East;
            else
                regionInfo.CompassDirection = CompassDirection.Centre; // Middle region
        }
    }

    // Set difficulty levels based on the starting direction
    public void SetCharacterLevelsBasedOnStart(CompassDirection startDirection)
    {
        // Get the regions in the adjacent edges (level 1)
        List<CompassDirection> adjacentEdges = GetAdjacentDirections(startDirection);

        // Get the opposite direction (level 3)
        CompassDirection oppositeEdge = GetOppositeCompassDirection(startDirection);

        // Get the regions in the starting direction and set them as PlayerStartRegion
        var startRegions = GetRegionsByDirection(startDirection);
        foreach (var region in startRegions)
        {
            region.CharacterLevel = 1; // Set the start region level to 1
            region.PlayerStartRegion = true; // Mark as player's starting region
        }

        // Set adjacent edges to level 1
        foreach (var direction in adjacentEdges)
        {
            var regions = GetRegionsByDirection(direction);
            foreach (var region in regions)
            {
                region.CharacterLevel = 1;
            }
        }

        // Set the opposite edge to level 3
        var oppositeRegions = GetRegionsByDirection(oppositeEdge);
        foreach (var region in oppositeRegions)
        {
            region.CharacterLevel = 3;
        }

        // Set middle ground regions (Centre) to level 2
        var middleRegions = GetRegionsByDirection(CompassDirection.Centre);
        foreach (var region in middleRegions)
        {
            region.CharacterLevel = 2;
        }
    }

    public void SetupRegionsBasedOnStartCell(Cell startCell)
    {
        if (startCell == null)
        {
            Debug.LogError("Start cell is null. Cannot set up regions.");
            return;
        }

        // Get the RegionInfo based on the startCell's region number
        RegionInfo startRegionInfo = GetRegionInfo(startCell.RegionNumber);

        if (startRegionInfo != null)
        {
            // Use the region's CompassDirection to set up the edges
            SetCharacterLevelsBasedOnStart(startRegionInfo.CompassDirection);

            Debug.Log($"Regions set up based on player start cell: Region {startCell.RegionNumber}, Direction: {startRegionInfo.CompassDirection}");
        }
        else
        {
            Debug.LogError($"RegionInfo not found for region number: {startCell.RegionNumber}");
        }
    }


    // Randomise player starting direction (edges only, not Centre)
    public CompassDirection GetRandomEdgeDirection()
    {
        List<CompassDirection> edgeDirections = new List<CompassDirection>
        {
            CompassDirection.North,
            CompassDirection.East,
            CompassDirection.South,
            CompassDirection.West,
            CompassDirection.NorthEast,
            CompassDirection.NorthWest,
            CompassDirection.SouthEast,
            CompassDirection.SouthWest
        };

        return edgeDirections[Random.Range(0, edgeDirections.Count)];
    }

    // Get the opposite compass direction based on the current direction
    public CompassDirection GetOppositeCompassDirection(CompassDirection direction)
    {
        switch (direction)
        {
            case CompassDirection.North: return CompassDirection.South;
            case CompassDirection.South: return CompassDirection.North;
            case CompassDirection.East: return CompassDirection.West;
            case CompassDirection.West: return CompassDirection.East;
            case CompassDirection.NorthEast: return CompassDirection.SouthWest;
            case CompassDirection.SouthWest: return CompassDirection.NorthEast;
            case CompassDirection.SouthEast: return CompassDirection.NorthWest;
            case CompassDirection.NorthWest: return CompassDirection.SouthEast;
            default: return CompassDirection.None;
        }
    }

    // Get the regions adjacent to a given direction
    public List<CompassDirection> GetAdjacentDirections(CompassDirection direction)
    {
        switch (direction)
        {
            case CompassDirection.North:
                return new List<CompassDirection> { CompassDirection.NorthWest, CompassDirection.North, CompassDirection.NorthEast };
            case CompassDirection.South:
                return new List<CompassDirection> { CompassDirection.SouthWest, CompassDirection.South, CompassDirection.SouthEast };
            case CompassDirection.East:
                return new List<CompassDirection> { CompassDirection.NorthEast, CompassDirection.East, CompassDirection.SouthEast };
            case CompassDirection.West:
                return new List<CompassDirection> { CompassDirection.NorthWest, CompassDirection.West, CompassDirection.SouthWest };
            case CompassDirection.NorthEast:
                return new List<CompassDirection> { CompassDirection.North, CompassDirection.NorthEast, CompassDirection.East };
            case CompassDirection.NorthWest:
                return new List<CompassDirection> { CompassDirection.North, CompassDirection.NorthWest, CompassDirection.West };
            case CompassDirection.SouthEast:
                return new List<CompassDirection> { CompassDirection.South, CompassDirection.SouthEast, CompassDirection.East };
            case CompassDirection.SouthWest:
                return new List<CompassDirection> { CompassDirection.South, CompassDirection.SouthWest, CompassDirection.West };
            case CompassDirection.Centre:
                return new List<CompassDirection> { CompassDirection.North, CompassDirection.South, CompassDirection.East, CompassDirection.West };
            default:
                return new List<CompassDirection>();
        }
    }

    // Get a random fruit or vegetable from a region
    public string GetNativeFruitOrVegetable(RegionInfo region, ItemType itemType)
    {
        if (itemType == ItemType.Fruit)
        {
            var possibleFruits = new List<string>
            {
                region.NativeTreeFruit,
                region.NativeVineFruit,
                region.NativeBushFruit
            }.Where(fruit => !string.IsNullOrEmpty(fruit)).ToList();

            if (possibleFruits.Count > 0)
            {
                return possibleFruits[Random.Range(0, possibleFruits.Count)];
            }
        }
        else if (itemType == ItemType.Vegetable && !string.IsNullOrEmpty(region.NativeVegetable))
        {
            return region.NativeVegetable;
        }

        return null; // No fruit or vegetable found
    }

    public RegionInfo GetRegionInfo(int regionNumber)
    {
        var regionInfoDictionary = PermaLists.Instance.RegionInfoDictionary;

        if (regionInfoDictionary.ContainsKey(regionNumber))
        {
            return regionInfoDictionary[regionNumber];
        }
        else
        {
            Debug.LogWarning($"RegionInfo for RegionNumber {regionNumber} not found.");
            return null;
        }
    }

    public List<RegionInfo> GetRegionsByDirection(CompassDirection direction)
    {
        return PermaLists.Instance.RegionInfoDictionary.Values
            .Where(regionInfo => regionInfo.CompassDirection == direction)
            .ToList();
    }

    public string FindNativePlant(string plantType, int regionNumber)
    {
        // Get the RegionInfo for the specified region
        RegionInfo regionInfo = GetRegionInfo(regionNumber);

        if (regionInfo == null)
        {
            Debug.LogWarning($"Region {regionNumber} not found. Cannot find native plant.");
            return null;  // If region is not found, return null
        }

        // Check plant type and return the corresponding native plant
        switch (plantType.ToLower())
        {
            case "tree":
                return regionInfo.NativeTreeFruit;

            case "vine":
                return regionInfo.NativeVineFruit;

            case "bush":
                return regionInfo.NativeBushFruit;

            case "vegetable":
                return regionInfo.NativeVegetable;

            case "fungi":
                return regionInfo.NativeFungi;

            default:
                Debug.LogWarning($"Unknown plant type '{plantType}'. Please use 'tree', 'vine', 'bush', 'vegetable', or 'fungi'.");
                return null;  // If the plant type is unknown, return null
        }
    }
}

public class RegionInfo
{
    public int RegionNumber { get; private set; }
    public int CellCount { get; private set; }
    public Dictionary<TerrainType, int> TerrainTypeCounts { get; private set; }
    public CompassDirection CompassDirection { get; set; }
    public int DungeonCount { get; private set; }
    public int LandmarkCount { get; private set; }
    public int VillageCount { get; private set; }
    public int OwnedCellCount { get; private set; }
    public int CharacterLevel { get; set; }  // Difficulty level
    public bool PlayerStartRegion { get; set; }  // Mark the player's starting region

    public string NativeTreeFruit { get; set; }
    public string NativeVineFruit { get; set; }
    public string NativeBushFruit { get; set; }
    public string NativeVegetable { get; set; }
    public string NativeFungi { get; set; }

    public RegionInfo(int regionNumber)
    {
        RegionNumber = regionNumber;
        CellCount = 0;
        TerrainTypeCounts = new Dictionary<TerrainType, int>();
        DungeonCount = 0;
        LandmarkCount = 0;
        VillageCount = 0;
        OwnedCellCount = 0;
        CharacterLevel = 0;  // Initialize CharacterLevel to 0
        PlayerStartRegion = false;  // Initialize PlayerStartRegion to false
        NativeVegetable = null;
    }

    public void IncrementCellCount()
    {
        CellCount++;
    }

    public void IncrementTerrainTypeCount(TerrainType terrainType)
    {
        if (!TerrainTypeCounts.ContainsKey(terrainType))
        {
            TerrainTypeCounts[terrainType] = 0;
        }
        TerrainTypeCounts[terrainType]++;
    }

    public void IncrementDungeonCount() => DungeonCount++;
    public void IncrementLandmarkCount() => LandmarkCount++;
    public void IncrementVillageCount() => VillageCount++;
    public void IncrementOwnedCellCount() => OwnedCellCount++;
}

public enum CompassDirection
{
    None,
    North,
    NorthEast,
    East,
    SouthEast,
    South,
    SouthWest,
    West,
    NorthWest,
    Centre
}
