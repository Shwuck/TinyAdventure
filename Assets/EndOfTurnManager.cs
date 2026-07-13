using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndOfTurnManager : MonoBehaviour
{
    public static EndOfTurnManager Instance { get; private set; }

    public NPCManager npcManager;
    public MapDisplayUI mapDisplayUI;

    private float remainingTurnTime = 1.0f;
    private int totalNestedTurns = 0;
    private Button waitButton;
    private TMP_Text waitButtonText;
    private string lastWaitButtonLabel = string.Empty;
    private bool? lastWaitButtonInteractable;

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
        GameDebugger.Instance.LogInfo("EndOfTurnManager Awake: Instance set.");
    }

    public void PlayerWaits()
    {
        RefreshWaitButtonPresentation();

        if (PlayerController.Instance != null &&
            PlayerStats.Instance != null &&
            PlayerStats.Instance.IsInNestedArea &&
            GameManager.Instance != null &&
            GameManager.Instance.ActiveTurnManager)
        {
            PlayerController.Instance.HandleWaitOrEndTurn("WaitButton", true);
            return;
        }

        AddTurnProgress(1f); // Waiting takes a full turn
    }

    public void RefreshWaitButtonPresentation()
    {
        EnsureWaitButtonReferences();
        if (waitButton == null)
        {
            return;
        }

        string label = "Wait";
        bool canUse = true;
        string reason = "Default";

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.GetWaitOrEndTurnPresentation(out label, out canUse, out reason);
        }

        bool labelChanged = !string.Equals(lastWaitButtonLabel, label, StringComparison.Ordinal);
        if (waitButtonText != null && labelChanged)
        {
            waitButtonText.text = label;
            lastWaitButtonLabel = label;
        }

        bool interactableChanged = lastWaitButtonInteractable != canUse;
        if (interactableChanged)
        {
            waitButton.interactable = canUse;
            lastWaitButtonInteractable = canUse;
        }

        if (labelChanged || interactableChanged)
        {
            GameDebugger.Instance.LogInfo($"EndOfTurnManager.RefreshWaitButtonPresentation label={label} interactable={canUse} reason={reason}");
        }
    }

    public void AddTurnProgress(float progress)
    {
        remainingTurnTime -= progress;
        while (remainingTurnTime <= 0)
        {
            remainingTurnTime += 1.0f;

            if (PlayerStats.Instance.IsInNestedArea)
            {
                EndNestedTurn();
            }
            else if (PlayerStats.Instance.IsInMainMap)
            {
                EndTurn();
            }
        }

        UIController.Instance.UpdateRemainingTurnTime(remainingTurnTime);
    }

    public void EndTurn()
    {
        remainingTurnTime = 1.0f;
        UpdateMaps();
        // Instead of calling TimedEvents directly, call TimeManager
        TimeManager.Instance.AdvanceOneHour();
        ProcessEndofTurn();
    }

    public void EndNestedTurn()
    {
        remainingTurnTime = 1.0f;
        totalNestedTurns++;
        PlayerStats.Instance.UpdateVisibility();
        mapDisplayUI.UpdateNestedMapDisplay(PlayerStats.Instance.CurrentNestedArea);
        ProcessEndofNestedTurn();
    }

    public void ConvertNestedTurnsToTime()
    {
        int minutesPerTurn = 1;
        int totalMinutes = totalNestedTurns * minutesPerTurn;
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;

        // Instead of calling TimedEvents directly, call TimeManager to pass time
        TimeManager.Instance.PassTime(hours, minutes);

        totalNestedTurns = 0;
    }

    private void UpdateMaps()
    {
        mapDisplayUI.UpdateBothMaps();
    }

    private void ProcessEndofTurn()
    {
        var (hours, minutes) = CalculateTimeToPass();
        // Instead of calling TimedEvents directly, call TimeManager to pass time
        TimeManager.Instance.PassTime(hours, minutes);
    }

    private void ProcessEndofNestedTurn()
    {
        // Nested turn-specific logic
    }

    private (int hours, int minutes) CalculateTimeToPass()
    {
        float speed = PlayerStats.Instance.TravelSpeed;
        if (speed == 0)
        {
            return (0, 0);
        }

        float baseTime = 240;
        float maxReductionFactor = 0.5f;
        float timeAdjustmentFactor = ((speed - 3) / 7) * maxReductionFactor;
        timeAdjustmentFactor = Mathf.Max(0, timeAdjustmentFactor);
        float timeToPass = baseTime * (1 - timeAdjustmentFactor);

        int hours = (int)timeToPass / 60;
        int minutes = (int)timeToPass % 60;

        return (hours, minutes);
    }

    private void EnsureWaitButtonReferences()
    {
        if (waitButton != null && waitButtonText != null)
        {
            return;
        }

        GameObject waitButtonObject = GameObject.Find("WaitButton");
        if (waitButtonObject == null)
        {
            return;
        }

        if (waitButton == null)
        {
            waitButton = waitButtonObject.GetComponent<Button>();
        }

        if (waitButtonText == null)
        {
            waitButtonText = waitButtonObject.GetComponentInChildren<TMP_Text>(true);
        }
    }
}
