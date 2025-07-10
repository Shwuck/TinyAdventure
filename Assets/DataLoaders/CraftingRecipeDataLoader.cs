using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class CraftingRecipeDataLoader : MonoBehaviour, IDataLoader
{
    public void LoadData()
    {
        LoadCraftingRecipesFromJson();
    }

    private void LoadCraftingRecipesFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "CraftingRecipeData.json");

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            List<CraftingRecipe> craftingRecipes = JsonConvert.DeserializeObject<List<CraftingRecipe>>(json);

            // Store the loaded crafting recipes in a global data structure, like PermaLists
            PermaLists.Instance.CraftingRecipeList = craftingRecipes;

            Debug.Log("All crafting recipes loaded successfully.");
        }
        else
        {
            Debug.LogWarning("CraftingRecipeData.json not found in StreamingAssets!");
        }
    }
}

[System.Serializable]
public class CraftingRecipe
{
    public string ResultingItemName { get; set; }
    public Dictionary<string, int> ItemsNeeded { get; set; } // ItemName and ItemQuantity
}
