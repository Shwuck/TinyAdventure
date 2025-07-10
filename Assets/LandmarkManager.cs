using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

public class LandmarkManager : MonoBehaviour
{
    // Singleton instance
    private static LandmarkManager _instance;

    public static LandmarkManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<LandmarkManager>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("LandmarkManager");
                    _instance = obj.AddComponent<LandmarkManager>();
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
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Initialize the random number generator with the game seed
        int gameSeed = GameManager.Instance.GameSeed;
        random = new System.Random(gameSeed);
    }

    private System.Random random;

    public void GenerateLandmark(TerrainType terrainType)
    {
        List<LandmarkCreationData> landmarksForTerrain = GetLandmarksForTerrain(terrainType);
        if (landmarksForTerrain.Count > 0)
        {
            int randomIndex = random.Next(landmarksForTerrain.Count);
            var selectedLandmark = landmarksForTerrain[randomIndex];
            Debug.Log($"Landmark created: {selectedLandmark.LandmarkName} in {terrainType}");
        }
        else
        {
            Debug.Log($"No landmarks available for terrain type: {terrainType}");
        }
    }

    private List<LandmarkCreationData> GetLandmarksForTerrain(TerrainType terrainType)
    {
        return PermaLists.Instance.LandmarkCreationData.FindAll(landmark => landmark.ApplicableTerrains.Contains(terrainType));
    }
}