using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;

public class TradePanelUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerItemListText;
    [SerializeField] private TextMeshProUGUI npcItemListText;
    [SerializeField] private TextMeshProUGUI tradeItemsListText;
    [SerializeField] private TextMeshProUGUI selectedFieldText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [SerializeField] private Button addButton;
    [SerializeField] private Button removeButton;
    [SerializeField] private Button acceptButton;

    [SerializeField] private GameObject tradePanel;
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private TextMeshProUGUI playerMoneyText;
    [SerializeField] private TextMeshProUGUI npcMoneyText;
    [SerializeField] private TextMeshProUGUI playerTradeValueText;
    [SerializeField] private TextMeshProUGUI npcTradeValueText;
    [SerializeField] private TextMeshProUGUI totalTradeValueText;

    private int playerSelectedItemIndex = 0;
    private int npcSelectedItemIndex = 0;
    private int tradeListSelectedItemIndex = 0;

    private List<InventoryContainer> playerTradableItems = new List<InventoryContainer>();
    private List<InventoryContainer> npcTradableItems = new List<InventoryContainer>();
    private List<TradeItem> currentTradeItems = new List<TradeItem>();

    private CharacterInventory playerInventory;
    private CharacterInventory npcInventory;
    private Character npc;

    private int playerTotalValue = 0;
    private int npcTotalValue = 0;
    private int totalTradeValue = 0;

    private enum SelectedTradeList
    {
        PlayerItems,
        TradeItems,
        NpcItems
    }

    private SelectedTradeList currentSelectedTradeList = SelectedTradeList.PlayerItems;

    void Awake()
    {
        tradePanel.SetActive(false);
        addButton.onClick.AddListener(AddItemToTrade);
        removeButton.onClick.AddListener(RemoveItemFromTrade);
        acceptButton.onClick.AddListener(ConfirmTrade);
    }

    public void SetupTrade(Character npc, CharacterInventory playerInventory)
    {
        this.npc = npc;
        this.playerInventory = playerInventory;
        this.npcInventory = npc.Inventory;

        PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Trade;
        tradePanel.SetActive(true);

        UpdateTradableItems();
        UpdateTradeUI();
    }

    private void UpdateTradableItems()
    {
        playerTradableItems = playerInventory.GetInventoryContainers();
        npcTradableItems = npcInventory.GetInventoryContainers();
    }

    private void UpdateTradeUI()
    {
        UpdateTradableItems();

        playerItemListText.text = GenerateContainerListText(playerTradableItems, playerSelectedItemIndex);
        npcItemListText.text = GenerateContainerListText(npcTradableItems, npcSelectedItemIndex);
        tradeItemsListText.text = GenerateTradeItemsListText(currentTradeItems, tradeListSelectedItemIndex);

        npcNameText.text = $"{npc?.Name ?? "NPC"}";
        playerMoneyText.text = $"Money: {PlayerStats.Instance?.Money ?? 0}";
        npcMoneyText.text = $"Money: {npc?.Money ?? 0}";

        playerTradeValueText.text = $"Your Trade Value: {playerTotalValue}";
        npcTradeValueText.text = $"{npc?.Name}'s Trade Value: {npcTotalValue}";
        totalTradeValueText.text = $"Full Transaction: {totalTradeValue}";

        UpdateButtonsInteractability();
        UpdateDescriptionText();
    }

    private void UpdateButtonsInteractability()
    {
        addButton.interactable = (currentSelectedTradeList == SelectedTradeList.PlayerItems && playerTradableItems.Any()) ||
                                 (currentSelectedTradeList == SelectedTradeList.NpcItems && npcTradableItems.Any());

        removeButton.interactable = currentSelectedTradeList == SelectedTradeList.TradeItems && currentTradeItems.Any();
        acceptButton.interactable = totalTradeValue <= (PlayerStats.Instance?.Money ?? 0);
    }

    private void NavigateItems(int direction)
    {
        switch (currentSelectedTradeList)
        {
            case SelectedTradeList.PlayerItems:
                playerSelectedItemIndex = Mathf.Clamp(playerSelectedItemIndex + direction, 0, playerTradableItems.Count - 1);
                break;
            case SelectedTradeList.NpcItems:
                npcSelectedItemIndex = Mathf.Clamp(npcSelectedItemIndex + direction, 0, npcTradableItems.Count - 1);
                break;
            case SelectedTradeList.TradeItems:
                tradeListSelectedItemIndex = Mathf.Clamp(tradeListSelectedItemIndex + direction, 0, currentTradeItems.Count - 1);
                break;
        }

        UpdateTradeUI();
    }

    private string GenerateContainerListText(List<InventoryContainer> containers, int selectedIndex)
    {
        if (containers == null || containers.Count == 0) return "No items available.";

        string itemListString = "";
        for (int i = 0; i < containers.Count; i++)
        {
            var firstItem = containers[i]?.Items.FirstOrDefault();
            string itemValue = firstItem != null ? $"Value: {firstItem.Value}" : "No items";

            itemListString += i == selectedIndex
                ? $"<color=#FFFF00>{containers[i].Name} x{containers[i].Amount} - {itemValue}</color>\n"
                : $"{containers[i].Name} x{containers[i].Amount} - {itemValue}\n";
        }
        return itemListString.TrimEnd('\n');
    }

    private string GenerateTradeItemsListText(List<TradeItem> tradeItems, int selectedIndex)
    {
        if (tradeItems == null || tradeItems.Count == 0) return "Currently not trading.";

        string itemListString = "";
        for (int i = 0; i < tradeItems.Count; i++)
        {
            string originPrefix = tradeItems[i].IsFromPlayer ? "Your Item: " : $"{npc?.Name}'s Item: ";

            itemListString += i == selectedIndex
                ? $"<color=#FFFF00>{originPrefix}{tradeItems[i].Item.ItemInGameName} - Value: {tradeItems[i].Item.Value}</color>\n"
                : $"{originPrefix}{tradeItems[i].Item.ItemInGameName} - Value: {tradeItems[i].Item.Value}\n";
        }
        return itemListString.TrimEnd('\n');
    }


    private void AddItemToTrade()
    {
        InventoryContainer selectedContainer = currentSelectedTradeList == SelectedTradeList.PlayerItems
            ? playerTradableItems.ElementAtOrDefault(playerSelectedItemIndex)
            : npcTradableItems.ElementAtOrDefault(npcSelectedItemIndex);

        if (selectedContainer == null || !selectedContainer.Items.Any()) return;

        Item selectedItem = selectedContainer.Items.First();
        currentTradeItems.Add(new TradeItem(selectedItem, currentSelectedTradeList == SelectedTradeList.PlayerItems));

        if (currentSelectedTradeList == SelectedTradeList.PlayerItems)
        {
            playerInventory.RemoveItem(selectedItem.ItemInGameName, 1);
            playerTotalValue += selectedItem.Value;
        }
        else
        {
            npcInventory.RemoveItem(selectedItem.ItemInGameName, 1);
            npcTotalValue += selectedItem.Value;
        }

        totalTradeValue = playerTotalValue - npcTotalValue;
        UpdateTradeUI();
    }

    private void RemoveItemFromTrade()
    {
        if (!currentTradeItems.Any()) return;

        TradeItem selectedItem = currentTradeItems.ElementAtOrDefault(tradeListSelectedItemIndex);
        if (selectedItem == null) return;

        if (selectedItem.IsFromPlayer)
        {
            playerInventory.AddItem(selectedItem.Item, 1);
            playerTotalValue -= selectedItem.Item.Value;
        }
        else
        {
            npcInventory.AddItem(selectedItem.Item, 1);
            npcTotalValue -= selectedItem.Item.Value;
        }

        currentTradeItems.RemoveAt(tradeListSelectedItemIndex);
        totalTradeValue = playerTotalValue - npcTotalValue;

        UpdateTradeUI();
    }

    private void ConfirmTrade()
    {
        if (totalTradeValue > (PlayerStats.Instance?.Money ?? 0)) return;

        foreach (var tradeItem in currentTradeItems)
        {
            if (tradeItem.IsFromPlayer)
            {
                npcInventory.AddItem(tradeItem.Item, 1);
                PlayerStats.Instance.Money += tradeItem.Item.Value;
            }
            else
            {
                playerInventory.AddItem(tradeItem.Item, 1);
                PlayerStats.Instance.Money -= tradeItem.Item.Value;
            }
        }

        currentTradeItems.Clear();
        playerTotalValue = 0;
        npcTotalValue = 0;
        totalTradeValue = 0;

        UpdateTradeUI();
    }

    private void UpdateDescriptionText()
    {
        descriptionText.text = currentTradeItems.Any()
            ? currentTradeItems.ElementAtOrDefault(tradeListSelectedItemIndex)?.Item.Description ?? "No item selected."
            : "No items selected.";
    }
}

public class TradeItem
{
    public Item Item { get; set; }
    public bool IsFromPlayer { get; set; }

    public TradeItem(Item item, bool isFromPlayer)
    {
        Item = item;
        IsFromPlayer = isFromPlayer;
    }
}
