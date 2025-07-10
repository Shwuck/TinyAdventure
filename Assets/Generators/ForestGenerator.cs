using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForestGenerator : MonoBehaviour
{
    public int forestSize = 25; // The radius of the forest
    public float densityFactor = 0.12f; // Adjust for denser or sparser forests
    public int randomNumber;

    public void GenerateForest()
    {
        if (MapGenerator.Instance == null || MapGenerator.Instance.map == null)
        {
            Debug.LogError("MapGenerator reference not set or map not generated.");
            return;
        }

        randomNumber = GameManager.Instance.GameSeed + MapGenerator.Instance.forestsGenerated;

        // Use the same seed to ensure consistency
        Random.InitState(randomNumber);

        Vector2Int forestStartPoint = new Vector2Int(Random.Range(0, MapGenerator.Instance.width), Random.Range(0, MapGenerator.Instance.height));
        GenerateCircularForest(forestStartPoint, forestSize, densityFactor);
        MapGenerator.Instance.forestsGenerated++;
    }

    private void GenerateCircularForest(Vector2Int center, int size, float densityFactor)
    {
        // Use the same seed to ensure consistency
        Random.InitState(randomNumber);

        int adjustedForestSize = Mathf.Max(1, size + Random.Range(-2, 3)); // Simple variation in size

        for (int x = center.x - adjustedForestSize; x <= center.x + adjustedForestSize; x++)
        {
            for (int y = center.y - adjustedForestSize; y <= center.y + adjustedForestSize; y++)
            {
                Vector2Int currentPoint = new Vector2Int(x, y);
                if (IsValidPosition(currentPoint))
                {
                    float distance = Vector2Int.Distance(center, currentPoint);
                    if (distance <= adjustedForestSize)
                    {
                        float noise = Mathf.PerlinNoise(x * densityFactor, y * densityFactor);
                        if (noise > 0.4) // Threshold for tree placement
                        {
                            MapGenerator.Instance.map[x, y].Terrain = TerrainType.Forest;
                            MapGenerator.Instance.map[x, y].TerrainToDisplay = GetTerrainToDisplay();
                        }
                    }
                }
            }
        }
    }

    private bool IsValidPosition(Vector2Int position)
    {
        // Check if the position is within the bounds of the map
        if (position.x >= 0 && position.x < MapGenerator.Instance.width && position.y >= 0 && position.y < MapGenerator.Instance.height)
        {
            // Get the cell from the map
            Cell cell = MapGenerator.Instance.map[position.x, position.y];

            // Check if the cell's terrain is neither Mountain nor Water
            return cell.Terrain != TerrainType.Mountain && cell.Terrain != TerrainType.Water && cell.Terrain != TerrainType.MountainPeak;
        }
        return false;
    }

    // Method to randomly select a terrain type for display with altered probabilities
    private TerrainType GetTerrainToDisplay()
    {
        int roll = Random.Range(1, 101); // Random number between 1 and 100 (inclusive)

        if (roll <= 50) 
        {
            return TerrainType.Forest2;
        }
        else if (roll <= 90) 
        {
            return TerrainType.Forest1;
        }
        else 
        {
            return TerrainType.Forest3;
        }
    }

    public void GenerateForests(int numberOfForests, int clusterSize)
    {
        // Use the same seed to ensure consistency
        Random.InitState(GameManager.Instance.GameSeed);

        forestSize = clusterSize;

        for (int i = 0; i < numberOfForests; i++)
        {
            GenerateForest();
        }
    }
}
