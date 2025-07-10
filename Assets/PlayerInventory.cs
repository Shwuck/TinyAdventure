using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerInventory : CharacterInventory
{
    private static readonly object padlock = new object();
    private static PlayerInventory instance;
    private static bool isInitialized = false;

    public static PlayerInventory Instance
    {
        get
        {
            lock (padlock)
            {
                if (instance == null)
                {
                    Debug.LogWarning("PlayerInventory: Instance was accessed before initialization! Initializing now.");
                    instance = new PlayerInventory();
                }
                return instance;
            }
        }
    }

    private int currentCharacterID;

    private PlayerInventory() : base() { }  // Ensures proper inheritance from CharacterInventory

    /// Explicitly initializes PlayerInventory to avoid timing issues.
    public static void Initialize()
    {
        if (instance == null)
        {
            instance = new PlayerInventory();
            isInitialized = true;
            Debug.Log("PlayerInventory: Successfully initialized.");
        }
        else
        {
            Debug.LogWarning("PlayerInventory: Already initialized.");
        }
    }

    /// Switches the PlayerInventory to match the selected PlayerCharacter's inventory.
    public void SwitchCharacterInventory(PlayerCharacter character)
    {
        if (character == null)
        {
            Debug.LogWarning("PlayerInventory: Attempted to switch to a null character.");
            return;
        }

        Debug.Log($"Switching inventory to {character.FullName}...");

        // Log items BEFORE the switch
        Debug.Log("PlayerInventory BEFORE switch:");
        if (Items.Count == 0) Debug.Log("- Inventory is empty.");
        else
        {
            foreach (var container in Items)
            {
                Debug.Log($"- {container.Key}: {container.Value.Amount}");
            }
        }

        currentCharacterID = character.PlayerCharacterID;

        CopyInventory(character.Inventory);

        // Log items AFTER the switch
        Debug.Log("PlayerInventory AFTER switch:");
        if (Items.Count == 0) Debug.Log("- Inventory is empty.");
        else
        {
            foreach (var container in Items)
            {
                Debug.Log($"- {container.Key}: {container.Value.Amount}");
            }
        }
    }

    private void CopyInventory(CharacterInventory sourceInventory)
    {
        if (sourceInventory == null)
        {
            Debug.LogError("CopyInventory: Source inventory is null.");
            return;
        }

        Debug.Log($"Copying inventory from {sourceInventory.GetInventoryContainers().Count} containers...");

        // STEP 1: Create a temporary storage container
        Dictionary<string, InventoryContainer> tempInventory = new Dictionary<string, InventoryContainer>();

        // STEP 2: Copy items from CharacterInventory to tempInventory
        foreach (var container in sourceInventory.GetInventoryContainers())
        {
            if (!tempInventory.ContainsKey(container.Name))
            {
                tempInventory[container.Name] = new InventoryContainer(container.Name, new List<Item>());
            }

            foreach (var item in container.Items)
            {
                tempInventory[container.Name].AddItem(item);
            }
        }

        // Log the temp inventory BEFORE moving
        System.Text.StringBuilder tempLog = new System.Text.StringBuilder();
        tempLog.AppendLine("Temp Inventory BEFORE Transfer:");
        foreach (var container in tempInventory)
        {
            tempLog.AppendLine($"- {container.Key}: {container.Value.Amount}");
        }
        Debug.Log(tempLog.ToString());

        // STEP 3: Clear PlayerInventory before adding new items
        RemoveAllItems();

        Debug.Log("Inventory cleared. Now transferring items from Temp Inventory...");

        // STEP 4: Move items from tempInventory to PlayerInventory
        foreach (var kvp in tempInventory)
        {
            foreach (var item in kvp.Value.Items)
            {
                AddItem(item, 1);  // Mimics how ContainerPanelUI moves items
                Debug.Log($"Transferred {item.ItemInGameName} x1 to PlayerInventory.");
            }
        }

        // STEP 5: Clear the temporary storage
        tempInventory.Clear();

        // STEP 6: Log the final PlayerInventory contents
        System.Text.StringBuilder finalLog = new System.Text.StringBuilder();
        finalLog.AppendLine("PlayerInventory AFTER Transfer:");
        if (Items.Count == 0) finalLog.AppendLine("- Inventory is empty.");
        else
        {
            foreach (var container in Items)
            {
                finalLog.AppendLine($"- {container.Key}: {container.Value.Amount}");
            }
        }
        Debug.Log(finalLog.ToString());
    }



    /// Tries to give required items to an NPC if the player has them.
    public virtual bool TryToGiveItemsToNPC(NPC npc)
    {
        if (npc?.CurrentNeed != null && npc.CurrentNeed.HasNeed)
        {
            bool hasRequiredItems = HasItem(npc.CurrentNeed.ItemName) &&
                                    GetItemCount(npc.CurrentNeed.ItemName) >= npc.CurrentNeed.NumberRequired;

            if (hasRequiredItems)
            {
                RemoveItems(npc.CurrentNeed.ItemName, npc.CurrentNeed.NumberRequired);
                npc.CurrentNeed = null;
                return true;
            }
        }
        return false;
    }
}
