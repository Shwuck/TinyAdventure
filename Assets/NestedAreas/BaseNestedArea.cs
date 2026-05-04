using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System.Linq;
using System;

public interface INestedArea
{
    string Name { get; set; }
    int ParentCellID { get; set; }
    Cell ParentCell { get; set; }
    int MainMapCellID { get; set; }
    int DangerLevel { get; set; }
    int RegionNumber { get; set; }
    int CharacterLevel { get; set; }
    int DangerRating { get; set; }
    int NestedAreaID { get; set; }
    int NestedAreaLevel { get; set; }
    int MaxAnimalsToPlace { get; set; }
    int MaxMonstersToPlace { get; set; }
    Vector2Int EntrancePosition { get; set; }
    NestedAreaType Type { get; set; }
    List<Animal> GeneratedAnimals { get; set; }
    List<Monster> GeneratedMonsters { get; set; }
    bool IsHostileArea { get; set; }

    // New corpse/carcass handling
    void AddCorpseToArea(Corpse corpse);
    void RemoveCorpseFromArea(Corpse corpse);
    List<Corpse> GetAllCorpsesInArea();

    void AddCarcassToArea(Carcass carcass);
    void RemoveCarcassFromArea(Carcass carcass);
    List<Carcass> GetAllCarcassesInArea();

    // New monster handling
    List<Monster> GetAllMonstersInArea();
    void GenerateMonstersForCellID(int cellID); 
    void PlaceGeneratedMonsters();

    // New properties
    int LastDayVisited { get; set; }
    Season LastSeasonVisited { get; set; }
    bool HasVisited { get; set; }

    // Existing methods
    void Initialize();
    bool IsValidPosition(Vector2Int position);
    bool IsPassable(Vector2Int position);
    void UpdatePlayerPosition(Vector2Int newPosition);
    Cell[,] GetNestedMap();
    bool IsPlayerPresent(Vector2Int position);
    Vector2Int GetSize();
    List<IInteractable> GetObjectsAtPosition(Vector2Int position);
    List<Animal> GetAllAnimalsInArea();
    void HandlePlayerExit(MapGenerator mapGenerator);
    void HandlePlayerExitFromSpecificNestedAreaType(MapGenerator mapGenerator);
    List<NPCGroup> GetNPCGroups();
    void GenerateAnimalsForCellID(int cellID);
    void PlaceGeneratedAnimals();
    void AddAnimal(Animal animal, Vector2Int position);
    void UpdateCharacterPosition(Character character, Vector2Int newPosition);
    void UpdateNPCGroupPosition(NPCGroup npcGroup, Vector2Int newPosition);
    void AddObjectToArea(IInteractable interactable);
    void RemoveObjectFromArea(IInteractable interactable);
    List<IInteractable> GetAllObjectsInArea();
    List<NPC> GetAllNPCsInArea();
    List<Character> GetAllCharactersInArea();
    Cell GetCellAtPosition(Vector2Int position);
    void RegisterCharacterWithTurnManager(Character character);
    void DeregisterCharacterFromTurnManager(Character character);
    void ReplaceObject(IInteractable oldObject, IInteractable newObject);
    void HandlePlayerReentry();
    void UpdateHostileAreaStatus();

    // New methods for plant management
    void AddPlantToArea(PlantBase plant);
    void RemovePlantFromArea(PlantBase plant);
    List<PlantBase> GetAllPlantsInArea();
    void UpdatePlantGrowth();
}

public abstract class BaseNestedArea : INestedArea
{
    public string Name { get; set; }
    public int NestedAreaID { get; set; }
    public int DangerLevel { get; set; }
    public int ParentCellID { get; set; }
    public Cell ParentCell { get; set; }
    public int MainMapCellID { get; set; }
    public int RegionNumber { get; set; }
    public int CharacterLevel { get; set; }
    public int NestedAreaLevel { get; set; }
    public int MaxAnimalsToPlace { get; set; } = 4;
    public Vector2Int EntrancePosition { get; set; }
    public NestedAreaType Type { get; set; }
    public Cell[,] AreaMap { get; protected set; }
    protected const int Size = 9;

    public int DangerRating { get; set; }
    public int MaxMonstersToPlace { get; set; }

    public List<Animal> GeneratedAnimals { get; set; } = new List<Animal>();
    public List<Animal> AnimalsToPlace { get; set; } = new List<Animal>();
    public List<Animal> PlacedAnimals { get; set; } = new List<Animal>(); // New list to keep track of placed animals
    public List<Monster> GeneratedMonsters { get; set; } = new List<Monster>();
    public List<Monster> PlacedMonsters { get; set; } = new List<Monster>();

    // Track corpses and carcasses
    public List<Corpse> CorpsesInArea { get; private set; } = new List<Corpse>();
    public List<Carcass> CarcassesInArea { get; private set; } = new List<Carcass>();
    public List<Monster> MonstersInArea { get; private set; } = new List<Monster>();

    public int NumberOfTrees;
    public int NumberOfRocks;
    public int NumberOfGrassPatches;

    // Implement new properties
    public int LastDayVisited { get; set; }
    public Season LastSeasonVisited { get; set; }
    public bool HasVisited { get; set; }

    public bool IsHostileArea { get; set; }

    private List<PlantBase> plantsInArea = new List<PlantBase>(); // List to keep track of plants in the area

    public abstract void Initialize();

    public void SetUpNestedArea()
    {
        OrchestrateParentCellChecks();
    }

    protected virtual int GenerateUniqueCellID()
    {
        // Use GameManager or a similar system to generate a globally unique ID
        return GameManager.Instance.GetCellID();
    }

    public virtual bool IsValidPosition(Vector2Int position)
    {
        return position.x >= 0 && position.x < Size && position.y >= 0 && position.y < Size;
    }

    public virtual bool IsPassable(Vector2Int position)
    {
        Cell cell = GetCellAtPosition(position);
        return cell != null && cell.isPassable && cell.Objects.TrueForAll(obj => obj.IsPassable);
    }

    protected void SetWaterEdges(Cell[,] areaMap, List<string> waterEdges, int size)
    {
        foreach (string edge in waterEdges)
        {
            for (int i = 0; i < size; i++)
            {
                if (edge == "North")
                {
                    areaMap[i, size - 1].Terrain = TerrainType.Water;
                    areaMap[i, size - 1].isFishable = true;
                    areaMap[i, size - 1].isPassable = false;
                }
                else if (edge == "South")
                {
                    areaMap[i, 0].Terrain = TerrainType.Water;
                    areaMap[i, 0].isFishable = true;
                    areaMap[i, 0].isPassable = false;
                }
                else if (edge == "East")
                {
                    areaMap[size - 1, i].Terrain = TerrainType.Water;
                    areaMap[size - 1, i].isFishable = true;
                    areaMap[size - 1, i].isPassable = false;
                }
                else if (edge == "West")
                {
                    areaMap[0, i].Terrain = TerrainType.Water;
                    areaMap[0, i].isFishable = true;
                    areaMap[0, i].isPassable = false;
                }
            }
        }
    }

    public virtual void UpdatePlayerPosition(Vector2Int newPosition)
    {
        Cell newCellPosition = GetCellAtPosition(newPosition);
        if (newCellPosition != null)
        {
            PlayerStats.Instance.UpdateCurrentCellID(newCellPosition.CellID);
            PlayerStats.Instance.UpdateParentNestedAreaID(newCellPosition.ParentAreaID);
        }
    }

    public virtual void UpdateCharacterPosition(Character character, Vector2Int newPosition)
    {
        // Ensure that the new position is valid and passable before making any updates
        if (IsPassable(newPosition))
        {
            // Remove character from the current cell
            Cell currentCell = GetCellAtPosition(character.NestedMapPosition);
            if (currentCell != null)
            {
                currentCell.Objects.Remove(character);
                currentCell.isPassable = true;
                Debug.Log($"Character {character.IInteractableID} removed from position {character.NestedMapPosition}");
            }

            // Update character's position
            character.NestedMapPosition = newPosition;

            // Add character to the new cell
            Cell newCell = GetCellAtPosition(newPosition);
            if (newCell != null)
            {
                newCell.Objects.Add(character);
                newCell.isPassable = false;
                Debug.Log($"Character {character.IInteractableID} moved to position {newPosition}");
            }
        }
        else
        {
            Debug.LogWarning($"Invalid move: Character {character.IInteractableID} tried to move to an impassable position {newPosition}");
        }
    }


    public virtual void UpdateNPCGroupPosition(NPCGroup npcGroup, Vector2Int newPosition)
    {
        foreach (NPC npc in npcGroup.NPCs)
        {
            UpdateCharacterPosition(npc, newPosition);
        }
    }

    public virtual void UpdateAnimalPosition(Animal animal, Vector2Int newPosition)
    {
        UpdateCharacterPosition(animal, newPosition);
    }

    public virtual void SetParentCell()
    {
        // Retrieve the main map cell using ParentCellID from MapGenerator
        Cell ParentCell = MapGenerator.Instance.GetCellByID(ParentCellID);
        Debug.Log("ParentCell has been set with ID" + ParentCellID);
    }

    public virtual void UpdateNestedAreaLevel()
    {
        if (ParentCell != null)
        {
            if (ParentCell.isMainMapCell)
            {
                NestedAreaLevel = -1;
            }
            else if (!ParentCell.isMainMapCell && ParentCell.NestedArea != null)
            {
                NestedAreaLevel = ParentCell.NestedArea.NestedAreaLevel - 1;
            }
            else
            {
                Debug.LogWarning("ParentCell's NestedArea is null or ParentCell is not properly configured.");
            }
        }
        else
        {
            Debug.LogError("ParentCell is null. Cannot update NestedAreaLevel.");
        }
    }


    // Orchestrator method to perform all relevant checks on the ParentCellID
    public virtual void OrchestrateParentCellChecks()
    {

        SetRandomSeed(ParentCellID);

        // Call this if needed to set the ParentCell
        if (ParentCell != null)
        {
            Debug.Log($"Performing checks on main map cell with ID {ParentCellID}");
            // Perform any checks for the main map cell before the player enters the nested area
            PerformRelevantChecksForParentCell(ParentCell);
        }
        else
        {
            Debug.LogWarning($"Parent cell with ID {ParentCellID} is null or out of bounds.");
        }
    }

    // Centralized method to check and handle conditions for the parent cell
    protected virtual void PerformRelevantChecksForParentCell(Cell parentCell)
    {
        DangerLevel = parentCell.DangerLevel;

        // Example: Check if the parent cell is the player's start position and place a StartingChest
        if (parentCell.WasPlayerStart)
        {
            Debug.Log("ParentCell is PlayerStart, placing Starting Chest in nested area.");
            PlaceStartingChestInNestedArea(); // Call the updated method
        }

        // Check if the parent cell has a camp and place it
        if (parentCell.HasCamp)
        {
            Debug.Log($"ParentCell has a Camp with ID {parentCell.CampID}, adding a Camp to the area.");
            PlaceCampInNestedArea(parentCell.CampID);  // Place the camp using the CampID stored in the parent cell
        }

        // Check if the parent cell has a cave and place a cave entrance
        if (parentCell.HasCave)
        {
            Debug.Log($"ParentCell has a Cave, placing a Cave Entrance in nested area.");
            PlaceEntrance(parentCell, "Cave"); // Call the PlaceEntrance method for Cave
        }

        // Check if the parent cell has a dungeon and place a dungeon entrance
        if (parentCell.HasDungeon)
        {
            DangerLevel = 5;
            Debug.Log($"ParentCell has a Dungeon, placing a Dungeon Entrance in nested area.");
            PlaceEntrance(parentCell, "Dungeon"); // Call the PlaceEntrance method for Dungeon
        }

     
    }

    #region Chests and Treasures

    protected void PlaceStartingChestInNestedArea()
    {
        // Define the range for the central part of the map (x3 to x7, y3 to y7)
        int minX = 3;
        int maxX = 7;
        int minY = 3;
        int maxY = 7;

        // Randomly select coordinates within the central area
        int randomX = UnityEngine.Random.Range(minX, maxX + 1); // +1 because Range is exclusive on the upper bound
        int randomY = UnityEngine.Random.Range(minY, maxY + 1);

        Vector2Int randomPosition = new Vector2Int(randomX, randomY);
        Cell chestCell = GetCellAtPosition(randomPosition);

        if (chestCell != null)
        {
            // Create a new ChestBase with the type "starting"
            Chest startingChest = new Chest("starting")
            {
                Position = randomPosition // Set the chest's position to the randomly selected spot
            };

            chestCell.Objects.Add(startingChest); // Add the chest to the cell's object list
            Debug.Log($"Placed Starting Chest at random position {randomPosition} in the nested area.");
        }
        else
        {
            Debug.LogError("Randomly selected cell is null. Cannot place Starting Chest.");
        }
    }

    protected void PlaceDungeonChest(int dungeonLevel)
    {
        Vector2Int chestPosition = GetValidChestPosition(); // Ensures valid placement
        Cell chestCell = GetCellAtPosition(chestPosition);

        if (chestCell != null)
        {
            // Create a new DungeonChest with level-scaled loot
            Chest dungeonChest = new Chest("dungeon", dungeonLevel)
            {
                Position = chestPosition // Assign chest position
            };

            chestCell.Objects.Add(dungeonChest); // Add the chest to the cell
            Debug.Log($"Placed Dungeon Chest at {chestPosition} in {this.Type} (Level {dungeonLevel}).");
        }
        else
        {
            Debug.LogError($"Failed to place Dungeon Chest in {this.Type} due to an invalid cell.");
        }
    }

    protected Vector2Int GetValidChestPosition()
    {
        System.Random random = new System.Random();
        Vector2Int randomPos;
        int attempts = 0;

        do
        {
            randomPos = new Vector2Int(random.Next(Size / 4, (Size * 3) / 4), random.Next(Size / 4, (Size * 3) / 4)); // Central area
            attempts++;
            if (attempts > 100) // Avoid infinite loops
            {
                Debug.LogWarning("Failed to find a valid chest position, defaulting to central area.");
                return new Vector2Int(Size / 2, Size / 2); // Center of the map
            }
        }
        while (!IsPassable(randomPos) || GetCellAtPosition(randomPos)?.Objects.Any() == true); // Ensure passable & not occupied

        return randomPos;
    }

    #endregion

    public virtual void PlaceEntrance(Cell parentCell, string entranceType)
    {
        Vector2Int entrancePosition = new Vector2Int(UnityEngine.Random.Range(0, Size), UnityEngine.Random.Range(0, Size));
        IInteractable entrance = null;

        // Find the correct DungeonID before placing the entrance
        int correctDungeonID = PermaLists.Instance.DungeonCreationDataList
            .FirstOrDefault(d => d.DungeonCellID == parentCell.CellID)?.DungeonID ?? -1;

        if (correctDungeonID == -1)
        {
            GameDebugger.Instance.LogError($"Failed to find DungeonCreationData for CellID: {parentCell.CellID}");
            return; // Stop execution if we can't find a valid DungeonID
        }

        if (entranceType == "Dungeon")
        {
            entrance = new DungeonEntrance(correctDungeonID) // Now using the correct DungeonID
            {
                Position = entrancePosition,
                NestedMapPosition = entrancePosition,
                CurrentNestedArea = this
            };
        }
        else if (entranceType == "Cave")
        {
            entrance = new CaveEntrance(parentCell.CellID) // Cave logic remains the same
            {
                Position = entrancePosition,
                NestedMapPosition = entrancePosition,
                CurrentNestedArea = this
            };
        }

        if (entrance != null)
        {
            Cell entranceCell = GetCellAtPosition(entrancePosition);
            if (entranceCell != null)
            {
                entranceCell.Objects.Add(entrance);
                entranceCell.isPassable = false; // Entrance makes the cell impassable
                GameDebugger.Instance.LogInfo($"Placed {entranceType} Entrance at {entrancePosition} in nested area (DungeonID: {correctDungeonID}).");
            }
            else
            {
                GameDebugger.Instance.LogError($"Could not find entrance cell at {entrancePosition}.");
            }
        }
        else
        {
            GameDebugger.Instance.LogError("Entrance type not recognized.");
        }
    }

    public virtual Cell GetCellAtPosition(Vector2Int position)
    {
        if (position.x < 0 || position.x >= AreaMap.GetLength(0) ||
            position.y < 0 || position.y >= AreaMap.GetLength(1))
        {
            Debug.LogError($"GetCellAtPosition: Position {position} is out of bounds!");
            return null;
        }

        if (AreaMap[position.x, position.y] == null)
        {
            Debug.LogError($"GetCellAtPosition: AreaMap[{position.x}, {position.y}] is NULL!");
            return null;
        }

        return AreaMap[position.x, position.y];
    }

    // The IfCellIs and other helper methods remain as needed
    public bool IfCellIs(Vector2Int position, Func<Cell, bool> condition)
    {
        Cell cell = GetCellAtPosition(position);
        if (cell != null)
        {
            return condition(cell);
        }
        Debug.LogWarning($"Cell at position {position} is null or out of bounds.");
        return false;
    }

    public void AddCorpseToArea(Corpse corpse)
    {
        if (corpse != null)
        {
            CorpsesInArea.Add(corpse);
            GameDebugger.Instance.LogInfo($"Corpse {corpse.Name} added to NestedArea {NestedAreaID}.");
        }
    }

    public void RemoveCorpseFromArea(Corpse corpse)
    {
        if (corpse != null && CorpsesInArea.Contains(corpse))
        {
            CorpsesInArea.Remove(corpse);
            GameDebugger.Instance.LogInfo($"Corpse {corpse.Name} removed from NestedArea {NestedAreaID}.");
        }
    }

    public List<Corpse> GetAllCorpsesInArea()
    {
        return new List<Corpse>(CorpsesInArea);
    }

    public void AddCarcassToArea(Carcass carcass)
    {
        if (carcass != null)
        {
            CarcassesInArea.Add(carcass);
            GameDebugger.Instance.LogInfo($"Carcass {carcass.Name} added to NestedArea {NestedAreaID}.");
        }
    }

    public void RemoveCarcassFromArea(Carcass carcass)
    {
        if (carcass != null && CarcassesInArea.Contains(carcass))
        {
            CarcassesInArea.Remove(carcass);
            GameDebugger.Instance.LogInfo($"Carcass {carcass.Name} removed from NestedArea {NestedAreaID}.");
        }
    }

    public List<Carcass> GetAllCarcassesInArea()
    {
        return new List<Carcass>(CarcassesInArea);
    }

    public List<Monster> GetAllMonstersInArea()
    {
        return new List<Monster>(MonstersInArea);
    }

    public virtual List<Character> GetAllCharactersInArea()
    {
        List<Character> allCharacters = new List<Character>();

        // Get all NPCs
        allCharacters.AddRange(GetAllNPCsInArea());

        // Get all Animals
        allCharacters.AddRange(GetAllAnimalsInArea());

        // Get all Monsters
        allCharacters.AddRange(GetAllMonstersInArea());

        return allCharacters;
    }

    public virtual void HandlePlayerExit(MapGenerator mapGenerator)
    {
        // Update the last visited day and season
        LastDayVisited = TimeManager.Instance.currentDay;
        LastSeasonVisited = TimeManager.Instance.currentSeason;
        HasVisited = true;

        // Common tasks
        Vector2Int playerExitPosition = PlayerStats.Instance.NestedMapPosition;
        Cell cellToUpdate = GetCellAtPosition(playerExitPosition);
        if (cellToUpdate != null)
        {
            cellToUpdate.isPassable = true;
        }

        DeregisterAllCharacters();

        // Remove placed animals
        RemovePlacedAnimals();

        UpdateHostileAreaStatus();

        // Call specific nested area type logic
        HandlePlayerExitFromSpecificNestedAreaType(mapGenerator);

        GameDebugger.Instance.LogInfo($"Finished handling player exit for nested area {NestedAreaID}.");
    }

    private void RemovePlacedAnimals()
    {
        Debug.Log("Removing placed animals from the nested area.");

        foreach (var animal in PlacedAnimals)
        {
            Vector2Int position = animal.Position;
            Cell cell = GetCellAtPosition(position);

            if (cell != null && cell.Animals.Contains(animal))
            {
                cell.Animals.Remove(animal); // Remove animal from the cell's animal list
                cell.isPassable = true; // Set the cell to passable again
                animal.IsActive = false; // Set the animal to inactive

                Debug.Log($"Animal {animal.Name} removed from cell at position {position}.");
            }
            else
            {
                Debug.LogWarning($"Failed to remove animal {animal.Name} at position {position}.");
            }
        }

        PlacedAnimals.Clear(); // Clear the list of placed animals after removal

        Debug.Log("All placed animals have been successfully removed.");
    }

    public virtual void HandlePlayerExitFromSpecificNestedAreaType(MapGenerator mapGenerator)
    {
        // Default implementation, can be overridden by specific nested area classes
    }

    public virtual void HandlePlayerReentry()
    {
        // Calculate the time difference since the last visit
        int daysSinceLastVisit = TimeManager.Instance.currentDay - LastDayVisited;
        Season currentSeason = TimeManager.Instance.currentSeason;

        // Debug log for tracking the last visit time
        GameDebugger.Instance.LogInfo($"Player last visited this NestedArea {daysSinceLastVisit} days ago.");

        // If the last visit was in a different season or the days elapsed cross seasons, adjust for seasons
        bool crossedWinter = HasCrossedWinter(daysSinceLastVisit, LastSeasonVisited, currentSeason);

        // Update the growth of all plants
        foreach (var plant in plantsInArea)
        {
            if (!plant.IsTree && crossedWinter)
            {
                plant.WitherPlant();
            }
            else
            {
                plant.AdvanceGrowthByDays(daysSinceLastVisit, LastSeasonVisited, currentSeason);
            }
        }

        // Update the last visit time and season
        LastDayVisited = TimeManager.Instance.currentDay;
        LastSeasonVisited = TimeManager.Instance.currentSeason;
        UpdateHostileAreaStatus();

        GameDebugger.Instance.LogInfo($"Finished handling player reentry for nested area {NestedAreaID} after {daysSinceLastVisit} days.");
    }

    // Helper method to determine if the player has crossed a winter season
    private bool HasCrossedWinter(int daysElapsed, Season lastSeason, Season currentSeason)
    {
        int totalDaysInYear = 28 * 4; // 4 seasons, each with 28 days
        int fullYearsElapsed = daysElapsed / totalDaysInYear;
        int remainingDays = daysElapsed % totalDaysInYear;

        // If a full year has passed, winter has definitely been crossed
        if (fullYearsElapsed > 0)
        {
            return true;
        }

        // Determine if winter has been crossed in the current year
        int lastSeasonDayOffset = GetDayOffsetForSeason(lastSeason);
        int currentSeasonDayOffset = GetDayOffsetForSeason(currentSeason);

        return lastSeasonDayOffset <= currentSeasonDayOffset &&
               (lastSeasonDayOffset <= GetDayOffsetForSeason(Season.Winter) &&
               GetDayOffsetForSeason(Season.Winter) <= currentSeasonDayOffset);
    }

    // Helper method to get the day offset for a specific season
    private int GetDayOffsetForSeason(Season season)
    {
        switch (season)
        {
            case Season.Spring: return 0;
            case Season.Summer: return 28;
            case Season.Autumn: return 56;
            case Season.Winter: return 84;
            default: return 0;
        }
    }

    public virtual List<NPCGroup> GetNPCGroups()
    {
        return new List<NPCGroup>();
    }

    public virtual List<NPC> GetAllNPCsInArea()
    {
        List<NPC> allNPCs = new List<NPC>();
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Cell cell = AreaMap[x, y];
                if (cell.isNPCPresent)
                {
                    foreach (var obj in cell.Objects)
                    {
                        if (obj is NPC npc)
                        {
                            allNPCs.Add(npc);
                        }
                    }
                }
            }
        }
        return allNPCs;
    }

    public virtual List<Animal> GetAllAnimalsInArea()
    {
        List<Animal> allAnimals = new List<Animal>();
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Cell cell = AreaMap[x, y];
                allAnimals.AddRange(cell.Animals);
            }
        }
        return allAnimals;
    }

    public virtual List<IInteractable> GetObjectsAtPosition(Vector2Int position)
    {
        List<IInteractable> objectsAtPosition = new List<IInteractable>();
        if (IsValidPosition(position))
        {
            Cell cell = AreaMap[position.x, position.y];
            objectsAtPosition.AddRange(cell.Objects);
        }
        return objectsAtPosition;
    }

    public virtual bool IsPlayerPresent(Vector2Int position)
    {
        Cell cell = GetCellAtPosition(position);
        return cell != null && cell.isPlayerPresent;
    }

    public virtual Vector2Int GetSize()
    {
        return new Vector2Int(Size, Size);
    }

    public List<Vector2Int> GetInitialValidCells()
    {
        return GetValidCells(cell => cell.isPassable && cell.Objects.Count == 0);
    }


    public virtual void AddObjectToArea(IInteractable interactable)
    {
        if (interactable != null && IsValidPosition(interactable.Position))
        {
            Cell cell = AreaMap[interactable.Position.x, interactable.Position.y];
            if (cell != null)
            {
                cell.Objects.Add(interactable);
                GameDebugger.Instance.LogInfo($"Added interactable {interactable} to position {interactable.Position}");
            }
        }
        else
        {
            GameDebugger.Instance.LogWarning("Attempted to add invalid interactable to the nested area.");
        }
    }

    public virtual void RemoveObjectFromArea(IInteractable interactable)
    {
        if (interactable != null && IsValidPosition(interactable.Position))
        {
            Cell cell = AreaMap[interactable.Position.x, interactable.Position.y];

            // Remove the object from the cell
            cell.Objects.Remove(interactable);
            GameDebugger.Instance.LogInfo($"Removed {interactable.GetType().Name} from position {interactable.Position}");

            // Handle character-specific logic, if the interactable is a Character
            if (interactable is Character character)
            {
                TurnOrchestrator.Instance.DeregisterCharacter(character);
                GameDebugger.Instance.LogInfo($"Deregistered character '{character.Name}' from the TurnManager.");
            }

            // Handle plant-specific logic, if the interactable is a PlantBase
            if (interactable is PlantBase plant)
            {
                plantsInArea.Remove(plant);
                GameDebugger.Instance.LogInfo($"Removed plant '{plant.Name}' from plantsInArea.");
            }

            // Handle animal-specific logic, if the interactable is an Animal
            if (interactable is Animal animal)
            {
                GeneratedAnimals.Remove(animal);
                GameDebugger.Instance.LogInfo($"Removed animal '{animal.Name}' from GeneratedAnimals.");
            }


            cell.isNPCPresent = false;
            cell.isPassable = true;
        }
        else
        {
            GameDebugger.Instance.LogWarning("Attempted to remove an invalid interactable or from an invalid position.");
        }
    }

    public virtual List<IInteractable> GetAllObjectsInArea()
    {
        List<IInteractable> objectsInArea = new List<IInteractable>();
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                objectsInArea.AddRange(AreaMap[x, y].Objects);
            }
        }
        return objectsInArea;
    }

    public virtual void ReplaceObject(IInteractable oldObject, IInteractable newObject)
    {
        if (oldObject != null && newObject != null && IsValidPosition(oldObject.Position))
        {
            Cell cell = GetCellAtPosition(oldObject.Position);

            if (cell != null)
            {
                // Remove the old object
                cell.Objects.Remove(oldObject);
                oldObject.IsActive = false;

                // Add the new object
                newObject.Position = oldObject.Position;
                newObject.NestedMapPosition = oldObject.NestedMapPosition;
                newObject.CurrentNestedArea = this;
                cell.Objects.Add(newObject);
            }
        }
        else
        {
            Debug.LogWarning("ReplaceObject: Invalid old or new object, or invalid position.");
        }
    }

    public virtual Cell[,] GetNestedMap()
    {
        return AreaMap;
    }

    public void RegisterCharacterWithTurnManager(Character character)
    {
        float turnDuration = CalculateTurnDuration(character.Speed);
        TurnOrchestrator.Instance.RegisterCharacter(character); // Updated to pass the Character object
        Debug.Log($"Registered Character {character.IInteractableID} to the TurnManager via BaseNestedArea");
    }

    public void DeregisterCharacterFromTurnManager(Character character)
    {
        TurnOrchestrator.Instance.DeregisterCharacter(character); // Updated to pass the Character object
        GameDebugger.Instance.LogInfo($"Deregistered Character {character.IInteractableID} from TurnManager");
    }

    private void DeregisterAllCharacters()
    {
        var allCharacters = GetAllNPCsInArea().Cast<Character>().Concat(GetAllAnimalsInArea());
        foreach (var character in allCharacters)
        {
            DeregisterCharacterFromTurnManager(character);
        }
    }

    private float CalculateTurnDuration(float speed)
    {
        return Mathf.Max(0.1f, 1.0f / speed);
    }

    public virtual void GenerateAnimalsForCellID(int cellID)
    {
        Debug.Log($"Starting animal generation for CellID {cellID} in Region {RegionNumber} with CharacterLevel {CharacterLevel}.");

        if (PermaLists.Instance.AnimalsToGenerate.ContainsKey(cellID))
        {
            var animalNames = PermaLists.Instance.AnimalsToGenerate[cellID];
            Debug.Log($"Animals to generate for CellID {cellID}: {string.Join(", ", animalNames)}");

            // Step 1: Identify groups of animals by their occurrences
            var animalGroups = animalNames.GroupBy(name => name)
                                          .Where(group => group.Count() > 1)
                                          .ToDictionary(group => group.Key, group => group.Count());

            foreach (var animalName in animalNames.Distinct())
            {
                var animalData = AnimalGenerator.Instance.GetAnimalDataByName(animalName);
                if (animalData != null)
                {
                    // Step 2: Generate lower-level animals using CharacterLevel instead of RegionNumber
                    var lowerLevelAnimalData = AnimalFactory.GenerateLowerLevelAnimal(animalData, CharacterLevel); // Using CharacterLevel here

                    // Step 3: Generate animals and add them to the list
                    int groupCount = animalGroups.ContainsKey(animalName) ? animalGroups[animalName] : 1;
                    int groupID = GameManager.Instance.GetGroupID();

                    for (int i = 0; i < groupCount; i++)
                    {
                        var animal = AnimalFactory.CreateAnimal(lowerLevelAnimalData);
                        animal.GroupID = groupID;
                        GeneratedAnimals.Add(animal);
                        Debug.Log($"Generated lower-level animal: {animal.Name} (GroupID: {animal.GroupID}) for Region {RegionNumber}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Animal data not found for animal name: {animalName}");
                }
            }

            // Clear the entry after generating animals
            PermaLists.Instance.AnimalsToGenerate.Remove(cellID);
            Debug.Log($"Cleared animals to generate entry for CellID {cellID}.");
        }
        else
        {
            Debug.Log($"No animals to generate for CellID {cellID}.");
        }

        // After generating animals, add them to the AnimalsToPlace list and shuffle it
        AnimalsToPlace = new List<Animal>(GeneratedAnimals);
        ShuffleAnimalsToPlace();
    }

    public virtual void GenerateMonstersForCellID(int cellID)
    {
        if (!IsHostileArea) return;

        Debug.Log($"Starting monster generation for CellID {cellID} in NestedArea {NestedAreaID}.");

        // Prevent duplication: Check if monsters were already generated
        if (GeneratedMonsters.Count > 0)
        {
            Debug.LogWarning($"Monsters already generated for CellID {cellID}. Skipping generation.");
            return;
        }

        if (PermaLists.Instance.MonstersToGenerate.ContainsKey(cellID))
        {
            var monsterNames = PermaLists.Instance.MonstersToGenerate[cellID];
            Debug.Log($"Monsters to generate for CellID {cellID}: {string.Join(", ", monsterNames)}");

            foreach (var monsterName in monsterNames.Distinct())
            {
                var monsterData = PermaLists.Instance.MonsterCreationData.FirstOrDefault(m => m.MonsterName == monsterName);
                if (monsterData != null)
                {
                    var monster = new Monster(monsterData);
                    GeneratedMonsters.Add(monster);
                    Debug.Log($"Generated monster: {monster.Name} (ID: {monster.MonsterID}) for CellID {cellID}");
                }
                else
                {
                    Debug.LogWarning($"Monster data not found for: {monsterName}");
                }
            }

            PermaLists.Instance.MonstersToGenerate.Remove(cellID);
        }
        else
        {
            Debug.Log($"No monsters to generate for CellID {cellID}.");
        }
    }


    private void ShuffleAnimalsToPlace()
    {
        // Shuffle the AnimalsToPlace list to randomize the order of placement
        System.Random rng = new System.Random();
        int n = AnimalsToPlace.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            Animal value = AnimalsToPlace[k];
            AnimalsToPlace[k] = AnimalsToPlace[n];
            AnimalsToPlace[n] = value;
        }
    }

    public virtual void PlaceGeneratedAnimals()
    {
        Debug.Log("Starting animal placement in nested area via BaseNestedArea.");

        List<Vector2Int> validCells = GetValidCells(cell => cell.isPassable && cell.Objects.Count == 0);
        Debug.Log($"There are {validCells.Count} valid cells available for placing animals.");

        // Allow for 0 to MaxAnimalsToPlace animals to be placed
        int animalsToPlace = UnityEngine.Random.Range(0, Mathf.Min(MaxAnimalsToPlace, AnimalsToPlace.Count) + 1);

        for (int i = 0; i < animalsToPlace; i++)
        {
            if (validCells.Count == 0)
            {
                Debug.LogWarning($"No more valid cells available for placing animal: {AnimalsToPlace[i].Name}.");
                break;
            }

            // Randomly select a valid cell from the list
            int randomIndex = UnityEngine.Random.Range(0, validCells.Count);
            Vector2Int position = validCells[randomIndex];
            validCells.RemoveAt(randomIndex); // Remove the selected cell from the list

            var animal = AnimalsToPlace[i];

            // Try to place group animals closer together
            if (animal.IsHerd || animal.IsPack)
            {
                List<Vector2Int> nearbyCells = GetNearbyCells(position, radius: 2); // Get cells within a small radius
                var availableNearbyCells = nearbyCells.Intersect(validCells).ToList(); // Filter those that are still valid

                if (availableNearbyCells.Count > 0)
                {
                    randomIndex = UnityEngine.Random.Range(0, availableNearbyCells.Count);
                    position = availableNearbyCells[randomIndex];
                    validCells.Remove(position); // Remove the selected cell from the list
                }
            }

            AddAnimal(animal, position);
            PlacedAnimals.Add(animal); // Add animal to the list of placed animals
            Debug.Log($"Placed animal: {animal.Name} (GroupID: {animal.GroupID}) at position {position} via BaseNestedArea");

            // Additional debug log to verify animal position
            Cell cell = GetCellAtPosition(position);
            if (cell != null && cell.Animals.Contains(animal))
            {
                Debug.Log($"Verified: Animal {animal.Name} (GroupID: {animal.GroupID}) is correctly placed at {position}.");
            }
            else
            {
                Debug.LogWarning($"Failed to verify: Animal {animal.Name} at position {position}.");
            }

        }

        // Remove placed animals from the AnimalsToPlace list
        AnimalsToPlace.RemoveRange(0, animalsToPlace);

        if (AnimalsToPlace.Count > 0)
        {
            Debug.Log($"Some animals are still pending placement: {string.Join(", ", AnimalsToPlace.Select(a => a.Name))}");
            // Handle pending animals if needed (e.g., keep them in the list for later placement, etc.)
        }
        else
        {
            Debug.Log("All animals to place have been successfully placed.");
        }
    }

    public virtual void PlaceGeneratedMonsters()
    {
        Debug.Log($"Placing monsters in NestedArea {NestedAreaID}.");

        List<Vector2Int> validCells = GetValidCells(cell => cell.isPassable && cell.Objects.Count == 0);
        int maxMonsters = Mathf.Min(GeneratedMonsters.Count, validCells.Count);

        for (int i = 0; i < maxMonsters; i++)
        {
            Vector2Int position = validCells[i];
            var monster = GeneratedMonsters[i];

            AddMonster(monster, position);
            PlacedMonsters.Add(monster);
            Debug.Log($"Placed monster: {monster.Name} at {position} in NestedArea {NestedAreaID}");
        }

        // Prevent duplication by clearing GeneratedMonsters after placement
        GeneratedMonsters.Clear();
    }

    private List<Vector2Int> GetNearbyCells(Vector2Int position, int radius)
    {
        List<Vector2Int> nearbyCells = new List<Vector2Int>();

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector2Int checkPosition = new Vector2Int(position.x + x, position.y + y);

                // Ensure the position is within bounds and is a valid position
                if (IsValidPosition(checkPosition))
                {
                    nearbyCells.Add(checkPosition);
                }
            }
        }

        return nearbyCells;
    }

    public int CountValidCells(Func<Cell, bool> isValidCell)
    {
        int validCellCount = 0;

        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                Cell cell = GetCellAtPosition(position);

                if (cell == null)
                {
                    Debug.Log($"Cell at position {position} is null.");
                    continue;
                }

                if (!IsValidPosition(position))
                {
                    Debug.Log($"Position {position} is not valid.");
                    continue;
                }

                if (!cell.isPassable)
                {
                    Debug.Log($"Cell at position {position} is not passable.");
                    continue;
                }

                if (cell.Objects.Count > 0)
                {
                    Debug.Log($"Cell at position {position} already has {cell.Objects.Count} objects.");
                    continue;
                }

                if (isValidCell(cell))
                {
                    validCellCount++;
                }
            }
        }

        Debug.Log($"Total valid cells: {validCellCount}");
        return validCellCount;
    }

    public List<Vector2Int> GetValidCells(Func<Cell, bool> isValidCell)
    {
        List<Vector2Int> validCells = new List<Vector2Int>();

        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                Cell cell = GetCellAtPosition(position);

                if (cell == null)
                {
                    Debug.Log($"Cell at position {position} is null.");
                    continue;
                }

                if (!IsValidPosition(position))
                {
                    Debug.Log($"Position {position} is not valid.");
                    continue;
                }

                if (!cell.isPassable)
                {
                    Debug.Log($"Cell at position {position} is not passable.");
                    continue;
                }

                if (cell.Objects.Count > 0)
                {
                    Debug.Log($"Cell at position {position} already has {cell.Objects.Count} objects.");
                    continue;
                }

                if (isValidCell(cell))
                {
                    validCells.Add(position);
                }
            }
        }

        Debug.Log($"Total valid cells: {validCells.Count}");
        return validCells;
    }

    public void AddAnimal(Animal animal, Vector2Int position)
    {
        if (IsValidPosition(position))
        {
            Cell cell = GetCellAtPosition(position);
            if (cell != null)
            {
                animal.Position = position; // Update animal position
                animal.NestedMapPosition = position; // Update nested map position
                animal.IsActive = true; // Set animal to active

                cell.isPassable = false; // Set cell to impassable
                cell.Animals.Add(animal); // Add animal to the cell's animal list

                Debug.Log($"Animal {animal.Name} added to cell at position {position}.");
            }
            else
            {
                Debug.LogError($"Failed to add animal {animal.Name}: Cell at position {position} is null.");
            }

        }
        else
        {
            Debug.LogError($"Failed to add animal {animal.Name}: Position {position} is not valid.");
        }
    }

    public void AddMonster(Monster monster, Vector2Int position)
    {
        if (IsValidPosition(position))
        {
            Cell cell = GetCellAtPosition(position);
            if (cell != null)
            {
                monster.Position = position;
                monster.NestedMapPosition = position;
                monster.IsActive = true;
                cell.isPassable = false;
                cell.Objects.Add(monster);
                RegisterCharacterWithTurnManager(monster);

                Debug.Log($"Monster {monster.Name} placed at {position}.");
            }

        }
        else
        {
            Debug.LogError($"Invalid position {position} for monster {monster.Name}.");
        }
    }

    public void UpdateHostileAreaStatus()
    {
        // Get all characters in the area
        int totalHostiles = GetAllCharactersInArea().Count(character => character.IsHostile);

        // Define different levels of danger
        if (totalHostiles == 0)
        {
            IsHostileArea = false;
            DangerLevel = 0; // Safe
        }
        else if (totalHostiles <= 2)
        {
            IsHostileArea = true;
            DangerLevel = 1; // Mild danger
        }
        else if (totalHostiles <= 5)
        {
            IsHostileArea = true;
            DangerLevel = 2; // Moderate danger
        }
        else
        {
            IsHostileArea = true;
            DangerLevel = 3; // High danger
        }

        GameDebugger.Instance.LogInfo($"Updated Hostile Status for {this.Name}: {IsHostileArea} (Danger Level: {DangerLevel})");
    }

    protected Vector2Int GetRandomValidPosition()
    {
        List<Vector2Int> validPositions = GetValidCells(cell => cell.isPassable && cell.Objects.Count == 0);
        if (validPositions.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, validPositions.Count);
            return validPositions[randomIndex];
        }
        return Vector2Int.zero; // Return a default value if no valid position is found
    }

    public int DetermineNumberOfRocks(int distance)
    {
        switch (distance)
        {
            case 1:
                return UnityEngine.Random.Range(6, 8); // 8 to 11 rocks
            case 2:
                return UnityEngine.Random.Range(4, 8); // 5 to 10 rocks
            case 3:
                return UnityEngine.Random.Range(2, 6);  // 3 to 8 rocks
            case 4:
                return UnityEngine.Random.Range(0, 4);  // 0 to 3 rocks
            default:
                return 0; // No rocks
        }
    }

    public void PlaceRocks(int numberOfRocks, int distance)
    {
        Debug.Log($"Placing {numberOfRocks} rocks, distance to mountain: {distance}");
        int rocksPlaced = 0; // Local variable to count rocks placed in this method call
        for (int i = 0; i < numberOfRocks; i++)
        {
            Vector2Int position = GetRandomValidPosition();
            if (position != Vector2Int.zero)
            {
                Rock rock;
                if (distance == 1)
                {
                    rock = UnityEngine.Random.value > 0.2f ? (Rock)new LargeRock() : new SmallRock(); // 70% Large, 30% Small
                }
                else if (distance == 2)
                {
                    rock = UnityEngine.Random.value > 0.5f ? (Rock)new LargeRock() : new SmallRock(); // 50% Large, 50% Small
                }
                else if (distance == 3)
                {
                    rock = UnityEngine.Random.value > 0.7f ? (Rock)new LargeRock() : new SmallRock(); // 30% Large, 70% Small
                }
                else
                {
                    rock = new SmallRock(); // 100% Small
                }
                rock.Position = position;
                AreaMap[position.x, position.y].Objects.Add(rock);
                Debug.Log($"Placed {rock.Name} at position {position}");
                rocksPlaced++; // Increment rocks placed
            }
            else
            {
                Debug.LogWarning("No valid position found for placing a rock.");
            }
        }

        NumberOfRocks += rocksPlaced; // Update the class-level number of rocks placed
        Debug.Log($"Total number of rocks placed so far: {NumberOfRocks}");
    }

    public void AddTreesBasedOnProximityToForest(int distance)
    {
        int numberOfTrees = DetermineNumberOfTrees(distance);
        PlaceTrees(numberOfTrees, distance);
    }

    public int DetermineNumberOfTrees(int distance)
    {
        switch (distance)
        {
            case 1:
                return UnityEngine.Random.Range(10, 15); // 10 to 15 trees
            case 2:
                return UnityEngine.Random.Range(5, 10); // 5 to 10 trees
            case 3:
                return UnityEngine.Random.Range(2, 5);  // 2 to 5 trees
            case 4:
                return UnityEngine.Random.Range(0, 2);  // 0 to 2 trees
            default:
                return 0; // No trees
        }
    }

    public void PlaceTrees(int numberOfTrees, int distance)
    {
        Debug.Log($"Placing {numberOfTrees} trees, distance to forest: {distance}");

        // Retrieve the native tree fruit for this region using FindNativePlant
        string nativeTreeFruit = RegionManager.Instance.FindNativePlant("tree", RegionNumber) ?? "Apple"; // Default to "Apple" if no specific tree fruit is found

        // Local variable to count trees placed in this method call
        int treesPlaced = 0;

        // Place trees within the area
        for (int i = 1; i <= numberOfTrees; i++)
        {
            // Ensure that we're placing the tree in a valid empty spot
            Vector2Int position = GetRandomValidPosition();
            if (position != Vector2Int.zero && AreaMap[position.x, position.y].Objects.Count == 0)
            {
                // Roll to determine if this tree will produce fruit
                bool producesFruit = UnityEngine.Random.value > 0.5f; // 50% chance to produce fruit

                // Create the tree object
                List<Item> initialFruits = new List<Item>();
                if (producesFruit)
                {
                    initialFruits = GenerateInitialFruitsForTree(nativeTreeFruit);
                }

                // Create the tree, pass producesFruit
                Tree tree = new Tree(producesFruit ? nativeTreeFruit : "Non-Fruit", initialFruits, producesFruit);
                tree.Name = producesFruit ? $"{nativeTreeFruit} Tree {i}" : $"Non-Fruit Tree {i}"; // Set the tree name
                tree.IsPassable = false;
                tree.CurrentNestedArea = this;
                tree.IsInNestedArea = true;

                // Place the tree in the area
                AreaMap[position.x, position.y].Objects.Add(tree);
                AreaMap[position.x, position.y].Terrain = TerrainType.Dirt;

                // Add the tree to the area
                AddPlantToArea(tree);

                Debug.Log($"Placed {tree.Name} at position {position} with fruit: {producesFruit}");

                treesPlaced++; // Increment the count of trees placed
            }
            else
            {
                Debug.LogWarning("No valid position found for placing a tree or cell is not empty.");
            }
        }

        NumberOfTrees += treesPlaced; // Update the class-level number of trees placed
        Debug.Log($"Total number of trees placed so far: {NumberOfTrees}");
    }



    private List<Item> GenerateInitialFruitsForTree(string nativeTreeFruit)
    {
        List<Item> initialFruits = new List<Item>();
        Item fruitItem = ItemGenerator.Instance.GenerateItem(nativeTreeFruit);

        if (fruitItem != null)
        {
            int quantity = UnityEngine.Random.Range(1, 5); // Randomly decide how many fruits the tree starts with
            for (int i = 0; i < quantity; i++)
            {
                initialFruits.Add(fruitItem);
            }
        }
        else
        {
            Debug.LogWarning($"Could not generate initial fruits for tree with fruit type: {nativeTreeFruit}");
        }

        return initialFruits;
    }

    public void AddFlowersBasedOnFertility()
    {
        Debug.Log("Placing flowers based on cell fertility");
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Cell cell = AreaMap[x, y];
                if (cell != null && cell.FertilityValue > 0)
                {
                    int numberOfFlowers = DetermineNumberOfFlowers(ParentCell.FertilityValue);
                    for (int i = 0; i < numberOfFlowers; i++)
                    {
                        Flower flower = new Flower();
                        flower.Position = new Vector2Int(x, y);
                        AreaMap[x, y].Objects.Add(flower);
                        Debug.Log($"Placed {flower.Name} at position {new Vector2Int(x, y)} based on fertility {cell.FertilityValue}");
                    }
                }
            }
        }
    }

    public int DetermineNumberOfFlowers(int fertility)
    {
        if (fertility > 80)
            return UnityEngine.Random.Range(5, 10); // 5 to 10 flowers
        else if (fertility > 50)
            return UnityEngine.Random.Range(3, 6); // 3 to 6 flowers
        else if (fertility > 20)
            return UnityEngine.Random.Range(1, 3); // 1 to 3 flowers
        else
            return 0; // No flowers
    }

    // Implementing the AddPlantToArea method
    public void AddPlantToArea(PlantBase plant)
    {
        if (plant != null && IsValidPosition(plant.Position))
        {
            plantsInArea.Add(plant);
            // Only call AddObjectToArea if it's not already handled elsewhere
            if (!AreaMap[plant.Position.x, plant.Position.y].Objects.Contains(plant))
            {
                AddObjectToArea(plant);
            }
            Debug.Log($"Plant {plant.Name} added to area at position {plant.Position}.");
        }
    }


    // Implementing the RemovePlantFromArea method
    public void RemovePlantFromArea(PlantBase plant)
    {
        if (plant != null && plantsInArea.Contains(plant))
        {
            plantsInArea.Remove(plant);
            RemoveObjectFromArea(plant); // Ensure this does not fail if plant is already removed
            Debug.Log($"Plant {plant.Name} removed from area at position {plant.Position}.");
        }
    }


    // Implementing the GetAllPlantsInArea method
    public List<PlantBase> GetAllPlantsInArea()
    {
        return new List<PlantBase>(plantsInArea);
    }

    // Implementing the UpdatePlantGrowth method
    public void UpdatePlantGrowth()
    {
        foreach (var plant in plantsInArea)
        {
            plant.ProgressGrowth();
        }
        Debug.Log("Plant growth updated for all plants in the area.");
    }

    public void CreateWallsAroundMap(string wallType)
    {
        // Function to create a wall object based on the wallType string
        Func<Vector2Int, IInteractable> createWall = (position) =>
        {
            IInteractable wall = null;
            switch (wallType)
            {
                case "WoodenWall":
                    wall = new WoodenWall { Position = position, NestedMapPosition = position };
                    break;
                case "StoneWall":
                    wall = new StoneWall { Position = position, NestedMapPosition = position };
                    break;
                default:
                    Debug.LogWarning($"Unknown wall type: {wallType}");
                    return null;
            }
            return wall;
        };

        Vector2Int mapSize = GetSize();
        int mapWidth = mapSize.x;
        int mapHeight = mapSize.y;

        // Iterate over the edges of the map
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                // Top row and bottom row (except the first two cells on each side)
                if (y == mapHeight - 1 || (y == 0 && x >= 2 && x < mapWidth - 2))
                {
                    AddWallAtPosition(new Vector2Int(x, y), createWall);
                }
                // Left and right edges, except the bottom two rows
                else if ((x == 0 || x == mapWidth - 1) && y >= 2)
                {
                    AddWallAtPosition(new Vector2Int(x, y), createWall);
                }
            }
        }

        Debug.Log($"Walls of type {wallType} have been added around the map, excluding the bottom two rows.");
    }

    // Helper method to add the wall at the specified position
    private void AddWallAtPosition(Vector2Int position, Func<Vector2Int, IInteractable> createWall)
    {
        Cell cell = GetCellAtPosition(position);
        if (cell != null)
        {
            IInteractable wall = createWall(position);
            if (wall != null)
            {
                AddObjectToArea(wall);
                cell.isPassable = false;  // Set cell as impassable due to the wall
                Debug.Log($"{wall.Name} added at {position}");
            }
            else
            {
                Debug.LogWarning($"Failed to create a wall at {position}");
            }
        }
        else
        {
            Debug.LogWarning($"Invalid position {position} for adding a wall.");
        }
    }

    public void PlaceRandomWalls(int numberOfWalls, string wallType)
    {
        Debug.Log($"Placing {numberOfWalls} random walls of type {wallType} in the area.");

        Func<Vector2Int, IInteractable> createWall = (position) =>
        {
            IInteractable wall = null;
            switch (wallType)
            {
                case "WoodenWall":
                    wall = new WoodenWall { Position = position, NestedMapPosition = position };
                    break;
                case "StoneWall":
                    wall = new StoneWall { Position = position, NestedMapPosition = position };
                    break;
                default:
                    Debug.LogWarning($"Unknown wall type: {wallType}");
                    return null;
            }
            return wall;
        };

        // Get all valid cells where we can place walls
        List<Vector2Int> validCells = GetValidCells(cell => cell.isPassable && cell.Objects.Count == 0);

        if (validCells.Count < numberOfWalls)
        {
            Debug.LogWarning("Not enough valid cells to place all the walls.");
            numberOfWalls = validCells.Count;  // Adjust number of walls to the available valid cells
        }

        for (int i = 0; i < numberOfWalls; i++)
        {
            // Randomly pick a valid position and remove it from the list
            int randomIndex = UnityEngine.Random.Range(0, validCells.Count);
            Vector2Int randomPosition = validCells[randomIndex];
            validCells.RemoveAt(randomIndex);

            // Create and place the wall
            IInteractable wall = createWall(randomPosition);
            if (wall != null)
            {
                AddObjectToArea(wall);
                Cell cell = GetCellAtPosition(randomPosition);
                cell.isPassable = false;  // Set the cell as impassable due to the wall
                Debug.Log($"Placed {wallType} wall at position {randomPosition}.");
            }
            else
            {
                Debug.LogWarning($"Failed to create {wallType} wall at position {randomPosition}.");
            }
        }
    }


    public void PlaceFungiNearWallsTreesOrRocks(int numberOfFungi)
    {
        Debug.Log($"Placing {numberOfFungi} fungi near walls, trees, or rocks.");

        // Retrieve the native fungi for this region using FindNativePlant
        string nativeFungi = RegionManager.Instance.FindNativePlant("fungi", RegionNumber) ?? "Button Mushroom"; // Default to "Common Fungi" if none is found

        // Get all valid positions near walls, trees, or rocks
        List<Vector2Int> validFungiPositions = GetValidCells(cell =>
        {
            Vector2Int position = cell.Coordinates;  // Use Coordinates instead of Position
                                                     // Check if the adjacent cells have walls, trees, or rocks
            return IsAdjacentToWallTreeOrRock(position);
        });

        if (validFungiPositions.Count < numberOfFungi)
        {
            Debug.LogWarning("Not enough valid positions near walls, trees, or rocks to place all fungi.");
            numberOfFungi = validFungiPositions.Count;  // Adjust number of fungi to available positions
        }

        for (int i = 0; i < numberOfFungi; i++)
        {
            // Randomly pick a valid position and remove it from the list
            int randomIndex = UnityEngine.Random.Range(0, validFungiPositions.Count);
            Vector2Int randomPosition = validFungiPositions[randomIndex];
            validFungiPositions.RemoveAt(randomIndex);

            // Create and place the fungi, passing the nativeFungi type
            Fungi fungi = new Fungi(nativeFungi) { Position = randomPosition };
            AddObjectToArea(fungi);

            Debug.Log($"Placed {nativeFungi} fungi at position {randomPosition} near a wall, tree, or rock.");
        }
    }


    // Helper method to check if a position is adjacent to a wall, tree, or rock
    private bool IsAdjacentToWallTreeOrRock(Vector2Int position)
    {
        List<Vector2Int> adjacentPositions = GetNearbyCells(position, 1);  // Get adjacent cells
        foreach (var adjacentPosition in adjacentPositions)
        {
            Cell adjacentCell = GetCellAtPosition(adjacentPosition);
            if (adjacentCell != null)
            {
                // Check if the adjacent cell has a wall, tree, or rock
                bool hasWall = adjacentCell.Objects.Any(obj => obj is WoodenWall || obj is StoneWall);
                bool hasTree = adjacentCell.Objects.Any(obj => obj is Tree);
                bool hasRock = adjacentCell.Objects.Any(obj => obj is Rock);

                if (hasWall || hasTree || hasRock)
                {
                    return true;  // If adjacent to a wall, tree, or rock, return true
                }
            }
        }
        return false;  // No adjacent wall, tree, or rock found
    }

    protected void PlaceCampInNestedArea(int campID)
    {
        // Retrieve the camp data from PermaLists using the CampID
        var camp = PermaLists.Instance.Camps.FirstOrDefault(c => c.CampID == campID);

        if (camp == null)
        {
            Debug.LogError($"PlaceCampInNestedArea: Camp with ID {campID} not found.");
            return;
        }

        Debug.Log($"Placing camp with ID {campID} in nested area at cell {camp.Location.CellID}.");

        // Define the range for the central part of the map (x3 to x7, y3 to y7)
        int minX = 3;
        int maxX = 7;
        int minY = 3;
        int maxY = 7;

        // Randomly select coordinates within the central area for placing the campfire
        int randomX = UnityEngine.Random.Range(minX, maxX + 1); // +1 because Range is exclusive on the upper bound
        int randomY = UnityEngine.Random.Range(minY, maxY + 1);

        Vector2Int randomPosition = new Vector2Int(randomX, randomY);
        Cell campFireCell = GetCellAtPosition(randomPosition);

        if (campFireCell != null)
        {
            // Create and place the campfire
            Campfire campfire = new Campfire
            {
                Position = randomPosition
            };
            campFireCell.Objects.Add(campfire);
            campFireCell.isPassable = false;
            Debug.Log($"Placed Campfire at random position {randomPosition} for camp {campID}.");

            // Spawn NPCs for this camp
            PlaceNPCsFromList(camp.CampNPCs);
        }
        else
        {
            Debug.LogError("Campfire cell is null. Cannot place Campfire.");
        }
    }

    public void PlaceNPCsFromList(List<NPC> npcList)
    {
        if (npcList == null || npcList.Count == 0)
        {
            Debug.LogWarning("No NPCs provided to place in the area.");
            return;
        }

        // Get all valid positions in the nested area where NPCs can be placed
        List<Vector2Int> validPositions = GetValidCells(cell => cell.isPassable && cell.Objects.Count == 0);
        Debug.Log($"Found {validPositions.Count} valid positions for placing NPCs.");

        if (validPositions.Count == 0)
        {
            Debug.LogError("No valid positions available for placing NPCs.");
            return;
        }

        // Iterate through the NPCs in the list and place them at valid positions
        foreach (var npc in npcList)
        {
            if (validPositions.Count == 0)
            {
                Debug.LogWarning("No more valid positions left to place NPCs.");
                break; // Stop placing NPCs if there are no valid positions remaining
            }

            // Randomly select a valid position from the list
            int randomIndex = UnityEngine.Random.Range(0, validPositions.Count);
            Vector2Int npcPosition = validPositions[randomIndex];
            validPositions.RemoveAt(randomIndex); // Remove the selected position so it's not reused

            // Place the NPC at the selected position
            npc.NestedMapPosition = npcPosition; // Update NPC's position

            Cell npcCell = GetCellAtPosition(npcPosition);
            if (npcCell != null)
            {
                npcCell.Objects.Add(npc); // Add NPC to the cell's object list
                npcCell.isPassable = false; // NPCs make the cell impassable
                Debug.Log($"Placed NPC {npc.Name} at position {npcPosition} in the nested area.");
            }
            else
            {
                Debug.LogError($"Failed to place NPC {npc.Name}: Cell at position {npcPosition} is null.");
            }
        }

        Debug.Log($"Successfully placed {npcList.Count} NPCs in the nested area.");
    }


    // Method to get appropriate NPC role based on camp type
    private NPCRole GetNPCRoleForCampType(CampType campType)
    {
        switch (campType)
        {
            case CampType.BanditCamp:
                return NPCRole.Bandit;
            case CampType.TraderCamp:
                return NPCRole.Trader;
            case CampType.RefugeeCamp:
                return NPCRole.Villager;
            case CampType.ExplorerCamp:
                return NPCRole.Explorer;
            case CampType.HunterCamp:
                return NPCRole.Hunter;
            default:
                Debug.LogError($"Unknown camp type: {campType}");
                return NPCRole.Villager; // Default role
        }
    }


    // Helper method to get a random nearby position for NPC placement
    private Vector2Int GetRandomNearbyPosition(Vector2Int campPosition)
    {
        int offsetX = UnityEngine.Random.Range(-1, 2); // Random offset between -1 and 1
        int offsetY = UnityEngine.Random.Range(-1, 2);

        Vector2Int nearbyPosition = new Vector2Int(campPosition.x + offsetX, campPosition.y + offsetY);

        // Ensure that the position is within bounds and valid
        if (IsValidPosition(nearbyPosition) && GetCellAtPosition(nearbyPosition).Objects.Count == 0)
        {
            return nearbyPosition;
        }

        // Fallback to campPosition if no valid nearby position is found
        return campPosition;
    }

    // New method to set a random seed for this nested area based on ParentCellID and GameSeed
    protected void SetRandomSeed(int parentCellID)
    {
        // Combine the ParentCellID with a global game seed to create a unique seed
        int seedValue = parentCellID + GameManager.Instance.GameSeed;

        // Set the random seed using Unity's random number generator
        UnityEngine.Random.InitState(seedValue);

        // Log the seed value for debugging
        Debug.Log($"Random seed set to {seedValue} for nested area.");
    }

    public void PlaceLongGrass(int numberOfGrassPatches)
    {
        Debug.Log($"Placing {numberOfGrassPatches} patches of LongGrass in the area.");

        int grassPatchesPlaced = 0; // Local variable to track the number of grass patches placed in this call

        // Place grass patches within the area
        for (int i = 0; i < numberOfGrassPatches; i++)
        {
            // Use GetRandomValidPosition() to select a valid position
            Vector2Int grassPosition = GetRandomValidPosition();

            // Ensure we have a valid position
            if (grassPosition != Vector2Int.zero)
            {
                // Create and place the long grass at the selected position
                LongGrass longGrass = new LongGrass();
                AddObjectToArea(longGrass);

                // Make the cell passable since it's grass
                Cell cell = GetCellAtPosition(grassPosition);
                if (cell != null)
                {
                    cell.Objects.Add(longGrass); // Add the long grass to the cell's object list
                    Debug.Log($"Placed LongGrass at position {grassPosition}.");
                    grassPatchesPlaced++; // Increment local counter
                }
                else
                {
                    Debug.LogWarning($"Failed to place LongGrass at position {grassPosition}: cell is null.");
                }
            }
            else
            {
                Debug.LogWarning("No valid position found for placing long grass.");
            }
        }

        NumberOfGrassPatches += grassPatchesPlaced; // Update the class-level total grass patches placed
        Debug.Log($"Total number of grass patches placed so far: {NumberOfGrassPatches}");
    }

}
