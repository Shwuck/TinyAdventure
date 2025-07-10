using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public class VillageDataLoader : MonoBehaviour, IDataLoader
{
    public void LoadData()
    {
        LoadVillageCreationDataFromJson();
    }

    public void LoadVillageCreationDataFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "VillageCreationData.json");

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            VillageCreationDataWrapper villageCreationData = JsonConvert.DeserializeObject<VillageCreationDataWrapper>(json);

            // Assign the loaded data to PermaLists
            PermaLists.Instance.VillageCreationData = villageCreationData.Villages;

            Debug.Log("Village creation data loaded successfully.");
        }
        else
        {
            Debug.LogError("VillageCreationData.json not found in StreamingAssets!");
        }
    }
}

public class VillageCreationDataWrapper
{
    public List<VillageCreationData> Villages { get; set; }
}

public class VillageCreationData
{
    public VillageType VillageType { get; set; }
    public bool IsValid { get; set; }
    public int PrestigeLevel { get; set; }
    public string DominantRace { get; set; }
    public List<string> CommonRaces { get; set; }
    public List<string> UncommonRaces { get; set; }
    public List<string> RareRaces { get; set; }
    public List<string> NPCRoles { get; set; }
    public List<TerrainType> PreferredTerrains { get; set; }
}


public enum VillageType
{
    HumanVillage,
    DwarvenHall,
    ElvenGrove,
    SwampVillage,
    CaraphraxNest,
    SabrenCamp
}
