using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI; // For Button

public class CustomGameSetupPanelUI : MonoBehaviour
{
    // Reference to the GameStartButton
    public Button gameStartButton;

    // Start is called before the first frame update
    void Start()
    {
        // Disable the button initially
        gameStartButton.interactable = false;
    }

    // Update is called once per frame
    void Update()
    {
        // Check if both MapSet and PlayerSet are true
        if (GameManager.Instance.MapSet && GameManager.Instance.PlayerSet)
        {
            gameStartButton.interactable = true;
        }
        else
        {
            gameStartButton.interactable = false;
        }
    }

    // Attach this method to the button's OnClick event in the Inspector
    public void OnGameStartButtonClicked()
    {
        if (gameStartButton.interactable)
        {
            Debug.Log("Game Started");
            // Call your Start Game method here
        }
    }
}
