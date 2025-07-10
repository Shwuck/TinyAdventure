using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;

public class NPCDataLoader : MonoBehaviour, IDataLoader
{
    private void Start()
    {
        LoadData();
    }

    public void LoadData()
    {
        LoadCreationDataFromJson();
        UnlockAllContentIfDebugMode();
        CheckForNullLists(); // Call the method at the end of data loading
    }

    public void LoadCreationDataFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "NPCCreationData.json");

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            ParseAndAssignCreationData(json);
        }
        else
        {
            Debug.LogError("NPCCreationData.json not found in StreamingAssets!");
        }
    }

    private void ParseAndAssignCreationData(string json)
    {
        try
        {
            NPCcreationData creationData = JsonConvert.DeserializeObject<NPCcreationData>(json);
            AssignCreationDataToPermaLists(creationData);
            Debug.Log("NPC creation data loaded successfully.");
        }
        catch (JsonSerializationException ex)
        {
            Debug.LogError($"JSON Deserialization error: {ex.Message}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"General error: {ex.Message}");
        }
    }

    private void AssignCreationDataToPermaLists(NPCcreationData creationData)
    {
        PermaLists.Instance.HumanFirstNames = creationData.HumanFirstNames;
        PermaLists.Instance.HumanSurnames = creationData.HumanSurnames;
        PermaLists.Instance.DwarfFirstNames = creationData.DwarfFirstNames;
        PermaLists.Instance.DwarfSurnames = creationData.DwarfSurnames;
        PermaLists.Instance.ElfFirstNames = creationData.ElfFirstNames;
        PermaLists.Instance.ElfSurnames = creationData.ElfSurnames;
        PermaLists.Instance.SabrenFirstNames = creationData.SabrenFirstNames;
        PermaLists.Instance.SabrenSurnames = creationData.SabrenSurnames;
        PermaLists.Instance.SaurosinFirstNames = creationData.SaurosinFirstNames;
        PermaLists.Instance.SaurosinSurnames = creationData.SaurosinSurnames;
        PermaLists.Instance.CaraphraxFirstNames = creationData.CaraphraxFirstNames;
        PermaLists.Instance.CaraphraxSurnames = creationData.CaraphraxSurnames;
        PermaLists.Instance.Races = creationData.Races;
        PermaLists.Instance.Backgrounds = creationData.Backgrounds;
        PermaLists.Instance.Loadouts = creationData.Loadouts;
        PermaLists.Instance.RoleData = creationData.RoleData;
    }

    private void UnlockAllContentIfDebugMode()
    {
        if (GameManager.Instance.isDebugModeOn)
        {
            foreach (var race in PermaLists.Instance.Races)
            {
                race.IsUnlocked = true;
                if (race.SubRaces != null)
                {
                    foreach (var subRace in race.SubRaces)
                    {
                        subRace.IsUnlocked = true;
                    }
                }
            }

            foreach (var background in PermaLists.Instance.Backgrounds)
            {
                background.IsUnlocked = true;
            }

            Debug.Log("All content unlocked in debug mode.");
        }
        else
        {
            foreach (var race in PermaLists.Instance.Races)
            {
                if (race.Name == "Human")
                {
                    race.IsUnlocked = true;
                    if (race.SubRaces != null)
                    {
                        foreach (var subRace in race.SubRaces)
                        {
                            subRace.IsUnlocked = true;
                        }
                    }
                }
            }

            Debug.Log("Human race content unlocked.");
        }
    }


    private void CheckForNullLists()
    {
        var creationData = PermaLists.Instance;

        if (creationData.HumanFirstNames == null)
            Debug.LogError("HumanFirstNames list is null.");
        else
            Debug.Log($"HumanFirstNames list contains {creationData.HumanFirstNames.Count} items.");

        if (creationData.HumanSurnames == null)
            Debug.LogError("HumanSurnames list is null.");
        else
            Debug.Log($"HumanSurnames list contains {creationData.HumanSurnames.Count} items.");

        if (creationData.DwarfFirstNames == null)
            Debug.LogError("DwarfFirstNames list is null.");
        else
            Debug.Log($"DwarfFirstNames list contains {creationData.DwarfFirstNames.Count} items.");

        if (creationData.DwarfSurnames == null)
            Debug.LogError("DwarfSurnames list is null.");
        else
            Debug.Log($"DwarfSurnames list contains {creationData.DwarfSurnames.Count} items.");

        if (creationData.ElfFirstNames == null)
            Debug.LogError("ElfFirstNames list is null.");
        else
            Debug.Log($"ElfFirstNames list contains {creationData.ElfFirstNames.Count} items.");

        if (creationData.ElfSurnames == null)
            Debug.LogError("ElfSurnames list is null.");
        else
            Debug.Log($"ElfSurnames list contains {creationData.ElfSurnames.Count} items.");

        if (creationData.SabrenFirstNames == null)
            Debug.LogError("SabrenFirstNames list is null.");
        else
            Debug.Log($"SabrenFirstNames list contains {creationData.SabrenFirstNames.Count} items.");

        if (creationData.SabrenSurnames == null)
            Debug.LogError("SabrenSurnames list is null.");
        else
            Debug.Log($"SabrenSurnames list contains {creationData.SabrenSurnames.Count} items.");

        if (creationData.SaurosinFirstNames == null)
            Debug.LogError("SaurosinFirstNames list is null.");
        else
            Debug.Log($"SaurosinFirstNames list contains {creationData.SaurosinFirstNames.Count} items.");

        if (creationData.SaurosinSurnames == null)
            Debug.LogError("SaurosinSurnames list is null.");
        else
            Debug.Log($"SaurosinSurnames list contains {creationData.SaurosinSurnames.Count} items.");

        if (creationData.CaraphraxFirstNames == null)
            Debug.LogError("CaraphraxFirstNames list is null.");
        else
            Debug.Log($"CaraphraxFirstNames list contains {creationData.CaraphraxFirstNames.Count} items.");

        if (creationData.CaraphraxSurnames == null)
            Debug.LogError("CaraphraxSurnames list is null.");
        else
            Debug.Log($"CaraphraxSurnames list contains {creationData.CaraphraxSurnames.Count} items.");

        if (creationData.Races == null)
            Debug.LogError("Races list is null.");
        else
            Debug.Log($"Races list contains {creationData.Races.Count} items.");

        if (creationData.Backgrounds == null)
            Debug.LogError("Backgrounds list is null.");
        else
            Debug.Log($"Backgrounds list contains {creationData.Backgrounds.Count} items.");

        if (creationData.Loadouts == null)
            Debug.LogError("Loadouts list is null.");
        else
            Debug.Log($"Loadouts list contains {creationData.Loadouts.Count} items.");

        if (creationData.RoleData == null)
            Debug.LogError("RoleData list is null.");
        else
            Debug.Log($"RoleData list contains {creationData.RoleData.Count} items.");
    }

}



[System.Serializable]
public class NPCcreationData
{
    public List<string> HumanFirstNames;
    public List<string> HumanSurnames;
    public List<string> DwarfFirstNames;
    public List<string> DwarfSurnames;
    public List<string> ElfFirstNames;
    public List<string> ElfSurnames;
    public List<string> SabrenFirstNames;
    public List<string> SabrenSurnames;
    public List<string> SaurosinFirstNames;
    public List<string> SaurosinSurnames;
    public List<string> CaraphraxFirstNames;
    public List<string> CaraphraxSurnames;
    public List<Race> Races;
    public List<Loadout> Loadouts;
    public List<Background> Backgrounds;
    public List<NPCRoleData> RoleData;
}

[System.Serializable]
public class Loadout
{
    public string LoadoutName;
    public List<string> ApplicableNPCs;
    public List<string> ApplicablePlayerBackgrounds;
    public Dictionary<EquipmentSlot, string> Equipment; // Equipment the NPC will be wearing/holding at start
    public Dictionary<string, int> Inventory;  // Specific items found in the NPCs inventory
    public Dictionary<ItemType, int> InventoryByType; // Items found in the NPCs inventory by type
    public int Money;
}

[System.Serializable]
public class NPCRoleData
{
    public NPCRole Role;
    public List<string> Titles; // Such as "The Wise" for a Scholar or "The Battlehardened" for a Warrior
    public List<string> LoadoutNames; //Not applicable right now, leave blank
    public List<string> FrequentNeeds; // ItemTypes the NPC commonly needs, like Fruit and Vegetables for a Villager. Or Weapons, for a Warrior.
    public NewsType NewsType; // Local, Regional, Worldwide
    public bool IsCraftsman;
    public CraftingType CraftingType;
    public Dictionary<string, int> StatModifiers;
}

[System.Serializable]
public class Race
{
    public string Name;
    public string Description;
    public string UnlockHint;
    public bool IsUnlocked;
    public VillageType Village;
    public bool HasSubRace;
    public List<SubRace> SubRaces;
    public int BaseHealth;
    public int BaseStrength;
    public int BaseDexterity;
    public int BaseConstitution;
    public int BaseWisdom;
    public int BaseIntelligence;
    public int BaseCharisma;
    public int BaseLuck;
    public int Rarity;
    public List<TerrainType> PreferredTerrains;

    public Dictionary<TerrainType, float> ExpansionModifiers { get; set; } = new Dictionary<TerrainType, float>();

    public string BodyType;

    public float GetExpansionModifier(TerrainType terrain)
    {
        return ExpansionModifiers.TryGetValue(terrain, out float modifier) ? modifier : 1.5f;
    }
}

[System.Serializable]
public class SubRace
{
    public string Name;
    public string Description;
    public string UnlockHint;
    public bool IsUnlocked;
    public VillageType Village;
    public int BaseHealth;
    public int BaseStrength;
    public int BaseDexterity;
    public int BaseConstitution;
    public int BaseWisdom;
    public int BaseIntelligence;
    public int BaseCharisma;
    public int BaseLuck;
    public int Rarity;
    public List<TerrainType> PreferredTerrains;

    public Dictionary<TerrainType, float> ExpansionModifiers { get; set; } = new Dictionary<TerrainType, float>();

    public string BodyType;

    public float GetExpansionModifier(TerrainType terrain)
    {
        return ExpansionModifiers.TryGetValue(terrain, out float modifier) ? modifier : 1.5f;
    }

    public string MainRaceName;
}

[System.Serializable]
public class Background
{
    public string Name;
    public string Description;
    public string UnlockHint;
    public bool IsUnlocked;
    public Dictionary<string, int> StatModifiers;
}
