using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AnimalGenerator : MonoBehaviour
{
    // Singleton pattern
    private static AnimalGenerator _instance;
    public static AnimalGenerator Instance
    {
        get
        {
            if (_instance == null)
            {
                // Try to find an existing instance
                _instance = FindObjectOfType<AnimalGenerator>();

                // If no instance found, create a new one
                if (_instance == null)
                {
                    GameObject generatorObject = new GameObject("AnimalGenerator");
                    _instance = generatorObject.AddComponent<AnimalGenerator>();
                    DontDestroyOnLoad(generatorObject);
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        // Ensure that there is only one instance
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePermaLists();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    // Fields and Constants
    private const float InitialRarityChance = 0.01f;
    private float currentRarityChance = InitialRarityChance;

    private static readonly HashSet<TerrainType> ExcludedTerrains = new HashSet<TerrainType>
    {
        TerrainType.Village,
        TerrainType.Hall,
        TerrainType.Camp,
        TerrainType.Grove,
        TerrainType.Sand,
        TerrainType.Mountain,
        TerrainType.Water,
        TerrainType.MountainPeak
    };

    private static readonly Dictionary<AnimalSize, float> AnimalDistributionRatios = new Dictionary<AnimalSize, float>
    {
        { AnimalSize.Tiny, 0.5f },
        { AnimalSize.Small, 0.25f },
        { AnimalSize.Medium, 0.15f },
        { AnimalSize.Large, 0.08f },
        { AnimalSize.Huge, 0.02f }
    };

    // Initialization
    private void InitializePermaLists()
    {
        var permaLists = PermaLists.Instance;
        if (permaLists == null)
        {
            GameDebugger.Instance.LogError("PermaLists.Instance is null in InitializePermaLists");
            return;
        }

        permaLists.AllWildAnimals ??= new List<WildAnimal>();
        permaLists.NativeAnimalsPerTerrain ??= new Dictionary<TerrainType, HashSet<string>>();
        permaLists.AnimalsToGenerate ??= new Dictionary<int, List<string>>();
    }

    // Animal Generation
    public void BeginAnimalGeneration()
    {
        UnityEngine.Random.InitState(GameManager.Instance.GameSeed);
        SelectNativeAnimalsForAllTerrains();
    }

    public void PopulateWorld()
    {
        GenerateAnimalsForAllTerrains();
    }

    private void SelectNativeAnimalsForAllTerrains()
    {
        var permaLists = PermaLists.Instance;
        if (permaLists?.TerrainTypeCounts == null)
        {
            GameDebugger.Instance.LogError("PermaLists.Instance or PermaLists.Instance.TerrainTypeCounts is null in SelectNativeAnimalsForAllTerrains");
            return;
        }

        foreach (var terrainType in permaLists.TerrainTypeCounts.Keys)
        {
            if (!ExcludedTerrains.Contains(terrainType))
            {
                SelectNativeAnimalsForTerrain(terrainType);
            }
        }
    }

    private void SelectNativeAnimalsForTerrain(TerrainType terrainType)
    {
        try
        {
            var allAnimals = GetAnimalsForTerrain(terrainType);
            if (allAnimals.Count == 0)
            {
                GameDebugger.Instance.LogInfo($"No animals found for terrain type {terrainType}. Cannot select native animals.");
                return;
            }

            var nativeAnimalsBySize = allAnimals
                .Where(a => a.TerrainRarities.All(tr => tr.Terrain != terrainType || tr.Rarity != RarityType.Impossible))
                .GroupBy(a => a.Size)
                .ToDictionary(g => g.Key, g => ChooseAnimalBasedOnRarityAndTerrain(terrainType, g.ToList())?.AnimalName);

            AddNativeAnimalsToPermaLists(terrainType, nativeAnimalsBySize);
        }
        catch (Exception ex)
        {
            GameDebugger.Instance.LogError($"Exception in SelectNativeAnimalsForTerrain: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void GenerateAnimalsForAllTerrains()
    {
        try
        {
            var permaLists = PermaLists.Instance;
            if (permaLists?.TerrainTypeCounts == null)
            {
                GameDebugger.Instance.LogError("PermaLists.Instance or PermaLists.Instance.TerrainTypeCounts is null in GenerateAnimalsForAllTerrains");
                return;
            }

            int totalAnimalsAssigned = 0;

            foreach (var terrainType in permaLists.TerrainTypeCounts.Keys)
            {
                if (!ExcludedTerrains.Contains(terrainType))
                {
                    totalAnimalsAssigned += GenerateAnimalsForTerrain(terrainType);
                }
            }

            GameDebugger.Instance.LogInfo($"Total number of animals assigned: {totalAnimalsAssigned}");
        }
        catch (Exception ex)
        {
            GameDebugger.Instance.LogError($"Exception in GenerateAnimalsForAllTerrains: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private int GenerateAnimalsForTerrain(TerrainType terrainType)
    {
        try
        {
            var cellsOfTerrainType = MapGenerator.Instance.GetCellsByTerrain(terrainType);
            if (cellsOfTerrainType == null || cellsOfTerrainType.Count == 0)
            {
                GameDebugger.Instance.LogWarning($"No cells found for terrain type {terrainType}");
                return 0;
            }

            var animalNames = GenerateAnimalNamesForTerrain(terrainType);
            return DistributeAnimalsToCells(cellsOfTerrainType, animalNames);
        }
        catch (Exception ex)
        {
            GameDebugger.Instance.LogError($"Exception in GenerateAnimalsForTerrain: {ex.Message}\n{ex.StackTrace}");
            return 0;
        }
    }

    public List<WildAnimal> GenerateAnimal(string animalName, int count, Cell location)
    {
        List<WildAnimal> animals = new List<WildAnimal>();

        for (int i = 0; i < count; i++)
        {
            AnimalCreationData animalData = GetAnimalDataByName(animalName);
            if (animalData != null)
            {
                Animal animal = AnimalFactory.CreateAnimal(animalData);
                if (animal != null)
                {
                    WildAnimal wildAnimal = new WildAnimal(animal.AnimalID, animal, location.Terrain);
                    PermaLists.Instance.AllWildAnimals.Add(wildAnimal);
                    animals.Add(wildAnimal);

                    GameDebugger.Instance.LogInfo($"Generated {animalName} (ID: {animal.AnimalID}) at cell {location.CellID}");
                }
            }
            else
            {
                GameDebugger.Instance.LogWarning($"Failed to find data for animal: {animalName}");
            }
        }

        return animals;
    }

    private void RegisterLowerLevelAnimal(AnimalCreationData lowerLevelAnimalData, TerrainType terrain)
    {
        var animal = AnimalFactory.CreateAnimal(lowerLevelAnimalData);
        if (animal != null)
        {
            var wildAnimal = new WildAnimal(animal.AnimalID, animal, terrain);
            PermaLists.Instance.AllWildAnimals.Add(wildAnimal);
            Debug.Log($"Registered lower-level animal: {animal.Name} in terrain: {terrain}");
        }
    }

    private AnimalCreationData ChooseAnimalBasedOnRarityAndTerrain(TerrainType currentTerrain, List<AnimalCreationData> animals)
    {
        try
        {
            var animalWeights = animals
                .Select(animal => new
                {
                    Animal = animal,
                    Weight = GetWeightFromRarity(
                        animal.TerrainRarities.FirstOrDefault(tr => tr.Terrain == currentTerrain)?.Rarity ?? RarityType.Rare,
                        animal.AnimalName,
                        currentTerrain) + (int)(100 * currentRarityChance)
                })
                .Where(a => a.Weight > 0)
                .ToList();

            int totalWeight = animalWeights.Sum(a => a.Weight);
            if (totalWeight == 0)
            {
                GameDebugger.Instance.LogWarning($"No valid animals found for terrain {currentTerrain}.");
                return null;
            }

            int randomNumber = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            foreach (var animalWeight in animalWeights)
            {
                cumulative += animalWeight.Weight;
                if (randomNumber <= cumulative)
                {
                    return animalWeight.Animal;
                }
            }
        }
        catch (Exception ex)
        {
            GameDebugger.Instance.LogError($"Exception in ChooseAnimalBasedOnRarityAndTerrain: {ex.Message}\n{ex.StackTrace}");
        }

        return null;
    }

    private int GetWeightFromRarity(RarityType rarity, string animalName, TerrainType terrainType)
    {
        try
        {
            int baseWeight = rarity switch
            {
                RarityType.Common => 10,
                RarityType.Uncommon => 5,
                RarityType.Rare => 1,
                RarityType.Impossible => 0,
                _ => 1,
            };

            return PermaLists.Instance.NativeAnimalsPerTerrain.TryGetValue(terrainType, out var animals) && animals.Contains(animalName)
                ? baseWeight * 2
                : baseWeight;
        }
        catch (Exception ex)
        {
            GameDebugger.Instance.LogError($"Exception in GetWeightFromRarity: {ex.Message}\n{ex.StackTrace}");
            return 0;
        }
    }

    public AnimalCreationData GetAnimalDataByName(string animalName)
    {
        try
        {
            return PermaLists.Instance.AnimalCreationData.FirstOrDefault(a => a.AnimalName == animalName);
        }
        catch (Exception ex)
        {
            GameDebugger.Instance.LogError($"Exception in GetAnimalDataByName: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    private List<AnimalCreationData> GetAnimalsForTerrain(TerrainType terrainType)
    {
        try
        {
            return PermaLists.Instance.AnimalCreationData
                .Where(data => data?.TerrainRarities != null && data.TerrainRarities.All(tr => tr.Terrain != terrainType || tr.Rarity != RarityType.Impossible))
                .ToList();
        }
        catch (Exception ex)
        {
            GameDebugger.Instance.LogError($"Exception in GetAnimalsForTerrain: {ex.Message}\n{ex.StackTrace}");
            return new List<AnimalCreationData>();
        }
    }

    private void AddNativeAnimalsToPermaLists(TerrainType terrainType, Dictionary<AnimalSize, string> nativeAnimalsBySize)
    {
        if (!PermaLists.Instance.NativeAnimalsPerTerrain.ContainsKey(terrainType))
        {
            PermaLists.Instance.NativeAnimalsPerTerrain[terrainType] = new HashSet<string>();
        }

        foreach (var entry in nativeAnimalsBySize)
        {
            if (entry.Value != null)
            {
                PermaLists.Instance.NativeAnimalsPerTerrain[terrainType].Add(entry.Value);
                GameDebugger.Instance.LogInfo($"Selected native animal {entry.Value} for {terrainType} with size {entry.Key}");
            }
        }
    }

    private List<string> GenerateAnimalNamesForTerrain(TerrainType terrainType)
    {
        var animalNames = new List<string>();

        // Iterate over each size in AnimalDistributionRatios
        foreach (var sizeEntry in AnimalDistributionRatios)
        {
            // Get native animals for the current terrain and size
            var nativeAnimals = GetNativeAnimalsForTerrainBySize(terrainType, sizeEntry.Key);

            // Log the number of retrieved native animals for the current size and terrain type
            if (nativeAnimals.Count > 0)
            {
                GameDebugger.Instance.LogInfo($"Retrieved {nativeAnimals.Count} native animals for terrain {terrainType} of size {sizeEntry.Key}");
            }
            else
            {
                GameDebugger.Instance.LogWarning($"No native animals found for terrain type {terrainType} of size {sizeEntry.Key}");
            }

            // Skip to the next size if no animals were found for the current size
            if (nativeAnimals.Count == 0)
            {
                continue;
            }

            // Determine the number of animals to generate based on the distribution ratio
            int numberOfAnimals = Mathf.Max(1, Mathf.RoundToInt(nativeAnimals.Count * sizeEntry.Value));

            // Generate the animals for the current size
            for (int i = 0; i < numberOfAnimals; i++)
            {
                var selectedAnimalData = ChooseAnimalBasedOnRarityAndTerrain(terrainType, nativeAnimals);
                if (selectedAnimalData != null)
                {
                    animalNames.Add(selectedAnimalData.AnimalName);
                    GameDebugger.Instance.LogInfo($"Added {selectedAnimalData.AnimalName} for terrain {terrainType} of size {sizeEntry.Key}");
                }
                else
                {
                    GameDebugger.Instance.LogWarning($"Failed to select animal for terrain {terrainType} of size {sizeEntry.Key}");
                }
            }
        }

        // Final log to indicate the total number of animals generated for this terrain type
        GameDebugger.Instance.LogInfo($"Generated {animalNames.Count} animal names for terrain {terrainType}");

        return animalNames;
    }

    private List<AnimalCreationData> GetNativeAnimalsForTerrainBySize(TerrainType terrainType, AnimalSize size)
    {
        if (PermaLists.Instance.NativeAnimalsPerTerrain.ContainsKey(terrainType))
        {
            // Retrieve and filter animals by size
            var animals = PermaLists.Instance.NativeAnimalsPerTerrain[terrainType]
                .Select(animalName => GetAnimalDataByName(animalName))
                .Where(a => a != null && a.Size == size)
                .ToList();

            // Log the number of animals retrieved for debugging
            GameDebugger.Instance.LogInfo($"Found {animals.Count} animals of size {size} for terrain {terrainType}");
            return animals;
        }
        else
        {
            // Log if no animals exist for the terrain type
            GameDebugger.Instance.LogWarning($"No animals found for terrain type {terrainType} in NativeAnimalsPerTerrain");
            return new List<AnimalCreationData>();
        }
    }


    private int DistributeAnimalsToCells(List<Cell> cells, List<string> animalNames)
    {
        int totalAssigned = 0;
        var random = new System.Random();

        foreach (var cell in cells)
        {
            List<string> assignedAnimals = new List<string>();

            // Iterate through the animal names
            foreach (var animalName in animalNames)
            {
                var animalData = GetAnimalDataByName(animalName);

                if (animalData == null) continue;

                // Check if the animal is part of a herd or pack
                if (animalData.IsHerd || animalData.IsPack)
                {
                    // Randomly generate the range between 3 and 7
                    int groupSize = random.Next(3, 8);

                    // Add the animal to the list multiple times based on the group size
                    for (int i = 0; i < groupSize; i++)
                    {
                        assignedAnimals.Add(animalName);
                    }

                    GameDebugger.Instance.LogInfo($"Assigned a group of {groupSize} {animalName}(s) to cell {cell.CellID} because it is part of a {(animalData.IsHerd ? "herd" : "pack")}");
                }
                else
                {
                    // If not a herd or pack, just add it once
                    assignedAnimals.Add(animalName);
                }
            }

            // Shuffle the assigned animals for the cell
            assignedAnimals = assignedAnimals.OrderBy(x => random.Next()).ToList();

            if (!PermaLists.Instance.AnimalsToGenerate.ContainsKey(cell.CellID))
            {
                PermaLists.Instance.AnimalsToGenerate[cell.CellID] = new List<string>();
            }

            PermaLists.Instance.AnimalsToGenerate[cell.CellID].AddRange(assignedAnimals);

            totalAssigned += assignedAnimals.Count;
            GameDebugger.Instance.LogInfo($"Assigned {assignedAnimals.Count} animals to cell {cell.CellID}");
        }

        return totalAssigned;
    }


    // Method to generate and add materials based on native animals
    public void GenerateMaterialsFromNativeAnimals()
    {
        Debug.Log("GenerateMaterialsFromNativeAnimals Called");

        foreach (var terrainAnimals in PermaLists.Instance.NativeAnimalsPerTerrain)
        {
            foreach (var animal in terrainAnimals.Value)
            {
                // Create a new Material based on the animal name
                AnimalCreationData animalData = GetAnimalDataByName(animal);

                if (animalData != null)
                {
                    ObjectMaterial newMaterial = new ObjectMaterial
                    {
                        MaterialName = $"{animal} Hide",
                        Type = MaterialType.Leather, // Assuming leather type for animal materials
                        Rarity = DetermineMaterialRarity(animalData.Size)
                    };

                    // Add the new material to the materials list
                    PermaLists.Instance.ObjectMaterials.Add(newMaterial);
                }
            }
        }

        Debug.Log("Materials generated and added based on native animals.");
    }

    // Method to determine material rarity based on animal size
    private MaterialRarity DetermineMaterialRarity(AnimalSize size)
    {
        switch (size)
        {
            case AnimalSize.Medium:
                return MaterialRarity.Common;
            case AnimalSize.Tiny:
            case AnimalSize.Huge:
                return MaterialRarity.VeryRare;
            case AnimalSize.Small:
                return MaterialRarity.Rare;
            case AnimalSize.Large:
                return MaterialRarity.Uncommon;
            default:
                return MaterialRarity.Common;
        }
    }
}

public static class AnimalFactory
{
    private static readonly System.Random random = new System.Random();

    public static Animal CreateAnimal(AnimalCreationData data)
    {
        if (!ValidateAnimalCreationData(data)) return null;

        int animalID = GameManager.Instance.GetAnimalID();

        // Initialize the animal object
        Animal animal = InitializeAnimal(animalID, data);

        // Ensure BodyType is not null, default to "Quadruped"
        string bodyType = data.BodyType ?? "Quadruped";

        animal.Anatomy = AnatomyGenerator.Instance.GenerateAnatomy(bodyType);

        // Cap the Animal's Health
        animal.CapMaxHealthBasedOnSize();

        // Inject dependencies and set default states
        InjectDependencies(animal);

        // Register the animal in the game
        RegisterAnimal(animal);

        return animal;
    }


    private static bool ValidateAnimalCreationData(AnimalCreationData data)
    {
        if (data == null)
        {
            Debug.LogError("AnimalCreationData is null. Cannot create animal.");
            return false;
        }

        if (string.IsNullOrEmpty(data.AnimalName))
        {
            Debug.LogError("AnimalCreationData.AnimalName is null or empty. Cannot create animal.");
            return false;
        }

        return true;
    }

    private static Animal InitializeAnimal(int animalID, AnimalCreationData data)
    {
        return new Animal
        {
            AnimalID = animalID,
            IInteractableID = animalID,
            Name = data.AnimalName,
            Symbol = data.Symbol != default(char) ? data.Symbol : 'A',
            Color = !string.IsNullOrEmpty(data.Color) ? data.Color : "White",
            IsPredator = data.IsPredator,
            Diet = data.Diet,
            Size = data.Size,
            TerrainRarities = data.TerrainRarities ?? new List<TerrainRarity>(),
            CommonColours = data.CommonColours ?? new List<string>(),
            Health = data.Health > 0 ? data.Health : 100,
            MaxHealth = data.MaxHealth > 0 ? data.MaxHealth : 100,
            Strength = AdjustStat(data.Strength > 0 ? data.Strength : 8),
            Speed = AdjustStat(data.Speed > 0 ? data.Speed : 5),
            Awareness = AdjustStat(data.Awareness > 0 ? data.Awareness : 8),
            Charisma = AdjustStat(data.Charisma > 0 ? data.Charisma : 8),
            Dexterity = AdjustStat(data.Dexterity > 0 ? data.Dexterity : 8),
            Constitution = AdjustStat(data.Constitution > 0 ? data.Constitution : 8),
            Wisdom = AdjustStat(data.Wisdom > 0 ? data.Wisdom : 8),
            Luck = AdjustStat(data.Luck > 0 ? data.Luck : 8),
            Intelligence = AdjustStat(data.Intelligence > 0 ? data.Intelligence : 8),
            PreferredTerrains = data.PreferredTerrains ?? new List<TerrainType>(),
            CoverType = data.CoverType,
            IsHostile = data.IsHostile,
            IsPack = data.IsPack,
            IsHerd = data.IsHerd,
            IsDomestic = data.IsDomestic,
            IsMountable = data.IsMountable,
            IsTame = false,
            IsActive = true
        };
    }


    private static void InjectDependencies(Animal animal)
    {
        animal.DirectionFacing = Direction.North;
        animal.Position = Vector2Int.zero;
        // Add any additional dependencies here
    }

    private static void RegisterAnimal(Animal animal)
    {
        PermaLists.Instance.AllAnimals.Add(animal);
        PermaLists.Instance.AllCharacters.Add(animal);
        Debug.Log($"Created Animal - ID: {animal.AnimalID}, Level {animal.Level}, Name: {animal.Name}, Symbol: {animal.Symbol}, Color: {animal.Color}, IsActive: {animal.IsActive}");
    }

    private static int AdjustStat(int baseValue)
    {
        int adjustment = random.Next(-2, 3); // Generates a random number between -2 and 2
        return Mathf.Clamp(baseValue + adjustment, 0, 10); // Ensure value is within 0 to 10
    }

    private static int AdjustStatForLevel(int baseStat, int level)
    {
        // Use an inverse logic where higher levels have less reduction
        float reductionFactor = 1.0f - (1.0f / (level + 1)); // Level 1 => 50%, Level 2 => 66%, etc.
        return Mathf.Max(1, Mathf.RoundToInt(baseStat * reductionFactor)); // Ensure the stat is at least 1
    }

    private static int AdjustMaxHealthForLevel(int baseMaxHealth, int level)
    {
        // Use a formula where higher levels have less reduction
        float reductionFactor = 1.0f - (1.0f / (level + 1)); // Level 1 => 50%, Level 2 => 66%, Level 3 => 75%, etc.
        return Mathf.Max(1, Mathf.RoundToInt(baseMaxHealth * reductionFactor)); // Ensure health is at least 1
    }


    // Move the GenerateLowerLevelAnimal method inside the class
    public static AnimalCreationData GenerateLowerLevelAnimal(AnimalCreationData baseAnimalData, int level)
    {
        // Ensure that level is at least 1
        if (level <= 0)
        {
            level = 1;
        }

        // Create a new instance for the lower level animal based on the original data
        AnimalCreationData lowerLevelAnimal = new AnimalCreationData
        {
            AnimalName = baseAnimalData.AnimalName,
            Symbol = baseAnimalData.Symbol,
            Color = baseAnimalData.Color,
            IsPredator = baseAnimalData.IsPredator,
            Diet = baseAnimalData.Diet,
            Size = baseAnimalData.Size,
            TerrainRarities = baseAnimalData.TerrainRarities,
            CommonColours = baseAnimalData.CommonColours,
            PreferredTerrains = baseAnimalData.PreferredTerrains,
            CoverType = baseAnimalData.CoverType,
            IsHostile = baseAnimalData.IsHostile,
            IsPack = baseAnimalData.IsPack,
            IsHerd = baseAnimalData.IsHerd,
            IsDomestic = baseAnimalData.IsDomestic,
            IsMountable = baseAnimalData.IsMountable,
            // Reduce stats by a level factor (halving for simplicity)
            Health = AdjustStatForLevel(baseAnimalData.Health, level),
            MaxHealth = AdjustStatForLevel(baseAnimalData.MaxHealth, level),
            Strength = AdjustStatForLevel(baseAnimalData.Strength, level),
            Speed = AdjustStatForLevel(baseAnimalData.Speed, level),
            Awareness = AdjustStatForLevel(baseAnimalData.Awareness, level),
            Charisma = AdjustStatForLevel(baseAnimalData.Charisma, level),
            Dexterity = AdjustStatForLevel(baseAnimalData.Dexterity, level),
            Constitution = AdjustStatForLevel(baseAnimalData.Constitution, level),
            Wisdom = AdjustStatForLevel(baseAnimalData.Wisdom, level),
            Luck = AdjustStatForLevel(baseAnimalData.Luck, level),
            Intelligence = AdjustStatForLevel(baseAnimalData.Intelligence, level)
        };

        return lowerLevelAnimal;
    }
}

public class WildAnimal
{
    public int WildAnimalID { get; set; }
    public Animal Animal { get; set; }
    public TerrainType Terrain { get; set; }

    public WildAnimal(int id, Animal animal, TerrainType terrain)
    {
        WildAnimalID = id;
        Animal = animal;
        Terrain = terrain;
    }
}
