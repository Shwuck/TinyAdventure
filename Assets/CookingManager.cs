using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CookingManager : MonoBehaviour
{
    public static CookingManager Instance { get; private set; }

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

    // Retrieve all available cooking recipes based on player's inventory
    public List<CookingRecipe> GetAvailableRecipes()
    {
        List<CookingRecipe> availableRecipes = new List<CookingRecipe>();

        foreach (var recipe in PermaLists.Instance.CookingRecipeList)
        {
            if (PlayerCanCookRecipe(recipe))
            {
                availableRecipes.Add(recipe);
            }
        }

        return availableRecipes;
    }

    private bool PlayerCanCookRecipe(CookingRecipe recipe)
    {
        var inventory = PlayerStats.Instance.CurrentPlayerCharacter.Inventory; // Direct access to the player’s inventory

        foreach (var itemNeeded in recipe.ItemsNeeded)
        {
            ItemType itemType = itemNeeded.Key;
            int requiredAmount = itemNeeded.Value;

            if (!inventory.GetItemsOfType(itemType, requiredAmount))
            {
                return false;
            }
        }

        return true;
    }

    // Handle cooking an item from a recipe
    public void CookItem(CookingRecipe recipe)
    {
        var inventory = PlayerStats.Instance.CurrentPlayerCharacter.Inventory; // Direct access

        if (PlayerCanCookRecipe(recipe))
        {
            foreach (var itemNeeded in recipe.ItemsNeeded)
            {
                ItemType itemType = itemNeeded.Key;
                int requiredAmount = itemNeeded.Value;

                inventory.RemoveItemsByType(itemType, requiredAmount);
            }

            // Generate the cooked item and add it to inventory
            Item cookedItem = ItemGenerator.Instance.GenerateItem(recipe.ResultingItemName);
            if (cookedItem != null)
            {
                inventory.AddItem(cookedItem);
                Debug.Log($"Successfully cooked {recipe.ResultingItemName}.");
            }
            else
            {
                Debug.LogWarning($"Failed to generate item: {recipe.ResultingItemName}");
            }
        }
        else
        {
            Debug.LogWarning("Cannot cook item. Inventory lacks required ingredients.");
        }
    }
}
