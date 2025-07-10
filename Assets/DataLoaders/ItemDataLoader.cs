using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;

public class ItemDataLoader : MonoBehaviour, IDataLoader
{

    public void LoadData()
    {
        LoadItemCreationDataFromJson();
        LoadItemNamingDataFromJson();
        CheckItemDataIntegrity();
        PopulateExcludedMaterialTypes();
        AssignMainHandToWeapons();
        PopulateWeaponsDamageTypes();
    }

    private void LoadItemCreationDataFromJson()
    {
        string[] filePaths = {
            Path.Combine(Application.streamingAssetsPath, "Weapons.json"),
            Path.Combine(Application.streamingAssetsPath, "Tools.json"),
            Path.Combine(Application.streamingAssetsPath, "Apparel.json"),
            Path.Combine(Application.streamingAssetsPath, "Miscellaneous.json"),
            Path.Combine(Application.streamingAssetsPath, "FruitAndVeg.json"),
            Path.Combine(Application.streamingAssetsPath, "FleshAndBone.json"),
            Path.Combine(Application.streamingAssetsPath, "CookedMeals.json"),
            Path.Combine(Application.streamingAssetsPath, "Constructables.json"),
            Path.Combine(Application.streamingAssetsPath, "Components.json")
        };

        List<ItemCreationData> itemDataList = new List<ItemCreationData>();

        foreach (var filePath in filePaths)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var items = JsonConvert.DeserializeObject<List<ItemCreationData>>(json);
                    itemDataList.AddRange(items);
                }
                catch (JsonException e)
                {
                    Debug.LogError($"Failed to deserialize JSON data from {filePath}: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"{Path.GetFileName(filePath)} not found in StreamingAssets!");
            }
        }

        PermaLists.Instance.ItemCreationData = itemDataList;
        Debug.Log("All item creation data loaded successfully.");
    }

    private void CheckItemDataIntegrity()
    {
        if (PermaLists.Instance.ItemCreationData == null)
        {
            Debug.LogError("ItemCreationData is null after loading. Data integrity cannot be verified.");
            return;
        }

        int itemCount = PermaLists.Instance.ItemCreationData.Count;
        if (itemCount == 0)
        {
            Debug.LogWarning("ItemCreationData list is empty.");
        }
        else
        {
            Debug.Log($"ItemCreationData loaded with {itemCount} items.");
        }

        foreach (var item in PermaLists.Instance.ItemCreationData)
        {
            if (item == null)
            {
                Debug.LogWarning("Null item found in ItemCreationData list.");
                continue;
            }

            if (item.ItemTypes == null || item.ItemTypes.Count == 0)
            {
                Debug.LogWarning($"Item '{item.Name}' has missing or empty ItemTypes.");
            }
        }
    }

    private void PopulateExcludedMaterialTypes()
    {
        var rules = new Dictionary<ItemType, List<MaterialType>>
        {
            { ItemType.Clothing, new List<MaterialType> { MaterialType.Stone, MaterialType.Metal, MaterialType.Wood } },
            { ItemType.Weapon, new List<MaterialType> { MaterialType.Fabric } },
            { ItemType.Armour, new List<MaterialType> { MaterialType.Gemstone, MaterialType.Wood } },
            { ItemType.Consumable, new List<MaterialType> { MaterialType.Metal, MaterialType.Wood, MaterialType.Stone, MaterialType.Leather } },
            { ItemType.Component, new List<MaterialType> { MaterialType.Fabric, MaterialType.Leather } },
            { ItemType.Junk, new List<MaterialType> { MaterialType.Gemstone } },
            { ItemType.Tool, new List<MaterialType> { MaterialType.Fabric, MaterialType.Gemstone } }
        };

        foreach (var item in PermaLists.Instance.ItemCreationData)
        {
            if (item.ExcludedMaterialTypes == null || item.ExcludedMaterialTypes.Count == 0)
            {
                foreach (var itemType in item.ItemTypes)
                {
                    if (rules.ContainsKey(itemType))
                    {
                        foreach (var materialType in rules[itemType])
                        {
                            if (!item.ExcludedMaterialTypes.Contains(materialType))
                            {
                                item.ExcludedMaterialTypes.Add(materialType);
                            }
                        }
                    }
                }
            }
        }

        Debug.Log("Excluded material types populated based on item types.");
    }

    private void PopulateWeaponsDamageTypes()
    {
        var damageTypeMapping = new Dictionary<WeaponType, List<DamageType>>
    {
        { WeaponType.Sharp, new List<DamageType> { DamageType.Slashing, DamageType.Piercing } },
        { WeaponType.Blunt, new List<DamageType> { DamageType.Bludgeoning, DamageType.Crushing } },
        { WeaponType.Serrated, new List<DamageType> { DamageType.Rending } },
        { WeaponType.Magic, new List<DamageType> { DamageType.Magic } }
    };

        foreach (var item in PermaLists.Instance.ItemCreationData)
        {
            if (item.ItemTypes.Contains(ItemType.Weapon) && item.WeaponType != WeaponType.None)
            {

                if (damageTypeMapping.TryGetValue(item.WeaponType, out var damageTypes))
                {
                    item.DamageTypes = damageTypes.ToList();  // Replace directly with new list
                }
                else
                {
                    Debug.LogWarning($"Weapon '{item.Name}' has an unmapped WeaponType '{item.WeaponType}'.");
                }
            }
        }

        Debug.Log("Weapons damage types populated based on weapon types.");
    }

    private void AssignMainHandToWeapons()
    {
        foreach (var item in PermaLists.Instance.ItemCreationData)
        {
            // Check if the item is a weapon by looking for ItemType.Weapon in the ItemTypes list
            if (item.ItemTypes.Contains(ItemType.Weapon))
            {
                // Initialize EquipmentSlots list if it's null
                if (item.EquipmentSlots == null)
                {
                    item.EquipmentSlots = new List<string>();
                }

                // Add "MainHand" to EquipmentSlots if it's not already present
                if (!item.EquipmentSlots.Contains("MainHand"))
                {
                    item.EquipmentSlots.Add("MainHand");
                    Debug.Log($"MainHand added to EquipmentSlots for item: {item.Name}");
                }
            }
        }

        Debug.Log("MainHand assignment completed for all weapons.");
    }

    private void LoadItemNamingDataFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "ItemNameData.json");

        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                PermaLists.Instance.ItemNamingData = JsonConvert.DeserializeObject<ItemNamingData>(json);
                Debug.Log("Item naming data loaded successfully.");
            }
            catch (JsonException e)
            {
                Debug.LogError($"Failed to deserialize ItemNameData.json: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"ItemNameData.json not found in StreamingAssets!");
        }
    }
}

[System.Serializable]
public class ItemCreationData
{
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsUnique { get; set; }
    public bool IsIdentified { get; set; }
    public bool IsHistoric { get; set; }
    public bool IsActive { get; set; }
    public int Quantity { get; set; }
    public int Value { get; set; }
    public bool IsTradable { get; set; }
    public bool Reserved { get; set; }
    public WeaponType WeaponType { get; set; }
    public int DamageOutput { get; set; }
    public int ArmourValue { get; set; }
    public List<string> EquipmentSlots { get; set; }
    public int HungerValue { get; set; }
    public int ThirstValue { get; set; }
    public bool IsEdible { get; set; }
    public List<ItemType> ItemTypes { get; set; }
    public List<string> Interfaces { get; set; }
    public List<MaterialType> ExcludedMaterialTypes { get; set; } = new List<MaterialType>();
    public List<ComponentRequirement> ComponentsRequired { get; set; } = new List<ComponentRequirement>();
    public List<DamageType> DamageTypes { get; set; } = new List<DamageType>();
    public string ObjectString { get; set; }
    public ItemSize Size { get; set; } = ItemSize.Medium; // Default to Medium if not specified

    public List<BuffDebuff> Modifiers { get; set; } = new List<BuffDebuff>();  // Existing (Buffs & Debuffs)
    public List<OnHitEffect> OnHitEffects { get; set; } = new List<OnHitEffect>(); // New - For Weapons
    public List<OnHitEffect> OnHitTakenEffects { get; set; } = new List<OnHitEffect>(); // New - For Armor
}



[System.Serializable]
public class ComponentRequirement
{
    public string ComponentName { get; set; }
    public List<MaterialType> AllowedMaterialTypes { get; set; } = new List<MaterialType>();
}

[System.Serializable]
public class ItemNamingData
{
    public Dictionary<string, List<NameThreshold>> Prefixes { get; set; }
    public Dictionary<string, List<NameThreshold>> Suffixes { get; set; }
}

[System.Serializable]
public class NameThreshold
{
    public int Min { get; set; }  
    public int Max { get; set; }  
    public string Name { get; set; }
}
