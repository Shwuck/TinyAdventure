using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AnimalManager : MonoBehaviour
{
    public static AnimalManager Instance { get; private set; }

    private List<Animal> allAnimals = new List<Animal>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlaceAnimalsForNestedArea(INestedArea nestedArea)
    {
        if (nestedArea.GeneratedAnimals == null || nestedArea.GeneratedAnimals.Count == 0)
        {
            Debug.Log("No animals to place in this nested area.");
            return;
        }

        int maxAnimalsToPlace = Mathf.Min(nestedArea.MaxAnimalsToPlace, nestedArea.GeneratedAnimals.Count);
        Debug.Log($"Placing up to {maxAnimalsToPlace} animals in the nested area.");

        nestedArea.GeneratedAnimals = nestedArea.GeneratedAnimals.OrderBy(a => UnityEngine.Random.value).ToList();
        // Loop through each animal in the GeneratedAnimals list, but respect MaxAnimalsToPlace
        int placedAnimals = 0;
        foreach (var animal in nestedArea.GeneratedAnimals.ToList())
        {
            if (placedAnimals >= maxAnimalsToPlace)
            {
                Debug.Log($"Reached MaxAnimalsToPlace limit: {maxAnimalsToPlace}");
                break;
            }

            PlaceAnimal(nestedArea, animal);
            placedAnimals++;
        }

        Debug.Log($"{placedAnimals} animals have been placed in the nested area.");
    }


    public void PlaceAnimal(INestedArea nestedArea, Animal animal)
    {
        Debug.Log($"Placing Animal - '{animal.Name}' in nested area.");
        Vector2Int animalPosition = DetermineAnimalPositionInNestedArea(nestedArea);

        int attempts = 0;
        while (!nestedArea.IsValidPosition(animalPosition) || !nestedArea.IsPassable(animalPosition) || HasCollision(nestedArea, animalPosition))
        {
            animalPosition = AdjustAnimalPosition(nestedArea, animalPosition);
            attempts++;
            if (attempts > 5)
            {
                Debug.LogError($"Failed to place '{animal.Name}' after {attempts} attempts.");
                return;
            }
        }

        nestedArea.UpdateCharacterPosition(animal, animalPosition);
        animal.NestedMapPosition = animalPosition;
        animal.IsInNestedArea = true;
        animal.CurrentNestedArea = nestedArea;
        animal.CanLeaveArea = true;
        Debug.Log($"'{animal.Name}' placed at {animalPosition} within nested area.");

        if (!TurnManager.Instance.IsCharacterRegistered(animal))
        {
            TurnManager.Instance.RegisterCharacter(animal);  // Updated to pass the Animal object directly
            Debug.Log($"Registering Animal '{animal.Name}' with TurnManager.");
        }
        else
        {
            Debug.Log($"Animal '{animal.Name}' is already registered with the TurnManager.");
        }
    }

    public void RemoveAnimalFromNestedArea(Animal animal)
    {
        if (animal.IsInNestedArea && animal.CurrentNestedArea != null)
        {
            INestedArea nestedArea = animal.CurrentNestedArea;
            Cell[,] nestedMap = nestedArea.GetNestedMap();

            if (animal.NestedMapPosition.x >= 0 && animal.NestedMapPosition.x < nestedMap.GetLength(0) &&
                animal.NestedMapPosition.y >= 0 && animal.NestedMapPosition.y < nestedMap.GetLength(1))
            {
                Cell cell = nestedMap[animal.NestedMapPosition.x, animal.NestedMapPosition.y];
                cell.isNPCPresent = false;
                cell.isPassable = true;
                cell.Objects.Remove(animal);
                Debug.Log($"Animal '{animal.Name}' removed from nested area at position {animal.NestedMapPosition}");
            }
            else
            {
                Debug.LogWarning($"Animal '{animal.Name}' position ({animal.NestedMapPosition}) is outside the bounds of the nested area.");
            }

            animal.IsInNestedArea = false;
            animal.CurrentNestedArea = null;
            TurnManager.Instance.DeregisterCharacter(animal);  // Updated to pass the Animal object directly
            Debug.Log($"Animal '{animal.Name}' deregistered from turn manager.");

            // Remove from the GeneratedAnimals list
            nestedArea.GeneratedAnimals.Remove(animal);
        }
        else
        {
            Debug.LogWarning($"Animal '{animal.Name}' is not in a nested area.");
        }
    }

    private float CalculateTurnDuration(float speed)
    {
        return Mathf.Max(0.1f, 1.0f / speed);
    }

    public Animal GetAnimalByID(int animalID)
    {
        return PermaLists.Instance.AllAnimals.FirstOrDefault(animal => animal.AnimalID == animalID);
    }

    private Vector2Int DetermineAnimalPositionInNestedArea(INestedArea nestedArea)
    {
        Cell[,] nestedMap = nestedArea.GetNestedMap();
        int width = nestedMap.GetLength(0);
        int height = nestedMap.GetLength(1);

        Vector2Int position;
        do
        {
            int x = Random.Range(0, width);
            int y = Random.Range(0, height);
            position = new Vector2Int(x, y);
        } while (!nestedArea.IsValidPosition(position) || !nestedArea.IsPassable(position) || HasCollision(nestedArea, position));

        return position;
    }

    private Vector2Int AdjustAnimalPosition(INestedArea nestedArea, Vector2Int animalPosition)
    {
        int width = nestedArea.GetNestedMap().GetLength(0);
        int height = nestedArea.GetNestedMap().GetLength(1);

        int offsetX = Random.Range(-1, 2);
        int offsetY = Random.Range(-1, 2);
        Vector2Int adjustedPosition = animalPosition + new Vector2Int(offsetX, offsetY);

        adjustedPosition.x = Mathf.Clamp(adjustedPosition.x, 0, width - 1);
        adjustedPosition.y = Mathf.Clamp(adjustedPosition.y, 0, height - 1);

        return adjustedPosition;
    }

    public bool HasCollision(INestedArea nestedArea, Vector2Int position)
    {
        Cell cell = nestedArea.GetCellAtPosition(position);
        return cell != null && cell.Objects.Any(obj => obj is Animal);
    }
}