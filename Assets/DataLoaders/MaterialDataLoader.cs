using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class MaterialDataLoader : MonoBehaviour, IDataLoader
{
    private void Start()
    {

    }

    public void LoadData()
    {
        LoadMaterialCreationDataFromJson();
    }

    public async void LoadMaterialCreationDataFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "MaterialCreationData.json");

        if (File.Exists(filePath))
        {
            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                List<Material> materials = JsonConvert.DeserializeObject<List<Material>>(json);

                if (materials != null)
                {
                    PermaLists.Instance.Materials = materials;
                    Debug.Log("Material data loaded successfully.");
                }
                else
                {
                    Debug.LogError("Material data could not be loaded - deserialization returned null.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading material data: {e.Message}");
            }
        }
        else
        {
            Debug.LogError("MaterialData.json not found in StreamingAssets!");
        }
    }
}

public enum MaterialRarity
{
    Common,
    Uncommon,
    Rare,
    VeryRare,
    Legendary
}

public enum MaterialType
{
    Metal,
    Fabric,
    Leather,
    Gemstone,
    Wood,
    Stone,
    Other
}

public class Material
{
    public string MaterialName { get; set; }
    public MaterialType Type { get; set; }
    public MaterialRarity Rarity { get; set; }
}