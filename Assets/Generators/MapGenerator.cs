using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public class MapGenerator : MonoBehaviour
{

    public static MapGenerator Instance { get; private set; }

    public int width = 75; // Map width
    public int height = 75; // Map height
    public float noiseScale = 1;
    public float magicNoiseScale = 1;
    public float weatherNoiseScale = 1;
    public Climate climate = Climate.Temperate;
    public int forestClusters = 10; // Number of forest clusters
    public int forestClusterSize = 25; // Size of each forest cluster
    public int forestsGenerated = 0;
    public int swampClusters = 5; // Number of swamp clusters
    public int swampClusterSize = 20; // Size of each swamp cluster
    public int swampsGenerated = 0;
    public int dungeonsAtStart;
    public int cavesAtStart;
    public int campsAtStart;

    public Cell startCell;

    public Cell[,] map; // 2D array of Cell objects

    // Edge lists
    public List<Cell> NorthMapEdge = new List<Cell>();
    public List<Cell> SouthMapEdge = new List<Cell>();
    public List<Cell> EastMapEdge = new List<Cell>();
    public List<Cell> WestMapEdge = new List<Cell>();

    // List for corners
    public List<Cell> CornerCells = new List<Cell>();

    private List<INestedArea> nestedAreas = new List<INestedArea>();
    public List<Cell> allCells = new List<Cell>();

    public ForestGenerator forestGenerator;
    public SwampGenerator swampGenerator;
    public DesertGenerator desertGenerator;
    public RiverGenerator riverGenerator;

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



    void Start()
    {
        // Initialize ForestGenerator
        forestGenerator = GetComponent<ForestGenerator>();
        if (forestGenerator == null)
        {
            forestGenerator = gameObject.AddComponent<ForestGenerator>();
        }

        // Initialize SwampGenerator
        swampGenerator = GetComponent<SwampGenerator>();
        if (swampGenerator == null)
        {
            swampGenerator = gameObject.AddComponent<SwampGenerator>();
        }
    }

    public void GenerateMap()
    {
        UpdateSettingsFromGameManager();

        map = new Cell[width, height];
        InitializeMap();
        GenerateForests(); 
        GenerateSwamps();
        GenerateRivers();
        GenerateDeserts();
        PermaLists.Instance.CountTerrainTypes();
        IdentifyEdgeCells();
        IdentifyCornerCells();
        SetupPlayerStart();
        DetermineNeighbourTerrainTypes();
        GenerateSubterraneanWaterMap();
        GenerateOreDeposits();
        GenerateMagicLevels();

        // Set up Civlisations
        CivilisationManager.Instance.GenerateCivilisationsAtMapStart();
        FactionManager.Instance.SpreadInfluenceForAllFactions();

        // Generate dungeons after the map is initialized
        DungeonGenerator.Instance.GenerateAndAssignDungeons(dungeonsAtStart);
        CaveGenerator.Instance.GenerateAndAssignCaves(cavesAtStart);
        CampGenerator.Instance.GenerateAndAssignCamps(campsAtStart);

        RegionManager.Instance.PopulateRegionInfoAtGameStart();
        SetupRegionsBasedOnStartCell();

        // Begin plant generation process
        PlantFlowerManager.Instance.BeginPlantGeneration();

        // Weather Manager
        WeatherManager.Instance.StartWeatherManagement();


        // Begin animal generation process
        AnimalGenerator.Instance.BeginAnimalGeneration();
        AnimalGenerator.Instance.PopulateWorld();
        AnimalGenerator.Instance.GenerateMaterialsFromNativeAnimals();



        /* Tag cells in the central range
       TagCentralRangeCells();
       SetCentralRangeTerrainToTest();
        */

        // Debugging: Print all entries in AnimalsToGenerate
        foreach (var entry in PermaLists.Instance.AnimalsToGenerate)
        {
            Debug.Log($"CellID {entry.Key} has {entry.Value.Count} animals assigned.");
        }


        GameManager.Instance.MapGenerated = true;
        DisplayCellIDRange();
    }


    void UpdateSettingsFromGameManager()
    {
        if (GameManager.Instance != null)
        {

            GameManager.Instance.SetZeros();

            // Set the random seed
            UnityEngine.Random.InitState(GameManager.Instance.GameSeed);

            GameManager.Instance.ApplyAllSettings();
            climate = GameManager.Instance.climate;
            width = GameManager.Instance.mapWidth;
            height = GameManager.Instance.mapHeight;
            noiseScale = GameManager.Instance.noiseScale;
            forestClusters = GameManager.Instance.forestClusters; // Get forest cluster count from GameManager
            forestClusterSize = GameManager.Instance.forestClusterSize; // Get forest cluster size from GameManager
            swampClusters = GameManager.Instance.swampClusters; // Get swamp cluster count from GameManager
            swampClusterSize = GameManager.Instance.swampClusterSize; // Get swamp cluster size from GameManager
            weatherNoiseScale = GameManager.Instance.weatherNoiseScale;
            magicNoiseScale = GameManager.Instance.magicNoiseScale;

            dungeonsAtStart = GameManager.Instance.DungeonsAtStart;
            cavesAtStart = GameManager.Instance.CavesAtStart;
            campsAtStart = GameManager.Instance.CampsAtStart;

        }
        else
        {
            Debug.LogWarning("GameManager instance not found. Using default map dimensions.");
        }
    }

    void InitializeMap()
    {
        Debug.Log("Initialising the Map!");

        int regionSize = 25; // Size of a region
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("GameManager instance not found!");
            return;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int cellID = gameManager.GetCellID();
                var cell = new Cell(cellID, x, y, TerrainType.Land);


                // Calculate and assign the noise value here
                cell.NoiseValue = GetNoiseValue(x, y, noiseScale);

                // Continue with the rest of the cell initialization...
                cell.MainMapID = cellID;
                cell.PreviousTerrain = TerrainType.Land;
                cell.SubterraneanTerrain = TerrainType.Dirt;
                cell.Terrain = DetermineTerrainType(cell);
                cell.Elevation = AssignElevation(cell);

                if (cell.Terrain == TerrainType.Land)
                {
                    cell.TerrainToDisplay = GetRandomLandTerrainType();
                }
                if (cell.Terrain == TerrainType.Mountain)
                {
                    cell.SubterraneanTerrain = TerrainType.Stone;
                }


                map[x, y] = cell;

                // Assign region number based on cell position
                int regionX = x / regionSize;
                int regionY = y / regionSize;
                int regionNumber = regionY * (width / regionSize) + regionX; // This line assumes a top-down, left-to-right numbering
                cell.RegionNumber = regionNumber;

                cell.CurrentAreaID = 0;

                // Determine fertility
                GetFertility(cell);

                // Add the cell to the allCells list
                allCells.Add(cell);
                PermaLists.Instance.AllMapCells.Add(cell);

                // Determine adjacency
                int adjacentCount = 0;
                Dictionary<string, Vector2Int?> adjacentCells = new Dictionary<string, Vector2Int?>();

                if (x > 0) { adjacentCount++; adjacentCells["West"] = new Vector2Int(x - 1, y); }
                if (x < width - 1) { adjacentCount++; adjacentCells["East"] = new Vector2Int(x + 1, y); }
                if (y > 0) { adjacentCount++; adjacentCells["South"] = new Vector2Int(x, y - 1); }
                if (y < height - 1) { adjacentCount++; adjacentCells["North"] = new Vector2Int(x, y + 1); }

                cell.AdjacentCellCount = adjacentCount;
                cell.AdjacentCells = adjacentCells;
                cell.isMainMapCell = true;

                // Ensure the Animals list is initialized
                cell.Animals = new List<Animal>();
            }
        }

        UpdatePassibility();
    }

    private float GetNoiseValue(float x, float y, float scale)
    {
        float seed = GameManager.Instance.GameSeed;
        float xCoord = (x + seed) / width * scale;
        float yCoord = (y + seed) / height * scale;
        float baseNoise = Mathf.PerlinNoise(xCoord, yCoord);

        // Edge influence calculation
        float edgeDistance = Mathf.Min(x, width - x, y, height - y);
        float edgeInfluence = Mathf.Clamp01((edgeDistance - 1) / 2); // Adjust these values to control the extent and falloff

        // Combine base noise with edge influence
        return Mathf.Lerp(0.0f, baseNoise, edgeInfluence);
    }

    TerrainType DetermineTerrainType(Cell cell)
    {
        // Use the cell's NoiseValue to determine the terrain type
        float noiseValue = cell.NoiseValue;

        // Optional adjustment to noise value
        if (noiseValue > 0.22f && noiseValue < 0.9f)
        {
            float randomAdjustmentValue = 0f;
            bool randomChoice = UnityEngine.Random.value > 0.5f;

            if (randomChoice)
            {
                randomAdjustmentValue = UnityEngine.Random.Range(-0.02f, 0.02f);
            }

            noiseValue += randomAdjustmentValue;
        }

        float waterLevelAdjustment = 0f;
        float mountainLevelAdjustment = 0f;

        // Adjust noise thresholds based on water level setting
        switch (GameManager.Instance.waterLevel)
        {
            case TerrainWaterLevel.Dry:
                waterLevelAdjustment = -0.1f;
                break;
            case TerrainWaterLevel.Wet:
                waterLevelAdjustment = 0.1f;
                break;
        }

        // Adjust noise thresholds based on mountainousness setting
        switch (GameManager.Instance.mountainousness)
        {
            case TerrainMountainousness.Flat:
                mountainLevelAdjustment = 0.2f;
                break;
            case TerrainMountainousness.Mountainous:
                mountainLevelAdjustment = -0.1f;
                break;
        }

        // Climate-specific adjustments (you can comment out this entire section if needed)
        switch (GameManager.Instance.climate)
        {
            case Climate.Temperate:
                if (noiseValue < 0.2f + waterLevelAdjustment) return TerrainType.Water;
                else if (noiseValue < 0.21f + waterLevelAdjustment) return TerrainType.Sand;
                else if (noiseValue < 0.52f + mountainLevelAdjustment) return TerrainType.Land;
                else if (noiseValue < 0.9f + mountainLevelAdjustment) return TerrainType.Mountain;
                else return TerrainType.MountainPeak;

            case Climate.Tropical:
                if (noiseValue < 0.3f + waterLevelAdjustment) return TerrainType.Water;
                else if (noiseValue < 0.32f + waterLevelAdjustment) return TerrainType.Sand;
                else if (noiseValue < 0.6f + mountainLevelAdjustment) return TerrainType.Land;
                else if (noiseValue < 0.9f + mountainLevelAdjustment) return TerrainType.Mountain;
                else return TerrainType.MountainPeak;

            case Climate.Arid:
                if (noiseValue < 0.02f) return TerrainType.Water;
                else if (noiseValue < 0.3f + waterLevelAdjustment) return TerrainType.Saltflat;
                else if (noiseValue < 0.65f + mountainLevelAdjustment) return TerrainType.Land;
                else if (noiseValue < 0.9f + mountainLevelAdjustment) return TerrainType.Mountain;
                else return TerrainType.MountainPeak;

            case Climate.Polar:
                if (noiseValue < 0.02f) return TerrainType.Water;
                else if (noiseValue < 0.3f + waterLevelAdjustment) return TerrainType.Ice;
                else if (noiseValue < 0.65f + mountainLevelAdjustment) return TerrainType.Land;
                else if (noiseValue < 0.9f + mountainLevelAdjustment) return TerrainType.Mountain;
                else return TerrainType.MountainPeak;

            default:
                // Default case if the climate is unknown or not set
                if (noiseValue < 0.2f + waterLevelAdjustment) return TerrainType.Water;
                else if (noiseValue < 0.21f + waterLevelAdjustment) return TerrainType.Sand;
                else if (noiseValue < 0.52f + mountainLevelAdjustment) return TerrainType.Land;
                else if (noiseValue < 0.9f + mountainLevelAdjustment) return TerrainType.Mountain;
                else return TerrainType.MountainPeak;
        }
    }

    Elevation AssignElevation(Cell cell)
    {
        switch (cell.Terrain)
        {
            case TerrainType.Water:
            case TerrainType.River:
            case TerrainType.Lake:
                // Water-based terrains are typically at the lowest elevation
                return Elevation.Low;

            case TerrainType.Sand:
            case TerrainType.Desert:
            case TerrainType.Saltflat:
                // Flat, dry terrains might be lower-medium in elevation
                return Elevation.LowerMedium;

            case TerrainType.Plains:
            case TerrainType.Tundra:
            case TerrainType.Forest:
                // Plains, tundra, and forests can be medium elevation
                return Elevation.Medium;

            case TerrainType.Mountain:
            case TerrainType.Snow:
                // Mountainous or elevated terrains are higher
                return Elevation.UpperMedium;

            case TerrainType.MountainPeak:
            case TerrainType.Volcano:
                // Mountain peaks are the highest elevation
                return Elevation.High;

            default:
                // Default case for any unspecified terrain type
                return Elevation.Medium;
        }
    }

    // Utility method to get a cell by its coordinates
    public Cell GetCell(Vector2Int coordinates)
    {
        if (coordinates.x >= 0 && coordinates.x < width && coordinates.y >= 0 && coordinates.y < height)
        {
            return map[coordinates.x, coordinates.y];
        }
        else
        {
            Debug.LogError($"Coordinates out of range: {coordinates}");
            return null;
        }
    }

    public Cell GetCellByID(int id)
    {
        // Find the cell with the specified ID in the PermaLists.Instance.AllMapCells list
        Cell cell = PermaLists.Instance.AllMapCells.FirstOrDefault(cell => cell.CellID == id);

        // If the cell doesn't exist, log a warning
        if (cell == null)
        {
            Debug.LogWarning($"Cell with ID {id} not found in PermaLists.");
        }

        // Return the cell (null if not found)
        return cell;
    }

    void IdentifyEdgeCells()
    {
        NorthMapEdge.Clear();
        SouthMapEdge.Clear();
        EastMapEdge.Clear();
        WestMapEdge.Clear();

        for (int x = 0; x < width; x++)
        {
            SouthMapEdge.Add(map[x, 0]);
            NorthMapEdge.Add(map[x, height - 1]);
        }

        for (int y = 0; y < height; y++)
        {
            WestMapEdge.Add(map[0, y]);
            EastMapEdge.Add(map[width - 1, y]);
        }
    }

    void IdentifyCornerCells()
    {
        CornerCells.Clear();

        CornerCells.Add(map[0, 0]); // Bottom-left
        CornerCells.Add(map[width - 1, 0]); // Bottom-right
        CornerCells.Add(map[0, height - 1]); // Top-left
        CornerCells.Add(map[width - 1, height - 1]); // Top-right
    }

    public INestedArea GetNestedAreaAtCoordinates(Vector2Int coordinates)
    {
        if (IsPositionValid(coordinates))
        {
            return map[coordinates.x, coordinates.y].NestedArea;
        }
        else
        {
            Debug.LogWarning("Invalid coordinates specified.");
            return null;
        }
    }

    public List<Cell> GetCellsByTerrain(TerrainType terrain)
    {
        List<Cell> cells = new List<Cell>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (map[x, y].Terrain == terrain)
                {
                    cells.Add(map[x, y]);
                }
            }
        }

        return cells;
    }

    public void AddMapNuances()
    {
        // Example: Add lakes based on proximity to rivers or other criteria
        AddSandAtRiverBends();
    }

    private void AddSandAtRiverBends()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Check cells adjacent to rivers for potential conversion to sand
                if (map[x, y].Terrain != TerrainType.River && AdjacentToRiverBend(x, y))
                {
                    // Convert this cell's terrain to Sand if it's adjacent to a river bend
                    map[x, y].Terrain = TerrainType.Sand;
                }
            }
        }
    }

    private bool AdjacentToRiverBend(int x, int y)
    {
        // Check for river cells in all four directions
        bool hasNorthRiver = y + 1 < height && map[x, y + 1].Terrain == TerrainType.River;
        bool hasSouthRiver = y - 1 >= 0 && map[x, y - 1].Terrain == TerrainType.River;
        bool hasEastRiver = x + 1 < width && map[x + 1, y].Terrain == TerrainType.River;
        bool hasWestRiver = x - 1 >= 0 && map[x - 1, y].Terrain == TerrainType.River;

        // Check diagonally adjacent cells for river presence to better identify bends
        bool hasNorthEastRiver = x + 1 < width && y + 1 < height && map[x + 1, y + 1].Terrain == TerrainType.River;
        bool hasNorthWestRiver = x - 1 >= 0 && y + 1 < height && map[x - 1, y + 1].Terrain == TerrainType.River;
        bool hasSouthEastRiver = x + 1 < width && y - 1 >= 0 && map[x + 1, y - 1].Terrain == TerrainType.River;
        bool hasSouthWestRiver = x - 1 >= 0 && y - 1 >= 0 && map[x - 1, y - 1].Terrain == TerrainType.River;

        // Random chance implementation
        float randomChance = UnityEngine.Random.Range(1, 11); // Generates a number between 1 and 10

        // Apply 70% chance for the first condition
        if ((hasNorthRiver || hasSouthRiver) && (hasEastRiver || hasWestRiver) && randomChance <= 3.5)
        {
            return true;
        }
        // Apply 20% chance for the second condition
        else if ((hasNorthEastRiver || hasNorthWestRiver || hasSouthEastRiver || hasSouthWestRiver) &&
                 (hasNorthRiver || hasSouthRiver || hasEastRiver || hasWestRiver) && randomChance <= 0.5)
        {
            return true;
        }

        return false;
    }

    public Vector2Int GetCellCoordinatesContainingNestedArea(INestedArea nestedArea)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (map[x, y].NestedArea == nestedArea)
                {
                    return new Vector2Int(x, y); // Return the coordinates of the cell containing the nested area
                }
            }
        }
        return new Vector2Int(-1, -1); // Return an invalid position if not found
    }

    public List<IInteractable> GetObjectsAtPosition(Vector2Int position)
    {
        if (IsPositionValid(position))
        {
            return map[position.x, position.y].Objects;
        }
        else
        {
            Debug.LogWarning("Invalid position specified.");
            return new List<IInteractable>();
        }
    }

    public bool IsPositionValid(Vector2Int position)
    {
        return position.x >= 0 && position.x < width && position.y >= 0 && position.y < height;
    }

    // Method to add a nested area to the list
    public void AddNestedAreaToList(INestedArea nestedArea)
    {
        nestedAreas.Add(nestedArea);
    }

    // Method to find a nested area based on its ID
    public INestedArea FindNestedAreaBasedOnNestedAreaID(int nestedAreaID)
    {
        foreach (var nestedArea in nestedAreas)
        {
            if (nestedArea.NestedAreaID == nestedAreaID)
            {
                return nestedArea;
            }
        }
        return null; // Return null if no matching nested area is found
    }

    public void DebugTerrainTypeCounts()
    {
        // Create a dictionary to hold counts of each TerrainType
        Dictionary<TerrainType, int> terrainCounts = new Dictionary<TerrainType, int>();

        // Iterate over all cells in the list
        foreach (var cell in allCells)
        {
            // If the terrain type of the current cell has not been added to the dictionary, add it with a count of 1
            // Otherwise, increment the count for that terrain type
            if (!terrainCounts.ContainsKey(cell.Terrain))
            {
                terrainCounts[cell.Terrain] = 1;
            }
            else
            {
                terrainCounts[cell.Terrain]++;
            }
        }

        // Debug output the counts for each TerrainType
        foreach (var entry in terrainCounts)
        {
            Debug.Log($"Terrain Type: {entry.Key}, Count: {entry.Value}");
        }
    }

    public Cell FindStartingCell()
    {
        // Initialize a list to keep track of potential starting positions
        List<Cell> possibleStarts = new List<Cell>();

        // Initial edge check
        CheckMapEdges(possibleStarts);

        // If no valid cells are found on the edges, start an expanded search
        if (possibleStarts.Count == 0)
        {
            return ExpandedSearchForStartCell(possibleStarts);
        }

        // Return a random valid cell from the list of possible starting positions
        return possibleStarts[UnityEngine.Random.Range(0, possibleStarts.Count)];
    }

    private void CheckMapEdges(List<Cell> list)
    {
        // Top and bottom edges
        for (int x = 0; x < width; x++)
        {
            AddIfPassable(new Vector2Int(x, 0), list); // Bottom edge
            AddIfPassable(new Vector2Int(x, height - 1), list); // Top edge
        }
        // Left and right edges
        for (int y = 0; y < height; y++)
        {
            AddIfPassable(new Vector2Int(0, y), list); // Left edge
            AddIfPassable(new Vector2Int(width - 1, y), list); // Right edge
        }
    }

    private Cell ExpandedSearchForStartCell(List<Cell> possibleStarts)
    {
        // Start from one layer inside the edge and move towards the center
        for (int layer = 1; layer < Mathf.Min(width, height) / 2; layer++)
        {
            // Check the ring defined by 'layer'
            for (int x = layer; x < width - layer; x++)
            {
                AddIfPassable(new Vector2Int(x, layer), possibleStarts);
                AddIfPassable(new Vector2Int(x, height - 1 - layer), possibleStarts);
            }
            for (int y = layer; y < height - layer; y++)
            {
                AddIfPassable(new Vector2Int(layer, y), possibleStarts);
                AddIfPassable(new Vector2Int(width - 1 - layer, y), possibleStarts);
            }
            // Check if a valid cell has been found in this layer
            if (possibleStarts.Count > 0)
            {
                return possibleStarts[UnityEngine.Random.Range(0, possibleStarts.Count)];
            }
        }

        // If no valid cell is found after full search, report error or handle
        Debug.LogError("No valid starting cell found on the map.");
        return null; // or handle more appropriately based on game requirements
    }

    private void AddIfPassable(Vector2Int position, List<Cell> list)
    {
        if (IsValidStartCell(position))
        {
            list.Add(map[position.x, position.y]);
        }
    }

    private bool IsValidStartCell(Vector2Int position)
    {
        Cell cell = map[position.x, position.y];
        return cell.isPassable; // Ensure the Cell class has an isPassable property that returns false for water, etc.
    }

    public void SetupPlayerStart()
    {
        startCell = FindStartingCell();
        if (startCell != null)
        {
            // Mark the start cell with appropriate flags
            startCell.WasPlayerStart = true; // Mark this as the starting cell
            startCell.IsPlayerStart = true;
            startCell.TerrainToDisplay = TerrainType.PlayerStart; // Set the terrain to display as PlayerStart

            // Update GameManager with the start cell information
            GameManager.Instance.SetPlayerStartCell(startCell);

            Debug.Log($"Player start cell set at coordinates {startCell.Coordinates} with CellID {startCell.CellID}");
        }
        else
        {
            Debug.LogError("Failed to find a valid starting cell.");
            // Handle failure or fallback logic here
        }
    }


    public void UpdatePlayerCell()
    {
        Debug.Log("UpdatingPlayerCell");
        // Get the nested map
        Cell[,] nestedMap = PlayerStats.Instance.CurrentNestedArea.GetNestedMap();

        // Get the position from player stats
        Vector2Int position = PlayerStats.Instance.NestedMapPosition;

        // Get the cell at the provided position
        Cell cell = PlayerStats.Instance.CurrentNestedArea.GetCellAtPosition(position);
        cell.isPassable = false;
        Debug.Log("UpdatedPlayerCell");
    }

    public void UpdatePreviousPlayerCell()
    {
        Debug.Log("UpdatingPreviousPlayerCell");
        // Get the nested map
        Cell[,] nestedMap = PlayerStats.Instance.CurrentNestedArea.GetNestedMap();

        // Get the position from player stats
        Vector2Int position = PlayerStats.Instance.PreviousNestedMapPosition;

        // Get the cell at the provided position
        Cell cell = PlayerStats.Instance.CurrentNestedArea.GetCellAtPosition(position);
        cell.isPassable = true;
        Debug.Log("UpdatedPreviousPlayerCell");
    }

    public void UpdatePassibility()
    {
        // Iterate through each cell in AllMapCells
        foreach (Cell cell in allCells)
        {
            // Check if the terrain type is Water, River, Lake, or MountainPeak
            if (cell.Terrain == TerrainType.Water ||
                cell.Terrain == TerrainType.River ||
                cell.Terrain == TerrainType.Lake ||
                cell.Terrain == TerrainType.MountainPeak)
            {
                // Make the cell impassable
                cell.isPassable = false;
            }
            else
            {
                // Make the cell passable
                cell.isPassable = true;
            }
        }
    }

    void DetermineNeighbourTerrainTypes()
    {
        foreach (var cell in allCells)
        {
            var neighbours = new Dictionary<string, TerrainType>();
            int x = cell.Coordinates.x;
            int y = cell.Coordinates.y;

            // North Neighbour
            if (y < height - 1)
                neighbours["North"] = map[x, y + 1].Terrain;
            else
                neighbours["North"] = TerrainType.None;  // Use TerrainType.None or similar if there is no neighbour

            // South Neighbour
            if (y > 0)
                neighbours["South"] = map[x, y - 1].Terrain;
            else
                neighbours["South"] = TerrainType.None;

            // East Neighbour
            if (x < width - 1)
                neighbours["East"] = map[x + 1, y].Terrain;
            else
                neighbours["East"] = TerrainType.None;

            // West Neighbour
            if (x > 0)
                neighbours["West"] = map[x - 1, y].Terrain;
            else
                neighbours["West"] = TerrainType.None;

            // Assign the dictionary of neighbours to the cell
            cell.NeighbouringTerrainTypes = neighbours;
        }
    }

    // Method to check if a position is at the edge of the map
    public bool IsPositionAtEdge(Vector2Int position)
    {
        return NorthMapEdge.Any(cell => cell.Coordinates == position) ||
               SouthMapEdge.Any(cell => cell.Coordinates == position) ||
               EastMapEdge.Any(cell => cell.Coordinates == position) ||
               WestMapEdge.Any(cell => cell.Coordinates == position);
    }

    void GetFertility(Cell cell)
    {
        switch (cell.Terrain)
        {
            case TerrainType.Forest:
                cell.isFertile = true;
                cell.FertilityValue = UnityEngine.Random.Range(80, 101);
                break;
            case TerrainType.Dirt:
            case TerrainType.MountainPeak:
            case TerrainType.Desert:
            case TerrainType.Sand:
            case TerrainType.Snow:
            case TerrainType.Tundra:
                cell.isFertile = false;
                cell.FertilityValue = 0;
                break;
            default:
                cell.isFertile = true;
                cell.FertilityValue = UnityEngine.Random.Range(50, 81);
                break;
        }
    }

    void GenerateForests()
    {
        forestGenerator.GenerateForests(forestClusters, forestClusterSize); // Use the forest generator to create forests
    }

    void GenerateSwamps()
    {
        swampGenerator.GenerateSwamps(swampClusters); // Use the swamp generator to create swamps
    }

    void GenerateRivers()
    {
        if (riverGenerator != null)
        {
            riverGenerator.GenerateRivers();
        }
        else
        {
            Debug.LogError("RiverGenerator reference not set in MapGenerator.");
        }
    }

    void GenerateDeserts()
    {
        if (desertGenerator != null)
        {
            desertGenerator.GenerateDeserts();
        }
        else
        {
            Debug.LogError("DesertGenerator reference not set in MapGenerator.");
        }
    }

    public void GenerateOreDeposits()
    {
        Debug.Log("Generating ore deposits...");

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Cell cell = map[x, y];

                // Skip non-mountain or non-cave areas unless randomly chosen
                if (cell.Terrain != TerrainType.Mountain &&
                    cell.Terrain != TerrainType.MountainPeak &&
                    cell.Terrain != TerrainType.Cave)
                {
                    if (UnityEngine.Random.value > 0.02f) // 2% chance for rare surface ore
                        continue;
                }

                // Determine ore type based on terrain & randomness
                AssignOreToCell(cell);
            }
        }

        Debug.Log("Ore deposits successfully generated.");
    }

    private void AssignOreToCell(Cell cell)
    {
        float roll = UnityEngine.Random.value;

        // Common ores (higher chance)
        if (roll < 0.4f) // 40% chance for Iron Ore
        {
            cell.AddTag(ref cell.ResourceTagFlags, ResourceTags.IronOre);
        }
        else if (roll < 0.65f) // 25% chance for Copper Ore
        {
            cell.AddTag(ref cell.ResourceTagFlags, ResourceTags.CopperOre);
        }

        // Uncommon ores (lower chance)
        else if (roll < 0.80f) // 15% chance for Silver Ore
        {
            cell.AddTag(ref cell.ResourceTagFlags, ResourceTags.SilverOre);
        }
        else if (roll < 0.90f) // 10% chance for Gold Ore
        {
            cell.AddTag(ref cell.ResourceTagFlags, ResourceTags.GoldOre);
        }

        // Rare ores (very low chance, only in mountains or deep caves)
        else if (cell.Terrain == TerrainType.MountainPeak || cell.Terrain == TerrainType.Cave)
        {
            if (roll < 0.96f) // 6% chance for Mithril
            {
                cell.AddTag(ref cell.ResourceTagFlags, ResourceTags.MithrilOre);
            }
            else if (roll < 0.99f) // 3% chance for Adamantine
            {
                cell.AddTag(ref cell.ResourceTagFlags, ResourceTags.AdamantineOre);
            }
        }
    }


    public int GetDistanceToNearestCellWithTerrainType(int cellID, TerrainType targetTerrainType)
    {
        Cell startingCell = GetCellByID(cellID);
        if (startingCell == null)
        {
            Debug.LogError("Cell with given ID not found.");
            return 0;
        }

        Debug.Log($"Starting search for {targetTerrainType} from CellID: {cellID} at position {startingCell.Coordinates}");

        Queue<Cell> queue = new Queue<Cell>();
        HashSet<Cell> visited = new HashSet<Cell>();
        queue.Enqueue(startingCell);
        visited.Add(startingCell);

        int distance = 0;

        while (queue.Count > 0 && distance <= 4)
        {
            int count = queue.Count;
            Debug.Log($"Distance: {distance}, Queue Count: {count}");
            for (int i = 0; i < count; i++)
            {
                Cell current = queue.Dequeue();
                Debug.Log($"Checking cell at {current.Coordinates} with terrain {current.Terrain}");

                if (current.Terrain == targetTerrainType)
                {
                    Debug.Log($"Found {targetTerrainType} at distance {distance}");
                    return distance;
                }

                foreach (var neighbour in current.AdjacentCells.Values)
                {
                    if (neighbour.HasValue)
                    {
                        Cell neighbourCell = GetCell(neighbour.Value);
                        if (neighbourCell != null && !visited.Contains(neighbourCell))
                        {
                            queue.Enqueue(neighbourCell);
                            visited.Add(neighbourCell);
                            Debug.Log($"Added neighbour at {neighbour.Value} to the queue");
                        }
                    }
                    else
                    {
                        Debug.Log($"Neighbour is not set for direction at {neighbour}");
                    }
                }
            }

            distance++;
        }

        Debug.Log($"No {targetTerrainType} found within 4 cells from CellID: {cellID}");
        return 0; // Return 0 if no cell with the target terrain type is found within 4 cells.
    }


    public (string direction, int distance) GetNearestDungeonDirection(int cellID)
    {
        Cell startingCell = GetCellByID(cellID);
        if (startingCell == null)
        {
            Debug.LogError("Cell with given ID not found.");
            return ("Unknown", 0);
        }

        Queue<(Cell cell, int distance, string direction)> queue = new Queue<(Cell, int, string)>();
        HashSet<Cell> visited = new HashSet<Cell>();
        queue.Enqueue((startingCell, 0, "Start"));
        visited.Add(startingCell);

        while (queue.Count > 0)
        {
            var (currentCell, currentDistance, currentDirection) = queue.Dequeue();

            if (currentCell.HasDungeon)
            {
                return (currentDirection, currentDistance);
            }

            foreach (var kvp in currentCell.AdjacentCells)
            {
                var direction = kvp.Key;
                var neighbourCoords = kvp.Value;

                if (neighbourCoords.HasValue)
                {
                    Cell neighbourCell = GetCell(neighbourCoords.Value);
                    if (neighbourCell != null && !visited.Contains(neighbourCell))
                    {
                        queue.Enqueue((neighbourCell, currentDistance + 1, direction));
                        visited.Add(neighbourCell);
                    }
                }
            }
        }

        return ("Unknown", 0); // Return "Unknown" if no cell with a dungeon is found.
    }

    public void SetupRegionsBasedOnStartCell()
    {
        if (startCell == null)
        {
            Debug.LogError("Start cell is null. Cannot set up regions.");
            return;
        }

        // Get the RegionInfo based on the startCell's region number
        RegionInfo startRegionInfo = RegionManager.Instance.GetRegionInfo(startCell.RegionNumber);

        if (startRegionInfo != null)
        {
            // Use the region's CompassDirection to set up the edges
            RegionManager.Instance.SetCharacterLevelsBasedOnStart(startRegionInfo.CompassDirection);

            Debug.Log($"Regions set up based on player start cell: Region {startCell.RegionNumber}, Direction: {startRegionInfo.CompassDirection}");
        }
        else
        {
            Debug.LogError($"RegionInfo not found for region number: {startCell.RegionNumber}");
        }
    }

    public void TagCentralRangeCells()
    {
        // Flatten the 2D array of cells into a list
        List<Cell> allCellsList = allCells;

        // Sort the cells by CellID
        allCellsList.Sort((a, b) => a.CellID.CompareTo(b.CellID));

        // Calculate the index range for 25% to 75%
        int totalCells = allCellsList.Count;
        int startIndex = Mathf.FloorToInt(totalCells * 0.25f);
        int endIndex = Mathf.FloorToInt(totalCells * 0.75f);

        // Tag the cells within this range
        for (int i = startIndex; i < endIndex; i++)
        {
            allCellsList[i].IsInCentralRange = true;
        }

        Debug.Log($"Tagged {endIndex - startIndex} cells as being in the central range.");
    }

    public void SetCentralRangeTerrainToTest()
    {
        foreach (var cell in allCells)
        {
            if ((cell.RegionNumber == 3 || cell.RegionNumber == 4 || cell.RegionNumber == 5) && cell.IsInCentralRange)
            {
                cell.Terrain = TerrainType.Test;
            }
        }

        Debug.Log("Set terrain to TerrainType.Test for all cells in regions 2, 5, or 8 that are in the central range.");
    }

    TerrainType GetRandomLandTerrainType()
    {
        TerrainType[] landTypes = { TerrainType.Land1, TerrainType.Land2, TerrainType.Land3 };
        int randomValue = UnityEngine.Random.Range(0, 100); // Generate a random number between 0 and 99

        if (randomValue < 45) // 45% chance
        {
            return landTypes[0]; // TerrainType.Land1
        }
        else if (randomValue < 90) // Next 45% chance (50 to 89)
        {
            return landTypes[1]; // TerrainType.Land2
        }
        else // Remaining 10% chance (90 to 99)
        {
            return landTypes[2]; // TerrainType.Land3
        }
    }

    public void GenerateSubterraneanWaterMap()
    {
        Debug.Log("Generating subterranean water map with expanded water levels...");

        // First pass: Mark direct water cells with WaterLevel = 100
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Cell originalCell = map[x, y];

                // Default: No underground water
                originalCell.WaterLevel = 0;
                originalCell.SubterraneanTerrain = TerrainType.Dirt; // Default underground

                // If the surface terrain is a water body, set full underground saturation
                if (originalCell.Terrain == TerrainType.Water ||
                    originalCell.Terrain == TerrainType.River ||
                    originalCell.Terrain == TerrainType.Lake)
                {
                    originalCell.WaterLevel = 100;
                    originalCell.SubterraneanTerrain = TerrainType.Water; // Ensure underground water is set
                }
            }
        }

        // Second pass: Expand water influence to adjacent land cells
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Cell originalCell = map[x, y];

                if (originalCell.WaterLevel == 100)
                    continue; // Skip fully water-saturated cells

                int nearbyWaterCount = 0;

                // Check adjacent cells
                Vector2Int[] directions = {
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(1, 1), new Vector2Int(-1, -1),
                new Vector2Int(1, -1), new Vector2Int(-1, 1)
            };

                foreach (var dir in directions)
                {
                    Vector2Int neighborPos = new Vector2Int(x, y) + dir;
                    if (IsPositionValid(neighborPos))
                    {
                        Cell neighborCell = map[neighborPos.x, neighborPos.y];
                        if (neighborCell.WaterLevel == 100)
                        {
                            nearbyWaterCount++;
                        }
                    }
                }

                // Assign water level based on proximity to full water sources
                if (nearbyWaterCount >= 3)
                {
                    originalCell.WaterLevel = UnityEngine.Random.Range(60, 80); // High underground water
                }
                else if (nearbyWaterCount >= 1)
                {
                    originalCell.WaterLevel = UnityEngine.Random.Range(30, 50); // Medium underground water
                }

                // Ensure underground terrain reflects water presence
                if (originalCell.WaterLevel > 0)
                {
                    originalCell.SubterraneanTerrain = TerrainType.Water;
                }
            }
        }

        // Third pass: Add random underground water pockets (for wells)
        int numPockets = (width * height) / 200; // Adjust density based on map size
        for (int i = 0; i < numPockets; i++)
        {
            int randX = UnityEngine.Random.Range(0, width);
            int randY = UnityEngine.Random.Range(0, height);
            Cell waterPocketCell = map[randX, randY];

            // Ensure it's not already a high-water area
            if (waterPocketCell.WaterLevel < 30)
            {
                waterPocketCell.WaterLevel = UnityEngine.Random.Range(40, 70);
                waterPocketCell.SubterraneanTerrain = TerrainType.Water;
            }
        }

        Debug.Log("Subterranean water levels generated successfully.");
    }



    public void GenerateMagicLevels()
    {
        // Iterate through the original map and update the MagicLevel for each cell
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Get the original cell
                Cell cell = map[x, y];

                // Calculate the magic noise value
                float magicNoiseValue = GetNoiseValue(x, y, magicNoiseScale);

                // Assign MagicLevel based on the magic noise value
                if (magicNoiseValue < 0.5f)
                {
                    cell.MagicLevel = MagicLevel.None;
                }
                else if (magicNoiseValue < 0.65f)
                {
                    cell.MagicLevel = MagicLevel.Low;
                }
                else if (magicNoiseValue < 0.75f)
                {
                    cell.MagicLevel = MagicLevel.Medium;
                }
                else
                {
                    cell.MagicLevel = MagicLevel.High;
                }
            }
        }

        // Debugging: Print out that the MagicLevel for all cells has been set
        Debug.Log("MagicLevel for all cells has been set.");
    }

    public void UpdateFogOfWar(Cell playerCell)
    {
        // Ensure the player's cell is within valid bounds
        if (playerCell == null)
        {
            Debug.LogError("Player cell is null.");
            return;
        }

        // Get the coordinates of the player's current cell
        Vector2Int playerPosition = playerCell.Coordinates;

        // Loop through a 9x9 grid centered on the player's cell
        for (int xOffset = -4; xOffset <= 4; xOffset++)
        {
            for (int yOffset = -4; yOffset <= 4; yOffset++)
            {
                // Calculate the coordinates of the neighbouring cell
                Vector2Int neighbourPosition = new Vector2Int(playerPosition.x + xOffset, playerPosition.y + yOffset);

                // Check if the neighbouring cell is within the bounds of the map
                if (IsPositionValid(neighbourPosition))
                {
                    // Get the cell at the neighbour position
                    Cell neighbourCell = GetCell(neighbourPosition);

                    // Mark the neighbouring cell as seen by the player
                    if (neighbourCell != null)
                    {
                        neighbourCell.SeenByPlayer = true;
                    }
                }
            }
        }

        Debug.Log("Updated fog of war for a 9x9 area around player's position.");
    }

    public void DisplayCellIDRange()
    {
        if (allCells == null || allCells.Count == 0)
        {
            Debug.Log("No cells have been generated yet.");
            return;
        }

        int minCellID = int.MaxValue;
        int maxCellID = int.MinValue;

        foreach (Cell cell in allCells)
        {
            if (cell.CellID < minCellID)
                minCellID = cell.CellID;

            if (cell.CellID > maxCellID)
                maxCellID = cell.CellID;
        }

        Debug.Log($"CellID range: Lowest = {minCellID}, Highest = {maxCellID}");
    }

    public void CalculateDangerLevels()
    {
        Debug.Log("Calculating danger levels for all cells...");

        foreach (Cell cell in allCells)
        {
            // Default unexplored danger level
            cell.DangerLevel = 4;

            // 5 Extreme Danger - Dungeons, Monster Lairs, High-Risk Caves
            if (cell.HasDungeon || IsNearFlag(cell, EnvironmentalTags.Dungeon, 1))
            {
                cell.DangerLevel = 5;
                continue;
            }

            // 3-4 Moderate Risk - Unexplored areas, far from civilization
            if (IsNearFlag(cell, EnvironmentalTags.Forest, 5) || IsNearFlag(cell, EnvironmentalTags.Swamp, 5))
            {
                cell.DangerLevel = UnityEngine.Random.value < 0.5f ? 3 : 4; // 50% chance of slight variance
            }

            // 2 Low Risk - Near roads, outposts, or minor settlements
            if (IsNearTerrainType(cell, TerrainType.Road, 3) || IsNearFlag(cell, EnvironmentalTags.Village, 3))
            {
                cell.DangerLevel = 2;
            }

            // 1 Safe Zone - Near villages OR near player start
            if (cell.HasVillage || IsNearFlag(cell, EnvironmentalTags.Village, 5) || IsNearPlayerStart(cell))
            {
                cell.DangerLevel = 1;
            }

            // 0 Absolute Safe Zone - The player's exact starting cell
            if (cell.IsPlayerStart)
            {
                cell.DangerLevel = 0;
            }
        }

        Debug.Log("Danger levels assigned successfully.");
    }

    public void UpdateDangerLevelsOverTime()
    {
        Debug.Log("Updating dynamic danger levels...");

        foreach (Cell cell in allCells)
        {
            int originalLevel = cell.DangerLevel;

            // Player's start location must NEVER become dangerous
            if (cell.IsPlayerStart)
            {
                cell.DangerLevel = 0;
                continue;
            }

            // If near player start, keep it relatively safe
            if (IsNearPlayerStart(cell))
            {
                cell.DangerLevel = Mathf.Max(1, cell.DangerLevel - 1);
            }

            // If near a dungeon or monster lair, increase danger over time
            if (IsNearFlag(cell, EnvironmentalTags.Dungeon, 2) || IsNearFlag(cell, EnvironmentalTags.Cave, 2))
            {
                cell.DangerLevel = Mathf.Min(cell.DangerLevel + UnityEngine.Random.Range(0, 2), 5);
            }

            // If near a village or patrolled area, decrease danger over time
            else if (IsNearFlag(cell, EnvironmentalTags.Village, 5) || IsNearTerrainType(cell, TerrainType.Road, 3))
            {
                cell.DangerLevel = Mathf.Max(cell.DangerLevel - UnityEngine.Random.Range(0, 2), 1);
            }

            // If previously unexplored but now visited, lower danger slightly
            else if (cell.DangerLevel == 4 && cell.HasVisited)
            {
                cell.DangerLevel = 3;
            }

            // If under control (e.g., outposts built), lower danger
            else if (IsNearFlag(cell, EnvironmentalTags.Village, 3))
            {
                cell.DangerLevel = Mathf.Max(1, cell.DangerLevel - 1);
            }

            // Log danger level changes
            if (originalLevel != cell.DangerLevel)
            {
                Debug.Log($"Danger level updated for Cell {cell.CellID}: {originalLevel} -> {cell.DangerLevel}");
            }
        }

        Debug.Log("Dynamic danger levels updated.");
    }

    // Checks if a cell is near the player's start location
    private bool IsNearPlayerStart(Cell cell)
    {
        return cell.IsPlayerStart || GetDistanceToNearestPlayerStart(cell) < 5;
    }

    // Generic function to check if a cell is near a specific TerrainType
    private bool IsNearTerrainType(Cell cell, TerrainType targetType, int range)
    {
        return GetDistanceToNearestTerrainType(cell, targetType) <= range;
    }

    // Finds the distance to the nearest TerrainType
    private int GetDistanceToNearestTerrainType(Cell startCell, TerrainType targetType)
    {
        Queue<Cell> queue = new Queue<Cell>();
        HashSet<Cell> visited = new HashSet<Cell>();

        queue.Enqueue(startCell);
        visited.Add(startCell);

        int distance = 0;

        while (queue.Count > 0)
        {
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                Cell current = queue.Dequeue();

                if (current.Terrain == targetType)
                {
                    return distance;
                }

                foreach (var neighborCoords in current.AdjacentCells.Values)
                {
                    if (neighborCoords.HasValue)
                    {
                        Cell neighbor = GetCell(neighborCoords.Value);
                        if (neighbor != null && !visited.Contains(neighbor))
                        {
                            queue.Enqueue(neighbor);
                            visited.Add(neighbor);
                        }
                    }
                }
            }
            distance++;
        }

        return int.MaxValue; // No match found
    }

    // Generic function to check if a cell is near an EnvironmentalTag (bitwise flag)
    private bool IsNearFlag(Cell cell, EnvironmentalTags targetFlag, int range)
    {
        return GetDistanceToNearestFlag(cell, targetFlag) <= range;
    }

    // Finds the distance to the nearest EnvironmentalTag
    private int GetDistanceToNearestFlag(Cell startCell, EnvironmentalTags targetFlag)
    {
        Queue<Cell> queue = new Queue<Cell>();
        HashSet<Cell> visited = new HashSet<Cell>();

        queue.Enqueue(startCell);
        visited.Add(startCell);

        int distance = 0;

        while (queue.Count > 0)
        {
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                Cell current = queue.Dequeue();

                if (current.HasTag(current.EnvironmentalTagFlags, targetFlag))
                {
                    return distance;
                }

                foreach (var neighborCoords in current.AdjacentCells.Values)
                {
                    if (neighborCoords.HasValue)
                    {
                        Cell neighbor = GetCell(neighborCoords.Value);
                        if (neighbor != null && !visited.Contains(neighbor))
                        {
                            queue.Enqueue(neighbor);
                            visited.Add(neighbor);
                        }
                    }
                }
            }
            distance++;
        }

        return int.MaxValue; // No match found
    }

    // Finds the distance to the nearest player start location
    private int GetDistanceToNearestPlayerStart(Cell startCell)
    {
        Queue<Cell> queue = new Queue<Cell>();
        HashSet<Cell> visited = new HashSet<Cell>();

        queue.Enqueue(startCell);
        visited.Add(startCell);

        int distance = 0;

        while (queue.Count > 0)
        {
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                Cell current = queue.Dequeue();

                if (current.IsPlayerStart)
                {
                    return distance;
                }

                foreach (var neighborCoords in current.AdjacentCells.Values)
                {
                    if (neighborCoords.HasValue)
                    {
                        Cell neighbor = GetCell(neighborCoords.Value);
                        if (neighbor != null && !visited.Contains(neighbor))
                        {
                            queue.Enqueue(neighbor);
                            visited.Add(neighbor);
                        }
                    }
                }
            }
            distance++;
        }

        return int.MaxValue; // No player start found nearby
    }

}

public enum Climate
{
    Temperate,
    Tropical,
    Arid,
    Polar
}

public enum Density
{
    None,
    Sparse,
    Average,
    Numerous
}

public enum Size
{
    Small,
    Medium,
    Large
}

public enum TerrainMountainousness
{
    Flat,
    Average,
    Mountainous
}

public enum TerrainWaterLevel
{
    Dry,
    Average,
    Wet
}

public enum MagicLevel
{
    None,
    Low,
    Medium,
    High
}

public enum Elevation
{
    Low,
    LowerMedium,
    Medium,
    UpperMedium,
    High
}


public enum TerrainType
{
    Bridge,
    Camp,
    Cave,
    Desert,
    Dirt,
    Forest,
    Glade,
    Graveyard,
    Grove,
    Hall,
    Ice,
    Lake,
    Land,
    Mountain,
    MountainPeak,
    Plains,
    River,
    Road,
    Ruins,
    Saltflat,
    Salt,
    Sand,
    SandDesert,
    SandBeach,
    Slate,
    Snow,
    Stone,
    Swamp,
    TilledSoil,
    Tundra,
    Village,
    Water,
    Volcano,
    Land1,
    Land2,
    Land3,
    Forest1,
    Forest2,
    Forest3,
    Path,
    Path1,
    Path2,
    Path3,
    Plank,
    Underground,
    PlayerStart,
    None,
    Test,
    Default
}

public class Cell
{
    public int CellID { get; private set; }
    public int MainMapID { get; set; }
    public int RegionNumber { get; set; }
    public float NoiseValue { get; set; }
    public int ParentAreaID { get; set; }
    public int CurrentAreaID { get; set; }
    public int ChildAreaID { get; set; }
    public Vector2Int Coordinates { get; private set; }
    public bool isMainMapCell { get; set; }
    public bool IsInCentralRange { get; set; }
    public int cellLevel { get; set; }
    public int DangerLevel { get; set; }
    public TerrainType Terrain { get; set; }
    public TerrainType PreviousTerrain { get; set; }
    public TerrainType VillageTypeTerrain { get; set; }
    public TerrainType LandTerrainType { get; set; }
    public TerrainType? SubterraneanTerrain { get; set; }
    public TerrainType? TerrainToDisplay { get; set; }
    public MagicLevel MagicLevel { get; set; }
    public Elevation Elevation { get; set; }
    public EnvironmentalTags EnvironmentalTagFlags;
    public ResourceTags ResourceTagFlags;
    public WeatherType CurrentWeather { get; set; }
    public int AdjacentCellCount { get; set; }
    public Dictionary<string, Vector2Int?> AdjacentCells { get; set; }
    public Dictionary<string, TerrainType> NeighbouringTerrainTypes = new Dictionary<string, TerrainType>();
    public bool isIndoors = false;
    public bool isPlayerPresent = false;
    public bool isPassable = true;
    public bool isInNestedArea = false;
    public int parentCell { get; set; }
    public bool hasNestedArea = false;
    public bool nestedAreaCanBeSeen = false;
    public INestedArea NestedArea { get; set; }
    public List<IInteractable> Objects { get; set; } = new List<IInteractable>();
    public List<Item> Items { get; set; } = new List<Item>();
    public List<Animal> Animals { get; set; } = new List<Animal>();
    public List<NPC> NPCs { get; set; } = new List<NPC>();
    public bool isNPCGroupPresent = false;
    public bool isNPCPresent = false;
    public bool canBeSeenByNPC = false;
    public bool isPlayerHome = false;
    public bool isFertile = false;
    public bool isCurated = false;
    public bool isFishable = false;
    public bool HasHadRain = false;
    public int FertilityValue = 0;
    public int OverallFertilityAdjustment = 0;
    public int WaterLevel = 0;

    public bool SeenByPlayer = false;

    public bool HasDungeon = false;
    public int DungeonID;
    public bool HasCave = false;
    public int CaveID;
    public bool HasCamp = false;
    public int CampID;
    public Camp Camp;

    public bool WasPlayerStart = false;
    public bool IsPlayerStart = false;

    public bool HasLandmark = false;
    public string LandmarkName;
    
    public bool HasVillage = false;
    public bool IsPlayerHome = false;
    public Village Village { get; set; }

    public bool HasVisited { get; set; } = false;
    public int LastVisited { get; set; } = -1;
    public int NestedAreaLevel { get; set; }
    public int PassedThroughCount { get; set; } = 0;

    public bool IsOwned = false;
    public bool IsOwnedByPlayer = false;
    public string OwnedBy { get; set; }
    public Faction OwnedByFaction { get; set; }

    public int ExpansionCost { get; set; } = 10;

    public Cell(int cellID, int x, int y, TerrainType terrain)
    {
        CellID = cellID;
        Coordinates = new Vector2Int(x, y);
        Terrain = terrain;
        RegionNumber = (y / 25) * 3 + (x / 25); // For a 75x75 map divided into 25x25 regions
        AdjacentCells = new Dictionary<string, Vector2Int?>
        {
            { "North", null },
            { "South", null },
            { "East", null },
            { "West", null }
        };

        isPassable = true;
        hasNestedArea = false;

        // Initialize the Animals list
        Animals = new List<Animal>();
    }

    public bool IsAtRegionEdge()
    {
        int regionSize = 25; // Assuming a constant region size, but this could also be dynamically set
        return Coordinates.x % regionSize == 0 || Coordinates.x % regionSize == regionSize - 1
            || Coordinates.y % regionSize == 0 || Coordinates.y % regionSize == regionSize - 1;
    }



    public void SetNestedArea(INestedArea nestedArea)
    {
        NestedArea = nestedArea;
        hasNestedArea = nestedArea != null;
    }

    public void UpdateNPCPosition(NPC npc, Vector2Int newPosition)
    {
        if (Objects.Contains(npc))
        {
            Objects.Remove(npc);
        }
        Objects.Add(npc);
    }

    public void UpdateNPCGroupPosition(NPCGroup npcGroup)
    {
        isNPCGroupPresent = true;
    }

    public bool IsNPCGroupPresent()
    {
        return isNPCGroupPresent;
    }

    // Add Tag (Generic for EnvironmentalTags, ResourceTags, or other enums)
    public void AddTag<T>(ref T tagContainer, T tag) where T : Enum
    {
        int tagValue = Convert.ToInt32(tag);
        tagContainer = (T)(object)(Convert.ToInt32(tagContainer) | tagValue);
    }

    // Remove Tag (Generic)
    public void RemoveTag<T>(ref T tagContainer, T tag) where T : Enum
    {
        int tagValue = Convert.ToInt32(tag);
        tagContainer = (T)(object)(Convert.ToInt32(tagContainer) & ~tagValue);
    }

    // Check Tag (Generic)
    public bool HasTag<T>(T tagContainer, T tag) where T : Enum
    {
        int tagValue = Convert.ToInt32(tag);
        int containerValue = Convert.ToInt32(tagContainer);

        // Check if the exact tag is present
        if ((containerValue & tagValue) == tagValue)
            return true;

        // NEW: Check Parent Categories for ResourceTags
        if (typeof(T) == typeof(ResourceTags))
        {
            if ((tagValue & (int)ResourceTags.Ore) != 0 && (containerValue & (int)ResourceTags.Ore) != 0)
                return true;
            if ((tagValue & (int)ResourceTags.PreciousStone) != 0 && (containerValue & (int)ResourceTags.PreciousStone) != 0)
                return true;
        }

        return false;
    }

    // Print Active Tags (for Debugging)
    public void PrintTags()
    {
        Debug.Log($"Environmental Tags: {GetActiveTags(EnvironmentalTagFlags)}");
        Debug.Log($"Resource Tags: {GetActiveTags(ResourceTagFlags)}");
    }

    public string GetActiveTags<T>(T tagContainer) where T : Enum
    {
        var activeTags = Enum.GetValues(typeof(T))
            .Cast<T>()
            .Where(tag => HasTag(tagContainer, tag) && !tag.Equals(default(T)))
            .Select(tag => tag.ToString());

        return string.Join(", ", activeTags);
    }

    public bool HasAnyTag<T>(T tagContainer, T tag) where T : Enum
    {
        int tagValue = Convert.ToInt32(tag);
        return (Convert.ToInt32(tagContainer) & tagValue) != 0;
    }

    // AUDIT TAGS
    public void AuditTags()
    {
        // Clear existing tags to avoid duplicates
        EnvironmentalTagFlags = EnvironmentalTags.None;
        ResourceTagFlags = ResourceTags.None;

        // Environmental Tagging
        switch (Terrain)
        {
            case TerrainType.Mountain:
            case TerrainType.MountainPeak:
                AddTag(ref EnvironmentalTagFlags, EnvironmentalTags.Mountain);
                AddTag(ref ResourceTagFlags, ResourceTags.Stone);
                break;
            case TerrainType.Desert:
                AddTag(ref EnvironmentalTagFlags, EnvironmentalTags.Desert);
                break;
            case TerrainType.Forest:
                AddTag(ref EnvironmentalTagFlags, EnvironmentalTags.Forest);
                AddTag(ref ResourceTagFlags, ResourceTags.Wood | ResourceTags.Herbs);
                break;
            case TerrainType.Swamp:
                AddTag(ref EnvironmentalTagFlags, EnvironmentalTags.Swamp);
                AddTag(ref ResourceTagFlags, ResourceTags.Herbs);
                break;
            case TerrainType.Water:
            case TerrainType.Lake:
            case TerrainType.River:
                AddTag(ref EnvironmentalTagFlags, EnvironmentalTags.Water);
                AddTag(ref ResourceTagFlags, ResourceTags.Water | ResourceTags.Food);
                break;
            case TerrainType.Plains:
                AddTag(ref EnvironmentalTagFlags, EnvironmentalTags.Plains);
                break;
            case TerrainType.Village:
                AddTag(ref EnvironmentalTagFlags, EnvironmentalTags.Village);
                break;
        }

        // NEW: Auto-Assign Parent Categories for Resources
        if (HasAnyTag(ResourceTagFlags, ResourceTags.IronOre | ResourceTags.CopperOre | ResourceTags.SilverOre | ResourceTags.GoldOre |
                                        ResourceTags.MithrilOre | ResourceTags.AdamantineOre))
        {
            AddTag(ref ResourceTagFlags, ResourceTags.Ore);
        }

        if (HasAnyTag(ResourceTagFlags, ResourceTags.Diamond | ResourceTags.Ruby | ResourceTags.Sapphire |
                                        ResourceTags.Emerald | ResourceTags.Moonstone | ResourceTags.Obsidian))
        {
            AddTag(ref ResourceTagFlags, ResourceTags.PreciousStone);
        }

        // Check Magic Influence
        if (MagicLevel == MagicLevel.High)
        {
            AddTag(ref ResourceTagFlags, ResourceTags.Magic);
        }
    }


    public void SetExpansionCost(int cost)
    {
        ExpansionCost = cost;
    }


}

public interface IMapArea
{
    bool IsValidPosition(Vector2Int position);
    bool IsCellPassable(Vector2Int position);
    Cell GetCellAtPosition(Vector2Int position);
    void UpdatePlayerPosition(Vector2Int position);
}

public enum Tags
{
    None,
    Wood,
    Stone,
    Water,
    Cave
}

[Flags]
public enum EnvironmentalTags
{
    None = 0,
    Mountain = 1 << 0,  // 0000 0001 = 1
    Desert = 1 << 1,  // 0000 0010 = 2
    Forest = 1 << 2,  // 0000 0100 = 4
    Swamp = 1 << 3,  // 0000 1000 = 8
    Water = 1 << 4,  // 0001 0000 = 16
    Plains = 1 << 5,  // 0010 0000 = 32
    Road = 1 << 6,  // 0100 0000 = 64
    Village = 1 << 7,   // 1000 0000 = 128
    Cave = 1 << 8,
    Dungeon = 1 << 9
}

[Flags]
public enum ResourceTags
{
    None = 0,

    // Parent Categories
    FoodSource = 1 << 0,
    WaterSource = 1 << 1,
    WoodSource = 1 << 2,
    StoneSource = 1 << 3,
    Ore = 1 << 4,   // Parent category for all ores
    PreciousStone = 1 << 5, // Parent category for all gemstones
    MagicSource = 1 << 6,  // Parent category for magical energy sources

    // Common Basic Resources
    Stone = 1 << 7 | StoneSource,  // Generic stone, used for buildings
    Wood = 1 << 8 | WoodSource,  // General timber source
    Water = 1 << 9 | WaterSource,  // Freshwater sources
    Food = 1 << 10 | FoodSource,  // General food availability
    Herbs = 1 << 11 | FoodSource,  // Used for potions & medicine
    Magic = 1 << 12 | MagicSource,  // Raw magical energy collection

    // Common Metals (Ore)
    IronOre = 1 << 13 | Ore,
    CopperOre = 1 << 14 | Ore,
    SilverOre = 1 << 15 | Ore,
    GoldOre = 1 << 16 | Ore,
    MithrilOre = 1 << 17 | Ore,
    AdamantineOre = 1 << 18 | Ore,

    // Precious Stones
    Diamond = 1 << 19 | PreciousStone,
    Ruby = 1 << 20 | PreciousStone,
    Sapphire = 1 << 21 | PreciousStone,
    Emerald = 1 << 22 | PreciousStone,
    Moonstone = 1 << 23 | PreciousStone,
    Obsidian = 1 << 24 | PreciousStone,
}

