using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class Inventory
{
    #region Fields and Properties
    public Character Owner { get; set; }
    public Dictionary<string, InventoryContainer> Items { get; protected set; } = new Dictionary<string, InventoryContainer>();
    public int MaxCapacity { get; protected set; } = 200000;

    #endregion

    #region Item Management

    public virtual void AddItem(Item item, int amount = 1)
    {
        if (item == null)
        {
            Debug.LogWarning("Inventory: Attempted to add a null item.");
            return;
        }

        int currentWeight = Items.Sum(container => container.Value.Items.Sum(i => i.Value));
        if (currentWeight + (item.Value * amount) > MaxCapacity)
        {
            Debug.LogWarning("Inventory: Cannot add item. Inventory capacity exceeded.");
            return;
        }

        if (Items.TryGetValue(item.ItemInGameName, out InventoryContainer container))
        {
            for (int i = 0; i < amount; i++)
            {
                container.AddItem(item);
            }
        }
        else
        {
            var newContainer = new InventoryContainer(item.ItemInGameName, new List<Item>());
            for (int i = 0; i < amount; i++)
            {
                newContainer.AddItem(item);
            }
            Items.Add(item.ItemInGameName, newContainer);
        }
    }

    public virtual void RemoveItem(string itemName, int amount = 1)
    {
        if (Items.TryGetValue(itemName, out InventoryContainer container))
        {
            for (int i = 0; i < amount && container.Items.Count > 0; i++)
            {
                Item itemToRemove = container.Items.FirstOrDefault(item => item.ItemInGameName == itemName);
                if (itemToRemove != null)
                {
                    container.RemoveItem(itemToRemove);
                }
            }

            if (container.Amount <= 0)
            {
                Items.Remove(itemName);
            }
        }
    }

    public virtual void RemoveAllItems()
    {
        Items.Clear();
        Debug.Log("Inventory: All items removed.");
    }

    public virtual void RemoveAllItemsOfType(string itemName)
    {
        if (Items.ContainsKey(itemName))
        {
            Items.Remove(itemName);
            Debug.Log($"Inventory: Removed all {itemName}.");
        }
        else
        {
            Debug.LogWarning($"Inventory: No {itemName} found.");
        }
    }

    public virtual void RemoveItemsByType(ItemType itemType, int amount)
    {
        int removedAmount = 0;

        foreach (var container in Items.Values)
        {
            var itemsToRemove = container.Items.Where(item => item.ItemTypes.Contains(itemType)).ToList();

            foreach (var item in itemsToRemove)
            {
                container.RemoveItem(item);
                removedAmount++;

                if (removedAmount >= amount)
                {
                    return;
                }
            }
        }

        Debug.LogWarning($"RemoveItemsByType: Only removed {removedAmount} out of {amount} {itemType}.");
    }

    public virtual void RemoveItems(string itemName, int amount)
    {
        if (Items.TryGetValue(itemName, out InventoryContainer container))
        {
            for (int i = 0; i < amount && container.Items.Count > 0; i++)
            {
                container.RemoveItem(container.Items.First());
            }

            if (container.Amount <= 0)
            {
                Items.Remove(itemName);
            }
        }
    }

    #endregion

    #region Equipping

    public bool IsEquipped(Item item)
    {
        return Owner?.EquippedItems.ContainsValue(item) ?? false;
    }

    public virtual void EquipItem(Item item, EquipmentSlot slot)
    {
        if (item == null)
        {
            Debug.LogWarning("Inventory: Attempted to equip a null item.");
            return;
        }

        if (!Items.ContainsKey(item.ItemInGameName))
        {
            Debug.LogWarning($"Inventory: Cannot equip {item.ItemInGameName}, it is not in inventory.");
            return;
        }

        if (Owner == null || Owner.Anatomy == null)
        {
            Debug.LogWarning("Inventory: Cannot equip item because the owning character or their anatomy is missing.");
            return;
        }

        if (!Owner.Anatomy.CanEquipSlot(slot))
        {
            Debug.LogWarning($"{Owner.Name} cannot equip {item.ItemInGameName} in {slot} due to missing or non-functional anatomy.");
            return;
        }

        if (Owner.EquippedItems.ContainsKey(slot))
        {
            UnEquipItem(slot);
        }

        Owner.EquippedItems[slot] = item;
        item.IsEquipped = true;

        Debug.Log($"{Owner.Name} equipped {item.ItemInGameName} in {slot}.");
    }

    public virtual void UnEquipItem(EquipmentSlot slot)
    {
        if (Owner == null || Owner.EquippedItems == null)
        {
            Debug.LogWarning("UnEquipItem: Owner or EquippedItems dictionary is null.");
            return;
        }

        if (!Owner.EquippedItems.TryGetValue(slot, out Item item) || item == null)
        {
            Debug.LogWarning($"UnEquipItem: No item equipped in {slot} to unequip.");
            return;
        }

        Owner.EquippedItems.Remove(slot);
        item.IsEquipped = false;

        Debug.Log($"{Owner.Name} unequipped {item.ItemInGameName} from {slot}.");
    }

    public void HandleLostEquipment()
    {
        if (Owner == null || Owner.Anatomy == null)
        {
            Debug.LogWarning("HandleLostEquipment: Owner or Anatomy is null.");
            return;
        }

        List<EquipmentSlot> validSlots = Owner.Anatomy.GetActiveEquipmentSlots();

        var toRemove = Owner.EquippedItems
            .Where(kv => !validSlots.Contains(kv.Key))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var slot in toRemove)
        {
            UnEquipItem(slot);
            Debug.Log($"{Owner.Name} lost a body part, unequipping item in {slot}.");
        }
    }

    #endregion



    #region Inventory Queries

    public virtual bool HasItem(string itemName) => Items.ContainsKey(itemName) && Items[itemName].Amount > 0;

    public virtual int GetItemCount(string itemName) => Items.ContainsKey(itemName) ? Items[itemName].Amount : 0;

    public virtual List<InventoryContainer> GetInventoryContainers() => Items.Values.ToList();

    public virtual List<Item> GetAllItems() => Items.Values.SelectMany(container => container.Items).ToList();

    public virtual bool HasItemOfType(ItemType itemType)
    {
        return Items.Values.Any(container => container.Items.Any(item => item.ItemTypes.Contains(itemType)));
    }

    public virtual bool GetItemsOfType(ItemType itemType, int number)
    {
        int itemCount = 0;

        foreach (var container in Items.Values)
        {
            foreach (var item in container.Items)
            {
                if (item.ItemTypes.Contains(itemType))
                {
                    itemCount++;
                    if (itemCount >= number) return true;
                }
            }
        }
        return false;
    }

    public virtual bool HasConstructable(string objectString)
    {
        return Items.Values.Any(container => container.Items.Any(item => item.ObjectString == objectString));
    }

    public bool CanAddItem(Item item, int amount = 1)
    {
        if (item == null) return false;

        int currentWeight = 0;

        foreach (var container in Items.Values)
        {
            foreach (var i in container.Items)
            {
                currentWeight += i.Value;
            }
        }

        return (currentWeight + (item.Value * amount)) <= MaxCapacity;
    }


    #endregion
}



public class InventoryContainer
{
    public string Name { get; private set; }
    public List<Item> Items { get; private set; }

    public int Amount => Items.Count; // Read-only, calculated based on the number of items

    public InventoryContainer(string name, List<Item> items)
    {
        Name = name;
        Items = items;
    }

    public void AddItem(Item item)
    {
        Items.Add(item);
    }

    public void RemoveItem(Item item)
    {
        Items.Remove(item);
    }
}
