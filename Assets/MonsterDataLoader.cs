using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class MonsterDataLoader : MonoBehaviour, IDataLoader
{

    public void LoadData()
    {
        LoadCreationDataFromJson();
        AdjustMonsterSymbols();
        SetSpecialAttributes();
    }

    public void LoadCreationDataFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "MonsterCreationData.json");

        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                MonsterCreationDataList monsterData = JsonConvert.DeserializeObject<MonsterCreationDataList>(json);

                if (monsterData == null || monsterData.Monsters == null)
                {
                    Debug.LogError("MonsterCreationData.json is empty or has an incorrect format.");
                    return;
                }

                // Assign the loaded data to PermaLists
                PermaLists.Instance.MonsterCreationData = monsterData.Monsters;

                Debug.Log($"Loaded {monsterData.Monsters.Count} monsters into PermaLists.");
            }
            catch (JsonException ex)
            {
                Debug.LogError($"Error parsing MonsterCreationData.json: {ex.Message}");
            }
        }
        else
        {
            Debug.LogError("MonsterCreationData.json not found in StreamingAssets!");
        }
    }

    private void AdjustMonsterSymbols()
    {
        foreach (var monster in PermaLists.Instance.MonsterCreationData)
        {
            if (monster.Size == MonsterSize.Tiny || monster.Size == MonsterSize.Small)
            {
                monster.Symbol = char.ToLower(monster.Symbol);
                Debug.Log($"Adjusted Symbol for {monster.MonsterName} to lowercase.");
            }
        }
    }

    private void SetSpecialAttributes()
    {
        foreach (var monster in PermaLists.Instance.MonsterCreationData)
        {
            if (monster.Type == MonsterType.Undead)
            {
                monster.IsResistantToPoison = true;
                Debug.Log($"{monster.MonsterName} is undead and resistant to poison.");
            }

            if (monster.Type == MonsterType.Dragon)
            {
                monster.CanFly = true;
                Debug.Log($"{monster.MonsterName} is a dragon and can fly.");
            }
        }
    }
}

[System.Serializable]
public class MonsterCreationDataList
{
    public List<MonsterCreationData> Monsters;
}

[System.Serializable]
public class MonsterCreationData
{
    public string MonsterName;
    public char Symbol;
    public string Color;
    public MonsterType Type;
    public bool IsBoss;
    public RarityType Rarity;
    public MonsterSize Size;
    public string BodyType;
    public int MaxHealth;
    public int Strength;
    public int Dexterity;
    public int Constitution;
    public int Intelligence;
    public int Wisdom;
    public int Luck;
    public int Awareness;
    public int Speed;
    public List<MonsterAbility> Abilities;
    public List<TerrainType> AllowedTerrains;
    public bool IsResistantToPoison;
    public bool CanFly;
    public Dictionary<DamageType, float> DamageResistances { get; set; } = new Dictionary<DamageType, float>();

    public MonsterCreationData()
    {
        Abilities = new List<MonsterAbility>();
        AllowedTerrains = new List<TerrainType>();
    }
}

public enum MonsterType
{
    Beast,
    Undead,
    Dragon,
    Elemental,
    Humanoid,
    Brute,
    Aberration,
    Insectoid
}

public enum MonsterSize
{
    Tiny,
    Small,
    Medium,
    Large,
    Huge
}
