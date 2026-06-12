using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using TMPro;
using UnityEngine.UI;

// Interfaces and Enums
#region Interfaces and Enums

public interface IInteraction
{
    InteractionType Type { get; }
    string Name { get; }
    int ActionPointCost { get; } // Updated property
    void ExecuteInteraction(IInteractable entity, PlayerInventory inventory);
    bool IsAvailable(IInteractable entity, PlayerInventory inventory);
}

public interface IItemInteraction
{
    InteractionType Type { get; }
    string Name { get; }
    void ExecuteInteraction(Item item, Inventory inventory);
    bool IsAvailable(Item item, Inventory inventory);
}

public interface IEnvironmentalAction
{
    InteractionType Type { get; }
    string Name { get; }
    int ActionPointCost { get; } // Updated property
    bool IsAvailable(Cell cell, PlayerInventory inventory);
    void ExecuteAction(Cell cell, PlayerInventory inventory);
}

public interface IDialogueInteraction
{
    InteractionType Type { get; }
    string Name { get; }
    void ExecuteInteraction(IInteractable entity, PlayerInventory inventory);
    bool IsAvailable(IInteractable entity, PlayerInventory inventory);
}

public interface ICombatAction
{
    InteractionType Type { get; }
    string Name { get; }
    int ActionPointCost { get; } // Updated property
    void ExecuteCombatAction(IInteractable entity, PlayerInventory inventory);
    bool IsCombatAvailable(IInteractable entity, PlayerInventory inventory);
}


public enum InteractionType
{
    Social,
    Item,
    Combat,
    Tool,
    Environmental,
    Special,
    Inspection
}

#endregion

// Interactions
#region Interactions

public class InspectInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Inspection;
    public string Name => "Inspect";
    public int ActionPointCost => 0; // Inspecting takes no time

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable == null)
        {
            Debug.LogWarning("Tried to inspect a null object.");
            return;
        }

        Debug.Log($"Inspecting: {interactable.Name}");

        // Use the central InspectionManager for all cases
        InspectionManager.Instance.Inspect(interactable);

        // End turn after inspecting (if needed)
        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable != null && interactable.IsActive;
    }
}

public class InspectItemsAction : IEnvironmentalAction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Inspect Items";
    public int ActionPointCost => 0; // Inspecting takes no time

    public bool IsAvailable(Cell cell, PlayerInventory inventory)
    {
        // The action is only available if there are items in the cell
        return cell.Items.Any();
    }

    public void ExecuteAction(Cell cell, PlayerInventory inventory)
    {
        if (cell.Items.Any())
        {
            // Create a list of item names
            string itemList = string.Join(", ", cell.Items.Select(item => item.Name));

            // Format the inspection text
            string inspectionText = $"You are inspecting the following items in this area: {itemList}";

            // Display the inspection text in the UI, using the same logic as the InspectInteraction
            UIController.Instance.UpdateInspectionText(inspectionText);
            UIController.Instance.OpenInspectionPanel();

            // Log the inspection for debugging
            Debug.Log($"Inspecting items: {itemList}");
        }
        else
        {
            Debug.LogWarning("There are no items to inspect in this cell.");
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }
}


public class InspectNPCInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Inspection;
    public string Name => "Inspect NPC";
    public int ActionPointCost => 0; // Inspecting takes no time

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable != null && interactable is NPC npc)
        {
            Debug.Log($"Inspecting: {npc.Name}");
            string inspectionText = $"You are inspecting {npc.Name}, they are a {npc.Race.Name} {npc.Role}. They are {npc.Age} years old";
            UIController.Instance.UpdateInspectionText(inspectionText);
        }
        else
        {
            Debug.LogWarning("Interactable object is null or not an NPC.");
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable != null && interactable.IsActive && interactable is NPC;
    }
}

public class TalkInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Social;
    public string Name => "Talk";
    public int ActionPointCost => 0; // Talking takes a small fraction of a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is NPC npc && IsAvailable(npc, inventory))
        {
            UIController uiController = GameObject.FindObjectOfType<UIController>();
            if (uiController != null)
            {
                uiController.ActivateDialoguePanel();
                DialoguePanelUI dialoguePanel = uiController.dialoguePanel.GetComponent<DialoguePanelUI>();
                if (dialoguePanel != null)
                {
                    dialoguePanel.SetupDialogue(npc);
                }
                else
                {
                    Debug.LogError("DialoguePanelUI component not found.");
                }
            }
            else
            {
                Debug.LogError("UIController not found.");
            }
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable.IsActive;
    }
}

public class TradeInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Social;
    public string Name => "Trade";
    public int ActionPointCost => 0; // Trading takes half a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is NPC npc && IsAvailable(npc, inventory))
        {
            UIController uiController = GameObject.FindObjectOfType<UIController>();
            if (uiController != null)
            {
                uiController.ActivateTradePanel(npc);
                TradePanelUI tradePanel = uiController.tradePanel.GetComponent<TradePanelUI>();
                if (tradePanel != null)
                {
                    tradePanel.SetupTrade(npc, inventory);
                }
                else
                {
                    Debug.LogError("TradePanelUI component not found.");
                }
            }
            else
            {
                Debug.LogError("UIController not found.");
            }
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable.IsActive;
    }
}

public class PickPocketInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Social;
    public string Name => "Pickpocket";
    public int ActionPointCost => 0; // Pickpocketing takes a fraction of a turn

    public void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        if (entity is NPC npc && !npc.IsPlayerVisible && npc.Inventory.GetInventoryContainers().Any(c => c.Items.Count > 0))
        {
            List<Item> allItems = npc.Inventory.GetInventoryContainers().SelectMany(c => c.Items).ToList();

            if (allItems.Count > 0)
            {
                System.Random rnd = new System.Random();
                int index = rnd.Next(0, allItems.Count);
                Item stolenItem = allItems[index];

                inventory.AddItem(stolenItem);
                npc.Inventory.RemoveItem(stolenItem.ItemInGameName, 1); // Fixed Call

                Debug.Log($"You successfully pick-pocketed {stolenItem.ItemInGameName} from the NPC!");
            }
            else
            {
                Debug.Log("Pickpocket attempt failed. The NPC has no items.");
            }
        }
        else
        {
            Debug.Log("Pickpocket attempt failed. Either the player is visible or the NPC has no items.");
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        return entity is NPC npc && !npc.IsPlayerVisible && npc.Inventory.GetInventoryContainers().Any(c => c.Items.Count > 0);
    }
}

public class ShoveInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Social;
    public string Name => "Shove";
    public int ActionPointCost => 0; // Shoving takes a small fraction of a turn

    public void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        if (entity is NPC npc)
        {
            Direction shoveDirection = PlayerStats.Instance.PlayerFacing;
            int shoveStrength = 1;
            Vector2Int positionBefore = npc.NestedMapPosition;
            INestedArea areaBefore = npc.CurrentNestedArea;
            Vector2Int intendedTarget = positionBefore + DirectionToVector(shoveDirection);
            bool targetInBounds = areaBefore != null && areaBefore.IsValidPosition(intendedTarget);
            Cell targetCell = targetInBounds ? areaBefore.GetCellAtPosition(intendedTarget) : null;
            bool targetPassable = areaBefore != null && targetInBounds && areaBefore.IsPassable(intendedTarget);
            int oldCellObjectCountBefore = areaBefore != null && areaBefore.IsValidPosition(positionBefore)
                ? areaBefore.GetCellAtPosition(positionBefore)?.Objects?.Count ?? -1
                : -1;
            int targetCellObjectCountBefore = targetCell?.Objects?.Count ?? -1;

            // CODEXLOG002_MOVEMENT_AI: temporary shove diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[SHOVE]", "ShoveInteraction.ExecuteInteraction begin",
                $"Target position before: {positionBefore}\n" +
                $"Intended shove direction: {shoveDirection}\n" +
                $"Intended target cell: {intendedTarget}\n" +
                $"Target in bounds: {targetInBounds}\n" +
                $"Passable: {targetPassable}\n" +
                $"Old cell occupant count before: {oldCellObjectCountBefore}\n" +
                $"Target cell occupant count before: {targetCellObjectCountBefore}\n" +
                "Forced movement attempted: True",
                npc);
            // CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic.
            NestedMapDebugger.LogSnapshotForMovement(areaBefore, npc, "SNAPSHOT_BEFORE_SHOVE");

            bool wasShoved = npc.SimpleMovement(shoveStrength, shoveDirection);
            Vector2Int positionAfter = npc.NestedMapPosition;
            bool positionChanged = positionAfter != positionBefore;
            Cell oldCellAfter = areaBefore?.GetCellAtPosition(positionBefore);
            Cell newCellAfter = areaBefore?.GetCellAtPosition(positionAfter);
            bool oldCellStillContainsTarget = oldCellAfter?.Objects?.Contains(npc) ?? false;
            bool newCellContainsTarget = newCellAfter?.Objects?.Contains(npc) ?? false;

            // CODEXLOG002_MOVEMENT_AI: temporary shove diagnostic.
            MovementAIDiagnosticsLogger.LogEvent("[SHOVE]", "ShoveInteraction.ExecuteInteraction end",
                $"Target position before: {positionBefore}\n" +
                $"Target position after: {positionAfter}\n" +
                $"Forced movement attempted: True\n" +
                $"Forced movement succeeded: {wasShoved}\n" +
                $"Position changed: {positionChanged}\n" +
                $"Old cell still contains target: {oldCellStillContainsTarget}\n" +
                $"New cell contains target: {newCellContainsTarget}\n" +
                "Map refresh requested: False",
                npc);
            // CODEXLOG002_MOVEMENT_AI: temporary nested map snapshot diagnostic.
            NestedMapDebugger.LogSnapshotForMovement(areaBefore, npc, "SNAPSHOT_AFTER_SHOVE");

            if (wasShoved)
            {
                npc.UpdateLineOfSight();
                Debug.Log($"Successfully shoved the NPC in direction {shoveDirection}.");
            }
            else
            {
                Debug.LogWarning("Failed to shove the NPC. They might be blocked.");
            }
        }
        else
        {
            Debug.Log("This entity cannot be shoved.");
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    // CODEXLOG002_MOVEMENT_AI: temporary shove diagnostic helper.
    private static Vector2Int DirectionToVector(Direction direction)
    {
        switch (direction)
        {
            case Direction.North: return Vector2Int.up;
            case Direction.South: return Vector2Int.down;
            case Direction.West: return Vector2Int.left;
            case Direction.East: return Vector2Int.right;
            default: return Vector2Int.zero;
        }
    }

    public bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        return entity is NPC npc && npc.IsActive;
    }
}

public class ChopInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Tool;
    public string Name => "Chop";
    public int ActionPointCost => 1; // Chopping a tree takes a full turn

    public void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        if (entity is Tree tree && inventory.HasItem("Axe"))
        {
            if (tree.WoodQuantity > 0)
            {
                tree.WoodQuantity--;
                Debug.Log("Chopping Tree");
                Item woodItem = ItemGenerator.Instance.GenerateItem("Wood");
                if (woodItem != null)
                {
                    inventory.AddItem(woodItem);
                    Debug.Log($"Wood chopped. Wood remaining: {tree.WoodQuantity}");
                }
                else
                {
                    Debug.LogError("Failed to generate Wood.");
                }
                if (tree.WoodQuantity == 0)
                {
                    tree.CurrentNestedArea.RemoveObjectFromArea(tree);
                    Debug.Log("Tree is completely chopped down.");
                }
            }
            else
            {
                Debug.Log("No wood remaining.");
            }
        }
        else
        {
            Debug.Log("You need an axe to chop the tree.");
        }

        // Deduct the turn time
        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        return entity is Tree tree && tree.IsActive && inventory.HasItem("Axe");
    }
}

public class GatherInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Gather";
    public int ActionPointCost => 1; // Gathering takes a full turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is IContainer container && interactable is PlantBase plantBase)
        {
            // Check if the container has any items to gather
            if (container.GetInventoryContainers().Any(c => c.Items.Count > 0))
            {
                var availableContainers = container.GetInventoryContainers().Where(c => c.Items.Count > 0).ToList();
                InventoryContainer selectedContainer = availableContainers.First();

                // Get the first item from the container
                Item gatheredItem = selectedContainer.Items.First();
                inventory.AddItem(gatheredItem);  // Add item to player inventory
                container.Inventory.RemoveItem(gatheredItem.Name, 1);  // Remove item from the container

                Debug.Log($"You gather {gatheredItem.Name} from the {interactable.Name}.");

                // Stack multiple items to prevent log spam
                int itemCount = inventory.GetItemCount(gatheredItem.Name);
                MessageLogManager.Instance.Log("item", gatheredItem.Name, itemCount);

                // Check if the container is empty after gathering
                if (container.GetInventoryContainers().All(c => c.Items.Count == 0))
                {
                    Debug.Log($"{interactable.Name} has been fully harvested and will be removed.");
                    plantBase.Destroy();  // Call the destroy method
                }
            }
            else
            {
                Debug.LogWarning($"Nothing to gather from {interactable.Name}.");
                MessageLogManager.Instance.Log("special", $"Nothing to gather from {interactable.Name}.");
            }
        }
        else
        {
            Debug.LogWarning($"The object {interactable.Name} is not gatherable.");
            MessageLogManager.Instance.Log("special", $"{interactable.Name} is not gatherable.");
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        // Ensure the interactable is a container and has items available for gathering
        return interactable is IContainer container && container.GetInventoryContainers().Any(c => c.Items.Count > 0);
    }
}

public class MineInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Tool;
    public string Name => "Mine";
    public int ActionPointCost => 1; // Mining takes a full turn

    public void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        if (entity is Rock rock && inventory.HasItem("PickAxe"))
        {
            // Check if the rock has any items (like Stone, Ore, or Gems) left in its inventory
            var minedItem = rock.RemoveItem("Stone"); // Try to remove a stone from the rock's inventory

            if (minedItem != null)
            {
                Debug.Log("Mining Rock");
                inventory.AddItem(minedItem); // Add the mined stone to the player's inventory
                Debug.Log($"Stone mined. Stones remaining in rock: {rock.GetItems().Count(item => item.Name == "Stone")}");

                // Check if the rock has any remaining items
                if (!rock.GetItems().Any()) // If the rock's inventory is empty
                {
                    rock.CurrentNestedArea.RemoveObjectFromArea(rock); // Remove the rock from the nested area
                    Debug.Log("Rock is completely mined.");
                }
            }
            else
            {
                Debug.Log("No more stone remaining in this rock.");
            }
        }
        else
        {
            Debug.Log("You need a pickaxe to mine the rock.");
        }

        // Deduct the turn time
        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        // The interaction is available only if the player has a PickAxe
        return inventory.HasItem("PickAxe");
    }
}


public class PetInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Social;
    public string Name => "Pet";
    public int ActionPointCost => 0; // Petting takes a small fraction of a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is Animal animal)
        {
            Debug.Log($"You pet the {animal.Name}. It seems happy!");
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable is Animal animal && !animal.IsHostile;
    }
}

public class ShakeInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Shake";
    public int ActionPointCost => 0; // Shaking takes a small fraction of a turn

    public void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        if (entity is IContainer container && container.GetInventoryContainers().Any(c => c.Items.Count > 0))
        {
            var availableContainers = container.GetInventoryContainers().Where(c => c.Items.Count > 0).ToList();
            System.Random rnd = new System.Random();
            int containerIndex = rnd.Next(0, availableContainers.Count);
            InventoryContainer selectedContainer = availableContainers[containerIndex];

            Item shakenItem = selectedContainer.Items.First();
            inventory.AddItem(shakenItem);
            container.Inventory.RemoveItem(shakenItem.Name, 1);

            // Stack item collection in the logs
            int itemCount = inventory.GetItemCount(shakenItem.Name);
            MessageLogManager.Instance.Log("item", shakenItem.Name, itemCount);

            Debug.Log($"You shake the {entity.Name} and find {shakenItem.Name}!");
        }
        else
        {
            string messageText = $"Shaking the {entity.Name} yields nothing.";
            Debug.LogWarning(messageText);
            MessageLogManager.Instance.Log("special", messageText);
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        return entity is IContainer container && container.GetInventoryContainers().Any(c => c.Items.Count > 0);
    }
}

public class OpenChestInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Open Chest";
    public int ActionPointCost => 0; // Opening a chest takes half a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        // Check if the interactable is a Chest
        if (interactable is Chest chest && chest.IsActive)
        {
            Debug.Log($"{chest.Name} opened.");
            chest.IsActive = false; // Mark chest as no longer active (i.e., opened)
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        // Check if interactable is Chest and if it is active (i.e., can be opened)
        return interactable is Chest chest && chest.IsActive;
    }
}

public class TakeEar : IInteraction
{
    public InteractionType Type => InteractionType.Tool;
    public string Name => "Take Ear";
    public int ActionPointCost => 0; // Taking an ear takes a significant fraction of a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is Corpse corpse && corpse.IsActive)
        {
            Debug.Log("Ear taken.");
            Item ear = ItemGenerator.Instance.GenerateItem("Ear");
            inventory.AddItem(ear);
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable is Corpse corpse && corpse.IsActive && inventory.HasItem("SkinningKnife");
    }
}

public class PickFlowerInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental; // Categorize as an environmental interaction
    public string Name => "Pick Flower";
    public int ActionPointCost => 1; // Picking a flower takes a full turn

    public void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        if (entity is Flower flower && flower.IsActive)
        {
            flower.PickFlower();  // Call the PickFlower method on the Flower entity
            Debug.Log($"You picked a {flower.Name} and added it to your inventory.");

            // Deduct action points for picking the flower
            EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
        }
        else
        {
            Debug.LogWarning("You cannot pick this entity.");
        }
    }

    public bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        // The interaction is available if the entity is a Flower and it's still active (i.e., hasn't been picked)
        return entity is Flower flower && flower.IsActive;
    }
}


public class CutInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Tool; // Categorizing as a tool interaction
    public string Name => "Cut";
    public int ActionPointCost => 1; // Cutting takes a full turn

    // Executes the cut interaction
    public void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        if (entity is LongGrass longGrass && longGrass.IsActive) // Assuming you use a scythe to cut grass
        {
            longGrass.CutGrass();  // Call the CutGrass method on the LongGrass entity
            Debug.Log("You cut the Long Grass and obtained seeds.");

            // Progress the turn by consuming the action point cost
            EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
        }
        else
        {
            Debug.LogWarning("You need a Scythe to cut the Long Grass.");
        }
    }

    // Checks if the Cut interaction is available
    public bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        // The interaction is available if the entity is LongGrass and the player has a Scythe
        return entity is LongGrass longGrass && longGrass.IsActive;
    }
}



public class ViewVillageSignPostInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "View Village Sign Post";
    public int ActionPointCost => 0; // Viewing a sign post takes a small fraction of a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is VillageSignPost signPost && IsAvailable(signPost, inventory))
        {
            UIController uiController = GameObject.FindObjectOfType<UIController>();
            if (uiController != null)
            {
                uiController.ActivateVillageInfoPanel(signPost.VillageToShow);
            }
            else
            {
                Debug.LogError("UIController not found.");
            }
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable.IsActive;
    }
}

public class DonateInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Donate";
    public int ActionPointCost => 0; // Donating takes half a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is DonationCrate donationCrate && IsAvailable(donationCrate, inventory))
        {
            UIController uiController = GameObject.FindObjectOfType<UIController>();
            if (uiController != null)
            {
                uiController.ActivateDonationPanel();
                DonationPanelUI donationPanel = uiController.donationPanel.GetComponent<DonationPanelUI>();
                if (donationPanel != null)
                {
                    donationPanel.SetupDonation(donationCrate.AssignedVillage, inventory);
                }
                else
                {
                    Debug.LogError("DonationPanelUI component not found.");
                }
            }
            else
            {
                Debug.LogError("UIController not found.");
            }
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable.IsActive;
    }
}

public class SmithInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Smith";
    public int ActionPointCost => 0; // Smithing takes half a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is Anvil anvil && IsAvailable(anvil, inventory))
        {
            UIController uiController = GameObject.FindObjectOfType<UIController>();
            if (uiController != null)
            {
                uiController.OpenSmithingPanel();
            }
            else
            {
                Debug.LogError("UIController not found.");
            }
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable.IsActive;
    }
}

public class CraftInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Craft";
    public int ActionPointCost => 0; // Crafting takes half a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (IsAvailable(interactable, inventory))
        {
            UIController uiController = GameObject.FindObjectOfType<UIController>();
            if (uiController != null)
            {
                uiController.OpenCraftingPanel();
            }
            else
            {
                Debug.LogError("UIController not found.");
            }
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable.IsActive;
    }
}

public class CookInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Cook at ";
    public int ActionPointCost => 0; // Cooking takes half a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (IsAvailable(interactable, inventory))
        {
            UIController uiController = GameObject.FindObjectOfType<UIController>();
            if (uiController != null)
            {
                uiController.OpenCookingPanel();
            }
            else
            {
                Debug.LogError("UIController not found.");
            }
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    // Check if the interaction is available (i.e., if the object is flammable and currently on fire)
    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        // Check if the interactable object implements IFlammable and is on fire
        if (interactable is IFlammable flammable)
        {
            return flammable.IsFlammable && flammable.IsOnFire;
        }

        return false;
    }
}

public class OpenContainerInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Inspection;
    public string Name => "Open Container";
    public int ActionPointCost => 0;

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is IContainer container && IsAvailable(interactable, inventory))
        {
            UIController uiController = GameObject.FindObjectOfType<UIController>();
            if (uiController != null)
            {
                uiController.ActivateContainerPanel();
                ContainerPanelUI containerPanel = uiController.containerPanel.GetComponent<ContainerPanelUI>();

                if (containerPanel != null)
                {
                    containerPanel.SetupContainerInteraction(container.Inventory, inventory);
                }
                else
                {
                    Debug.LogError("OpenContainerInteraction: ContainerPanelUI component not found.");
                }
            }
            else
            {
                Debug.LogError("OpenContainerInteraction: UIController not found.");
            }
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable.IsActive;
    }
}

public class EmptyContainerInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Empty Container";
    public int ActionPointCost => 0;

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is IContainer container && IsAvailable(interactable, inventory))
        {
            List<string> emptiedItems = new List<string>();

            foreach (var inventoryContainer in container.GetInventoryContainers().ToList())
            {
                foreach (var item in inventoryContainer.Items.ToList())
                {
                    inventory.AddItem(item);

                    if (inventory.HasItem(item.ItemInGameName))
                    {
                        container.Inventory.RemoveItem(item.ItemInGameName, 1);
                        emptiedItems.Add(item.ItemInGameName);
                    }
                    else
                    {
                        Debug.LogWarning($"EmptyContainerInteraction: Could not add item {item.ItemInGameName} to player inventory. Capacity may be exceeded.");
                        break;
                    }
                }
            }

            if (emptiedItems.Count > 0)
            {
                string itemSummary = string.Join(", ", emptiedItems.Distinct());
                MessageLogManager.Instance.Log("item", $"You emptied the container and obtained: {itemSummary}");
            }
            else
            {
                MessageLogManager.Instance.Log("special", "The container was empty.");
            }
        }
        else
        {
            MessageLogManager.Instance.Log("special", "There is nothing to empty in this container.");
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable is IContainer container && container.GetInventoryContainers().Any(c => c.Items.Count > 0);
    }
}

public class AscendInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Ascend";
    public int ActionPointCost => 0; // Ascending takes half a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable.IsActive)
        {
            Debug.Log("Ascended using " + interactable.Name);
            PlayerStats.Instance.AscendOutOfNestedArea();
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable.IsActive;
    }
}


public class DescendInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Descend";
    public int ActionPointCost => 0; // Descending takes half a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable.IsActive)
        {
            Debug.Log("Descended using " + interactable.Name);
            PlayerStats.Instance.DescendIntoNestedArea();
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable.IsActive;
    }
}


public class EnterDungeonInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Enter Dungeon";
    public int ActionPointCost => 0; // Entering takes half a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is DungeonEntrance dungeonEntrance && dungeonEntrance.IsActive)
        {
            Debug.Log("Entering Dungeon");
            PlayerStats.Instance.EnterDungeon();
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable is DungeonEntrance dungeonEntrance && dungeonEntrance.IsActive;
    }
}

public class EnterCaveInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Enter Cave";
    public int ActionPointCost => 0; // Entering takes half a turn

    // Determines if the interaction is available (can customize this logic)
    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {

        return true; 
    }

    // Executes the interaction
    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is CaveEntrance caveEntrance)
        {
            PlayerStats.Instance.EnterCave();
        }
    }
}


public class OpenDoorInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Open Door";
    public int ActionPointCost => 1; // Opening a door takes a small fraction of a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is Door door && door.State == DoorState.Closed)
        {
            door.OpenDoor();
            Debug.Log("You open the door.");
        }
        else
        {
            Debug.Log("The door is already open.");
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        // The door must be closed and active to allow opening
        return interactable is Door door && door.IsActive && door.State == DoorState.Closed;
    }
}

public class CloseDoorInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Close Door";
    public int ActionPointCost => 1; // Closing a door takes a small fraction of a turn

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is Door door && door.State == DoorState.Open)
        {
            door.CloseDoor();
            Debug.Log("You close the door.");
        }
        else
        {
            Debug.Log("The door is already closed.");
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        // The door must be open and active to allow closing
        return interactable is Door door && door.IsActive && door.State == DoorState.Open;
    }
}

public class ExtinguishInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Extinguish";
    public int ActionPointCost => 1; // Extinguishing takes a full turn

    // Check if the interaction is available (i.e., if the object is flammable and currently on fire)
    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        // Check if the interactable object implements IFlammable and is on fire
        return interactable is IFlammable flammable && flammable.IsFlammable && flammable.IsOnFire;
    }

    // Execute the interaction to extinguish the fire
    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is IFlammable flammable && flammable.IsOnFire)
        {
            // Extinguish the object
            flammable.Extinguish();
            Debug.Log($"{interactable.Name} has been extinguished.");

            // Log success message
            MessageLogManager.Instance.Log("special", $"{interactable.Name} has been extinguished.");
        }
        else
        {
            // Log failure message if the object isn't on fire or isn't flammable
            Debug.LogWarning($"ExtinguishInteraction: Cannot extinguish {interactable.Name} as it is not on fire.");
            MessageLogManager.Instance.Log("special", $"{interactable.Name} is not on fire.");
        }

        // Deduct action points or progress the turn
        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }
}


public class LightCampfireInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Light Campfire";
    public int ActionPointCost => 1; // Lighting the campfire takes a full turn

    // Check if the interaction is available (i.e., if the object is a Campfire, flammable, and not currently on fire)
    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable is Campfire campfire && !campfire.IsOnFire;
    }

    // Execute the interaction to light the campfire
    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is Campfire campfire && !campfire.IsOnFire)
        {
            // Ignite the campfire
            campfire.CatchFire();
            Debug.Log($"{interactable.Name} has been lit.");

            // Log success message
            MessageLogManager.Instance.Log("special", $"{interactable.Name} has been lit.");
        }
        else
        {
            // Log failure message if the campfire is already on fire or is not valid
            Debug.LogWarning($"LightCampfireInteraction: {interactable.Name} is already on fire or is not a valid campfire.");
            MessageLogManager.Instance.Log("special", $"{interactable.Name} is already burning.");
        }

        // Deduct action points or progress the turn
        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }
}



public class ClearShovelInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Tool;
    public string Name => "Clear with Shovel";
    public int ActionPointCost => 1; // Takes a full turn to clear

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is Block block && IsAvailable(block, inventory))
        {
            Debug.Log($"Clearing {block.Name} with a shovel.");

            // Logic to remove the block from the game world
            block.CurrentNestedArea.RemoveObjectFromArea(block);
            Debug.Log($"{block.Name} has been cleared.");

            // Add logging for inventory or other side effects, if needed

            // Deduct action point cost
            EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
        }
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        // Check if the player has a shovel and if the block is Dirt or Sand
        return inventory.HasItem("Shovel");
    }
}

public class ClearPickaxeInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Tool;
    public string Name => "Clear with Pickaxe";
    public int ActionPointCost => 2; // Takes a longer time to clear Stone

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is StoneBlock stoneBlock && IsAvailable(stoneBlock, inventory))
        {
            Debug.Log($"Clearing {stoneBlock.Name} with a pickaxe.");

            // Logic to remove the block from the game world
            stoneBlock.CurrentNestedArea.RemoveObjectFromArea(stoneBlock);
            Debug.Log($"{stoneBlock.Name} has been cleared.");

            // Add logging for inventory or other side effects, if needed

            // Deduct action point cost
            EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
        }
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        // Check if the player has a pickaxe and if the block is Stone
        return inventory.HasItem("Pickaxe");
    }
}



#endregion

// Animal Actions
#region Animal Actions

public class FeedAnimalInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Social;
    public string Name => "Feed Animal";
    public int ActionPointCost => 1; // Feeding takes a full turn

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        // Feed action is always available
        return interactable is Animal;
    }

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is Animal animal)
        {
            // Determine the appropriate food item to remove based on the animal's diet
            Item foodItemToRemove = null;

            switch (animal.Diet)
            {
                case Diet.Herbivore:
                    foodItemToRemove = inventory.GetInventoryContainers()
                                                .SelectMany(container => container.Items)
                                                .FirstOrDefault(item => item.ItemTypes.Contains(ItemType.Fruit) || item.ItemTypes.Contains(ItemType.Vegetable));
                    break;

                case Diet.Carnivore:
                    foodItemToRemove = inventory.GetInventoryContainers()
                                                .SelectMany(container => container.Items)
                                                .FirstOrDefault(item => item.ItemTypes.Contains(ItemType.Meat) || item.ItemTypes.Contains(ItemType.Fish));
                    break;

                case Diet.Fungivore:
                    foodItemToRemove = inventory.GetInventoryContainers()
                                                .SelectMany(container => container.Items)
                                                .FirstOrDefault(item => item.ItemTypes.Contains(ItemType.Fungi));
                    break;

                case Diet.Omnivore:
                    foodItemToRemove = inventory.GetInventoryContainers()
                                                .SelectMany(container => container.Items)
                                                .FirstOrDefault(item => item.ItemTypes.Contains(ItemType.Meat) ||
                                                                        item.ItemTypes.Contains(ItemType.Fish) ||
                                                                        item.ItemTypes.Contains(ItemType.Fruit) ||
                                                                        item.ItemTypes.Contains(ItemType.Vegetable) ||
                                                                        item.ItemTypes.Contains(ItemType.Fungi));
                    break;
            }

            if (foodItemToRemove != null)
            {
                // Remove the food item from the inventory
                inventory.RemoveItem(foodItemToRemove.ItemInGameName, 1);
                Debug.Log($"Fed {animal.Name} with {foodItemToRemove.Name}. The item has been removed from the inventory.");

                // Increase the animal's PlayerFavour by 5
                animal.PlayerFavour += 5f;
                Debug.Log($"{animal.Name}'s favour towards the player increased by 5. Current favour: {animal.PlayerFavour}");

                // Deduct the turn time
                EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
            }
            else
            {
                // If no appropriate food item is found
                Debug.Log($"You have nothing to feed the {animal.Name}.");
            }
        }
        else
        {
            Debug.LogWarning("FeedAnimalInteraction: The interactable is not an animal.");
        }
    }
}



public class TameAnimalInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Social;
    public string Name => "Tame Animal";
    public int ActionPointCost => 2; // Taming takes more time

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        // Check if the interactable is a wild animal, not already tamed, and has a PlayerFavour greater than 85
        if (interactable is Animal animal)
        {
            return animal.PlayerFavour > 85f && !animal.IsTame;
        }

        return false;
    }

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is Animal animal)
        {
            // Simulate the taming process
            Debug.Log($"Attempting to tame the {animal.Name}.");

            // Update the animal's state to indicate it has been tamed
            animal.IsTame = true;
            animal.IsTamedBy = PlayerStats.Instance.CurrentPlayerCharacter;
            Debug.Log($"The {animal.Name} has been tamed by {animal.IsTamedBy.Name}!");

            // Deduct the turn time
            EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
        }
        else
        {
            Debug.LogWarning("TameAnimalInteraction: The interactable is not a wild animal.");
        }
    }
}

public class MountAnimalInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Special; // You can choose a different type if appropriate
    public string Name => "Mount";
    public int ActionPointCost => 1; // Mounting should take a small amount of time

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        // Check if the interactable is a tamed, mountable animal and tamed by the current player character
        if (interactable is Animal animal)
        {
            return animal.IsTame && animal.IsTamedBy == PlayerStats.Instance.CurrentPlayerCharacter && animal.IsMountable;
        }

        return false;
    }

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is Animal animal)
        {
            // Mount the animal
            Debug.Log($"Mounting the {animal.Name}.");

            // Assign the animal as the current player's mount
            PlayerStats.Instance.AssignMount(animal);
            Debug.Log($"{animal.Name} has been mounted by {PlayerStats.Instance.CurrentPlayerCharacter.Name}.");

            // Deduct the turn time
            EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
        }
        else
        {
            Debug.LogWarning("MountAnimalInteraction: The interactable is not a mountable animal.");
        }
    }
}


#endregion

// Environmental Actions
#region Environmental Actions

public class DigAction : IEnvironmentalAction
{
    public InteractionType Type => InteractionType.Tool;
    public string Name => "Dig";
    public int ActionPointCost => 1; // Digging takes a full turn

    public bool IsAvailable(Cell cell, PlayerInventory inventory)
    {
        bool hasShovel = inventory.HasItem("Shovel");
        bool isTerrainDiggable = cell.Terrain == TerrainType.Land || cell.Terrain == TerrainType.Dirt || cell.Terrain == TerrainType.Swamp;
        bool cellIsEmptyOfObjects = !cell.Objects.Any();
        return hasShovel && isTerrainDiggable && cellIsEmptyOfObjects;
    }

    public void ExecuteAction(Cell cell, PlayerInventory inventory)
    {
        switch (cell.Terrain)
        {
            case TerrainType.Land:
                cell.Terrain = TerrainType.Dirt;
                Debug.Log("Dug up the land, now it's dirt.");
                break;
            case TerrainType.Dirt:
                Hole hole = new Hole();
                hole.IsActive = true;
                cell.Objects.Add(hole);
                hole.Position = cell.Coordinates;
                hole.NestedMapPosition = cell.Coordinates;
                Debug.Log("Dug up the dirt, now there's a hole.");
                break;
            case TerrainType.Swamp:
                cell.Terrain = (UnityEngine.Random.value < 0.5f) ? TerrainType.Water : TerrainType.Dirt;
                Debug.Log("Dug up the swamp, now it's " + (cell.Terrain == TerrainType.Water ? "water." : "dirt."));
                break;
            default:
                Debug.LogError("Unsupported terrain type for digging.");
                break;
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }
}

public class TillSoilAction : IEnvironmentalAction
{
    public InteractionType Type => InteractionType.Tool;
    public string Name => "Till Soil";
    public int ActionPointCost => 1; // Tilling soil takes a full turn

    public bool IsAvailable(Cell cell, PlayerInventory inventory)
    {
        bool hasHoe = inventory.HasItem("Hoe");
        bool isDirt = cell.Terrain == TerrainType.Dirt;
        bool cellIsSuitable = !cell.Objects.Any();
        return hasHoe && isDirt && cellIsSuitable;
    }

    public void ExecuteAction(Cell cell, PlayerInventory inventory)
    {
        if (cell.Terrain == TerrainType.Dirt)
        {
            cell.Terrain = TerrainType.TilledSoil;
            Debug.Log("Tilled the soil, ready for planting.");
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }
}

public class PlantSeedsAction : IEnvironmentalAction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Plant Seeds";
    public int ActionPointCost => 1; // Planting seeds takes a full turn

    public bool IsAvailable(Cell cell, PlayerInventory inventory)
    {
        // Check if the player has any seeds in their inventory
        bool hasSeeds = inventory.GetInventoryContainers()
            .SelectMany(container => container.Items)
            .Any(item => !string.IsNullOrEmpty(item.SeedType));

        bool isTilledSoil = cell.Terrain == TerrainType.TilledSoil;
        bool cellIsSuitableForPlanting = !cell.Objects.Any();

        return hasSeeds && isTilledSoil && cellIsSuitableForPlanting;
    }

    public void ExecuteAction(Cell cell, PlayerInventory inventory)
    {
        // Find the first seed item in the inventory
        var seedItem = inventory.GetInventoryContainers()
            .SelectMany(container => container.Items)
            .FirstOrDefault(item => !string.IsNullOrEmpty(item.SeedType));

        if (seedItem != null && cell.Terrain == TerrainType.TilledSoil)
        {
            // Create a new SeededSoil object and pass the SeedType to it
            SeededSoil seededSoil = new SeededSoil(seedItem.SeedType);
            seededSoil.Position = cell.Coordinates;
            seededSoil.NestedMapPosition = cell.Coordinates;

            // Add the SeededSoil object to the cell
            cell.Objects.Add(seededSoil);

            // Remove the seed from the inventory
            inventory.RemoveItem(seedItem.ItemInGameName, 1);

            Debug.Log($"Planted {seedItem.SeedType} seeds, a seedling sprouts at position {cell.Coordinates}.");
        }
        else
        {
            Debug.LogWarning("Failed to plant seeds. Either no seeds found or the soil is not suitable.");
        }

        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }
}


public class HarvestInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Harvest";
    public int ActionPointCost => 1;

    private readonly string cropType;

    public HarvestInteraction(string cropType)
    {
        this.cropType = cropType;
    }

    public void ExecuteInteraction(IInteractable interactable, PlayerInventory inventory)
    {
        if (interactable is Plant plant && plant.IsActive)
        {
            Item harvestedCrop = ItemGenerator.Instance.GenerateItem(cropType);
            if (harvestedCrop != null)
            {
                inventory.AddItem(harvestedCrop);
                if (!plant.IsContinuousGrowth)
                {
                    plant.IsActive = false;
                    plant.CurrentNestedArea.RemoveObjectFromArea(plant);
                }
                Debug.Log($"You harvested {harvestedCrop.Name} from the plant.");
                MessageLogManager.Instance.Log("item", harvestedCrop.Name, inventory.GetItemCount(harvestedCrop.Name));
            }
            else
            {
                Debug.LogError("Failed to generate the harvested crop.");
            }
        }
        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable interactable, PlayerInventory inventory)
    {
        return interactable is Plant plant && plant.IsActive;
    }
}

public class FishAction : IEnvironmentalAction
{
    public InteractionType Type => InteractionType.Tool;
    public string Name => "Fish";
    public int ActionPointCost => 1;

    public bool IsAvailable(Cell cell, PlayerInventory inventory)
    {
        return inventory.HasItem("Fishing Rod") && cell.isFishable;
    }

    public void ExecuteAction(Cell cell, PlayerInventory inventory)
    {
        if (!cell.isFishable) return;

        Debug.Log("Fishing commenced. Waiting for a bite...");
        int roll = UnityEngine.Random.Range(0, 100);
        if (roll < cell.FertilityValue)
        {
            Item fish = ItemGenerator.Instance.GenerateItem("Fish");
            inventory.AddItem(fish);
            MessageLogManager.Instance.Log("item", fish.Name, inventory.GetItemCount(fish.Name));
        }
        else
        {
            MessageLogManager.Instance.Log("special", "No fish caught this time.");
        }
        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }
}

public class DrinkInteraction : IEnvironmentalAction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Drink";
    public int ActionPointCost => 1;

    public bool IsAvailable(Cell cell, PlayerInventory inventory)
    {
        return cell.Terrain == TerrainType.Water;
    }

    public void ExecuteAction(Cell cell, PlayerInventory inventory)
    {
        if (cell.Terrain == TerrainType.Water)
        {
            Debug.Log("You drink some water from the nearby source.");
            PlayerStats.Instance.DecreaseHunger(5);
            MessageLogManager.Instance.Log("special", "You drank some fresh water.");
        }
        else
        {
            MessageLogManager.Instance.Log("special", "This is not a drinkable water source.");
        }
        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }
}

public class PickUpItemsAction : IEnvironmentalAction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Pick Up Item";
    public int ActionPointCost => 0;

    public bool IsAvailable(Cell cell, PlayerInventory inventory)
    {
        return cell.Items.Any();
    }

    public void ExecuteAction(Cell cell, PlayerInventory inventory)
    {
        foreach (var item in cell.Items.ToList())
        {
            inventory.AddItem(item);
            cell.Items.Remove(item);
        }
        MessageLogManager.Instance.Log("item", "Picked up all items.");
        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }
}

public class PickUpALLItemsAction : IEnvironmentalAction
{
    public InteractionType Type => InteractionType.Environmental;
    public string Name => "Pick Up All Items";
    public int ActionPointCost => 0;

    public bool IsAvailable(Cell cell, PlayerInventory inventory)
    {
        return cell.Items.Count > 1;
    }

    public void ExecuteAction(Cell cell, PlayerInventory inventory)
    {
        foreach (var item in cell.Items.ToList())
        {
            inventory.AddItem(item);
            cell.Items.Remove(item);
        }
        MessageLogManager.Instance.Log("item", "Picked up all available items.");
        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }
}

#endregion

#region Combat Interactions


public abstract class BaseCombatInteraction : IInteraction
{
    public abstract InteractionType Type { get; }
    public abstract string Name { get; }
    public abstract int ActionPointCost { get; }

    public virtual void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        Debug.Log($"{Name} Attack executed.");

        if (entity is Character character)
        {
            var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
            if (playerCharacter == null || playerCharacter.EquippedItems == null)
            {
                Debug.LogWarning("BaseCombatInteraction: Player character or EquippedItems dictionary is null.");
                return;
            }

            float finalAccuracy = CalculateAccuracy(character);
            float accuracyRoll = UnityEngine.Random.Range(0f, 100f);

            if (accuracyRoll < finalAccuracy)
            {
                bool isCriticalHit = DetermineCriticalHit();
                List<DamageType> damageTypes = GetDamageTypes();
                Dictionary<DamageType, int> damageByType = playerCharacter.GetWeaponDamage();

                foreach (var damageType in damageTypes)
                {
                    if (isCriticalHit)
                    {
                        float criticalMultiplier = playerCharacter.GetCriticalHitMultiplier() / 100f;
                        if (damageByType.ContainsKey(damageType))
                        {
                            damageByType[damageType] = Mathf.RoundToInt(damageByType[damageType] * criticalMultiplier);
                        }
                    }
                }

                Debug.Log($"{Name} Attack: Damage Breakdown:");
                foreach (var entry in damageByType)
                {
                    Debug.Log($"- {entry.Key}: {entry.Value}");
                }

                character.TakeDamage(damageByType, playerCharacter);
                playerCharacter.ApplyOnHitEffects(character);
            }
            else
            {
                Debug.Log($"{Name} Attack missed.");
                MessageLogManager.Instance.Log("combat_result", PlayerStats.Instance.PlayerCharacterName, character.Name, 0, false);
            }

            EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
        }
    }

    protected virtual float CalculateAccuracy(Character character)
    {
        var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        if (playerCharacter == null)
        {
            Debug.LogWarning("CalculateAccuracy: PlayerCharacter is null.");
            return 0f;
        }

        float baseAccuracy = 80f;
        float characterAccuracy = playerCharacter.GetStatValue("Perception");
        float enemyEvasion = character.IsPlayerVisible ? character.GetStatValue("Dexterity") : 0f;
        return Mathf.Clamp(baseAccuracy + characterAccuracy - enemyEvasion, 0f, 100f);
    }

    protected virtual List<DamageType> GetDamageTypes()
    {
        var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        if (playerCharacter == null || playerCharacter.EquippedItems == null)
        {
            Debug.LogWarning("GetDamageTypes: Player character or EquippedItems dictionary is null.");
            return new List<DamageType>();
        }

        List<DamageType> damageTypes = new List<DamageType>();

        if (playerCharacter.EquippedItems.TryGetValue(EquipmentSlot.MainHand, out Item mainHandItem) && mainHandItem != null)
        {
            if (!damageTypes.Contains(mainHandItem.DamageType))
                damageTypes.Add(mainHandItem.DamageType);

            foreach (var modifier in mainHandItem.Modifiers.Where(m => m.Key == "Damage"))
            {
                if (Enum.TryParse(modifier.Value.AffectedDamageType.ToString(), out DamageType infusedType))
                {
                    if (!damageTypes.Contains(infusedType))
                        damageTypes.Add(infusedType);
                }
            }
        }

        return damageTypes;
    }

    protected virtual bool DetermineCriticalHit()
    {
        var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        if (playerCharacter == null)
        {
            Debug.LogWarning("DetermineCriticalHit: PlayerCharacter is null.");
            return false;
        }

        return UnityEngine.Random.Range(0f, 100f) < playerCharacter.GetCriticalHitChance();
    }

    public abstract bool IsAvailable(IInteractable entity, PlayerInventory inventory);
}


public class PunchInteraction : BaseCombatInteraction
{
    public override InteractionType Type => InteractionType.Combat;
    public override string Name => "Punch";
    public override int ActionPointCost => 2;

    public override void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        Character attacker = PlayerStats.Instance.CurrentPlayerCharacter;
        Character target = entity as Character;

        // CODEXLOG003_ACTIONS_AAM: temporary Punch actor/target diagnostic.
        ActionAAMDiagnosticsLogger.LogEvent("[COMBAT EXECUTE]", "PunchInteraction.ExecuteInteraction",
            $"ActionName: {Name}\n" +
            $"ActionPointCost: {ActionPointCost}\n" +
            $"Attacker: {FormatCombatCharacter(attacker)}\n" +
            $"Target: {FormatCombatCharacter(target)}\n" +
            $"AttackerNestedArea: {FormatCombatArea(attacker?.CurrentNestedArea)}\n" +
            $"TargetNestedArea: {FormatCombatArea(target?.CurrentNestedArea)}\n" +
            $"TargetIsActive: {target?.IsActive.ToString() ?? "NULL"}\n" +
            $"TargetIsAlive: {target?.IsAlive.ToString() ?? "NULL"}");

        if (attacker != null && target != null)
        {
            attacker.PerformAttack(target, DamageType.Bludgeoning);
        }
    }

    public override bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        if (!(entity is Character target) || !target.IsActive)
        {
            return false;
        }

        Character attacker = PlayerStats.Instance.CurrentPlayerCharacter;
        if (attacker == null)
        {
            return false;
        }

        if (attacker.Anatomy == null)
        {
            return true;
        }

        return attacker.Anatomy.CanEquipSlot(EquipmentSlot.MainHand);
    }

    // CODEXLOG003_ACTIONS_AAM: temporary Punch actor/target diagnostic helper.
    private static string FormatCombatCharacter(Character character)
    {
        if (character == null) return "NULL";
        return $"{character.Name} [{character.IInteractableID}] ({character.GetType().Name})";
    }

    // CODEXLOG003_ACTIONS_AAM: temporary Punch actor/target diagnostic helper.
    private static string FormatCombatArea(INestedArea area)
    {
        if (area == null) return "NULL";
        return $"{area.Name} (ID={area.NestedAreaID}, Level={area.NestedAreaLevel})";
    }
}

public class SlashInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Combat;
    public string Name => "Slash";
    public int ActionPointCost => 2;

    public void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        if (entity is Character target)
        {
            var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
            if (playerCharacter != null)
            {
                playerCharacter.PerformAttack(target, DamageType.Slashing);
            }
        }
    }

    public bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        var mainHand = playerCharacter?.GetMainHandItem();
        return mainHand != null && mainHand.WeaponType == WeaponType.Sharp;
    }
}

public class StabInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Combat;
    public string Name => "Stab";
    public int ActionPointCost => 2;

    public void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        if (entity is Character target)
        {
            var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
            if (playerCharacter != null)
            {
                playerCharacter.PerformAttack(target, DamageType.Piercing);
            }
        }
    }

    public bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        var mainHand = playerCharacter?.GetMainHandItem();
        return mainHand != null && mainHand.WeaponType == WeaponType.Sharp;
    }
}

public class BashInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Combat;
    public string Name => "Bash";
    public int ActionPointCost => 2;

    public void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        if (entity is Character target)
        {
            var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
            if (playerCharacter != null)
            {
                playerCharacter.PerformAttack(target, DamageType.Bludgeoning);
            }
        }
    }

    public bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        var mainHand = playerCharacter?.GetMainHandItem();
        return mainHand != null && mainHand.WeaponType == WeaponType.Blunt;
    }
}

public class RendInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Combat;
    public string Name => "Rend";
    public int ActionPointCost => 2;

    public void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        if (entity is Character target)
        {
            var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
            if (playerCharacter != null)
            {
                playerCharacter.PerformAttack(target, DamageType.Rending);
            }
        }
    }

    public bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        var mainHand = playerCharacter?.GetMainHandItem();
        return mainHand != null && mainHand.WeaponType == WeaponType.Serrated;
    }
}

public class MagicInteraction : IInteraction
{
    public InteractionType Type => InteractionType.Combat;
    public string Name => "Magic Attack";
    public int ActionPointCost => 2;

    public void ExecuteInteraction(IInteractable entity, PlayerInventory inventory)
    {
        if (entity is NPC npc)
        {
            var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
            if (playerCharacter == null)
            {
                Debug.LogWarning("MagicInteraction: Player character is null.");
                return;
            }

            float perception = playerCharacter.GetStatValue("Perception");
            float enemyEvasion = npc.Intelligence;
            float finalAccuracy = Mathf.Clamp(perception - enemyEvasion, 0f, 100f);
            float accuracyRoll = UnityEngine.Random.Range(0f, 100f);

            if (accuracyRoll < finalAccuracy)
            {
                Dictionary<DamageType, int> magicDamage = playerCharacter.GetWeaponDamage();

                Debug.Log($"Magic Attack: Damage Breakdown:");
                foreach (var entry in magicDamage)
                {
                    Debug.Log($"- {entry.Key}: {entry.Value}");
                }

                npc.TakeDamage(magicDamage, playerCharacter);
                playerCharacter.ApplyOnHitEffects(npc);
            }
            else
            {
                Debug.Log($"{Name} Attack missed.");
                MessageLogManager.Instance.Log("combat_result", PlayerStats.Instance.PlayerCharacterName, npc.Name, 0, false);
            }
        }
        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }

    public bool IsAvailable(IInteractable entity, PlayerInventory inventory)
    {
        var playerCharacter = PlayerStats.Instance.CurrentPlayerCharacter;
        return playerCharacter.EquippedItems.TryGetValue(EquipmentSlot.MainHand, out Item item)
               && item != null && item.WeaponType == WeaponType.Magic;
    }
}


#endregion


// Special Interactions



public class ClaimLandInteraction : IEnvironmentalAction
{
    public InteractionType Type => InteractionType.Special;
    public string Name => "Claim Land";
    public int ActionPointCost => 1;

    public bool IsAvailable(Cell cell, PlayerInventory inventory)
    {
        if (PlayerStats.Instance.AdaptiveActionMenuPanel != AdapativeActionMenu.Special) return false;
        if (cell == null || PlayerStats.Instance.CurrentNestedArea == null) return false;

        int parentCellID = PlayerStats.Instance.CurrentNestedArea.ParentCellID;
        var parentCell = MapGenerator.Instance.GetCellByID(parentCellID);

        return parentCell != null && !parentCell.IsOwned;
    }

    public void ExecuteAction(Cell cell, PlayerInventory inventory)
    {
        var currentArea = PlayerStats.Instance.CurrentNestedArea;
        if (currentArea == null) return;

        string areaName = currentArea.Name;

        UIController.Instance.ShowConfirmationPopup(
            $"Are you sure you want to make {areaName} your home? You can only have one home!",
            () => ClaimLand(cell, inventory),
            () => MessageLogManager.Instance.Log("special", "Claim Land action was cancelled.")
        );
    }

    private void ClaimLand(Cell cell, PlayerInventory inventory)
    {
        var parentCell = MapGenerator.Instance.GetCellByID(PlayerStats.Instance.CurrentNestedArea.ParentCellID);
        if (parentCell != null && !parentCell.IsOwned)
        {
            parentCell.IsOwned = true;
            parentCell.OwnedBy = PlayerStats.Instance.PlayerName;
            parentCell.IsOwnedByPlayer = true;

            MessageLogManager.Instance.Log("special", "You have successfully claimed the land!");
        }
        else
        {
            MessageLogManager.Instance.Log("special", "Cannot claim this land. It is either already owned or invalid.");
        }
        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }
}

public abstract class BaseConstructableInteraction : IEnvironmentalAction
{
    public abstract InteractionType Type { get; }
    public abstract string Name { get; }
    public abstract int ActionPointCost { get; }
    public abstract string ObjectString { get; }

    public bool IsAvailable(Cell cell, PlayerInventory inventory)
    {
        if (PlayerStats.Instance.AdaptiveActionMenuPanel != AdapativeActionMenu.Special) return false;
        return !cell.Objects.Any() && inventory.HasConstructable(ObjectString);
    }

    public void ExecuteAction(Cell cell, PlayerInventory inventory)
    {
        if (!IsAvailable(cell, inventory))
        {
            MessageLogManager.Instance.Log("special", $"Cannot place {ObjectString}. Either the cell is not empty or the player lacks the item.");
            return;
        }

        bool success = ObjectPlacementFactory.Instance.PlaceObjectAt(ObjectString, cell.Coordinates, PlayerStats.Instance.CurrentNestedArea);
        if (success)
        {
            Item constructableItem = inventory.GetInventoryContainers()
                .SelectMany(container => container.Items)
                .FirstOrDefault(item => item.ObjectString == ObjectString);

            if (constructableItem != null)
            {
                inventory.RemoveItem(constructableItem.ItemInGameName, 1);
                MessageLogManager.Instance.Log("item", $"Successfully placed {constructableItem.ItemInGameName} at {cell.Coordinates}.");
            }
            else
            {
                MessageLogManager.Instance.Log("special", $"Failed to find {ObjectString} in inventory after placing.");
            }
        }
        else
        {
            MessageLogManager.Instance.Log("special", $"Failed to place {ObjectString} at {cell.Coordinates}.");
        }
        EndOfTurnManager.Instance.AddTurnProgress(ActionPointCost);
    }
}



public class PlaceWoodenWallInteraction : BaseConstructableInteraction
{
    public override InteractionType Type => InteractionType.Environmental;
    public override string Name => "Place Wooden Wall";
    public override int ActionPointCost => 1;
    public override string ObjectString => "WoodenWall";
}

public class PlaceWoodenDoorInteraction : BaseConstructableInteraction
{
    public override InteractionType Type => InteractionType.Environmental;
    public override string Name => "Place Wooden Door";
    public override int ActionPointCost => 1;
    public override string ObjectString => "WoodenDoor";
}

public class PlaceAnvilInteraction : BaseConstructableInteraction
{
    public override InteractionType Type => InteractionType.Environmental;
    public override string Name => "Place Anvil";
    public override int ActionPointCost => 2; // Maybe placing an Anvil is a heavier action
    public override string ObjectString => "Anvil";
}

public class PlaceBedInteraction : BaseConstructableInteraction
{
    public override InteractionType Type => InteractionType.Environmental;
    public override string Name => "Place Bed";
    public override int ActionPointCost => 1;
    public override string ObjectString => "Bed";
}

public class PlaceStoneWallInteraction : BaseConstructableInteraction
{
    public override InteractionType Type => InteractionType.Environmental;
    public override string Name => "Place Stone Wall";
    public override int ActionPointCost => 1;
    public override string ObjectString => "StoneWall";
}
