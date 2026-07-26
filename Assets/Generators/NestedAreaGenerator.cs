using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NestedAreaGenerator : MonoBehaviour
{
    public MapGenerator mapGenerator;

    // Method to trigger the generation of a nested area based on the cell
    public void GenerateNestedArea(Cell currentCell)
    {
        if (currentCell == null)
        {
            GameDebugger.Instance.LogError("Cell is null.");
            return;
        }

        if (currentCell.isMainMapCell)
        {
            if (currentCell.hasNestedArea)
            {
                GameDebugger.Instance.LogError($"Cell with ID {currentCell.CellID} already has a nested area.");
                return;
            }

            // Retrieve the water edges, animals, and region number from the current cell
            List<string> waterEdges = CheckNeighboursForWater(currentCell);
            List<Animal> parentAnimals = currentCell.Animals;
            int regionNumber = currentCell.RegionNumber;

            // Generate the nested area based on the current cell's terrain
            INestedArea nestedArea = GenerateAreaBasedOnTerrain(
                currentCell.Terrain,
                waterEdges,
                parentAnimals,
                currentCell,
                regionNumber
            );

            if (nestedArea != null)
            {
                int currentID = nestedArea.NestedAreaID;
                Cell[,] nestedCellsArray = nestedArea.GetNestedMap(); // Retrieve the 2D array of nested cells
                List<Cell> nestedCells = ConvertArrayToList(nestedCellsArray); // Convert the 2D array to a List

                // Update properties of each nested cell
                foreach (Cell cell in nestedCells)
                {
                    if (cell != null)
                    {
                        cell.ParentAreaID = 100;
                        cell.CurrentAreaID = currentID;
                        cell.NestedAreaLevel = 0;
                    }
                }

                LogCellsToPermaList(nestedCells); // Log the cells to PermaLists

                // Update properties of the current cell
                currentCell.hasNestedArea = true;
                nestedArea.NestedAreaLevel = 0;
                nestedArea.ParentCellID = currentCell.CellID;
                nestedArea.SetParentCell();
                nestedArea.MainMapCellID = currentCell.CellID;
                currentCell.SetNestedArea(nestedArea);
                currentCell.ChildAreaID = nestedArea.NestedAreaID;

                // Add the nested area to the map generator's list
                mapGenerator.AddNestedAreaToList(nestedArea);

                // Set visit properties for the current cell
                SetVisitProperties(currentCell);

                GameDebugger.Instance.LogInfo($"Nested area generated for cell with ID {currentCell.CellID}");

                // Handle NPC groups or dungeon presence if applicable
                if (currentCell.isNPCGroupPresent)
                {
                    UpdateNestedAreaWithNPCGroup(nestedArea, currentCell.Coordinates);
                }

            }
        }
        else
        {
            GameDebugger.Instance.LogInfo("Can't be done! The cell is not part of the main map.");
        }
    }



    public void GenerateNestedAreaWithinNestedArea(INestedArea parentNestedArea, Vector2Int cellPosition)
    {
        GameDebugger.Instance.LogInfo("GenerateNestedAreaWithinNestedArea Called");
        if (parentNestedArea == null)
        {
            GameDebugger.Instance.LogError("Parent nested area is null.");
            return;
        }

        if (CheckIfAnyCellHasNestedArea(parentNestedArea))
        {
            GameDebugger.Instance.LogInfo("A cell within the current area already has a nested area. Aborting new nested area creation.");
            return;
        }

        Cell currentCell = parentNestedArea.GetCellAtPosition(cellPosition);
        if (currentCell == null)
        {
            GameDebugger.Instance.LogError($"Cell at position {cellPosition} within nested area is null.");
            return;
        }

        // Check for DungeonEntrance
        var dungeonEntrance = currentCell.Objects.OfType<DungeonEntrance>().FirstOrDefault();
        if (dungeonEntrance != null)
        {
            HandleDungeonEntrance(currentCell, dungeonEntrance, parentNestedArea);
        }
        // Check for CaveEntrance
        else
        {
            var caveEntrance = currentCell.Objects.OfType<CaveEntrance>().FirstOrDefault();
            if (caveEntrance != null)
            {
                HandleCaveEntrance(currentCell, caveEntrance, parentNestedArea);
            }
            else
            {
                if (parentNestedArea.Type == NestedAreaType.Dungeon)
                {
                    HandleDungeonLevel(currentCell, parentNestedArea);
                }
                else
                {
                    Cell parentCell = MapGenerator.Instance.GetCellByID(parentNestedArea.ParentCellID);
                    if (parentCell != null && parentCell.HasDungeon)
                    {
                        GameDebugger.Instance.LogInfo("Cannot generate an underground area because the parent cell has a dungeon.");
                        return;
                    }

                    GenerateAndSetUndergroundNestedArea(currentCell, parentNestedArea);
                }
            }
        }
    }


    private bool CheckIfAnyCellHasNestedArea(INestedArea parentNestedArea)
    {
        var nestedMap = parentNestedArea.GetNestedMap();
        foreach (Cell cell in nestedMap)
        {
            if (cell != null && cell.hasNestedArea)
            {
                return true;
            }
        }
        return false;
    }

    private void HandleDungeonEntrance(Cell currentCell, DungeonEntrance dungeonEntrance, INestedArea parentNestedArea)
    {
        int dungeonID = dungeonEntrance.DungeonID;
        GameDebugger.Instance.LogInfo($"Dungeon entrance found at cell ID: {currentCell.CellID} with dungeon ID: {dungeonID}");

        Vector2Int entrancePosition = currentCell.Coordinates;
        DungeonNestedArea dungeon = new DungeonNestedArea(entrancePosition, 1, dungeonID);

        SetNestedAreaProperties(dungeon, parentNestedArea, currentCell);
        SetNestedAreaAndAddToMap(currentCell, dungeon);

        GameDebugger.Instance.LogInfo($"DungeonNestedArea generated for cell at {entrancePosition} with Dungeon ID {dungeonID}");
    }

    private void HandleDungeonLevel(Cell currentCell, INestedArea parentNestedArea)
    {
        if (parentNestedArea is DungeonNestedArea parentDungeon)
        {
            int dungeonID = parentDungeon.DungeonID; // Use the DungeonID from the parent nested area
            int newDungeonLevel = parentDungeon.DungeonLevel + 1; // Increment the dungeon level

            GameDebugger.Instance.LogInfo($"Dungeon level found at cell ID: {currentCell.CellID} with dungeon ID: {dungeonID} and new dungeon level: {newDungeonLevel}");

            Vector2Int entrancePosition = currentCell.Coordinates;
            DungeonNestedArea dungeon = new DungeonNestedArea(entrancePosition, newDungeonLevel, dungeonID);
            dungeon.Initialize();

            SetNestedAreaProperties(dungeon, parentNestedArea, currentCell);
            SetNestedAreaAndAddToMap(currentCell, dungeon);

            GameDebugger.Instance.LogInfo($"DungeonNestedArea generated for cell at {entrancePosition} with Dungeon ID {dungeonID} and new dungeon level {newDungeonLevel}");
        }
        else
        {
            GameDebugger.Instance.LogError("Parent nested area is not a DungeonNestedArea.");
        }
    }

    private void HandleCaveEntrance(Cell currentCell, CaveEntrance caveEntrance, INestedArea parentNestedArea)
    {
        int caveID = caveEntrance.CaveID;
        GameDebugger.Instance.LogInfo($"Cave entrance found at cell ID: {currentCell.CellID} with cave ID: {caveID}");

        Vector2Int entrancePosition = currentCell.Coordinates;
        CaveNestedArea cave = new CaveNestedArea(entrancePosition, caveID);
        cave.Initialize();

        SetNestedAreaProperties(cave, parentNestedArea, currentCell);
        SetNestedAreaAndAddToMap(currentCell, cave);

        GameDebugger.Instance.LogInfo($"CaveNestedArea generated for cell at {entrancePosition} with Cave ID {caveID}");
    }



    private void GenerateAndSetUndergroundNestedArea(Cell currentCell, INestedArea parentNestedArea)
    {
        INestedArea nestedArea = GenerateUndergroundNestedArea();
        SetNestedAreaProperties(nestedArea, parentNestedArea, currentCell);
        SetNestedAreaAndAddToMap(currentCell, nestedArea);

        GameDebugger.Instance.LogInfo($"Nested area generated for currentCell at {currentCell.Coordinates} with NestedArea ID of {nestedArea.NestedAreaID} linked to cellID of {nestedArea.ParentCellID}");
        GameDebugger.Instance.LogInfo("Nested area added to list.");
    }

    private void SetNestedAreaAndAddToMap(Cell currentCell, INestedArea nestedArea)
    {
        currentCell.SetNestedArea(nestedArea);
        currentCell.hasNestedArea = true;
        currentCell.ChildAreaID = nestedArea.NestedAreaID;
        mapGenerator.AddNestedAreaToList(nestedArea);
        SetVisitProperties(currentCell);
    }

    private void PlaceDungeonEntrance(INestedArea nestedArea, Cell parentCell)
    {

        Debug.Log("This is the PlaceDungeonEntrance that places a DungeonEntrance does this ever get activated? if not, remove it");

        Vector2Int entrancePosition = new Vector2Int(Random.Range(0, 9), Random.Range(0, 9));
        DungeonEntrance entrance = new DungeonEntrance(parentCell.CellID) // Use CellID for tracking
        {
            Position = entrancePosition,
            NestedMapPosition = entrancePosition,
            CurrentNestedArea = nestedArea
        };

        Cell entranceCell = nestedArea.GetCellAtPosition(entrancePosition);
        if (entranceCell != null)
        {
            entranceCell.Objects.Add(entrance);
            GameDebugger.Instance.LogInfo($"Placed Dungeon Entrance in nested area at {entrancePosition}");

            //: Searching DungeonCreationData by Parent Cell's CellID
            var dungeonData = PermaLists.Instance.DungeonCreationDataList
                .FirstOrDefault(data => data.DungeonCellID == parentCell.CellID); // Match using CellID instead

            if (dungeonData != null)
            {
                dungeonData.DungeonEntranceCellID = entranceCell.CellID;
                entrance.DungeonID = dungeonData.DungeonID;
                GameDebugger.Instance.LogInfo($"Updated DungeonCreationData with entrance cell ID {entranceCell.CellID} for Dungeon ID {dungeonData.DungeonID}");
            }
            else
            {
                GameDebugger.Instance.LogWarning($"DungeonCreationData NOT FOUND for CellID: {parentCell.CellID}. Check dungeon generation!");
            }
        }
    }

    private void PlaceCaveEntrance(INestedArea nestedArea, Cell parentCell)
    {
        Vector2Int entrancePosition = new Vector2Int(Random.Range(0, 9), Random.Range(0, 9));
        CaveEntrance entrance = new CaveEntrance(parentCell.CellID)
        {
            Position = entrancePosition,
            NestedMapPosition = entrancePosition,
            CurrentNestedArea = nestedArea
        };

        Cell entranceCell = nestedArea.GetCellAtPosition(entrancePosition);
        if (entranceCell != null)
        {
            entranceCell.Objects.Add(entrance);
            GameDebugger.Instance.LogInfo($"Placed Cave Entrance in nested area at {entrancePosition}");

            var caveData = PermaLists.Instance.CaveCreationDataList
                .FirstOrDefault(data => data.CaveCellID == parentCell.CellID);
            if (caveData != null)
            {
                caveData.CaveEntranceCellID = entranceCell.CellID;
                entrance.CaveID = caveData.CaveID;
                GameDebugger.Instance.LogInfo($"Updated CaveCreationData with entrance cell ID {entranceCell.CellID} for cave ID {caveData.CaveID}");
            }
            else
            {
                GameDebugger.Instance.LogWarning($"CaveCreationData not found for cell ID {parentCell.CellID}");
            }
        }
    }


    private void SetNestedAreaProperties(INestedArea nestedArea, INestedArea parentNestedArea, Cell currentCell)
    {
        int parentID = parentNestedArea.NestedAreaID;
        int currentID = nestedArea.NestedAreaID;

        GameDebugger.Instance.LogInfo($"Populating all cells with the parentID of {parentID} and the currentID of {currentID}");

        Cell[,] nestedCellsArray = nestedArea.GetNestedMap(); // Retrieve the 2D array of nested cells
        List<Cell> nestedCells = ConvertArrayToList(nestedCellsArray); // Convert the 2D array to a List

        foreach (Cell cell in nestedCells)
        {
            if (cell != null)
            {
                cell.ParentAreaID = parentID;
                cell.CurrentAreaID = currentID;
                cell.NestedAreaLevel = parentNestedArea.NestedAreaLevel + 1;
            }
        }

        LogCellsToPermaList(nestedCells); // Log the cells to PermaLists

        nestedArea.ParentCellID = currentCell.CellID;
        nestedArea.SetParentCell();
        nestedArea.MainMapCellID = parentNestedArea.MainMapCellID;
        nestedArea.NestedAreaLevel = parentNestedArea.NestedAreaLevel + 1;
    }


    private void SetVisitProperties(Cell cell)
    {
        if (!cell.HasVisited)
        {
            cell.HasVisited = true;
        }
        cell.LastVisited = TimeManager.Instance.currentDay;
    }

    private INestedArea GenerateUndergroundNestedArea()
    {
        UndergroundNestedArea undergroundNestedArea = new UndergroundNestedArea();
        int currentAreaID = undergroundNestedArea.NestedAreaID;
        GameDebugger.Instance.LogInfo("UndergroundArea Generated with ID of " + currentAreaID);
        return undergroundNestedArea;
    }

    public INestedArea GenerateAreaBasedOnTerrain(TerrainType terrain, List<string> waterEdges, List<Animal> parentAnimals, Cell parentCell, int regionNumber)
    {
        INestedArea nestedArea;

        // Create the nested area based on terrain type
        switch (terrain)
        {
            case TerrainType.Forest:
                nestedArea = new ForestNestedArea(waterEdges, parentAnimals, parentCell, regionNumber);
                break;
            case TerrainType.Land:
                nestedArea = new LandNestedArea(waterEdges, parentAnimals, parentCell, regionNumber);
                break;
            case TerrainType.Mountain:
                nestedArea = new MountainNestedArea(waterEdges, parentAnimals, parentCell, regionNumber);
                break;
            case TerrainType.Swamp:
                nestedArea = new SwampNestedArea(waterEdges, parentAnimals, parentCell, regionNumber);
                break;
            case TerrainType.Desert:
                nestedArea = new DesertNestedArea(waterEdges, parentAnimals, parentCell, regionNumber);
                break;
            case TerrainType.Sand:
                nestedArea = new SandNestedArea(waterEdges, parentAnimals, parentCell, regionNumber);
                break;
            case TerrainType.Saltflat:
                nestedArea = new SaltFlatsNestedArea(waterEdges, parentAnimals, parentCell, regionNumber);
                break;
            case TerrainType.Tundra:
                nestedArea = new TundraNestedArea(parentCell, regionNumber);
                break;
            default:
                GameDebugger.Instance.LogError($"Unsupported terrain type for nested area generation: {terrain}");
                return null;
        }

        // Retrieve the RegionInfo for the given regionNumber
        RegionInfo regionInfo = RegionManager.Instance.GetRegionInfo(regionNumber);

        if (regionInfo != null)
        {
            // Assign the CharacterLevel based on the region's CharacterLevel
            nestedArea.CharacterLevel = regionInfo.CharacterLevel;
        }
        else
        {
            // If region info is not found, log an error or set a default CharacterLevel
            GameDebugger.Instance.LogError($"RegionInfo not found for regionNumber: {regionNumber}");
            nestedArea.CharacterLevel = 1;  // Default CharacterLevel in case of error
        }

        return nestedArea;
    }


    private void UpdateNestedAreaWithNPCGroup(INestedArea nestedArea, Vector2Int cellPosition)
    {
        NPCGroup group = FindNPCGroupAtPosition(cellPosition);
        if (group != null)
        {
            foreach (var npc in group.NPCs)
            {
                Vector2Int npcPosition = DetermineNPCPositionInNestedArea(nestedArea, npc);
                if (nestedArea.IsValidPosition(npcPosition) && nestedArea.IsPassable(npcPosition))
                {
                    nestedArea.UpdateCharacterPosition(npc, npcPosition);
                }
            }
        }
    }

    private NPCGroup FindNPCGroupAtPosition(Vector2Int cellPosition)
    {
        return NPCManager.Instance.FindNPCGroupAtPosition(cellPosition);
    }

    private Vector2Int DetermineNPCPositionInNestedArea(INestedArea nestedArea, NPC npc)
    {
        Vector2Int size = nestedArea.GetSize();
        Vector2Int position = new Vector2Int(Random.Range(0, size.x), Random.Range(0, size.y));
        while (!nestedArea.IsPassable(position) || nestedArea.IsPlayerPresent(position))
        {
            position = new Vector2Int(Random.Range(0, size.x), Random.Range(0, size.y));
        }
        return position;
    }

    private List<string> CheckNeighboursForWater(Cell cell)
    {
        List<string> waterDirections = new List<string>();
        foreach (var neighbour in cell.NeighbouringTerrainTypes)
        {
            if (neighbour.Value == TerrainType.Water)
            {
                waterDirections.Add(neighbour.Key);
            }
        }
        return waterDirections;
    }

    private void LogCellsToPermaList(List<Cell> cells)
    {
        foreach (var cell in cells)
        {
            if (cell != null && !PermaLists.Instance.AllMapCells.Contains(cell))
            {
                PermaLists.Instance.AllMapCells.Add(cell);
                GameDebugger.Instance.LogInfo($"Logged cell with ID {cell.CellID} to AllMapCells.");
            }
        }
    }

    private List<Cell> ConvertArrayToList(Cell[,] array)
    {
        List<Cell> list = new List<Cell>();
        foreach (Cell cell in array)
        {
            if (cell != null)
            {
                list.Add(cell);
            }
        }
        return list;
    }


}

public enum NestedAreaType
{
    Basic,
    Building,
    Cave,
    Dungeon,
    Tower,
    Camp,
    Underground
}
