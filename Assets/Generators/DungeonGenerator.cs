using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class DungeonGenerator : MonoBehaviour
{
    private static DungeonGenerator instance;
    public static DungeonGenerator Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<DungeonGenerator>();
                if (instance == null)
                {
                    var obj = new GameObject("DungeonGenerator");
                    instance = obj.AddComponent<DungeonGenerator>();
                    DontDestroyOnLoad(obj);
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void GenerateAndAssignDungeons(int numberOfDungeons)
    {
        UnityEngine.Random.InitState(GameManager.Instance.GameSeed);

        GameDebugger.Instance.LogWarning($"Count of Dungeons to Assign: {numberOfDungeons}");

        List<Cell> allCells = GetAllCells().Where(IsValidDungeonCell).ToList();

        if (allCells == null || allCells.Count == 0)
        {
            GameDebugger.Instance.LogWarning("No cells available to assign dungeons.");
            return;
        }

        int dungeonsCreated = 0;

        for (int i = 0; i < numberOfDungeons; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, allCells.Count);
            var selectedCell = allCells[randomIndex];

            if (!selectedCell.HasDungeon)
            {
                selectedCell.HasDungeon = true;
                dungeonsCreated++;

                int dungeonID = GameManager.Instance.GetDungeonID();
                int totalDungeonLevels = UnityEngine.Random.Range(3, 13);
                var dungeonData = new DungeonCreationData(dungeonID, totalDungeonLevels);
                dungeonData.DungeonCellID = selectedCell.CellID;
                selectedCell.DungeonID = dungeonID;

                // **Coin flip to assign DungeonType**
                dungeonData.DungeonType = UnityEngine.Random.value < 0.5f ? "Skeleton" : "Zombie";

                PermaLists.Instance.DungeonCreationDataList.Add(dungeonData);
                GameDebugger.Instance.LogInfo($"Generated dungeon data with ID {dungeonID} at cell {selectedCell.CellID}, Type: {dungeonData.DungeonType}");
            }
            else
            {
                GameDebugger.Instance.LogWarning($"Cell {selectedCell.CellID} already has a dungeon.");
            }

            allCells.RemoveAt(randomIndex);
        }

        GameDebugger.Instance.LogInfo($"Total dungeons assigned: {numberOfDungeons}");
        GameDebugger.Instance.LogInfo($"Total dungeons actually created: {dungeonsCreated}");
        GameDebugger.Instance.LogInfo($"Current Dungeon Creation Data List Count: {PermaLists.Instance.DungeonCreationDataList.Count}");

        foreach (var data in PermaLists.Instance.DungeonCreationDataList)
        {
            GameDebugger.Instance.LogInfo($"Dungeon Creation Data - ID: {data.DungeonID}, Levels: {data.TotalDungeonLevels}, CellID: {data.DungeonCellID}, Type: {data.DungeonType}");
        }
    }

    public DungeonNestedArea GetDungeonByID(int dungeonID)
    {
        var dungeon = PermaLists.Instance.Dungeons.Find(d => d.NestedAreaID == dungeonID);
        if (dungeon == null)
        {
            Debug.LogError($"GetDungeonByID: Dungeon with ID {dungeonID} not found.");
        }
        else
        {
            Debug.Log($"GetDungeonByID: Retrieved dungeon with ID {dungeonID}");
        }
        return dungeon;
    }

    private List<Cell> GetAllCells()
    {
        var allCells = new List<Cell>();
        var terrainTypes = System.Enum.GetValues(typeof(TerrainType));

        foreach (TerrainType terrainType in terrainTypes)
        {
            var cellsOfTerrainType = MapGenerator.Instance.GetCellsByTerrain(terrainType);
            if (cellsOfTerrainType != null)
            {
                allCells.AddRange(cellsOfTerrainType);
            }
        }

        return allCells;
    }

    private bool IsValidDungeonCell(Cell cell)
    {
        if (cell.HasDungeon) return false;
        if (cell.Terrain == TerrainType.Water || cell.Terrain == TerrainType.River || cell.Terrain == TerrainType.Lake || cell.Terrain == TerrainType.Sand || cell.Terrain == TerrainType.Mountain || cell.Terrain == TerrainType.MountainPeak || cell.Terrain == TerrainType.Water) return false;
        if (MapGenerator.Instance.IsPositionAtEdge(cell.Coordinates)) return false;

        return true;
    }
}
