using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayPanelUI : MonoBehaviour
{
    public static PlayPanelUI Instance { get; private set; }

    public List<GameObject> panels;

    // Existing UI elements
    public Button descriptionButton;
    public Button inspectionButton;
    public GameObject descriptionPanel;
    public GameObject inspectionPanel;

    // New Combat Panel UI Elements
    public Button combatButton;
    public GameObject combatPanel;
    public TMP_Text turnOrderText; // Display for Turn Order

    public Button openColourPanelButton;
    public GameObject colourSettingsPanel;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Add listeners for panel toggle buttons
        descriptionButton.onClick.AddListener(() => ToggleTabs(descriptionButton, descriptionPanel, inspectionButton, inspectionPanel, combatButton, combatPanel));
        inspectionButton.onClick.AddListener(() => ToggleTabs(inspectionButton, inspectionPanel, descriptionButton, descriptionPanel, combatButton, combatPanel));
        combatButton.onClick.AddListener(() => ToggleTabs(combatButton, combatPanel, descriptionButton, descriptionPanel, inspectionButton, inspectionPanel));

        // Add listener for opening the ColourPanel
        if (openColourPanelButton != null)
        {
            openColourPanelButton.onClick.AddListener(OpenColourPanel);
        }
        else
        {
            Debug.LogWarning("openColourPanelButton is not assigned.");
        }

        // Initially, show only one panel and disable its button
        ToggleTabs(descriptionButton, descriptionPanel, inspectionButton, inspectionPanel, combatButton, combatPanel);
    }

    // Function to toggle between tabs
    public void ToggleTabs(Button clickedButton, GameObject panelToOpen, Button otherButton1, GameObject panelToClose1, Button otherButton2, GameObject panelToClose2)
    {
        // Close other panels
        panelToClose1.SetActive(false);
        panelToClose2.SetActive(false);

        // Open the selected panel
        panelToOpen.SetActive(true);

        // Disable the clicked button and enable the others
        clickedButton.interactable = false;
        otherButton1.interactable = true;
        otherButton2.interactable = true;
    }

    // Function to update Turn Order text
    public void UpdateTurnOrderText(string turnOrder)
    {
        if (turnOrderText != null)
        {
            turnOrderText.text = turnOrder;
        }
    }

    public void OpenColourPanel()
    {
        if (colourSettingsPanel != null)
        {
            colourSettingsPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("ColourSettingsPanel reference is not assigned.");
        }
    }

}
