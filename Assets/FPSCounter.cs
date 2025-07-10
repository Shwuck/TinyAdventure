using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    public TextMeshProUGUI fpsText; // Reference to a TextMeshProUGUI component where you want to display FPS

    private float deltaTime = 0.0f;

    private void Start()
    {
        // Initialize the visibility of the fpsText based on debug mode
        if (fpsText != null)
        {
            fpsText.gameObject.SetActive(GameManager.Instance != null && GameManager.Instance.isDebugModeOn);
        }
    }

    private void Update()
    {
        // Check if the game is in debug mode before updating the FPS counter
        if (GameManager.Instance != null && GameManager.Instance.isDebugModeOn)
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            float fps = 1.0f / deltaTime;
            fpsText.text = $"FPS: {Mathf.Round(fps)}";

            // Ensure the fpsText is active in case debug mode was toggled on after start
            if (!fpsText.gameObject.activeSelf)
            {
                fpsText.gameObject.SetActive(true);
            }
        }
        else
        {
            // Deactivate the fpsText if not in debug mode
            if (fpsText != null && fpsText.gameObject.activeSelf)
            {
                fpsText.gameObject.SetActive(false);
            }
        }
    }
}
