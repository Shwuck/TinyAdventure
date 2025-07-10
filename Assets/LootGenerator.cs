using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LootGenerator : MonoBehaviour
{
    public static LootGenerator Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public Item GenerateLoot(string enemyName)
    {
        var lootData = PermaLists.Instance.LootCreationData;
        List<string> possibleLoot = new List<string>();

        foreach (var loot in lootData)
        {
            if (loot.CommonlyDroppedBy.Contains(enemyName))
            {
                possibleLoot.AddRange(Enumerable.Repeat(loot.ItemName, 60)); // 60% chance
            }
            if (loot.UncommonlyDroppedBy.Contains(enemyName))
            {
                possibleLoot.AddRange(Enumerable.Repeat(loot.ItemName, 30)); // 30% chance
            }
            if (loot.RarelyDroppedBy.Contains(enemyName))
            {
                possibleLoot.AddRange(Enumerable.Repeat(loot.ItemName, 10)); // 10% chance
            }
        }

        if (possibleLoot.Count == 0)
        {
            Debug.LogWarning($"No loot found for enemy: {enemyName}");
            return null; // No loot found for the enemy
        }

        string selectedItemName = possibleLoot[UnityEngine.Random.Range(0, possibleLoot.Count)];
        return ItemGenerator.Instance.GenerateItem(selectedItemName);
    }
}
