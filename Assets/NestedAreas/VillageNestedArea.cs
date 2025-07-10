using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Village : BaseNestedArea
{
    public string VillageName { get; set; }
    public Cell Location { get; set; }
    public VillageType VillageType { get; set; }
    public Vector2Int SignPosition { get; set; }
    public Vector2Int CampFirePosition { get; set; }
    public Vector2Int DonationCratePosition { get; set; }
    public List<NPC> VillageNPCs { get; set; } = new List<NPC>();
    public List<NPC> AvailableVillageNPCs { get; set; } = new List<NPC>();
    public List<NPC> GuestNPCs { get; set; } = new List<NPC>();
    public VillageStats Stats { get; private set; }
    public Faction Faction { get; set; }
    public Dictionary<string, bool> FulfilledRoles { get; private set; } = new Dictionary<string, bool>();

    public Village(Faction faction, VillageType villageType)
    {
        Faction = faction;
        VillageType = villageType;  // Set VillageType in the constructor
        Initialize();  // Initialize the village, now with the correct VillageType
        EntrancePosition = new Vector2Int(0, 0);

        // Place a Sign at the fixed location (6, 4)
        SignPosition = new Vector2Int(6, 4);
        AddSignToVillage();

        // Place the DonationCrate next to the SignPosition
        DonationCratePosition = new Vector2Int(SignPosition.x + 1, SignPosition.y);
        AddDonationCrateToVillage();

        // Place the CampFire at a desired location (let's say (6, 4))
        CampFirePosition = new Vector2Int(3, 3);
        AddCampFireToVillage();

        // Use GetWallType to determine the wall type for the village based on VillageType
        CreateWallsAroundMap(GetWallType(VillageType));
    }

    // Constructor without arguments
    public Village()
    {
        Initialize();
        EntrancePosition = new Vector2Int(0, 0);

        // Place a Sign at the fixed location (7, 5)
        SignPosition = new Vector2Int(7, 5);
        AddSignToVillage();

        // Place the DonationCrate next to the SignPosition
        DonationCratePosition = new Vector2Int(SignPosition.x + 1, SignPosition.y);
        AddDonationCrateToVillage();

        // Place the CampFire at a desired location (let's say (6, 4))
        CampFirePosition = new Vector2Int(3, 3);
        AddCampFireToVillage();

        // Use GetWallType to determine the wall type for the village based on VillageType
        CreateWallsAroundMap(GetWallType(VillageType));

    }

    public override void Initialize()
    {
        AreaMap = new Cell[Size, Size];
        TerrainType terrainType = GetTerrainType(VillageType);
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                int cellID = GenerateUniqueCellID();
                Cell newCell = new Cell(cellID, x, y, terrainType); 
                AreaMap[x, y] = newCell;

                // Add the new cell to the PermaList's AllCells list
                PermaLists.Instance.AllMapCells.Add(newCell);
            }
        }

        int nestedAreaID = GameManager.Instance.GetNestedAreaID();
        NestedAreaID = nestedAreaID;

    }


    private void AddSignToVillage()
    {
        IInteractable sign = new VillageSignPost(this);
        Cell signCell = GetCellAtPosition(SignPosition);

        if (signCell != null)
        {
            signCell.Objects.Add(sign);
            signCell.isPassable = false;
        }
        else
        {
            Debug.LogError("Sign cell is null. Cannot add Sign.");
        }
    }


    private void AddDonationCrateToVillage()
    {
        IInteractable donationCrate = new DonationCrate(this);
        Cell donationCrateCell = GetCellAtPosition(DonationCratePosition);
        if (donationCrateCell != null)
        {
            donationCrateCell.Objects.Add(donationCrate);
            donationCrateCell.isPassable = false;
        }
        else
        {
            Debug.LogError("Donation crate cell is null. Cannot add Donation Crate.");
        }
    }

    private void AddCampFireToVillage()
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


    public void InitializeVillageStats(string name, int population, int prestige)
    {
        Stats = new VillageStats(name)
        {
            Population = population,
            PrestigeLevel = prestige,
            StoredFood = UnityEngine.Random.Range(50, 101),
            StoredWater = UnityEngine.Random.Range(50, 101),
            StoredWood = UnityEngine.Random.Range(50, 101),
            StoredStone = UnityEngine.Random.Range(50, 101)
        };
    }

    public void RefreshFulfilledRoles(List<string> roles)
    {
        foreach (var role in roles)
        {
            FulfilledRoles[role] = false;
        }
        foreach (var npc in VillageNPCs)
        {
            if (FulfilledRoles.ContainsKey(npc.Role.ToString()))
            {
                FulfilledRoles[npc.Role.ToString()] = true;
            }
        }
    }

    public string GetWallType(VillageType villageType)
    {
        switch (villageType)
        {
            case VillageType.HumanVillage:
            case VillageType.SwampVillage:
            case VillageType.ElvenGrove:
            case VillageType.SabrenCamp:
                return "WoodenWall";

            case VillageType.CaraphraxNest:
            case VillageType.DwarvenHall:
                return "StoneWall";

            default:
                return "WoodenWall"; // Default if no matching VillageType
        }
    }

    public TerrainType GetTerrainType(VillageType villageType)
    {
        switch (villageType)
        {
            case VillageType.HumanVillage:
                return TerrainType.Path;

            case VillageType.DwarvenHall:
                return TerrainType.Stone;

            case VillageType.SwampVillage:
                return TerrainType.Plank;

            case VillageType.ElvenGrove:
            case VillageType.SabrenCamp:
            case VillageType.CaraphraxNest:
            default:
                return TerrainType.Land; // Default for other types
        }
    }


    public override void UpdatePlayerPosition(Vector2Int newPosition)
    {
        Cell newCellPosition = GetCellAtPosition(newPosition);
        PlayerStats.Instance.UpdateCurrentCellID(newCellPosition.CellID);
        PlayerStats.Instance.UpdateParentNestedAreaID(newCellPosition.ParentAreaID);
    }

    public override void UpdateCharacterPosition(Character character, Vector2Int newPosition)
    {
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
        if (position.x >= 0 && position.x < AreaMap.GetLength(0) &&
            position.y >= 0 && position.y < AreaMap.GetLength(1))
        {
            return AreaMap[position.x, position.y];
        }
        return null;
    }

    public override void HandlePlayerExit(MapGenerator mapGenerator)
    {
        Vector2Int playerExitPosition = PlayerStats.Instance.NestedMapPosition;
        Cell cellToUpdate = GetCellAtPosition(playerExitPosition);
        if (cellToUpdate != null)
        {
            cellToUpdate.isPassable = true;
        }

        DeregisterAllNPCs();
        // No need to deregister animals as the Village does not have animals.

        HandlePlayerExitFromSpecificNestedAreaType(mapGenerator);
    }

    public override void HandlePlayerExitFromSpecificNestedAreaType(MapGenerator mapGenerator)
    {
        // Village-specific exit logic, currently empty
    }

    private void DeregisterAllNPCs()
    {
        foreach (var npc in GetAllNPCsInArea())
        {
            DeregisterCharacterFromTurnManager(npc);
        }
    }

    public void AddNPC(NPC npc)
    {
        VillageNPCs.Add(npc);
        AvailableVillageNPCs.Add(npc);

        // Check if this NPC has a need, and if so, generate news
        if (npc.CurrentNeed.HasNeed)
        {
            string needDescription = $"{npc.Name} needs {npc.CurrentNeed.NumberRequired} {npc.CurrentNeed.ItemName}.";
            NewsManager.Instance.GenerateNewsForVillage(this);
        }
    }

    public void DisplayNews(NewsType newsType)
    {
        NewsManager.Instance.DisplayNews(NewsManager.Instance.GetVillageNews(this));
    }

    public void ExpandTerritory()
    {
        if (Stats.ExpansionPoints < 10) return; // Ensure minimum points to consider expansion

        List<Cell> candidates = GetValidExpansionCandidates();
        if (candidates.Count == 0) return; // No valid land

        Dictionary<Cell, float> expansionScores = ScoreExpansionTiles(candidates);

        // Sort tiles by score (best first)
        var sortedTiles = expansionScores.OrderByDescending(kv => kv.Value).ToList();

        // Check if the best tile is affordable
        Cell bestTile = sortedTiles[0].Key;
        float bestTileCost = Faction.Race.GetExpansionModifier(bestTile.Terrain);

        if (Stats.ExpansionPoints < bestTileCost)
        {
            Debug.Log($"{VillageName} wants to expand to {bestTile.Terrain} but lacks {bestTileCost - Stats.ExpansionPoints} points. Waiting.");
            return; // Not enough points, so we wait
        }

        // Convert scores into weighted choices
        Cell selectedTile = SelectWeightedTile(expansionScores);
        float selectedTileCost = Faction.Race.GetExpansionModifier(selectedTile.Terrain);

        if (Stats.ExpansionPoints < selectedTileCost)
        {
            Debug.Log($"{VillageName} wants {selectedTile.Terrain} but lacks points. Waiting.");
            return; // Not enough points, so we wait
        }

        ExpandIntoTile(selectedTile);
    }

    public void ExpandIntoTile(Cell tile)
    {
        float expansionCost = Faction.Race.GetExpansionModifier(tile.Terrain);
        Stats.ExpansionPoints -= Mathf.RoundToInt(expansionCost);

        tile.IsOwned = true;
        tile.OwnedBy = VillageName;

        // Adjust village resources based on new land
        if (tile.HasTag(tile.ResourceTagFlags, ResourceTags.Food)) Stats.StoredFood += 10;
        if (tile.HasTag(tile.ResourceTagFlags, ResourceTags.Water)) Stats.StoredWater += 5;
        if (tile.HasTag(tile.ResourceTagFlags, ResourceTags.Wood)) Stats.StoredWood += 10;
        if (tile.HasTag(tile.ResourceTagFlags, ResourceTags.Stone)) Stats.StoredStone += 5;

        Debug.Log($"{VillageName} expanded into {tile.Terrain} at {tile.Coordinates}");
    }


    public Cell SelectWeightedTile(Dictionary<Cell, float> scores)
    {
        float totalWeight = scores.Values.Sum(); // Sum of all scores

        float randomRoll = UnityEngine.Random.Range(0, totalWeight);
        float currentWeight = 0;

        foreach (var entry in scores)
        {
            currentWeight += entry.Value;
            if (randomRoll <= currentWeight)
            {
                return entry.Key; // Select this tile
            }
        }

        return scores.Keys.First(); // Fallback (should never reach here)
    }

    private int CountAdjacentOwnedCells(Cell cell)
    {
        Vector2Int[] directions = {
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 0), new Vector2Int(-1, 0)
    };

        int count = 0;
        foreach (var direction in directions)
        {
            Cell neighbor = MapGenerator.Instance.GetCell(cell.Coordinates + direction);
            if (neighbor != null && neighbor.IsOwned && neighbor.OwnedBy == VillageName)
            {
                count++;
            }
        }

        return count;
    }

    private float EvaluateCellExpansionScore(Cell cell)
    {
        float score = 0f;

        // Prioritize expansion based on resources
        switch (cell.Terrain)
        {
            case TerrainType.Land:
                score += Stats.StoredFood < 50 ? 10 : 3;
                break;
            case TerrainType.Forest:
                score += Stats.StoredWood < 50 ? 8 : 2;
                break;
            case TerrainType.River:
                score += Stats.StoredWater < 50 ? 10 : 5;
                break;
            case TerrainType.Mountain:
                score += Stats.StoredStone < 50 ? 6 : 3;
                break;
            default:
                score += 1; // Default for neutral terrain
                break;
        }

        // Add weight for connected expansion
        score += CountAdjacentOwnedCells(cell) * 2;

        // Defensive advantage
        if (cell.Terrain == TerrainType.Forest || cell.Terrain == TerrainType.Mountain)
        {
            score += 4;
        }

        return score;
    }

    public List<Cell> GetValidExpansionCandidates()
    {
        List<Cell> candidates = new List<Cell>();
        Vector2Int[] directions = {
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 0), new Vector2Int(-1, 0)
    };

        foreach (Cell cell in PermaLists.Instance.AllMapCells)
        {
            if (cell.IsOwned && cell.OwnedBy == VillageName)
            {
                foreach (var direction in directions)
                {
                    Vector2Int newPosition = cell.Coordinates + direction;
                    Cell neighbor = MapGenerator.Instance.GetCell(newPosition);

                    if (neighbor != null && !neighbor.IsOwned && neighbor.Terrain != TerrainType.Water)
                    {
                        candidates.Add(neighbor);
                    }
                }
            }
        }
        return candidates;
    }

    public Dictionary<Cell, float> ScoreExpansionTiles(List<Cell> candidates)
    {
        Dictionary<Cell, float> expansionScores = new Dictionary<Cell, float>();

        foreach (var cell in candidates)
        {
            float score = 0f;

            // Get race-specific terrain preference
            float terrainCost = Faction.Race.GetExpansionModifier(cell.Terrain);
            score -= terrainCost; // Lower cost = higher priority

            // Prioritize missing resources
            if (cell.HasTag(cell.ResourceTagFlags, ResourceTags.Food) && Stats.StoredFood < 100) score += 20;
            if (cell.HasTag(cell.ResourceTagFlags, ResourceTags.Water) && Stats.StoredWater < 100) score += 15;
            if (cell.HasTag(cell.ResourceTagFlags, ResourceTags.Wood) && Stats.StoredWood < 50) score += 10;
            if (cell.HasTag(cell.ResourceTagFlags, ResourceTags.Stone) && Stats.StoredStone < 50) score += 5;

            // Defensive advantage (mountains can be good for some races)
            if (cell.Terrain == TerrainType.Mountain) score += 5;

            expansionScores[cell] = score;
        }
        return expansionScores;
    }



    public void InitializeVillageRelationships()
    {
        foreach (NPC npc in VillageNPCs)
        {
            foreach (NPC otherNPC in VillageNPCs)
            {
                if (npc != otherNPC)
                {
                    npc.AddRelationship(otherNPC, UnityEngine.Random.Range(-5, 5)); // Slight variations
                }
            }
        }
    }


}

public class VillageStats
{
    public string VillageName { get; set; }

    // Population stats
    public int Population { get; set; }
    public int MaxPopulation { get; set; }

    // Core Stored Resources (New System)
    public int StoredFood { get; set; }
    public int MaxStoredFood { get; set; }
    public int StoredWater { get; set; }
    public int MaxStoredWater { get; set; }
    public int StoredWood { get; set; }
    public int MaxStoredWood { get; set; }
    public int StoredStone { get; set; }
    public int MaxStoredStone { get; set; }

    // Resource Gain (Dynamic Daily Production)
    public int FoodGain { get; set; }
    public int WaterGain { get; set; }
    public int WoodGain { get; set; }
    public int StoneGain { get; set; }

    // Resource Consumption (Dynamic Daily Needs)
    public int FoodConsumption => Population * 2;
    public int WaterConsumption => Population;

    // Prestige
    public int Prestige { get; set; }
    public int PrestigeLevel { get; set; }
    public int MaxPrestige { get; set; }

    // Expansion
    public int ExpansionPoints { get; set; }

    // Role management
    public Dictionary<string, bool> RolesPresent { get; set; }
    public ItemType NeededResource { get; set; }

    // Social stats
    public int Happiness { get; set; }
    public int MaxHappiness { get; set; }
    public int Unrest { get; set; }
    public int MaxUnrest { get; set; }
    public int CrimeRate { get; set; }
    public int MaxCrimeRate { get; set; }

    // Defence and health stats
    public int Defence { get; set; }
    public int MaxDefence { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }

    // Economic stats
    public int TradeValue { get; set; }
    public int MaxTradeValue { get; set; }

    // Magical stats
    public int ArcaneInfluence { get; set; }
    public int MaxArcaneInfluence { get; set; }

    // Player-related stats
    public int PlayerRecognition { get; set; }
    public int MaxPlayerRecognition { get; set; }
    public int PlayerRenown { get; set; }
    public int MaxPlayerRenown { get; set; }

    public VillageStats(string name)
    {
        VillageName = name;

        // Initialise core stats
        Population = 10;
        MaxPopulation = 100;

        // Stored Resources
        StoredFood = 100;
        MaxStoredFood = 500;
        StoredWater = 100;
        MaxStoredWater = 500;
        StoredWood = 50;
        MaxStoredWood = 300;
        StoredStone = 50;
        MaxStoredStone = 300;

        // Resource Production
        FoodGain = 5;
        WaterGain = 5;
        WoodGain = 2;
        StoneGain = 2;

        // Prestige
        Prestige = 0;
        PrestigeLevel = 0;
        MaxPrestige = 100;

        // Social & Stability
        Happiness = 50;
        MaxHappiness = 100;
        Unrest = 0;
        MaxUnrest = 100;
        CrimeRate = 10;
        MaxCrimeRate = 100;

        // Defence & Health
        Defence = 20;
        MaxDefence = 100;
        Health = 70;
        MaxHealth = 100;

        // Trade & Economy
        TradeValue = 50;
        MaxTradeValue = 100;

        // Magic & Influence
        ArcaneInfluence = 0;
        MaxArcaneInfluence = 100;

        PlayerRecognition = 0;
        MaxPlayerRecognition = 100;
        PlayerRenown = 0;
        MaxPlayerRenown = 100;

        RolesPresent = new Dictionary<string, bool>()
        {
            { "Blacksmith", false },
            { "Mayor", false },
            { "Healer", false },
            { "Trader", false }
        };
    }

    // -------------------- Updated Methods --------------------

    public void IncreaseHappiness(int amount)
    {
        Happiness = Mathf.Min(Happiness + amount, MaxHappiness);
    }

    public void DecreaseHappiness(int amount)
    {
        Happiness = Mathf.Max(Happiness - amount, 0);
        if (Happiness < 30) Unrest += 10;
    }

    public void IncreaseUnrest(int amount)
    {
        Unrest = Mathf.Min(Unrest + amount, MaxUnrest);
        if (Unrest > 70) CrimeRate += 5;
    }

    public void DecreaseUnrest(int amount)
    {
        Unrest = Mathf.Max(Unrest - amount, 0);
    }

    public void AdjustCrimeRate(int amount)
    {
        CrimeRate = Mathf.Clamp(CrimeRate + amount, 0, MaxCrimeRate);

        if (CrimeRate > 60) { TradeValue -= 5; Happiness -= 5; }
        if (CrimeRate < 20 && Defence > 50) { TradeValue += 3; }
    }

    public void ProcessDailyResourceUse()
    {
        StoredFood = Mathf.Clamp(StoredFood + FoodGain - FoodConsumption, 0, MaxStoredFood);
        StoredWater = Mathf.Clamp(StoredWater + WaterGain - WaterConsumption, 0, MaxStoredWater);
        StoredWood = Mathf.Clamp(StoredWood + WoodGain, 0, MaxStoredWood);
        StoredStone = Mathf.Clamp(StoredStone + StoneGain, 0, MaxStoredStone);

        if (StoredFood == 0) Happiness -= 10;
        if (StoredWater == 0) Happiness -= 10;
    }

    public void UpdateHealth()
    {
        if (StoredFood > Population * 3 && StoredWater > Population * 2)
        {
            Health = Mathf.Min(Health + 3, MaxHealth);
        }
        else if (StoredFood == 0 || StoredWater == 0)
        {
            Health = Mathf.Max(Health - 5, 0);
        }

        if (Health < 20 && Population > 1) Population--;
    }

    public void UpdatePopulation()
    {
        if (Health > 75 && StoredFood > Population * 4)
        {
            Population = Mathf.Min(Population + 1, MaxPopulation);
        }
        else if (Health < 25 || StoredFood == 0)
        {
            Population = Mathf.Max(Population - 1, 1);
        }
    }

    public void UpdateExpansionPoints()
    {
        if (Population > 50) ExpansionPoints += Mathf.FloorToInt(Population / 10);
        if (Population < 30) ExpansionPoints = Mathf.Max(ExpansionPoints - 2, 0);
    }

    public void DetermineNewNeeds()
    {
        Dictionary<string, int> resourceLevels = new Dictionary<string, int>
        {
            { "Food", StoredFood },
            { "Water", StoredWater },
            { "Wood", StoredWood },
            { "Stone", StoredStone }
        };

        string lowestResource = resourceLevels.OrderBy(r => r.Value).First().Key;

        switch (lowestResource)
        {
            case "Food": NeededResource = ItemType.Fruit; break;
            case "Water": NeededResource = ItemType.Water; break;
            case "Wood": NeededResource = ItemType.CraftingMaterial; break;
            case "Stone": NeededResource = ItemType.CraftingMaterial; break;
        }
    }

    public void AdjustPrestige(int amount)
    {
        Prestige = Mathf.Clamp(Prestige + amount, 0, MaxPrestige);
    }
}
