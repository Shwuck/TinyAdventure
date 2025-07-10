using UnityEngine;
using TMPro;
using System.Text;
using System.Linq;

public class MapDisplayUI : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public PlayerController playerController;
    public NPCManager npcManager;
    public TMP_Text mapDisplayText;
    public TMP_Text nestedMapDisplayText;
    public TMP_Text mapDisplayDebugText;
    public Vector2Int playerPosition;

    private StringBuilder mapTextBuilder = new StringBuilder();
    private StringBuilder nestedMapTextBuilder = new StringBuilder();
    private StringBuilder subterraneanMapTextBuilder = new StringBuilder();
    private StringBuilder magicMapTextBuilder = new StringBuilder();
    private StringBuilder factionMapTextBuilder = new StringBuilder();
    private StringBuilder weatherMapTextBuilder = new StringBuilder();

    private int zoomLevel = 1;
    private const int maxZoomLevel = 5;
    private const int minZoomLevel = 1;
    private int visibleAreaSize = 5;
    private bool isMapZoomed = false;
    private bool swapPerformed = false;

    public void UpdateBothMaps()
    {
        if (PlayerStats.Instance.IsInMainMap)
        {
            playerPosition = PlayerStats.Instance.Position;
            Cell playerCell = mapGenerator.GetCell(playerPosition);

            if (GameManager.Instance.showMagicMap)
            {
                UpdateMagicMapDisplay();
            }
            else if (GameManager.Instance.showSubMap)
            {
                UpdateSubterraneanMapDisplay();
            }
            else if (GameManager.Instance.showFactions)
            {
                UpdateFactionMapDisplay();
            }
            else if (GameManager.Instance.showWeatherMap)  
            {
                UpdateWeatherMapDisplay();
            }
            else
            {
                UpdateMapDisplay();  
            }

            if (playerCell != null)
            {
                UpdateNestedMapDisplay(playerCell.NestedArea);
            }
        }
        else if (PlayerStats.Instance.IsInNestedArea)
        {
            playerPosition = PlayerStats.Instance.MainMapPosition;
            UpdateMapDisplay();

            if (PlayerStats.Instance.CurrentNestedArea != null)
            {
                UpdateNestedMapDisplay(PlayerStats.Instance.CurrentNestedArea);
            }
        }

        if (PlayerStats.Instance.SwapOutputs && !swapPerformed)
        {
            SwapMapDisplays();
            swapPerformed = true;
        }
        else if (!PlayerStats.Instance.SwapOutputs && swapPerformed)
        {
            SwapMapDisplays();
            swapPerformed = false;
        }
    }

    private void ClearNestedMapDisplay()
    {
        nestedMapDisplayText.text = string.Empty;
    }

    public void UpdateMapDisplay()
    {
        mapTextBuilder.Clear();  // Clear previous content

        int startX, startY, endX, endY;

        // Always show a fixed region or full map based on settings
        if (GameManager.Instance.showFullMap)
        {
            startX = 0;
            startY = 0;
            endX = mapGenerator.width;
            endY = mapGenerator.height;
        }
        else
        {
            int regionSize = 25;  // Define a fixed region size
            int regionsPerRow = mapGenerator.width / regionSize;
            int regionRow = PlayerStats.Instance.CurrentRegionNumber / regionsPerRow;
            int regionCol = PlayerStats.Instance.CurrentRegionNumber % regionsPerRow;

            startX = regionCol * regionSize;
            startY = regionRow * regionSize;
            endX = startX + regionSize;
            endY = startY + regionSize;
        }

        // Render the map without zoom
        for (int y = endY - 1; y >= startY; y--)
        {
            for (int x = startX; x < endX; x++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                Cell currentCell = mapGenerator.map[x, y];

                string symbol;

                if (playerController.playerPosition.x == x && playerController.playerPosition.y == y && PlayerStats.Instance.IsInMainMap)
                {
                    symbol = "X ";  // Player position
                }
                else if (!currentCell.SeenByPlayer)
                {
                    symbol = "<color=#FFFFFF>?</color> ";  // Unexplored area
                }
                else
                {
                    TerrainType terrainToDisplay = currentCell.TerrainToDisplay ?? currentCell.Terrain;
                    symbol = GetColoredSymbolForTerrain(terrainToDisplay);

                    if (GameManager.Instance.highlightPOI && (currentCell.HasLandmark || currentCell.HasDungeon || currentCell.HasCave || currentCell.HasVillage || currentCell.HasCamp))
                    {
                        symbol = "<color=#FFFFFF>i</color>"; // Highlight points of interest
                    }

                    symbol += " "; // Space for consistent formatting
                }

                mapTextBuilder.Append(symbol);
            }
            mapTextBuilder.AppendLine();  // New line at the end of each row
        }

        // Output to the appropriate text display
        if (PlayerStats.Instance.SwapOutputs)
        {
            nestedMapDisplayText.text = mapTextBuilder.ToString();
        }
        else
        {
            mapDisplayText.text = mapTextBuilder.ToString();
        }
    }

    public void UpdateNestedMapDisplay(INestedArea nestedArea)
    {
        nestedMapTextBuilder.Clear();  // Clear previous content

        if (nestedArea == null || nestedArea.GetNestedMap() == null)
        {
            GenerateQuestionMarkGrid(9, 9);
            return;
        }

        Cell[,] nestedMap = nestedArea.GetNestedMap();
        int nestedWidth = nestedMap.GetLength(0);
        int nestedHeight = nestedMap.GetLength(1);

        for (int y = nestedHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < nestedWidth; x++)
            {
                Cell cell = nestedMap[x, y];
                if (PlayerStats.Instance.NestedMapPosition.x == x && PlayerStats.Instance.NestedMapPosition.y == y && PlayerStats.Instance.IsInNestedArea)
                {
                    nestedMapTextBuilder.Append("X ");
                }
                else if (cell.Objects.Count > 0)
                {
                    // First, try to find if there is any active object that is of type Character (or derived from it)
                    var characterObject = cell.Objects.FirstOrDefault(obj => obj.IsActive && obj is Character);
                    if (characterObject != null)
                    {
                        // If a character object is found, show it first
                        string characterDisplay = GetColoredSymbol(characterObject.Symbol, characterObject.Color);
                        nestedMapTextBuilder.Append($"{characterDisplay} ");
                    }
                    else
                    {
                        // Otherwise, fall back to showing the first active object
                        var activeObject = cell.Objects.FirstOrDefault(obj => obj.IsActive);
                        if (activeObject != null)
                        {
                            string objectDisplay = GetColoredSymbol(activeObject.Symbol, activeObject.Color);
                            nestedMapTextBuilder.Append($"{objectDisplay} ");
                        }
                    }
                }
                else if (cell.canBeSeenByNPC && PlayerController.Instance.IsShiftHeld)
                {
                    nestedMapTextBuilder.Append("* ");
                }
                else
                {
                    string symbol = GetColoredSymbolForTerrain(cell.Terrain);
                    nestedMapTextBuilder.Append($"{symbol} ");
                }
            }
            nestedMapTextBuilder.AppendLine();
        }

        if (PlayerStats.Instance.SwapOutputs)
        {
            mapDisplayText.text = nestedMapTextBuilder.ToString();
        }
        else
        {
            nestedMapDisplayText.text = nestedMapTextBuilder.ToString();
        }
    }

    public void UpdateSubterraneanMapDisplay()
    {
        subterraneanMapTextBuilder.Clear();  // Clear previous content

        int startX, startY, endX, endY;

        if (GameManager.Instance.showFullMap)
        {
            startX = 0;
            startY = 0;
            endX = mapGenerator.width;
            endY = mapGenerator.height;
        }
        else
        {
            int regionSize = 25;
            int regionsPerRow = mapGenerator.width / regionSize;
            int regionRow = PlayerStats.Instance.CurrentRegionNumber / regionsPerRow;
            int regionCol = PlayerStats.Instance.CurrentRegionNumber % regionsPerRow;

            startX = regionCol * regionSize;
            startY = regionRow * regionSize;
            endX = startX + regionSize;
            endY = startY + regionSize;

            if (isMapZoomed)
            {
                startX = Mathf.Clamp(playerPosition.x - visibleAreaSize / 2 * zoomLevel, startX, endX - 1);
                startY = Mathf.Clamp(playerPosition.y - visibleAreaSize / 2 * zoomLevel, startY, endY - 1);
                endX = Mathf.Clamp(startX + visibleAreaSize * zoomLevel, startX, endX);
                endY = Mathf.Clamp(startY + visibleAreaSize * zoomLevel, startY, endY);
            }
        }

        for (int y = endY - 1; y >= startY; y--)
        {
            for (int x = startX; x < endX; x++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                Cell currentCell = mapGenerator.map[x, y];

                if (playerController.playerPosition.x == x && playerController.playerPosition.y == y && PlayerStats.Instance.IsInMainMap)
                {
                    subterraneanMapTextBuilder.Append("X ");
                }
                else if (!currentCell.SeenByPlayer)  // Check if unexplored
                {
                    subterraneanMapTextBuilder.Append("<color=#FFFFFF>?</color> ");  // Unexplored area
                }
                else
                {
                    TerrainType subterraneanTerrain = currentCell.SubterraneanTerrain ?? TerrainType.Default;
                    string symbol = GetColoredSymbolForTerrain(subterraneanTerrain);
                    subterraneanMapTextBuilder.Append($"{symbol} ");
                }
            }
            subterraneanMapTextBuilder.AppendLine();
        }

        mapDisplayText.text = subterraneanMapTextBuilder.ToString();
    }


    public void UpdateMagicMapDisplay()
    {
        magicMapTextBuilder.Clear();  // Clear previous content

        int startX, startY, endX, endY;

        if (GameManager.Instance.showFullMap)
        {
            startX = 0;
            startY = 0;
            endX = mapGenerator.width;
            endY = mapGenerator.height;
        }
        else
        {
            int regionSize = 25;
            int regionsPerRow = mapGenerator.width / regionSize;
            int regionRow = PlayerStats.Instance.CurrentRegionNumber / regionsPerRow;
            int regionCol = PlayerStats.Instance.CurrentRegionNumber % regionsPerRow;

            startX = regionCol * regionSize;
            startY = regionRow * regionSize;
            endX = startX + regionSize;
            endY = startY + regionSize;

            if (isMapZoomed)
            {
                startX = Mathf.Clamp(playerPosition.x - visibleAreaSize / 2 * zoomLevel, startX, endX - 1);
                startY = Mathf.Clamp(playerPosition.y - visibleAreaSize / 2 * zoomLevel, startY, endY - 1);
                endX = Mathf.Clamp(startX + visibleAreaSize * zoomLevel, startX, endX);
                endY = Mathf.Clamp(startY + visibleAreaSize * zoomLevel, startY, endY);
            }
        }

        for (int y = endY - 1; y >= startY; y--)
        {
            for (int x = startX; x < endX; x++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                Cell currentCell = mapGenerator.map[x, y];

                if (playerController.playerPosition.x == x && playerController.playerPosition.y == y && PlayerStats.Instance.IsInMainMap)
                {
                    magicMapTextBuilder.Append("X ");
                }
                else if (!currentCell.SeenByPlayer)  // Check if the cell hasn't been explored yet
                {
                    magicMapTextBuilder.Append("<color=#FFFFFF>?</color> ");  // Unexplored area
                }
                else
                {
                    MagicLevel magicLevel = currentCell.MagicLevel;
                    string symbol = GetSymbolForMagicLevel(magicLevel);
                    magicMapTextBuilder.Append($"{symbol} ");
                }
            }
            magicMapTextBuilder.AppendLine();
        }

        mapDisplayText.text = magicMapTextBuilder.ToString();
    }

    public void UpdateFactionMapDisplay()
    {
        factionMapTextBuilder.Clear();  // Clear previous content

        int startX, startY, endX, endY;

        if (GameManager.Instance.showFullMap)
        {
            startX = 0;
            startY = 0;
            endX = mapGenerator.width;
            endY = mapGenerator.height;
        }
        else
        {
            int regionSize = 25;
            int regionsPerRow = mapGenerator.width / regionSize;
            int regionRow = PlayerStats.Instance.CurrentRegionNumber / regionsPerRow;
            int regionCol = PlayerStats.Instance.CurrentRegionNumber % regionsPerRow;

            startX = regionCol * regionSize;
            startY = regionRow * regionSize;
            endX = startX + regionSize;
            endY = startY + regionSize;

            if (isMapZoomed)
            {
                startX = Mathf.Clamp(playerPosition.x - visibleAreaSize / 2 * zoomLevel, startX, endX - 1);
                startY = Mathf.Clamp(playerPosition.y - visibleAreaSize / 2 * zoomLevel, startY, endY - 1);
                endX = Mathf.Clamp(startX + visibleAreaSize * zoomLevel, startX, endX);
                endY = Mathf.Clamp(startY + visibleAreaSize * zoomLevel, startY, endY);
            }
        }

        for (int y = endY - 1; y >= startY; y--)
        {
            for (int x = startX; x < endX; x++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                Cell currentCell = mapGenerator.map[x, y];

                string symbol;

                // Player position
                if (playerController.playerPosition.x == x && playerController.playerPosition.y == y && PlayerStats.Instance.IsInMainMap)
                {
                    factionMapTextBuilder.Append("X ");
                }
                else if (!currentCell.SeenByPlayer)  // Check if unexplored
                {
                    factionMapTextBuilder.Append("<color=#FFFFFF>?</color> ");  // Unexplored area
                }
                else
                {
                    // Get the terrain symbol first (as char)
                    char terrainSymbol = GetSymbolForTerrain(currentCell.TerrainToDisplay ?? currentCell.Terrain);

                    // Now apply faction-related colors to the terrain symbol
                    if (currentCell.HasVillage && currentCell.Village != null)
                    {
                        symbol = GetColoredSymbol(terrainSymbol, currentCell.Village.Faction.FactionColour);  // Village faction color
                    }
                    else if (currentCell.IsOwned)
                    {
                        if (currentCell.OwnedByFaction != null)
                        {
                            symbol = GetColoredSymbol(terrainSymbol, currentCell.OwnedByFaction.FactionColour);  // Owned by faction color
                        }
                        else
                        {
                            symbol = GetColoredSymbol(terrainSymbol, "#FFFF00");  // Yellow for owned without faction
                        }
                    }
                    else
                    {
                        symbol = GetColoredSymbol(terrainSymbol, "#000000");  // Black for not owned
                    }

                    factionMapTextBuilder.Append($"{symbol} ");
                }
            }
            factionMapTextBuilder.AppendLine();
        }

        mapDisplayText.text = factionMapTextBuilder.ToString();
    }

    public void UpdateWeatherMapDisplay()
    {
        weatherMapTextBuilder.Clear();  // Clear previous content

        int startX, startY, endX, endY;

        if (GameManager.Instance.showFullMap)
        {
            startX = 0;
            startY = 0;
            endX = mapGenerator.width;
            endY = mapGenerator.height;
        }
        else
        {
            int regionSize = 25;
            int regionsPerRow = mapGenerator.width / regionSize;
            int regionRow = PlayerStats.Instance.CurrentRegionNumber / regionsPerRow;
            int regionCol = PlayerStats.Instance.CurrentRegionNumber % regionsPerRow;

            startX = regionCol * regionSize;
            startY = regionRow * regionSize;
            endX = startX + regionSize;
            endY = startY + regionSize;

            if (isMapZoomed)
            {
                startX = Mathf.Clamp(playerPosition.x - visibleAreaSize / 2 * zoomLevel, startX, endX - 1);
                startY = Mathf.Clamp(playerPosition.y - visibleAreaSize / 2 * zoomLevel, startY, endY - 1);
                endX = Mathf.Clamp(startX + visibleAreaSize * zoomLevel, startX, endX);
                endY = Mathf.Clamp(startY + visibleAreaSize * zoomLevel, startY, endY);
            }
        }

        for (int y = endY - 1; y >= startY; y--)
        {
            for (int x = startX; x < endX; x++)
            {
                Vector2Int cellPosition = new Vector2Int(x, y);
                Cell currentCell = mapGenerator.map[x, y];

                if (playerController.playerPosition.x == x && playerController.playerPosition.y == y)
                {
                    weatherMapTextBuilder.Append("X ");
                }
                else if (!currentCell.SeenByPlayer)  // Check if unexplored
                {
                    weatherMapTextBuilder.Append("<color=#FFFFFF>?</color> ");
                }
                else
                {
                    WeatherType weather = currentCell.CurrentWeather;
                    string symbol = GetSymbolForWeather(weather);
                    weatherMapTextBuilder.Append($"{symbol} ");
                }
            }
            weatherMapTextBuilder.AppendLine();  // To ensure each row of symbols starts on a new line
        }

        mapDisplayText.text = weatherMapTextBuilder.ToString();
    }


    public void GenerateQuestionMarkGrid(int width, int height)
    {
        StringBuilder gridBuilder = new StringBuilder();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                gridBuilder.Append("? ");
            }
            gridBuilder.AppendLine();
        }

        if (PlayerStats.Instance.SwapOutputs)
        {
            mapDisplayText.text = gridBuilder.ToString();
        }
        else
        {
            nestedMapDisplayText.text = gridBuilder.ToString();
        }
    }

    public NPCGroup FindNPCGroupAtCell(Vector2Int cellPosition)
    {
        if (npcManager != null)
        {
            return npcManager.FindNPCGroupAtPosition(cellPosition);
        }
        return null;
    }

    public NPC FindNPCAtCell(Vector2Int cellPosition)
    {
        if (npcManager != null)
        {
            var npcs = npcManager.FindNPCsAtCell(cellPosition);
            return npcs.FirstOrDefault(npc => npc.IsActive);
        }
        return null;
    }

    private char GetSymbolForTerrain(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.Land:
            case TerrainType.Land1:
            case TerrainType.Land2:
            case TerrainType.Land3:
            case TerrainType.Plains:
                return 'L';
            case TerrainType.Road:
                return 'R';
            case TerrainType.Forest:
            case TerrainType.Forest1:
            case TerrainType.Forest2:
            case TerrainType.Forest3:
                return 'F'; 
            case TerrainType.Bridge:
                return 'B';
            case TerrainType.River:
                return 'R';
            case TerrainType.Water:
                return 'W';
            case TerrainType.Village:
                return 'V';
            case TerrainType.Sand:
                return 'S';
            case TerrainType.Desert:
            case TerrainType.Dirt:
                return 'D';
            case TerrainType.Stone:
                return 'S';
            case TerrainType.Mountain:
                return 'M';
            case TerrainType.MountainPeak:
                return 'P';
            case TerrainType.Swamp:
                return 'S';
            case TerrainType.Glade:
                return 'G';
            case TerrainType.Hall:
                return 'H';
            case TerrainType.Grove:
                return 'E';
            case TerrainType.Camp:
                return 'C';
            case TerrainType.Cave:
                return 'C';
            case TerrainType.Snow:
                return ' ';
            case TerrainType.TilledSoil:
                return 't';
            case TerrainType.Tundra:
                return 'T';
            case TerrainType.Volcano:
                return 'V';
            case TerrainType.Saltflat:
                return 'S';
            case TerrainType.Ice:
                return 'I';
            case TerrainType.Lake:
                return 'L';
            case TerrainType.Plank:
                return 'P';  // Plank symbol
            case TerrainType.Path:
            case TerrainType.Path1:
                return 'P';  // Path1 symbol
            case TerrainType.Path2:
                return 'P';  // Path2 symbol
            case TerrainType.Path3:
                return 'P';  // Path3 symbol
            case TerrainType.Slate:
                return 'S';  // Slate symbol
            case TerrainType.SandDesert:
                return 'D';  // Desert sand symbol
            case TerrainType.SandBeach:
                return 'B';  // Beach sand symbol
            case TerrainType.PlayerStart:
                return 'X';
            case TerrainType.Test:
                return 'T';
            case TerrainType.None:
            case TerrainType.Default:
                return '1';
            default:
                return '1';
        }
    }

    private string GetColoredSymbolForTerrain(TerrainType terrain)
    {
        char symbol = GetSymbolForTerrain(terrain);
        string colorCode;

        bool isWinterOrPolar = TimeManager.Instance.currentSeason == Season.Winter || GameManager.Instance.climate == Climate.Polar;
        bool isAutumnOrArid = TimeManager.Instance.currentSeason == Season.Autumn || GameManager.Instance.climate == Climate.Arid;
        bool isSummerOrTropical = TimeManager.Instance.currentSeason == Season.Summer || GameManager.Instance.climate == Climate.Tropical;

        switch (terrain)
        {
            case TerrainType.Land:
            case TerrainType.Land1:
                if (isWinterOrPolar)
                    colorCode = "#B0E0E6"; // LightSteelBlue for winter/polar
                else if (isAutumnOrArid)
                    colorCode = "#FFD700"; // Golden yellow for autumn/arid
         //       else if (isSummerOrTropical)
       //             colorCode = "#32CD32"; // LimeGreen for summer/tropical
                else
                    colorCode = "#90EE90"; // LightGreen for default
                break;
            case TerrainType.Land2:
                if (isWinterOrPolar)
                    colorCode = "#ADD8E6"; // Lighter blue for Land2 in winter/polar
                else if (isAutumnOrArid)
                    colorCode = "#FFB347"; // Darker orange for autumn/arid
         //       else if (isSummerOrTropical)
          //          colorCode = "#00FF7F"; // SpringGreen for summer/tropical
                else
                    colorCode = "#70D870"; // Default summer color for Land2
                break;
            case TerrainType.Land3:
                if (isWinterOrPolar)
                    colorCode = "#87CEFA"; // Lighter sky blue for Land3 in winter
                else if (isAutumnOrArid)
                    colorCode = "#FF8C00"; // DarkOrange for autumn/arid
      //          else if (isSummerOrTropical)
       //             colorCode = "#3CB371"; // MediumSeaGreen for summer/tropical
                else
                    colorCode = "#50C850"; // Default green for Land3
                break;
            case TerrainType.Plains:
                colorCode = "#98FB98"; // PaleGreen
                break;
            case TerrainType.Forest:
            case TerrainType.Forest2:
                if (isWinterOrPolar)
                    colorCode = "#4682B4"; // SteelBlue for winter forest
                else if (isAutumnOrArid)
                    colorCode = "#CD853F"; // Peru (brownish) for autumn/arid forest
   //             else if (isSummerOrTropical)
     //               colorCode = "#2E8B57"; // SeaGreen for summer/tropical forest
                else
                    colorCode = "#006400"; // DarkGreen for default forest
                break;
            case TerrainType.Forest1:
                if (isWinterOrPolar)
                    colorCode = "#87CEEB"; // LightSkyBlue for winter forest
              else if (isAutumnOrArid)
                   colorCode = "#DEB887"; // BurlyWood for autumn/arid forest
   //             else if (isSummerOrTropical)
   //                 colorCode = "#66CDAA"; // MediumAquamarine for summer/tropical forest
                else
                    colorCode = "#228B22"; // ForestGreen for default
                break;
            case TerrainType.Forest3: // Evergreen (no seasonal color change)
                colorCode = "#004d00"; // Always dark green (evergreen)
                break;
            case TerrainType.Road:
                colorCode = "#FFFF00"; // Yellow
                break;
            case TerrainType.Bridge:
                colorCode = "#A0522D"; // Sienna
                break;
            case TerrainType.River:
                colorCode = "#ADD8E6"; // LightBlue
                break;
            case TerrainType.Water:
                colorCode = "#0000FF"; // Blue
                break;
            case TerrainType.Lake:
                colorCode = "#4682B4"; // SteelBlue
                break;
            case TerrainType.Village:
                colorCode = "#1A1A1A"; // Very Dark Grey
                break;
            case TerrainType.Sand:
                colorCode = "#FFFACD"; // LemonChiffon
                break;
            case TerrainType.Desert:
                colorCode = "#EDC9AF"; // DesertSand
                break;
            case TerrainType.Dirt:
                colorCode = "#A0522D"; // Sienna
                break;
            case TerrainType.Mountain:
            case TerrainType.Stone:
                colorCode = "#808080"; // Grey
                break;
            case TerrainType.MountainPeak:
                colorCode = "#F0F0FF"; // GhostWhite
                break;
            case TerrainType.Swamp:
                colorCode = "#2E8B57"; // SeaGreen
                break;
            case TerrainType.Glade:
                colorCode = "#32CD32"; // LimeGreen
                break;
            case TerrainType.Hall:
                colorCode = "#404040"; // Dark Grey
                break;
            case TerrainType.Grove:
                colorCode = "#228B22"; // ForestGreen
                break;
            case TerrainType.Camp:
                colorCode = "#B8860B"; // DarkGolden
                break;
            case TerrainType.Snow:
                colorCode = "#F0FFFF"; // LightCyan
                break;
            case TerrainType.TilledSoil:
                colorCode = "#8B4513"; // SaddleBrown
                break;
            case TerrainType.Tundra:
                colorCode = isWinterOrPolar ? "#C0C0C0" : "#A0D0E0"; // Silver for a more frosty tundra
                break;
            case TerrainType.Volcano:
                colorCode = "#FF4500"; // OrangeRed
                break;
            case TerrainType.Saltflat:
                colorCode = "#F5F5F5"; // WhiteSmoke
                break;
            case TerrainType.Ice:
                colorCode = "#D8EEF6"; // A brighter, more crystalline blue for Ice
                break;
            case TerrainType.Plank:
                colorCode = "#D2B48C";  // Tan (wooden planks)
                break;
            case TerrainType.Path:
            case TerrainType.Path1:
                colorCode = "#A0522D";  // Sienna (Path1 - dirt path)
                break;
            case TerrainType.Path2:
                colorCode = "#8B4513";  // SaddleBrown (Path2 - more worn path)
                break;
            case TerrainType.Path3:
                colorCode = "#5C4033";  // Darker brown (Path3 - oldest path)
                break;
            case TerrainType.Slate:
                colorCode = "#708090";  // SlateGray (Slate ground)
                break;
            case TerrainType.SandDesert:
                colorCode = "#EDC9AF";  // DesertSand (Desert sand)
                break;
            case TerrainType.SandBeach:
                colorCode = "#FFFACD";  // LemonChiffon (Beach sand)
                break;
            case TerrainType.Test:
                colorCode = "#FFFFFF"; // White
                break;
            default:
                colorCode = "#FFFFFF"; // Default white
                break;
        }

        return GetColoredSymbol(symbol, colorCode);
    }

    private string GetSymbolForMagicLevel(MagicLevel magicLevel)
    {
        switch (magicLevel)
        {
            case MagicLevel.High:
                return GetColoredSymbol('H', "#800080"); // Strong purple for High magic
            case MagicLevel.Medium:
                return GetColoredSymbol('M', "#9370DB"); // Medium purple (MediumOrchid) for Medium magic
            case MagicLevel.Low:
                return GetColoredSymbol('L', "#DDA0DD"); // Light purple (Plum) for Low magic
            case MagicLevel.None:
            default:
                return GetColoredSymbol('o', "#E6E6FA"); // Very faint purple (Lavender) for No magic
        }
    }

    private string GetSymbolForWeather(WeatherType weather)
    {
        switch (weather)
        {
            case WeatherType.Sunny:
                return "<color=#FFFF00>Y</color>"; // Yellow sun symbol
            case WeatherType.Cloudy:
                return "<color=#808080>C</color>"; // Grey cloud symbol
            case WeatherType.Rainy:
                return "<color=#0000FF>R</color>"; // Blue umbrella symbol
            case WeatherType.Stormy:
                return "<color=#800080>S</color>"; // Purple lightning symbol
            case WeatherType.Snowy:
                return "<color=#FFFFFF>S</color>"; // White snowflake symbol
            case WeatherType.Blizzard:
                return "<color=#00FFFF>B</color>"; // Cyan snowstorm symbol
            default:
                return "<color=#FFFFFF>?</color>"; // Default unknown symbol
        }
    }

    private string GetColoredSymbol(char symbol, string colorName)
    {
        if (ColourPool.AllColours.TryGetValue(colorName, out string hexColor))
        {
            return $"<color={hexColor}>{symbol}</color>";
        }
        else if (ColourPool.IsValidHexColour(colorName))
        {
            return $"<color={colorName}>{symbol}</color>";
        }
        else
        {
            return $"{symbol}";
        }
    }

    public void IncreaseZoomLevel()
    {
        if (zoomLevel < maxZoomLevel)
        {
            zoomLevel++;
            UpdateMapDisplay();
        }
    }

    public void DecreaseZoomLevel()
    {
        if (zoomLevel > minZoomLevel)
        {
            zoomLevel--;
            UpdateMapDisplay();
        }
    }

    private void SwapMapDisplays()
    {
        string temp = mapDisplayText.text;
        mapDisplayText.text = nestedMapDisplayText.text;
        nestedMapDisplayText.text = temp;
    }
}
