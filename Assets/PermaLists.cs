using System.Collections.Generic;
using UnityEngine;

public class PermaLists : MonoBehaviour
{
    public static PermaLists Instance { get; private set; }

    #region Map Generation
    public List<INestedArea> AllNestedAreas = new List<INestedArea>();
    public List<Cell> AllMapCells = new List<Cell>();
    public Dictionary<int, RegionInfo> RegionInfoDictionary { get; set; }
    #endregion

    #region Races
    public List<Race> MainInGameRaces = new List<Race>();
    public List<Race> SelectedRaces = new List<Race>();
    public List<Race> Races = new List<Race>();
    public Dictionary<string, RaceData> RaceDataDict { get; set; } = new Dictionary<string, RaceData>();
    #endregion

    #region Character Management
    public List<Character> AllCharacters = new List<Character>();
    public List<NPC> AllNPCs = new List<NPC>();
    public List<PlayerCharacter> PlayerCharacters;
    public List<NPCRoleData> RoleData = new List<NPCRoleData>();
    public List<Personality> Personalities { get; set; } = new List<Personality>();
    #endregion

    #region Village Management
    public List<Village> AllVillages = new List<Village>();
    public List<VillageCreationData> VillageCreationData = new List<VillageCreationData>();
    #endregion

    #region Animal Management
    public List<Animal> AllAnimals = new List<Animal>();
    public List<Animal> WildLandAnimals = new List<Animal>();
    public List<Animal> WildForestAnimals = new List<Animal>();
    public List<WildAnimal> AllWildAnimals = new List<WildAnimal>();
    public List<AnimalCreationData> AnimalCreationData = new List<AnimalCreationData>();

    // Dictionary to hold native animals per terrain type
    public Dictionary<TerrainType, HashSet<string>> NativeAnimalsPerTerrain { get; set; } = new Dictionary<TerrainType, HashSet<string>>();
    public Dictionary<int, List<string>> AnimalsToGenerate { get; set; } = new Dictionary<int, List<string>>();
    #endregion

    #region Monster Management
    public List<Monster> AllMonsters = new List<Monster>(); // Stores all monsters in the game
    public List<Monster> ActiveMonsters = new List<Monster>(); // Monsters currently in the world
    public List<MonsterCreationData> MonsterCreationData = new List<MonsterCreationData>(); // Stores creation data

    // Dictionary to hold native monsters per terrain type
    public Dictionary<TerrainType, HashSet<string>> NativeMonstersPerTerrain { get; set; } = new Dictionary<TerrainType, HashSet<string>>();

    // Dictionary to track which monsters should spawn in a region
    public Dictionary<int, List<string>> MonstersToGenerate { get; set; } = new Dictionary<int, List<string>>();
    #endregion

    #region Items, Loot and Objects
    public List<Item> ItemList = new List<Item>();
    public ItemNamingData ItemNamingData { get; set; }
    public List<IInteractable> AllObjects = new List<IInteractable>();
    public List<LootCreationData> LootCreationData = new List<LootCreationData>();
    public List<EntityLootData> EntityLootData = new List<EntityLootData>();
    public List<ItemCreationData> ItemCreationData = new List<ItemCreationData>();
    public List<SmithingRecipe> SmithingRecipeList = new List<SmithingRecipe>();
    public List<ObjectMaterial> ObjectMaterials = new List<ObjectMaterial>();
    #endregion

    #region Buffs, Debuffs, Effects
    public List<OnHitEffect> OnHitEffects = new List<OnHitEffect>();
    public List<OnHitEffect> OnHitTakenEffects = new List<OnHitEffect>();
    #endregion

    #region Event and Landmark Management
    public List<EventCreationData> EventCreationData { get; set; } = new List<EventCreationData>();
    public List<LandmarkCreationData> LandmarkCreationData { get; set; } = new List<LandmarkCreationData>();
    #endregion

    #region Dungeon, Cave & Camp Management
    public List<DungeonCreationData> DungeonCreationDataList = new List<DungeonCreationData>();
    public List<DungeonNestedArea> Dungeons = new List<DungeonNestedArea>();

    public List<CaveCreationData> CaveCreationDataList = new List<CaveCreationData>();
    public List<CaveNestedArea> Caves = new List<CaveNestedArea>();

    public List<CampCreationData> CampCreationDataList = new List<CampCreationData>();
    public List<Camp> Camps = new List<Camp>();


    #endregion

    #region Crafting and Recipes
    public List<CraftingRecipe> CraftingRecipeList { get; set; }
    public List<CookingRecipe> CookingRecipeList { get; set; }
    #endregion

    #region Name Lists
    public List<string> HumanFirstNames = new List<string>();
    public List<string> HumanSurnames = new List<string>();
    public List<string> DwarfFirstNames = new List<string>();
    public List<string> DwarfSurnames = new List<string>();
    public List<string> ElfFirstNames = new List<string>();
    public List<string> ElfSurnames = new List<string>();
    public List<string> SabrenFirstNames = new List<string>();
    public List<string> SabrenSurnames = new List<string>();
    public List<string> SaurosinFirstNames = new List<string>();
    public List<string> SaurosinSurnames = new List<string>();
    public List<string> CaraphraxFirstNames = new List<string>();
    public List<string> CaraphraxSurnames = new List<string>();
    #endregion

    #region CharacterFeatures

    public List<AnatomyData> AnatomyData; // Stores all anatomy structures
    public Dictionary<string, BodyPartData> BodyPartData = new Dictionary<string, BodyPartData>(); // Stores all body parts by name

    #endregion

    #region Backgrounds and Loadouts
    public List<Background> Backgrounds = new List<Background>();
    public List<Loadout> Loadouts = new List<Loadout>();
    #endregion

    #region Dialogue Management
    public List<DialogueScript> DialogueScripts { get; set; } = new List<DialogueScript>();
    #endregion

    #region Factions
    public List<Faction> Factions;
    #endregion

    #region Terrain Management
    // Terrain Type Counts
    public Dictionary<TerrainType, int> TerrainTypeCounts { get; private set; } = new Dictionary<TerrainType, int>();

    // Native vegetation per region
    public Dictionary<CompassDirection, string> NativeTreeFruitsPerRegion { get; set; } = new Dictionary<CompassDirection, string>();
    public Dictionary<CompassDirection, string> NativeBushFruitsPerRegion { get; set; } = new Dictionary<CompassDirection, string>();
    public Dictionary<CompassDirection, string> NativeVineFruitsPerRegion { get; set; } = new Dictionary<CompassDirection, string>();
    public Dictionary<CompassDirection, string> NativeVegetablesPerRegion { get; set; } = new Dictionary<CompassDirection, string>();
    public Dictionary<CompassDirection, string> NativeFungiPerRegion = new Dictionary<CompassDirection, string>();

    #endregion

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Optionally keep it persistent across scenes
            InitializeLists();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeLists()
    {
        // Initialize all lists if they aren't already initialized
        AnimalsToGenerate ??= new Dictionary<int, List<string>>();
        AllWildAnimals ??= new List<WildAnimal>();
        NativeAnimalsPerTerrain ??= new Dictionary<TerrainType, HashSet<string>>();
        AnimalCreationData ??= new List<AnimalCreationData>();
        TerrainTypeCounts ??= new Dictionary<TerrainType, int>();
        NativeTreeFruitsPerRegion ??= new Dictionary<CompassDirection, string>();
        NativeVineFruitsPerRegion ??= new Dictionary<CompassDirection, string>();
        NativeBushFruitsPerRegion ??= new Dictionary<CompassDirection, string>();
        NativeVegetablesPerRegion ??= new Dictionary<CompassDirection, string>();

        EventCreationData ??= new List<EventCreationData>();
        LandmarkCreationData ??= new List<LandmarkCreationData>();
        AllNPCs ??= new List<NPC>();
        AllVillages ??= new List<Village>();
        AllAnimals ??= new List<Animal>();
        AllCharacters ??= new List<Character>();
        WildLandAnimals ??= new List<Animal>();
        WildForestAnimals ??= new List<Animal>();
        ItemList ??= new List<Item>();
        HumanFirstNames ??= new List<string>();
        HumanSurnames ??= new List<string>();
        DwarfFirstNames ??= new List<string>();
        DwarfSurnames ??= new List<string>();
        ElfFirstNames ??= new List<string>();
        ElfSurnames ??= new List<string>();
        SabrenFirstNames ??= new List<string>();
        SabrenSurnames ??= new List<string>();
        SaurosinFirstNames ??= new List<string>();
        SaurosinSurnames ??= new List<string>();
        CaraphraxFirstNames ??= new List<string>();
        CaraphraxSurnames ??= new List<string>();
        Races ??= new List<Race>();
        Backgrounds ??= new List<Background>();
        Loadouts ??= new List<Loadout>();
        RoleData ??= new List<NPCRoleData>();
        VillageCreationData ??= new List<VillageCreationData>();
        LootCreationData ??= new List<LootCreationData>();
        ItemCreationData ??= new List<ItemCreationData>();
        ObjectMaterials ??= new List<ObjectMaterial>();
        SmithingRecipeList ??= new List<SmithingRecipe>();
        DialogueScripts ??= new List<DialogueScript>();
        Personalities ??= new List<Personality>();
        PlayerCharacters ??= new List<PlayerCharacter>();
        RegionInfoDictionary ??= new Dictionary<int, RegionInfo>();
        MainInGameRaces ??= new List<Race>();
        SelectedRaces ??= new List<Race>();
        Factions ??= new List<Faction>();
        CraftingRecipeList ??= new List<CraftingRecipe>();
        AllMapCells ??= new List<Cell>();
        AllNestedAreas ??= new List<INestedArea>();
        RaceDataDict ??= new Dictionary<string, RaceData>();
    }

    // Method to count terrain types and store them in a dictionary
    public void CountTerrainTypes()
    {
        TerrainTypeCounts.Clear();

        foreach (Cell cell in AllMapCells)
        {
            TerrainType terrainType = cell.Terrain;

            if (TerrainTypeCounts.ContainsKey(terrainType))
            {
                TerrainTypeCounts[terrainType]++;
            }
            else
            {
                TerrainTypeCounts.Add(terrainType, 1);
            }
        }

        // Construct a string with the names of all terrain types
        List<string> terrainNames = new List<string>();
        foreach (var terrainType in TerrainTypeCounts.Keys)
        {
            terrainNames.Add(terrainType.ToString());
        }
        string terrainNamesStr = string.Join(", ", terrainNames);

        Debug.Log("CountTerrainTypes has been run. Count of Unique TerrainTypes is " + TerrainTypeCounts.Count + ". TerrainTypes: " + terrainNamesStr);
    }
}
