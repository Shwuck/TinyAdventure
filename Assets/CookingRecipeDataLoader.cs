using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class CookingRecipeDataLoader : MonoBehaviour, IDataLoader
{
    public void LoadData()
    {
        LoadCookingRecipesFromJson();
    }

    private void LoadCookingRecipesFromJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "CookingRecipeData.json");

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            List<CookingRecipe> cookingRecipes = JsonConvert.DeserializeObject<List<CookingRecipe>>(json);

            // Store the loaded cooking recipes in a global data structure, like PermaLists
            PermaLists.Instance.CookingRecipeList = cookingRecipes;

            Debug.Log("All cooking recipes loaded successfully.");
        }
        else
        {
            Debug.LogWarning("CookingRecipeData.json not found in StreamingAssets!");
        }
    }
}

[System.Serializable]
public class CookingRecipe
{
    public string ResultingItemName { get; set; }
    public Dictionary<ItemType, int> ItemsNeeded { get; set; } // ItemName and ItemQuantity
}
