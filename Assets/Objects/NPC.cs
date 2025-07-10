using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class NPC : Character
{
    #region Identification
    public int NPCID;
    public string FirstName { get; set; }
    public string Surname { get; set; }
    public NPCRole Role { get; set; }
    public Race Race { get; set; }
    public SubRace SubRace { get; set; }
    public Personality Personality { get; set; }
    public override char Symbol { get; set; } = 'N';  // Override to use the NPC symbol
    public override string Color { get; set; } = "#FFFFFF";  // Override to use the NPC color

    public override string Name
    {
        get
        {
            if (string.IsNullOrEmpty(Title))
            {
                return $"{FirstName} {Surname}";
            }
            else
            {
                return $"{FirstName} {Title}";
            }
        }
    }

    public Village HomeVillage;
    public NewsType NewsType;
    #endregion

    #region Role Info
    public bool IsCraftsman;
    public bool HasTitle;
    public string Title;
    #endregion

    #region Constructor
    public NPC()
    {
        IsActive = true;
        IsHostile = false;
        Health = 100;
        Status = NPCStatus.Idling;
        IsPassable = false;
        Speed = Mathf.Max(1, Speed);
        ActionPoints = MaxActionPoints;
        CurrentNeed = new Need();

        InitializeInteractions();
    }
    #endregion

    #region Needs Management
    public Need CurrentNeed { get; set; }

    public void SetNeed(bool hasNeed, string itemName, int numberRequired, bool isFinancialNeed, bool isFavourNeed)
    {
        CurrentNeed = new Need(hasNeed, itemName, numberRequired, isFinancialNeed, isFavourNeed);
    }

    public void ClearNeed()
    {
        CurrentNeed = new Need();
    }
    #endregion

    #region Interactions
    protected override void InitializeInteractions()
    {
        interactions = new List<IInteraction>
        {
            new TalkInteraction(),
            new TradeInteraction(),
            new InspectInteraction(),
            new PickPocketInteraction(),
            new PunchInteraction(),
            new ShoveInteraction(),
            new StabInteraction(),
            new SlashInteraction(),
            new BashInteraction(),
            new MagicInteraction()
        };
    }
    #endregion

    protected override void OnDeath()
    {
        Debug.Log($"NPC {Name} has died!");
        ClearLineOfSight();  // Clear the NPC's line of sight

        if (IsInNestedArea && CurrentNestedArea != null)
        {
            // Generate a corpse at the NPC's position
            Corpse corpse = Corpse.GenerateCorpse(this, $"{Name}'s Corpse");

            // Remove the NPC from the nested area
            RemoveFromNestedArea();
        }

        IsActive = false;  // Mark the NPC as inactive
    }

    public virtual string GetDescription()
    {
        // Start with a basic description
        string description = $"{Name} is a {Race.Name} {Role}";

        // Add age if available
        if (Age > 0)
        {
            description += $" who is {Age} years old";
        }

        // Add location information if they have a home
        if (!string.IsNullOrEmpty(Home))
        {
            description += $" and lives in {Home}.";
        }
        else
        {
            description += ".";
        }

        // Add equipped items description
        string equippedItemsDescription = GetEquippedItemsDescription();
        if (!string.IsNullOrEmpty(equippedItemsDescription))
        {
            description += $" They are wearing {equippedItemsDescription}.";
        }
        else
        {
            description += " They are currently not wearing anything!";
        }

        return description;
    }

    private string GetEquippedItemsDescription()
    {
        List<string> equippedItemsDescriptions = new List<string>();
        string mainHandDescription = null;
        string feetDescription = null;

        if (EquippedItems == null || EquippedItems.Count == 0 || EquippedItems.All(item => item.Value == null))
        {
            return null; // No equipment to describe
        }

        foreach (var item in EquippedItems)
        {
            EquipmentSlot slot = item.Key;
            Item equippedItem = item.Value;

            if (equippedItem != null)
            {
                switch (slot)
                {
                    case EquipmentSlot.Head:
                    case EquipmentSlot.Face:
                    case EquipmentSlot.Body:
                    case EquipmentSlot.Legs:
                    case EquipmentSlot.Neck:
                    case EquipmentSlot.Waist:
                        string slotDescription = slot switch
                        {
                            EquipmentSlot.Head => "on their head",
                            EquipmentSlot.Face => "on their face",
                            EquipmentSlot.Body => "on their body",
                            EquipmentSlot.Legs => "on their legs",
                            EquipmentSlot.Neck => "around their neck",
                            EquipmentSlot.Waist => "around their waist",
                            _ => $"on their {slot.ToString().ToLower()}",
                        };
                        equippedItemsDescriptions.Add($"a {equippedItem.ItemInGameName} {slotDescription}");
                        break;

                    case EquipmentSlot.Feet:
                        feetDescription = $"and {equippedItem.ItemInGameName} on their feet";
                        break;

                    case EquipmentSlot.MainHand:
                        mainHandDescription = $"carrying a {equippedItem.ItemInGameName} in their main hand";
                        break;
                }
            }
        }

        string finalDescription = string.Join(", ", equippedItemsDescriptions);

        if (!string.IsNullOrEmpty(feetDescription))
        {
            finalDescription += (finalDescription.Length > 0 ? ", " : "") + feetDescription;
        }

        if (!string.IsNullOrEmpty(mainHandDescription))
        {
            finalDescription += (finalDescription.Length > 0 ? " " : "") + mainHandDescription;
        }

        return finalDescription;
    }

    public void AdjustNPCStateBasedOnRelationships()
    {
        foreach (var otherNPC in CurrentNestedArea.GetAllNPCsInArea())
        {
            if (Relationships.TryGetValue(otherNPC.IInteractableID, out float relationValue))
            {
                if (relationValue < -50 && stateMachine.CurrentState is not HostileState)
                {
                    stateMachine.ChangeState(new HostileState()); // Become aggressive to enemies
                }
                else if (relationValue > 50 && otherNPC.Role == NPCRole.Guard && stateMachine.CurrentState is not FollowingState)
                {
                    stateMachine.ChangeState(new FollowingState()); // Follow allies for protection
                }
            }
        }
    }

}

public enum NPCPersonalityType
{
    Default,
    Friendly,
    Grumpy,
    Shy,
    Confident,
    Mysterious,
    Cheerful,
    Sarcastic,
    Nervous,
    Skittish,
    Curious,
    Arrogant,
    Optimistic,
    Pessimistic,
    Brave,
    Cowardly,
    Generous,
    Greedy,
    Intellectual,
    Naive,
    Mischievous,
    Honest,
    Snivelling,
    Jealous,
    Deceitful
}

public enum NPCRole
{
    Citizen, // General inhabitants of an area
    Villager, // Someone who lives in the Villager
    Blacksmith, // Crafts and repairs weapons and armor
    Mayor, // Leader of a village or town
    Healer, // Provides health restoration services and potions
    Merchant, // Buys and sells goods
    Innkeeper, // Runs an inn, provides lodging
    Farmer, // Produces food, might offer related quests
    Scholar, // Offers knowledge, might give lore-related quests
    Guard, // Protects an area, might offer escort quests
    Bandit, // Hostile NPCs that can attack the player
    EvilWizard, // A powerful enemy with magical abilities
    Hermit, // Lives in isolation, can offer unique items or wisdom
    Explorer, // Can offer quests to uncharted territories
    Miner, // Provides minerals, might need help with mining tasks
    Fisher, // Provides fish, might offer fishing quests
    Alchemist, // Crafts potions, might need ingredients collected
    Priest, // Offers spiritual guidance or blessings
    Bard, // Shares tales, might give hints to treasures or quests
    Thief, // Offers stealth-related quests or items
    Noble, // Offers high-profile quests, involved in political intrigue
    Adventurer, // Similar to the player, can offer competitive quests or help
    Craftsman, // Specializes in non-weapon items, like furniture or tools
    Wizard, // Non-evil magic user, can offer magical items or quests
    Beastmaster, // Can offer quests or items related to animals
    Trader,
    Warrior,
    Hunter,
    Scout,
    Herbalist,
    Chef,
    Carpenter,
    Leader,
    Default,
    Any,
    BountyHunter // Offers quests to capture or defeat certain NPCs or monsters
}

public class Need
{
    public bool HasNeed { get; set; }
    public string ItemName { get; set; }
    public int NumberRequired { get; set; } // How many of the item the NPC wants
    public bool Finance { get; set; } // Determines if the NPC intends to reward the player with Finance or Favour. If Finance is True, Favour is False.
    public bool Favour { get; set; } // Determines if the NPC intends to reward the player with Finance or Favour. If Favour is True, Finance is False.

    public Need(bool hasNeed = false, string itemName = "", int numberRequired = 1, bool isFinance = false, bool isFavour = false)
    {
        HasNeed = hasNeed;
        ItemName = itemName;
        NumberRequired = numberRequired;
        Finance = isFinance;
        Favour = isFavour;
    }
}

public enum NPCStatus
{
    Normal,
    Injured,
    Asleep,
    Fleeing,
    Dead,
    Idling,
    Hostile,
    Patrolling,
    Following,
    Chasing,
    TrueIdle,

}

public enum Direction
{
    North,
    South,
    East,
    West
}

public enum NPCStance
{
    Neutral,
    Default,
    Friendly,
    Hostile,
    TrueIdle,
    Following,
    Fleeing 
}
