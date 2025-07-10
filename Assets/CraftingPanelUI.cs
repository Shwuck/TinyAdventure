using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class CraftingPanelUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI craftingListText;        // Shows the list of craftable items
    [SerializeField] private TextMeshProUGUI craftedItemsLogText;     // Temporary log of crafted items
    [SerializeField] private TextMeshProUGUI descriptionText;         // Text to display item description
    [SerializeField] private TextMeshProUGUI playerNameText;          // Text to display player name
    [SerializeField] private TextMeshProUGUI craftableCountText;      // Text to display count of craftable recipes
    [SerializeField] private TextMeshProUGUI craftedItemsCountText;   // Text to display count of crafted items this session
    [SerializeField] private Button craftButton;
    [SerializeField] private Button closeButton;                      // Button to close the crafting panel

    private PlayerInventory playerInventory;
    private List<CraftingRecipe> availableRecipes;                    // Available crafting recipes
    private List<string> craftedItemsLog = new List<string>();         // Tracks crafted items during the session

    private int selectedRecipeIndex = 0;

    void OnEnable()
    {
        playerInventory = PlayerInventory.Instance;
        craftedItemsLog.Clear();                                       // Clear the log on open
        RefreshCraftingList();                                         // Get all available recipes
        UpdatePlayerName();                                            // Update player name display
        UpdateCraftingListDisplay();                                   // Update the UI
        UpdateCraftedItemsCount();                                     // Reset crafted items count
    }

    void OnDisable()
    {
        // Debug message showing the crafted items after the window closes
        if (craftedItemsLog.Count > 0)
        {
            Debug.Log("The following items were crafted:\n" + string.Join(", ", craftedItemsLog));
        }
    }

    private void Update()
    {
        HandleInputNavigation();
    }

    private void HandleInputNavigation()
    {
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            ChangeSelectedRecipe(-1); // Move up
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            ChangeSelectedRecipe(1); // Move down
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            OnCraftButtonPressed(); // Craft the selected item
        }
    }

    private void RefreshCraftingList()
    {
        availableRecipes = CraftingManager.Instance.GetAvailableRecipes(playerInventory) ?? new List<CraftingRecipe>();

        // Ensure the selected index is clamped within the available recipe list bounds
        selectedRecipeIndex = Mathf.Clamp(selectedRecipeIndex, 0, availableRecipes.Count - 1);

        // Update the craftable recipes count
        UpdateCraftableCount();
    }

    private void UpdateCraftingListDisplay()
    {
        craftingListText.text = "";  // Clear current text

        if (availableRecipes.Count == 0)
        {
            craftingListText.text = "No available recipes.";
            descriptionText.text = "";
            craftButton.interactable = false;
            return;
        }

        for (int i = 0; i < availableRecipes.Count; i++)
        {
            if (i == selectedRecipeIndex)
            {
                craftingListText.text += $"<color=#FFFF00>{availableRecipes[i].ResultingItemName}</color>\n";  // Highlight selected recipe
            }
            else
            {
                craftingListText.text += $"{availableRecipes[i].ResultingItemName}\n";
            }
        }

        // Display the description of the currently selected recipe
        DisplayItemDescription(availableRecipes[selectedRecipeIndex]);

        craftButton.interactable = availableRecipes.Count > 0;  // Enable or disable the button based on availability
    }

    public void ChangeSelectedRecipe(int direction)
    {
        selectedRecipeIndex += direction;
        selectedRecipeIndex = Mathf.Clamp(selectedRecipeIndex, 0, availableRecipes.Count - 1);
        UpdateCraftingListDisplay();
    }

    public void OnCraftButtonPressed()
    {
        if (availableRecipes.Count > 0)
        {
            CraftingRecipe recipe = availableRecipes[selectedRecipeIndex];
            CraftingManager.Instance.CraftItem(recipe, playerInventory);  // Perform the crafting
            craftedItemsLog.Add(recipe.ResultingItemName);  // Add to crafted items log
            UpdateCraftedItemsLogDisplay();  // Update the log UI
            RefreshCraftingList();  // Refresh available recipes
            UpdateCraftingListDisplay();  // Update UI to reflect new available recipes
            UpdateCraftedItemsCount();  // Update the count of crafted items
        }
    }

    private void UpdateCraftedItemsLogDisplay()
    {
        craftedItemsLogText.text = "Items Crafted:\n" + string.Join("\n", craftedItemsLog);
    }

    public void CloseCraftingPanel()
    {
        gameObject.SetActive(false);
        UIController.Instance.DeactivateGreyOutPanel();
    }

    private void DisplayItemDescription(CraftingRecipe recipe)
    {
        // Search for the item in the creation data using the recipe's ResultingItemName
        var itemData = PermaLists.Instance.ItemCreationData
            .FirstOrDefault(data => data.Name.Equals(recipe.ResultingItemName, System.StringComparison.OrdinalIgnoreCase));

        if (itemData != null)
        {
            descriptionText.text = itemData.Description;  // Set the description text
        }
        else
        {
            descriptionText.text = "No description available.";  // Fallback message if not found
        }
    }

    // Update player's name on the UI
    private void UpdatePlayerName()
    {
        if (PlayerStats.Instance != null)
        {
            playerNameText.text = PlayerStats.Instance.CurrentPlayerCharacter.Name;  // Assuming PlayerStats has a Name property
        }
    }

    // Update the count of available craftable recipes
    private void UpdateCraftableCount()
    {
        craftableCountText.text = $"Craftable Recipes: {availableRecipes.Count}";
    }

    // Update the count of items crafted this session
    private void UpdateCraftedItemsCount()
    {
        craftedItemsCountText.text = $"Items Crafted This Session: {craftedItemsLog.Count}";
    }
}
