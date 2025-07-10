using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class ContainerPanelUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerItemListText;
    [SerializeField] private TextMeshProUGUI containerItemListText;
    [SerializeField] private TextMeshProUGUI transferItemListText;
    [SerializeField] private TextMeshProUGUI selectedFieldText;

    [SerializeField] private Button addButton;
    [SerializeField] private Button removeButton;
    [SerializeField] private Button finalizeButton;

    [SerializeField] private GameObject containerPanel;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI playerMoneyText;

    private bool isPlayerInventorySelected = true;
    private int playerSelectedItemIndex = 0;
    private int containerSelectedItemIndex = 0;
    private int transferSelectedItemIndex = 0;

    private List<InventoryContainer> playerContainers = new List<InventoryContainer>();
    private List<InventoryContainer> containerContainers = new List<InventoryContainer>();
    private List<Item> middleTransferItems = new List<Item>();

    private Inventory playerInventory;
    private Inventory containerInventory;
    private IInteractable interactableContainer;

    void Awake()
    {
        if (containerPanel == null || addButton == null || removeButton == null || finalizeButton == null)
        {
            Debug.LogError("ContainerPanelUI: UI elements are not assigned.");
            return;
        }

        containerPanel.SetActive(false);
        addButton.onClick.AddListener(MoveItemToTransfer);
        removeButton.onClick.AddListener(MoveItemBackFromTransfer);
        finalizeButton.onClick.AddListener(FinalizeTransfer);
    }

    public void SetupContainerInteraction(Inventory containerInventory, Inventory playerInventory)
    {
        this.containerInventory = containerInventory;
        this.playerInventory = playerInventory;
        this.interactableContainer = containerInventory as IInteractable;

        containerPanel.SetActive(true);
        PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Container;

        UpdateItems();
        UpdateUI();
    }

    private void UpdateItems()
    {
        playerContainers = playerInventory.GetInventoryContainers();
        containerContainers = containerInventory.GetInventoryContainers();
    }

    private void UpdateUI()
    {
        UpdateItems();

        playerItemListText.text = GenerateContainerListText(playerContainers, playerSelectedItemIndex, isPlayerInventorySelected);
        containerItemListText.text = GenerateContainerListText(containerContainers, containerSelectedItemIndex, !isPlayerInventorySelected);
        transferItemListText.text = GenerateTransferListText(middleTransferItems, transferSelectedItemIndex);

        playerMoneyText.text = $"Money: {PlayerStats.Instance.Money}";

        UpdatePlayerName();
        UpdateItemListDisplay();
        UpdateButtonsInteractability();
    }

    void Update()
    {
        if (PlayerStats.Instance.KeyboardPanel == KeyboardPanel.Container)
        {
            HandleInput();
        }
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            NavigateItems(-1);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            NavigateItems(1);
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) ||
                 Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            isPlayerInventorySelected = !isPlayerInventorySelected;
            UpdateHighlight();
        }

        UpdateButtonsInteractability();
    }

    private void UpdateButtonsInteractability()
    {
        addButton.interactable = (isPlayerInventorySelected && playerContainers.Count > 0) ||
                                 (!isPlayerInventorySelected && containerContainers.Count > 0);

        removeButton.interactable = middleTransferItems.Count > 0;
        finalizeButton.interactable = middleTransferItems.Count > 0;
    }

    private void NavigateItems(int direction)
    {
        if (isPlayerInventorySelected)
        {
            playerSelectedItemIndex = Mathf.Clamp(playerSelectedItemIndex + direction, 0, playerContainers.Count - 1);
        }
        else
        {
            containerSelectedItemIndex = Mathf.Clamp(containerSelectedItemIndex + direction, 0, containerContainers.Count - 1);
        }

        UpdateItemListDisplay();
    }

    private void UpdateItemListDisplay()
    {
        playerItemListText.text = GenerateContainerListText(playerContainers, playerSelectedItemIndex, isPlayerInventorySelected);
        containerItemListText.text = GenerateContainerListText(containerContainers, containerSelectedItemIndex, !isPlayerInventorySelected);
        transferItemListText.text = GenerateTransferListText(middleTransferItems, transferSelectedItemIndex);

        UpdateButtonsInteractability();
    }

    private string GenerateContainerListText(List<InventoryContainer> containers, int selectedIndex, bool isActivePanel)
    {
        if (containers == null || containers.Count == 0) return "No items available.";

        string itemListString = "";
        for (int i = 0; i < containers.Count; i++)
        {
            var container = containers[i];
            if (container.Items == null || container.Items.Count == 0) continue;

            string formattedItem = $"{container.Name} x{container.Amount} - Value: {container.Items.First().Value}";
            itemListString += isActivePanel && i == selectedIndex ? $"<color=#FFFF00>{formattedItem}</color>\n" : $"{formattedItem}\n";
        }

        return itemListString.TrimEnd('\n');
    }

    private string GenerateTransferListText(List<Item> transferItems, int selectedIndex)
    {
        if (transferItems == null || transferItems.Count == 0) return "No items waiting for transfer.";

        return string.Join("\n", transferItems.Select((item, i) =>
            i == selectedIndex ? $"<color=#FFFF00>{item.ItemInGameName} - Value: {item.Value}</color>" : $"{item.ItemInGameName} - Value: {item.Value}"));
    }

    private void MoveItemToTransfer()
    {
        InventoryContainer selectedContainer = isPlayerInventorySelected ? playerContainers.ElementAtOrDefault(playerSelectedItemIndex)
                                                                         : containerContainers.ElementAtOrDefault(containerSelectedItemIndex);
        if (selectedContainer == null || !selectedContainer.Items.Any()) return;

        Item selectedItem = selectedContainer.Items.First();
        middleTransferItems.Add(selectedItem);

        if (isPlayerInventorySelected)
            playerInventory.RemoveItem(selectedItem.ItemInGameName, 1);
        else
            containerInventory.RemoveItem(selectedItem.ItemInGameName, 1);

        UpdateUI();
    }

    private void MoveItemBackFromTransfer()
    {
        if (middleTransferItems.Count == 0) return;

        Item selectedItem = middleTransferItems[transferSelectedItemIndex];
        if (isPlayerInventorySelected)
            playerInventory.AddItem(selectedItem, 1);
        else
            containerInventory.AddItem(selectedItem, 1);

        middleTransferItems.RemoveAt(transferSelectedItemIndex);
        transferSelectedItemIndex = Mathf.Clamp(transferSelectedItemIndex, 0, middleTransferItems.Count - 1);

        UpdateUI();
    }

    private void FinalizeTransfer()
    {
        foreach (var item in middleTransferItems)
        {
            if (isPlayerInventorySelected)
                containerInventory.AddItem(item, 1);
            else
                playerInventory.AddItem(item, 1);
        }

        middleTransferItems.Clear();
        UpdateUI();
    }

    private void UpdateHighlight()
    {
        selectedFieldText.text = isPlayerInventorySelected ? "Selected: Your Items" : "Selected: Container Items";
        UpdateItemListDisplay();
    }

    public void CloseContainerPanel()
    {
        PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;
        containerPanel.SetActive(false);
        UIController.Instance.DeactivateGreyOutPanel();
    }

    private void UpdatePlayerName()
    {
        playerNameText.text = PlayerStats.Instance.CurrentPlayerCharacter.Name;
    }
}
