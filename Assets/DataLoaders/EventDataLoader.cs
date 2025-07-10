using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventDataLoader : MonoBehaviour, IDataLoader
{
    private void Start()
    {

    }

    public void LoadData()
    {
        LoadEventDataFromJson();
    }

    public void LoadEventDataFromJson()
    {
        string filePath = System.IO.Path.Combine(Application.streamingAssetsPath, "EventCreationData.json");

        if (System.IO.File.Exists(filePath))
        {
            string json = System.IO.File.ReadAllText(filePath);
            EventCreationDataList eventData = JsonUtility.FromJson<EventCreationDataList>(json);

            // Assign the loaded data to a global manager or singleton instance
            PermaLists.Instance.EventCreationData = eventData.Events;

            Debug.Log("Event creation data loaded successfully.");
        }
        else
        {
            Debug.LogError("EventCreationData.json not found in StreamingAssets!");
        }
    }
}

[System.Serializable]
public class EventCreationData
{
    public string EventName { get; set; }
    public EventType EventType { get; set; }
    public List<TerrainType> ApplicableTerrains { get; set; }
}

[System.Serializable]
public class EventCreationDataList
{
    public List<EventCreationData> Events;
}

public enum EventType
{
    Minor,
    Medium,
    Major
}