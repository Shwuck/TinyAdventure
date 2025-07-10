using System.Collections.Generic;
using System;
using System.Collections;
using UnityEngine;

public class IntegrityChecker : MonoBehaviour
{
    // Singleton instance
    private static IntegrityChecker _instance;

    public static IntegrityChecker Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogError("IntegrityChecker instance is not set. Make sure to attach the script to a GameObject in the scene.");
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public List<Loadout> Loadouts => PermaLists.Instance.Loadouts;
    public List<ItemCreationData> ItemCreationData => PermaLists.Instance.ItemCreationData;
    public List<VillageCreationData> VillageCreationData => PermaLists.Instance.VillageCreationData;
    public List<Race> Races => PermaLists.Instance.Races;
    public List<NPCRoleData> RoleData => PermaLists.Instance.RoleData;

    public void CheckDataIntegrity()
    {
        CheckItemTypesIntegrity();
        CheckLoadoutIntegrity();
        RemoveMissingItemsFromLoadouts();
        CheckRoleDataIntegrity();
        CheckVillageRaceIntegrity();
        DialogueChecker();
        SetDefaultBodyTypes();

        // After all integrity checks, set the game as good to start
        SetGameAsGoodToStart();
    }

    private void SetGameAsGoodToStart()
    {
        // Set GoodToStart to true and call StartGame on GameManager
        GameManager.Instance.GoodToStart = true;
        GameManager.Instance.StartGame();
    }

    private void DialogueChecker()
    {
        DialogueManager.Instance.SetDialogueScripts();
    }

    private void CheckLoadoutIntegrity()
    {
        HashSet<string> existingItems = new HashSet<string>();
        int totalItems = 0;
        int validItems = 0;
        List<string> missingItems = new List<string>();

        foreach (var item in ItemCreationData)
        {
            existingItems.Add(item.Name);
        }

        foreach (var loadout in Loadouts)
        {
            foreach (var itemName in loadout.Inventory.Keys)
            {
                totalItems++;

                if (existingItems.Contains(itemName))
                {
                    validItems++;
                }
                else
                {
                    missingItems.Add(itemName);
                }
            }
        }

        string logMessage = $"Checked Loadouts for integrity.\n" +
                            $"Total items checked: {totalItems}\n" +
                            $"Valid items found: {validItems}\n" +
                            $"Missing items: {missingItems.Count}\n";

        if (missingItems.Count > 0)
        {
            logMessage += "The following items are missing:\n" + string.Join(", ", missingItems);
        }
        else
        {
            logMessage += "All loadout items are valid.";
        }

        Debug.Log(logMessage);
    }

    private void RemoveMissingItemsFromLoadouts()
    {
        HashSet<string> existingItems = new HashSet<string>();

        foreach (var item in ItemCreationData)
        {
            existingItems.Add(item.Name);
        }

        foreach (var loadout in Loadouts)
        {
            List<string> itemsToRemove = new List<string>();

            foreach (var itemName in loadout.Inventory.Keys)
            {
                if (!existingItems.Contains(itemName))
                {
                    itemsToRemove.Add(itemName);
                }
            }

            foreach (var itemName in itemsToRemove)
            {
                loadout.Inventory.Remove(itemName);
                Debug.Log($"Removed missing item '{itemName}' from loadout '{loadout.LoadoutName}'.");
            }
        }

        Debug.Log("Finished cleaning up loadouts.");
    }

    public void CheckItemTypesIntegrity()
    {
        Debug.Log("Starting CheckItemTypesIntegrity...");

        if (ItemCreationData == null)
        {
            Debug.LogError("ItemCreationData is null. Cannot check item types integrity.");
            return;
        }

        int totalItems = 0;
        int itemsWithMissingTypes = 0;
        List<string> itemsWithMissingTypesNames = new List<string>();
        int weaponsUpdated = 0;

        foreach (var item in ItemCreationData)
        {
            totalItems++;

            if (item == null)
            {
                Debug.LogWarning($"Item at index {totalItems - 1} is null.");
                continue;
            }

            if (item.ItemTypes == null || item.ItemTypes.Count == 0)
            {
                itemsWithMissingTypes++;
                itemsWithMissingTypesNames.Add(item.Name);
                Debug.LogWarning($"Item '{item.Name}' has missing or empty ItemTypes.");
            }

            // Ensure all Weapons with a default or unassigned WeaponType are set to Blunt
            if (item.ItemTypes.Contains(ItemType.Weapon) && item.WeaponType == default(WeaponType))
            {
                item.WeaponType = WeaponType.Blunt;
                weaponsUpdated++;
                Debug.Log($"Item '{item.Name}' was missing WeaponType and has been set to Blunt.");
            }
        }

        Debug.Log($"CheckItemTypesIntegrity completed.\n" +
                  $"Total items checked: {totalItems}\n" +
                  $"Items with missing or empty ItemTypes: {itemsWithMissingTypes}\n" +
                  $"Weapons updated with default WeaponType.Blunt: {weaponsUpdated}");

        if (itemsWithMissingTypes > 0)
        {
            Debug.Log("Items with missing or empty ItemTypes:\n" + string.Join(", ", itemsWithMissingTypesNames));
        }
        else
        {
            Debug.Log("All items have valid ItemTypes.");
        }
    }

    private void CheckVillageRaceIntegrity()
    {
        HashSet<string> validRaces = CreateValidRacesSet();
        List<string> missingRaces = new List<string>();

        foreach (var village in VillageCreationData)
        {
            CheckDominantRaceValidity(village, validRaces, missingRaces);
            RemoveInvalidRacesFromList(village.CommonRaces, validRaces);
            RemoveInvalidRacesFromList(village.UncommonRaces, validRaces);
            RemoveInvalidRacesFromList(village.RareRaces, validRaces);
        }

        if (missingRaces.Count > 0)
        {
            Debug.Log($"Missing races found in village data: {string.Join(", ", missingRaces)}");
        }
        else
        {
            Debug.Log("All village races are valid.");
        }
    }

    public void CheckRoleDataIntegrity()
    {
        Debug.Log("Step 1: Calling CheckRoleDataIntegrity");

        // Step 1: Get all unique item types from ItemCreationData
        HashSet<ItemType> availableItemTypes = new HashSet<ItemType>();

        if (ItemCreationData == null)
        {
            Debug.LogError("ItemCreationData is null. Cannot proceed with integrity check.");
            return;
        }

        Debug.Log("ItemCreationData is not null. Proceeding to gather item types.");

        foreach (var item in ItemCreationData)
        {
            if (item == null)
            {
                Debug.LogWarning("Found a null item in ItemCreationData.");
                continue;
            }

            if (item.ItemTypes == null)
            {
                Debug.LogWarning($"Item '{item.Name}' has a null ItemTypes list.");
                continue;
            }

            foreach (var itemType in item.ItemTypes)
            {
                availableItemTypes.Add(itemType);
            }
        }

        Debug.Log($"Step 2: Created List of ItemTypes. Count of available item types: {availableItemTypes.Count}");

        // Step 2: Check each NPCRoleData for valid FrequentNeeds
        List<string> missingItemTypes = new List<string>();

        foreach (var roleData in PermaLists.Instance.RoleData)
        {
            Debug.Log($"Checking role data for role: {roleData.Role}");

            if (roleData.FrequentNeeds == null)
            {
                Debug.LogWarning($"Role '{roleData.Role}' has no FrequentNeeds defined.");
                continue;
            }

            foreach (var need in roleData.FrequentNeeds)
            {
                if (string.IsNullOrEmpty(need))
                {
                    Debug.LogWarning($"A null or empty FrequentNeed entry found in role '{roleData.Role}'.");
                    continue;
                }

                if (Enum.TryParse(need, out ItemType needType))
                {
                    if (!availableItemTypes.Contains(needType))
                    {
                        missingItemTypes.Add(need);
                        Debug.LogWarning($"FrequentNeed '{need}' in role '{roleData.Role}' is not available in ItemCreationData.");
                    }
                }
                else
                {
                    Debug.LogWarning($"FrequentNeed '{need}' in role '{roleData.Role}' is not a valid ItemType.");
                }
            }
        }

        Debug.Log("Step 3: Finished checking all RoleData entries.");

        // Step 3: Log results
        string logMessage = $"Checked all RoleData entries.\n" +
                            $"Total item types checked: {availableItemTypes.Count}\n" +
                            $"Missing item types: {missingItemTypes.Count}\n";

        if (missingItemTypes.Count > 0)
        {
            logMessage += "The following item types are missing from the loaded items:\n" + string.Join(", ", missingItemTypes);
        }
        else
        {
            logMessage += "All item types required by RoleData are available.";
        }

        Debug.Log(logMessage);
    }

    private void SetDefaultBodyTypes()
    {
        int updatedRaces = 0;
        int updatedAnimals = 0;

        foreach (var race in PermaLists.Instance.Races)
        {
            if (string.IsNullOrEmpty(race.BodyType))
            {
                race.BodyType = "Humanoid";
                updatedRaces++;
                Debug.Log($"Race '{race.Name}' had no BodyType and was set to 'Humanoid'.");
            }
        }

        foreach (var animal in PermaLists.Instance.AnimalCreationData) 
        {
            if (string.IsNullOrEmpty(animal.BodyType)) 
            {
                animal.BodyType = "Quadruped"; 
                updatedAnimals++;
                Debug.Log($"Animal '{animal.AnimalName}' had no BodyType and was set to 'Quadruped'.");
            }
        }

        Debug.Log($"Set default body types for {updatedRaces} races and {updatedAnimals} animals.");
    }





    private HashSet<string> CreateValidRacesSet()
    {
        HashSet<string> validRaces = new HashSet<string>();

        foreach (var race in Races)
        {
            validRaces.Add(race.Name);
        }

        return validRaces;
    }

    private void CheckDominantRaceValidity(VillageCreationData village, HashSet<string> validRaces, List<string> missingRaces)
    {
        if (!string.IsNullOrEmpty(village.DominantRace))
        {
            if (!validRaces.Contains(village.DominantRace))
            {
                missingRaces.Add(village.DominantRace);
                village.IsValid = false;
            }
            else
            {
                village.IsValid = true;
            }
        }
        else
        {
            village.IsValid = false;
        }
    }

    private void RemoveInvalidRacesFromList(List<string> raceNames, HashSet<string> validRaces)
    {
        raceNames.RemoveAll(raceName => !validRaces.Contains(raceName));
    }
}
