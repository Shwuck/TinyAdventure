using System.IO;
using UnityEngine;

public class MapLogger : MonoBehaviour
{
    public MapGenerator mapGenerator;

    public void RunAllLogs()
    {
        LogMap();
        LogAllCellDetails();
    }

    public void LogMap()
    {
        if (mapGenerator == null)
        {
            Debug.LogError("MapGenerator reference not set in MapLogger.");
            return;
        }

        string logPath;
        if (GameManager.Instance != null && GameManager.Instance.isDebugModeOn)
        {
            // Path targeting Project/Assets/Logs for Unity Editor
            logPath = Path.Combine(Application.dataPath, "Logs");
        }
        else
        {
            // Default log path for runtime/builds
            logPath = Path.Combine(Application.persistentDataPath, "Logs");
        }

        // Ensure the Logs directory exists
        Directory.CreateDirectory(logPath);

        string fileName = $"MapLog_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string filePath = Path.Combine(logPath, fileName);

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            for (int y = mapGenerator.height - 1; y >= 0; y--) // Start from the top for readability
            {
                string line = "";
                for (int x = 0; x < mapGenerator.width; x++)
                {
                    // Assuming the TerrainType enum and that each Cell knows its Terrain
                    char symbol = GetSymbolForTerrain(mapGenerator.map[x, y].Terrain);
                    line += symbol;
                }
                writer.WriteLine(line);
            }
        }

        Debug.Log($"Map logged to {filePath}");
    }


    // Convert TerrainType to a single character for logging
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
                return 'R'; // Bridge
            case TerrainType.Village:
                return 'V'; // Village
            case TerrainType.Sand:
                return 'S'; // Sand
            case TerrainType.Dirt:
                return 'D'; // Dirt
            // Add more cases as needed for different terrain types
            default:
                return ' '; // Unknown or unassigned terrain
        }
    }

    public void LogAllCellDetails()
    {
        if (mapGenerator == null)
        {
            Debug.LogError("MapGenerator reference not set in MapLogger.");
            return;
        }

        string logPath;
        if (GameManager.Instance != null && GameManager.Instance.isDebugModeOn)
        {
            // Path targeting Project/Assets/Logs for Unity Editor
            logPath = Path.Combine(Application.dataPath, "Logs");
        }
        else
        {
            // Default log path for runtime/builds
            logPath = Path.Combine(Application.persistentDataPath, "Logs");
        }

        // Ensure the Logs directory exists
        Directory.CreateDirectory(logPath);

        string fileName = $"AllCellDetails_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string filePath = Path.Combine(logPath, fileName);

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            // Iterate through every cell in the map
            for (int y = 0; y < mapGenerator.height; y++)
            {
                for (int x = 0; x < mapGenerator.width; x++)
                {
                    Cell cell = mapGenerator.map[x, y];
                    PrintCellDetails(cell, writer, 0);
                    writer.WriteLine(); // Add an empty line for better readability between cells
                }
            }
        }

        Debug.Log($"All cell details logged to {filePath}");
    }



    public static void PrintCellDetails(Cell cell, StreamWriter writer, int depth = 0)
    {
        string indent = new string(' ', depth * 4); 

        writer.WriteLine($"{indent}Cell Details:");
        writer.WriteLine($"{indent}Coordinates: {cell.Coordinates}");
        writer.WriteLine($"{indent}Terrain: {cell.Terrain}");
        writer.WriteLine($"{indent}Adjacent Cell Count: {cell.AdjacentCellCount}");
        writer.WriteLine($"{indent}Adjacent Cells:");
        foreach (var entry in cell.AdjacentCells)
        {
            writer.WriteLine($"{indent}  {entry.Key}: {(entry.Value.HasValue ? entry.Value.Value.ToString() : "null")}");
        }
        writer.WriteLine($"{indent}Is Player Present: {cell.isPlayerPresent}");
        writer.WriteLine($"{indent}Is Passable: {cell.isPassable}");
        writer.WriteLine($"{indent}Has Nested Area: {cell.hasNestedArea}");
        writer.WriteLine($"{indent}Nested Area Can Be Seen: {cell.nestedAreaCanBeSeen}");

        if (cell.NestedArea != null)
        {
            writer.WriteLine($"{indent}Nested Area Details:");
            PrintNestedAreaDetails(cell.NestedArea, writer, depth + 1);
        }

        writer.WriteLine($"{indent}Objects:");
        foreach (var obj in cell.Objects)
        {
            writer.WriteLine($"{indent}  {obj}");
        }

        writer.WriteLine($"{indent}Animals:");
        foreach (var animal in cell.Animals)
        {
            writer.WriteLine($"{indent}  {animal}");
        }

        writer.WriteLine($"{indent}Is NPC Group Present: {cell.isNPCGroupPresent}");
        writer.WriteLine($"{indent}Is NPC Present: {cell.isNPCPresent}");
        writer.WriteLine($"{indent}Is Fertile: {cell.isFertile}");
        writer.WriteLine($"{indent}Fertility Value: {cell.FertilityValue}");
    }

    private static void PrintNestedAreaDetails(INestedArea nestedArea, StreamWriter writer, int depth)
    {
        Cell[,] nestedMap = nestedArea.GetNestedMap();
        int rows = nestedMap.GetLength(0);
        int cols = nestedMap.GetLength(1);

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                if (nestedMap[i, j] != null)
                {
                    PrintCellDetails(nestedMap[i, j], writer, depth);
                }
            }
        }
    }

    public static void WriteCellDetailsToFile(Cell cell)
    {
        string filePath = Path.Combine(Application.dataPath, "Logs/CellDetailsLog.txt");

        using (StreamWriter writer = new StreamWriter(filePath, false))
        {
            PrintCellDetails(cell, writer);
        }

        Debug.Log($"Cell details written to {filePath}");
    }
}

