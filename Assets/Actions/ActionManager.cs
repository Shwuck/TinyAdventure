using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ActionManager : MonoBehaviour
{
    public PlayerController playerController;

    // List of environmental actions
    private List<IEnvironmentalAction> environmentalActions = new List<IEnvironmentalAction>();

    // List of special actions
    private List<IInteraction> specialActions = new List<IInteraction>();

    void Start()
    {
        // Initialize environmental actions
        environmentalActions.Add(new DigAction());
        environmentalActions.Add(new TillSoilAction());
        environmentalActions.Add(new PlantSeedsAction());
        environmentalActions.Add(new FishAction());
        environmentalActions.Add(new PickUpItemsAction());
        environmentalActions.Add(new PickUpALLItemsAction());
        environmentalActions.Add(new InspectItemsAction());
        environmentalActions.Add(new ClaimLandInteraction());
        environmentalActions.Add(new DrinkInteraction());
        // Add constructable interactions
        environmentalActions.Add(new PlaceWoodenWallInteraction());
        environmentalActions.Add(new PlaceWoodenDoorInteraction());
        environmentalActions.Add(new PlaceAnvilInteraction());
        environmentalActions.Add(new PlaceBedInteraction());

    }

    public IEnumerable<IEnvironmentalAction> GetAvailableEnvironmentalActions(Cell cell, PlayerInventory inventory)
    {
        return environmentalActions.Where(action => action.IsAvailable(cell, inventory));
    }

    public IEnumerable<IInteraction> GetAvailableSpecialActions(PlayerInventory inventory)
    {
        return specialActions.Where(action => action.IsAvailable(null, inventory));
        // Note: If Special actions need to check a cell or other context, adjust IsAvailable accordingly
    }
}


