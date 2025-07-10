using System.Collections.Generic;
using UnityEngine;

public class NestedAreaManager : MonoBehaviour
{
    #region Singleton
    private static NestedAreaManager instance;

    public static NestedAreaManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<NestedAreaManager>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("NestedAreaManager");
                    instance = obj.AddComponent<NestedAreaManager>();
                }
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
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    #endregion

    public NPCManager npcManager;
    public AnimalManager animalManager;

    /*
    public void PlaceObjectsInNestedArea(INestedArea nestedArea)
    {
        foreach (var obj in nestedArea.GetAllObjectsInArea())
        {
            Cell cell = nestedArea.GetCellAtPosition(obj.Position); // Assuming obj.Position gives the position
            if (cell != null)
            {
                cell.Objects.Add(obj);
                Debug.Log($"Placed object {obj.Name} at {obj.Position}");
            }
        }
    }

    public void PlaceAnimalsInNestedArea(INestedArea nestedArea)
    {
        foreach (var animal in nestedArea.GetAllAnimalsInArea())
        {
            Cell cell = nestedArea.GetCellAtPosition(animal.Position); // Assuming animal.Position gives the position
            if (cell != null)
            {
                cell.Animals.Add(animal);
                animalManager.PlaceAnimal(nestedArea, animal);
                Debug.Log($"Placed animal {animal.Name} at {animal.Position}");
            }
        }
    }

    public void PlaceNPCsInNestedArea(INestedArea nestedArea)
    {
        foreach (var npc in nestedArea.GetAllNPCsInArea())
        {
            Cell cell = nestedArea.GetCellAtPosition(npc.Position); // Assuming npc.Position gives the position
            if (cell != null)
            {
                npcManager.PlaceNPC(nestedArea, npc);
                Debug.Log($"Placed NPC {npc.Name} at {npc.Position}");
            }
        }
    }
    */
}
