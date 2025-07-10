using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CaveGenerator : MonoBehaviour
{
    private static CaveGenerator instance;
    public static CaveGenerator Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<CaveGenerator>();
                if (instance == null)
                {
                    var obj = new GameObject("CaveGenerator");
                    instance = obj.AddComponent<CaveGenerator>();
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

    public void GenerateAndAssignCaves(int numberOfCaves)
    {
        UnityEngine.Random.InitState(GameManager.Instance.GameSeed);

        GameDebugger.Instance.LogInfo($"Generating {numberOfCaves} caves in the subterranean layer...");

        List<Cell> allCells = GetUndergroundCells().Where(IsValidCaveCell).ToList();

        if (allCells == null || allCells.Count == 0)
        {
            GameDebugger.Instance.LogWarning("No valid underground cells available to assign caves.");
            return;
        }

        int cavesCreated = 0;

        for (int i = 0; i < numberOfCaves; i++)
        {
            if (allCells.Count == 0) break;

            int randomIndex = UnityEngine.Random.Range(0, allCells.Count);
            var startCell = allCells[randomIndex];

            GenerateCaveFromPoint(startCell);
            cavesCreated++;

            allCells.RemoveAt(randomIndex);
        }

        GameDebugger.Instance.LogInfo($"Total caves actually created: {cavesCreated}");
    }

    private void GenerateCaveFromPoint(Cell startCell)
    {
        Queue<Cell> caveFrontier = new Queue<Cell>();
        HashSet<Cell> caveCells = new HashSet<Cell>();

        caveFrontier.Enqueue(startCell);
        caveCells.Add(startCell);
        startCell.SubterraneanTerrain = TerrainType.Cave; // Mark as underground cave
        startCell.AddTag(ref startCell.EnvironmentalTagFlags, EnvironmentalTags.Cave);

        int caveSize = UnityEngine.Random.Range(10, 30); // Adjust size range

        while (caveFrontier.Count > 0 && caveCells.Count < caveSize)
        {
            Cell current = caveFrontier.Dequeue();

            Vector2Int[] directions = {
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 0), new Vector2Int(-1, 0)
            };

            foreach (var dir in directions)
            {
                Vector2Int neighborPos = current.Coordinates + dir;
                if (!MapGenerator.Instance.IsPositionValid(neighborPos)) continue;

                Cell neighbor = MapGenerator.Instance.GetCell(neighborPos);

                if (!caveCells.Contains(neighbor) && neighbor.SubterraneanTerrain != TerrainType.Water)
                {
                    if (UnityEngine.Random.value < 0.6f) // 60% chance to expand cave
                    {
                        caveFrontier.Enqueue(neighbor);
                        caveCells.Add(neighbor);
                        neighbor.SubterraneanTerrain = TerrainType.Cave;
                        neighbor.AddTag(ref neighbor.EnvironmentalTagFlags, EnvironmentalTags.Cave);
                    }
                }
            }
        }

        AssignCaveType(startCell, caveCells);
        GameDebugger.Instance.LogInfo($"Underground cave generated with {caveCells.Count} cells.");
    }

    private void AssignCaveType(Cell startCell, HashSet<Cell> caveCells)
    {
        CaveType caveType = AssignRandomCaveType();
        int caveID = GameManager.Instance.GetCaveID();

        CaveCreationData caveData = new CaveCreationData(caveID)
        {
            CaveCellID = startCell.CellID,
            CaveType = caveType
        };

        PermaLists.Instance.CaveCreationDataList.Add(caveData);

        foreach (Cell cell in caveCells)
        {
            cell.CaveID = caveID;
        }

        GameDebugger.Instance.LogInfo($"Cave ID {caveID} assigned as {caveType}.");
    }

    private CaveType AssignRandomCaveType()
    {
        float randomValue = UnityEngine.Random.value;

        if (randomValue < 0.1f) return CaveType.Empty;
        else if (randomValue < 0.2f) return CaveType.Collapsed;
        else if (randomValue < 0.35f) return CaveType.AbandonedCamp;
        else if (randomValue < 0.5f) return CaveType.ActiveCamp;
        else if (randomValue < 0.65f) return CaveType.TreasureCave;
        else if (randomValue < 0.8f) return CaveType.MonsterLair;
        else return CaveType.FungiCave;
    }

    private List<Cell> GetUndergroundCells()
    {
        return MapGenerator.Instance.allCells
            .Where(c => c.SubterraneanTerrain != TerrainType.Water)
            .ToList();
    }

    private bool IsValidCaveCell(Cell cell)
    {
        if (cell.HasTag(cell.EnvironmentalTagFlags, EnvironmentalTags.Cave)) return false;
        if (MapGenerator.Instance.IsPositionAtEdge(cell.Coordinates)) return false;
        return true;
    }
}

// Enum for different cave types
public enum CaveType
{
    Empty,
    Collapsed,
    AbandonedCamp,
    ActiveCamp,
    TreasureCave,
    MonsterLair,
    FungiCave
}
