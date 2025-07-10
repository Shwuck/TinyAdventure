using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class AnimalDataLoader : MonoBehaviour, IDataLoader
{
    private void Start()
    {

    }

    public void LoadData()
    {
        LoadCreationDataFromJson();
        SetDietForAnimals();
        SetMountableForLargeAnimals();
    }

    public void LoadCreationDataFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "AnimalCreationData.json");

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            AnimalCreationDataList animalData = JsonConvert.DeserializeObject<AnimalCreationDataList>(json);

            // Assign the loaded data to a global manager or singleton instance
            PermaLists.Instance.AnimalCreationData = animalData.Animals;

            Debug.Log("Animal creation data loaded successfully.");

            foreach (var animal in PermaLists.Instance.AnimalCreationData)
            {
                // Change symbol to lowercase for Tiny or Small animals
                if (animal.Size == AnimalSize.Tiny || animal.Size == AnimalSize.Small)
                {
                    animal.Symbol = char.ToLower(animal.Symbol);
                }

                Debug.Log($"Animal Name: {animal.AnimalName}");
                Debug.Log($"Symbol: {animal.Symbol}");
                Debug.Log($"Color: {animal.Color}");
                Debug.Log($"Is Predator: {animal.IsPredator}");
                Debug.Log($"Size: {animal.Size}");
                Debug.Log($"TerrainRarities Count: {animal.TerrainRarities?.Count ?? 0}");
                if (animal.TerrainRarities != null)
                {
                    foreach (var terrainRarity in animal.TerrainRarities)
                    {
                        Debug.Log($"Terrain: {terrainRarity.Terrain}, Rarity: {terrainRarity.Rarity}");
                    }
                }
                Debug.Log($"CommonColours Count: {animal.CommonColours?.Count ?? 0}");
                if (animal.CommonColours != null)
                {
                    foreach (var color in animal.CommonColours)
                    {
                        Debug.Log($"Colour: {color}");
                    }
                }
            }
        }
        else
        {
            Debug.LogError("AnimalCreationData.json not found in StreamingAssets!");
        }
    }

    private void SetDietForAnimals()
    {
        foreach (var animal in PermaLists.Instance.AnimalCreationData)
        {
            if (animal.Diet == Diet.Unknown)
            {
                animal.Diet = animal.IsPredator ? Diet.Carnivore : Diet.Herbivore;
                Debug.Log($"Set Diet for {animal.AnimalName} to {animal.Diet}");
            }
        }
    }

    private void SetMountableForLargeAnimals()
    {
        foreach (var animal in PermaLists.Instance.AnimalCreationData)
        {
            if (animal.Size == AnimalSize.Medium || animal.Size == AnimalSize.Large || animal.Size == AnimalSize.Huge)
            {
                animal.IsMountable = true;
                Debug.Log($"Set IsMountable for {animal.AnimalName} to true");
            }
        }
    }


}

[System.Serializable]
public class AnimalCreationDataList
{
    public List<AnimalCreationData> Animals;
}

[System.Serializable]
public class AnimalCreationData
{
    public string AnimalName;
    public char Symbol;
    public string Color;
    public bool IsPredator;
    public Diet Diet;
    public AnimalSize Size;
    public string BodyType;
    public List<TerrainRarity> TerrainRarities;
    public List<string> CommonColours;
    public HideType Hide;
    public int Health;
    public int MaxHealth;
    public int Strength;
    public int Speed;
    public int Awareness;
    public int Charisma;
    public int Dexterity;
    public int Constitution;
    public int Wisdom;
    public int Luck;
    public int Intelligence;
    public List<TerrainType> PreferredTerrains;
    public CoverType CoverType;
    public bool IsHostile;
    public bool IsPack;
    public bool IsHerd;
    public bool IsDomestic;
    public bool IsMountable;

    public AnimalCreationData()
    {
        TerrainRarities = new List<TerrainRarity>();
        CommonColours = new List<string>();
        PreferredTerrains = new List<TerrainType>();
    }
}

public enum Diet
{
    Unknown,    // This would be the default value
    Herbivore,
    Omnivore,
    Fungivore,
    Carnivore
}

public enum AnimalSize
{
    Tiny,
    Small,
    Medium,
    Large,
    Huge
}

[System.Serializable]
public class TerrainRarity
{
    public TerrainType Terrain;
    public RarityType Rarity;

    public TerrainRarity(TerrainType terrain, RarityType rarity)
    {
        Terrain = terrain;
        Rarity = rarity;
    }
}

public enum RarityType
{
    Common,
    Uncommon,
    Rare,
    Impossible,
    Legendary
}
