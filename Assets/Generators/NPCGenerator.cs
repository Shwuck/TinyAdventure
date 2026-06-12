using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class NPCGenerator : MonoBehaviour
{
    public static NPCGenerator Instance { get; private set; }

    private const int DominantRaceWeight = 10;
    private const int CommonRaceWeight = 5;
    private const int UncommonRaceWeight = 2;
    private const int RareRaceWeight = 1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public List<NPC> GenerateNPCs(int numberOfNPCs, Village village, NPCRole role = NPCRole.Villager)
    {
        if (village == null)
        {
            Debug.LogError("Village is null when generating NPCs.");
            return null;
        }

        List<NPC> generatedNPCs = new List<NPC>();
        var villageData = CivilisationManager.Instance.GetVillageCreationData(village.VillageType);

        for (int i = 0; i < numberOfNPCs; i++)
        {
            NPC newNPC = GenerateNPC(village, villageData, role);
            if (newNPC != null)
            {
                generatedNPCs.Add(newNPC);
                village.AddNPC(newNPC);
            }
        }
        return generatedNPCs;
    }

    public NPC GenerateNPCWithRole(Village village, NPCRole role)
    {
        if (village == null)
        {
            Debug.LogError("Village is null when generating NPC with role.");
            return null;
        }

        var villageData = CivilisationManager.Instance.GetVillageCreationData(village.VillageType);
        NPC newNPC = GenerateNPC(village, villageData, role);
        if (newNPC != null)
        {
            village.AddNPC(newNPC);
        }
        return newNPC;
    }

    public NPC GenerateNPC(Village village, VillageCreationData villageData, NPCRole role)
    {
        if (village == null)
        {
            Debug.LogError("Village is null when generating NPC.");
            return null;
        }

        int npcCounter = GameManager.Instance.GetNextNPCCounter();
        UnityEngine.Random.InitState(GameManager.Instance.GameSeed + npcCounter);

        NPC npc = new NPC();
        npc.NPCID = GetNPCID();
        npc.IInteractableID = npc.NPCID;
        npc.Race = GetRandomRace(villageData);
        npc.SubRace = GetRandomSubRace(npc.Race);
        (npc.FirstName, npc.Surname) = GetRandomName(npc.Race);
        npc.Age = GetRandomAge();
        npc.Role = role;
        npc.Personality = GetRandomPersonality(role);
        npc.IsAlive = true;
        npc.IsActive = true;
        npc.HasMetPlayer = false;
        npc.Home = village.VillageName;
        npc.Faction = village.VillageName;
        npc.HomeVillage = village;

        string bodyType = npc.SubRace?.BodyType ?? npc.Race.BodyType;
        npc.Anatomy = AnatomyGenerator.Instance.GenerateAnatomy(bodyType);
        // 1 in 25 chance to modify anatomy
        if (UnityEngine.Random.Range(1, 7) == 1)
        {
            AdjustAnatomy(npc);
        }


        AssignStance(npc);
        SetInitialStateForNPC(npc);

        InitializeNPCStats(npc);
        AssignRandomBirthday(npc);
        AssignRoleSpecificProperties(npc, village);
        ApplyStatModifiers(npc);

        // Perform Integrity Check
        if (!ValidateNPCIntegrity(npc))
        {
            GameDebugger.Instance.LogError($"NPC {npc.NPCID}: {npc.Name} failed integrity check and will not be added.");
            return null;
        }

        Debug.Log($"Generated Character {npc.NPCID}: {npc.Name}, with anatomy type {(npc.Anatomy != null ? npc.Anatomy.BodyType : "NULL")} - Anatomy is {(npc.Anatomy != null && npc.Anatomy.BodyParts.Count > 0 ? "CORRECT" : "INVALID")}");

        PermaLists.Instance.AllNPCs.Add(npc);
        PermaLists.Instance.AllCharacters.Add(npc);
        return npc;
    }

    public NPC GenerateStandaloneNPC(NPCRole role = NPCRole.Villager)
    {
        int npcCounter = GameManager.Instance.GetNextNPCCounter();
        UnityEngine.Random.InitState(GameManager.Instance.GameSeed + npcCounter);

        NPC npc = new NPC();
        npc.NPCID = GetNPCID();
        npc.IInteractableID = npc.NPCID;
        npc.Race = GetRandomRace();
        npc.SubRace = GetRandomSubRace(npc.Race);
        (npc.FirstName, npc.Surname) = GetRandomName(npc.Race);
        npc.Age = GetRandomAge();
        npc.Role = role;
        npc.Personality = GetRandomPersonality(role);
        npc.IsAlive = true;
        npc.IsActive = true;
        npc.HasMetPlayer = false;
        npc.Home = "Wilderness";

        string bodyType = npc.SubRace?.BodyType ?? npc.Race.BodyType;
        npc.Anatomy = AnatomyGenerator.Instance.GenerateAnatomy(bodyType);
        // 1 in 25 chance to modify anatomy
        if (UnityEngine.Random.Range(1, 3) == 1)
        {
            AdjustAnatomy(npc);
        }


        AssignStance(npc);
        SetInitialStateForNPC(npc);

        InitializeNPCStats(npc); 
        AssignRandomBirthday(npc);
        AssignRoleSpecificProperties(npc);
        ApplyStatModifiers(npc);

        // Perform Integrity Check
        if (!ValidateNPCIntegrity(npc))
        {
            GameDebugger.Instance.LogError($"NPC {npc.NPCID}: {npc.Name} failed integrity check and will not be added.");
            return null;
        }

        Debug.Log($"Generated Character {npc.NPCID}: {npc.Name}, for {npc.Home}, with role {npc.Role}. Their Strength is {npc.Strength} and their Dexterity is {npc.Dexterity}");

        PermaLists.Instance.AllNPCs.Add(npc);
        PermaLists.Instance.AllCharacters.Add(npc);
        return npc;
    }

    public List<NPC> GenerateCampNPCs(int numberOfNPCs, Cell campCell, NPCRole role)
    {
        if (campCell == null || !campCell.HasCamp || campCell.Camp == null)
        {
            Debug.LogError("GenerateCampNPCs: Invalid camp cell.");
            return null;
        }

        Camp campData = campCell.Camp; // Access the camp data from the cell
        List<NPC> generatedNPCs = new List<NPC>();

        for (int i = 0; i < numberOfNPCs; i++)
        {
            // Use the GenerateCampNPC method for each NPC generation to ensure complete initialization
            NPC newNPC = GenerateCampNPC(campData.CampID, role);

            // Add the generated NPC to the list of NPCs for the camp
            generatedNPCs.Add(newNPC);
        }

        Debug.Log($"Generated {generatedNPCs.Count} NPCs for camp {campData.CampID}.");
        return generatedNPCs;
    }



    // Method to generate a single NPC for a camp
    public NPC GenerateCampNPC(int campID, NPCRole role)
    {
        int npcCounter = GameManager.Instance.GetNextNPCCounter();
        UnityEngine.Random.InitState(GameManager.Instance.GameSeed + npcCounter);

        int newID = GetNPCID();

        NPC npc = new NPC
        {
            NPCID = newID,
            IInteractableID = newID,
            Role = role,
            IsCamped = true,      // Mark as camped
            CampID = campID,      // Assign camp ID
            IsAlive = true,
            IsActive = true,
            HasMetPlayer = false,
            Home = $"Camp_{campID}"  // Set home to camp ID for tracking
        };

        // Optionally, generate race, name, and other attributes
        npc.Race = GetRandomRace();  // Adjust to camp-specific races if needed
        npc.SubRace = GetRandomSubRace(npc.Race);
        (npc.FirstName, npc.Surname) = GetRandomName(npc.Race);
        npc.Age = GetRandomAge();
        npc.Personality = GetRandomPersonality(role);

        string bodyType = npc.SubRace?.BodyType ?? npc.Race.BodyType;
        npc.Anatomy = AnatomyGenerator.Instance.GenerateAnatomy(bodyType);
        // 1 in 25 chance to modify anatomy
        if (UnityEngine.Random.Range(1, 26) == 1)
        {
            AdjustAnatomy(npc);
        }


        AssignStance(npc);
        SetInitialStateForNPC(npc);

        // Initialize stats and role-specific properties
        InitializeNPCStats(npc);
        AssignRandomBirthday(npc);
        AssignRoleSpecificProperties(npc);
        ApplyStatModifiers(npc);

        // Perform Integrity Check
        if (!ValidateNPCIntegrity(npc))
        {
            GameDebugger.Instance.LogError($"NPC {npc.NPCID}: {npc.Name} failed integrity check and will not be added.");
            return null;
        }

        Debug.Log($"Generated Camp NPC {npc.NPCID}: {npc.Name}, for CampID {campID}, with role {npc.Role}");

        // Add the NPC to persistent lists for tracking
        PermaLists.Instance.AllNPCs.Add(npc);
        PermaLists.Instance.AllCharacters.Add(npc);

        return npc;
    }


    private int GetNPCID()
    {
        return GameManager.Instance.GetNPCID();
    }

    private (string, string) GetRandomName(Race race)
    {
        List<string> firstNames;
        List<string> surnames;

        switch (race.Name)
        {
            case "Human":
                firstNames = PermaLists.Instance.HumanFirstNames;
                surnames = PermaLists.Instance.HumanSurnames;
                break;
            case "Dwarf":
                firstNames = PermaLists.Instance.DwarfFirstNames;
                surnames = PermaLists.Instance.DwarfSurnames;
                break;
            case "Elf":
                firstNames = PermaLists.Instance.ElfFirstNames;
                surnames = PermaLists.Instance.ElfSurnames;
                break;
            case "Sabren":
                firstNames = PermaLists.Instance.SabrenFirstNames;
                surnames = PermaLists.Instance.SabrenSurnames;
                break;
            case "Saurosin":
                firstNames = PermaLists.Instance.SaurosinFirstNames;
                surnames = PermaLists.Instance.SaurosinSurnames;
                break;
            case "Caraphrax":
                firstNames = PermaLists.Instance.CaraphraxFirstNames;
                surnames = PermaLists.Instance.CaraphraxSurnames;
                break;
            default:
                firstNames = new List<string> { "John" };
                surnames = new List<string> { "Doe" };
                break;
        }

        if (firstNames.Count == 0 || surnames.Count == 0)
        {
            Debug.LogWarning("Name lists are empty or not initialized properly. Using fallback names.");
            return ("John", "Doe");
        }

        string firstName = firstNames[UnityEngine.Random.Range(0, firstNames.Count)];
        string surname = surnames[UnityEngine.Random.Range(0, surnames.Count)];

        return (firstName, surname);
    }

    private int GetRandomAge()
    {
        return UnityEngine.Random.Range(18, 60);
    }

    private void InitializeNPCStats(NPC npc)
    {
        Debug.Log($"Checking Racial Stats to add for {npc.NPCID}: {npc.Name}");

        if (npc.SubRace != null)
        {
            // Apply subrace stats
            Debug.Log($"Applying SubRace Stats for {npc.SubRace.Name}");
            npc.MaxHealth = npc.SubRace.BaseHealth;
            npc.Strength = npc.SubRace.BaseStrength;
            npc.Dexterity = npc.SubRace.BaseDexterity;
            npc.Constitution = npc.SubRace.BaseConstitution;
            npc.Wisdom = npc.SubRace.BaseWisdom;
            npc.Intelligence = npc.SubRace.BaseIntelligence;
            npc.Charisma = npc.SubRace.BaseCharisma;
            npc.Luck = npc.SubRace.BaseLuck;
        }
        else
        {
            // Apply base race stats
            var raceStats = PermaLists.Instance.Races.FirstOrDefault(r => r.Name == npc.Race.Name);
            if (raceStats != null)
            {
                Debug.Log($"Race Stats for {npc.Race.Name}: Health: {raceStats.BaseHealth}, Strength: {raceStats.BaseStrength}, Dexterity: {raceStats.BaseDexterity}, Constitution: {raceStats.BaseConstitution}, Wisdom: {raceStats.BaseWisdom}, Intelligence: {raceStats.BaseIntelligence}, Charisma: {raceStats.BaseCharisma}, Luck: {raceStats.BaseLuck}");
                npc.MaxHealth = raceStats.BaseHealth;
                npc.Strength = raceStats.BaseStrength;
                npc.Dexterity = raceStats.BaseDexterity;
                npc.Constitution = raceStats.BaseConstitution;
                npc.Wisdom = raceStats.BaseWisdom;
                npc.Intelligence = raceStats.BaseIntelligence;
                npc.Charisma = raceStats.BaseCharisma;
                npc.Luck = raceStats.BaseLuck;
            }
            else
            {
                Debug.LogError($"Race stats for '{npc.Race.Name}' not found.");
            }
        }

        if (npc.MaxHealth <= 1)
        {
            npc.MaxHealth = 10;
        }

        npc.Health = npc.MaxHealth;

        Debug.Log($"NPC {npc.Name}'s stats are now: Health: {npc.Health}, Strength: {npc.Strength}, Dexterity: {npc.Dexterity}, Constitution: {npc.Constitution}, Wisdom: {npc.Wisdom}, Intelligence: {npc.Intelligence}, Charisma: {npc.Charisma}, Luck: {npc.Luck}");
    }

    private void AssignRandomBirthday(NPC npc)
    {
        npc.BirthdayDay = UnityEngine.Random.Range(1, 29);
        npc.BirthdaySeason = (Season)UnityEngine.Random.Range(0, 4);
        npc.BirthdayYear = GetCurrentYear() - npc.Age;
    }

    private int GetCurrentYear()
    {
        return TimeManager.Instance.GetCurrentYear();
    }

    private void AssignRoleSpecificProperties(NPC npc, Village village = null)
    {
        AssignRoleSpecificTitle(npc);

        var roleData = PermaLists.Instance.RoleData
            .FirstOrDefault(role => role.Role == npc.Role);

        if (roleData != null)
        {
            npc.NewsType = roleData.NewsType;
            npc.IsCraftsman = roleData.IsCraftsman;

            var applicableLoadouts = PermaLists.Instance.Loadouts
                .Where(loadout => loadout.ApplicableNPCs.Contains(npc.Role.ToString()))
                .ToList();

            if (applicableLoadouts.Count == 0)
            {
                Debug.LogError($"No applicable loadouts found for role {npc.Role}");
                return;
            }

            var selectedLoadout = applicableLoadouts[UnityEngine.Random.Range(0, applicableLoadouts.Count)];
            npc.Loadout = selectedLoadout;
            npc.Money = selectedLoadout.Money;
            npc.BaseMoney = selectedLoadout.Money;

            int equippableItemCount = selectedLoadout.Equipment.Count;
            Debug.Log($"{npc.Role} has {equippableItemCount} equippable items, starting to generate now.");

            foreach (var equipmentSlot in selectedLoadout.Equipment.Keys)
            {
                Item item = ItemGenerator.Instance.GenerateItem(selectedLoadout.Equipment[equipmentSlot]);
                if (item != null)
                {
                    npc.Inventory.AddItem(item);

                    npc.EquipItem(item, equipmentSlot);
                    item.IsEquipped = true;
                    Debug.Log($"NPC {npc.Name} equipped {item.Name} in slot {equipmentSlot}.");
                }
                else
                {
                    Debug.LogError($"Failed to generate item for slot {equipmentSlot}: {selectedLoadout.Equipment[equipmentSlot]}");
                }
            }

            Debug.Log($"NPC {npc.Name} now has {npc.EquippedItems.Count} items equipped.");

            int totalInventoryItems = selectedLoadout.Inventory.Sum(item => item.Value);
            Debug.Log($"{npc.Role} has {totalInventoryItems} inventory items, starting to generate now.");

            foreach (var inventoryItem in selectedLoadout.Inventory)
            {
                string itemName = inventoryItem.Key;
                int quantity = inventoryItem.Value;

                for (int i = 0; i < quantity; i++)
                {
                    Item item = ItemGenerator.Instance.GenerateItem(itemName);
                    if (item != null)
                    {
                        npc.Inventory.AddItem(item);
                        Debug.Log($"NPC {npc.Name} received {item.Name} as inventory item.");
                    }
                    else
                    {
                        Debug.LogError($"Failed to generate item for inventory: {itemName}");
                    }
                }
            }

            if (selectedLoadout.InventoryByType != null)
            {
                foreach (var inventoryItemByType in selectedLoadout.InventoryByType)
                {
                    ItemType itemType = inventoryItemByType.Key;
                    int quantity = inventoryItemByType.Value;

                    for (int i = 0; i < quantity; i++)
                    {
                        Item item = ItemGenerator.Instance.GenerateRandomItem(itemType);
                        if (item != null)
                        {
                            npc.Inventory.AddItem(item);
                            Debug.Log($"NPC {npc.Name} received {item.Name} of type {itemType} as inventory item.");
                        }
                        else
                        {
                            Debug.LogError($"Failed to generate random item for type: {itemType}");
                        }
                    }
                }
            }

            int containerCount = npc.Inventory.GetInventoryContainers().Count;
            int totalItemCount = npc.Inventory.GetInventoryContainers().Sum(container => container.Amount);
            Debug.Log($"NPC {npc.Name} now has {containerCount} inventory containers with {totalItemCount} total items.");
        }
        else
        {
            Debug.LogError($"No role data found for role {npc.Role}");
        }
    }


    private void AssignRoleSpecificTitle(NPC npc)
    {
        if (UnityEngine.Random.value > 0.2f)
        {
            return;
        }

        var roleData = PermaLists.Instance.RoleData
            .FirstOrDefault(role => role.Role == npc.Role);

        if (roleData == null)
        {
            Debug.LogError($"No role data found for role {npc.Role}");
            return;
        }

        if (roleData.Titles != null && roleData.Titles.Count > 0)
        {
            var selectedTitle = roleData.Titles[UnityEngine.Random.Range(0, roleData.Titles.Count)];
            npc.Title = selectedTitle;
        }
        else
        {
            Debug.Log($"No titles available for role {npc.Role}");
        }
    }

    private void ApplyStatModifiers(NPC npc)
    {
        Debug.Log($"Checking Stat Modifiers for {npc.NPCID}: {npc.Name}");

        var roleData = PermaLists.Instance.RoleData.FirstOrDefault(r => r.Role == npc.Role);
        if (roleData != null)
        {
            foreach (var modifier in roleData.StatModifiers)
            {
                switch (modifier.Key)
                {
                    case "Health":
                        npc.Health += modifier.Value;
                        break;
                    case "Strength":
                        npc.Strength += modifier.Value;
                        break;
                    case "Dexterity":
                        npc.Dexterity += modifier.Value;
                        break;
                    case "Constitution":
                        npc.Constitution += modifier.Value;
                        break;
                    case "Wisdom":
                        npc.Wisdom += modifier.Value;
                        break;
                    case "Intelligence":
                        npc.Intelligence += modifier.Value;
                        break;
                    case "Charisma":
                        npc.Charisma += modifier.Value;
                        break;
                    case "Luck":
                        npc.Luck += modifier.Value;
                        break;
                    default:
                        Debug.LogError($"Unhandled stat type: {modifier.Key}");
                        break;
                }
            }
        }

        Debug.Log($"NPC {npc.Name}'s stats are now: Health: {npc.Health}, Strength: {npc.Strength}, Dexterity: {npc.Dexterity}, Constitution: {npc.Constitution}, Wisdom: {npc.Wisdom}, Intelligence: {npc.Intelligence}, Charisma: {npc.Charisma}, Luck: {npc.Luck}");
    }


    private Loadout GetLoadoutForRole(NPCRole role)
    {
        var roleData = PermaLists.Instance.RoleData.FirstOrDefault(r => r.Role == role);
        if (roleData != null && roleData.LoadoutNames != null && roleData.LoadoutNames.Count > 0)
        {
            string loadoutName = roleData.LoadoutNames[UnityEngine.Random.Range(0, roleData.LoadoutNames.Count)];
            return PermaLists.Instance.Loadouts.FirstOrDefault(l => l.LoadoutName == loadoutName);
        }
        return null;
    }

    private Race GetRandomRace(VillageCreationData villageData = null)
    {
        List<Race> weightedRaces = new List<Race>();
        Race selectedRace = null;

        if (villageData != null)
        {
            bool isDominantRace = UnityEngine.Random.value < 0.65f;

            if (isDominantRace)
            {
                selectedRace = LookupRace(villageData.DominantRace);
            }
            else
            {
                foreach (string commonRaceName in villageData.CommonRaces)
                {
                    Race commonRace = LookupRace(commonRaceName);
                    for (int i = 0; i < CommonRaceWeight; i++)
                    {
                        weightedRaces.Add(commonRace);
                    }
                }

                foreach (string uncommonRaceName in villageData.UncommonRaces)
                {
                    Race uncommonRace = LookupRace(uncommonRaceName);
                    for (int i = 0; i < UncommonRaceWeight; i++)
                    {
                        weightedRaces.Add(uncommonRace);
                    }
                }

                foreach (string rareRaceName in villageData.RareRaces)
                {
                    Race rareRace = LookupRace(rareRaceName);
                    for (int i = 0; i < RareRaceWeight; i++)
                    {
                        weightedRaces.Add(rareRace);
                    }
                }

                int weightedRaceIndex = UnityEngine.Random.Range(0, weightedRaces.Count);
                selectedRace = weightedRaces[weightedRaceIndex];
            }
        }
        else
        {
            foreach (Race race in PermaLists.Instance.Races)
            {
                int raceWeight = 6 - race.Rarity;
                for (int i = 0; i < raceWeight; i++)
                {
                    weightedRaces.Add(race);
                }
            }

            int weightedRaceIndex = UnityEngine.Random.Range(0, weightedRaces.Count);
            selectedRace = weightedRaces[weightedRaceIndex];
        }

        return selectedRace;
    }

    private SubRace GetRandomSubRace(Race race)
    {
        if (race == null)
        {
            Debug.LogError("Race is null when trying to assign a subrace.");
            return null;
        }

        if (race.HasSubRace && race.SubRaces != null && race.SubRaces.Count > 0)
        {
            // Select a random subrace from the list of available subraces
            int subRaceIndex = UnityEngine.Random.Range(0, race.SubRaces.Count);
            SubRace subRace = race.SubRaces[subRaceIndex];
            Debug.Log($"SubRace selected: {subRace.Name} for Race: {race.Name}");
            return subRace;
        }
        else if (race.HasSubRace && (race.SubRaces == null || race.SubRaces.Count == 0))
        {
            // Handle the case where the race claims to have subraces, but none are listed
            Debug.LogError($"Race '{race.Name}' has 'HasSubRace' set to true, but no subraces are defined.");
            return null;
        }
        else
        {
            // Return null if the race does not have subraces
            Debug.Log($"Race '{race.Name}' has no subraces.");
            return null;
        }
    }




    private Race LookupRace(string raceName)
    {
        Race race = PermaLists.Instance.Races.FirstOrDefault(r => r.Name == raceName);
        if (race != null)
        {
            return race;
        }
        Debug.LogError($"Race '{raceName}' not found. Using default race.");
        return new Race
        {
            Name = "Human",
            BaseHealth = 10,
            BaseStrength = 10,
            BaseDexterity = 10,
            BaseConstitution = 10,
            BaseWisdom = 10,
            BaseIntelligence = 10,
            BaseCharisma = 10,
            BaseLuck = 10
        };
    }

    private Personality GetRandomPersonality(NPCRole role)
    {
        var dialogueScripts = PermaLists.Instance.DialogueScripts;
        string roleString = role.ToString();

        var applicablePersonalities = new List<Personality>();

        foreach (var script in dialogueScripts)
        {
            if (script.Roles.Contains(roleString, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var personalityDialogue in script.Personalities)
                {
                    var personality = PermaLists.Instance.Personalities
                        .FirstOrDefault(p => p.PersonalityName == personalityDialogue.Personality);
                    if (personality != null)
                    {
                        applicablePersonalities.Add(personality);
                    }
                }
            }
        }

        if (applicablePersonalities.Count == 0)
        {
            Debug.LogWarning($"No applicable personalities with dialogue found for role {roleString}. Defaulting to generic personality.");
            return new Personality
            {
                PersonalityName = "Default",
                Rarity = 1
            };
        }

        List<Personality> weightedPersonalities = new List<Personality>();
        foreach (var personality in applicablePersonalities)
        {
            for (int i = 0; i < personality.Rarity; i++)
            {
                weightedPersonalities.Add(personality);
            }
        }

        int randomIndex = UnityEngine.Random.Range(0, weightedPersonalities.Count);
        return weightedPersonalities[randomIndex];
    }

    // Method to assign stance based on NPC role
    public void AssignStance(NPC npc)
    {
        if (npc.Role != NPCRole.Villager)
        {
            npc.Stance = NPCStance.TrueIdle;
            Debug.Log($"NPC {npc.Name} with role {npc.Role} set to TrueIdle stance.");
        }
        else
        {
            npc.Stance = NPCStance.Default;
            Debug.Log($"NPC {npc.Name} with role {npc.Role} set to default stance.");
        }
    }

    // Method to set the initial state of the NPC based on their role or stance
    private void SetInitialStateForNPC(NPC npc)
    {
        if (npc.Stance == NPCStance.TrueIdle)
        {
            npc.SetInitialState(new TrueIdleState());
            Debug.Log($"NPC {npc.Name} initialized with TrueIdleState.");
        }
        else if (npc.Stance == NPCStance.Hostile)
        {
            npc.SetInitialState(new HostileState());
            Debug.Log($"NPC {npc.Name} initialized with HostileState.");
        }
        else if (npc.Stance == NPCStance.Friendly)
        {
            npc.SetInitialState(new FriendlyState());
            Debug.Log($"NPC {npc.Name} initialized with FriendlyState.");
        }
        else
        {
            npc.SetInitialState(new IdleState());
            Debug.Log($"NPC {npc.Name} initialized with IdleState.");
        }
    }

    private bool ValidateNPCIntegrity(NPC npc)
    {
        if (npc == null)
        {
            GameDebugger.Instance.LogError("NPC validation failed: NPC is null.");
            return false;
        }

        List<string> missingProperties = new List<string>();

        // Check required fields
        if (string.IsNullOrEmpty(npc.FirstName)) missingProperties.Add("FirstName");
        if (string.IsNullOrEmpty(npc.Surname)) missingProperties.Add("Surname");
        if (npc.Race == null) missingProperties.Add("Race");
        if (npc.Anatomy == null) missingProperties.Add("Anatomy");
        if (npc.Personality == null) missingProperties.Add("Personality");

        // Log missing properties
        if (missingProperties.Count > 0)
        {
            GameDebugger.Instance.LogError($"NPC {npc.NPCID} failed integrity check: Missing {string.Join(", ", missingProperties)}.");
            return false;
        }

        // Warn if the role is set to default
        if (npc.Role == NPCRole.Default)
        {
            GameDebugger.Instance.LogWarning($"NPC {npc.NPCID} has a default role. Ensure this is intentional.");
        }

        // Ensure all stats are at least 1
        npc.Health = Mathf.Max(npc.Health, 1);
        npc.MaxHealth = Mathf.Max(npc.MaxHealth, 1);
        npc.Strength = Mathf.Max(npc.Strength, 1);
        npc.Dexterity = Mathf.Max(npc.Dexterity, 1);
        npc.Constitution = Mathf.Max(npc.Constitution, 1);
        npc.Wisdom = Mathf.Max(npc.Wisdom, 1);
        npc.Intelligence = Mathf.Max(npc.Intelligence, 1);
        npc.Charisma = Mathf.Max(npc.Charisma, 1);
        npc.Luck = Mathf.Max(npc.Luck, 1);

        // Ensure NPC has an inventory initialized
        if (npc.Inventory == null)
        {
            npc.Inventory = new CharacterInventory();
            GameDebugger.Instance.LogWarning($"NPC {npc.NPCID} had a null inventory. Initialized a new one.");
        }

        // Final check
        return true;
    }

    #region AnatomyAdjustment

    private void AdjustAnatomy(NPC npc)
    {
        if (npc.Anatomy == null)
        {
            Debug.LogError($"[NPCGenerator] Cannot adjust anatomy: {npc.Name} has no anatomy assigned.");
            return;
        }

        Debug.Log($"[NPCGenerator] Adjusting Anatomy for {npc.Name}");

        // Expanded possible changes: Missing parts & scars
        List<string> possibleChanges = new List<string>
    {
        // Missing body parts
        "Missing Eye", "Missing Hand", "Missing Arm", "Missing Leg", "Missing Finger", "Missing Foot",

        // Scars in various locations
        "Scar on Head", "Scar on Arm", "Scar on Leg", "Scar on Torso", "Scar on Hand"
    };

        // Randomly apply 1-3 modifications
        int numModifications = UnityEngine.Random.Range(1, 4);
        HashSet<string> appliedModifications = new HashSet<string>();

        for (int i = 0; i < numModifications; i++)
        {
            string selectedChange = possibleChanges[UnityEngine.Random.Range(0, possibleChanges.Count)];

            if (!appliedModifications.Contains(selectedChange))
            {
                appliedModifications.Add(selectedChange);
                ApplyAnatomyChange(npc, selectedChange);
            }
        }

        Debug.Log($"[NPCGenerator] {npc.Name} now has {string.Join(", ", appliedModifications)}.");
    }

    private void ApplyAnatomyChange(NPC npc, string change)
    {
        switch (change)
        {
            case "Missing Eye":
                MarkBodyPartAsLost(npc, "Eye");
                break;

            case "Missing Hand":
                MarkBodyPartAsLost(npc, "Hand");
                break;

            case "Missing Arm":
                MarkBodyPartAsLost(npc, "Arm");
                break;

            case "Missing Leg":
                MarkBodyPartAsLost(npc, "Leg");
                break;

            case "Missing Finger":
                MarkBodyPartAsLost(npc, "Finger");
                break;

            case "Missing Foot":
                MarkBodyPartAsLost(npc, "Foot");
                break;

            case "Scar on Head":
            case "Scar on Arm":
            case "Scar on Leg":
            case "Scar on Torso":
            case "Scar on Hand":
                AddScarToBodyPart(npc, change.Replace("Scar on ", ""));
                break;

            default:
                Debug.LogWarning($"[NPCGenerator] Unknown anatomy change: {change}");
                break;
        }
    }

private void MarkBodyPartAsLost(NPC npc, string partName)
{
    BodyPart part = npc.Anatomy.GetRandomBodyPart(partName);

    if (part == null)
    {
        Debug.LogWarning($"[NPCGenerator] {npc.Name} does not have a {partName} to lose.");
        return;
    }

    part.LosePart();
    Debug.Log($"[NPCGenerator] {npc.Name} has lost their {part.Name}.");
}

private void AddScarToBodyPart(NPC npc, string partName)
{
    BodyPart part = npc.Anatomy.GetRandomBodyPart(partName);

    if (part == null)
    {
        Debug.LogWarning($"[NPCGenerator] {npc.Name} does not have a {partName} to scar.");
        return;
    }

    part.IncreaseScar();
    Debug.Log($"[NPCGenerator] {npc.Name} now has a scar on their {part.Name}. Severity: {part.Scars}");
}
    #endregion

}
