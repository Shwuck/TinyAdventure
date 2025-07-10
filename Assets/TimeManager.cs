using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    private static TimeManager instance;
    public static TimeManager Instance => instance;

    public int currentDay = 1;
    public int currentWeek = 1;
    public int currentYear = 1;
    public string[] daysOfWeek = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
    public int currentDayIndex = 0;
    public int TotalDaysPassed = 0;

    public enum TimeSegment { Morning, Afternoon, Evening, Night }
    public TimeSegment currentSegment = TimeSegment.Morning;

    // New clock variables
    public int currentHour = 5; // Starting at 5 AM
    public int currentMinute = 0;

    public Season currentSeason = Season.Spring;

    public delegate void TurnChangedDelegate(int currentDay, int currentWeek, Season currentSeason, int currentYear, int currentHour, int currentMinute);
    public event TurnChangedDelegate OnTurnChanged;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            Debug.Log("TimeManager: Instance initialized.");
        }
        else
        {
            Destroy(gameObject);
            Debug.LogError("TimeManager: Another instance already exists. Destroying this instance.");
        }

        TotalDaysPassed = (currentYear - 1) * 4 * 28 + (GetSeasonIndex(currentSeason) * 28) + (currentWeek - 1) * 7 + currentDay;
        Debug.Log("TimeManager: TotalDaysPassed initialized to " + TotalDaysPassed);
    }

    private int GetSeasonIndex(Season season)
    {
        switch (season)
        {
            case Season.Spring: return 0;
            case Season.Summer: return 1;
            case Season.Autumn: return 2;
            case Season.Winter: return 3;
            default: return 0;
        }
    }

    public void InitializeGame()
    {
        Debug.Log("TimeManager: Initializing game.");
        // Initialization code
    }

    public void AdvanceOneHour()
    {
        Debug.Log("TimeManager: Advancing one hour. Current hour: " + currentHour);
        // Add 1 hour
        currentHour++;
        if (TimedEvents.Instance != null)
        {
            TimedEvents.Instance.OnHourPass();
        }
        else
        {
            Debug.LogError("TimeManager: TimedEvents.Instance is null.");
        }

        if (currentHour >= 24)
        {
            currentHour = 0;
            AdvanceDay();
        }
        UpdateTimeSegment();
        OnTurnChanged?.Invoke(currentDay, currentWeek, currentSeason, currentYear, currentHour, currentMinute);
        Debug.Log("TimeManager: Advanced one hour. New hour: " + currentHour);
    }

    public void AdvanceThirtyMinutes()
    {
        Debug.Log("TimeManager: Advancing thirty minutes. Current minute: " + currentMinute);
        // Add 30 minutes
        currentMinute += 30;
        if (currentMinute >= 60)
        {
            currentMinute = 0;
            AdvanceOneHour(); // Automatically updates time segment and triggers OnTurnChanged
        }
        else
        {
            UpdateTimeSegment();
            OnTurnChanged?.Invoke(currentDay, currentWeek, currentSeason, currentYear, currentHour, currentMinute);
        }
        Debug.Log("TimeManager: Advanced thirty minutes. New minute: " + currentMinute);
    }

    private void UpdateTimeSegment()
    {
        Debug.Log("TimeManager: Updating time segment. Current hour: " + currentHour);

        TimeSegment previousSegment = currentSegment; // Store the previous segment for comparison

        // Update the time segment based on currentHour
        if (currentHour >= 5 && currentHour < 11)
            currentSegment = TimeSegment.Morning;
        else if (currentHour >= 11 && currentHour < 17)
            currentSegment = TimeSegment.Afternoon;
        else if (currentHour >= 17 && currentHour < 23)
            currentSegment = TimeSegment.Evening;
        else
            currentSegment = TimeSegment.Night;

        Debug.Log("TimeManager: Updated time segment to " + currentSegment);

        // Trigger the OnSegmentPass event if the segment has changed
        if (currentSegment != previousSegment && TimedEvents.Instance != null)
        {
            TimedEvents.Instance.OnSegmentPass();
        }
    }

    private void AdvanceDay()
    {
        Debug.Log("TimeManager: Advancing day. Current day: " + currentDay);
        // Advance the day, week, season, and year as necessary
        currentDay++;
        currentDayIndex++;
        TotalDaysPassed++; // Update total days passed
        if (TimedEvents.Instance != null)
        {
            TimedEvents.Instance.OnDayPass();
        }
        else
        {
            Debug.LogError("TimeManager: TimedEvents.Instance is null.");
        }

        if (currentDayIndex >= daysOfWeek.Length)
        {
            currentDayIndex = 0;
            currentWeek++;
            HandleEndOfWeek(); // Call at the end of the week
        }
        if (currentDay > 28)
        {
            currentDay = 1;
            currentWeek = 1;
            HandleEndOfSeason(); // Call at the end of the season
            currentSeason += 1;
            if ((int)currentSeason > 3) // After Winter, reset to Spring
            {
                currentSeason = Season.Spring;
                currentYear++;
                HandleEndOfYear(); // Call at the end of the year
            }
        }
        HandleEndOfDay(); // Call at the end of the day
        Debug.Log("TimeManager: Advanced day. New day: " + currentDay);
    }

    private void HandleEndOfDay()
    {
        Debug.Log("TimeManager: End of the day");
        // Add your logic for the end of the day here
    }

    private void HandleEndOfWeek()
    {
        Debug.Log("TimeManager: End of the week");
        if (EndOfWeekManager.Instance != null)
        {
            EndOfWeekManager.Instance.UpdateVillages();
        }
        else
        {
            Debug.LogError("TimeManager: EndOfWeekManager.Instance is null.");
        }

        if (TimedEvents.Instance != null)
        {
            TimedEvents.Instance.OnWeekPass();
        }
        else
        {
            Debug.LogError("TimeManager: TimedEvents.Instance is null.");
        }
    }

    private void HandleEndOfSeason()
    {
        Debug.Log("TimeManager: End of the season");
        if (TimedEvents.Instance != null)
        {
            TimedEvents.Instance.OnSeasonPass();
        }
        else
        {
            Debug.LogError("TimeManager: TimedEvents.Instance is null.");
        }
        // Add your logic for the end of the season here
    }

    private void HandleEndOfYear()
    {
        Debug.Log("TimeManager: End of the year");
        if (TimedEvents.Instance != null)
        {
            TimedEvents.Instance.OnYearPass();
        }
        else
        {
            Debug.LogError("TimeManager: TimedEvents.Instance is null.");
        }
        // Add your logic for the end of the year here
    }

    public void PassTime(int hours, int minutes)
    {
        Debug.Log("TimeManager: Passing time. Hours: " + hours + ", Minutes: " + minutes);
        // Advance hours
        for (int i = 0; i < hours; i++)
        {
            AdvanceOneHour();
        }

        // Advance minutes in a more granular way
        currentMinute += minutes;
        while (currentMinute >= 60)
        {
            currentMinute -= 60; // Subtract 60 minutes for each hour overflow
            AdvanceOneHour(); // This will also handle day, month, and year progression if needed
        }

        UpdateTimeSegment();
        OnTurnChanged?.Invoke(currentDay, currentWeek, currentSeason, currentYear, currentHour, currentMinute);
        Debug.Log("TimeManager: Passed time. Current time: " + GetCurrentTimeFormatted());
    }

    public void NextTurn()
    {
        Debug.Log("TimeManager: Advancing to the next turn.");
        // Advance the clock by 6 hours
        for (int i = 0; i < 6; i++)
        {
            AdvanceOneHour();
        }
        Debug.Log("TimeManager: Advanced to the next turn.");
    }

    public void NextNestedTurn()
    {
        Debug.Log("TimeManager: Advancing to the next nested turn.");
        // Advance the clock by 30 minutes
        for (int i = 0; i < 1; i++)
        {
            AdvanceThirtyMinutes();
        }
        Debug.Log("TimeManager: Advanced to the next nested turn.");
    }

    public string GetCurrentDayOfWeek()
    {
        return daysOfWeek[currentDayIndex];
    }

    public string GetCurrentTimeSegment()
    {
        return currentSegment.ToString();
    }

    public string GetCurrentSeason()
    {
        return currentSeason.ToString();
    }

    public int GetCurrentYear()
    {
        return currentYear;
    }

    public string GetCurrentDateFormatted()
    {
        // Helper array for ordinal suffixes
        string[] ordinalSuffixes = { "th", "st", "nd", "rd" };
        int day = currentDay;
        int mod100 = day % 100;

        // Determine the correct ordinal suffix
        string suffix = (mod100 - 20) % 10 > 2 ? "th" : ordinalSuffixes[Mathf.Min(mod100 % 10, 3)];
        if (mod100 >= 11 && mod100 <= 13)
        {
            suffix = "th";
        }

        // Format the current date string
        string formattedDate = $"{daysOfWeek[currentDayIndex]} {day}{suffix} of {currentSeason}";

        return formattedDate;
    }

    public string GetCurrentTimeFormatted()
    {
        // Ensure hours and minutes are always two digits
        string displayHour = currentHour.ToString().PadLeft(2, '0');
        string displayMinute = currentMinute.ToString().PadLeft(2, '0');

        // Combine into a formatted time string in 24-hour format
        string formattedTime = $"{displayHour}:{displayMinute}";

        return formattedTime;
    }
}

public enum Season { Spring, Summer, Autumn, Winter }
