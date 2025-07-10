using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;


public class DungeonDataLoader : MonoBehaviour
{
    private void Start()
    {
        LoadData();
    }

    public void LoadData()
    {
        LoadDungeonCreationDataFromJson();
    }

    public void LoadDungeonCreationDataFromJson()
    {
        return;
    }



}


[System.Serializable]
public class DungeonCreationData
{
    public int DungeonMainMapCell { get; set; }
    public int DungeonID { get; set; }
    public int DungeonCellID { get; set; }
    public int DungeonEntranceCellID { get; set; }
    public string DungeonType { get; set; }
    public int TotalDungeonLevels { get; set; }

    public DungeonCreationData(int dungeonID, int totalDungeonLevels)
    {
        DungeonID = dungeonID;
        TotalDungeonLevels = totalDungeonLevels;
    }
}

[System.Serializable]
public class CaveCreationData
{
    public int CaveMainMapCell { get; set; }
    public int CaveID { get; set; }
    public int CaveCellID { get; set; }
    public int CaveEntranceCellID { get; set; }
    public CaveType CaveType { get; set; }

    public CaveCreationData(int caveID)
    {
        CaveID = caveID;
    }
}


[System.Serializable]
public class CampCreationData
{
    public int CampID { get; set; }               // Unique ID for the camp
    public int CampCellID { get; set; }            // Cell where the camp is located
    public int CampEntranceCellID { get; set; }    // Entrance cell of the camp, if applicable
    public CampType CampType { get; set; }         // Type of camp (Bandit, Trader, etc.)

    // Constructor to initialize the camp creation data
    public CampCreationData(int campID)
    {
        CampID = campID;
    }
}
