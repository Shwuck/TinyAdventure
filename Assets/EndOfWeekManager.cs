using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndOfWeekManager : MonoBehaviour
{
    private static EndOfWeekManager instance;
    public static EndOfWeekManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("EndOfWeekManager");
                instance = go.AddComponent<EndOfWeekManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void UpdateVillages()
    {
        CivilisationManager.Instance.UpdateAllVillages();
    }
}
