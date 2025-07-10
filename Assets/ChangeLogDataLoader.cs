using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;

public class ChangeLogDataLoader : MonoBehaviour, IDataLoader
{
    public static List<ChangelogData> changelogData;

    public void LoadData()
    {
        LoadChangelogFromJson();
        DebugLogChangelog();
    }

    private void LoadChangelogFromJson()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "ChangeLog", "ChangeLog.json");

        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            ParseChangelogData(json);
            Debug.Log($"Loaded ChangeLog.json from: {path}");
        }
        else
        {
            Debug.LogError($"ChangeLog.json not found at: {path}");
        }
    }

    private void ParseChangelogData(string json)
    {
        try
        {
            changelogData = JsonConvert.DeserializeObject<List<ChangelogData>>(json);
            Debug.Log("Changelog loaded successfully.");
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

    private void DebugLogChangelog()
    {
        if (changelogData == null || changelogData.Count == 0)
        {
            Debug.Log("Changelog is empty or not loaded.");
            return;
        }

        foreach (var changelog in changelogData)
        {
            Debug.Log($"Title: {changelog.title} (Version {changelog.version})");

            foreach (var section in changelog.sections)
            {
                Debug.Log($"  Section: {section.subtitle}");

                foreach (var subsection in section.subsections)
                {
                    Debug.Log($"    Subsection: {subsection.subtitle}");

                    foreach (var change in subsection.changes)
                    {
                        Debug.Log($"      - {change}");
                    }
                }
            }
        }
    }
}

[System.Serializable]
public class ChangelogData
{
    public string title;
    public string version;
    public string date;
    public List<ChangelogSection> sections; 
}

[System.Serializable]
public class ChangelogSection
{
    public string subtitle;
    public List<ChangelogSubsection> subsections;
}

[System.Serializable]
public class ChangelogSubsection
{
    public string subtitle;
    public List<string> changes;
}
