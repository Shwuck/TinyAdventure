using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class DonationPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI playerItemListText;
    [SerializeField] private TextMeshProUGUI selectedItemsListText;
    [SerializeField] private TextMeshProUGUI villageNameText;
    [SerializeField] private TextMeshProUGUI playerMoneyText;
    [SerializeField] private TextMeshProUGUI villageNeedsText;
    [SerializeField] private TextMeshProUGUI potentialRecognitionAndRenownText;
    [SerializeField] private Button addButton;
    [SerializeField] private Button removeButton;
    [SerializeField] private Button finalizeDonationButton;

    private int playerSelectedItemIndex = 0;
    private int selectedItemIndex = 0;
    private CharacterInventory playerInventory;
    private Village village;

    private List<Item> playerDonatableItems = new List<Item>();
    private List<Item> selectedItems = new List<Item>();

    void Awake()
    {
        panel.SetActive(false);
        addButton.onClick.AddListener(SelectItemForDonation);
        removeButton.onClick.AddListener(RemoveSelectedItem);
        finalizeDonationButton.onClick.AddListener(FinalizeDonation);
    }

    public void SetupDonation(Village village, CharacterInventory playerInventory)
    {
        this.village = village;
        this.playerInventory = playerInventory;

        if (village == null)
        {
            Debug.LogError("Village is null in SetupDonation");
            return;
        }

        Debug.Log($"Setting up donation panel for village: {village.Stats.VillageName}");
        panel.SetActive(true);
        UpdateDonationUI();
    }

    private void UpdateDonationUI()
    {
        playerDonatableItems = playerInventory.GetAllItems()
            .Where(item => item.IsTradable && !item.Reserved)
            .ToList();

        playerItemListText.text = GenerateItemListText(playerDonatableItems, playerSelectedItemIndex);
        selectedItemsListText.text = GenerateItemListText(selectedItems, selectedItemIndex);
        villageNameText.text = $"Village: {village.Stats.VillageName}";
        playerMoneyText.text = $"Money: {PlayerStats.Instance.Money}";
        villageNeedsText.text = $"Needed Resource: {village.Stats.NeededResource}";

        UpdatePotentialRecognitionAndRenown();
        UpdateButtonsInteractability();
    }

    private void UpdateButtonsInteractability()
    {
        addButton.interactable = playerDonatableItems.Count > 0;
        removeButton.interactable = selectedItems.Count > 0;
        finalizeDonationButton.interactable = selectedItems.Count > 0;
    }

    private string GenerateItemListText(List<Item> items, int selectedIndex)
    {
        if (items == null || items.Count == 0) return "No items available.";

        return string.Join("\n", items.Select((item, i) =>
            i == selectedIndex ? $"<color=#FFFF00>{item.ItemInGameName} - Value: {item.Value}</color>" :
            $"{item.ItemInGameName} - Value: {item.Value}"));
    }

    private void SelectItemForDonation()
    {
        if (playerSelectedItemIndex < 0 || playerSelectedItemIndex >= playerDonatableItems.Count) return;

        var selectedItem = playerDonatableItems[playerSelectedItemIndex];

        selectedItems.Add(selectedItem);
        playerInventory.RemoveItem(selectedItem.ItemInGameName, 1);
        playerDonatableItems.RemoveAt(playerSelectedItemIndex);

        playerSelectedItemIndex = Mathf.Clamp(playerSelectedItemIndex, 0, playerDonatableItems.Count - 1);
        UpdateDonationUI();
    }

    private void RemoveSelectedItem()
    {
        if (selectedItemIndex < 0 || selectedItemIndex >= selectedItems.Count) return;

        var removedItem = selectedItems[selectedItemIndex];

        playerInventory.AddItem(removedItem, 1);
        selectedItems.RemoveAt(selectedItemIndex);

        selectedItemIndex = Mathf.Clamp(selectedItemIndex, 0, selectedItems.Count - 1);
        UpdateDonationUI();
    }

    private void UpdatePotentialRecognitionAndRenown()
    {
        int potentialRecognition = selectedItems.Sum(item => item.Value * (item.ItemTypes.Contains(village.Stats.NeededResource) ? 2 : 1));
        potentialRecognitionAndRenownText.text = $"Potential Recognition & Renown: {potentialRecognition}";
    }

    public void FinalizeDonation()
    {
        int totalRecognition = selectedItems.Sum(item => item.Value * (item.ItemTypes.Contains(village.Stats.NeededResource) ? 2 : 1));

        foreach (var item in selectedItems)
        {
            UpdateVillageStats(item);
            UpdatePlayerRecognitionAndRenown(item);
        }

        MessageLogManager.Instance.Log("item", $"You donated to {village.Stats.VillageName} and received {totalRecognition} Recognition and Renown.");

        selectedItems.Clear();
        UpdateDonationUI();
        CloseDonationPanel();
    }

    private void UpdateVillageStats(Item donatedItem)
    {
        switch (donatedItem.ItemTypes.FirstOrDefault())
        {
            case ItemType.Fruit:
            case ItemType.Vegetable:
            case ItemType.Meat:
                village.Stats.StoredFood++;
                break;
            case ItemType.Water:
                village.Stats.StoredWater++;
                break;
            case ItemType.Wood:
                village.Stats.StoredWood++;
                break;
            case ItemType.Stone:
                village.Stats.StoredStone++;
                break;
        }
    }

    private void UpdatePlayerRecognitionAndRenown(Item donatedItem)
    {
        int value = donatedItem.Value * (donatedItem.ItemTypes.Contains(village.Stats.NeededResource) ? 2 : 1);
        village.Stats.PlayerRecognition = Mathf.Clamp(village.Stats.PlayerRecognition + value, 0, 100);
        village.Stats.PlayerRenown = Mathf.Clamp(village.Stats.PlayerRenown + value, -100, 100);
    }

    public void CloseDonationPanel()
    {
        panel.SetActive(false);
        UIController.Instance.DeactivateGreyOutPanel();
    }
}
