using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections;

public class PlayerProgress : MonoBehaviour
{
    public static PlayerProgress Instance;
    public Dictionary<string, bool> RaceUnlockStatus = new Dictionary<string, bool>();
    public Dictionary<string, bool> SubRaceUnlockStatus = new Dictionary<string, bool>();
    public Dictionary<string, bool> BackgroundUnlockStatus = new Dictionary<string, bool>();

    private void Awake()
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

    private void InitializeUnlocks()
    {
        LoadProgress();
    }

    public void UnlockRace(string raceName)
    {
        if (RaceUnlockStatus.ContainsKey(raceName))
        {
            RaceUnlockStatus[raceName] = true;
            SaveProgress();
        }
    }

    public void UnlockSubRace(string subRaceName)
    {
        if (SubRaceUnlockStatus.ContainsKey(subRaceName))
        {
            SubRaceUnlockStatus[subRaceName] = true;
            SaveProgress();
        }
    }

    public void UnlockBackground(string backgroundName)
    {
        if (BackgroundUnlockStatus.ContainsKey(backgroundName))
        {
            BackgroundUnlockStatus[backgroundName] = true;
            SaveProgress();
        }
    }

    public bool IsRaceUnlocked(string raceName)
    {
        return RaceUnlockStatus.ContainsKey(raceName) && RaceUnlockStatus[raceName];
    }

    public bool IsSubRaceUnlocked(string subRaceName)
    {
        return SubRaceUnlockStatus.ContainsKey(subRaceName) && SubRaceUnlockStatus[subRaceName];
    }

    public bool IsBackgroundUnlocked(string backgroundName)
    {
        return BackgroundUnlockStatus.ContainsKey(backgroundName) && BackgroundUnlockStatus[backgroundName];
    }

    private void SaveProgress()
    {
        string filePath = Path.Combine(Application.persistentDataPath, "playerProgress.json");
        string json = JsonConvert.SerializeObject(this);
        File.WriteAllText(filePath, json);
    }

    private void LoadProgress()
    {
        string filePath = Path.Combine(Application.persistentDataPath, "playerProgress.json");

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            JsonConvert.PopulateObject(json, this);
        }
        else
        {
            // Initialize with default values if no save file exists
            foreach (var race in PermaLists.Instance.Races)
            {
                RaceUnlockStatus[race.Name] = race.IsUnlocked;
            }

            foreach (var subRace in PermaLists.Instance.Races.SelectMany(r => r.SubRaces))
            {
                SubRaceUnlockStatus[subRace.Name] = subRace.IsUnlocked;
            }

            foreach (var background in PermaLists.Instance.Backgrounds)
            {
                BackgroundUnlockStatus[background.Name] = background.IsUnlocked;
            }
        }
    }
}
