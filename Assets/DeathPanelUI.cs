using UnityEngine;
using TMPro; // For TextMeshPro components

public class DeathPanelUI : MonoBehaviour
{
    // UI elements to display player character information
    public TMP_Text playerNameText;
    public TMP_Text gravesiteMapText;

    // Method to update the death panel with the player's information
    public void SetPlayerCharacterData(Character playerCharacter)
    {
        if (playerCharacter != null)
        {
            // Update the text fields with the player's name
            playerNameText.text = $"Here lies {playerCharacter.Name}...";

            // Generate the gravesite map when the player dies
            string graveMap = GenerateGravesiteMap();
            gravesiteMapText.text = graveMap; // Display the map in the text field
        }
        else
        {
            Debug.LogWarning("PlayerCharacter data is null.");
        }
    }

    // Optional: You can add any additional logic or animations for when the panel is shown
    public void ShowDeathPanel()
    {
        UIController.Instance.ToggleGreyOutPanel();
        gameObject.SetActive(true);
    }

    // Method to generate the 7x7 gravesite map with a road "R" and grave "G"
    private string GenerateGravesiteMap()
    {
        // Define the gravesite map with a road leading up to the grave
        string gravesiteMap =
            "<color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color>\n" +
            "<color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color>\n" +
            "<color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color>\n" +
            "<color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#808080>G</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color>\n" +
            "<color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#8B4513>R</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color>\n" +
            "<color=#8B4513>R</color> <color=#8B4513>R</color> <color=#8B4513>R</color> <color=#8B4513>R</color> <color=#8B4513>R</color> <color=#8B4513>R</color> <color=#8B4513>R</color>\n" +
            "<color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color> <color=#90EE90>L</color>";

        return gravesiteMap;
    }
}
