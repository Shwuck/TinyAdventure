using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;
using System;

public enum InventorySortMode
{
    Alphabetical,
    ItemValue
}

public enum InventoryFilterMode
{
    All,
    Equipped,
    NotEquipped
}

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI equipmentSlotsText;
    [SerializeField] private TextMeshProUGUI itemListText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;
    [SerializeField] private Transform actionButtonContainer;
    [SerializeField] private GameObject actionButtonPrefab;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button sortButton;
    [SerializeField] private Button filterButton;
    [SerializeField] private TextMeshProUGUI sortButtonText;
    [SerializeField] private TextMeshProUGUI filterButtonText;

    private InventorySortMode currentSortMode = InventorySortMode.Alphabetical;
    private InventoryFilterMode currentFilterMode = InventoryFilterMode.All;
    private PlayerInventory inventory;
    private List<InventoryContainer> containers;
    private int selectedItemIndex = 0;
    private int selectedEquipmentIndex = 0;

    private EquipmentSlot[] equipmentSlots;

    private void Start()
    {
        if (sortButton != null)
        {
            sortButton.onClick.AddListener(ToggleSortingMode);
        }
        if (filterButton != null)
        {
            filterButton.onClick.AddListener(ToggleFilterMode);
        }
    }


    private enum ActiveList
    {
        Inventory,
        Equipment
    }

    private ActiveList currentList = ActiveList.Inventory;

    void OnEnable()
    {
        StartCoroutine(WaitForInventoryAssignment());
        currentFilterMode = InventoryFilterMode.All;
        RefreshItemsList();
        UpdateItemListDisplay();
        UpdateFilterAndSortText();
    }

    private System.Collections.IEnumerator WaitForInventoryAssignment()
    {
        float timeout = 5f;
        float elapsedTime = 0f;

        while (PlayerStats.Instance == null ||
               PlayerStats.Instance.CurrentPlayerCharacter == null ||
               !(PlayerStats.Instance.CurrentPlayerCharacter.Inventory is PlayerInventory) ||
               PlayerStats.Instance.CurrentPlayerCharacter.Anatomy == null ||
               PlayerStats.Instance.CurrentPlayerCharacter.Anatomy.GetActiveEquipmentSlots().Count == 0)
        {
            if (elapsedTime > timeout)
            {
                Debug.LogError("InventoryUI: Timeout waiting for PlayerInventory and Equipment Slots!");
                yield break;
            }

            Debug.Log("InventoryUI: Waiting for PlayerInventory and Equipment Slots...");
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Assign the inventory
        inventory = PlayerStats.Instance.CurrentPlayerCharacter.Inventory as PlayerInventory;

        if (inventory == null)
        {
            Debug.LogError("InventoryUI: Failed to assign PlayerInventory!");
            yield break;
        }

        // Retrieve Equipment Slots properly from Anatomy
        equipmentSlots = PlayerStats.Instance.CurrentPlayerCharacter.Anatomy.GetActiveEquipmentSlots().ToArray();

        if (equipmentSlots == null || equipmentSlots.Length == 0)
        {
            Debug.LogError("InventoryUI: Retrieved Equipment Slots are NULL or EMPTY!");
            yield break;
        }

        Debug.Log($"InventoryUI: Successfully retrieved {equipmentSlots.Length} equipment slots from Anatomy.");

        // Initialize UI safely
        try
        {
            RefreshListsAndKeepPlace();
            closeButton.onClick.AddListener(ClosePanel);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"InventoryUI: Exception in UI Setup - {e.Message}");
        }
    }


    void OnDisable()
    {
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.KeyboardPanel = PlayerStats.Instance.IsInMainMap
                ? KeyboardPanel.MainMap
                : (PlayerStats.Instance.IsInNestedArea ? KeyboardPanel.NestedArea : KeyboardPanel.Default);
        }

        // Prevent multiple listeners stacking over time
        closeButton.onClick.RemoveListener(ClosePanel);
    }


    private void Update()
    {
        if (PlayerStats.Instance?.KeyboardPanel == KeyboardPanel.Inventory)
        {
            HandleInput();
            UpdateItemListDisplay();
            PopulateEquipmentSlots();
        }
    }

    private void HandleInput()
    {
        if (currentList == ActiveList.Inventory)
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) ChangeSelectedItem(-1);
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) ChangeSelectedItem(1);
        }
        else if (currentList == ActiveList.Equipment)
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) ChangeSelectedEquipment(-1);
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) ChangeSelectedEquipment(1);
        }

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) SwitchToEquipment();
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) SwitchToInventory();
    }

    private void ToggleSortingMode()
    {
        currentSortMode = (InventorySortMode)(((int)currentSortMode + 1) % Enum.GetValues(typeof(InventorySortMode)).Length);
        RefreshItemsList();
        UpdateItemListDisplay();
        UpdateFilterAndSortText();
    }

    private void ToggleFilterMode()
    {
        currentFilterMode = (InventoryFilterMode)(((int)currentFilterMode + 1) % Enum.GetValues(typeof(InventoryFilterMode)).Length);
        RefreshItemsList();
        UpdateItemListDisplay();
        UpdateFilterAndSortText();
    }

    private void UpdateFilterAndSortText()
    {
        sortButtonText.text = $"Sort: {currentSortMode}";
        filterButtonText.text = $"Filter: {currentFilterMode}";
    }


    private void SwitchToInventory()
    {
        currentList = ActiveList.Inventory;
        selectedItemIndex = Mathf.Clamp(selectedItemIndex, 0, containers.Count - 1);
        UpdateActionButtons();
    }

    private void SwitchToEquipment()
    {
        currentList = ActiveList.Equipment;
        selectedEquipmentIndex = Mathf.Clamp(selectedEquipmentIndex, 0, equipmentSlots.Length - 1);
        UpdateActionButtons();
    }

    private void ChangeSelectedItem(int direction)
    {
        selectedItemIndex = Mathf.Clamp(selectedItemIndex + direction, 0, containers.Count - 1);
        UpdateActionButtons();
    }

    private void ChangeSelectedEquipment(int direction)
    {
        selectedEquipmentIndex = Mathf.Clamp(selectedEquipmentIndex + direction, 0, equipmentSlots.Length - 1);
        UpdateActionButtons();
    }

    public void UpdateItemListDisplay()
    {
        RefreshItemsList();
        itemListText.text = "";
        itemDescriptionText.text = "";

        for (int i = 0; i < containers.Count; i++)
        {
            if (containers[i] == null || containers[i].Amount <= 0) continue;

            string itemName = containers[i].Items[0].ItemInGameName;
            bool isEquipped = containers[i].Items[0].IsEquipped;
            string colorTag = isEquipped ? "<color=#CCCCCC>" : "<color=#FFFFFF>";
            string highlightTag = (currentList == ActiveList.Inventory && i == selectedItemIndex) ? "<mark=#FFFF00AA>" : "";
            string endHighlightTag = highlightTag != "" ? "</mark>" : "";

            itemListText.text += $"{highlightTag}{colorTag}{itemName} x{containers[i].Amount}</color>{endHighlightTag}\n";

            if (currentList == ActiveList.Inventory && i == selectedItemIndex && containers[i].Items.Count > 0)
            {
                itemDescriptionText.text = containers[i].Items[0].Description;
            }
        }

        if (containers.Count == 0) itemListText.text = "Inventory is empty.";
    }

    private string GetInventoryItemName(Item item)
    {
        return item.IsEquipped ? $"{item.ItemInGameName} (Equipped)" : item.ItemInGameName;
    }

    public void PopulateEquipmentSlots()
    {
        var character = PlayerStats.Instance.CurrentPlayerCharacter;
        if (character == null || character.EquippedItems == null)
        {
            Debug.LogError("InventoryUI: Character or EquippedItems is NULL!");
            return;
        }

        string equipmentText = "Equipment:\n";

        for (int i = 0; i < equipmentSlots.Length; i++)
        {
            string itemName = GetItemName(equipmentSlots[i]);

            equipmentText += currentList == ActiveList.Equipment && i == selectedEquipmentIndex
                ? $"<color=#FFFF00>{equipmentSlots[i]}: {itemName}</color>\n"
                : $"{equipmentSlots[i]}: {itemName}\n";
        }

        equipmentSlotsText.text = equipmentText;
    }

    private string GetItemName(EquipmentSlot slot)
    {
        var character = PlayerStats.Instance.CurrentPlayerCharacter;
        if (character == null || character.EquippedItems == null)
        {
            return "None";
        }

        return character.EquippedItems.TryGetValue(slot, out Item equippedItem)
            ? equippedItem?.ItemInGameName ?? "None"
            : "None";
    }


    public void RefreshItemsList()
    {
        if (inventory == null)
        {
            Debug.LogError("RefreshItemsList: Inventory is NULL!");
            return;
        }

        var containersList = inventory.GetInventoryContainers();

        if (containersList == null)
        {
            Debug.LogError("RefreshItemsList: GetInventoryContainers() returned NULL!");
            return;
        }

        // Apply filtering first
        switch (currentFilterMode)
        {
            case InventoryFilterMode.Equipped:
                containersList = containersList.Where(c => c.Items.Any(i => i.IsEquipped)).ToList();
                break;
            case InventoryFilterMode.NotEquipped:
                containersList = containersList.Where(c => c.Items.All(i => !i.IsEquipped)).ToList();
                break;
        }

        // Apply sorting
        switch (currentSortMode)
        {
            case InventorySortMode.Alphabetical:
                containersList = containersList.OrderBy(c => c.Name).ToList();
                break;
            case InventorySortMode.ItemValue:
                containersList = containersList.OrderByDescending(c => c.Items.FirstOrDefault()?.Value ?? 0).ToList();
                break;
        }

        containers = new List<InventoryContainer>(containersList);
        selectedItemIndex = Mathf.Clamp(selectedItemIndex, 0, containers.Count - 1);
    }

    private void UpdateActionButtons()
    {
        foreach (Transform child in actionButtonContainer) Destroy(child.gameObject);

        if (currentList == ActiveList.Inventory)
        {
            if (containers.Count > 0 && selectedItemIndex >= 0 && selectedItemIndex < containers.Count)
            {
                InventoryContainer container = containers[selectedItemIndex];
                if (container?.Items.Count > 0)
                {
                    Item selectedItem = container.Items[0];
                    CreateActionButtons(selectedItem);
                }
            }
        }
        else if (currentList == ActiveList.Equipment)
        {
            var character = PlayerStats.Instance.CurrentPlayerCharacter;
            if (character == null || character.EquippedItems == null) return;

            if (character.EquippedItems.TryGetValue(equipmentSlots[selectedEquipmentIndex], out Item equippedItem))
            {
                CreateActionButtons(equippedItem);
            }
        }
    }

    private void CreateActionButtons(Item item)
    {

        Debug.Log($"Fetching interactions for {item.ItemInGameName}. Actions found: {string.Join(", ", item.GetAvailableInteractions(inventory).Select(a => a.Name))}");
        Debug.Log("");

        foreach (var action in item.GetAvailableInteractions(inventory))
        {
            if (action == null || !action.IsAvailable(item, inventory)) continue; // Ensure only valid actions are added

            var buttonObj = Instantiate(actionButtonPrefab, actionButtonContainer);
            buttonObj.GetComponentInChildren<TextMeshProUGUI>().text = action.Name;

            IItemInteraction tempAction = action;
            buttonObj.GetComponent<Button>().onClick.AddListener(() => PerformAction(tempAction));
        }
    }

    private void PerformAction(IItemInteraction action)
    {
        if (action == null)
        {
            Debug.LogWarning("InventoryUI: Attempted to perform a null action.");
            return;
        }

        var character = PlayerStats.Instance.CurrentPlayerCharacter;
        if (character == null)
        {
            Debug.LogError("InventoryUI: No active player character found.");
            return;
        }

        bool combatContext = TurnOrchestrator.Instance != null &&
                             TurnOrchestrator.Instance.CurrentContext == TurnContext.Combat;

        ActionCostProfile typedProfile = ActionEconomyExecutionRouter.ResolveProfile(action, combatContext)
            ?? ActionCostProfileResolver.BuildForItemInteraction(action);
        if (typedProfile != null && typedProfile.MigrationState == ActionEconomyMigrationState.TypedActionEconomy)
        {
            ActionCostProfileResolver.LogPredictedCost("InventoryUI.PerformAction typed", action.Name, typedProfile, character);

            Item selectedTypedItem = null;
            if (currentList == ActiveList.Inventory &&
                containers.Count > 0 &&
                selectedItemIndex >= 0 &&
                selectedItemIndex < containers.Count)
            {
                InventoryContainer typedContainer = containers[selectedItemIndex];
                if (typedContainer?.Items.Count > 0)
                {
                    selectedTypedItem = typedContainer.Items[0];
                }
            }

            if (combatContext && typedProfile.CombatBehaviour == CombatActionBehaviour.Unavailable)
            {
                GameDebugger.Instance.LogWarning($"InventoryUI.PerformAction rejected typed item action '{action.Name}' because combat behaviour is unavailable.");
                return;
            }

            if (!combatContext && typedProfile.ExplorationBehaviour == ExplorationActionBehaviour.Unavailable)
            {
                GameDebugger.Instance.LogWarning($"InventoryUI.PerformAction rejected typed item action '{action.Name}' because exploration behaviour is unavailable.");
                return;
            }

            if (action is ConsumeInteraction && selectedTypedItem != null)
            {
                typedProfile.ConsumptionCapacityCost = Mathf.Max(1, selectedTypedItem.ConsumptionCapacityCost);
                typedProfile.CombatExertionCost = combatContext ? FixedPointResourceMath.FromPoints(1f) : 0;
            }

            ActionCostCommitment commitment = ActionCostProfileResolver.CreateCommitment(typedProfile, null, $"InventoryUI.PerformAction:{action.Name}");
            ActionCostCommitResult commitResult = commitment.TryCommit(character, $"InventoryUI.PerformAction:{action.Name}");
            if (!commitResult.IsCommitted)
            {
                GameDebugger.Instance.LogWarning($"InventoryUI.PerformAction typed economy rejected for '{action.Name}'. Reason={commitResult.RejectionReason}");
                return;
            }

            if (currentList == ActiveList.Inventory)
            {
                if (containers.Count > 0 && selectedItemIndex >= 0 && selectedItemIndex < containers.Count)
                {
                    InventoryContainer container = containers[selectedItemIndex];
                    if (container?.Items.Count > 0) action.ExecuteInteraction(container.Items[0], inventory);
                }
            }
            else if (currentList == ActiveList.Equipment)
            {
                if (character.EquippedItems.TryGetValue(equipmentSlots[selectedEquipmentIndex], out Item equippedItem))
                {
                    action.ExecuteInteraction(equippedItem, inventory);
                }
            }

            RefreshListsAndKeepPlace();
            return;
        }

        if (currentList == ActiveList.Inventory)
        {
            if (containers.Count > 0 && selectedItemIndex >= 0 && selectedItemIndex < containers.Count)
            {
                InventoryContainer container = containers[selectedItemIndex];
                if (container?.Items.Count > 0) action.ExecuteInteraction(container.Items[0], inventory);
            }
        }
        else if (currentList == ActiveList.Equipment)
        {
            if (character.EquippedItems.TryGetValue(equipmentSlots[selectedEquipmentIndex], out Item equippedItem))
            {
                action.ExecuteInteraction(equippedItem, inventory);
            }
        }

        RefreshListsAndKeepPlace();
    }

    private void RefreshListsAndKeepPlace()
    {
        RefreshItemsList();
        selectedItemIndex = Mathf.Clamp(selectedItemIndex, 0, containers.Count - 1);
        selectedEquipmentIndex = Mathf.Clamp(selectedEquipmentIndex, 0, equipmentSlots.Length - 1);
        UpdateItemListDisplay();
        PopulateEquipmentSlots();
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
        UIController.Instance?.DeactivateGreyOutPanel();
    }
}
