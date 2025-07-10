using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public class LootDataLoader : MonoBehaviour, IDataLoader
{
    public void LoadData()
    {
        LoadLootCreationDataFromJson();
        LoadEntityLootDataFromJson();
    }

    public void LoadLootCreationDataFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "LootCreationData.json");

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            LootCreationDataWrapper lootCreationData = JsonConvert.DeserializeObject<LootCreationDataWrapper>(json);

            // Assign the loaded data to PermaLists
            PermaLists.Instance.LootCreationData = lootCreationData.Loots;

            Debug.Log("Loot creation data loaded successfully.");
        }
        else
        {
            Debug.LogError("LootCreationData.json not found in StreamingAssets!");
        }
    }

    public void LoadEntityLootDataFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "EntityLootData.json");

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            EntityLootDataWrapper entityLootData = JsonConvert.DeserializeObject<EntityLootDataWrapper>(json);

            // Assign the loaded data to PermaLists
            PermaLists.Instance.EntityLootData = entityLootData.Entities;

            Debug.Log("Entity loot data loaded successfully.");
        }
        else
        {
            Debug.LogError("EntityLootData.json not found in StreamingAssets!");
        }
    }
}

public class LootCreationDataWrapper
{
    public List<LootCreationData> Loots { get; set; }
}

public class LootCreationData
{
    public string ItemName { get; set; }
    public List<string> CommonlyDroppedBy { get; set; }
    public List<string> UncommonlyDroppedBy { get; set; }
    public List<string> RarelyDroppedBy { get; set; }
    public List<string> AlwaysDroppedBy { get; set; }
}

public class EntityLootDataWrapper
{
    public List<EntityLootData> Entities { get; set; }
}

public class EntityLootData
{
    public string EntityName { get; set; }
    public Dictionary<string, int> CommonlyDrops { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> UncommonlyDrops { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> RarelyDrops { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> AlwaysDrops { get; set; } = new Dictionary<string, int>();
}
