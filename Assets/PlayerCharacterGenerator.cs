using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCharacterGenerator : MonoBehaviour
{
    public void GenerateNewPlayerCharacter()
    {
        Debug.Log("PC1: Generating new player character...");

        if (PlayerStats.Instance == null)
        {
            Debug.LogError("PC1a: PlayerStats.Instance is null!");
            return;
        }

        // Confirm GameManager.Instance is not null if used within GenerateUniqueID()
        if (GameManager.Instance == null)
        {
            Debug.LogError("PC1b: GameManager.Instance is null!");
            return;
        }

        PlayerCharacter newPlayer = new PlayerCharacter();
        Debug.Log("PC2: Created new PlayerCharacter instance");

        newPlayer.FirstName = "Bobby";
        newPlayer.Surname = "B";
        newPlayer.PlayerCharacterID = GenerateUniqueID();
        newPlayer.IsActive = true;
        Debug.Log("PC3: Generated unique ID: " + newPlayer.PlayerCharacterID);

        GenerateRandomStats(newPlayer);
        Debug.Log("PC4: Generated random stats for " + newPlayer.FirstName);



        PlayerStats.Instance.AddPlayerCharacter(newPlayer);
        Debug.Log("PC5: Added " + newPlayer.FirstName + " to PlayerStats");

    }



    private int GenerateUniqueID()
    {
        int PlayerID = GameManager.Instance.GetPlayerCharacterID();
        return PlayerID; 
    }

    private void GenerateRandomStats(PlayerCharacter player)
    {
        // Implement logic to generate random stats for the player
        // Example:
        player.Strength = Random.Range(1, 11); // Example: Generating strength between 1 and 10
        player.Dexterity = Random.Range(1, 11);
        player.Constitution = Random.Range(1, 11);
        player.Intelligence = Random.Range(1, 11);
        player.Wisdom = Random.Range(1, 11);
        player.Charisma = Random.Range(1, 11);
    }
}
