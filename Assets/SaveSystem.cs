using UnityEngine;
using System.IO;

public class SaveSystem : MonoBehaviour
{
    public void SaveMap(Cell[,] map, int width, int height)
    {
        MapData mapData = new MapData();
        mapData.width = width;
        mapData.height = height;
        mapData.cells = new CellData[width * height];

        // Convert each cell to serializable data
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Cell cell = map[x, y];
                mapData.cells[y * width + x] = new CellData(cell.Coordinates, cell.Terrain, cell.hasNestedArea);
            }
        }

        // Serialize map data to JSON
        string json = JsonUtility.ToJson(mapData);

        // Save JSON to file
        string savePath = GetSavePath();
        File.WriteAllText(savePath, json);

        Debug.Log("Map saved to: " + savePath);
    }

    public MapData LoadMap()
    {
        string savePath = GetSavePath();
        if (File.Exists(savePath))
        {
            // Read JSON from file
            string json = File.ReadAllText(savePath);

            // Deserialize JSON to map data
            MapData mapData = JsonUtility.FromJson<MapData>(json);
            Debug.Log("Map loaded from: " + savePath);
            return mapData;
        }
        else
        {
            Debug.LogWarning("No save file found.");
            return null;
        }
    }

    private string GetSavePath()
    {
        // Get path for saving/loading files (platform-dependent)
        string saveDirectory = Application.persistentDataPath;
        string saveFileName = "mapSave.json";
        return Path.Combine(saveDirectory, saveFileName);
    }
}

[System.Serializable]
public class MapData
{
    public int width;
    public int height;
    public CellData[] cells;
}

[System.Serializable]
public class CellData
{
    public Vector2Int coordinates;
    public TerrainType terrain;
    public bool hasNestedArea;

    public CellData(Vector2Int coordinates, TerrainType terrain, bool hasNestedArea)
    {
        this.coordinates = coordinates;
        this.terrain = terrain;
        this.hasNestedArea = hasNestedArea;
    }
}
