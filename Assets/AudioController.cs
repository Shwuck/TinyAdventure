using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioController : MonoBehaviour
{
    public static AudioController Instance { get; private set; }

    public AudioSource moveSoundSource;
    public AudioClip[] movementSounds;

    void Awake()
    {
        // Ensure this is the only instance of the AudioController
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Optional: Keep this object alive when loading new scenes
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayMovementSound()
    {
        if (movementSounds.Length > 0)
        {
            int index = Random.Range(0, movementSounds.Length); // Randomly select an index
            moveSoundSource.clip = movementSounds[index]; // Set the AudioSource clip to the selected sound
            moveSoundSource.Play(); // Play the sound
        }
    }
}
