using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerPanelUI : MonoBehaviour
{
    public TMP_Text playerNameText;
    public TMP_Text healthDefenceText; // Combined text for Health and Defence
    public TMP_Text otherStatsText; // Text for Hunger, Stamina, Action Points, Move Points
    public TMP_Text rpgStatsText; // Text for RPG stats like Strength, Dexterity, etc.
    public TMP_Text equipmentText; // Text for displaying equipped items
    public TMP_Text activeItemsText; // New TextMeshPro for displaying active items

    public Button togglePanelButton; // Button to toggle between panels
    public Button toggleInventoryButton; // Button to open/close inventory panel
    public List<GameObject> panels; // List of panels to toggle between
    public GameObject inventoryPanel; // Reference to the inventory panel

    // References to the buttons and their texts
    public Button toggleSubMapButton;
    public TMP_Text toggleSubMapButtonText;

    public Button toggleMagicMapButton;
    public TMP_Text toggleMagicMapButtonText;

    public Button togglePOIButton;
    public TMP_Text togglePOIButtonText;

    public Button toggleFactionsButton;
    public TMP_Text toggleFactionsButtonText;

    public Button toggleWeatherMapButton;
    public TMP_Text toggleWeatherMapButtonText;

    private int currentPanelIndex = 0;
    private bool isFirstToggle = true; // Flag to track the first toggle

    private void Start()
    {
        // Ensure the panels list is not empty
        if (panels == null || panels.Count == 0)
        {
            Debug.LogError("Panels list is not assigned or is empty.");
            return;
        }

        // Deactivate all panels initially
        foreach (var panel in panels)
        {
            panel.SetActive(false);
        }

        // SubMap Button
        if (toggleSubMapButton != null && toggleSubMapButtonText != null)
        {
            UpdateSubMapButtonText();
            toggleSubMapButton.onClick.AddListener(ToggleSubMap);
        }

        // MagicMap Button
        if (toggleMagicMapButton != null && toggleMagicMapButtonText != null)
        {
            UpdateMagicMapButtonText();
            toggleMagicMapButton.onClick.AddListener(ToggleMagicMap);
        }

        // POI Button
        if (togglePOIButton != null && togglePOIButtonText != null)
        {
            UpdatePOIButtonText();
            togglePOIButton.onClick.AddListener(TogglePOIHighlight);
        }

        // Factions Button
        if (toggleFactionsButton != null && toggleFactionsButtonText != null)
        {
            UpdateFactionsButtonText();
            toggleFactionsButton.onClick.AddListener(ToggleFactions);
        }

        // WeatherMap Button
        if (toggleWeatherMapButton != null && toggleWeatherMapButtonText != null)
        {
            UpdateWeatherMapButtonText();
            toggleWeatherMapButton.onClick.AddListener(ToggleWeatherMap);
        }


        // Ensure currentPanelIndex is within range
        currentPanelIndex = Mathf.Clamp(currentPanelIndex, 0, panels.Count - 1);

        togglePanelButton.onClick.AddListener(TogglePanel);
        toggleInventoryButton.onClick.AddListener(ToggleInventoryPanel);
    }

    private void Update()
    {
        // Ensure we're only updating if the panels list is valid
        if (panels != null && panels.Count > 0 && panels[currentPanelIndex].activeSelf)
        {
            // Only update the UI for the currently active panel
            UpdateActivePanelUI();
        }
    }

    private void UpdateActivePanelUI()
    {
        // Ensure the currentPanelIndex is within the valid range
        if (currentPanelIndex >= 0 && currentPanelIndex < panels.Count)
        {
            if (currentPanelIndex == 0)
            {
                UpdatePlayerInformationUI();
            }
            else if (currentPanelIndex == 1)
            {
                UpdatePlayerEquipmentUI();
            }
        }
    }

    private void UpdatePlayerInformationUI()
    {
        if (PlayerStats.Instance.CurrentPlayerCharacter != null)
        {
            var currentPlayerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
            playerNameText.text = $"{currentPlayerCharacter.Name}";

            UpdateHealthDefenceDisplay(currentPlayerCharacter);
            UpdateOtherStatsDisplay();
            UpdateRPGStatsDisplay();
        }
        else
        {
            playerNameText.text = "No Player Selected"; // Or any default text you prefer
            healthDefenceText.text = ""; // Clear the health and defence text
            otherStatsText.text = ""; // Clear the other stats text
            rpgStatsText.text = ""; // Clear the RPG stats text
        }
    }

    private void UpdatePlayerEquipmentUI()
    {
        UpdateEquipmentDisplay();
        UpdateActiveItemsDisplay();
    }

    public void UpdateHealthDefenceDisplay(Character currentPlayerCharacter)
    {
        healthDefenceText.text = $"Health: {currentPlayerCharacter.Health} / {currentPlayerCharacter.MaxHealth}\n" +
                                 $"Defence: {PlayerStats.Instance.Defence}";
    }

    public void UpdateOtherStatsDisplay()
    {
        string otherStatsTextContent = $"Hunger: {PlayerStats.Instance.Satiety} / {PlayerStats.Instance.MaxSatiety}\n" +
                                       $"Stamina: {PlayerStats.Instance.Stamina} / {PlayerStats.Instance.MaxStamina}\n" +
                                       $"Action Points: {PlayerStats.Instance.ActionPoints} / {PlayerStats.Instance.MaxActionPoints}\n" +
                                       $"Move Points: {PlayerStats.Instance.MovePoints} / {PlayerStats.Instance.MaxMovePoints}";

        otherStatsText.text = otherStatsTextContent;
    }

    public void UpdateRPGStatsDisplay()
    {
        string rpgStatsTextContent = $"Strength: {PlayerStats.Instance.Strength}\n" +
                                     $"Dexterity: {PlayerStats.Instance.Dexterity}\n" +
                                     $"Constitution: {PlayerStats.Instance.Constitution}\n" +
                                     $"Intelligence: {PlayerStats.Instance.Intelligence}\n" +
                                     $"Wisdom: {PlayerStats.Instance.Wisdom}\n" +
                                     $"Charisma: {PlayerStats.Instance.Charisma}";

        rpgStatsText.text = rpgStatsTextContent;
    }

    public void UpdateEquipmentDisplay()
    {
        string equipmentTextContent = "Equipment:\n";

        equipmentTextContent += $"Head: {GetItemName(EquipmentSlot.Head)}\n";
        equipmentTextContent += $"Neck: {GetItemName(EquipmentSlot.Neck)}\n";
        equipmentTextContent += $"Body: {GetItemName(EquipmentSlot.Body)}\n";
        equipmentTextContent += $"Main Hand: {GetItemName(EquipmentSlot.MainHand)}\n";
        equipmentTextContent += $"Off Hand: {GetItemName(EquipmentSlot.OffHand)}\n";
        equipmentTextContent += $"Waist: {GetItemName(EquipmentSlot.Waist)}\n";
        equipmentTextContent += $"Feet: {GetItemName(EquipmentSlot.Feet)}\n";

        equipmentText.text = equipmentTextContent;
    }

    public void UpdateActiveItemsDisplay()
    {
        string activeItemsTextContent = "Active Items:\n";

        foreach (var container in PlayerInventory.Instance.GetInventoryContainers())
        {
            foreach (var item in container.Items)
            {
                if (item.IsActiveInInventory)
                {
                    activeItemsTextContent += $"{item.ItemInGameName}\n";
                }
            }
        }

        activeItemsText.text = activeItemsTextContent;
    }

    private string GetItemName(EquipmentSlot slot)
    {
        var equippedItems = PlayerStats.Instance.CurrentPlayerCharacter?.EquippedItems;

        if (equippedItems != null && equippedItems.TryGetValue(slot, out Item equippedItem))
        {
            return equippedItem?.ItemInGameName ?? "None";
        }

        return "None";
    }


    public void TogglePanel()
    {
        // Safeguard to ensure the currentPanelIndex is valid
        if (panels == null || panels.Count == 0)
        {
            return;
        }

        if (isFirstToggle)
        {
            // On the first toggle, activate the first panel
            panels[currentPanelIndex].SetActive(true);
            isFirstToggle = false;
        }
        else
        {
            // Deactivate the current panel and move to the next
            panels[currentPanelIndex].SetActive(false);
            currentPanelIndex = (currentPanelIndex + 1) % panels.Count;
            panels[currentPanelIndex].SetActive(true);
        }

        // Update the new active panel immediately after toggling
        UpdateActivePanelUI();
    }

    public void ToggleInventoryPanel()
    {
        if (UIController.Instance != null)
        {
            UIController.Instance.ToggleInventoryPanel(); // Delegate the inventory panel toggling to UIController
        }
        else
        {
            Debug.LogWarning("UIController instance is not available.");
        }
    }

    // Toggle and update methods for each button

    private void ToggleSubMap()
    {
        GameManager.Instance.showSubMap = !GameManager.Instance.showSubMap;
        UpdateSubMapButtonText();
    }

    private void UpdateSubMapButtonText()
    {
        toggleSubMapButtonText.text = GameManager.Instance.showSubMap ? "Disable Sub Map" : "Enable Sub Map";
    }

    private void ToggleMagicMap()
    {
        GameManager.Instance.showMagicMap = !GameManager.Instance.showMagicMap;
        UpdateMagicMapButtonText();
    }

    private void UpdateMagicMapButtonText()
    {
        toggleMagicMapButtonText.text = GameManager.Instance.showMagicMap ? "Disable Magic Map" : "Enable Magic Map";
    }

    private void TogglePOIHighlight()
    {
        GameManager.Instance.highlightPOI = !GameManager.Instance.highlightPOI;
        UpdatePOIButtonText();
    }

    private void UpdatePOIButtonText()
    {
        togglePOIButtonText.text = GameManager.Instance.highlightPOI ? "Disable POI Highlight" : "Enable POI Highlight";
    }

    private void ToggleFactions()
    {
        GameManager.Instance.showFactions = !GameManager.Instance.showFactions;
        UpdateFactionsButtonText();
    }

    private void UpdateFactionsButtonText()
    {
        toggleFactionsButtonText.text = GameManager.Instance.showFactions ? "Disable Factions Map" : "Enable Factions Map";
    }

    private void ToggleWeatherMap()
    {
        GameManager.Instance.showWeatherMap = !GameManager.Instance.showWeatherMap;
        UpdateWeatherMapButtonText();
    }

    private void UpdateWeatherMapButtonText()
    {
        toggleWeatherMapButtonText.text = GameManager.Instance.showWeatherMap ? "Disable Weather Map" : "Enable Weather Map";
    }
}
