using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

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

    // Method to retrieve all available recipes based on an inventory
    public List<CraftingRecipe> GetAvailableRecipes(Inventory inventory)
    {
        List<CraftingRecipe> availableRecipes = new List<CraftingRecipe>();

        foreach (var recipe in PermaLists.Instance.CraftingRecipeList)
        {
            if (CanCraftRecipe(inventory, recipe))
            {
                availableRecipes.Add(recipe);
            }
        }

        return availableRecipes;
    }

    // Check if the inventory has the necessary items to craft a recipe
    private bool CanCraftRecipe(Inventory inventory, CraftingRecipe recipe)
    {
        foreach (var itemNeeded in recipe.ItemsNeeded)
        {
            string itemName = itemNeeded.Key;
            int requiredAmount = itemNeeded.Value;

            if (!inventory.HasItem(itemName) || inventory.GetItemCount(itemName) < requiredAmount)
            {
                return false;
            }
        }

        return true;
    }

    // Method to handle crafting an item from a recipe
    public void CraftItem(CraftingRecipe recipe, Inventory inventory)
    {
        if (CanCraftRecipe(inventory, recipe))
        {
            foreach (var itemNeeded in recipe.ItemsNeeded)
            {
                string itemName = itemNeeded.Key;
                int requiredAmount = itemNeeded.Value;

                inventory.RemoveItems(itemName, requiredAmount);
            }

            // Generate the crafted item and add it to inventory
            Item craftedItem = ItemGenerator.Instance.GenerateItem(recipe.ResultingItemName);
            if (craftedItem != null)
            {
                inventory.AddItem(craftedItem);
                Debug.Log($"Successfully crafted {recipe.ResultingItemName}.");
            }
            else
            {
                Debug.LogWarning($"Failed to generate item: {recipe.ResultingItemName}");
            }
        }
        else
        {
            Debug.LogWarning("Cannot craft item. Inventory lacks required materials.");
        }
    }
}
