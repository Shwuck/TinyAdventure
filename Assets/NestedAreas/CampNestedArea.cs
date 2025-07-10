using System.Collections.Generic;
using UnityEngine;

public class CampNestedArea : BaseNestedArea
{
    public string CampName { get; set; }
    public Cell Location { get; set; }
    public CampType CampType { get; set; }
    public Vector2Int CampFirePosition { get; set; }
    public List<NPC> CampNPCs { get; set; } = new List<NPC>();
    public List<WildAnimal> CampAnimals { get; set; } = new List<WildAnimal>();
    public Faction Faction { get; set; }
    public Dictionary<NPCRole, bool> FulfilledRoles { get; private set; } = new Dictionary<NPCRole, bool>();


    public CampNestedArea(Faction faction, CampType campType, string campName)
    {
        Faction = faction;
        CampType = campType;
        CampName = campName;

        Initialize();

        // Set up positions for campfire or other camp objects
        CampFirePosition = new Vector2Int(3, 3);  // Example position
        AddCampFireToCamp();

        // Use walls or boundaries for specific camp types
        CreateWallsAroundMap(GetWallType(campType));
    }

    public override void Initialize()
    {
        AreaMap = new Cell[Size, Size];

        // Initialize the map for the camp area, setting terrain based on camp type
        TerrainType terrainType = GetTerrainType(CampType);
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                int cellID = GenerateUniqueCellID();
                Cell newCell = new Cell(cellID, x, y, terrainType);
                AreaMap[x, y] = newCell;
            }
        }

        // Set all cells as outdoors for a camp
        SetAllCellsOutdoors();
    }

    private void AddCampFireToCamp()
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

    public void AddNPCToCamp(NPC npc)
    {
        CampNPCs.Add(npc);

        // Add simple roles like a Camp Leader or Guard
        if (npc.Role == NPCRole.Leader || npc.Role == NPCRole.Guard)  // Compare NPCRole enum values
        {
            FulfilledRoles[npc.Role] = true;
        }

        Debug.Log($"{npc.Name} has been added to the camp as a {npc.Role}.");
    }

    public string GetWallType(CampType campType)
    {
        // Camps can have simpler walls or barriers
        switch (campType)
        {
            case CampType.BanditCamp:
            case CampType.HunterCamp:
                return "WoodenFence";  // Simple wooden fences

            case CampType.TraderCamp:
                return "NoWall";  // Open camp, no walls

            default:
                return "WoodenFence";
        }
    }

    public TerrainType GetTerrainType(CampType campType)
    {
        // Return a suitable terrain type for the camp
        switch (campType)
        {
            case CampType.BanditCamp:
                return TerrainType.Dirt;

            case CampType.TraderCamp:
                return TerrainType.Plains;

            case CampType.HunterCamp:
                return TerrainType.Forest;

            default:
                return TerrainType.Plains;
        }
    }

    public override void UpdatePlayerPosition(Vector2Int newPosition)
    {
        Cell newCellPosition = GetCellAtPosition(newPosition);
        PlayerStats.Instance.UpdateCurrentCellID(newCellPosition.CellID);
        PlayerStats.Instance.UpdateParentNestedAreaID(newCellPosition.ParentAreaID);
    }

    public override Cell GetCellAtPosition(Vector2Int position)
    {
        if (position.x >= 0 && position.x < AreaMap.GetLength(0) &&
            position.y >= 0 && position.y < AreaMap.GetLength(1))
        {
            return AreaMap[position.x, position.y];
        }
        return null;
    }

    // Set all cells as outdoors, since camps are not typically indoors
    public void SetAllCellsOutdoors()
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                if (AreaMap[x, y] != null)
                {
                    AreaMap[x, y].isIndoors = false;  // Mark all cells as outdoors
                }
            }
        }
    }

    public override void HandlePlayerExit(MapGenerator mapGenerator)
    {
        // Handle the player exiting the camp
    }
}
