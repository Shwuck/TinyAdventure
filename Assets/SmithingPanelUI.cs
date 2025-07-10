using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class SmithingPanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown componentDropdown1;
    [SerializeField] private TMP_Dropdown componentDropdown2;
    [SerializeField] private TextMeshProUGUI resultingWeaponText;             // Text to display the resulting weapon
    [SerializeField] private TextMeshProUGUI resultingWeaponDescriptionText;  // Text to display the resulting weapon's description
    [SerializeField] private TextMeshProUGUI playerNameText;                  // Text to display the player's name
    [SerializeField] private Button smithButton;
    [SerializeField] private Button closeButton;                              // Button to close the panel
    [SerializeField] private GameObject smithingPanel;

    private PlayerInventory playerInventory;
    private List<InventoryContainer> componentContainers = new List<InventoryContainer>();
    private string selectedComponent1;
    private string selectedComponent2;

    private void Awake()
    {
        smithingPanel.SetActive(false);
        smithButton.onClick.AddListener(OnSmithButtonClicked);
        closeButton.onClick.AddListener(CloseSmithingPanel); // Assign the CloseSmithingPanel method to the close button
    }

    public void OpenSmithingPanel(PlayerInventory inventory)
    {
        playerInventory = inventory;
        componentContainers = playerInventory.GetInventoryContainers()
            .Where(container => container.Items.Any(item => item.ItemTypes.Contains(ItemType.Component)))
            .ToList();

        PopulateDropdown(componentDropdown1, componentContainers);
        PopulateDropdown(componentDropdown2, componentContainers);

        smithingPanel.SetActive(true);

        // Update the player's name in the panel
        UpdatePlayerName();
    }

    private void PopulateDropdown(TMP_Dropdown dropdown, List<InventoryContainer> containers)
    {
        dropdown.ClearOptions();
        List<string> options = containers.Select(container => container.Name).ToList();
        dropdown.AddOptions(options);
        dropdown.onValueChanged.AddListener(delegate { OnDropdownValueChanged(dropdown); });
    }

    private void OnDropdownValueChanged(TMP_Dropdown changedDropdown)
    {
        if (changedDropdown == componentDropdown1)
        {
            selectedComponent1 = componentDropdown1.options[componentDropdown1.value].text;
        }
        else if (changedDropdown == componentDropdown2)
        {
            selectedComponent2 = componentDropdown2.options[componentDropdown2.value].text;
        }

        DisplayResultingWeapon();
    }

    private void DisplayResultingWeapon()
    {
        if (!string.IsNullOrEmpty(selectedComponent1) && !string.IsNullOrEmpty(selectedComponent2))
        {
            var matchingRecipe = PermaLists.Instance.SmithingRecipeList
                .FirstOrDefault(recipe => (recipe.BodyComponent == selectedComponent1 && recipe.HeadComponent == selectedComponent2) ||
                                           (recipe.BodyComponent == selectedComponent2 && recipe.HeadComponent == selectedComponent1));

            if (matchingRecipe != null)
            {
                resultingWeaponText.text = $"Resulting Weapon: {matchingRecipe.ResultingWeapon}";

                // Fetch and display the description of the resulting weapon
                DisplayResultingWeaponDescription(matchingRecipe.ResultingWeapon);
            }
            else
            {
                resultingWeaponText.text = "No matching weapon found for the selected components.";
                resultingWeaponDescriptionText.text = ""; // Clear the description if no weapon is found
            }
        }
    }

    private void DisplayResultingWeaponDescription(string weaponName)
    {
        // Look for the weapon description in the item creation data
        var itemData = PermaLists.Instance.ItemCreationData
            .FirstOrDefault(data => data.Name.Equals(weaponName, System.StringComparison.OrdinalIgnoreCase));

        if (itemData != null)
        {
            resultingWeaponDescriptionText.text = itemData.Description; // Display the description
        }
        else
        {
            resultingWeaponDescriptionText.text = "No description available."; // Fallback if no description is found
        }
    }

    private void OnSmithButtonClicked()
    {
        if (!string.IsNullOrEmpty(selectedComponent1) && !string.IsNullOrEmpty(selectedComponent2))
        {
            var matchingRecipe = PermaLists.Instance.SmithingRecipeList
                .FirstOrDefault(recipe => (recipe.BodyComponent == selectedComponent1 && recipe.HeadComponent == selectedComponent2) ||
                                           (recipe.BodyComponent == selectedComponent2 && recipe.HeadComponent == selectedComponent1));

            if (matchingRecipe != null)
            {
                List<string> components = new List<string> { selectedComponent1, selectedComponent2 };
                Item resultingWeapon = ItemFactory.SmithItem(matchingRecipe.ResultingWeapon, components);

                if (resultingWeapon != null)
                {
                    var container1 = componentContainers.FirstOrDefault(c => c.Name == selectedComponent1);
                    var container2 = componentContainers.FirstOrDefault(c => c.Name == selectedComponent2);

                    if (container1 != null && container2 != null && container1.Items.Any() && container2.Items.Any())
                    {
                        Item item1 = container1.Items.First();
                        Item item2 = container2.Items.First();

                        // Ensure the player inventory has these items before attempting to remove them
                        if (playerInventory.HasItem(item1.ItemInGameName) && playerInventory.HasItem(item2.ItemInGameName))
                        {
                            playerInventory.RemoveItem(item1.ItemInGameName, 1);
                            playerInventory.RemoveItem(item2.ItemInGameName, 1);

                            // Add the resulting weapon
                            playerInventory.AddItem(resultingWeapon, 1);

                            Debug.Log($"Player crafted: {resultingWeapon.Name}");
                        }
                        else
                        {
                            Debug.LogWarning("Player inventory does not contain the required items for smithing.");
                        }
                    }
                    else
                    {
                        Debug.LogError("One or both components could not be found in the inventory.");
                    }
                }
                else
                {
                    Debug.LogError("Smithing failed. No resulting weapon was generated.");
                }
            }
            else
            {
                Debug.LogWarning("No matching recipe found.");
            }
        }
        else
        {
            Debug.LogWarning("Component selection is incomplete.");
        }
    }


    public void CloseSmithingPanel()
    {
        smithingPanel.SetActive(false);
        UIController.Instance.DeactivateGreyOutPanel();
        PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;
    }

    private void UpdatePlayerName()
    {
        if (PlayerStats.Instance != null)
        {
            playerNameText.text = PlayerStats.Instance.CurrentPlayerCharacter.Name;  // Assuming PlayerStats has a Name property
        }
    }
}
