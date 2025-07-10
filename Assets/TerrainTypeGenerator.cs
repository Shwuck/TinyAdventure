using UnityEngine;

public class TerrainTypeGenerator : MonoBehaviour
{
    public int width = 256;
    public int height = 256;
    public float scale = 20f;

    // Define your terrain types using a simple enumeration
    enum TerrainType { Water, Grassland, Mountain }

    void Start()
    {
        GenerateTerrainTypes();
    }

    void GenerateTerrainTypes()
    {
        TerrainType[,] terrainMap = new TerrainType[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float xCoord = (float)x / width * scale;
                float yCoord = (float)y / height * scale;
                float sample = Mathf.PerlinNoise(xCoord, yCoord);

                // Assign terrain types based on the noise value
                if (sample < 0.2)
                {
                    terrainMap[x, y] = TerrainType.Water;
                }
                else if (sample < 0.5)
                {
                    terrainMap[x, y] = TerrainType.Grassland;
                }
                else
                {
                    terrainMap[x, y] = TerrainType.Mountain;
                }
            }
        }

        // You can now use terrainMap to visualize or otherwise utilize the assigned terrain types in your game
    }
}