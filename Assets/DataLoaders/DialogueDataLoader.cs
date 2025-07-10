using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;

public class DialogueDataLoader : MonoBehaviour, IDataLoader
{
    public void LoadData()
    {
        LoadDialogueDataFromJson();
    }

    private void LoadDialogueDataFromJson()
    {
        string[] filePaths = {
            Path.Combine(Application.streamingAssetsPath, "DialogueScriptsVillager.json"),
            Path.Combine(Application.streamingAssetsPath, "DialogueScriptsTrader.json"),
            Path.Combine(Application.streamingAssetsPath, "DialogueScriptsBlacksmith.json"),
            Path.Combine(Application.streamingAssetsPath, "DialogueScriptsWarior.json")
            // Add more file paths as needed
        };

        List<DialogueScript> dialogueScripts = new List<DialogueScript>();

        foreach (string filePath in filePaths)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    List<DialogueScript> scripts = JsonConvert.DeserializeObject<List<DialogueScript>>(json);
                    if (scripts != null)
                    {
                        dialogueScripts.AddRange(scripts);
                    }
                    else
                    {
                        Debug.LogError($"Failed to parse dialogue scripts from {filePath} or no scripts found.");
                    }
                }
                catch (JsonException e)
                {
                    Debug.LogError($"Failed to deserialize JSON data from {filePath}: {e.Message}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"General error: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"{Path.GetFileName(filePath)} not found in StreamingAssets!");
            }
        }

        if (dialogueScripts.Count > 0)
        {
            PermaLists.Instance.DialogueScripts = dialogueScripts;
            Debug.Log("Dialogue data loaded successfully.");
        }
        else
        {
            Debug.LogError("No dialogue data loaded. Check the JSON files.");
        }
    }
}

[System.Serializable]
public class DialogueScript
{
    public List<string> Roles { get; set; }
    public List<PersonalityDialogue> Personalities { get; set; }
}

[System.Serializable]
public class PersonalityDialogue
{
    public string Personality { get; set; }
    public DialogueLines Dialogue { get; set; }
}

[System.Serializable]
public class DialogueLines
{
    public IntroductionDialogue Introduction { get; set; }
    public TradeDialogue Trade { get; set; }
    public NeedAnythingDialogue NeedAnything { get; set; }
    public NewsDialogue News { get; set; }
    public CraftingDialogue Crafting { get; set; }
}

[System.Serializable]
public class IntroductionDialogue
{
    public List<string> FirstTime { get; set; }
    public List<string> HasMetPlayer { get; set; }
}

[System.Serializable]
public class TradeDialogue
{
    public List<string> Yes { get; set; }
    public List<string> No { get; set; }
}

[System.Serializable]
public class NeedAnythingDialogue
{
    public List<string> Yes { get; set; }
    public List<string> No { get; set; }
}

[System.Serializable]
public class NewsDialogue
{
    public List<string> Yes { get; set; }
    public List<string> No { get; set; }
}

[System.Serializable]
public class CraftingDialogue
{
    public List<string> Yes {get; set; }
    public List<string> No { get; set; }
}