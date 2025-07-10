using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;

public class AnatomyDataLoader : MonoBehaviour, IDataLoader
{
    public void LoadData()
    {
        LoadAnatomyDataFromJson();
        LoadBodyPartDataFromJson();
    }

    private void LoadAnatomyDataFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "AnatomyData.json");

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            ParseAndAssignAnatomyData(json);
        }
        else
        {
            Debug.LogError("AnatomyData.json not found in StreamingAssets!");
        }
    }

    private void ParseAndAssignAnatomyData(string json)
    {
        try
        {
            List<AnatomyData> loadedAnatomy = JsonConvert.DeserializeObject<List<AnatomyData>>(json);
            if (loadedAnatomy == null || loadedAnatomy.Count == 0)
            {
                Debug.LogError("No valid anatomy data found in JSON.");
                return;
            }

            PermaLists.Instance.AnatomyData = loadedAnatomy;
            Debug.Log($"Anatomy data loaded successfully. {loadedAnatomy.Count} anatomies stored.");
        }
        catch (JsonSerializationException ex)
        {
            Debug.LogError($"JSON Deserialization error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"General error: {ex.Message}");
        }
    }

    private void ParseAndAssignBodyPartData(string json)
    {
        try
        {
            List<BodyPartData> loadedBodyParts = JsonConvert.DeserializeObject<List<BodyPartData>>(json);
            Dictionary<string, BodyPartData> bodyPartDictionary = new Dictionary<string, BodyPartData>();

            foreach (var partData in loadedBodyParts)
            {
                if (partData.EquipmentSlots == null)
                {
                    partData.EquipmentSlots = new List<string>(); // Ensure non-null
                }
                StoreBodyPartWithSubparts(partData, bodyPartDictionary);
            }

            PermaLists.Instance.BodyPartData = bodyPartDictionary;
            Debug.Log($"Body part data loaded successfully. {bodyPartDictionary.Count} total body parts stored.");
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

    /// Recursively stores all body parts and ensures correct classification.
    private void StoreBodyPartWithSubparts(BodyPartData partData, Dictionary<string, BodyPartData> dictionary)
    {
        if (!dictionary.ContainsKey(partData.Name))
        {
            dictionary[partData.Name] = partData;
        }

        if (partData.SubParts != null)
        {
            foreach (var subPart in partData.SubParts)
            {
                StoreBodyPartWithSubparts(subPart, dictionary);
            }
        }
    }


    /// **Classifies parts as Base/Sub and Stores them**
    private void ClassifyBodyPart(BodyPartData partData, Dictionary<string, BodyPartData> dictionary)
    {
        if (!dictionary.ContainsKey(partData.Name))
        {
            // **Ensure correct classification of body parts**
            partData.HasSubs = partData.SubParts != null && partData.SubParts.Count > 0;
            partData.BasePart = !dictionary.Values.Any(bp => bp.SubParts?.Any(sp => sp.Name == partData.Name) == true);
            partData.SubPart = !partData.BasePart;

            dictionary[partData.Name] = partData;
        }

        if (partData.SubParts != null)
        {
            foreach (var subPart in partData.SubParts)
            {
                ClassifyBodyPart(subPart, dictionary);
            }
        }
    }

    private void LoadBodyPartDataFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "BodyPartCreationData.json");

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            ParseAndAssignBodyPartData(json);
        }
        else
        {
            Debug.LogError("BodyPartData.json not found in StreamingAssets!");
        }
    }
}



[System.Serializable]
public class AnatomyData
{
    public string BodyType; // Defines the overall anatomical structure
    public Dictionary<string, int> DefaultParts; // List of default body parts for this anatomy
}

[System.Serializable]
public class BodyPartData
{
    public string Name;
    public bool BasePart;
    public bool SubPart;
    public bool HasSubs;
    public string BodyPartType;
    public string Position;
    public int MaxHealth;
    public bool IsVital;
    public List<string> EquipmentSlots;
    public List<BodyPartData> SubParts;
}

