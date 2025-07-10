using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class CaveNestedArea : BaseNestedArea
{
    public new Vector2Int EntrancePosition { get; set; }
    public bool HasExit { get; set; }
    public Vector2Int CampFirePosition { get; set; }
    public int CaveID;
    private CaveCreationData caveData;  // Store the cave data

    // Constructor
    public CaveNestedArea(Vector2Int entrancePosition, int caveID)
    {
        Type = NestedAreaType.Cave;  // Distinguish this as a Cave type
        EntrancePosition = entrancePosition;
        CaveID = caveID;

        Initialize();

        // Fetch and apply cave data
        ApplyCaveData();

        // Set all cells to indoors
        SetAllCellsIndoors();

        // Create stone walls around the map
        CreateWallsAroundMap("CaveWall");

        PlaceFungiNearWallsTreesOrRocks(6);
    }

    // Fetch and apply the cave data from the CaveGenerator or a related system
    private void ApplyCaveData()
    {
        // Retrieve cave data from CaveGenerator or a similar system
        caveData = PermaLists.Instance.CaveCreationDataList.FirstOrDefault(c => c.CaveID == CaveID);

        if (caveData == null)
        {
            Debug.LogError($"Cave data not found for cave with ID {CaveID}");
            return;
        }

        // Handle the cave type
        switch (caveData.CaveType)
        {
            case CaveType.Empty:
                Debug.Log($"Cave {CaveID} is empty.");
                // No special objects or features
                break;

            case CaveType.Collapsed:
                Debug.Log($"Cave {CaveID} is collapsed.");
                BlockCaveEntrance();
                break;

            case CaveType.AbandonedCamp:
                Debug.Log($"Cave {CaveID} is an abandoned camp.");
                AddCampFireToCave();
                break;

            case CaveType.ActiveCamp:
                Debug.Log($"Cave {CaveID} is an active camp.");
                AddCampFireToCave();
                // Optionally, you could add NPCs or other features
                break;

            case CaveType.TreasureCave:
                Debug.Log($"Cave {CaveID} contains treasure.");
                break;

            case CaveType.MonsterLair:
                Debug.Log($"Cave {CaveID} is a monster lair.");
                break;

            case CaveType.FungiCave:
                Debug.Log($"Cave {CaveID} is filled with fungi.");
                PlaceFungiInCave();
                break;
        }
    }

    // Block parts of the cave for collapsed caves
    private void BlockCaveEntrance()
    {
        Cell entranceCell = GetCellAtPosition(EntrancePosition);
        if (entranceCell != null)
        {
            entranceCell.isPassable = false;  // Make the entrance impassable
            Debug.Log($"Cave {CaveID}'s entrance is blocked due to collapse.");
        }
    }

    // Add a campfire to active camps
    private void AddCampFireToCave()
    {
        IInteractable campFire = new Campfire();
        Cell campFireCell = GetCellAtPosition(CampFirePosition);

        if (campFireCell != null)
        {
            campFireCell.Objects.Add(campFire);
            campFireCell.isPassable = false;
        }
        else
        {
            Debug.LogError("CampFire cell is null. Cannot add CampFire.");
        }
    }

    // Place fungi in fungi caves
    private void PlaceFungiInCave()
    {
        // Example logic for placing fungi
        PlaceFungiNearWallsTreesOrRocks(10);  // Place more fungi in a fungi cave
        Debug.Log($"Fungi placed in cave {CaveID}.");
    }

    // Initialize the nested area map
    public override void Initialize()
    {
        int nestedAreaID = GameManager.Instance.GetNestedAreaID();
        NestedAreaID = nestedAreaID;

        AreaMap = new Cell[Size, Size];

        // Initialize all cells as part of the cave
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                int cellID = GenerateUniqueCellID();
                AreaMap[x, y] = new Cell(cellID, x, y, TerrainType.Stone);  // Terrain is stone for a cave
            }
        }

        // Add UpwardStaircase to the entrance
        AddUpwardStaircaseToEntrance();
    }

    private void AddUpwardStaircaseToEntrance()
    {
        IInteractable upwardStaircase = new UpwardStaircase();  // Create a new upward staircase instance
        Cell entranceCell = GetCellAtPosition(EntrancePosition);

        if (entranceCell != null)
        {
            entranceCell.Objects.Add(upwardStaircase);  // Add staircase to the entrance
            HasExit = true;
        }
        else
        {
            Debug.LogError("Entrance cell is null. Cannot add UpwardStaircase.");
        }
    }

    public override bool IsValidPosition(Vector2Int position)
    {
        return position.x >= 0 && position.x < Size && position.y >= 0 && position.y < Size;
    }

    public override bool IsPassable(Vector2Int position)
    {
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
        // Ensure the new position is passable and within bounds
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
        // Logic for when the player exits the cave, if needed
    }

    // Set all cells in the cave as indoors
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
}
