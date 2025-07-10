using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public class UIController : MonoBehaviour
{
    public static UIController Instance { get; private set; } // Singleton instance

    public GameObject[] panels; // Array to hold all the panels you want to toggle
    public List<GameObject> backgroundPanels; // List to hold background panels
    public List<GameObject> mapPanels; // List to hold map panels
    public List<GameObject> coreGameplayPanels; // List to hold core gameplay panels
    public List<GameObject> brightnessAffectedPanels; // Assign panels in Inspector

    public KeyCode inventoryToggleKey = KeyCode.I; // Keycode for toggling the inventory panel
    public KeyCode closeAllPanelsKey = KeyCode.Escape; // Keycode for closing all panels
    public KeyCode openColourPanelKey = KeyCode.C; // Keycode for opening the colour panel
    public GameObject tradePanel;
    public GameObject dialoguePanel;
    public GameObject donationPanel;
    public GameObject villageInfoPanel;
    public GameObject containerPanel;
    public GameObject colourSettingsPanel; // Reference to the colour settings panel
    public GameObject oldMapEditior;
    public GameObject deathPanel;
    public GameObject craftingPanel;
    public GameObject cookingPanel;
    public MessageLogUIManager messageLogUIManager; // Reference to the MessageLogUIManager
    public TMP_Text inspectionText;
    public TMP_Text currentDateText;
    public TMP_Text currentTimeText;
    public TMP_Text currentCellText;
    public TMP_Text currentTimeSegmentText;
    public TMP_Text remainingTurnTimeText; // New text component for displaying remaining turn time
    public TMP_Text playerNameButtonText;
    public TMP_Text playerCharacterDetailsText;
    public MapDisplayUI mapDisplayUI;
    public FontManager fontManager;
    public MainMenuPanelUI mainMenuPanelUI;
    public GameObject smithingPanel;
    public GameObject hintPanel;
    public MultipurposePopupPanelUI multipurposePopupPanelUI;
    public GameObject splashPanel; // Reference to the splash panel to fade out
    public float fadeDuration = 1.0f; // Duration for the fade effect

    public Button descriptionButton;
    public Button inspectionButton;
    public Button playButton;
    public GameObject descriptionPanel;
    public GameObject inspectionPanel;

    public GameObject messageLog;
    public GameObject panelToShake;
    public GameObject uiCombatPanel;

    public Button playerCharacterButton;
    public GameObject playerCharacterPanel; // Reference to the PlayerCharacterPanel

    public GameObject greyOutPanel;

    public Button interactablesButton;
    public Button combatButton;
    public Button specialButton;

    private TimeManager.TimeSegment lastTimeSegment;


    private Dictionary<KeyCode, System.Action> panelShortcuts;

    private void Awake()
    {
        // Ensure only one instance of UIController exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Optional: if you want the UIController to persist across scenes
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Assign the MessageLogUIManager here if not assigned in the Inspector
        if (messageLogUIManager == null)
        {
            messageLogUIManager = FindObjectOfType<MessageLogUIManager>();
        }

        if (mapDisplayUI == null)
        {
            mapDisplayUI = FindObjectOfType<MapDisplayUI>();
        }

        if (currentDateText == null || currentTimeText == null || currentTimeSegmentText == null)
        {
            Debug.LogWarning("Date/Time text components are not assigned in the UIController.");
        }

        if (playerNameButtonText != null)
        {
            UpdatePlayerNameButtonText();
        }
        else
        {
            Debug.LogWarning("Player Name Button Text is not assigned in the UIController.");
        }

        // Ensure PlayerNameButton is assigned and add a listener to it
        if (playerCharacterButton != null)
        {
            playerCharacterButton.onClick.AddListener(TogglePlayerCharacterPanel);
        }
        else
        {
            Debug.LogWarning("PlayerCharacterButton is not assigned in the UIController.");
        }

        InitializePanelShortcuts();
        // Assign button click events for Adaptive Action Menu switching
        if (interactablesButton != null)
        {
            interactablesButton.onClick.AddListener(() => SetAdaptiveActionMenuPanel(AdapativeActionMenu.IInteractables));
        }

        if (combatButton != null)
        {
            combatButton.onClick.AddListener(() => SetAdaptiveActionMenuPanel(AdapativeActionMenu.Combat));
        }

        if (specialButton != null)
        {
            specialButton.onClick.AddListener(() => SetAdaptiveActionMenuPanel(AdapativeActionMenu.Special));
        }

        // Update the button states initially
        UpdateButtonStates();
    }

    private void SetAdaptiveActionMenuPanel(AdapativeActionMenu panel)
    {
        // Set the current panel in PlayerStats
        PlayerStats.Instance.AdaptiveActionMenuPanel = panel;

        // Update the button states
        UpdateButtonStates();

        PlayerController.Instance.UpdateAdaptiveActionMenu();
    }

    private void UpdateButtonStates()
    {
        // Reset all buttons to be interactable
        interactablesButton.interactable = true;
        combatButton.interactable = true;
        specialButton.interactable = true;

        // Grey out the button that corresponds to the current panel
        switch (PlayerStats.Instance.AdaptiveActionMenuPanel)
        {
            case AdapativeActionMenu.IInteractables:
                interactablesButton.interactable = false;
                break;
            case AdapativeActionMenu.Combat:
                combatButton.interactable = false;
                break;
            case AdapativeActionMenu.Special:
                specialButton.interactable = false;
                break;
        }
    }

    private void InitializePanelShortcuts()
    {
        // Initialize the dictionary and add key-panel mappings
        panelShortcuts = new Dictionary<KeyCode, System.Action>
    {
        { KeyCode.I, ToggleInventoryPanel },  // Link the inventory toggle key to the method
        // Add more key-panel mappings as needed
        };
    }

    private GameObject GetPanelByName(string panelName)
    {
        foreach (GameObject panel in panels)
        {
            if (panel.name == panelName)
            {
                return panel;
            }
        }
        return null;
    }

    private void Update()
    {
        // Check if the game has started before updating
        if (GameManager.Instance != null && GameManager.Instance.GameStarted)
        {
            UpdateMaps();
            GetCurrentDateTime();

            if (TimeManager.Instance.currentSegment != lastTimeSegment)
            {
                lastTimeSegment = TimeManager.Instance.currentSegment;
                AdjustTextBrightness();
            }

            UpdateMessageLogUI();
            UpdatePlayerNameButtonText();
            UpdateCurrentCellText();
            UpdatePlayerCharacterDetails();

            // Check for panel toggle keys and invoke associated actions
            foreach (var entry in panelShortcuts)
            {
                if (Input.GetKeyDown(entry.Key))
                {
                    entry.Value.Invoke(); // Invoke the method linked to the key (System.Action)
                }
            }

            // Check if the close all panels key is pressed
            if (Input.GetKeyDown(closeAllPanelsKey))
            {
                CloseAllPanels();
                DeactivateGreyOutPanel();
            }

            // Check if the player presses the C key to open the crafting panel
            if (Input.GetKeyDown(KeyCode.C))
            {
                OpenCraftingPanel(); // Call the method to open the crafting panel
            }

            // Check if the player presses the H key to open the hint panel
            if (Input.GetKeyDown(KeyCode.H))
            {
                if (hintPanel != null)
                {
                    OpenHintPanel(); // Show the hints
                }
                else
                {
                    Debug.LogWarning("HintPanel reference is not assigned in the UIController.");
                }
            }
        }
    }

    private void UpdateMaps()
    {
        if (mapDisplayUI != null)
        {
            mapDisplayUI.UpdateBothMaps();
        }
    }

    public void UpdateMapsAfterAction()
    {
        UpdateMaps();
    }


    public void ApplyFonts()
    {
        fontManager.ApplyFontStyleToAllTextMeshPro();
    }

    // Function to toggle the specified panel on or off
    public void TogglePanel(GameObject panelToToggle)
    {
        if (panelToToggle != null)
        {
            bool isActive = !panelToToggle.activeSelf;
            panelToToggle.SetActive(isActive);

            // Update KeyboardPanel in PlayerStats
            if (isActive)
            {
                UpdateKeyboardPanel(panelToToggle);
            }
            else
            {
                PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;
            }
        }
        else
        {
            Debug.LogWarning("Panel to toggle is not assigned.");
        }
    }

    // Function to toggle a panel by its name
    public void TogglePanelByName(string panelName)
    {
        foreach (GameObject panel in panels)
        {
            if (panel.name == panelName)
            {
                TogglePanel(panel);
                break;
            }
        }
    }

    // Function to close all panels
    public void CloseAllPanels()
    {
        foreach (GameObject panel in panels)
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;
    }

    // Function to close all panels except for the specified one
    public void CloseAllPanelsExcept(GameObject panelToKeepOpen)
    {
        foreach (GameObject panel in panels)
        {
            if (panel != panelToKeepOpen && panel != null)
            {
                panel.SetActive(false);
            }
        }
        UpdateKeyboardPanel(panelToKeepOpen);
    }

    public void UpdateInspectionText(string newText)
    {
        if (inspectionText != null)
        {
            inspectionText.text = newText;
        }
        else
        {
            Debug.LogWarning("Inspection Text component is not assigned in the UIController.");
        }
    }

    // Function to activate the TradePanel
    public void ActivateTradePanel(NPC npc)
    {
        if (tradePanel != null)
        {
            // Assuming tradePanel has a reference to TradePanelUI
            TradePanelUI tradePanelUI = tradePanel.GetComponent<TradePanelUI>();

            if (tradePanelUI != null)
            {
                tradePanelUI.SetupTrade(npc, PlayerInventory.Instance); 
                tradePanel.SetActive(true);
                greyOutPanel.SetActive(true);
                PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Trade;
            }
            else
            {
                Debug.LogWarning("TradePanelUI component not found on TradePanel.");
            }
        }
    }


    public void ActivateDialoguePanel()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
            greyOutPanel.SetActive(true);
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Dialogue;
        }
        else
        {
            Debug.LogWarning("DialoguePanel is not assigned.");
        }
    }

    public void CloseDialoguePanel()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;
        }
        else
        {
            Debug.LogWarning("DialoguePanel is not assigned.");
        }
    }

    public void ActivateDonationPanel()
    {
        if (donationPanel != null)
        {
            donationPanel.SetActive(true);
            greyOutPanel.SetActive(true);
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Donation;
        }
        else
        {
            Debug.LogWarning("DonationPanel is not assigned.");
        }
    }

    public void ActivateContainerPanel()
    {
        if (containerPanel != null)
        {
            containerPanel.SetActive(true);
            greyOutPanel.SetActive(true);
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Container;
        }
        else
        {
            Debug.LogWarning("ContainerPanel is not assigned.");
        }
    }

    public void GetCurrentDateTime()
    {
        if (TimeManager.Instance != null)
        {
            if (currentDateText != null)
            {
                currentDateText.text = TimeManager.Instance.GetCurrentDateFormatted();
            }

            if (currentTimeText != null)
            {
                currentTimeText.text = TimeManager.Instance.GetCurrentTimeFormatted();
            }

            if (currentTimeSegmentText != null)
            {
                currentTimeSegmentText.text = TimeManager.Instance.GetCurrentTimeSegment();
            }
        }
        else
        {
            Debug.LogWarning("TimeManager instance is not found.");
        }
    }

    public void ActivateVillageInfoPanel(Village villageToShow)
    {
        if (villageInfoPanel != null)
        {
            VillageInfoPanel infoPanel = villageInfoPanel.GetComponent<VillageInfoPanel>();
            if (infoPanel != null)
            {
                infoPanel.OpenVillageInfoPanel(villageToShow);
                greyOutPanel.SetActive(true);
                PlayerStats.Instance.KeyboardPanel = KeyboardPanel.MainMap;
            }
            else
            {
                Debug.LogWarning("VillageInfoPanel component is not found on the assigned villageInfoPanel.");
            }
        }
        else
        {
            Debug.LogWarning("VillageInfoPanel is not assigned.");
        }
    }

    public void UpdateMessageLogUI()
    {
        if (messageLogUIManager != null)
        {
            messageLogUIManager.DisplayMessages();
        }
        else
        {
            Debug.LogWarning("MessageLogUIManager is not assigned in the UIController.");
        }
    }

    public void UpdateTurnOrderUI()
    {
        List<string> turnOrderList = TurnManager.Instance.GetTurnOrderList();
        string turnOrderText = "Turn Order:\n" + string.Join("\n", turnOrderList);

        // Ensure PlayPanelUI is updated
        PlayPanelUI.Instance.UpdateTurnOrderText(turnOrderText);
    }


    // Method to update the color of all panels
    public void UpdatePanelColors(Color newColor)
    {
        foreach (GameObject panel in panels)
        {
            if (panel != null)
            {
                Image panelImage = panel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = newColor;
                }
                else
                {
                    Debug.LogWarning($"Panel {panel.name} does not have an Image component.");
                }
            }
        }
    }

    // Method to update the background colour
    public void UpdateBackgroundColor(Color newColor)
    {
        foreach (GameObject panel in backgroundPanels)
        {
            if (panel != null)
            {
                Image panelImage = panel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = newColor;
                }
                else
                {
                    Debug.LogWarning($"Background panel {panel.name} does not have an Image component.");
                }
            }
        }
    }

    // Method to update the map colour
    public void UpdateMapColor(Color newColor)
    {
        foreach (GameObject panel in mapPanels)
        {
            if (panel != null)
            {
                Image panelImage = panel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = newColor;
                }
                else
                {
                    Debug.LogWarning($"Map panel {panel.name} does not have an Image component.");
                }
            }
        }
    }

    // Update KeyboardPanel based on the active panel
    private void UpdateKeyboardPanel(GameObject activePanel)
    {
        if (activePanel == tradePanel)
        {
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Trade;
        }
        else if (activePanel == donationPanel)
        {
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Donation;
        }
        else if (activePanel == containerPanel)
        {
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Container;
        }
        else if (activePanel == villageInfoPanel)
        {
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.VillageInfo;
        }
        else
        {
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;
        }
    }

    // Method to update remaining turn time
    public void UpdateRemainingTurnTime(float remainingTurnTime)
    {
        if (remainingTurnTimeText != null)
        {
            remainingTurnTimeText.text = $"Remaining Turn Time: {remainingTurnTime:F2}";
        }
        else
        {
            Debug.LogWarning("Remaining Turn Time Text component is not assigned in the UIController.");
        }
    }

    // Method to open the colour settings panel
    public void OpenColourPanel()
    {
        if (colourSettingsPanel != null)
        {
            colourSettingsPanel.SetActive(true);
            greyOutPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Colour Settings Panel is not assigned.");
        }
    }

    // Method to open the old Map Editor during DebugMode
    public void OpenOldMapEditor()
    {
        if (oldMapEditior != null)
        {
            oldMapEditior.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Old Map Editor is not assigned.");
        }
    }

    // Function to open the main menu
    public void OpenMainMenu()
    {
        if (mainMenuPanelUI != null)
        {
            // Activate the main menu panel
            mainMenuPanelUI.gameObject.SetActive(true);

            ToggleGreyOutPanel();
        }
        else
        {
            Debug.LogWarning("MainMenuPanelUI is not assigned in the UIController.");
        }
    }

    // Method to fade out the splash panel and deactivate it
    public void FadeSplashPanel()
    {
        if (splashPanel != null)
        {
            DeactivateCoreGameplayPanels();
            CloseAllPanels();
            CanvasGroup canvasGroup = splashPanel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                StartCoroutine(FadeOutAndDeactivate(canvasGroup));
            }
            else
            {
                Debug.LogWarning("CanvasGroup component is missing on the splash panel.");
            }
        }
        else
        {
            Debug.LogWarning("Splash panel is not assigned.");
        }
    }

    // Coroutine to handle the fade-out effect
    private IEnumerator FadeOutAndDeactivate(CanvasGroup canvasGroup)
    {
        float startAlpha = canvasGroup.alpha;
        float rate = 1.0f / fadeDuration;

        float progress = 0.0f;
        while (progress < 1.0f)
        {
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0, progress);
            progress += Time.deltaTime / fadeDuration; 

            yield return null;
        }

        canvasGroup.alpha = 0;
        splashPanel.SetActive(false);

        // Open the main menu after the splash panel fades out
        OpenMainMenu();
    }

    // Method to activate core gameplay panels
    public void ActivateCoreGameplayPanels()
    {
        foreach (GameObject panel in coreGameplayPanels)
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }
        }

        PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;
    }

    // Method to Dectivate core gameplay panels
    public void DeactivateCoreGameplayPanels()
    {
        foreach (GameObject panel in coreGameplayPanels)
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;
    }

    public void UpdatePlayerNameButtonText()
    {
        if (PlayerStats.Instance != null)
        {
            playerNameButtonText.text = PlayerStats.Instance.PlayerCharacterName;
        }
        else
        {
            Debug.LogWarning("PlayerStats instance is not found.");
        }
    }

    // Method to toggle the PlayerCharacterPanel
    private void TogglePlayerCharacterPanel()
    {
        if (playerCharacterPanel != null)
        {
            bool isActive = playerCharacterPanel.activeSelf;
            playerCharacterPanel.SetActive(!isActive); // Toggle the panel's active state
        }
        else
        {
            Debug.LogWarning("PlayerCharacterPanel is not assigned in the UIController.");
        }
    }

    private void UpdateCurrentCellText()
    {
        if (PlayerStats.Instance != null && PlayerStats.Instance.CurrentCell != null)
        {
            Cell cell = PlayerStats.Instance.CurrentCell;

            // Get the terrain description
            string terrainDescription = cell.Terrain.ToString();

            // Determine ownership status with additional check for player ownership
            string ownershipDescription;
            if (cell.IsOwnedByPlayer)
            {
                ownershipDescription = "It is owned by you";
            }
            else if (cell.IsOwned)
            {
                ownershipDescription = $"It is owned by {cell.OwnedBy}";
            }
            else
            {
                ownershipDescription = "It is unowned";
            }

            // Get the weather description
            string weatherDescription = cell.CurrentWeather.ToString(); // Convert WeatherType enum to string

            // Combine all the information into the currentCellText
            currentCellText.text = $"You are in {cell.CellID}. It is {terrainDescription}. {ownershipDescription}. It is {weatherDescription}.";
        }
        else
        {
            currentCellText.text = "Current cell information is not available.";
        }
    }

    public void UpdatePlayerCharacterDetails()
    {
        if (PlayerStats.Instance != null)
        {
            var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;

            if (playerCharacter != null)
            {
                string mainMapPosition = $"({playerCharacter.Position.x}, {playerCharacter.Position.y})";
                string nestedMapPosition = $"({playerCharacter.NestedMapPosition.x}, {playerCharacter.NestedMapPosition.y})";

                playerCharacterDetailsText.text = $"{playerCharacter.Name} is at Main Map Position {mainMapPosition} and Nested Map Position {nestedMapPosition}.";
            }
            else
            {
                playerCharacterDetailsText.text = "Player character information is not available.";
            }
        }
        else
        {
            playerCharacterDetailsText.text = "Player stats are not available.";
        }
    }

    public void ToggleInventoryPanel()
    {
        GameObject inventoryPanel = GetPanelByName("InventoryPanel");

        if (inventoryPanel != null)
        {
            bool isActive = !inventoryPanel.activeSelf;
            inventoryPanel.SetActive(isActive);

            // Toggle the greyOutPanel based on the inventory panel's active state
            greyOutPanel.SetActive(isActive);

            // Update the KeyboardPanel in PlayerStats
            if (isActive)
            {
                PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Inventory;

                // Access the InventoryUI component and trigger necessary updates when the panel opens
                InventoryUI inventoryUI = inventoryPanel.GetComponent<InventoryUI>();
                if (inventoryUI != null)
                {
                    // Refresh the items list when the panel is opened
                    inventoryUI.RefreshItemsList();
                    inventoryUI.UpdateItemListDisplay();
                    inventoryUI.PopulateEquipmentSlots();
                }

                Debug.Log("Inventory panel opened.");
            }
            else
            {
                PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;
                Debug.Log("Inventory panel closed.");
            }
        }
        else
        {
            Debug.LogWarning("InventoryPanel is not assigned.");
        }
    }


    public void OnPlayerDeath(Character playerCharacter)
    {
        if (deathPanel != null)
        {
            // Close all other panels to ensure only the death panel is visible
            CloseAllPanels();
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Death;

            // Activate the death panel and pass the player character data
            DeathPanelUI deathPanelUI = deathPanel.GetComponent<DeathPanelUI>();

            if (deathPanelUI != null)
            {
                deathPanelUI.SetPlayerCharacterData(playerCharacter);
                deathPanelUI.ShowDeathPanel();
                greyOutPanel.SetActive(true);
            }
            else
            {
                Debug.LogWarning("DeathPanelUI component is missing on the death panel.");
            }
        }
        else
        {
            Debug.LogWarning("DeathPanel is not assigned in the UIController.");
        }
    }

    // Add this method to your UIController class
    public void OpenSmithingPanel()
    {
        if (smithingPanel != null)
        {
            CloseAllPanelsExcept(smithingPanel); // Optional: Close other panels when opening smithing panel
            smithingPanel.SetActive(true);
            ActivateGreyOutPanel(true);
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Smithing; // Ensure you have this state in your KeyboardPanel enum
        }
        else
        {
            Debug.LogWarning("SmithingPanel is not assigned in the UIController.");
        }
    }

    public void OpenCraftingPanel()
    {
        if (craftingPanel != null)
        {
            CloseAllPanelsExcept(craftingPanel); // Optional: Close other panels when opening crafting panel
            craftingPanel.SetActive(true);
            ActivateGreyOutPanel(true);
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Crafting; // Ensure you have this state in your KeyboardPanel enum
        }
        else
        {
            Debug.LogWarning("CraftingPanel is not assigned in the UIController.");
        }
    }

    public void OpenCookingPanel()
    {
        if (cookingPanel != null)
        {
            CloseAllPanelsExcept(cookingPanel); // Optional: Close other panels when opening cooking panel
            cookingPanel.SetActive(true);
            ActivateGreyOutPanel(true);
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Cooking; // Ensure you have this state in your KeyboardPanel enum
        }
        else
        {
            Debug.LogWarning("CookingPanel is not assigned in the UIController.");
        }
    }

    public void OpenHintPanel()
    {
        if (cookingPanel != null)
        {
            CloseAllPanelsExcept(hintPanel); // Optional: Close other panels when opening cooking panel
            hintPanel.SetActive(true);
            ActivateGreyOutPanel(true);
            PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Hint; // Ensure you have this state in your KeyboardPanel enum
        }
        else
        {
            Debug.LogWarning("CookingPanel is not assigned in the UIController.");
        }
    }

    // Method to toggle the GreyOutPanel
    public void ToggleGreyOutPanel()
    {
        if (greyOutPanel != null)
        {
            bool isActive = greyOutPanel.activeSelf;  // Get the current state
            greyOutPanel.SetActive(!isActive);  // Toggle the active state
        }
        else
        {
            Debug.LogWarning("GreyOutPanel is not assigned in the UIController.");
        }
    }

    // Function to show a simple popup with GreyOutPanel activation
    public void ShowSimplePopup(string message)
    {
        if (multipurposePopupPanelUI != null)
        {
            ActivateGreyOutPanel(true); // Activate GreyOutPanel
            multipurposePopupPanelUI.ShowMessage(message);
        }
        else
        {
            Debug.LogWarning("MultipurposePopupPanelUI is not assigned.");
        }
    }

    // Function to show a confirmation popup with GreyOutPanel activation
    public void ShowConfirmationPopup(string message, System.Action onConfirm, System.Action onCancel)
    {
        if (multipurposePopupPanelUI != null)
        {
            ActivateGreyOutPanel(true); // Activate GreyOutPanel
            multipurposePopupPanelUI.ShowConfirmation(message,
                () => {
                    onConfirm();
                    DeactivateGreyOutPanel(); // Deactivate GreyOutPanel after confirming
                },
                () => {
                    onCancel();
                    DeactivateGreyOutPanel(); // Deactivate GreyOutPanel after cancelling
                });
        }
        else
        {
            Debug.LogWarning("MultipurposePopupPanelUI is not assigned.");
        }
    }

    public void OpenInspectionPanel()
    {
        // Check if the InspectionPanel is already active
        if (inspectionPanel.activeSelf)
        {
            return; // If it's active, do nothing
        }

        // If it's not active, close the DescriptionPanel and open the InspectionPanel
        descriptionPanel.SetActive(false);
        inspectionPanel.SetActive(true);

        // Update button states: grey out the InspectionButton, enable the DescriptionButton
        inspectionButton.interactable = false;
        descriptionButton.interactable = true;
    }

    // Function to activate or deactivate the GreyOutPanel
    private void ActivateGreyOutPanel(bool isActive)
    {
        if (greyOutPanel != null)
        {
            greyOutPanel.SetActive(isActive);
        }
        else
        {
            Debug.LogWarning("GreyOutPanel is not assigned.");
        }
    }

    public void DisablePlayButton()
    {
        if (playButton != null)
        {
            // Disable the button's interaction
            playButton.interactable = false;

            // Optionally, change the visual appearance to grey out the button text
            TextMeshProUGUI buttonText = playButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.color = new Color32(128, 128, 128, 255); // Set the text color to grey
            }
        }
        else
        {
            Debug.LogWarning("PlayButton is not assigned in the UIController.");
        }
    }


    // Function to deactivate the GreyOutPanel
    public void DeactivateGreyOutPanel()
    {
        ActivateGreyOutPanel(false);
    }



    public void ApplyPanelShakeOnDamage(float strength, float duration)
    {
        if (panelToShake == null)
        {
            Debug.LogWarning("UIController: No panel assigned for shaking!");
            return;
        }

        StartCoroutine(ShakePanelCoroutine(panelToShake.GetComponent<RectTransform>(), strength, duration));
    }

    private IEnumerator ShakePanelCoroutine(RectTransform panel, float strength, float duration)
    {
        Vector2 originalPosition = panel.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            panel.anchoredPosition = originalPosition + GetRandomOffset(strength);
            elapsed += Time.deltaTime;
            yield return null;
        }

        panel.anchoredPosition = originalPosition; // Reset position after shake
    }

    private Vector2 GetRandomOffset(float strength)
    {
        return new Vector2(UnityEngine.Random.Range(-strength, strength), UnityEngine.Random.Range(-strength, strength));
    }

    private void AdjustTextBrightness()
    {
        if (brightnessAffectedPanels == null || brightnessAffectedPanels.Count == 0) return;

        TimeManager.TimeSegment currentSegment = TimeManager.Instance.currentSegment;

        // Determine target brightness based on time of day
        float targetAlpha = (currentSegment == TimeManager.TimeSegment.Night || currentSegment == TimeManager.TimeSegment.Evening)
            ? 0.5f  // Darker at Night/Evening
            : 1f;    // Normal brightness at Morning/Afternoon

        Debug.Log($"[UIController] Adjusting text brightness to {targetAlpha} for {currentSegment}");

        // Call UIEffects method to adjust text brightness in the selected panels
        UIEffects.Instance.AdjustTextBrightnessInPanels(brightnessAffectedPanels, targetAlpha, 0.5f);
    }

}