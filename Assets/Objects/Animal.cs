using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using System.Collections;

public class Animal : Character
{
    #region Unique Animal Properties
    public int AnimalID { get; set; }
    public bool IsPredator { get; set; }
    public AnimalSize Size { get; set; }
    public List<TerrainRarity> TerrainRarities { get; set; }
    public List<string> CommonColours { get; set; }
    public bool IsLeader { get; set; } = false;
    public bool IsPack { get; set; }
    public string PackID { get; set; }
    public bool IsHerd { get; set; }
    public string HerdID { get; set; }
    public bool IsDomestic { get; set; }
    public bool IsTame { get; set; }
    public HideType Hide { get; set; } = HideType.None;
    public List<TerrainType> PreferredTerrains { get; set; } = new List<TerrainType>();

    public override char Symbol { get; set; } = 'A';  // Override to use the Animal symbol
    public override string Color { get; set; } = "White";  // Override to use the Animal color
    #endregion

    #region Constructor
    public Animal()
    {
        IsAlive = true;
        IsActive = true;
        IsHostile = false;
        Status = NPCStatus.Idling;
        IsPassable = false;
        Speed = Mathf.Max(1, Speed);
        ActionPoints = MaxActionPoints;
        CanLeaveArea = true;
        InitializeInteractions();
    }

    public Animal(AnimalCreationData data) : this()
    {
        Name = data.AnimalName;
        // Symbol and Color are handled by overrides now, so no need to assign them here.
        IsPredator = data.IsPredator;
        Size = data.Size;
        TerrainRarities = data.TerrainRarities;
        CommonColours = data.CommonColours;
        Health = data.Health;
        MaxHealth = data.MaxHealth;
        Strength = data.Strength;
        Speed = data.Speed;
        Awareness = data.Awareness;
        Charisma = data.Charisma;
        Dexterity = data.Dexterity;
        Constitution = data.Constitution;
        Wisdom = data.Wisdom;
        Luck = data.Luck;
        Intelligence = data.Intelligence;
        PreferredTerrains = data.PreferredTerrains;
        CoverType = data.CoverType;
        IsHostile = data.IsHostile;
        IsPack = data.IsPack;
        IsHerd = data.IsHerd;
        IsDomestic = data.IsDomestic;
        MaxActionPoints = 5;

        // Set the initial state to IdleState
        stateMachine.ChangeState(new IdleState());

        Hide = data.Hide;

        if (IsPack)
        {
            // 10% chance to be the pack leader (or assign via procedural generation)
            IsLeader = UnityEngine.Random.value < 0.1f;
        }
    }
    #endregion


    #region Combat
    public override int GetResistance(DamageType damageType)
    {
        // Switch based on the DamageType enum value
        float resistance = Hide switch
        {
            HideType.LargeScales => damageType == DamageType.Slashing ? 15f : 5f,
            HideType.SmallScales => damageType == DamageType.Piercing ? 10f : 5f,
            HideType.HeavyFur => damageType == DamageType.Bludgeoning ? 20f : 10f,
            HideType.ThinFur => damageType == DamageType.Rending ? 5f : 2f,
            HideType.ThickHide => damageType == DamageType.Bludgeoning ? 15f : 7f,
            HideType.ThinHide => damageType == DamageType.Slashing ? 5f : 2f,
            HideType.SoftFeather => damageType == DamageType.Piercing ? 5f : 0f,
            HideType.StiffFeather => damageType == DamageType.Slashing ? 10f : 5f,
            HideType.DenseChitin => damageType == DamageType.Bludgeoning ? 20f : 10f,
            HideType.LightChitin => damageType == DamageType.Piercing ? 10f : 5f,
            HideType.FineWool => damageType == DamageType.Bludgeoning ? 5f : 2f,
            HideType.CoarseWool => damageType == DamageType.Rending ? 7f : 3f,
            HideType.Bark => damageType == DamageType.Bludgeoning ? 25f : 15f,
            HideType.Silk => damageType == DamageType.Slashing ? 10f : 5f,
            HideType.Crystal => damageType == DamageType.Bludgeoning ? 30f : 20f,
            HideType.Metallic => damageType == DamageType.Slashing ? 20f : 10f,
            HideType.Spiked => damageType == DamageType.Piercing ? 15f : 10f,
            HideType.Stone => damageType == DamageType.Bludgeoning ? 35f : 25f,
            HideType.None => 0f,
            _ => 0f,
        };

        return Mathf.RoundToInt(resistance);
    }
    #endregion


    #region Interactions
    protected override void InitializeInteractions()
    {
        // Call the base class method to initialize default interactions
        base.InitializeInteractions();

        // Add new interactions specific to this subclass
        interactions.Add(new PetInteraction());
        interactions.Add(new InspectInteraction());
        interactions.Add(new FeedAnimalInteraction());
        interactions.Add(new TameAnimalInteraction());
        interactions.Add(new MountAnimalInteraction());
    }
    #endregion
    public Dictionary<ItemType, int> GenerateLoot()
    {
        var loot = new Dictionary<ItemType, int>();

        // Loot chances (independent of each other)
        float peltChance = 0.5f; // 50% chance to drop a pelt
        float meatChance = 0.8f; // 80% chance to drop meat
        float boneChance = 0.7f; // 70% chance to drop bones

        // Loot scaling based on animal size
        int sizeMultiplier = GetSizeMultiplier(); // Scale loot based on animal size

        // Pelt
        if (UnityEngine.Random.value <= peltChance) // Use UnityEngine.Random here
        {
            int peltCount = Mathf.Max(1, sizeMultiplier); // Ensure at least 1 pelt
            loot.Add(ItemType.Pelt, peltCount);
        }

        // Meat
        if (UnityEngine.Random.value <= meatChance) // Use UnityEngine.Random here
        {
            int meatCount = Mathf.Max(1, sizeMultiplier * 2); // Larger animals drop more meat
            loot.Add(ItemType.Meat, meatCount);
        }

        // Bone
        if (UnityEngine.Random.value <= boneChance) // Use UnityEngine.Random here
        {
            int boneCount = Mathf.Max(1, sizeMultiplier); // Ensure at least 1 bone
            loot.Add(ItemType.Bone, boneCount);
        }

        return loot;
    }

    public Dictionary<string, int> GenerateBasicLoot()
    {
        var loot = new Dictionary<string, int>();

        // Loot chances for basic items
        float peltChance = 0.5f; // 50% chance to drop a pelt
        float meatChance = 0.8f; // 80% chance to drop meat
        float boneChance = 0.7f; // 70% chance to drop bones

        // Loot scaling based on animal size
        int sizeMultiplier = GetSizeMultiplier();

        // Pelt
        if (UnityEngine.Random.value <= peltChance)
        {
            int peltCount = Mathf.Max(1, sizeMultiplier);
            loot.Add("Tattered Pelt", peltCount);
        }

        // Meat
        if (UnityEngine.Random.value <= meatChance)
        {
            int meatCount = Mathf.Max(1, sizeMultiplier * 2);
            loot.Add("Raw Meat Chunk", meatCount);
        }

        // Bone
        if (UnityEngine.Random.value <= boneChance)
        {
            int boneCount = Mathf.Max(1, sizeMultiplier);
            loot.Add("BoneChipping", boneCount);
        }

        return loot;
    }

    public void CapMaxHealthBasedOnSize()
    {
        int maxAllowedHealth = GetMaxHealthCapForSize();

        if (MaxHealth > maxAllowedHealth)
        {
            MaxHealth = maxAllowedHealth;
            Debug.Log($"MaxHealth capped to {MaxHealth} based on animal size.");
        }

        // Optionally, ensure current health is also capped if necessary
        Health = Mathf.Min(Health, MaxHealth);
    }

    private int GetMaxHealthCapForSize()
    {
        return Size switch
        {
            AnimalSize.Tiny => 10,
            AnimalSize.Small => 25,
            AnimalSize.Medium => 50,
            AnimalSize.Large => 100,
            AnimalSize.Huge => 200,
            _ => 100 // Default cap for unknown sizes
        };
    }


    private int GetSizeMultiplier()
    {
        // Scale multiplier based on animal size
        return Size switch
        {
            AnimalSize.Tiny => 1,      // Tiny animals drop minimal loot
            AnimalSize.Small => 2,     // Small animals drop small amounts of loot
            AnimalSize.Medium => 4,    // Medium-sized animals drop more loot
            AnimalSize.Large => 6,     // Large animals drop even more loot
            AnimalSize.Huge => 8,      // Huge animals drop the most loot
            _ => 1,                    // Default case for any other size
        };
    }

    protected override void OnDeath()
    {
        Debug.Log($"NPC {Name} has died!");
        ClearLineOfSight();  // Clear the NPC's line of sight

        if (IsInNestedArea && CurrentNestedArea != null)
        {

            // Generate a corpse at the NPC's position
            Carcass carcass = Carcass.GenerateCarcass(this, $"{Name}'s Corpse");

            // Remove the NPC from the nested area
            RemoveFromNestedArea();
        }

        IsActive = false;  // Mark the NPC as inactive
    }

    public void AdjustAnimalBehavior()
    {
        if (IsPack && !string.IsNullOrEmpty(PackID))
        {
            // Find the pack leader and follow them
            Character packLeader = CurrentNestedArea.GetAllAnimalsInArea()
                .FirstOrDefault(animal => animal.PackID == this.PackID && animal.IsLeader && animal != this);

            if (packLeader != null)
            {
                FollowTarget = packLeader;
                stateMachine.ChangeState(new FollowingState());
            }
        }
        else if (IsHerd && !string.IsNullOrEmpty(HerdID))
        {
            if (UnityEngine.Random.value > 0.5f)
                stateMachine.ChangeState(new TrueIdleState()); // Herd animals sometimes stay still
            else
                stateMachine.ChangeState(new FleeingState()); // Flee if scared
        }
    }

}

public enum HideType
{
    LargeScales,
    SmallScales,
    HeavyFur,
    ThinFur,
    ThickHide,
    ThinHide,
    SoftFeather,
    StiffFeather,
    DenseChitin,
    LightChitin,
    FineWool,
    CoarseWool,
    Bark,
    Silk,
    Crystal,
    Metallic,
    Spiked,
    Stone,
    None
}
