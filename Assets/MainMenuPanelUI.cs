using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuPanelUI : MonoBehaviour
{
    public GameObject gameSelectPanel;
    public GameObject customGamePanel;
    public GameObject mapEditorPanel;
    public GameObject characterCreationPanel;
    public GameObject mainMenuStartGamePanel;

    public Button gameSelectButton;
    public Button customGameButton;
    public Button mapEditorButton;
    public Button characterCreatorButton;
    public Button startCustomGameButton;
    public Button startQuickGameButton;
    public Button debugStartGameButton;
    public Button mapEditorBackButton;
    public Button characterCreatorBackButton;

    public TextMeshProUGUI mapPreviewTextBox;

    private void Start()
    {
        // Assign listeners to buttons
        gameSelectButton.onClick.AddListener(OpenGameSelectPanel);
        customGameButton.onClick.AddListener(OpenCustomGamePanel);
        mapEditorBackButton.onClick.AddListener(OpenCustomGamePanel);
        characterCreatorBackButton.onClick.AddListener(OpenCustomGamePanel);
        mapEditorButton.onClick.AddListener(OpenMapEditorPanel);
        startCustomGameButton.onClick.AddListener(StartCustomGame);
        startQuickGameButton.onClick.AddListener(StartQuickGame);
        characterCreatorButton.onClick.AddListener(OpenCharacterCreationPanel);

        // Ensure all panels are closed at the start
        CloseAllPanels();

        mainMenuStartGamePanel.SetActive(true);

        // Set the active state of the DebugStartGame button based on the debug mode
        if (GameManager.Instance != null && GameManager.Instance.isDebugModeOn)
        {
            // Check if the DebugStartGame is available
            if (GameManager.Instance.DebugStartGameAvailable)
            {
                debugStartGameButton.gameObject.SetActive(true); // Make the button visible
                debugStartGameButton.interactable = true; // Enable interaction
                debugStartGameButton.onClick.AddListener(StartDebugGame); // Assign the click listener
            }
            else
            {
                debugStartGameButton.gameObject.SetActive(true); // Make the button visible
                debugStartGameButton.interactable = false; // Disable interaction (grey out the button)
            }
        }
        else
        {
            debugStartGameButton.gameObject.SetActive(false); // Hide the button entirely if debug mode is off
        }

        // Update the map preview text
        UpdateMapPreview();

        // Initialize the startCustomGameButton state
        UpdateStartCustomGameButtonState();
    }


    private void Update()
    {
        // Update the button state each frame
        UpdateStartCustomGameButtonState();
    }

    private void UpdateStartCustomGameButtonState()
    {
        if (GameManager.Instance.MapSet && GameManager.Instance.PlayerSet)
        {
            startCustomGameButton.interactable = true;
        }
        else
        {
            startCustomGameButton.interactable = false;
        }
    }

    public void OpenGameSelectPanel()
    {
        CloseAllPanels();
        mainMenuStartGamePanel.SetActive(false);
        gameSelectPanel.SetActive(true);
    }

    public void OpenCustomGamePanel()
    {
        CloseAllPanels();
        UpdateMapPreview();
        customGamePanel.SetActive(true);
    }

    public void OpenMapEditorPanel()
    {
        CloseAllPanels();
        mapEditorPanel.SetActive(true);
    }

    public void OpenCharacterCreationPanel()
    {
        CloseAllPanels();
        characterCreationPanel.SetActive(true);
    }

    public void StartCustomGame()
    {
        if (startCustomGameButton.interactable)
        {
            Debug.Log("Game Started");

            // Close the MainMenuPanelUI
            this.gameObject.SetActive(false);

            // Activate core gameplay panels
            if (UIController.Instance != null)
            {
                UIController.Instance.ActivateCoreGameplayPanels();
            }
            else
            {
                Debug.LogError("UIController instance is null.");
            }
        }
    }

    public void StartQuickGame()
    {
        // Add logic for starting a quick game here
    }

    private void CloseAllPanels()
    {
        gameSelectPanel.SetActive(false);
        customGamePanel.SetActive(false);
        mapEditorPanel.SetActive(false);
        characterCreationPanel.SetActive(false);
    }

    private void DisplayMapPreview()
    {
        if (GameManager.Instance != null)
        {
            mapPreviewTextBox.text = GameManager.Instance.MapPreviewText;
        }
        else
        {
            Debug.LogError("GameManager instance is null.");
        }
    }

    public void UpdateMapPreview()
    {
        if (GameManager.Instance != null)
        {
            mapPreviewTextBox.text = GameManager.Instance.MapPreviewText;
        }
        else
        {
            Debug.LogError("GameManager instance is null.");
        }
    }

    public void StartDebugGame()
    {
        Debug.Log("Starting Debug Game");

        // Generate the map
        if (MapGenerator.Instance != null)
        {
            MapGenerator.Instance.GenerateMap();
        }
        else
        {
            Debug.LogError("MapGenerator instance is null.");
        }

        // Close the MainMenuPanelUI
        this.gameObject.SetActive(false);

        // Activate core gameplay panels
        if (UIController.Instance != null)
        {
            UIController.Instance.ActivateCoreGameplayPanels();
        }
        else
        {
            Debug.LogError("UIController instance is null.");
        }

        UIController.Instance.OpenOldMapEditor();
    }
}
