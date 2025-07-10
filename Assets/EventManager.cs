using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    // Singleton instance
    private static EventManager _instance;

    public static EventManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<EventManager>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("EventManager");
                    _instance = obj.AddComponent<EventManager>();
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeRandomSeed();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    // Event chances
    private int minorEventChance = 0;
    private int mediumEventChance = 0;
    private int majorEventChance = 0;

    // Method to determine if a minor event occurs
    public void CheckMinorEvent()
    {
        minorEventChance++;
        if (RollForEvent(minorEventChance))
        {
            Debug.Log("Minor event happens");
            minorEventChance = 0;
        }
    }

    // Method to determine if a medium event occurs
    public void CheckMediumEvent()
    {
        mediumEventChance++;
        if (RollForEvent(mediumEventChance))
        {
            Debug.Log("Medium event happens");
            mediumEventChance = 0;
        }
    }

    // Method to determine if a major event occurs
    public void CheckMajorEvent()
    {
        majorEventChance++;
        if (RollForEvent(majorEventChance))
        {
            Debug.Log("Major event happens");
            majorEventChance = 0;
        }
    }

    // Helper method to roll for an event
    private bool RollForEvent(int chance)
    {
        int roll = Random.Range(0, 101);
        return roll < chance;
    }

    // Method to initialize the random seed from GameManager
    private void InitializeRandomSeed()
    {
        if (GameManager.Instance != null)
        {
            int gameSeed = GameManager.Instance.GameSeed;
            Random.InitState(gameSeed);
        }
        else
        {
            Debug.LogWarning("EventManager: GameManager instance not found. Using default random seed.");
        }
    }
}
