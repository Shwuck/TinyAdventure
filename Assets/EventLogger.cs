using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class EventLogger : MonoBehaviour
{
    public static EventLogger Instance { get; private set; }

    public int maxRecentEvents = 20;
    public TextMeshProUGUI eventLogText;

    private List<string> recentEventLogs = new List<string>();
    private List<string> historicEventLogs = new List<string>();

    public string currentEventLog;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddLog(string log)
    {
        recentEventLogs.Add(log);
        historicEventLogs.Add(log);

        if (recentEventLogs.Count > maxRecentEvents)
        {
            recentEventLogs.RemoveRange(0, recentEventLogs.Count - maxRecentEvents);
        }

        RefreshEventLog();
    }

    public void RefreshEventLog()
    {
        eventLogText.text = string.Join("\n", recentEventLogs);
    }

    // New method to update descriptive text
    public void UpdateDescription(string description)
    {
        AddLog(description); 
    }
}
