using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System;

public class RoadGeneratorUI : MonoBehaviour
{
    public TMP_InputField startInputField; // Use TMP_InputField for text input
    public TMP_InputField endInputField;   // Use TMP_InputField for text input
    public Button generateRoadButton;
    public RoadGenerator roadGenerator;
    public MapGenerator mapGenerator; // Reference to your MapGenerator

    void Start()
    {
        generateRoadButton.onClick.AddListener(OnGenerateRoadClicked);
    }

    void OnGenerateRoadClicked()
    {
        GameManager.Instance.manualRoadOverride = true;

        // Directly use the text from the input fields to parse coordinates
        Vector2Int startCoords = ParseCoordinatesFromInput(startInputField.text);
        Vector2Int endCoords = ParseCoordinatesFromInput(endInputField.text);

        GameManager.Instance.startCellCoordinates = startCoords;
        GameManager.Instance.endCellCoordinates = endCoords;

        roadGenerator.StartRoadGeneration();
    }

    Vector2Int ParseCoordinatesFromInput(string input)
    {
        // Expected input format "x,y"
        string[] parts = input.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
        {
            return new Vector2Int(x, y);
        }
        else
        {
            Debug.LogError("Invalid input format. Please use 'x,y'.");
            return Vector2Int.zero; // Return a default value or handle this case as needed
        }
    }
}
