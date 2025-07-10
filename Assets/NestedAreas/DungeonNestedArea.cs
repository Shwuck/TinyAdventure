using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class DungeonNestedArea : BaseNestedArea
{
    public new Vector2Int EntrancePosition { get; set; }
    public bool HasExit { get; set; }
    public int DungeonLevel;
    public int DungeonRating;
    public int TotalDungeonLevels;
    public int DungeonID;
    public bool DungeonCleared;

    // Constructor
    public DungeonNestedArea(Vector2Int entrancePosition, int dungeonLevel, int dungeonID)
    {
        Type = NestedAreaType.Dungeon;
        EntrancePosition = entrancePosition;
        DungeonLevel = dungeonLevel;
        DungeonID = dungeonID;
        TotalDungeonLevels = GetTotalDungeonLevelsFromPermaLists(dungeonID);
        Initialize();

        // Call the method to set all cells to indoors after initialization
        SetAllCellsIndoors();

        CreateWallsAroundMap("StoneWall");
    }

    // Method to get TotalDungeonLevels from PermaLists based on DungeonID
    private int GetTotalDungeonLevelsFromPermaLists(int dungeonID)
    {
        DungeonCreationData dungeonData = PermaLists.Instance.DungeonCreationDataList
            .FirstOrDefault(d => d.DungeonID == dungeonID);

        if (dungeonData != null)
        {
            int totalDungeonLevelsToAdd = dungeonData.TotalDungeonLevels;
            Debug.Log("DungeonCreationData found for DungeonID: " + dungeonID + " adding TotalDungeonLevels of " + totalDungeonLevelsToAdd);
            return dungeonData.TotalDungeonLevels;
        }
        else
        {
            Debug.LogError("DungeonCreationData not found for DungeonID: " + dungeonID);
            return 0; // or a default value you prefer
        }
    }

    // Initialize the nested area map
    public override void Initialize()
    {
        int nestedAreaID = GameManager.Instance.GetNestedAreaID();
        NestedAreaID = nestedAreaID;

        AreaMap = new Cell[Size, Size];

        // Initialize all cells as Dungeon initially
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                int cellID = GenerateUniqueCellID(); // Generate a unique cell ID
                AreaMap[x, y] = new Cell(cellID, x, y, TerrainType.Stone); // Create a new Cell with the cellID parameter
            }
        }

        // Add the UpwardStaircase to the entrance cell
        AddUpwardStaircaseToEntrance();
        AddMonsters();

        // Add the DownwardStaircase if the current dungeon level is less than total dungeon levels
        if (DungeonLevel < TotalDungeonLevels)
        {
            Debug.Log("Current Level is " + DungeonLevel + ", Total levels are " + TotalDungeonLevels + ", therefore adding a downward staircase.");
            AddDownwardStaircase();
        }
        else
        {
            PlaceDungeonChest(DungeonLevel);
            Debug.Log("Current Level is " + DungeonLevel + ", Total levels are " + TotalDungeonLevels + ", therefore not adding a downward staircase.");
        }
    }

    private void AddUpwardStaircaseToEntrance()
    {
        IInteractable upwardStaircase = new UpwardStaircase(); // Create a new instance of UpwardStaircase
        Cell entranceCell = GetCellAtPosition(EntrancePosition);

        if (entranceCell != null)
        {
            entranceCell.Objects.Add(upwardStaircase); // Add the staircase to the entrance cell
            HasExit = true;
        }
        else
        {
            Debug.LogError("Entrance cell is null. Cannot add UpwardStaircase.");
        }
    }

    private void AddDownwardStaircase()
    {
        Vector2Int downwardStaircasePosition = GetRandomPosition();
        IInteractable downwardStaircase = new DownwardStaircase(); // Create a new instance of DownwardStaircase
        Cell staircaseCell = GetCellAtPosition(downwardStaircasePosition);

        if (staircaseCell != null)
        {
            staircaseCell.Objects.Add(downwardStaircase); // Add the staircase to the cell
        }
        else
        {
            Debug.LogError("Staircase cell is null. Cannot add DownwardStaircase.");
        }
    }

    private Vector2Int GetRandomPosition()
    {
        System.Random random = new System.Random();
        Vector2Int randomPos;
        int attempts = 0;

        do
        {
            randomPos = new Vector2Int(random.Next(0, Size), random.Next(0, Size));
            attempts++;
            if (attempts > 100) // Avoid infinite loops
            {
                Debug.LogWarning("Failed to find a valid spawn position, defaulting to entrance.");
                return EntrancePosition;
            }
        }
        while (!IsPassable(randomPos)); // Ensure position is walkable

        return randomPos;
    }

    public override bool IsValidPosition(Vector2Int position)
    {
        return position.x >= 0 && position.x < Size && position.y >= 0 && position.y < Size;
    }

    public override bool IsPassable(Vector2Int position)
    {
        if (position.x < 0 || position.x >= Size || position.y < 0 || position.y >= Size)
        {
            Debug.LogWarning($"IsPassable: Position {position} is OUT OF BOUNDS in {this.Type} (Size: {Size})");
            return false; // Prevents crash
        }

        return AreaMap[position.x, position.y].isPassable;
    }


    public override void UpdatePlayerPosition(Vector2Int newPosition)
    {
        Cell newCellPosition = GetCellAtPosition(newPosition);
        PlayerStats.Instance.UpdateCurrentCellID(newCellPosition.CellID);
        PlayerStats.Instance.UpdateParentNestedAreaID(newCellPosition.ParentAreaID);
    }

    public override void UpdateCharacterPosition(Character character, Vector2Int newPosition)
    {
        // Ensure the new position is within the bounds of the nested map
        if (IsPassable(newPosition))
        {
            Cell currentCell = GetCellAtPosition(character.NestedMapPosition);
            if (currentCell != null)
            {
                currentCell.Objects.Remove(character);
                currentCell.isPassable = true;
            }

            character.NestedMapPosition = newPosition;
            character.CurrentNestedArea = this;
            Cell newCell = GetCellAtPosition(newPosition);
            if (newCell != null)
            {
                newCell.Objects.Add(character);
                newCell.isPassable = false;
            }
        }
    }

    public override void UpdateNPCGroupPosition(NPCGroup npcGroup, Vector2Int newPosition)
    {
        foreach (NPC npc in npcGroup.NPCs)
        {
            UpdateCharacterPosition(npc, newPosition);
        }
    }

    public override Cell GetCellAtPosition(Vector2Int position)
    {
        if (position.x >= 0 && position.x < AreaMap.GetLength(0) && position.y >= 0 && position.y < AreaMap.GetLength(1))
        {
            return AreaMap[position.x, position.y];
        }
        return null;
    }

    public override void HandlePlayerExit(MapGenerator mapGenerator)
    {
        // Handle player exit logic, if needed
    }

    // Method to update isIndoors property for all cells
    public void SetAllCellsIndoors()
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                if (AreaMap[x, y] != null)
                {
                    AreaMap[x, y].isIndoors = true;
                }
            }
        }
    }

    #region Dungeon Monsters

    private void AddMonsters()
    {
        DungeonCreationData dungeonData = PermaLists.Instance.DungeonCreationDataList
            .FirstOrDefault(d => d.DungeonID == DungeonID);

        if (dungeonData == null)
        {
            Debug.LogError($"No DungeonCreationData found for DungeonID: {DungeonID}. Cannot add monsters.");
            return;
        }

        Debug.Log($"Dungeon ID: {DungeonID} | Type: {dungeonData.DungeonType} | Checking for monster spawns...");

        // Prevent duplication: Check if monsters already exist in this dungeon
        if (PlacedMonsters.Count > 0)
        {
            Debug.Log($"Monsters already exist in Dungeon Level {DungeonLevel}. Skipping spawn.");
            return;
        }

        if (dungeonData.DungeonType == "Skeleton" || dungeonData.DungeonType == "Zombie")
        {
            SpawnUndead();
        }
        else
        {
            Debug.Log($"No undead to spawn in Dungeon ID: {DungeonID} ({dungeonData.DungeonType}).");
        }
    }

    private void SpawnUndead()
    {
        DungeonCreationData dungeonData = PermaLists.Instance.DungeonCreationDataList
            .FirstOrDefault(d => d.DungeonID == DungeonID);

        if (dungeonData == null)
        {
            Debug.LogError($"No DungeonCreationData found for DungeonID: {DungeonID}. Cannot spawn undead.");
            return;
        }

        string dungeonType = dungeonData.DungeonType; // "Skeleton" or "Zombie"

        // Prevent duplicate spawns if undead are already present
        if (AreaMap.Cast<Cell>().Any(cell => cell.Objects.OfType<Monster>().Any()))
        {
            Debug.Log($"Undead already present in Dungeon Level {DungeonLevel}. Skipping additional spawns.");
            return;
        }

        // Determine the number of monsters based on DungeonLevel (minimum 1)
        int minMonsters = Mathf.Max(1, DungeonLevel - 2);
        int maxMonsters = DungeonLevel + 2;
        int numberOfMonsters = UnityEngine.Random.Range(minMonsters, maxMonsters + 1);

        Debug.Log($"Dungeon Level: {DungeonLevel} | Spawning {numberOfMonsters} {dungeonType}s in Dungeon ID: {DungeonID}");

        for (int i = 0; i < numberOfMonsters; i++)
        {
            Monster undead = (dungeonType == "Skeleton")
                ? MonsterGenerator.Instance.CreateSkeleton()
                : MonsterGenerator.Instance.CreateZombie();

            if (undead != null)
            {
                MonsterManager.Instance.PlaceMonster(this, undead);
            }
        }

        // Boss Monster on the final level
        if (DungeonLevel == TotalDungeonLevels)
        {
            Monster boss = (dungeonType == "Skeleton")
                ? MonsterGenerator.Instance.CreateSkeletonBoss()
                : MonsterGenerator.Instance.CreateZombieBoss();

            if (boss != null)
            {
                MonsterManager.Instance.PlaceMonster(this, boss);
                Debug.Log($"BOSS SPAWNED: {boss.MonsterName} in Dungeon ID: {DungeonID}!");
            }
        }
    }

    #endregion
}
