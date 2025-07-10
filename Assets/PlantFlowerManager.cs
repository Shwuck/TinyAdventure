using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlantFlowerManager : MonoBehaviour
{
    // Singleton pattern
    private static PlantFlowerManager _instance;
    public static PlantFlowerManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<PlantFlowerManager>();
                if (_instance == null)
                {
                    GameObject managerObject = new GameObject("PlantFlowerManager");
                    _instance = managerObject.AddComponent<PlantFlowerManager>();
                    DontDestroyOnLoad(managerObject);
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    // Plant Generation
    public void BeginPlantGeneration()
    {
        UnityEngine.Random.InitState(GameManager.Instance.GameSeed);
        SelectNativePlantsForAllRegions();
    }

    private void SelectNativePlantsForAllRegions()
    {
        Debug.Log("Selecting Native Plants for All Regions");
        // Grouping directions
        var northGroup = new[] { CompassDirection.North, CompassDirection.NorthEast, CompassDirection.East };
        var southGroup = new[] { CompassDirection.South, CompassDirection.SouthWest, CompassDirection.West };

        // Assign plants for the North group
        SelectNativePlantsForGroup(northGroup);

        // Assign plants for the South group
        SelectNativePlantsForGroup(southGroup);
    }

    private void SelectNativePlantsForGroup(CompassDirection[] directions)
    {
        try
        {
            Debug.Log($"Selecting native plants for directions: {string.Join(", ", directions)}");

            bool anyPlantSet = false; // To track if any plant is successfully set

            // Select native TreeFruit for the group
            var nativeTreeFruitName = ChooseNativePlantBasedOnGroup(directions, ItemType.TreeFruit);
            if (!string.IsNullOrEmpty(nativeTreeFruitName))
            {
                AddNativePlantToPermaLists(directions, nativeTreeFruitName, ItemType.TreeFruit);
                Debug.Log($"TreeFruit selected: {nativeTreeFruitName} for directions: {string.Join(", ", directions)}");
                anyPlantSet = true;
            }
            else
            {
                Debug.LogWarning($"No TreeFruit set for directions: {string.Join(", ", directions)}");
            }

            // Select native VineFruit for the group
            var nativeVineFruitName = ChooseNativePlantBasedOnGroup(directions, ItemType.VineFruit);
            if (!string.IsNullOrEmpty(nativeVineFruitName))
            {
                AddNativePlantToPermaLists(directions, nativeVineFruitName, ItemType.VineFruit);
                Debug.Log($"VineFruit selected: {nativeVineFruitName} for directions: {string.Join(", ", directions)}");
                anyPlantSet = true;
            }
            else
            {
                Debug.LogWarning($"No VineFruit set for directions: {string.Join(", ", directions)}");
            }

            // Select native BushFruit for the group
            var nativeBushFruitName = ChooseNativePlantBasedOnGroup(directions, ItemType.BushFruit);
            if (!string.IsNullOrEmpty(nativeBushFruitName))
            {
                AddNativePlantToPermaLists(directions, nativeBushFruitName, ItemType.BushFruit);
                Debug.Log($"BushFruit selected: {nativeBushFruitName} for directions: {string.Join(", ", directions)}");
                anyPlantSet = true;
            }
            else
            {
                Debug.LogWarning($"No BushFruit set for directions: {string.Join(", ", directions)}");
            }

            // Select native vegetable for the group
            var nativeVegetableName = ChooseNativePlantBasedOnGroup(directions, ItemType.Vegetable);
            if (!string.IsNullOrEmpty(nativeVegetableName))
            {
                AddNativePlantToPermaLists(directions, nativeVegetableName, ItemType.Vegetable);
                Debug.Log($"Vegetable selected: {nativeVegetableName} for directions: {string.Join(", ", directions)}");
                anyPlantSet = true;
            }
            else
            {
                Debug.LogWarning($"No Vegetable set for directions: {string.Join(", ", directions)}");
            }

            // Select native Fungi for the group
            var nativeFungiName = ChooseNativePlantBasedOnGroup(directions, ItemType.Fungi);
            if (!string.IsNullOrEmpty(nativeFungiName))
            {
                AddNativePlantToPermaLists(directions, nativeFungiName, ItemType.Fungi);
                Debug.Log($"Fungi selected: {nativeFungiName} for directions: {string.Join(", ", directions)}");
                anyPlantSet = true;
            }
            else
            {
                Debug.LogWarning($"No Fungi set for directions: {string.Join(", ", directions)}");
            }

            // Check if no plant was set at all for the group
            if (!anyPlantSet)
            {
                Debug.LogWarning($"No native plants set for directions: {string.Join(", ", directions)}. Consider reviewing the plant data.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception in SelectNativePlantsForGroup: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private string ChooseNativePlantBasedOnGroup(CompassDirection[] directions, ItemType itemType)
    {
        var allItems = PermaLists.Instance.ItemCreationData
            .Where(item => item.ItemTypes.Contains(itemType))
            .ToList();

        if (allItems.Count == 0)
        {
            Debug.LogWarning($"No {itemType} found for the group {string.Join(", ", directions)}. Cannot select native plants.");
            return null;
        }

        return allItems
            .OrderBy(_ => UnityEngine.Random.value)
            .Select(item => item.Name)
            .FirstOrDefault(); // Simple random selection, adjust logic as needed
    }

    private void AddNativePlantToPermaLists(CompassDirection[] directions, string nativePlantName, ItemType itemType)
    {
        var permaLists = PermaLists.Instance;

        foreach (var direction in directions)
        {
            // Update the PermaLists as before
            switch (itemType)
            {
                case ItemType.TreeFruit:
                    permaLists.NativeTreeFruitsPerRegion[direction] = nativePlantName;
                    break;
                case ItemType.VineFruit:
                    permaLists.NativeVineFruitsPerRegion[direction] = nativePlantName;
                    break;
                case ItemType.BushFruit:
                    permaLists.NativeBushFruitsPerRegion[direction] = nativePlantName;
                    break;
                case ItemType.Vegetable:
                    permaLists.NativeVegetablesPerRegion[direction] = nativePlantName;
                    break;
                case ItemType.Fungi:
                    permaLists.NativeFungiPerRegion[direction] = nativePlantName;
                    break;
            }

            // Update the RegionManager
            UpdateRegionsWithNativePlants(direction, nativePlantName, itemType);
        }
    }

    private void UpdateRegionsWithNativePlants(CompassDirection direction, string nativePlantName, ItemType itemType)
    {
        var regionManager = RegionManager.Instance;

        foreach (var regionInfo in regionManager.GetRegionsByDirection(direction))
        {
            switch (itemType)
            {
                case ItemType.TreeFruit:
                    regionInfo.NativeTreeFruit = nativePlantName;
                    break;
                case ItemType.VineFruit:
                    regionInfo.NativeVineFruit = nativePlantName;
                    break;
                case ItemType.BushFruit:
                    regionInfo.NativeBushFruit = nativePlantName;
                    break;
                case ItemType.Vegetable:
                    regionInfo.NativeVegetable = nativePlantName;
                    break;
                case ItemType.Fungi:
                    regionInfo.NativeFungi = nativePlantName;
                    break;
            }
        }
    }
}
