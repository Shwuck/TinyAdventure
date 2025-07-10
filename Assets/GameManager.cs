using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool GoodToStart = false;

    public int GameSeed;
    public int PlayerGivenSeed;
    public int ScenarioSeed;

    public int CivilisationCount = 4;
    public int RaceCount = 4;

    public bool GameStarted = false;
    public bool MapGenerated = false;

    public bool isDebugModeOn = true;
    public bool DebugStartGameAvailable = false;

    public bool manualRoadOverride = false;

    public int startEdgeSelection = 0;
    public int endEdgeSelection = 0;

    public Cell PlayerStartCell { get; private set; }
    public Vector2Int startCellCoordinates;
    public Vector2Int endCellCoordinates;
    public bool ActiveTurnManager;

    // GameStartDetails

    public bool MapSet = false;
    public bool PlayerSet = false;

    public int playerCharacterID = 1;
    public int cellID = 1;
    public int nestedAreaID = 1;
    public int interactableID = 1;
    public int npcID = 1;
    public int npcCounter = 0;
    public int AnimalID = 1;
    public int AnimalCounter = 0;
    public int MonsterID = 1;
    public int MonsterCounter = 0;
    public int VillageCounter = 0;
    public int DungeonID = 1;
    public int DungeonCount = 0;
    public int DungeonsAtStart = 18;
    public int CaveID = 1;
    public int CaveCount = 0;
    public int CavesAtStart = 25;
    public int CampID = 0;
    public int CampsAtStart = 18;
    public int CampCount = 0;
    public bool UseBasicLandmarks = true;
    public int LandmarkCount = 0;
    public int LandmarksAtStart = 10;
    public int ItemID = 1;
    public int GroupID = 1;

    // MapDetails

    public string MapPreviewText { get; private set; }
    public int mapWidth = 75;
    public int mapHeight = 75;
    public float noiseScale = 6;
    public float weatherNoiseScale = 3;
    public float magicNoiseScale = 12;
    public float riverNoiseScale = 20;
    public int numberOfRoads = 3;
    public int forestClusters = 5;
    public int forestClusterSize = 15;
    public int swampClusters = 5;
    public int swampClusterSize = 15;


    // In Game Details

    public int MaxTurnIterations = 10;


    // Visual Details

    public bool showFullMap = false;
    public bool displayUIHighlights = false;
    public bool showSubMap = false;
    public bool showMagicMap = false;
    public bool highlightPOI = false;
    public bool showFactions = false;
    public bool showWeatherMap = false;

    // ConcreteMapSettings to be updated at game start

    public int MaxRaceCount;

    // New fields for climate and other settings
    public Climate climate = Climate.Temperate;
    public Density forestDensity = Density.Average;
    public Size forestSize = Size.Medium;
    public Density swampDensity = Density.Average;
    public Size swampSize = Size.Medium;
    public TerrainMountainousness mountainousness = TerrainMountainousness.Average;
    public TerrainWaterLevel waterLevel = TerrainWaterLevel.Average;



    public NPCDataLoader npcDataLoader;
    public AnimalDataLoader animalDataLoader;
    public PlayerCharacterGenerator playerCharacterGenerator;
    public DataLoaderManager dataLoaderManager; // Reference to DataLoaderManager
    public GameObject splashPanel;

    private void Start()
    {
        
        splashPanel.SetActive(true);
        StartCoroutine(InitializeGame());
    }

    private IEnumerator InitializeGame()
    {
        // Ensure DataLoaderManager is assigned
        if (dataLoaderManager != null)
        {
            yield return dataLoaderManager.LoadAllData();
        }
        else
        {
            Debug.LogError("DataLoaderManager is not assigned in GameManager.");
        }

    }

    private void Awake()
    {
        // Ensure there's only one instance of the GameManager
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

    public void StartGame()
    {
        if (GoodToStart)
        {
            Debug.Log("Game is set to start. Performing necessary initializations.");

            UIController.Instance.ApplyFonts();

            // Perform any additional tasks needed to start the game
            UIController.Instance.FadeSplashPanel();
        }
        else
        {
            Debug.LogWarning("Game Start called when not Good to Start");
        }
    }

    public void SetPlayerStartCell(Cell startCell)
    {
        if (startCell != null)
        {
            PlayerStartCell = startCell;

            if (isDebugModeOn)
            {
                Debug.Log($"Player start cell set at coordinates: {startCellCoordinates}");
            }
        }
        else
        {
            Debug.LogError("Attempted to set a null start cell in GameManager.");
        }
    }

    public int GetCellID()
    {
        // Increment the cellID and return the new value
        return ++cellID;
    }

    public int GetPlayerCharacterID()
    {
        return ++playerCharacterID;
    }

    public int GetNestedAreaID()
    {
        return ++nestedAreaID;
    }

    public int GetInteractableID()
    {
        return ++interactableID;
    }

    public int GetNPCID()
    {
        return ++interactableID;
    }

    public int GetAnimalID()
    {
        return ++interactableID;
    }

    public int GetItemID()
    {
        return ++ItemID;
    }

    public int GetGroupID()
    {
        return ++GroupID;
    }

    public int GetNextNPCCounter()
    {
        return npcCounter++;
    }

    public int GetDungeonID()
    {
        return DungeonID++;
    }

    public int GetCaveID()
    {
        return CaveID++;
    }

    public int GetCampID()
    {
        return CampID++;
    }

    public int GetMonsterID()
    {
        return MonsterID++;
    }

    public int GetNextVillageCounter()
    {
        return VillageCounter++;
    }

    public int GenerateNewAnimalID()
    {
        return ++AnimalID;
    }

    public string GenerateNewHerdID()
    {
        return $"Herd_{++AnimalCounter}";
    }

    public string GenerateNewPackID()
    {
        return $"Pack_{++AnimalCounter}";
    }

    public void SetMapPreviewText(string text)
    {
        MapPreviewText = text;
    }


    public void ApplyAllSettings()
    {
        forestClusters = MapDensityToClusters(forestDensity);
        forestClusterSize = MapSizeToClusterSize(forestSize);
        swampClusters = MapDensityToClusters(swampDensity);
        swampClusterSize = MapSizeToClusterSize(swampSize);
        noiseScale = GetNoiseValue();

    }

    private int MapDensityToClusters(Density density)
    {
        switch (density)
        {
            case Density.None: return 0;
            case Density.Sparse: return 2;
            case Density.Average: return 5;
            case Density.Numerous: return 10;
            default: return 5; // Default to Average
        }
    }

    private int MapSizeToClusterSize(Size size)
    {
        switch (size)
        {
            case Size.Small: return 10;
            case Size.Medium: return 15;
            case Size.Large: return 20;
            default: return 15; // Default to Medium
        }
    }

    private float GetNoiseValue()
    {
        // Initialize the random number generator with GameSeed
        Random.InitState(GameSeed);

        // Generate a random float between 5.5 and 8.5
        float randomFloat = Random.Range(5.5f, 8.5f);

        return randomFloat;
    }

    public void ResetGame()
    {
        GameSeed = 0;
        PlayerGivenSeed = 0;
        ScenarioSeed = 0;

        CivilisationCount = 4;
        RaceCount = 4;

        GameStarted = false;

        isDebugModeOn = false;
        showFullMap = false;
        manualRoadOverride = false;

        startEdgeSelection = 0;
        endEdgeSelection = 0;

        PlayerStartCell = null;
        startCellCoordinates = Vector2Int.zero;
        endCellCoordinates = Vector2Int.zero;

        playerCharacterID = 1;
        cellID = 0;
        nestedAreaID = 0;
        npcID = 0;
        npcCounter = 0;
        AnimalID = 0;
        AnimalCounter = 0;
        VillageCounter = 0;
        DungeonID = 0;
        DungeonCount = 0;
        DungeonsAtStart = 3;
        ItemID = 0;

        // Reset map details
        mapWidth = 75;
        mapHeight = 75;
        noiseScale = 6;
        riverNoiseScale = 20;
        numberOfRoads = 3;
        forestClusters = 5;
        forestClusterSize = 15;
        swampClusters = 5;
        swampClusterSize = 15;

        // Reset new fields for climate and other settings
        climate = Climate.Temperate;
        forestDensity = Density.Average;
        forestSize = Size.Medium;
        swampDensity = Density.Average;
        swampSize = Size.Medium;
        mountainousness = TerrainMountainousness.Average;
        waterLevel = TerrainWaterLevel.Average;

        displayUIHighlights = false;

        Debug.Log("Game parameters reset for a new game.");
    }

    public void SetZeros()
    {
        cellID = 0;
        nestedAreaID = 0;
        interactableID = 0;
        npcID = 0;
        npcCounter = 0;
        AnimalID = 0;
        AnimalCounter = 0;
        VillageCounter = 0;
        DungeonID = 0;
        DungeonCount = 0;
        ItemID = 0;
        GroupID = 0;
    }




















}
