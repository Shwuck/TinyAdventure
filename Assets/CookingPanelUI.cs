using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class CookingPanelUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI cookingListText;
    [SerializeField] private TextMeshProUGUI cookedItemsLogText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI cookableCountText;
    [SerializeField] private TextMeshProUGUI cookedItemsCountText;
    [SerializeField] private Button cookButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject cookingPanel;

    private PlayerInventory playerInventory;
    private List<CookingRecipe> availableRecipes;
    private List<string> cookedItemsLog = new List<string>();

    private int selectedRecipeIndex = 0;

    void OnEnable()
    {
        playerInventory = (PlayerInventory)PlayerStats.Instance.CurrentPlayerCharacter.Inventory;
        cookedItemsLog.Clear();

        if (playerInventory != null)
        {
            RefreshCookingList();
        }
        else
        {
            Debug.LogError("Player inventory is null, unable to refresh the cooking list.");
        }

        UpdatePlayerName();
        UpdateCookingListDisplay();
        UpdateCookedItemsCount();

        // Add listener to cook button
        cookButton.onClick.AddListener(OnCookButtonPressed);
    }

    void OnDisable()
    {
        if (cookedItemsLog.Count > 0)
        {
            Debug.Log("The following items were cooked:\n" + string.Join(", ", cookedItemsLog));
        }

        // Remove listener when panel is disabled
        cookButton.onClick.RemoveListener(OnCookButtonPressed);
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
            OnCookButtonPressed(); // Cook the selected item
        }
    }

    private void RefreshCookingList()
    {
        if (CookingManager.Instance == null)
        {
            Debug.LogError("CookingManager instance is null.");
            availableRecipes = new List<CookingRecipe>();
            return;
        }

        if (playerInventory == null)
        {
            Debug.LogError("Player inventory is null in RefreshCookingList.");
            availableRecipes = new List<CookingRecipe>();
            return;
        }

        // **Fixed: No need to pass `playerInventory` as a parameter anymore**
        availableRecipes = CookingManager.Instance.GetAvailableRecipes() ?? new List<CookingRecipe>();

        selectedRecipeIndex = Mathf.Clamp(selectedRecipeIndex, 0, availableRecipes.Count - 1);

        // Update the cookable recipes count
        UpdateCookableCount();
    }

    private void UpdateCookingListDisplay()
    {
        cookingListText.text = "";  // Clear text

        if (availableRecipes == null || availableRecipes.Count == 0)
        {
            cookingListText.text = "No available recipes.";
            descriptionText.text = "";
            cookButton.interactable = false;
            return;
        }

        for (int i = 0; i < availableRecipes.Count; i++)
        {
            cookingListText.text += (i == selectedRecipeIndex)
                ? $"<color=#FFFF00>{availableRecipes[i].ResultingItemName}</color>\n" // Highlight selection
                : $"{availableRecipes[i].ResultingItemName}\n";
        }

        // Show selected recipe's description
        if (availableRecipes.Count > 0)
        {
            DisplayItemDescription(availableRecipes[selectedRecipeIndex]);
        }

        cookButton.interactable = availableRecipes.Count > 0;
    }

    public void ChangeSelectedRecipe(int direction)
    {
        selectedRecipeIndex += direction;
        selectedRecipeIndex = Mathf.Clamp(selectedRecipeIndex, 0, availableRecipes.Count - 1);
        UpdateCookingListDisplay();
    }

    public void OnCookButtonPressed()
    {
        if (availableRecipes != null && availableRecipes.Count > 0)
        {
            CookingRecipe recipe = availableRecipes[selectedRecipeIndex];
            Debug.Log($"Attempting to cook: {recipe.ResultingItemName}");

            if (CookingManager.Instance != null)
            {
                CookingManager.Instance.CookItem(recipe);  // Fix: Removed `playerInventory` argument
                cookedItemsLog.Add(recipe.ResultingItemName);
                UpdateCookedItemsLogDisplay();
                RefreshCookingList();
                UpdateCookingListDisplay();
                UpdateCookedItemsCount();
            }
            else
            {
                Debug.LogError("CookingManager instance is null.");
            }
        }
    }

    private void UpdateCookedItemsLogDisplay()
    {
        cookedItemsLogText.text = "Items Cooked:\n" + string.Join("\n", cookedItemsLog);
    }

    public void CloseCookingPanel()
    {
        cookingPanel.SetActive(false);
        gameObject.SetActive(false);
        UIController.Instance.DeactivateGreyOutPanel();
        PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;
    }

    private void DisplayItemDescription(CookingRecipe recipe)
    {
        if (recipe == null)
        {
            descriptionText.text = "No description available.";
            return;
        }

        var itemData = PermaLists.Instance?.ItemCreationData
            ?.FirstOrDefault(data => data.Name.Equals(recipe.ResultingItemName, System.StringComparison.OrdinalIgnoreCase));

        descriptionText.text = itemData?.Description ?? "No description available.";
    }

    private void UpdatePlayerName()
    {
        playerNameText.text = PlayerStats.Instance?.CurrentPlayerCharacter?.Name ?? "Unknown Player";
    }

    private void UpdateCookableCount()
    {
        cookableCountText.text = $"Cookable Recipes: {availableRecipes?.Count ?? 0}";
    }

    private void UpdateCookedItemsCount()
    {
        cookedItemsCountText.text = $"Items Cooked This Session: {cookedItemsLog.Count}";
    }
}
