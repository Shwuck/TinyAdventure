using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimedEvents : MonoBehaviour
{
    public static TimedEvents Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameDebugger.Instance.LogInfo("TimedEvents: Instance initialized.");
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            GameDebugger.Instance.LogWarning("TimedEvents: Duplicate instance found. Destroying this instance.");
        }
    }

    void Start()
    {
        GameDebugger.Instance.LogInfo("TimedEvents: Start called.");
    }

    public void OnGameStart()
    {

        GameManager.Instance.GameStarted = true;
        PlayerStats.Instance.ValidatePlayerCharacter();
        CivilisationManager.Instance.UpdateAllVillages();
        NPCManager.Instance.UpdateNeedsForNPCs();
        CivilisationManager.Instance.UpdateNewsForAllVillages();
        CampGenerator.Instance.GenerateNPCsForAllCamps();
        CampGenerator.Instance.GenerateAnimalsForAllCamps();
    }

    public void OnHourPass()
    {
        GameDebugger.Instance.LogInfo("TimedEvents: OnHourPass called.");
        PlayerStats.Instance.IncreaseHunger(2);
        PlayerStats.Instance.AddHoursAlive();
    }

    public void OnSegmentPass()
    {
        GameDebugger.Instance.LogInfo("TimedEvents: OnSegmentPass called.");
        WeatherManager.Instance.MoveWeather();
    }


    public void OnDayPass()
    {
        GameDebugger.Instance.LogInfo("TimedEvents: OnDayPass called.");
    }

    public void OnWeekPass()
    {
        GameDebugger.Instance.LogInfo("TimedEvents: OnWeekPass called.");
        FertilityManager.Instance.AdjustFertilityWeekly();
        EventManager.Instance.CheckMinorEvent();
        UpdateCivilisations();
        NPCManager.Instance.UpdateNeedsForNPCs();
        CivilisationManager.Instance.UpdateNewsForAllVillages();
    }

    public void OnSeasonPass()
    {
        GameDebugger.Instance.LogInfo("TimedEvents: OnSeasonPass called.");
        EventManager.Instance.CheckMediumEvent();
    }

    public void OnYearPass()
    {
        GameDebugger.Instance.LogInfo("TimedEvents: OnYearPass called.");
        FertilityManager.Instance.AdjustForestGrowth();
        EventManager.Instance.CheckMajorEvent();
    }

    private void UpdateCivilisations()
    {
        GameDebugger.Instance.LogInfo("UpdateCivilisations: Updating all villages.");
        CivilisationManager.Instance.UpdateAllVillages();
    }

    public void PassTime(int hours, int minutes)
    {
        // Handle time passage
        for (int i = 0; i < hours; i++)
        {
            OnHourPass();
            if ((i + 1) % 24 == 0)
            {
                OnDayPass();
            }
            if ((i + 1) % (24 * 7) == 0)
            {
                OnWeekPass();
            }
            if ((i + 1) % (24 * 30) == 0) // Assuming 30 days per month/season
            {
                OnSeasonPass();
            }
            if ((i + 1) % (24 * 365) == 0) // Assuming 365 days per year
            {
                OnYearPass();
            }
        }

    }
}
