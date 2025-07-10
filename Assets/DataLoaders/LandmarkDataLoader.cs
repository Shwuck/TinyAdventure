using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LandmarkDataLoader : MonoBehaviour, IDataLoader
{
 

    public void LoadData()
    {
        LoadLandmarkDataFromJson();
    }

    public void LoadLandmarkDataFromJson()
    {
        string filePath = System.IO.Path.Combine(Application.streamingAssetsPath, "LandmarkCreationData.json");

        if (System.IO.File.Exists(filePath))
        {
            string json = System.IO.File.ReadAllText(filePath);
            LandmarkCreationDataList landmarkData = JsonUtility.FromJson<LandmarkCreationDataList>(json);

            // Assign the loaded data to a global manager or singleton instance
            PermaLists.Instance.LandmarkCreationData = landmarkData.Landmarks;

            Debug.Log("Landmark creation data loaded successfully.");
        }
        else
        {
            Debug.LogError("LandmarkCreationData.json not found in StreamingAssets!");
        }
    }
}


[System.Serializable]
public class LandmarkCreationData
{
    public string LandmarkName { get; set; } // The name of the landmark
    public List<TerrainType> ApplicableTerrains { get; set; } // List of terrains where the landmark can be placed
    public List<Climate> ApplicableClimates { get; set; } // List of climates suitable for the landmark
    public bool OverrideTerrain { get; set; } // Whether the landmark overrides the existing terrain
    public TerrainType TerrainToOverride { get; set; } // The terrain type to override with this landmark
    public TerrainType TerrainToDisplay { get; set; } // The terrain type to display for the landmark
    public LandmarkRarity Rarity { get; set; } // The rarity of the landmark
    public bool Basic { get; set; } // Whether this is a basic landmark or not
    public Dictionary<string, int> ObjectsToPlace { get; set; } // A dictionary of objects to place (object name: count)

    public LandmarkCreationData()
    {
        // Initialize the list and dictionary to avoid null references
        ApplicableTerrains = new List<TerrainType>();
        ApplicableClimates = new List<Climate>();
        ObjectsToPlace = new Dictionary<string, int>();
    }
}

public enum LandmarkRarity
{
    Common,
    Uncommon,
    Rare,
    VeryRare,
    Legendary
}

[System.Serializable]
public class LandmarkCreationDataList
{
    public List<LandmarkCreationData> Landmarks;
}
