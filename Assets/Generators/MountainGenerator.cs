using UnityEngine;

public class MountainGenerator : MonoBehaviour
{
    public MapGenerator mapGenerator;
    public int mountainLength = 10; // Length of the mountain range
    public int maxMountainWidth = 5; // Maximum width of the mountain range

    public void GenerateMountains()
    {
        if (mapGenerator == null || mapGenerator.map == null)
        {
            Debug.LogError("MapGenerator reference not set or map not generated.");
            return;
        }

        Vector2Int mountainStartPoint = new Vector2Int(Random.Range(0, mapGenerator.width), Random.Range(0, mapGenerator.height));
        Vector2Int currentPoint = mountainStartPoint;
        Vector2Int lastDirection = Vector2Int.up; // Initial direction for the mountain range to grow

        for (int i = 0; i < mountainLength; i++)
        {
            if (!IsValidPosition(currentPoint))
            {
                break; // Stop if the mountain range would go out of bounds
            }

            CreateMountainSection(currentPoint, Random.Range(1, maxMountainWidth + 1)); // Create a section of the mountain

            // Decide the next direction of the mountain range
            Vector2Int directionChange = GetRandomDirectionChange();
            lastDirection += directionChange;

            // Ensure the direction change keeps the mountain range within bounds
            if (!IsValidPosition(currentPoint + lastDirection))
            {
                lastDirection -= directionChange; // Revert if the new direction would go out of bounds
            }

            currentPoint += lastDirection;
        }

        Debug.Log("Mountains generated.");
    }

    private void CreateMountainSection(Vector2Int center, int width)
    {
        for (int x = center.x - width; x <= center.x + width; x++)
        {
            for (int y = center.y - width; y <= center.y + width; y++)
            {
                Vector2Int currentPoint = new Vector2Int(x, y);
                if (IsValidPosition(currentPoint))
                {
                    // Simple distance check for a somewhat circular mountain base
                    if (Vector2Int.Distance(center, currentPoint) <= width)
                    {
                        mapGenerator.map[x, y].Terrain = TerrainType.Mountain;
                    }
                }
            }
        }
    }

    private Vector2Int GetRandomDirectionChange()
    {
        Vector2Int[] possibleChanges = new Vector2Int[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        return possibleChanges[Random.Range(0, possibleChanges.Length)];
    }

    private bool IsValidPosition(Vector2Int position)
    {
        return position.x >= 0 && position.x < mapGenerator.width && position.y >= 0 && position.y < mapGenerator.height;
    }
}
