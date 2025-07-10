using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BanditGroupGenerator : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public NPCManager npcManager;
    public NPCGenerator npcGenerator; // Reference to the NPCGenerator

    public int minBanditsPerGroup = 3;
    public int maxBanditsPerGroup = 6;

    public void GenerateBanditGroup()
    {
        if (mapGenerator == null || mapGenerator.map == null || npcManager == null)
        {
            Debug.LogError("MapGenerator or NPCManager reference not set or map not generated.");
            return;
        }

        Vector2Int banditGroupLocation = GetRandomPassableLocation();
        if (banditGroupLocation != Vector2Int.zero)
        {
            // Correctly instantiate a BanditGroup instead of a generic NPCGroup
            BanditGroup banditGroup = new BanditGroup(mapGenerator);
            banditGroup.Position = banditGroupLocation; // Set the group's position

            int numberOfBandits = Random.Range(minBanditsPerGroup, maxBanditsPerGroup + 1);
            for (int i = 0; i < numberOfBandits; i++)
            {

                // Generate a bandit using the NPCGenerator
           //     npcGenerator.GenerateNPC<Bandit>();
                // Here, ensure the bandit's name is properly set if needed
                 Bandit bandit = new Bandit();
                 banditGroup.AddNPC(bandit);
            }

            // Register the bandit group with the NPCManager at the generated location
            npcManager.RegisterNPCGroup(banditGroup, banditGroupLocation);
            mapGenerator.map[banditGroupLocation.x, banditGroupLocation.y].isNPCGroupPresent = true;

            Debug.Log($"Bandit group generated at {banditGroupLocation} with {numberOfBandits} bandits.");
        }
    }


    private Vector2Int GetRandomPassableLocation()
    {
        List<Vector2Int> potentialLocations = new List<Vector2Int>();

        for (int x = 0; x < mapGenerator.width; x++)
        {
            for (int y = 0; y < mapGenerator.height; y++)
            {
                Cell cell = mapGenerator.map[x, y];
                if (cell.isPassable && !cell.hasNestedArea && cell.Terrain != TerrainType.River)
                {
                    potentialLocations.Add(new Vector2Int(x, y));
                }
            }
        }

        if (potentialLocations.Count > 0)
        {
            int randomIndex = Random.Range(0, potentialLocations.Count);
            return potentialLocations[randomIndex];
        }

        return Vector2Int.zero; // Return an invalid position if no suitable location is found
    }
}
