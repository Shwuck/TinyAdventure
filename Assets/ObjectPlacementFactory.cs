using UnityEngine;
using System;
using System.Collections.Generic;

public class ObjectPlacementFactory : MonoBehaviour
{
    // Singleton instance
    private static ObjectPlacementFactory _instance;

    public static ObjectPlacementFactory Instance
    {
        get
        {
            if (_instance == null)
            {
                // Create a new GameObject to hold the ObjectPlacementFactory if it doesn't exist in the scene
                GameObject factoryObject = new GameObject("ObjectPlacementFactory");
                _instance = factoryObject.AddComponent<ObjectPlacementFactory>();

                // Optionally, set this object not to be destroyed when loading new scenes
                DontDestroyOnLoad(factoryObject);
            }
            return _instance;
        }
    }

    // Use reflection or dictionary-based object creation
    public bool PlaceObjectAt(string objectName, Vector2Int cellPosition, INestedArea nestedArea)
    {
        try
        {
            // Reflection example for getting type by name
            Type type = Type.GetType(objectName);
            if (type != null && typeof(IInteractable).IsAssignableFrom(type))
            {
                // Create an instance of the object
                IInteractable obj = (IInteractable)Activator.CreateInstance(type);

                // Set the object's position and nested area
                obj.Position = cellPosition;
                obj.CurrentNestedArea = nestedArea;
                obj.IsInNestedArea = true;
                obj.IsActive = true;

                // Add the object to the nested area's map
                Cell[,] map = nestedArea.GetNestedMap();
                map[cellPosition.x, cellPosition.y].Objects.Add(obj);

                Debug.Log($"Placed {objectName} at {cellPosition} in nested area.");
                return true;
            }
            else
            {
                Debug.LogError($"Type '{objectName}' is either null or does not implement IInteractable.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to place object '{objectName}': {ex.Message}");
            return false;
        }
    }
}
