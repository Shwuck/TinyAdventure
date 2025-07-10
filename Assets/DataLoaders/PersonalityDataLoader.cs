using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class PersonalityDataLoader : MonoBehaviour, IDataLoader
{
    private const string FileName = "PersonalityCreationData.json";
    private string FilePath => Path.Combine(Application.streamingAssetsPath, FileName);

    public void LoadData()
    {
        LoadPersonalityDataFromJson();
    }

    private void LoadPersonalityDataFromJson()
    {
        if (File.Exists(FilePath))
        {
            try
            {
                string json = File.ReadAllText(FilePath);
                List<Personality> personalities = DeserializeJson(json);
                if (personalities != null)
                {
                    PermaLists.Instance.Personalities = personalities;
                    Debug.Log("Personality data loaded successfully.");
                }
                else
                {
                    Debug.LogError("Failed to parse personality data or no personalities found.");
                }
            }
            catch (JsonException e)
            {
                Debug.LogError($"JSON deserialization error: {e.Message}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"General error: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"{FileName} not found in StreamingAssets!");
        }
    }

    private List<Personality> DeserializeJson(string json)
    {
        return JsonConvert.DeserializeObject<List<Personality>>(json);
    }
}

[System.Serializable]
public class Personality
{
    public string PersonalityName { get; set; }
    public int Rarity { get; set; } // Higher Value equals more common personality
}
