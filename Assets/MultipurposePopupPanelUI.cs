using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Import TextMeshPro namespace

public class MultipurposePopupPanelUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject popupPanel; // The panel to show/hide
    public TextMeshProUGUI messageText; // The TextMeshProUGUI element for the message
    public Button confirmButton; // Optional: confirmation button
    public Button cancelButton; // Optional: cancel button

    [Header("Settings")]
    public bool fadeInOut = false; // Enable/disable fade animation
    public float fadeDuration = 0.5f;

    // Start is called before the first frame update
    void Start()
    {
        // Initialize the panel to be hidden
        HidePopup();

        // Optionally, you can assign the button actions here.
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
    }

    // Function to show a simple message on the panel
    public void ShowMessage(string message)
    {
        messageText.text = message;
        ShowPopup();
    }

    // Function to show a confirmation message with "Yes" and "No" buttons
    public void ShowConfirmation(string message, System.Action onConfirm, System.Action onCancel)
    {
        messageText.text = message;

        // Ensure buttons are enabled
        confirmButton.gameObject.SetActive(true);
        cancelButton.gameObject.SetActive(true);

        // Assign the button actions
        confirmButton.onClick.RemoveAllListeners(); // Clear previous listeners
        cancelButton.onClick.RemoveAllListeners();

        confirmButton.onClick.AddListener(() => { onConfirm(); HidePopup(); });
        cancelButton.onClick.AddListener(() => { onCancel(); HidePopup(); });

        ShowPopup();
    }

    // Function to show the popup panel
    private void ShowPopup()
    {
        // Ensure the panel is active before starting any coroutines
        popupPanel.SetActive(true);
        PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Popup;

        if (fadeInOut)
        {
            // Start the fade-in effect
            StartCoroutine(FadeInPanel());
        }
        else
        {
            // If no fade-in effect, the panel will just be visible immediately
            popupPanel.SetActive(true);
        }
    }


    // Function to hide the popup panel
    private void HidePopup()
        {
        // Ensure the panel is active before starting any coroutines
        popupPanel.SetActive(false);
        PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;

        if (fadeInOut)
            {
                StartCoroutine(FadeOutPanel());
            }
            else
            {
                popupPanel.SetActive(false);
            }
        }

        // Fade in coroutine
        IEnumerator FadeInPanel()
        {
            popupPanel.SetActive(true);
            CanvasGroup canvasGroup = popupPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) yield break;

            float elapsedTime = 0;
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
                yield return null;
            }
        }

        // Fade out coroutine
        IEnumerator FadeOutPanel()
        {
            CanvasGroup canvasGroup = popupPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) yield break;

            float elapsedTime = 0;
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                canvasGroup.alpha = 1 - Mathf.Clamp01(elapsedTime / fadeDuration);
                yield return null;
            }

            popupPanel.SetActive(false);
        }

        // Optional: actions for confirmation and cancellation
        private void OnConfirm()
        {
            Debug.Log("Confirmed!");
            HidePopup();
        }

        private void OnCancel()
        {
            Debug.Log("Cancelled!");
            HidePopup();
        }
    }
