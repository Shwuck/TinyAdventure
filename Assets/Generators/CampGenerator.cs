using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CampGenerator : MonoBehaviour
{
    private static CampGenerator instance;
    public static CampGenerator Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<CampGenerator>();
                if (instance == null)
                {
                    var obj = new GameObject("CampGenerator");
                    instance = obj.AddComponent<CampGenerator>();
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

    // Method to generate and assign camps at the start of the game
    public void GenerateAndAssignCamps(int numberOfCamps)
    {
        UnityEngine.Random.InitState(GameManager.Instance.GameSeed); // Seed the random generator

        GameDebugger.Instance.LogWarning($"Count of Camps to Assign: {numberOfCamps}");

        // Fetch all valid cells that can host a camp
        List<Cell> allCells = GetAllCells().Where(IsValidCampCell).ToList();

        if (allCells == null || allCells.Count == 0)
        {
            GameDebugger.Instance.LogWarning("No cells available to assign camps.");
            return;
        }

        int campsCreated = 0;

        // Iterate through the required number of camps
        for (int i = 0; i < numberOfCamps; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, allCells.Count);
            var selectedCell = allCells[randomIndex];

            if (!selectedCell.HasCamp) // Ensure the cell does not already have a camp
            {
                selectedCell.HasCamp = true; // Mark this cell as having a camp
                campsCreated++;

                int campID = GameManager.Instance.GetCampID(); // Generate a unique CampID
                CampType campType = AssignRandomCampType();

                // Create the camp and assign it to the cell
                Camp newCamp = new Camp(campType, campID, selectedCell);
                selectedCell.CampID = campID; // Store camp ID in the cell
                selectedCell.Camp = newCamp;  // Assign the camp object to the cell

                // Add the camp to a global list of camps
                PermaLists.Instance.Camps.Add(newCamp); // Store generated camps in PermaLists

                GameDebugger.Instance.LogInfo($"Generated camp with ID {campID} at cell {selectedCell.CellID}, Type: {campType}");
            }
            else
            {
                GameDebugger.Instance.LogWarning($"Cell {selectedCell.CellID} already has a camp.");
            }

            // Remove the selected cell from the pool to prevent re-selection
            allCells.RemoveAt(randomIndex);
        }

        // Debug output for verification
        GameDebugger.Instance.LogInfo($"Total camps assigned: {numberOfCamps}");
        GameDebugger.Instance.LogInfo($"Total camps actually created: {campsCreated}");
        GameDebugger.Instance.LogInfo($"Current Camp List Count: {PermaLists.Instance.Camps.Count}");

        foreach (var camp in PermaLists.Instance.Camps)
        {
            GameDebugger.Instance.LogInfo($"Camp - ID: {camp.CampID}, CellID: {camp.Location.CellID}, Type: {camp.CampType}");
        }
    }

    // New method to generate NPCs for all camps
    public void GenerateNPCsForAllCamps()
    {
        Debug.Log("Generating NPCs for all camps");

        foreach (var camp in PermaLists.Instance.Camps)
        {
            // Determine how many NPCs to generate for the camp
            int npcCount = UnityEngine.Random.Range(2, 5); // Random number of NPCs between 2 and 5

            // Use NPCGenerator to generate camp-specific NPCs
            List<NPC> generatedNPCs = NPCGenerator.Instance.GenerateCampNPCs(npcCount, camp.Location, GetNPCRoleForCampType(camp.CampType));

            foreach (var npc in generatedNPCs)
            {
                npc.Faction = camp.CampName; // Assign NPC to camp faction
                GameDebugger.Instance.LogInfo($"NPC {npc.NPCID} ({npc.FirstName} {npc.Surname}) assigned to Camp_{camp.CampID}.");
            }

            // Add the generated NPCs to the camp's CampNPCs list
            camp.CampNPCs.AddRange(generatedNPCs);

            // Debugging to check NPC generation
            GameDebugger.Instance.LogInfo($"Generated {generatedNPCs.Count} NPCs for camp with ID {camp.CampID}, Type: {camp.CampType}");
        }
    }

    public void GenerateAnimalsForAllCamps()
    {
        Debug.Log("Generating animals for all camps");

        foreach (var camp in PermaLists.Instance.Camps)
        {
            // Generate horses (1-2 per camp)
            int horseCount = UnityEngine.Random.Range(1, 3);
            List<WildAnimal> horses = AnimalGenerator.Instance.GenerateAnimal("Horse", horseCount, camp.Location);

            foreach (var horse in horses)
            {
                horse.Animal.IsTame = true;
                horse.Animal.IsDomestic = true;
                horse.Animal.Faction = camp.CampName; // Assign to camp faction

                GameDebugger.Instance.LogInfo($"Horse (ID: {horse.Animal.AnimalID}) assigned to Camp_{camp.CampID} and tamed.");
            }

            camp.CampAnimals.AddRange(horses);
        }
    }

    // Method to randomly assign a camp type (Bandit, Trader, etc.)
    private CampType AssignRandomCampType()
    {
        CampType[] campTypes = { CampType.BanditCamp, CampType.TraderCamp, CampType.RefugeeCamp, CampType.ExplorerCamp, CampType.HunterCamp };
        int[] weights = { 20, 20, 20, 20, 20 }; // Adjust these for different probabilities

        int totalWeight = weights.Sum();
        int randomValue = UnityEngine.Random.Range(0, totalWeight);

        int cumulative = 0;
        for (int i = 0; i < campTypes.Length; i++)
        {
            cumulative += weights[i];
            if (randomValue < cumulative)
            {
                return campTypes[i];
            }
        }

        return CampType.HunterCamp; // Fallback
    }

    // Helper method to get the appropriate NPC role based on camp type
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
                return NPCRole.Scout;
            case CampType.HunterCamp:
                return NPCRole.Hunter;
            default:
                Debug.LogError($"Unknown camp type: {campType}");
                return NPCRole.Villager; // Default role
        }
    }

    private List<Cell> GetAllCells()
    {
        // Filter cells to only include those where IsMainMapCell is true
        var allCells = PermaLists.Instance.AllMapCells
                         .Where(cell => cell.isMainMapCell)
                         .ToList();

        GameDebugger.Instance.LogInfo($"Total main map cells fetched: {allCells?.Count ?? 0}");
        return allCells ?? new List<Cell>(); // Return empty list if null
    }

    private bool IsValidCampCell(Cell cell)
    {
        // Exclude cells that already have landmarks or camps
        if (cell.HasLandmark)
        {
            GameDebugger.Instance.LogInfo($"Cell {cell.CellID} is invalid due to Landmark.");
            return false;
        }

        // If you want to avoid certain terrains like water or mountains, you can keep these
        if (cell.Terrain == TerrainType.MountainPeak ||
            cell.Terrain == TerrainType.Water ||
            cell.Terrain == TerrainType.River ||
            cell.Terrain == TerrainType.Lake)
        {
            GameDebugger.Instance.LogInfo($"Cell {cell.CellID} is invalid due to terrain type {cell.Terrain}.");
            return false;
        }

        return true; // If the cell passes all checks, it's valid for camp placement
    }
}


// Enum for different camp types
public enum CampType
{
    BanditCamp,
    TraderCamp,
    RefugeeCamp,
    ExplorerCamp,
    HunterCamp
}

// Class representing the camp itself
public class Camp
{
    public int CampID { get; set; }
    public string CampName { get; set; }
    public CampType CampType { get; set; }
    public Cell Location { get; set; } // Changed this to Cell instead of CampCellID
    public List<NPC> CampNPCs { get; set; } // NPCs living in the camp
    public List<WildAnimal> CampAnimals { get; set; } // General animals in the camp
    public List<WildAnimal> CampHorses { get; set; } // Specifically for horses

    public Camp(CampType campType, int campID, Cell location)
    {
        CampType = campType;
        CampID = campID;
        CampName = $"{campType.ToString()}_{campID}";
        Location = location;
        CampNPCs = new List<NPC>(); // Initialize NPC list
        CampAnimals = new List<WildAnimal>(); // Initialize animal list
        CampHorses = new List<WildAnimal>(); // Initialize horse list
    }

    // Method to add an animal to the camp
    public void AddAnimal(WildAnimal animal)
    {
        if (animal == null) return;

        CampAnimals.Add(animal);

        // If the animal is a horse, store it separately for better management
        if (animal.Animal.Name == "Horse")
        {
            CampHorses.Add(animal);
        }

        GameDebugger.Instance.LogInfo($"Animal {animal.Animal.Name} (ID: {animal.Animal.AnimalID}) added to Camp_{CampID}.");
    }

    // Method to add multiple animals at once
    public void AddAnimals(List<WildAnimal> animals)
    {
        foreach (var animal in animals)
        {
            AddAnimal(animal);
        }
    }
}
