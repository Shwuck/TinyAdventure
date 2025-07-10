using UnityEngine;
using UnityEngine.UI;

public class MapGeneratorUI : MonoBehaviour
{
    // References to other generator scripts
    public MapGenerator mapGenerator;
    public RoadGenerator roadGenerator;
    public ForestGenerator forestGenerator;
    public RiverGenerator riverGenerator;
    public VillageGenerator villageGenerator;
    public BanditGroupGenerator banditGroupGenerator;

    // UI elements
    public GameObject manualRoadPanel;
    public Button generateMapButton;
    public Button generateRoadButton;
    public Button generateForestButton;
    public Button generateRiverButton;
    public Button generateVillageButton;
    public Button generateBanditButton;

    void Start()
    {
        // Error checking for references
        CheckReferences();

        // Assign button click listeners
        generateMapButton?.onClick.AddListener(GenerateMap);
        generateRoadButton?.onClick.AddListener(GenerateRoads);
        generateForestButton?.onClick.AddListener(GenerateForest);
        generateRiverButton?.onClick.AddListener(GenerateRiver);
        generateVillageButton?.onClick.AddListener(GenerateVillage);
        generateBanditButton?.onClick.AddListener(GenerateBandits);
    }

    void CheckReferences()
    {
        if (mapGenerator == null) Debug.LogError("MapGenerator reference not set in MapGeneratorUI.");
        if (roadGenerator == null) Debug.LogError("RoadGenerator reference not set in MapGeneratorUI.");
        if (forestGenerator == null) Debug.LogError("ForestGenerator reference not set in MapGeneratorUI.");
        if (riverGenerator == null) Debug.LogError("RiverGenerator reference not set in MapGeneratorUI.");
        if (generateMapButton == null) Debug.LogError("GenerateMapButton not assigned in MapGeneratorUI.");
        if (generateRoadButton == null) Debug.LogError("GenerateRoadButton not assigned in MapGeneratorUI.");
        if (generateForestButton == null) Debug.LogError("GenerateForestButton not assigned in MapGeneratorUI.");
        if (generateRiverButton == null) Debug.LogError("GenerateRiverButton not assigned in MapGeneratorUI.");
        if (villageGenerator == null) Debug.LogError("VillageGenerator reference not set in MapGeneratorUI.");
        if (banditGroupGenerator == null) Debug.LogError("BanditGroupGenerator reference not set in MapGeneratorUI.");
    }


    public void GenerateMap()
    {
        mapGenerator?.GenerateMap();
        Debug.Log("Map generated.");
    }

    public void GenerateRoads()
    {
        if (mapGenerator != null && mapGenerator.map != null && roadGenerator != null)
        {
            // Assuming you want to generate a number of roads as specified in GameManager
            int numberOfRoads = GameManager.Instance.numberOfRoads;
            for (int i = 0; i < numberOfRoads; i++)
            {
                roadGenerator.StartRoadGeneration(); // Call the method to generate each road
            }
            Debug.Log($"{numberOfRoads} roads generated.");
        }
        else
        {
            Debug.LogError("Map must be generated before generating roads.");
        }
    }

    public void GenerateForest()
    {
        if (mapGenerator != null && mapGenerator.map != null && forestGenerator != null)
        {
            forestGenerator.GenerateForest(); // Call the method to generate the forest
            Debug.Log("Forest generated.");
        }
        else
        {
            Debug.LogError("Map must be generated before generating forests.");
        }
    }

    public void GenerateRiver()
    {
        /* if (mapGenerator != null && mapGenerator.map != null && riverGenerator != null)
         {
             riverGenerator.GenerateRiver();
             Debug.Log("River generated.");
         }
         else
         {
             Debug.LogError("Map must be generated before generating rivers.");
         }

         */

        Debug.Log("Would Generate River");
    }

    public void ToggleManualRoadPanel()
    {
        // Check if the manualRoadPanel is not null to avoid NullReferenceException
        if (manualRoadPanel != null)
        {
            // Toggle the active state of the panel
            manualRoadPanel.SetActive(!manualRoadPanel.activeSelf);
        }
    }

    public void GenerateVillage()
    {
        /*   if (mapGenerator != null && mapGenerator.map != null && villageGenerator != null)
           {
               villageGenerator.PlaceVillage();
               Debug.Log("Village generated.");
           }
           else
           {
               Debug.LogError("Map must be generated before generating villages.");
           }
        */

        Debug.Log("You pressed a button");
    }

    public void GenerateBandits()
    {
        if (mapGenerator != null && mapGenerator.map != null && banditGroupGenerator != null)
        {
            banditGroupGenerator.GenerateBanditGroup(); // Call the method to generate a group of bandits
            Debug.Log("Bandit Group generated.");
        }
        else
        {
            Debug.LogError("Map must be generated before generating bandits.");
        }
    }


}
