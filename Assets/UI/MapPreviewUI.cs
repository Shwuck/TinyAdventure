using UnityEngine;
using TMPro;
using System.Text;

public class MapPreviewUI : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public TMP_Text mapPreviewText;

    void Start()
    {
        DisplayMapPreview();
    }

    public void UpdateMapPreview()
    {
        mapGenerator.GenerateMap();
        DisplayMapPreview();
    }

    private void DisplayMapPreview()
    {
        StringBuilder mapTextBuilder = new StringBuilder();
        int width = mapGenerator.width;
        int height = mapGenerator.height;
        int regionSize = 25;

        int startX = Random.Range(0, width - regionSize);
        int startY = Random.Range(0, height - regionSize);
        int endX = startX + regionSize;
        int endY = startY + regionSize;

        if (!GameManager.Instance.MapGenerated)
        {
            for (int y = endY - 1; y >= startY; y--)
            {
                for (int x = startX; x < endX; x++)
                {
                    mapTextBuilder.Append("? ").Append(' ');
                }
                mapTextBuilder.AppendLine();
            }
        }
        else
        {
            var map = mapGenerator.map;

            for (int y = endY - 1; y >= startY; y--)
            {
                for (int x = startX; x < endX; x++)
                {
                    mapTextBuilder.Append(GetColoredSymbolForTerrain(map[x, y].Terrain)).Append(' ');
                }
                mapTextBuilder.AppendLine();
            }
        }

        string mapPreviewString = mapTextBuilder.ToString();
        mapPreviewText.text = mapPreviewString;
        GameManager.Instance.SetMapPreviewText(mapPreviewString);
    }

    private char GetSymbolForTerrain(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.Land:
                return 'L'; // Land
            case TerrainType.Road:
                return 'R'; // Road
            case TerrainType.Forest:
                return 'F'; // Forest
            case TerrainType.Bridge:
                return 'B'; // Bridge
            case TerrainType.River:
                return 'R'; // River
            case TerrainType.Water:
                return 'W'; // Water
            case TerrainType.Village:
                return 'V'; // Village
            case TerrainType.Sand:
                return 'S'; // Sand
            case TerrainType.Stone:
                return 'S'; // Stone
            case TerrainType.Dirt:
                return 'D'; // Dirt
            case TerrainType.Mountain:
                return 'M'; // Mountain
            case TerrainType.MountainPeak:
                return 'P'; // Mountain Peak
            case TerrainType.Swamp:
                return 'S'; // Swamp
            case TerrainType.Glade:
                return 'G'; // Glade
            case TerrainType.Hall:
                return 'H'; // Hall
            case TerrainType.Grove:
                return 'E'; // Grove
            case TerrainType.Camp:
                return 'C'; // Camp
            case TerrainType.Test:
                return 'T'; // Test
            default:
                return ' '; // Unknown or unassigned terrain
        }
    }

    private string GetColoredSymbolForTerrain(TerrainType terrain)
    {
        char symbol = GetSymbolForTerrain(terrain);
        string colorCode;

        switch (terrain)
        {
            case TerrainType.Land:
                colorCode = "#90EE90"; // Light green for land
                break;
            case TerrainType.Forest:
                colorCode = "#006400"; // Dark green for forest
                break;
            case TerrainType.Road:
                colorCode = "#FFFF00"; // Yellow for roads
                break;
            case TerrainType.Bridge:
                colorCode = "#A0522D"; // Brown for bridges
                break;
            case TerrainType.River:
                colorCode = "#0000FF"; // Blue for rivers
                break;
            case TerrainType.Water:
                colorCode = "#0000FF"; // Blue for water
                break;
            case TerrainType.Village:
                colorCode = "#1A1A1A"; // Dark grey for villages
                break;
            case TerrainType.Sand:
                colorCode = "#FFFACD"; // Pale yellow for sand
                break;
            case TerrainType.Dirt:
                colorCode = "#A0522D"; // Brown for dirt
                break;
            case TerrainType.Mountain:
                colorCode = "#808080"; // Grey for mountains
                break;
            case TerrainType.Stone:
                colorCode = "#808080"; // Grey for Stone
                break;
            case TerrainType.MountainPeak:
                colorCode = "#F0F0FF"; // Grey for mountains
                break;
            case TerrainType.Swamp:
                colorCode = "#2E8B57"; // Sea green for swamps
                break;
            case TerrainType.Glade:
                colorCode = "#32CD32"; // Lime green for forest glades
                break;
            case TerrainType.Hall:
                colorCode = "#404040"; // Dark grey for Hall
                break;
            case TerrainType.Grove:
                colorCode = "#32CD32"; // Bright green for Grove
                break;
            case TerrainType.Camp:
                colorCode = "#B8860B"; // Dusty yellow/brown for Camp
                break;
            case TerrainType.Test:
                colorCode = "#FFFFFF"; // White for Test
                break;
            default:
                colorCode = "#FFFFFF"; // Default, white
                break;
        }

        return GetColoredSymbol(symbol, colorCode);
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
}
