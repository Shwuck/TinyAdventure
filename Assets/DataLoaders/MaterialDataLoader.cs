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

    public void LoadMaterialCreationDataFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "MaterialCreationData.json");

        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                List<ObjectMaterial> materials = JsonConvert.DeserializeObject<List<ObjectMaterial>>(json);

                if (materials != null)
                {
                    PermaLists.Instance.ObjectMaterials = materials;
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

public class ObjectMaterial
{
    public string MaterialName { get; set; }
    public MaterialType Type { get; set; }
    public MaterialRarity Rarity { get; set; }
}
