using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    public float weatherNoiseScale = 1f;
    public int weatherSeed = 0;
    public WindDirection windDirection = WindDirection.North;
    public Cell[,] weatherMap;
    public int timesMoved = 0;
    public Cell[,] previousWeatherMap;

    private MapGenerator mapGenerator;

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

    public void StartWeatherManagement()
    {
        mapGenerator = MapGenerator.Instance;
        if (mapGenerator == null)
        {
            Debug.LogError("MapGenerator instance not found. WeatherManager cannot generate weather.");
            return;
        }

        // Initialize the weather seed
        InitializeSeed();

        // Get weatherNoiseScale from GameManager
        GetWeatherNoiseScaleFromGameManager();
        UpdateWeatherNoiseScale();

        // Generate weather map
        GenerateWeatherMap();
        SynchronizeWeatherWithMainMap();

        // Call the method to count and debug the weather types
        CountWeatherTypes();
    }

    // Initialize seed using the GameManager's GameSeed
    private void InitializeSeed()
    {
        if (GameManager.Instance != null)
        {
            weatherSeed = GameManager.Instance.GameSeed;
            Debug.Log("Weather seed initialized with GameSeed: " + weatherSeed);
        }
        else
        {
            Debug.LogWarning("GameManager instance not found. Using default seed.");
            weatherSeed = 0;
        }
    }

    // New method to get weatherNoiseScale from GameManager
    private void GetWeatherNoiseScaleFromGameManager()
    {
        if (GameManager.Instance != null)
        {
            weatherNoiseScale = GameManager.Instance.weatherNoiseScale;
            Debug.Log("Weather noise scale set to: " + weatherNoiseScale);
        }
        else
        {
            Debug.LogWarning("GameManager instance not found. Using default weather noise scale.");
        }
    }

    // Method to add a random value to weatherNoiseScale
    public void UpdateWeatherNoiseScale()
    {
        // Roll a random float between -2 and +2
        float randomValue = Random.Range(-2f, 2f);

        // Add the random value to weatherNoiseScale
        weatherNoiseScale += randomValue;

        // Log the result to the console for debugging
        Debug.Log("Updated weatherNoiseScale: " + weatherNoiseScale);
    }

    public void GenerateWeatherMap()
    {
        int width = mapGenerator.width;
        int height = mapGenerator.height;
        weatherMap = new Cell[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Use the same noise generation logic as in MapGenerator
                float noiseValue = GetNoiseValue(x, y, weatherNoiseScale);

                // Determine weather type based on the noise value
                WeatherType weatherType = DetermineWeatherType(noiseValue);

                // Assign weather to both the map and weather map
                mapGenerator.map[x, y].CurrentWeather = weatherType;
                weatherMap[x, y] = mapGenerator.map[x, y];
            }
        }

        Debug.Log("Weather map generated successfully.");
    }

    // Same noise generation logic as MapGenerator, with dynamic weatherNoiseScale from GameManager
    private float GetNoiseValue(float x, float y, float scale)
    {
        float seed = GameManager.Instance.GameSeed;
        float xCoord = (x + seed) / mapGenerator.width * scale;
        float yCoord = (y + seed) / mapGenerator.height * scale;

        // Directly return the Perlin noise value without edge influence
        return Mathf.PerlinNoise(xCoord, yCoord);
    }

    private WeatherType DetermineWeatherType(float noiseValue)
    {
        // Map the noise value to different weather types
        Climate climate = GameManager.Instance.climate; // Get the current climate from GameManager

        switch (climate)
        {
            case Climate.Temperate:
                if (noiseValue < 0.4f) return WeatherType.Sunny;
                else if (noiseValue < 0.5f) return WeatherType.Cloudy;
                else if (noiseValue < 0.8f) return WeatherType.Rainy;
                else return WeatherType.Stormy;

            case Climate.Tropical:
                if (noiseValue < 0.1f) return WeatherType.Sunny;
                else if (noiseValue < 0.4f) return WeatherType.Cloudy;
                else if (noiseValue < 0.6f) return WeatherType.Rainy;
                else return WeatherType.Stormy;

            case Climate.Arid:
                if (noiseValue < 0.5f) return WeatherType.Sunny;
                else if (noiseValue < 0.7f) return WeatherType.Cloudy;
                else return WeatherType.Stormy;

            case Climate.Polar:
                if (noiseValue < 0.2f) return WeatherType.Sunny;
                else if (noiseValue < 0.4f) return WeatherType.Cloudy;
                else if (noiseValue < 0.7f) return WeatherType.Snowy;
                else return WeatherType.Blizzard;

            default:
                if (noiseValue < 0.3f) return WeatherType.Sunny;
                else if (noiseValue < 0.6f) return WeatherType.Cloudy;
                else if (noiseValue < 0.8f) return WeatherType.Rainy;
                else return WeatherType.Stormy;
        }
    }

    public void SynchronizeWeatherWithMainMap()
    {
        int width = mapGenerator.width;
        int height = mapGenerator.height;

        Season currentSeason = TimeManager.Instance.currentSeason; // Get the current season
        Climate currentClimate = GameManager.Instance.climate;     // Get the current climate

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (mapGenerator.map[x, y] != null && weatherMap[x, y] != null)
                {
                    // Get the current cell's elevation and weather
                    Cell currentCell = mapGenerator.map[x, y];
                    WeatherType currentWeather = weatherMap[x, y].CurrentWeather;

                    // Climate check: If Arid or Tropical, skip snow and blizzard changes
                    bool allowSnow = currentClimate != Climate.Arid && currentClimate != Climate.Tropical;

                    // Polar Climate: Always force rain to snow and storm to blizzard
                    if (currentClimate == Climate.Polar)
                    {
                        if (currentWeather == WeatherType.Rainy)
                        {
                            currentWeather = WeatherType.Snowy;
                        }
                        else if (currentWeather == WeatherType.Stormy)
                        {
                            currentWeather = WeatherType.Blizzard;
                        }
                    }
                    else
                    {
                        // Elevation-based weather conversion
                        if (currentCell.Elevation == Elevation.High && allowSnow)
                        {
                            // High elevation: turn rain into snow and storm into blizzard (if snow is allowed)
                            if (currentWeather == WeatherType.Rainy)
                            {
                                currentWeather = WeatherType.Snowy;
                            }
                            else if (currentWeather == WeatherType.Stormy)
                            {
                                currentWeather = WeatherType.Blizzard;
                            }
                        }
                        else if (currentCell.Elevation == Elevation.UpperMedium && allowSnow)
                        {
                            // UpperMedium elevation: 50% chance to turn rain into snow and storm into blizzard (if snow is allowed)
                            if (Random.value < 0.5f) // 50% chance
                            {
                                if (currentWeather == WeatherType.Rainy)
                                {
                                    currentWeather = WeatherType.Snowy;
                                }
                                else if (currentWeather == WeatherType.Stormy)
                                {
                                    currentWeather = WeatherType.Blizzard;
                                }
                            }
                        }
                        else if (currentCell.Elevation == Elevation.Low || currentCell.Elevation == Elevation.Medium)
                        {
                            // Season-based weather conversion for Low and Medium elevation
                            if (currentSeason == Season.Winter && allowSnow)
                            {
                                // Winter season: turn rain into snow and storm into blizzard (if snow is allowed)
                                if (currentWeather == WeatherType.Rainy)
                                {
                                    currentWeather = WeatherType.Snowy;
                                }
                                else if (currentWeather == WeatherType.Stormy)
                                {
                                    currentWeather = WeatherType.Blizzard;
                                }
                            }
                            else
                            {
                                // Non-winter seasons: convert snow to rain and blizzard to storm
                                if (currentWeather == WeatherType.Snowy)
                                {
                                    currentWeather = WeatherType.Rainy;
                                }
                                else if (currentWeather == WeatherType.Blizzard)
                                {
                                    currentWeather = WeatherType.Stormy;
                                }
                            }
                        }
                    }

                    // Update the main map's weather
                    currentCell.CurrentWeather = currentWeather;

                    // Check if the weather is Rainy or Stormy (after possible conversion), then update HasHadRain
                    if (currentWeather == WeatherType.Rainy || currentWeather == WeatherType.Stormy)
                    {
                        currentCell.HasHadRain = true;
                    }
                }
            }
        }

        Debug.Log("Weather synchronized with main map and adjusted for climate, elevation, and season.");
    }


    // Method to count and log the different weather types
    public void CountWeatherTypes()
    {
        Dictionary<WeatherType, int> weatherTypeCounts = new Dictionary<WeatherType, int>();

        foreach (var cell in weatherMap)
        {
            if (weatherTypeCounts.ContainsKey(cell.CurrentWeather))
            {
                weatherTypeCounts[cell.CurrentWeather]++;
            }
            else
            {
                weatherTypeCounts[cell.CurrentWeather] = 1;
            }
        }

        foreach (var weatherType in weatherTypeCounts)
        {
            Debug.Log($"{weatherType.Key}: {weatherType.Value}");
        }
    }

    public void MoveWeather()
    {
        int width = mapGenerator.width;
        int height = mapGenerator.height;

        // Store the previous weather map before making changes
        previousWeatherMap = new Cell[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Copy the existing cell to the previous weather map
                var currentCell = weatherMap[x, y];
                previousWeatherMap[x, y] = new Cell(currentCell.CellID, currentCell.Coordinates.x, currentCell.Coordinates.y, currentCell.Terrain);
                previousWeatherMap[x, y].CurrentWeather = currentCell.CurrentWeather;
            }
        }

        // Create a new weather map to hold the updated weather conditions
        Cell[,] newWeatherMap = new Cell[width, height];

        // Initialize the new weather map with default values or existing weather if needed
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Copy over the existing weather state with proper arguments for the constructor
                var currentCell = weatherMap[x, y];
                newWeatherMap[x, y] = new Cell(currentCell.CellID, currentCell.Coordinates.x, currentCell.Coordinates.y, currentCell.Terrain);
                newWeatherMap[x, y].CurrentWeather = currentCell.CurrentWeather;
            }
        }

        // Move the weather based on the current wind direction
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Get the new position based on the wind direction
                Vector2Int newPosition = GetNewPositionForWeather(x, y, windDirection);

                // Ensure newPosition is valid
                if (newPosition.x >= 0 && newPosition.x < width && newPosition.y >= 0 && newPosition.y < height)
                {
                    // Move the weather data from the old position to the new position
                    newWeatherMap[newPosition.x, newPosition.y].CurrentWeather = weatherMap[x, y].CurrentWeather;
                }
            }
        }

        // Replace the old weather map with the new one
        weatherMap = newWeatherMap;

        // Synchronize the weatherMap with the main map (mapGenerator.map)
        SynchronizeWeatherWithMainMap();

        // Increment the timesMoved counter
        timesMoved++;

        // Perform a random roll to change wind direction
        int randomRoll = Random.Range(1, 22); // Random number between 1 and 22
        if (randomRoll < timesMoved)
        {
            ChangeWindDirection(); // Change wind direction if the roll is lower than timesMoved
            Debug.Log($"Weather changed to {windDirection} direction. Times moved was: {timesMoved}, Random roll was: {randomRoll}");
            timesMoved = 0; // Reset the counter after changing direction
        }

        Debug.Log($"Weather moved in the {windDirection} direction. Times moved: {timesMoved}, Random roll: {randomRoll}");
    }

    // Get the new position for weather based on the wind direction, with edge wrapping
    private Vector2Int GetNewPositionForWeather(int x, int y, WindDirection windDirection)
    {
        int width = mapGenerator.width;
        int height = mapGenerator.height;

        int newX = x;
        int newY = y;

        switch (windDirection)
        {
            case WindDirection.North:
                newY = (y + 1) % height; // Move up, wrap around at top edge
                break;
            case WindDirection.South:
                newY = (y - 1 + height) % height; // Move down, wrap around at bottom edge
                break;
            case WindDirection.East:
                newX = (x + 1) % width; // Move right, wrap around at right edge
                break;
            case WindDirection.West:
                newX = (x - 1 + width) % width; // Move left, wrap around at left edge
                break;
        }

        return new Vector2Int(newX, newY);
    }

    public void ChangeWindDirection()
    {
        // Create a list of all possible directions
        List<WindDirection> remainingDirections = new List<WindDirection>()
    {
        WindDirection.North,
        WindDirection.South,
        WindDirection.East,
        WindDirection.West
    };

        // Remove the current wind direction from the list
        remainingDirections.Remove(windDirection);

        // Choose a new random direction from the remaining three directions
        windDirection = remainingDirections[Random.Range(0, remainingDirections.Count)];

        Debug.Log($"Wind direction changed to {windDirection}");
    }

    public WeatherType GetWeatherOfCell(int cellId)
    {
        // Attempt to locate the cell using MapGenerator's GetCellByID
        Cell cell = MapGenerator.Instance.GetCellByID(cellId);
        if (cell != null)
        {
            GameDebugger.Instance.LogInfo("Returning weather for cell ID " + cellId + ": " + cell.CurrentWeather);
            return cell.CurrentWeather;
        }
        else
        {
            GameDebugger.Instance.LogWarning("Cell with ID " + cellId + " not found. Returning default weather: Sunny.");
            return WeatherType.Sunny; // default fallback value
        }
    }



}

public enum WeatherType
{
    Sunny,
    Rainy,
    Stormy,
    Cloudy,
    Snowy,
    Blizzard,
    Foggy
}

public enum WindDirection
{
    North,
    South,
    East,
    West
}