using UnityEngine;
using TMPro;

public class HintPanelUI : MonoBehaviour
{
    // Reference to the TextMeshPro component
    public TextMeshProUGUI hintsText;

    // List of tips
    private string[] tips = new string[]
    {
        "1. Movement: Use WASD or the arrow keys to move around.",
        "2. Shift for Orientation: Hold Shift to change the direction you're facing without moving. This will also refresh the Adaptive Action Menu.",
        "3. Entering Areas: Press E or press the 'Enter' button at the bottom-right to access new areas. This is a key part of the gameplay loop.",
        "4. Character Panels: Click your character's name on the left side to switch between helpful panels.",
        "5. Stuck Menus: If any menus freeze or the screen remains greyed out, press Escape to reset everything to normal.",
        "6. Press H to see these hints again."
    };

    // Start is called before the first frame update
    void Start()
    {
        DisplayHints();
    }

    // Function to display the hints
    void DisplayHints()
    {
        // Clear the text box
        hintsText.text = "";

        // Loop through the tips array and display each tip on a new line
        foreach (string tip in tips)
        {
            hintsText.text += tip + "\n\n";
        }

        // Ensure the panel is active
        gameObject.SetActive(true);
    }

    // Function to close the hint panel
    public void Close()
    {
        // Clear the hints text
        hintsText.text = "";

        PlayerStats.Instance.KeyboardPanel = KeyboardPanel.Default;

        // Optionally deactivate the entire panel
        gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        // Check if the player presses H to display the hints again
        if (Input.GetKeyDown(KeyCode.H))
        {
            DisplayHints();
        }

        // Optional: Close the panel with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }
}
