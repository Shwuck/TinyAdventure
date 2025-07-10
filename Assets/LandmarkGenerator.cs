using System.Collections.Generic;
using UnityEngine;

public class LandmarkGenerator : MonoBehaviour
{
    public int maximumLandmarks = 10; // Control the max number of landmarks to place
    public int maxAttempts = 500; // Max attempts to find suitable placement
    public List<LandmarkCreationData> availableLandmarks;

    public void GenerateLandmarks()
    {

        availableLandmarks = PermaLists.Instance.LandmarkCreationData;

        int landmarksPlaced = 0;

        for (int attempt = 0; attempt < maxAttempts && landmarksPlaced < maximumLandmarks; attempt++)
        {
            // Pick a random landmark to place, filtering on "Basic" if required
            LandmarkCreationData landmark = ChooseLandmarkByRarity();

            // Find a valid cell based on the landmark's requirements
            Cell validCell = FindValidCellForLandmark(landmark);

            if (validCell != null)
            {
                PlaceLandmark(landmark, validCell);
                landmarksPlaced++;
                Debug.Log($"Landmark '{landmark.LandmarkName}' placed at {validCell.Coordinates}.");
            }
        }

        if (landmarksPlaced == 0)
        {
            Debug.LogWarning("No valid cells found for landmarks after max attempts.");
        }
    }

    // Randomly chooses a landmark with rarity weighting, applying the "Basic" filter if necessary
    private LandmarkCreationData ChooseLandmarkByRarity()
    {
        List<LandmarkCreationData> filteredLandmarks = new List<LandmarkCreationData>();

        // Filter landmarks based on whether we're using "Basic" landmarks only
        foreach (var landmark in availableLandmarks)
        {
            if (!GameManager.Instance.UseBasicLandmarks || landmark.Basic)
            {
                filteredLandmarks.Add(landmark);
            }
        }

        if (filteredLandmarks.Count == 0)
        {
            Debug.LogWarning("No landmarks available based on the filtering criteria.");
            return null;
        }

        // Weighted selection based on rarity
        List<LandmarkCreationData> weightedLandmarks = new List<LandmarkCreationData>();
        foreach (var landmark in filteredLandmarks)
        {
            int rarityWeight = GetRarityWeight(landmark.Rarity);
            for (int i = 0; i < rarityWeight; i++)
            {
                weightedLandmarks.Add(landmark);
            }
        }

        // Randomly select a landmark from the weighted list
        return weightedLandmarks[Random.Range(0, weightedLandmarks.Count)];
    }

    // Get a weight based on rarity
    private int GetRarityWeight(LandmarkRarity rarity)
    {
        switch (rarity)
        {
            case LandmarkRarity.Common: return 50;
            case LandmarkRarity.Uncommon: return 30;
            case LandmarkRarity.Rare: return 15;
            case LandmarkRarity.VeryRare: return 5;
            case LandmarkRarity.Legendary: return 1;
            default: return 1;
        }
    }

    // Find a valid cell that matches the landmark's requirements
    private Cell FindValidCellForLandmark(LandmarkCreationData landmark)
    {
        List<Cell> validCells = new List<Cell>();

        // Search the entire map for valid cells
        foreach (var cell in MapGenerator.Instance.allCells)
        {
            if (IsCellValidForLandmark(cell, landmark))
            {
                validCells.Add(cell);
            }
        }

        // Return a random valid cell if available
        if (validCells.Count > 0)
        {
            return validCells[Random.Range(0, validCells.Count)];
        }

        return null;
    }

    // Check if a cell is valid for a given landmark
    private bool IsCellValidForLandmark(Cell cell, LandmarkCreationData landmark)
    {
        // Check if the terrain matches any of the landmark's applicable terrains
        if (!landmark.ApplicableTerrains.Contains(cell.Terrain))
        {
            return false;
        }

        // Check if the climate matches any of the landmark's applicable climates
        if (!landmark.ApplicableClimates.Contains(GameManager.Instance.climate))
        {
            return false;
        }

        // Ensure the cell is passable and does not already have a landmark
        if (!cell.isPassable || cell.HasLandmark)
        {
            return false;
        }

        return true;
    }

    // Place a landmark on the map by setting the cell's landmark attributes
    private void PlaceLandmark(LandmarkCreationData landmark, Cell cell)
    {
        cell.HasLandmark = true;
        cell.LandmarkName = "Landmark";

        // You could also store the landmark in the PermaLists or another global system if needed
    }
}
